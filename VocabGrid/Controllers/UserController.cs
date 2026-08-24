using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VocabGrid.DTOs;
using VocabGrid.Entities;
using VocabGrid.Interfaces;
using VocabGrid.Services;

namespace VocabGrid.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public UserController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private int? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        return Ok(MapProfile(user));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto dto)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
        {
            return BadRequest("FirstName and LastName are required.");
        }

        var userRepository = _unitOfWork.Repository<User>();
        var user = await userRepository.GetByIdAsync(userId.Value);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var previousTargetCode = user.TargetLanguageCode;

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl) ? user.AvatarUrl : dto.AvatarUrl.Trim();
        user.NativeLanguage = string.IsNullOrWhiteSpace(dto.NativeLanguage) ? user.NativeLanguage : dto.NativeLanguage.Trim();
        user.TargetLanguage = string.IsNullOrWhiteSpace(dto.TargetLanguage) ? user.TargetLanguage : dto.TargetLanguage.Trim();
        // Normalize, sadece küçük harfe çevirmiyor: eski istemcilerin
        // gönderdiği bayrak kodlarını da ISO'ya taşıyor (bkz.
        // LanguageProgressEngine.Normalize).
        user.NativeLanguageCode = string.IsNullOrWhiteSpace(dto.NativeLanguageCode)
            ? user.NativeLanguageCode
            : LanguageProgressEngine.Normalize(dto.NativeLanguageCode);
        user.TargetLanguageCode = string.IsNullOrWhiteSpace(dto.TargetLanguageCode)
            ? user.TargetLanguageCode
            : LanguageProgressEngine.Normalize(dto.TargetLanguageCode);
        user.TargetProficiencyLevel = string.IsNullOrWhiteSpace(dto.TargetProficiencyLevel)
            ? user.TargetProficiencyLevel
            : dto.TargetProficiencyLevel.Trim();
        user.DailyGoalMinutes = dto.DailyGoalMinutes > 0 ? dto.DailyGoalMinutes : user.DailyGoalMinutes;

        userRepository.Update(user);

        // Hedef dilin profil satırı her koşulda var olmalı: istatistik, seri ve
        // "en son çalışılan" bilgisi oraya yazılıyor. Yeni bir dile geçildiğinde
        // satır burada açılır ve kurulumu tamamlanmamış olarak işaretlenir —
        // istemci seviye ölçümü ve kategori penceresini bu bayrağa bakarak açar.
        var languageChanged = !string.Equals(previousTargetCode, user.TargetLanguageCode, StringComparison.OrdinalIgnoreCase);
        var languageProfile = await LanguageProgressEngine.GetOrCreateAsync(
            _unitOfWork,
            userId.Value,
            user.TargetLanguageCode,
            user.TargetLanguage,
            user.TargetProficiencyLevel);

        // Seviye bu ekrandan değiştirildiyse dil profiline de işlenir; ikisi
        // ayrışırsa kelime seçimi bir değeri, ekran başka birini gösterir.
        if (languageProfile is not null && !languageChanged &&
            !string.IsNullOrWhiteSpace(dto.TargetProficiencyLevel) &&
            !string.Equals(languageProfile.ProficiencyLevel, user.TargetProficiencyLevel, StringComparison.OrdinalIgnoreCase))
        {
            languageProfile.ProficiencyLevel = user.TargetProficiencyLevel;
            languageProfile.DifficultyMode = LanguageProgressEngine.DifficultyModeFor(user.TargetProficiencyLevel);
            if (languageProfile.Id != 0)
            {
                _unitOfWork.Repository<UserLanguageProfile>().Update(languageProfile);
            }
        }

        await _unitOfWork.CompleteAsync();

        // Kitaplık yalnızca kurulumu tamamlanmış diller için eşitlenir. Yeni bir
        // dilde seviye ve ilgi alanları henüz sorulmadı; deste kurmak için
        // kurulum penceresinin sonucunu bekliyoruz
        // (<c>PUT /api/User/languages/{code}/setup</c>).
        if (languageChanged && languageProfile?.IsSetupCompleted == true)
        {
            await CategoryDeckSynchronizer.SyncAsync(_unitOfWork, userId.Value);
        }

        return Ok(new { Message = "Profile updated successfully.", Profile = MapProfile(user) });
    }

    /// <summary>
    /// Hesabı siler.
    ///
    /// <para>
    /// Silme işaretlemedir: <c>Users</c> satırı ve ona bağlı her şey —
    /// desteler, kartlar, kelime ilerlemesi, çalışma geçmişi, dil profilleri —
    /// veritabanında olduğu gibi kalır, satır yalnızca
    /// <see cref="User.IsDeleted"/> ile işaretlenir. Gerekçe
    /// <see cref="User.IsDeleted"/> üzerinde yazılı.
    /// </para>
    ///
    /// <para>
    /// İşaretlenen hesap bundan sonra hiçbir sorguda görünmez: giriş
    /// yapılamaz, elde kalmış erişim anahtarı da işe yaramaz. Yenileme
    /// anahtarı ayrıca temizleniyor — süzgeç zaten yeterli, ama kullanılamaz
    /// bir kimlik bilgisini satırda tutmanın bir nedeni yok.
    /// </para>
    ///
    /// <para>
    /// Aynı e-postayla yeniden kayıt olmak mümkün: benzersizlik indeksi
    /// yalnızca silinmemiş hesapları kapsıyor. Yeni kayıt yeni bir hesaptır;
    /// eskisinin ilerlemesini devralmaz.
    /// </para>
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var userRepository = _unitOfWork.Repository<User>();
        var user = await userRepository.GetByIdAsync(userId.Value);
        if (user is null)
        {
            // Süzgeç yüzünden zaten silinmiş bir hesap da buraya düşer; iki
            // durumu ayırmıyoruz — çağıran için sonuç aynı.
            return NotFound("User not found.");
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        userRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var settings = await GetOrCreateSettingsAsync(userId.Value);
        return Ok(MapSettings(settings));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UserSettingsDto dto)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var settingsRepository = _unitOfWork.Repository<UserSettings>();
        var settings = await GetOrCreateSettingsAsync(userId.Value);

        var previousDifficulty = settings.DifficultyMode;

        settings.DarkMode = dto.DarkMode;
        settings.DailyReminders = dto.DailyReminders;
        settings.SoundEffects = dto.SoundEffects;
        settings.ThemeColor = string.IsNullOrWhiteSpace(dto.ThemeColor) ? settings.ThemeColor : dto.ThemeColor.Trim();
        settings.TextSize = string.IsNullOrWhiteSpace(dto.TextSize) ? settings.TextSize : dto.TextSize.Trim();
        settings.DifficultyMode = string.IsNullOrWhiteSpace(dto.DifficultyMode)
            ? settings.DifficultyMode
            : dto.DifficultyMode.Trim();

        settingsRepository.Update(settings);
        await _unitOfWork.CompleteAsync();

        // Zorluk seviyesi kelime seçimini belirliyor: seviye yükseldiyse
        // kategori destelerine artık kapsama giren kartlar eklenir. Seviye
        // düştüğünde hiçbir şey silinmez — o kartlarda ilerleme olabilir.
        if (!string.Equals(previousDifficulty, settings.DifficultyMode, StringComparison.OrdinalIgnoreCase))
        {
            await CategoryDeckSynchronizer.SyncAsync(_unitOfWork, userId.Value);
        }

        return Ok(new { Message = "Settings updated successfully.", Settings = MapSettings(settings) });
    }

    /// <summary>
    /// Kullanıcının ilgi alanları. Seçim hedef dile bağlı olduğu için
    /// <paramref name="languageCode"/> verilmezse o an çalışılan dilinki döner.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetMyCategories([FromQuery] string? languageCode)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var code = await ResolveLanguageCodeAsync(userId.Value, languageCode);
        var links = await _unitOfWork.Repository<UserCategory>()
            .FindAsync(link => link.UserId == userId.Value &&
                (link.LanguageCode == code || link.LanguageCode == ""));
        var categoryIds = links.Select(link => link.CategoryId).ToHashSet();
        var categories = categoryIds.Count == 0
            ? new List<Category>()
            : (await _unitOfWork.Repository<Category>()
                    .FindAsync(category => categoryIds.Contains(category.Id)))
                .OrderBy(category => category.Id)
                .ToList();

        return Ok(categories.Select(MapCategoryDto));
    }

    /// <summary>
    /// İlgi alanı seçimini değiştirir. Seçim yalnızca
    /// <paramref name="languageCode"/> ile belirtilen dile (verilmezse o an
    /// çalışılan dile) yazılır; diğer dillerin seçimi olduğu gibi kalır.
    /// </summary>
    [HttpPut("categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> ReplaceMyCategories(
        [FromBody] ReplaceUserCategoriesDto dto,
        [FromQuery] string? languageCode)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var requestedIds = (dto.CategoryIds ?? new List<int>()).Distinct().ToList();
        if (requestedIds.Count > 0)
        {
            var existingCategories = await _unitOfWork.Repository<Category>()
                .FindAsync(category => requestedIds.Contains(category.Id));
            if (existingCategories.Count() != requestedIds.Count)
            {
                return BadRequest("One or more category ids are invalid.");
            }
        }

        var code = await ResolveLanguageCodeAsync(userId.Value, languageCode);
        var linkRepository = _unitOfWork.Repository<UserCategory>();
        // Dilsiz eski satırlar da temizleniyor: yerlerine dilli olanları
        // yazıyoruz, yoksa aynı kategori hem dilsiz hem dilli olarak durur ve
        // seçimden çıkarılan bir kategori dilsiz kopyası yüzünden geri gelir.
        var current = await linkRepository.FindAsync(link => link.UserId == userId.Value &&
            (link.LanguageCode == code || link.LanguageCode == ""));
        foreach (var link in current)
        {
            linkRepository.Delete(link);
        }

        foreach (var categoryId in requestedIds)
        {
            await linkRepository.AddAsync(new UserCategory
            {
                UserId = userId.Value,
                CategoryId = categoryId,
                LanguageCode = code
            });
        }

        await _unitOfWork.CompleteAsync();

        // Seçim kaydedildikten sonra kitaplığı ona göre kur: yeni kategorinin
        // destesi eklenir, çıkarılan kategorinin destesi dokunulmamışsa
        // kaldırılır. Kullanıcı bu ekranı kapattığında kitaplığın hazır olması
        // gerekiyor, bu yüzden arka plana atılmıyor.
        await CategoryDeckSynchronizer.SyncAsync(_unitOfWork, userId.Value);

        return await GetMyCategories(code);
    }

    [HttpGet("learning-purposes")]
    [ProducesResponseType(typeof(IEnumerable<LearningPurposeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LearningPurposeDto>>> GetMyLearningPurposes()
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var links = await _unitOfWork.Repository<UserLearningPurpose>()
            .FindAsync(link => link.UserId == userId.Value);
        var purposeIds = links.Select(link => link.LearningPurposeId).ToHashSet();
        var purposes = purposeIds.Count == 0
            ? new List<LearningPurpose>()
            : (await _unitOfWork.Repository<LearningPurpose>()
                    .FindAsync(purpose => purposeIds.Contains(purpose.Id)))
                .OrderBy(purpose => purpose.Id)
                .ToList();

        return Ok(purposes.Select(MapLearningPurposeDto));
    }

    [HttpPut("learning-purposes")]
    [ProducesResponseType(typeof(IEnumerable<LearningPurposeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LearningPurposeDto>>> ReplaceMyLearningPurposes([FromBody] ReplaceUserLearningPurposesDto dto)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var requestedIds = dto.ResolvedIds();
        if (requestedIds.Count > 0)
        {
            var existingPurposes = await _unitOfWork.Repository<LearningPurpose>()
                .FindAsync(purpose => requestedIds.Contains(purpose.Id));
            if (existingPurposes.Count() != requestedIds.Count)
            {
                return BadRequest("One or more learning purpose ids are invalid.");
            }
        }

        var linkRepository = _unitOfWork.Repository<UserLearningPurpose>();
        var current = await linkRepository.FindAsync(link => link.UserId == userId.Value);
        foreach (var link in current)
        {
            linkRepository.Delete(link);
        }

        foreach (var purposeId in requestedIds)
        {
            await linkRepository.AddAsync(new UserLearningPurpose
            {
                UserId = userId.Value,
                LearningPurposeId = purposeId
            });
        }

        await _unitOfWork.CompleteAsync();
        return await GetMyLearningPurposes();
    }

    /// <summary>
    /// İstekteki dil kodunu normalleştirir; boşsa kullanıcının o an çalıştığı
    /// hedef dile düşer. İstemcinin her kategori çağrısında dili göndermek
    /// zorunda kalmaması için var — göndermediğinde kastettiği zaten budur.
    /// </summary>
    private async Task<string> ResolveLanguageCodeAsync(int userId, string? languageCode)
    {
        var code = LanguageProgressEngine.Normalize(languageCode);
        if (code.Length > 0)
        {
            return code;
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
        return LanguageProgressEngine.Normalize(user?.TargetLanguageCode);
    }

    private async Task<UserSettings> GetOrCreateSettingsAsync(int userId)
    {
        var settingsRepository = _unitOfWork.Repository<UserSettings>();
        var existing = (await settingsRepository.FindAsync(s => s.UserId == userId)).FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new UserSettings { UserId = userId };
        await settingsRepository.AddAsync(created);
        await _unitOfWork.CompleteAsync();
        return created;
    }

    private static UserProfileDto MapProfile(User user) => new()
    {
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        AvatarUrl = user.AvatarUrl,
        NativeLanguage = user.NativeLanguage,
        TargetLanguage = user.TargetLanguage,
        NativeLanguageCode = user.NativeLanguageCode,
        TargetLanguageCode = user.TargetLanguageCode,
        TargetProficiencyLevel = user.TargetProficiencyLevel,
        DailyGoalMinutes = user.DailyGoalMinutes,
        CurrentStreak = user.CurrentStreak,
        LongestStreak = user.LongestStreak,
        Level = user.Level,
        TotalXp = user.TotalXp,
        IsPremium = user.IsPremium,
        IsEmailVerified = user.IsEmailVerified
    };

    private static CategoryDto MapCategoryDto(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        IconName = category.IconName,
        ColorHex = category.ColorHex
    };

    private static LearningPurposeDto MapLearningPurposeDto(LearningPurpose purpose) => new()
    {
        Id = purpose.Id,
        Name = purpose.Name,
        Description = purpose.Description
    };

    private static UserSettingsDto MapSettings(UserSettings settings) => new()
    {
        DarkMode = settings.DarkMode,
        DailyReminders = settings.DailyReminders,
        SoundEffects = settings.SoundEffects,
        ThemeColor = settings.ThemeColor,
        TextSize = settings.TextSize,
        DifficultyMode = settings.DifficultyMode
    };
}
