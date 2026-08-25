using VocabGrid.Entities;
using VocabGrid.Interfaces;

namespace VocabGrid.Services;

/// <summary>
/// Ham çalışma aktivitesini günlük özete işler.
///
/// Her aktivite iki yere yazılır: ayrıntının durduğu <see cref="StudyActivity"/>
/// ve gün başına tek satır tutan <see cref="DailyStudySummary"/>. İkincisi
/// türetilmiş veridir — istatistik ekranının bir yıllık ham aktiviteyi taramak
/// zorunda kalmaması için var.
///
/// Bir aktivite kaydedilirken bu çağrılmazsa özet o gün için eksik kalır; ham
/// veri yerinde durduğu için sonuçlar yeniden hesaplanabilir, ama ekran o güne
/// kadar yanlış gösterir. Bu yüzden çağrı, aktivitenin eklendiği yerin hemen
/// yanında durur.
///
/// Satır gün başına değil <em>gün + dil</em> başına tutulur: istatistik dil
/// bazına ayrıldıktan sonra aynı günün Almanca ve Japonca çalışması aynı
/// kutuda birikemez.
/// </summary>
internal static class DailySummaryEngine
{
    /// <summary>
    /// Aynı isteğe ait birden çok aktiviteyi işler.
    ///
    /// Tek tek <see cref="RecordAsync"/> çağırmak burada işe yaramaz: özet
    /// satırı veritabanından okunuyor ve henüz kaydedilmemiş bir satırı
    /// göremiyor, dolayısıyla her çağrı aynı gün için bir satır daha eklemeye
    /// çalışır ve benzersizlik kısıtına çarpar. Toplu giriş satırı bir kez
    /// bulur, hepsini onun üzerine işler.
    /// </summary>
    internal static async Task RecordManyAsync(IUnitOfWork unitOfWork, IReadOnlyList<StudyActivity> activities)
    {
        foreach (var group in activities.GroupBy(activity => new
                 {
                     Day = DateOnly.FromDateTime(activity.OccurredAt),
                     Language = LanguageProgressEngine.Normalize(activity.LanguageCode)
                 }))
        {
            var summary = await GetOrCreateAsync(unitOfWork, group.First().UserId, group.Key.Language, group.Key.Day);
            foreach (var activity in group)
            {
                Apply(summary, activity);
            }

            unitOfWork.Repository<DailyStudySummary>().Update(summary);
        }
    }

    internal static async Task RecordAsync(IUnitOfWork unitOfWork, StudyActivity activity)
    {
        var summary = await GetOrCreateAsync(
            unitOfWork,
            activity.UserId,
            LanguageProgressEngine.Normalize(activity.LanguageCode),
            DateOnly.FromDateTime(activity.OccurredAt));

        Apply(summary, activity);
        unitOfWork.Repository<DailyStudySummary>().Update(summary);
    }

    /// <summary>
    /// Resolves via <see cref="ConcurrentSingleton"/>, which persists a
    /// genuinely new row immediately rather than leaving it for the
    /// caller's own later SaveChanges -- so by the time this returns, the
    /// row always already exists in the database (real key, tracked as
    /// Unchanged, not Added), whether it was found or just created. Callers
    /// can always safely call Update() on the result.
    /// </summary>
    private static Task<DailyStudySummary> GetOrCreateAsync(
        IUnitOfWork unitOfWork,
        int userId,
        string languageCode,
        DateOnly day)
    {
        var repository = unitOfWork.Repository<DailyStudySummary>();
        return ConcurrentSingleton.GetOrCreateAsync(
            unitOfWork,
            find: async () => (await repository.FindAsync(s =>
                    s.UserId == userId && s.LanguageCode == languageCode && s.Day == day))
                .FirstOrDefault(),
            create: () => new DailyStudySummary { UserId = userId, LanguageCode = languageCode, Day = day });
    }

    private static void Apply(DailyStudySummary summary, StudyActivity activity)
    {
        switch (activity.ActivityType)
        {
            case "Review":
                summary.ReviewCount++;
                // "Again" tekrar görülmesi gereken kart demek; isabet sayısına
                // girmemeli. Diğer üç değerlendirme (Hard/Medium/Easy) hepsi
                // hatırlandı anlamına gelir.
                if (activity.Result is not null && activity.Result != "Again")
                {
                    summary.CorrectCount++;
                }
                break;

            case "Quiz":
                summary.QuizCount++;
                break;

            case "Lesson":
                summary.LessonCount++;
                break;
        }

        summary.StudySeconds += activity.DurationSeconds;
        summary.XpEarned += activity.XpEarned;
        summary.UpdatedAt = DateTime.UtcNow;
    }
}
