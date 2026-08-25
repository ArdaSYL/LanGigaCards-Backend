using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using VocabGrid.DTOs;
using VocabGrid.Entities;
using VocabGrid.Interfaces;
using VocabGrid.Services;

namespace VocabGrid.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string AppleOpenIdConfigurationUrl =
        "https://appleid.apple.com/.well-known/openid-configuration";
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Deliberately much shorter than <see cref="PasswordResetLifetime"/>: a
    /// 6-digit code is far weaker than a 64-byte token, so it must not stay
    /// guessable for an hour.
    /// </summary>
    private static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromMinutes(15);

    private const int MaxVerificationAttempts = 5;

    private static readonly ConfigurationManager<OpenIdConnectConfiguration> AppleConfigurationManager =
        new(
            AppleOpenIdConfigurationUrl,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });

    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IHostEnvironment _environment;

    public AuthController(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IEmailService emailService,
        IHostEnvironment environment)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _emailService = emailService;
        _environment = environment;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Registration)]
    public async Task<IActionResult> Register([FromBody] RegisterDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userRepository = _unitOfWork.Repository<User>();

        var existingUser = await userRepository.FindAsync(u => u.Email.ToLower() == request.Email.ToLower());
        if (existingUser.Any())
        {
            return BadRequest("User with this email already exists.");
        }

        CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Username = request.Email.Split('@')[0],
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Settings = new UserSettings()
        };

        await userRepository.AddAsync(user);
        await IssueRefreshTokenAsync(user);
        await _unitOfWork.CompleteAsync();

        // The client moves straight to its "Verify Your Email" step after this
        // response, so a code has to be waiting by the time it gets there.
        // CompleteAsync above has to run first — the token needs user.Id.
        var verificationCode = await IssueEmailVerificationCodeAsync(user);
        await _unitOfWork.CompleteAsync();
        await _emailService.SendEmailVerificationCodeAsync(user.Email, verificationCode);

        return Ok(BuildAuthResponse("Registration successful.", user));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<IActionResult> Login([FromBody] UserLoginDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and Password are required.");
        }

        var userRepository = _unitOfWork.Repository<User>();
        var users = await userRepository.FindAsync(u => u.Email.ToLower() == request.Email.Trim().ToLowerInvariant());
        var user = users.FirstOrDefault();

        if (user == null || !VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            // Normal (non-deleted) lookup found nothing usable -- check
            // separately whether this is a deactivated account with matching
            // credentials, so a real owner gets pointed at /reactivate instead
            // of the same "wrong password" message a stranger would see. Only
            // checked after the password already failed to match an active
            // account, and only reveals deactivation once the password itself
            // is verified, so a bare email guess still can't tell an active
            // account from a deactivated one.
            var deactivatedUser = await FindDeactivatedUserAsync(request.Email, request.Password);
            if (deactivatedUser is not null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "This account is deactivated. Reactivate it to sign back in.",
                    isDeactivated = true
                });
            }

            return Unauthorized("Invalid credentials.");
        }

        await IssueRefreshTokenAsync(user);
        userRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        return Ok(BuildAuthResponse("Login successful.", user));
    }

    /// <summary>
    /// Brings a soft-deleted account back: <see cref="UserController.DeleteAccount"/>
    /// only ever sets <see cref="User.IsDeleted"/>, it never removes the row,
    /// so nothing here needs to reconstruct data -- flipping the flag is
    /// enough to restore full access to everything that was there before.
    /// </summary>
    [HttpPost("reactivate")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<IActionResult> Reactivate([FromBody] UserLoginDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and Password are required.");
        }

        var user = await FindDeactivatedUserAsync(request.Email, request.Password);
        if (user is null)
        {
            return Unauthorized("Invalid credentials.");
        }

        user.IsDeleted = false;
        user.DeletedAt = null;

        var userRepository = _unitOfWork.Repository<User>();
        await IssueRefreshTokenAsync(user);
        userRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        return Ok(BuildAuthResponse("Account reactivated.", user));
    }

    /// <summary>
    /// The global query filter on <see cref="User"/> excludes deactivated
    /// rows from every normal lookup (see AppDbContext), so this bypasses it
    /// deliberately -- callers only use this after a normal lookup already
    /// failed, to tell "deactivated, right password" apart from "wrong
    /// password" or "no such account".
    /// </summary>
    private async Task<User?> FindDeactivatedUserAsync(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var candidate = await _unitOfWork.Repository<User>().Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.IsDeleted && u.Email.ToLower() == normalizedEmail);

        return candidate is not null && VerifyPasswordHash(password, candidate.PasswordHash, candidate.PasswordSalt)
            ? candidate
            : null;
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token is required.");
        }

        var userRepository = _unitOfWork.Repository<User>();
        var users = await userRepository.FindAsync(u => u.RefreshToken == request.RefreshToken);
        var user = users.FirstOrDefault();

        if (user is null || user.RefreshTokenExpiryTime is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Unauthorized("Invalid or expired refresh token.");
        }

        await IssueRefreshTokenAsync(user);
        userRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        return Ok(new
        {
            Token = CreateToken(user),
            RefreshToken = user.RefreshToken,
            RefreshTokenExpiryTime = user.RefreshTokenExpiryTime
        });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.Registration)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = (await _unitOfWork.Repository<User>()
            .FindAsync(u => u.Email.ToLower() == normalizedEmail)).FirstOrDefault();

        // Always return the same client message (anti-enumeration).
        const string clientMessage = "If an account exists, a password reset token has been generated.";

        if (user is null)
        {
            return Ok(new { Message = clientMessage });
        }

        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenEntity = new PasswordResetToken
        {
            UserId = user.Id,
            Token = resetToken,
            ExpiresAt = DateTime.UtcNow.Add(PasswordResetLifetime),
            IsUsed = false
        };

        await _unitOfWork.Repository<PasswordResetToken>().AddAsync(tokenEntity);
        await _unitOfWork.CompleteAsync();
        await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken);

        if (_environment.IsDevelopment())
        {
            // Swagger/local testing only — never expose tokens outside Development.
            return Ok(new { Message = clientMessage, DevResetToken = resetToken });
        }

        return Ok(new { Message = clientMessage });
    }

    [HttpPost("send-verification-code")]
    [EnableRateLimiting(RateLimitPolicies.Registration)]
    public async Task<IActionResult> SendEmailVerificationCode([FromBody] SendEmailVerificationDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = (await _unitOfWork.Repository<User>()
            .FindAsync(u => u.Email.ToLower() == normalizedEmail)).FirstOrDefault();

        // Same anti-enumeration stance as forgot-password: the reply must not
        // reveal whether the address is registered, or whether it is already
        // verified.
        const string clientMessage = "If an account exists, a verification code has been sent.";

        if (user is null || user.IsEmailVerified)
        {
            return Ok(new { Message = clientMessage });
        }

        var code = await IssueEmailVerificationCodeAsync(user);
        await _unitOfWork.CompleteAsync();
        await _emailService.SendEmailVerificationCodeAsync(user.Email, code);

        if (_environment.IsDevelopment())
        {
            // Swagger/local testing only — never expose codes outside Development.
            return Ok(new { Message = clientMessage, DevVerificationCode = code });
        }

        return Ok(new { Message = clientMessage });
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // One message for "no such user", "no live code" and "wrong code" alike,
        // so the endpoint can't be used to probe which addresses exist.
        const string failureMessage = "Invalid or expired verification code.";

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var userRepository = _unitOfWork.Repository<User>();
        var user = (await userRepository.FindAsync(u => u.Email.ToLower() == normalizedEmail)).FirstOrDefault();

        if (user is null)
        {
            return BadRequest(failureMessage);
        }

        if (user.IsEmailVerified)
        {
            // Idempotent: re-verifying is a no-op success, not an error, so a
            // retried request can't strand the client.
            return Ok(new { Message = "Email is already verified.", user.Email, user.IsEmailVerified });
        }

        var tokenRepository = _unitOfWork.Repository<EmailVerificationToken>();
        var tokenEntity = (await tokenRepository.FindAsync(t =>
            t.UserId == user.Id &&
            !t.IsUsed &&
            t.ExpiresAt > DateTime.UtcNow)).FirstOrDefault();

        if (tokenEntity is null)
        {
            return BadRequest(failureMessage);
        }

        if (tokenEntity.AttemptCount >= MaxVerificationAttempts)
        {
            // Burn the code rather than just rejecting this attempt: otherwise
            // the budget would reset the moment the caller guessed correctly.
            tokenEntity.IsUsed = true;
            tokenRepository.Update(tokenEntity);
            await _unitOfWork.CompleteAsync();
            return BadRequest("Too many attempts. Request a new verification code.");
        }

        var expected = Encoding.UTF8.GetBytes(tokenEntity.Code);
        var supplied = Encoding.UTF8.GetBytes(request.Code.Trim());
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            tokenEntity.AttemptCount++;
            tokenRepository.Update(tokenEntity);
            await _unitOfWork.CompleteAsync();
            return BadRequest(failureMessage);
        }

        tokenEntity.IsUsed = true;
        user.IsEmailVerified = true;
        tokenRepository.Update(tokenEntity);
        userRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        return Ok(new { Message = "Email verified successfully.", user.Email, user.IsEmailVerified });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tokenRepository = _unitOfWork.Repository<PasswordResetToken>();
        var tokenEntity = (await tokenRepository.FindAsync(t =>
            t.Token == request.Token &&
            !t.IsUsed &&
            t.ExpiresAt > DateTime.UtcNow)).FirstOrDefault();

        if (tokenEntity is null)
        {
            return BadRequest("Invalid, expired, or already used password reset token.");
        }

        var userRepository = _unitOfWork.Repository<User>();
        var user = await userRepository.GetByIdAsync(tokenEntity.UserId);
        if (user is null)
        {
            return BadRequest("Invalid, expired, or already used password reset token.");
        }

        CreatePasswordHash(request.NewPassword, out byte[] passwordHash, out byte[] passwordSalt);
        user.PasswordHash = passwordHash;
        user.PasswordSalt = passwordSalt;
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        tokenEntity.IsUsed = true;

        userRepository.Update(user);
        tokenRepository.Update(tokenEntity);
        await _unitOfWork.CompleteAsync();

        return Ok(new { Message = "Password has been successfully reset." });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            return BadRequest("Google IdToken is required.");
        }

        var clientId = _configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Google authentication is not configured. Set Authentication:Google:ClientId.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                dto.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized("Invalid Google IdToken.");
        }

        var googleId = payload.Subject;
        var email = payload.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Google account email is required.");
        }

        var displayName = payload.Name ?? string.Empty;
        var user = await FindOrLinkSocialUserAsync(
            googleId: googleId,
            appleId: null,
            email: email,
            displayName: displayName);

        await IssueRefreshTokenAsync(user);
        _unitOfWork.Repository<User>().Update(user);
        await _unitOfWork.CompleteAsync();

        return Ok(BuildAuthResponse("Google authentication successful.", user, includeGoogleId: true));
    }

    [HttpPost("apple")]
    public async Task<IActionResult> AppleAuth([FromBody] AppleAuthDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            return BadRequest("Apple IdToken is required.");
        }

        var clientId = _configuration["Authentication:Apple:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Apple authentication is not configured. Set Authentication:Apple:ClientId.");
        }

        ClaimsPrincipal principal;
        try
        {
            principal = await ValidateAppleIdTokenAsync(dto.IdToken, clientId);
        }
        catch (SecurityTokenException)
        {
            return Unauthorized("Invalid Apple IdToken.");
        }
        catch (Exception)
        {
            return Unauthorized("Apple IdToken validation failed.");
        }

        var appleId = principal.FindFirstValue("sub")
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = (principal.FindFirstValue(ClaimTypes.Email)
                     ?? principal.FindFirstValue("email"))
                    ?.Trim()
                    .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(appleId))
        {
            return Unauthorized("Apple subject claim is missing.");
        }

        var userRepository = _unitOfWork.Repository<User>();
        var existingByApple = (await userRepository.FindAsync(u => u.AppleId == appleId)).FirstOrDefault();
        if (existingByApple == null && string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Apple account email is required for first-time sign-in.");
        }

        var displayName = string.IsNullOrWhiteSpace(dto.Name) ? string.Empty : dto.Name.Trim();
        var user = await FindOrLinkSocialUserAsync(
            googleId: null,
            appleId: appleId,
            email: email ?? existingByApple!.Email,
            displayName: displayName);

        await IssueRefreshTokenAsync(user);
        userRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        return Ok(BuildAuthResponse("Apple authentication successful.", user, includeAppleId: true));
    }

    private async Task<User> FindOrLinkSocialUserAsync(
        string? googleId,
        string? appleId,
        string email,
        string displayName)
    {
        var userRepository = _unitOfWork.Repository<User>();
        User? user = null;

        if (!string.IsNullOrWhiteSpace(googleId))
        {
            user = (await userRepository.FindAsync(u => u.GoogleId == googleId)).FirstOrDefault();
        }
        else if (!string.IsNullOrWhiteSpace(appleId))
        {
            user = (await userRepository.FindAsync(u => u.AppleId == appleId)).FirstOrDefault();
        }

        if (user == null)
        {
            user = (await userRepository.FindAsync(u => u.Email.ToLower() == email.ToLower())).FirstOrDefault();
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(googleId))
                {
                    user.GoogleId = googleId;
                }

                if (!string.IsNullOrWhiteSpace(appleId))
                {
                    user.AppleId = appleId;
                }

                userRepository.Update(user);
                await _unitOfWork.CompleteAsync();
            }
        }

        if (user == null)
        {
            var (firstName, lastName) = SplitDisplayName(displayName, email);
            user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Username = email.Split('@')[0],
                Email = email.ToLowerInvariant(),
                GoogleId = googleId,
                AppleId = appleId,
                PasswordHash = Array.Empty<byte>(),
                PasswordSalt = Array.Empty<byte>(),
                Settings = new UserSettings()
            };

            await userRepository.AddAsync(user);
            await _unitOfWork.CompleteAsync();
        }

        return user;
    }

    private Task IssueRefreshTokenAsync(User user)
    {
        user.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshTokenLifetime);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retires any code the user still has outstanding and issues a fresh one.
    /// The caller is responsible for the surrounding <c>CompleteAsync</c> —
    /// register needs the user persisted first, so saving is left to it.
    /// </summary>
    private async Task<string> IssueEmailVerificationCodeAsync(User user)
    {
        var repository = _unitOfWork.Repository<EmailVerificationToken>();

        // Only one code may be live at a time. Without this, every "resend"
        // would leave the previously mailed codes valid too, multiplying the
        // number of values an attacker can guess against.
        var outstanding = await repository.FindAsync(t => t.UserId == user.Id && !t.IsUsed);
        foreach (var token in outstanding)
        {
            token.IsUsed = true;
            repository.Update(token);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await repository.AddAsync(new EmailVerificationToken
        {
            UserId = user.Id,
            Code = code,
            ExpiresAt = DateTime.UtcNow.Add(EmailVerificationLifetime),
            IsUsed = false
        });

        return code;
    }

    private object BuildAuthResponse(
        string message,
        User user,
        bool includeGoogleId = false,
        bool includeAppleId = false)
    {
        object userPayload = includeGoogleId
            ? new { user.Id, user.FirstName, user.LastName, user.Username, user.Email, user.IsEmailVerified, user.GoogleId }
            : includeAppleId
                ? new { user.Id, user.FirstName, user.LastName, user.Username, user.Email, user.IsEmailVerified, user.AppleId }
                : new { user.Id, user.FirstName, user.LastName, user.Username, user.Email, user.IsEmailVerified };

        return new
        {
            Message = message,
            Token = CreateToken(user),
            RefreshToken = user.RefreshToken,
            RefreshTokenExpiryTime = user.RefreshTokenExpiryTime,
            User = userPayload
        };
    }

    private static (string FirstName, string LastName) SplitDisplayName(string displayName, string email)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return (email.Split('@')[0], "User");
        }

        var parts = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return (parts[0], "User");
        }

        return (parts[0], parts[1]);
    }

    private static async Task<ClaimsPrincipal> ValidateAppleIdTokenAsync(string idToken, string clientId)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(idToken))
        {
            throw new SecurityTokenException("Apple IdToken is not a readable JWT.");
        }

        var appleConfig = await AppleConfigurationManager.GetConfigurationAsync(CancellationToken.None);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AppleIssuer,
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = appleConfig.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        return handler.ValidateToken(idToken, parameters, out _);
    }

    private string CreateToken(User user)
    {
        var keySecret = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(keySecret))
        {
            throw new InvalidOperationException(
                "Jwt:Key is missing. Configure User Secrets or environment variables.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
            new Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
            new Claim(ClaimTypes.Surname, user.LastName ?? string.Empty)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keySecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = creds,
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using var hmac = new HMACSHA512();
        passwordSalt = hmac.Key;
        passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    private static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
    {
        if (passwordHash.Length == 0 || passwordSalt.Length == 0)
        {
            return false;
        }

        using var hmac = new HMACSHA512(passwordSalt);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return computedHash.SequenceEqual(passwordHash);
    }
}
