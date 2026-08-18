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
public class ProgressController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public ProgressController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("lessons")]
    public async Task<IActionResult> GetLessonProgress()
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var lessons = await _unitOfWork.Repository<Lesson>().GetAllAsync();
        var progressByLesson = (await _unitOfWork.Repository<UserProgress>()
                .FindAsync(progress => progress.UserID == userId.Value))
            .ToDictionary(progress => progress.LessonID);

        return Ok(lessons.OrderBy(lesson => lesson.OrderIndex).Select(lesson =>
        {
            progressByLesson.TryGetValue(lesson.LessonID, out var progress);
            return new
            {
                LessonId = lesson.LessonID,
                lesson.Title,
                lesson.Level,
                lesson.OrderIndex,
                Score = progress?.Score ?? 0,
                Completed = progress?.Completed ?? false,
                LastAccess = progress?.LastAccess
            };
        }));
    }

    [HttpPut("lessons/{lessonId:int}")]
    public async Task<IActionResult> UpdateLessonProgress(int lessonId, [FromBody] UpdateLessonProgressDto dto)
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

        var lesson = await _unitOfWork.Repository<Lesson>().GetByIdAsync(lessonId);
        if (lesson is null)
        {
            return NotFound("Lesson not found.");
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        var progressRepository = _unitOfWork.Repository<UserProgress>();
        var progress = (await progressRepository.FindAsync(candidate =>
                candidate.UserID == user.Id && candidate.LessonID == lessonId))
            .FirstOrDefault();
        var occurredAt = DateTime.UtcNow;

        if (progress is null)
        {
            progress = new UserProgress
            {
                UserID = user.Id,
                LessonID = lessonId,
                Score = dto.Score,
                Completed = dto.Completed,
                LastAccess = occurredAt
            };
            await progressRepository.AddAsync(progress);
        }
        else
        {
            progress.Score = Math.Max(progress.Score, dto.Score);
            progress.Completed |= dto.Completed;
            progress.LastAccess = occurredAt;
            progressRepository.Update(progress);
        }

        var activity = new StudyActivity
        {
            UserId = user.Id,
            LessonId = lessonId,
            OccurredAt = occurredAt,
            ActivityType = "Lesson",
            Result = dto.Completed ? "Completed" : null,
            DurationSeconds = dto.StudyDurationSeconds,
            XpEarned = dto.Completed ? 5 : 0
        };
        await _unitOfWork.Repository<StudyActivity>().AddAsync(activity);
        await DailySummaryEngine.RecordAsync(_unitOfWork, activity);

        StudyEngine.ApplyXp(user, activity.XpEarned);
        await StudyEngine.UpdateStreakAsync(_unitOfWork, user, occurredAt);
        var newlyUnlocked = await AchievementEvaluator.UnlockEligibleAsync(_unitOfWork, user, activity);
        await _unitOfWork.CompleteAsync();

        return Ok(new
        {
            LessonId = lesson.LessonID,
            lesson.Title,
            progress.Score,
            progress.Completed,
            progress.LastAccess,
            NewlyUnlockedAchievements = newlyUnlocked.Select(badge => new { badge.Id, badge.Name, badge.Description, badge.Icon })
        });
    }

    [HttpGet("reviews/due")]
    public async Task<IActionResult> GetDueReviews([FromQuery] int? deckId, [FromQuery] int take = 50)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (take is < 1 or > 100)
        {
            return BadRequest("take must be between 1 and 100.");
        }

        if (deckId is not null && !await IsDeckOwnedByUserAsync(deckId.Value, userId.Value))
        {
            return NotFound("Deck not found.");
        }

        // Tek sorgu, tek geçiş. Buradaki eski uygulama üç ayrı listeyi
        // belleğe çekiyordu — kullanıcının tüm desteleri, tüm müfredat
        // bağlantıları ve kullanıcının *bütün* UserWordProgress satırları —
        // sonra süzme, sıralama ve Take'i C# tarafında yapıyordu. Yani
        // şemadaki indeksler hiç kullanılmıyor, take=50 istense bile
        // ilerleme tablosunun tamamı ağdan geçiyordu.
        //
        // Deste-siz bir kart yalnızca paylaşılan müfredata aitse
        // çalışılabilir; sahipsiz deste-siz kayıtlar tekrar kuyruğuna
        // girmez. Bu kural aşağıdaki LessonVocabularies alt sorgusunda.
        var includeCurriculum = deckId is null;
        var now = DateTime.UtcNow;

        var lessonLinks = _unitOfWork.Repository<LessonVocabulary>().Query();
        var progress = _unitOfWork.Repository<UserWordProgress>().Query()
            .Where(row => row.UserID == userId.Value);

        var pool = _unitOfWork.Repository<Vocabulary>().Query()
            .Where(word => deckId != null
                ? word.DeckId == deckId
                : (word.DeckId != null && word.Deck!.UserId == userId.Value)
                  || (includeCurriculum && word.DeckId == null
                      && lessonLinks.Any(link => link.WordID == word.WordID)));

        // Sol birleştirme: ilerleme kaydı olmayan kelime hiç çalışılmamış
        // demektir ve zamanı gelmiş sayılır.
        var due = await pool
            .Select(word => new
            {
                Word = word,
                Progress = progress.FirstOrDefault(row => row.WordID == word.WordID)
            })
            .Where(x => x.Progress == null
                || x.Progress.NextReviewDate == null
                || x.Progress.NextReviewDate <= now)
            .OrderBy(x => x.Progress == null ? 0 : 1)
            .ThenBy(x => x.Progress!.NextReviewDate)
            .Take(take)
            .Select(x => new
            {
                WordId = x.Word.WordID,
                x.Word.DeckId,
                x.Word.Term,
                x.Word.Translation,
                x.Word.ExampleSentence,
                x.Word.ImageUrl,
                x.Word.AudioUrl,
                MasteryLevel = x.Progress == null ? 0 : x.Progress.MasteryLevel,
                ReviewCount = x.Progress == null ? 0 : x.Progress.ReviewCount,
                IntervalDays = x.Progress == null ? 0 : x.Progress.IntervalDays,
                EaseFactor = x.Progress == null ? 2.5 : x.Progress.EaseFactor,
                LastRating = x.Progress == null ? null : x.Progress.LastRating,
                NextReviewDate = x.Progress == null ? null : x.Progress.NextReviewDate
            })
            .ToListAsync();

        return Ok(due);
    }

    [HttpPost("reviews/{wordId:int}")]
    public async Task<IActionResult> SubmitReview(int wordId, [FromBody] SubmitReviewDto dto)
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

        var word = await _unitOfWork.Repository<Vocabulary>().GetByIdAsync(wordId);
        if (word is null)
        {
            return NotFound("Flashcard not found.");
        }

        if (!await IsReviewableByUserAsync(word, userId.Value))
        {
            return NotFound("Flashcard is not available for review.");
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        var progressRepository = _unitOfWork.Repository<UserWordProgress>();
        var progress = (await progressRepository.FindAsync(candidate =>
                candidate.UserID == user.Id && candidate.WordID == word.WordID))
            .FirstOrDefault();
        var reviewedAt = DateTime.UtcNow;

        var isNewProgress = progress is null;
        if (isNewProgress)
        {
            progress = new UserWordProgress
            {
                UserID = user.Id,
                WordID = word.WordID,
                LastReviewedAt = reviewedAt
            };
        }

        var userWordProgress = progress!;

        var schedule = StudyEngine.CalculateReviewSchedule(
            userWordProgress.IntervalDays,
            userWordProgress.EaseFactor,
            dto.Rating,
            reviewedAt);

        userWordProgress.IntervalDays = schedule.IntervalDays;
        userWordProgress.EaseFactor = schedule.EaseFactor;
        userWordProgress.NextReviewDate = schedule.NextReviewDate;
        userWordProgress.LastReviewedAt = reviewedAt;
        userWordProgress.LastRating = dto.Rating;
        userWordProgress.ReviewCount++;
        userWordProgress.MasteryLevel = Math.Clamp(userWordProgress.MasteryLevel + schedule.MasteryDelta, 0, 5);
        if (isNewProgress)
        {
            await progressRepository.AddAsync(userWordProgress);
        }
        else
        {
            progressRepository.Update(userWordProgress);
        }

        var xpEarned = dto.Rating switch
        {
            "Easy" => 2,
            "Medium" => 1,
            "Hard" => 1,
            _ => 0
        };
        var activity = new StudyActivity
        {
            UserId = user.Id,
            WordId = word.WordID,
            DeckId = word.DeckId,
            OccurredAt = reviewedAt,
            ActivityType = "Review",
            Result = dto.Rating,
            DurationSeconds = dto.DurationSeconds,
            XpEarned = xpEarned
        };
        await _unitOfWork.Repository<StudyActivity>().AddAsync(activity);
        await DailySummaryEngine.RecordAsync(_unitOfWork, activity);

        StudyEngine.ApplyXp(user, xpEarned);
        await StudyEngine.UpdateStreakAsync(_unitOfWork, user, reviewedAt);
        var newlyUnlocked = await AchievementEvaluator.UnlockEligibleAsync(_unitOfWork, user, activity);
        await _unitOfWork.CompleteAsync();

        return Ok(new
        {
            WordId = word.WordID,
            dto.Rating,
            userWordProgress.MasteryLevel,
            userWordProgress.ReviewCount,
            userWordProgress.IntervalDays,
            userWordProgress.EaseFactor,
            userWordProgress.NextReviewDate,
            NewlyUnlockedAchievements = newlyUnlocked.Select(badge => new { badge.Id, badge.Name, badge.Description, badge.Icon })
        });
    }

    [HttpGet("streak")]
    public async Task<IActionResult> GetStreak()
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        var activityDates = (await _unitOfWork.Repository<StudyActivity>()
                .FindAsync(activity => activity.UserId == user.Id))
            .Select(activity => activity.OccurredAt);

        return Ok(new
        {
            CurrentStreak = StudyEngine.CalculateCurrentStreak(activityDates, DateTime.UtcNow),
            LongestStreak = Math.Max(user.LongestStreak, StudyEngine.CalculateLongestStreak(activityDates)),
            user.DailyGoalMinutes
        });
    }

    /// <summary>
    /// İstatistik ekranının ısı haritası için gün gün özet.
    ///
    /// Ham aktiviteleri tarayıp gruplamak yerine <see cref="DailyStudySummary"/>
    /// satırlarını okur — aynı sayılar, ama bir yıllık aralıkta on binlerce
    /// satır yerine en fazla 365 satır.
    ///
    /// Çalışılmayan günler için satır yoktur ve uydurulmaz; boşluğu istemci
    /// "o gün aktivite yok" olarak çizer.
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<IActionResult> GetDailySummary(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Varsayılan bir yıl: ısı haritasının gösterdiği aralık.
        var start = from ?? today.AddYears(-1);
        var end = to ?? today;

        if (start > end)
        {
            return BadRequest(new { Message = "'from' tarihi 'to' tarihinden sonra olamaz." });
        }

        var summaries = await _unitOfWork.Repository<DailyStudySummary>()
            .FindAsync(summary =>
                summary.UserId == userId.Value &&
                summary.Day >= start &&
                summary.Day <= end);

        var days = summaries.OrderBy(summary => summary.Day).ToList();

        return Ok(new
        {
            From = start,
            To = end,
            TotalReviews = days.Sum(day => day.ReviewCount),
            TotalCorrect = days.Sum(day => day.CorrectCount),
            TotalQuizzes = days.Sum(day => day.QuizCount),
            TotalLessons = days.Sum(day => day.LessonCount),
            TotalStudySeconds = days.Sum(day => day.StudySeconds),
            TotalXp = days.Sum(day => day.XpEarned),
            ActiveDays = days.Count,
            Days = days.Select(day => new
            {
                day.Day,
                day.ReviewCount,
                day.CorrectCount,
                day.QuizCount,
                day.LessonCount,
                day.StudySeconds,
                day.XpEarned
            })
        });
    }

    private async Task<bool> IsDeckOwnedByUserAsync(int deckId, int userId)
    {
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        return deck?.UserId == userId;
    }

    private async Task<bool> IsReviewableByUserAsync(Vocabulary word, int userId)
    {
        if (word.DeckId is not null)
        {
            return await IsDeckOwnedByUserAsync(word.DeckId.Value, userId);
        }

        return (await _unitOfWork.Repository<LessonVocabulary>()
                .FindAsync(link => link.WordID == word.WordID))
            .Any();
    }

    private int? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
