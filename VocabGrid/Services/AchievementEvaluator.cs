using VocabGrid.Entities;
using VocabGrid.Interfaces;

namespace VocabGrid.Services;

public static class AchievementEvaluator
{
    public static bool IsSupported(Badge badge)
        => badge.UnlockCondition != "LanguagesStarted";

    public static string? GetUnsupportedReason(Badge badge)
        => badge.UnlockCondition == "LanguagesStarted"
            ? "Language-history tracking is not yet part of the user model."
            : null;

    public static async Task<IReadOnlyList<Badge>> UnlockEligibleAsync(
        IUnitOfWork unitOfWork,
        User user,
        StudyActivity? pendingActivity = null)
    {
        var badges = (await unitOfWork.Repository<Badge>().GetAllAsync()).ToList();
        var unlockedBadgeIds = (await unitOfWork.Repository<UserBadge>()
                .FindAsync(userBadge => userBadge.UserId == user.Id))
            .Select(userBadge => userBadge.BadgeId)
            .ToHashSet();

        var quizSessions = (await unitOfWork.Repository<QuizSession>()
                .FindAsync(session => session.UserId == user.Id && session.CompletedAt != null))
            .ToList();
        var wordProgresses = (await unitOfWork.Repository<UserWordProgress>()
                .FindAsync(progress => progress.UserID == user.Id))
            .ToList();
        var activities = (await unitOfWork.Repository<StudyActivity>()
                .FindAsync(activity => activity.UserId == user.Id))
            .AppendIfNotNull(pendingActivity)
            .OrderBy(activity => activity.OccurredAt)
            .ToList();

        var newlyUnlocked = new List<Badge>();
        foreach (var badge in badges.Where(badge => !unlockedBadgeIds.Contains(badge.Id)))
        {
            if (!IsEligible(badge, user, quizSessions, wordProgresses, activities))
            {
                continue;
            }

            var badgeRepository = unitOfWork.Repository<UserBadge>();
            var (_, wasCreated) = await ConcurrentSingleton.GetOrCreateWithStatusAsync(
                unitOfWork,
                find: async () => (await badgeRepository.FindAsync(ub =>
                        ub.UserId == user.Id && ub.BadgeId == badge.Id))
                    .FirstOrDefault(),
                create: () => new UserBadge
                {
                    UserId = user.Id,
                    BadgeId = badge.Id,
                    UnlockedAt = DateTime.UtcNow
                });

            // Two concurrent requests can both newly qualify for the same
            // badge at once (e.g. two reviews crossing the unlock threshold
            // in the same instant); only report it to whichever one
            // actually won -- the other already sees it unlocked and has
            // nothing new to tell the caller.
            if (wasCreated)
            {
                newlyUnlocked.Add(badge);
            }
        }

        return newlyUnlocked;
    }

    private static bool IsEligible(
        Badge badge,
        User user,
        IReadOnlyCollection<QuizSession> quizSessions,
        IReadOnlyCollection<UserWordProgress> wordProgresses,
        IReadOnlyCollection<StudyActivity> activities) => badge.UnlockCondition switch
    {
        "StreakDays" => user.CurrentStreak >= badge.Threshold,
        "PerfectQuiz" => quizSessions.Any(session =>
            session.TotalQuestions > 0 &&
            session.CorrectCount * 100 >= session.TotalQuestions * badge.Threshold),
        "WordsLearned" => wordProgresses.Count(progress => progress.MasteryLevel >= 4) >= badge.Threshold,
        "CardsInMinutes" => HasReviewBurst(activities, badge.Threshold),
        // Deliberately unsupported until the user model tracks language history.
        "LanguagesStarted" => false,
        _ => false
    };

    private static bool HasReviewBurst(IEnumerable<StudyActivity> activities, int threshold)
    {
        var reviewTimes = activities
            .Where(activity => activity.ActivityType == "Review")
            .Select(activity => activity.OccurredAt)
            .OrderBy(date => date)
            .ToList();

        for (var start = 0; start < reviewTimes.Count; start++)
        {
            var end = start;
            while (end < reviewTimes.Count && reviewTimes[end] <= reviewTimes[start].AddMinutes(5))
            {
                end++;
            }

            if (end - start >= threshold)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<T> AppendIfNotNull<T>(this IEnumerable<T> source, T? item) where T : class
        => item is null ? source : source.Append(item);
}
