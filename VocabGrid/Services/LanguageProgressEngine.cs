using Microsoft.EntityFrameworkCore;
using VocabGrid.Entities;
using VocabGrid.Interfaces;

namespace VocabGrid.Services;

/// <summary>
/// Dil bazlı öğrenme durumunu tutan tek yer: profil satırının açılması, o
/// dildeki seri/XP/seviye sayaçları ve "en son çalışılan deste/kelime".
///
/// <para>
/// <see cref="StudyEngine"/> aynı işi hesabın tamamı için yapar ve öyle
/// kalır — rozetler, "toplam XP" ve hesap ömrü boyunca en uzun seri hâlâ
/// tüm dilleri birlikte sayar. Buradaki sayaçlar tek bir dile aittir;
/// ikisi aynı aktiviteden beslenir ama farklı soruları yanıtlar.
/// </para>
///
/// <para>
/// Her çalışma aktivitesinin yanında <see cref="RecordAsync"/> çağrılır.
/// Çağrılmazsa ham aktivite yine yazılır, yalnızca o dilin özeti geride
/// kalır — aktiviteler durduğu için sonradan yeniden hesaplanabilir.
/// </para>
/// </summary>
internal static class LanguageProgressEngine
{
    /// <summary>
    /// Dil kodunu tek biçime indirger. Karşılaştırmaların hepsi bu çıktı
    /// üzerinden yapılıyor: istemci kimi yerde <c>DE</c>, kimi yerde
    /// <c>de</c> gönderiyor ve ikisi aynı dil.
    ///
    /// <para>
    /// Bayrak kodları da burada ISO'ya çevriliyor. İstemcinin dil seçicisi
    /// ülke bayrağı kodlarıyla anahtarlanmış (<c>GB</c>, <c>JP</c>,
    /// <c>KR</c>, <c>CN</c>) ve onda altısı ISO diliyle çakıştığı için sorun
    /// yıllarca görünmedi; kalan dördü sunucuya <c>gb</c> gibi katalogda
    /// karşılığı olmayan bir kod yazıyordu. İstemci düzeltildi, ama güncel
    /// olmayan bir sürüm hâlâ eskisini gönderecek ve ilerleme dil başına
    /// tutulduğu için "gb İngilizcesi" ile "en İngilizcesi" iki ayrı dil
    /// sayılırdı. Çeviri burada, tek geçitte.
    /// </para>
    ///
    /// <para>
    /// Çakışma riski yok: <c>gb</c>, <c>jp</c>, <c>kr</c> ve <c>cn</c> ISO
    /// 639-1'de bir dil kodu değil.
    /// </para>
    /// </summary>
    internal static string Normalize(string? code)
    {
        var trimmed = (code ?? string.Empty).Trim().ToLowerInvariant();
        return trimmed switch
        {
            "gb" => "en",
            "jp" => "ja",
            "kr" => "ko",
            "cn" => "zh",
            _ => trimmed,
        };
    }

    /// <summary>
    /// Normalizes [languageCode], falling back to the caller's own current
    /// target language when it's not given.
    ///
    /// Several list/summary endpoints (due reviews, streak, daily summary,
    /// statistics overview/heatmap, deck list) take an optional languageCode
    /// query parameter, but historically treated "not given" as "no filter
    /// at all" rather than "my current language" -- every one of these
    /// endpoints then silently mixed every language the user had ever
    /// studied into one list/count, because the app's own client never
    /// actually sends this parameter. Each language a learner studies is
    /// meant to be its own isolated space (own decks, own review queue, own
    /// streak, own stats); "not specified" has to mean "whichever language
    /// I'm currently in", not "all of them at once". Call this instead of
    /// bare <see cref="Normalize"/> wherever an omitted languageCode should
    /// resolve to the current session's language rather than disable
    /// filtering.
    /// </summary>
    internal static async Task<string> ResolveOrDefaultAsync(IUnitOfWork unitOfWork, int userId, string? languageCode)
    {
        var code = Normalize(languageCode);
        if (code.Length > 0)
        {
            return code;
        }

        var user = await unitOfWork.Repository<User>().GetByIdAsync(userId);
        return Normalize(user?.TargetLanguageCode);
    }

