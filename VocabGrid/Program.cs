using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VocabGrid.Data;
using VocabGrid.Interfaces;
using VocabGrid.Repositories;
using VocabGrid.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "VocabGrid API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Token değerinizi girin."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    // Development: open for local Flutter/web testing.
    // Production: set Cors:AllowedOrigins in config (comma-separated).
    options.AddPolicy("AppCors", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
            return;
        }

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? Array.Empty<string>();
        if (origins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => false);
            return;
        }

        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        // User üzerindeki "silinmemiş olsun" süzgeci, ona zorunlu bağlı her
        // varlık için bir uyarı üretiyor (Decks, UserSettings, UserProgress...):
        // "principal süzülürse dependent beklenmedik sonuç verebilir". Bu
        // kodda böyle bir yol yok — hiçbir sorgu bu varlıklara User
        // gezinmesinden ulaşmıyor, hepsi kimliği doğrulanmış (yani silinmemiş)
        // bir kullanıcının UserId'siyle doğrudan süzülüyor.
        //
        // Uyarı model doğrulamasında, yani her açılışta on kez basılıyordu ve
        // gerçek uyarıları görünmez hâle getiriyordu. Susturmanın bedeli:
        // ileride biri User üzerinden dependent sorgularsa derleyici uyarmaz.
        .ConfigureWarnings(warnings =>
            warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));

// UseExceptionHandler'ın gövdesiz bir 500 yerine ProblemDetails üretmesi için.
builder.Services.AddProblemDetails();

// Kimlik uç noktalarında hız sınırı. Parola ve altı haneli doğrulama kodu
// deneyen tek yollar bunlar; sınır yokken bir kod, geçerli olduğu 15 dakika
// içinde kaba kuvvetle bulunabilirdi. Bölüm anahtarı IP: e-postaya göre
// bölmek, saldırganın başkasının hesabını kilitlemesine izin verirdi.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Credentials, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // Kayıt ve posta gönderen uç noktalarda sınır daha gevşek: burada
    // korunan bir sır değil, kayıt ve e-posta kuyruğunun kötüye kullanımı.
    options.AddPolicy(RateLimitPolicies.Registration, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
});

// Pick the transport from configuration rather than the environment: a
// developer with no SMTP credentials still gets a working API (codes land in
// the database and, in Development, in the send-verification-code response),
// while anyone who fills in Smtp:* starts sending real mail immediately.
var smtpSettings = builder.Configuration.GetSection(SmtpSettings.SectionName).Get<SmtpSettings>() ?? new SmtpSettings();
if (smtpSettings.IsConfigured)
{
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, EmailService>();
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key yapılandırması eksik. User Secrets veya ortam değişkeni ile ayarlayın; hard-coded secret kullanmayın.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Says out loud whether mail actually leaves the machine — without this, a
// misconfigured Smtp section looks identical to a working one until someone
// notices no email ever arrived.
app.Logger.LogInformation(
    smtpSettings.IsConfigured
        ? "Email transport: SMTP via {Host}:{Port} as {User}."
        : "Email transport: logging stub — no Smtp:Host/User/Password configured, so no mail will be sent.",
    smtpSettings.Host,
    smtpSettings.Port,
    smtpSettings.User);

// Creates the database on first run and applies anything added since, so a
// fresh clone needs nothing but `dotnet run` — no EF CLI tool, no separate
// command. Development only: in production a schema change should be a
// deliberate, reviewed step, and two instances starting at once would race
// each other trying to apply it.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            app.Logger.LogInformation(
                "Applying {Count} pending migration(s) to {Database}.",
                pending.Count,
                db.Database.GetDbConnection().Database);
            db.Database.Migrate();
        }

        app.Logger.LogInformation("Database ready: {Database}.", db.Database.GetDbConnection().Database);
    }
    catch (Exception ex)
    {
        // Rethrown on purpose: a half-prepared database would fail later in a
        // far more confusing way than at startup.
        app.Logger.LogError(
            ex,
            "Could not prepare the database. Check that SQL Server is running and that ConnectionStrings:DefaultConnection points at it.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Yakalanmayan bir istisna aksi halde geliştirme sayfasını, yani yığın izini
// ve kaynak parçalarını istemciye gönderiyordu. Bu, ortamdan bağımsız olarak
// RFC 9457 ProblemDetails döndürür; ayrıntı sunucu günlüğünde kalır.
app.UseExceptionHandler();

app.UseCors("AppCors");

// Kimlik doğrulamadan önce: sınıra takılan istek, parola karşılaştırma
// maliyetine hiç girmeden reddedilmeli.
app.UseRateLimiter();

var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));
app.Environment.WebRootPath ??= webRoot;
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
