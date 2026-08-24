using VocabGrid.Entities;
using VocabGrid.Interfaces;

namespace VocabGrid.Services;

public static class StudyEngine
{
    public const int XpPerLevel = 100;

    public static void ApplyXp(User user, int xpEarned)
    {
        user.TotalXp = Math.Max(0, user.TotalXp + xpEarned);
        user.Level = Math.Max(1, user.TotalXp / XpPerLevel + 1);
    }

    public static async Task UpdateStreakAsync(IUnitOfWork unitOfWork, User user, DateTime activityDate)
    {
        var activities = await unitOfWork.Repository<StudyActivity>()
            .FindAsync(activity => activity.UserId == user.Id);

        var dates = activities
            .Select(activity => activity.OccurredAt.Date)
            .Append(activityDate.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        user.CurrentStreak = CalculateCurrentStreak(dates, activityDate);
        user.LongestStreak = Math.Max(user.LongestStreak, CalculateLongestStreak(dates));
        unitOfWork.Repository<User>().Update(user);
    }

    public static int CalculateCurrentStreak(IEnumerable<DateTime> activityDates, DateTime referenceDate)
    {
        var dates = activityDates.Select(date => date.Date).ToHashSet();
        var cursor = referenceDate.Date;

        if (!dates.Contains(cursor))
        {
            cursor = cursor.AddDays(-1);
        }

        var streak = 0;
        while (dates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    public static int CalculateLongestStreak(IEnumerable<DateTime> activityDates)
    {
        var dates = activityDates.Select(date => date.Date).Distinct().OrderBy(date => date).ToList();
        if (dates.Count == 0)
        {
            return 0;
        }

        var longest = 1;
        var current = 1;

        for (var index = 1; index < dates.Count; index++)
        {
            if (dates[index] == dates[index - 1].AddDays(1))
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 1;
            }
        }

        return longest;
    }
}