    /// <summary>
    /// Kullanıcının o hedef dildeki profilini getirir, yoksa oluşturur.
    ///
    /// Yeni satır <see cref="UserLanguageProfile.IsSetupCompleted"/> false
    /// ile açılır: dil ilk kez hedef seçilmiştir ve istemcinin seviye ölçümü
    /// ile kategori seçimini sorması gerekir. Var olan satır asla
    /// sıfırlanmaz — öğrenen eski bir dile geri döndüğünde serisi ve XP'si
    /// yerindedir.
    /// </summary>
    internal static async Task<UserLanguageProfile?> GetOrCreateAsync(
        IUnitOfWork unitOfWork,
        int userId,
        string? languageCode,
        string? languageName = null,
        string? proficiencyLevel = null)
    {
        var code = Normalize(languageCode);
        if (code.Length == 0)
        {
            return null;
        }

        var repository = unitOfWork.Repository<UserLanguageProfile>();
        var profile = await ConcurrentSingleton.GetOrCreateAsync(
            unitOfWork,
            find: async () => (await repository.FindAsync(p => p.UserId == userId && p.LanguageCode == code))
                .FirstOrDefault(),
            create: () => new UserLanguageProfile
            {
                UserId = userId,
                LanguageCode = code,
                LanguageName = (languageName ?? string.Empty).Trim(),
                ProficiencyLevel = string.IsNullOrWhiteSpace(proficiencyLevel) ? "Beginner" : proficiencyLevel.Trim(),
                DifficultyMode = DifficultyModeFor(proficiencyLevel),
                IsSetupCompleted = false
            });

        // Ad boş kalmış eski satırlar (geçiş sırasında dil adı bilinmiyordu)
        // ilk fırsatta doldurulur. Yeni oluşturulan satırda zaten dolu, bu
        // dal yalnızca önceden var olan satırlar için anlamlı.
        if (string.IsNullOrWhiteSpace(profile.LanguageName) && !string.IsNullOrWhiteSpace(languageName))
        {
            profile.LanguageName = languageName.Trim();
            repository.Update(profile);
        }

        return profile;
    }

    /// <summary>
    /// Onboarding'in dört seviyesini kelime seçiminin kullandığı CEFR
    /// tavanına çevirir. <see cref="CategoryDeckSynchronizer"/> aynı eşleşmeyi
    /// kendi içinde yapıyordu; buradaki, yeni bir dil kurulurken tavanın
    /// baştan doğru yazılması için.
    /// </summary>
    internal static string DifficultyModeFor(string? proficiencyLevel) =>
        (proficiencyLevel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "just starting" => "A1",
            "beginner" => "A2",
            "intermediate" => "B2",
            "advanced" => "C2",
            "fluent" => "C2",
            _ => "B1",
        };

    /// <summary>
    /// Bir çalışma aktivitesini o dilin profiline işler: XP, seviye, seri ve
    /// en son çalışılan deste/kelime.
    ///
    /// Aktivitenin <see cref="StudyActivity.LanguageCode"/> alanı boşsa hiçbir
    /// şey yapılmaz — hangi dile yazılacağı bilinmeyen bir aktiviteyi rastgele
    /// bir profile eklemektense o dilin özetini eksik bırakmak yeğdir.
    /// </summary>
    internal static Task RecordAsync(IUnitOfWork unitOfWork, StudyActivity activity, string? languageName = null) =>
        RecordManyAsync(unitOfWork, new[] { activity }, languageName);

    /// <summary>
    /// Aynı isteğe ait birden çok aktiviteyi tek geçişte işler.
    ///
    /// Tek tek <see cref="RecordAsync"/> çağırmak burada işe yaramaz: profil
    /// satırı veritabanından okunuyor ve henüz kaydedilmemiş bir satırı
    /// göremiyor — her çağrı aynı dil için bir satır daha eklemeye çalışır ve
    /// benzersizlik kısıtına çarpar. Ayrıca seri hesabı her seferinde tüm
    /// aktivite geçmişini okuyor; beş soruluk bir quiz için beş kez yapılması
    /// gereksiz.
    /// </summary>
    internal static async Task RecordManyAsync(
        IUnitOfWork unitOfWork,
        IReadOnlyList<StudyActivity> activities,
        string? languageName = null)
    {
        foreach (var group in activities.GroupBy(a => Normalize(a.LanguageCode)))
        {
            if (group.Key.Length == 0)
            {
                continue;
            }

            await RecordGroupAsync(unitOfWork, group.Key, group.ToList(), languageName);
        }
    }

