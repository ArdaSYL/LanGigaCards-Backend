using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VocabGrid.DTOs;
using VocabGrid.Entities;
using VocabGrid.Interfaces;
using VocabGrid.Services;

namespace VocabGrid.Controllers;

/// <summary>
/// Kullanıcının öğrendiği diller ve her birinin kendi durumu.
///
/// <para>
/// <c>UserController</c> hesabın tamamına bakar — ad, e-posta, tercihler.
/// Burası ise "bu dilde neredeyim" sorusunu yanıtlar: seviye, kategori
/// seçimi, seri, XP ve en son çalışılan deste/kelime. İkisinin ayrı durması
/// gerekiyor çünkü ikinci bir dile geçmek profili değiştirmez, yeni bir
/// öğrenme başlatır.
/// </para>
///
/// <para>
/// Akış şöyle: istemci hedef dili <c>PUT switch</c> ile değiştirir, dönen
/// profilde <c>isSetupCompleted</c> false ise seviye ölçümü ve kategori
/// seçimi penceresini açar, sonucu <c>PUT {code}/setup</c> ile gönderir.
/// Kitaplık o çağrıda kurulur; istemcinin ayrıca deste yaratması gerekmez.
/// </para>
/// </summary>
[ApiController]
[Route("api/User/languages")]
[Authorize]
public class UserLanguagesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public UserLanguagesController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>Öğrenilen tüm diller, en son çalışılan başta.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LanguageProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LanguageProfileDto>>> GetMyLanguages()
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profiles = (await _unitOfWork.Repository<UserLanguageProfile>()
                .FindAsync(p => p.UserId == userId.Value))
            .OrderByDescending(p => p.LastStudiedAt ?? p.CreatedAt)
            .ToList();

        var categoriesByLanguage = await CategoryIdsByLanguageAsync(userId.Value);

        return Ok(profiles.Select(profile => MapProfile(
            profile,
            categoriesByLanguage.TryGetValue(profile.LanguageCode, out var ids) ? ids : new List<int>())));
    }

    /// <summary>
    /// Tek bir dilin durumu. Kullanıcı o dili hiç seçmediyse 404 — istemci
    /// bunu "henüz başlanmamış" olarak okur.
    /// </summary>
    [HttpGet("{languageCode}")]
    [ProducesResponseType(typeof(LanguageProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LanguageProfileDto>> GetLanguage(string languageCode)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var code = LanguageProgressEngine.Normalize(languageCode);
        var profile = (await _unitOfWork.Repository<UserLanguageProfile>()
                .FindAsync(p => p.UserId == userId.Value && p.LanguageCode == code))
            .FirstOrDefault();
        if (profile is null)
        {
            return NotFound("Language profile not found.");
        }

        return Ok(MapProfile(profile, await CategoryIdsForAsync(userId.Value, code)));
    }

    /// <summary>
    /// Hedef dili değiştirir ve o dilin profilini döndürür; profil yoksa
    /// açılır.
    ///
    /// <para>
    /// Aynı dile tekrar geçmek zararsızdır: var olan satır sıfırlanmaz, seri
    /// ve XP olduğu gibi kalır. Dönen <c>isSetupCompleted</c> false ise dil ilk
    /// kez seçilmiştir ve kurulum penceresi açılmalıdır.
    /// </para>
    ///
    /// <para>
    /// Kitaplık burada kurulmaz. Kurulum penceresi tamamlanmadan deste
    /// oluşturmak, seviyesi ve ilgi alanları henüz bilinmeyen birine rastgele
    /// bir kitaplık vermek olurdu. Kurulumu daha önce tamamlanmış bir dile geri
    /// dönüldüğünde ise eşitleme çalışır — o dilin desteleri yerli yerinde
    /// olsun diye.
    /// </para>
    /// </summary>
    [HttpPut("switch")]
    [ProducesResponseType(typeof(LanguageProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LanguageProfileDto>> SwitchTargetLanguage([FromBody] SwitchTargetLanguageDto dto)
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

        var code = LanguageProgressEngine.Normalize(dto.LanguageCode);
        if (code.Length == 0)
        {
            return BadRequest("LanguageCode is required.");
        }

        var userRepository = _unitOfWork.Repository<User>();
        var user = await userRepository.GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        // NativeLanguageCode artık öğrenilebilecek dilleri kısıtlamıyor -- o
        // alan yalnızca arayüz dilini (App Language) tutuyor, hedef dil
        // seçimiyle ilişkisi kalmadı. Bu kontrol eski eşleşmiş
        // native/target modelinden kalmıştı ve App Language'a denk gelen
        // her dile geçişi sessizce 400'lüyordu.
        var name = await ResolveLanguageNameAsync(code, dto.LanguageName);

        var profile = await LanguageProgressEngine.GetOrCreateAsync(_unitOfWork, userId.Value, code, name);
        if (profile is null)
        {
            return BadRequest("LanguageCode is required.");
        }

        user.TargetLanguageCode = code;
        user.TargetLanguage = name;
        // Hesap satırındaki seviye, o an çalışılan dilin seviyesini yansıtır;
        // asıl kayıt dil profilinde. İkisini birlikte tutmak, bu alanı hâlâ
        // okuyan eski yolların (istatistik özeti, kelime seçimi) doğru değeri
        // görmesini sağlıyor.
        user.TargetProficiencyLevel = profile.ProficiencyLevel;
        userRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        if (profile.IsSetupCompleted)
        {
            await CategoryDeckSynchronizer.SyncAsync(_unitOfWork, userId.Value);
        }

        return Ok(MapProfile(profile, await CategoryIdsForAsync(userId.Value, code)));
    }

    /// <summary>
    /// Kurulum penceresinin sonucunu kaydeder: seviye ölçümü ve kategori
    /// seçimi. Kitaplık aynı istekte kurulur.
    ///
    /// <para>
    /// Tekrar çağrılabilir — kullanıcı seviyesini sonradan değiştirdiğinde ya
    /// da kategorilerini düzenlediğinde aynı yol kullanılır. Eşitleme yalnızca
    /// eksik desteleri ekler; seviye düştüğünde hiçbir kart silinmez, çünkü o
    /// kartlarda ilerleme olabilir.
    /// </para>
    /// </summary>
    [HttpPut("{languageCode}/setup")]
    [ProducesResponseType(typeof(LanguageProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LanguageProfileDto>> CompleteSetup(
        string languageCode,
        [FromBody] CompleteLanguageSetupDto dto)
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

        var code = LanguageProgressEngine.Normalize(languageCode);
        if (code.Length == 0)
        {
            return BadRequest("LanguageCode is required.");
        }

        var requestedCategoryIds = dto.CategoryIds.Distinct().ToList();
        if (requestedCategoryIds.Count > 0)
        {
            var found = await _unitOfWork.Repository<Category>()
                .FindAsync(category => requestedCategoryIds.Contains(category.Id));
            if (found.Count() != requestedCategoryIds.Count)
            {
                return BadRequest("One or more category ids are invalid.");
            }
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        var profile = await LanguageProgressEngine.GetOrCreateAsync(
            _unitOfWork,
            userId.Value,
            code,
            await ResolveLanguageNameAsync(code, null));
        if (profile is null)
        {
            return BadRequest("LanguageCode is required.");
        }

        profile.ProficiencyLevel = dto.ProficiencyLevel.Trim();
        profile.DifficultyMode = string.IsNullOrWhiteSpace(dto.DifficultyMode)
            ? LanguageProgressEngine.DifficultyModeFor(dto.ProficiencyLevel)
            : dto.DifficultyMode.Trim();
        profile.IsSetupCompleted = true;
        if (profile.Id != 0)
        {
            _unitOfWork.Repository<UserLanguageProfile>().Update(profile);
        }

        // Seçimi bu dil için baştan yazıyoruz. Diğer dillerin satırlarına
        // dokunulmuyor — kullanıcı Japoncada "Anime", Almancada "İş" seçmiş
        // olabilir ve ikisi birbirini geçersiz kılmamalı.
        var linkRepository = _unitOfWork.Repository<UserCategory>();
        var current = await linkRepository.FindAsync(link => link.UserId == userId.Value && link.LanguageCode == code);
        foreach (var link in current)
        {
            linkRepository.Delete(link);
        }

        foreach (var categoryId in requestedCategoryIds)
        {
            await linkRepository.AddAsync(new UserCategory
            {
                UserId = userId.Value,
                CategoryId = categoryId,
                LanguageCode = code
            });
        }

        // Kurulum yapılan dil aynı zamanda o an çalışılan dilse hesap satırı da
        // güncel kalmalı: kelime seçimi ve istatistik özeti hâlâ oradan okuyor.
        if (LanguageProgressEngine.Normalize(user.TargetLanguageCode) == code)
        {
            user.TargetProficiencyLevel = profile.ProficiencyLevel;
            _unitOfWork.Repository<User>().Update(user);
        }

        await _unitOfWork.CompleteAsync();

        // Kitaplık yalnızca kurulum yapılan dil o an hedefse kurulabilir:
        // eşitleyici kullanıcının hedef diline bakarak çalışıyor.
        if (LanguageProgressEngine.Normalize(user.TargetLanguageCode) == code)
        {
            await CategoryDeckSynchronizer.SyncAsync(_unitOfWork, userId.Value);
        }

        return Ok(MapProfile(profile, await CategoryIdsForAsync(userId.Value, code)));
    }

    private async Task<string> ResolveLanguageNameAsync(string code, string? provided)
    {
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided.Trim();
        }

        var language = (await _unitOfWork.Repository<Language>().FindAsync(l => l.Code == code)).FirstOrDefault();
        return language?.Name ?? code;
    }

    private async Task<List<int>> CategoryIdsForAsync(int userId, string languageCode)
    {
        var links = await _unitOfWork.Repository<UserCategory>()
            .FindAsync(link => link.UserId == userId && link.LanguageCode == languageCode);
        return links.Select(link => link.CategoryId).Distinct().OrderBy(id => id).ToList();
    }

    private async Task<Dictionary<string, List<int>>> CategoryIdsByLanguageAsync(int userId)
    {
        var links = await _unitOfWork.Repository<UserCategory>().FindAsync(link => link.UserId == userId);
        return links
            .GroupBy(link => link.LanguageCode)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.CategoryId).Distinct().OrderBy(id => id).ToList());
    }

    private static LanguageProfileDto MapProfile(UserLanguageProfile profile, List<int> categoryIds) => new()
    {
        LanguageCode = profile.LanguageCode,
        LanguageName = profile.LanguageName,
        ProficiencyLevel = profile.ProficiencyLevel,
        DifficultyMode = profile.DifficultyMode,
        IsSetupCompleted = profile.IsSetupCompleted,
        CategoryIds = categoryIds,
        CurrentStreak = profile.CurrentStreak,
        LongestStreak = profile.LongestStreak,
        TotalXp = profile.TotalXp,
        Level = profile.Level,
        LastStudiedDeckId = profile.LastStudiedDeckId,
        LastStudiedWordId = profile.LastStudiedWordId,
        LastStudiedAt = profile.LastStudiedAt,
        CreatedAt = profile.CreatedAt
    };

    private int? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
