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
            // Dersler paylaşılan müfredattan geliyor ve kendi dil kodlarını
            // taşımıyor; öğrenenin o anki hedef dili en doğru karşılık.
            LanguageCode = LanguageProgressEngine.Normalize(user.TargetLanguageCode),
            Result = dto.Completed ? "Completed" : null,
            DurationSeconds = dto.StudyDurationSeconds,
            XpEarned = dto.Completed ? 5 : 0
        };
        await _unitOfWork.Repository<StudyActivity>().AddAsync(activity);
        await DailySummaryEngine.RecordAsync(_unitOfWork, activity);
        await LanguageProgressEngine.RecordAsync(_unitOfWork, activity, user.NativeLanguageCode, user.TargetLanguage);

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

    /// <summary>
    /// Tekrar sırası.
    ///
    /// <para>
    /// <paramref name="languageCode"/> verildiğinde yalnızca o hedef dilin
    /// desteleri taranır — Almanca çalışırken Japonca kartlarının kuyruğa
    /// karışması, dil başına ayrılmış bir kitaplıkta anlamsız olurdu.
    /// </para>
    ///
    /// <para>
    /// Sıra ayrıca kaldığı yerden devam eder: o dilde en son çalışılan
    /// kelimenin destesindeki kartlar öne alınır. Öğrenen uygulamayı kapatıp
    /// döndüğünde yarım bıraktığı desteyi baştan aramak zorunda kalmıyor.
    /// Bunun dışındaki sıralama değişmedi — hiç çalışılmamış kartlar önce,
    /// sonra tekrar tarihi en eski olanlar.
    /// </para>
    /// </summary>
    [HttpGet("reviews/due")]
    public async Task<IActionResult> GetDueReviews(
        [FromQuery] int? deckId,
        [FromQuery] int take = 50,
        [FromQuery] string? languageCode = null)
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

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        var code = LanguageProgressEngine.Normalize(languageCode);
        var nativeCode = LanguageProgressEngine.Normalize(user.NativeLanguageCode);
        var languageProfile = code.Length == 0
            ? null
            : (await _unitOfWork.Repository<UserLanguageProfile>()
                .FindAsync(p => p.UserId == userId.Value && p.NativeLanguageCode == nativeCode && p.LanguageCode == code)).FirstOrDefault();

        // Kaldığı yer: o dilde en son çalışılan kelime ve destesi.
        //
        // Kelimenin kendisi çoğu zaman kuyrukta olmaz — az önce çalışıldığı
        // için tekrar tarihi ileri atılmıştır. Ama "Again" denmişse on dakika
        // sonra yeniden gelir ve o durumda ilk sırada olması gerekir: öğrenen
        // hatırlayamadığını söylediği kelimeye dönmek ister. Kuyrukta değilse
        // bu sıralama hiçbir şeyi değiştirmez.
        //
        // Destesi ise her hâlükârda öne alınır; asıl "kaldığı yerden devam"
        // etkisi buradan geliyor.
        var resumeDeckId = languageProfile?.LastStudiedDeckId;
        var resumeWordId = languageProfile?.LastStudiedWordId;

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

        // Dil süzgeci yalnızca destelerdeki kartlara uygulanıyor. Müfredat
        // kartlarının kendi dil kodu yok; onlar zaten kullanıcının hedef diline
        // göre üretiliyor ve dil verildiğinde kuyruğa yalnızca o dil hedefse
        // giriyorlar (aşağıdaki includeCurriculum koşulu değişmedi, üstüne dil
        // eşleşmesi eklendi).
        var pool = _unitOfWork.Repository<Vocabulary>().Query()
            .Where(word => deckId != null
                ? word.DeckId == deckId
                : (word.DeckId != null && word.Deck!.UserId == userId.Value
                      && (code == "" || word.Deck!.LanguageCode == code))
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
            .OrderBy(x => resumeWordId != null && x.Word.WordID == resumeWordId ? 0 : 1)
            .ThenBy(x => resumeDeckId != null && x.Word.DeckId == resumeDeckId ? 0 : 1)
            .ThenBy(x => x.Progress == null ? 0 : 1)
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
        var reviewedAt = DateTime.UtcNow;

        var userWordProgress = await ConcurrentSingleton.GetOrCreateAsync(
            _unitOfWork,
            find: async () => (await progressRepository.FindAsync(candidate =>
                    candidate.UserID == user.Id && candidate.WordID == word.WordID))
                .FirstOrDefault(),
            create: () => new UserWordProgress
            {
                UserID = user.Id,
                WordID = word.WordID,
                LastReviewedAt = reviewedAt
            });

        // Must be read before LastReviewedAt is overwritten below -- FsrsEngine
        // needs the *previous* review's timestamp to know how many days have
        // elapsed since then, not this one. For a first-time review
        // (Stability still at its zero default) FsrsEngine never looks at
        // this value, so it not being meaningful yet for a freshly-created
        // row doesn't matter.
        var previousReviewedAt = userWordProgress.LastReviewedAt;

        var wordLanguageCode = await LanguageProgressEngine.ResolveLanguageAsync(_unitOfWork, word, user);
        var languageProfile = await LanguageProgressEngine.GetOrCreateAsync(
            _unitOfWork, user.Id, user.NativeLanguageCode, wordLanguageCode, user.TargetLanguage);

        var schedule = FsrsEngine.ReviewCard(
            userWordProgress.Stability,
            userWordProgress.Difficulty,
            previousReviewedAt,
            dto.Rating,
            reviewedAt,
            cefrLevel: languageProfile?.DifficultyMode);

        userWordProgress.Stability = schedule.Stability;
        userWordProgress.Difficulty = schedule.Difficulty;
        // Refreshed for continuity/debugging only -- nothing computes from
        // these anymore, see the doc comment on UserWordProgress.
        userWordProgress.IntervalDays = Math.Max(1, (int)Math.Round((schedule.NextReviewDate - reviewedAt).TotalDays));
        userWordProgress.NextReviewDate = schedule.NextReviewDate;
        userWordProgress.LastReviewedAt = reviewedAt;
        userWordProgress.LastRating = dto.Rating;
        userWordProgress.ReviewCount++;
        userWordProgress.MasteryLevel = schedule.MasteryLevel;
        // Always Update, never Add: ConcurrentSingleton.GetOrCreateAsync above
        // already persisted the row (whether it found one or had to create
        // it), so it's guaranteed to already exist in the database by now.
        progressRepository.Update(userWordProgress);

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
            // Kartın destesinden gelen dil; destesiz müfredat kartlarında
            // kullanıcının o anki hedef dili.
            LanguageCode = wordLanguageCode,
            Result = dto.Rating,
            DurationSeconds = dto.DurationSeconds,
            XpEarned = xpEarned
        };
        await _unitOfWork.Repository<StudyActivity>().AddAsync(activity);
        await DailySummaryEngine.RecordAsync(_unitOfWork, activity);
        await LanguageProgressEngine.RecordAsync(_unitOfWork, activity, user.NativeLanguageCode, user.TargetLanguage);

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

    /// <summary>
    /// Seri bilgisi. <paramref name="languageCode"/> verildiğinde yalnızca o
    /// dilde çalışılan günler sayılır.
    /// </summary>
    [HttpGet("streak")]
    public async Task<IActionResult> GetStreak([FromQuery] string? languageCode = null)
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

        var code = LanguageProgressEngine.Normalize(languageCode);
        var activityDates = (await _unitOfWork.Repository<StudyActivity>()
                .FindAsync(activity => activity.UserId == user.Id &&
                    (code == "" || activity.LanguageCode == code)))
            .Select(activity => activity.OccurredAt);

        var nativeCode = LanguageProgressEngine.Normalize(user.NativeLanguageCode);
        var recordedLongest = code.Length == 0
            ? user.LongestStreak
            : (await _unitOfWork.Repository<UserLanguageProfile>()
                    .FindAsync(p => p.UserId == user.Id && p.NativeLanguageCode == nativeCode && p.LanguageCode == code))
                .FirstOrDefault()?.LongestStreak ?? 0;

        return Ok(new
        {
            CurrentStreak = StudyEngine.CalculateCurrentStreak(activityDates, DateTime.UtcNow),
            LongestStreak = Math.Max(recordedLongest, StudyEngine.CalculateLongestStreak(activityDates)),
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
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? languageCode = null)
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

        var code = LanguageProgressEngine.Normalize(languageCode);
        var summaries = await _unitOfWork.Repository<DailyStudySummary>()
            .FindAsync(summary =>
                summary.UserId == userId.Value &&
                (code == "" || summary.LanguageCode == code) &&
                summary.Day >= start &&
                summary.Day <= end);

        // Dil süzgeci yokken aynı günün birden çok dile ait satırı olabilir;
        // ekranın istediği tek bir gün olduğu için birleştiriliyorlar.
        var days = summaries
            .GroupBy(summary => summary.Day)
            .Select(group => new DailyStudySummary
            {
                Day = group.Key,
                ReviewCount = group.Sum(s => s.ReviewCount),
                CorrectCount = group.Sum(s => s.CorrectCount),
                QuizCount = group.Sum(s => s.QuizCount),
                LessonCount = group.Sum(s => s.LessonCount),
                StudySeconds = group.Sum(s => s.StudySeconds),
                XpEarned = group.Sum(s => s.XpEarned)
            })
            .OrderBy(summary => summary.Day)
            .ToList();

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