    private static async Task RecordGroupAsync(
        IUnitOfWork unitOfWork,
        string code,
        IReadOnlyList<StudyActivity> activities,
        string? languageName)
    {
        var userId = activities[0].UserId;
        var latest = activities.OrderBy(a => a.OccurredAt).Last();

        var profile = await GetOrCreateAsync(unitOfWork, userId, code, languageName);
        if (profile is null)
        {
            return;
        }

        profile.TotalXp = Math.Max(0, profile.TotalXp + activities.Sum(a => a.XpEarned));
        profile.Level = Math.Max(1, profile.TotalXp / StudyEngine.XpPerLevel + 1);

        // Seri, o dildeki aktivite günlerinden hesaplanır. Sorgu aktivite
        // tablosuna gider çünkü profilde gün listesi tutulmuyor; indeks
        // (UserId, LanguageCode, OccurredAt) tam bu erişim için var.
        var days = await unitOfWork.Repository<StudyActivity>().Query()
            .Where(row => row.UserId == userId && row.LanguageCode == code)
            .Select(row => row.OccurredAt)
            .ToListAsync();

        var dates = days.Concat(activities.Select(a => a.OccurredAt)).ToList();
        profile.CurrentStreak = StudyEngine.CalculateCurrentStreak(dates, latest.OccurredAt);
        profile.LongestStreak = Math.Max(profile.LongestStreak, StudyEngine.CalculateLongestStreak(dates));

        // "En son çalışılan" yalnızca ileri gider: kuyrukta bekleyip geç ulaşan
        // eski bir aktivite, ana ekrandaki kartı geçmişe çekmemeli.
        if (profile.LastStudiedAt is null || latest.OccurredAt >= profile.LastStudiedAt)
        {
            profile.LastStudiedAt = latest.OccurredAt;

            // Deste ve kelime, hepsi arasından en son taşıyandan alınıyor:
            // bir quizin son sorusu deste bilgisi taşımayabilir ama aynı
            // oturumun önceki soruları taşır.
            var lastWithDeck = activities.Where(a => a.DeckId is not null).OrderBy(a => a.OccurredAt).LastOrDefault();
            if (lastWithDeck is not null)
            {
                profile.LastStudiedDeckId = lastWithDeck.DeckId;
            }

            var lastWithWord = activities.Where(a => a.WordId is not null).OrderBy(a => a.OccurredAt).LastOrDefault();
            if (lastWithWord is not null)
            {
                profile.LastStudiedWordId = lastWithWord.WordId;
            }
        }

        // Yeni eklenen satır hâlâ Added durumunda; üzerinde Update çağırmak
        // geçici anahtar yüzünden hata verir (bkz. DailySummaryEngine).
        if (profile.Id != 0)
        {
            unitOfWork.Repository<UserLanguageProfile>().Update(profile);
        }
    }

    /// <summary>
    /// Bir kartın hangi dile ait olduğunu bulur: kart bir destedeyse destenin
    /// dili, değilse (paylaşılan müfredat kartı) kullanıcının o anki hedef
    /// dili. Deste dili boşsa da aynı geri dönüş uygulanır — dil alanı
    /// eklenmeden önce kurulmuş destelerin kodu yok.
    /// </summary>
    internal static async Task<string> ResolveLanguageAsync(IUnitOfWork unitOfWork, Vocabulary word, User user)
    {
        if (word.DeckId is not null)
        {
            var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(word.DeckId.Value);
            var deckCode = Normalize(deck?.LanguageCode);
            if (deckCode.Length > 0)
            {
                return deckCode;
            }
        }

        return Normalize(user.TargetLanguageCode);
    }
}
