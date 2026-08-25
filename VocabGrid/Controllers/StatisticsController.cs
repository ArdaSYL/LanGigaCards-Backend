using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocabGrid.DTOs;
using VocabGrid.Entities;
using VocabGrid.Interfaces;
using VocabGrid.Services;

namespace VocabGrid.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public StatisticsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Özet istatistikler. <paramref name="languageCode"/> verildiğinde o
    /// hedef dilin çalışmaları sayılır — seri, XP ve seviye de o dilin kendi
    /// profilinden okunur.
    ///
    /// Parametre boş bırakılırsa kullanıcının o anki hedef diline düşülür,
    /// hesabın tamamına değil: her dil kendi izole öğrenme alanı, "dilsiz"
    /// bir görünüm hepsini birbirine karıştırırdı.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(StatisticsOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatisticsOverviewDto>> GetOverview(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var period = BuildPeriod(from, to);
        if (period is null)
        {
            return BadRequest("from must be earlier than or equal to to.");
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        var codeRaw = LanguageProgressEngine.Normalize(languageCode);
        var code = codeRaw.Length > 0 ? codeRaw : LanguageProgressEngine.Normalize(user.TargetLanguageCode);
        var nativeCode = LanguageProgressEngine.Normalize(user.NativeLanguageCode);
        var languageProfile = (await _unitOfWork.Repository<UserLanguageProfile>()
                .FindAsync(p => p.UserId == user.Id && p.NativeLanguageCode == nativeCode && p.LanguageCode == code))
            .FirstOrDefault();

        var activities = await GetActivitiesAsync(user.Id, period.Value.Start, period.Value.EndExclusive, code);
        var quizAnswers = activities
            .Where(activity => activity.ActivityType == "Quiz" && activity.Result is "Correct" or "Wrong")
            .ToList();
        var correctAnswers = quizAnswers.Count(activity => activity.Result == "Correct");
        var reviewCount = activities.Count(activity => activity.ActivityType == "Review");
        var completedLessons = (await _unitOfWork.Repository<UserProgress>()
                .FindAsync(progress => progress.UserID == user.Id && progress.Completed))
            .Count();
        // Zamanı gelen tekrarlar da dile göre süzülüyor: kartın dili
        // destesinden geliyor. Müfredat İngilizce içerik olduğu için (bkz.
        // CurriculumSeedData) destesiz kartlar yalnızca hedef dil İngilizce
        // olduğunda sayılır -- GetDueReviews'teki aynı kural burada da
        // geçerli.
        var now = DateTime.UtcNow;
        var dueReviews = await _unitOfWork.Repository<UserWordProgress>().Query()
            .Where(progress => progress.UserID == user.Id &&
                (progress.NextReviewDate == null || progress.NextReviewDate <= now) &&
                ((progress.Vocabulary.DeckId == null && code == "en") ||
                 progress.Vocabulary.Deck!.LanguageCode == code))
            .CountAsync();
        // The selected period is for the overview metrics only. Streaks must use the
        // user's full activity history, otherwise a short date filter resets them.
        var activityHistory = await _unitOfWork.Repository<StudyActivity>()
            .FindAsync(activity => activity.UserId == user.Id && activity.LanguageCode == code);
        var activityDates = activityHistory.Select(activity => activity.OccurredAt);

        return Ok(new StatisticsOverviewDto
        {
            Period = new StatisticsPeriodDto
            {
                Start = period.Value.Start,
                To = period.Value.EndExclusive.AddTicks(-1)
            },
            TotalStudySeconds = activities.Sum(activity => activity.DurationSeconds),
            TotalStudyMinutes = Math.Round(activities.Sum(activity => activity.DurationSeconds) / 60.0, 1),
            QuizAccuracyPercent = quizAnswers.Count == 0
                ? 0
                : Math.Round(correctAnswers * 100.0 / quizAnswers.Count, 1),
            QuizQuestionsAnswered = quizAnswers.Count,
            CorrectAnswers = correctAnswers,
            ReviewCount = reviewCount,
            CompletedLessons = completedLessons,
            DueReviews = dueReviews,
            CurrentStreak = StudyEngine.CalculateCurrentStreak(activityDates, DateTime.UtcNow),
            // En uzun seri, kaydedilmiş değerle hesaplananın büyüğü. Hesabın
            // toplam rekoru değil, code'un çözüldüğü dilin kendi profili
            // alınır -- yoksa yeni başlanan bir dil ilk günden hesabın en
            // uzun serisini (başka bir dilden) gösterirdi.
            LongestStreak = Math.Max(
                languageProfile?.LongestStreak ?? 0,
                StudyEngine.CalculateLongestStreak(activityDates)),
            TotalXp = languageProfile?.TotalXp ?? 0,
            Level = languageProfile?.Level ?? 1
        });
    }

    /// <summary>
    /// Isı haritası. <paramref name="languageCode"/> verildiğinde o dilin
    /// günleri sayılır; boş bırakılırsa kullanıcının o anki hedef diline
    /// düşülür, hesabın tamamına değil.
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(IEnumerable<HeatmapPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HeatmapPointDto>>> GetHeatmap(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? languageCode)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var period = BuildPeriod(from, to);
        if (period is null)
        {
            return BadRequest("from must be earlier than or equal to to.");
        }

        var code = await LanguageProgressEngine.ResolveOrDefaultAsync(_unitOfWork, userId.Value, languageCode);
        var activities = await GetActivitiesAsync(
            userId.Value,
            period.Value.Start,
            period.Value.EndExclusive,
            code);
        var byDate = activities
            .GroupBy(activity => activity.OccurredAt.Date)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    StudySeconds = group.Sum(activity => activity.DurationSeconds),
                    Reviews = group.Count(activity => activity.ActivityType == "Review"),
                    QuizAnswers = group.Count(activity => activity.ActivityType == "Quiz" && activity.Result is "Correct" or "Wrong"),
                    XpEarned = group.Sum(activity => activity.XpEarned)
                });

        var days = new List<HeatmapPointDto>();
        for (var date = period.Value.Start.Date; date < period.Value.EndExclusive.Date; date = date.AddDays(1))
        {
            byDate.TryGetValue(date, out var summary);
            days.Add(new HeatmapPointDto
            {
                Date = date,
                StudySeconds = summary?.StudySeconds ?? 0,
                Reviews = summary?.Reviews ?? 0,
                QuizAnswers = summary?.QuizAnswers ?? 0,
                XpEarned = summary?.XpEarned ?? 0
            });
        }

        return Ok(days);
    }

    /// <summary>
    /// Aralıktaki ham aktiviteler, tek bir dile ait. Çağıranlar
    /// <paramref name="languageCode"/>'u her zaman çözülmüş (boş olmayan)
    /// hâliyle verir -- bkz. LanguageProgressEngine.ResolveOrDefaultAsync.
    /// </summary>
    private async Task<List<StudyActivity>> GetActivitiesAsync(
        int userId,
        DateTime start,
        DateTime endExclusive,
        string languageCode)
    {
        return (await _unitOfWork.Repository<StudyActivity>()
                .FindAsync(activity => activity.UserId == userId &&
                    activity.OccurredAt >= start && activity.OccurredAt < endExclusive &&
                    activity.LanguageCode == languageCode))
            .OrderBy(activity => activity.OccurredAt)
            .ToList();
    }

    private static (DateTime Start, DateTime EndExclusive)? BuildPeriod(DateTime? from, DateTime? to)
    {
        var start = (from ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var end = (to ?? DateTime.UtcNow).Date;
        if (start > end)
        {
            return null;
        }

        return (start, end.AddDays(1));
    }

    private int? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
