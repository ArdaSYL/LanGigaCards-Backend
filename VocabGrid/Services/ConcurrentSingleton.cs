using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VocabGrid.Interfaces;

namespace VocabGrid.Services;

/// <summary>
/// Safely resolves a "one row per some key" singleton under concurrent
/// requests -- the shape every GetOrCreateAsync in this codebase shares:
/// UserWordProgress (per user+word), DailyStudySummary (per user+language+
/// day), UserLanguageProfile (per user+language). Two requests can both see
/// "no row yet" and both try to insert; the loser's insert violates the
/// row's unique index and used to crash the whole request with a 500 (load
/// testing surfaced this under concurrent SubmitReview calls).
///
/// Persists the create immediately -- its own small SaveChanges -- rather
/// than leaving it for the caller's own later, larger SaveChanges. By the
/// time this returns, the row is guaranteed to already exist in the
/// database, so every further mutation the caller makes on it is a plain
/// UPDATE, which cannot race the same way (concurrent updates just apply in
/// whichever order they land, not a constraint violation). The tradeoff:
/// this commits slightly ahead of whatever else the caller's request is
/// about to add to the same unit of work, rather than everything landing in
/// one final transaction -- acceptable here since none of these rows have a
/// meaningful reason to roll back together with the rest of the request.
/// </summary>
internal static class ConcurrentSingleton
{
    internal static async Task<T> GetOrCreateAsync<T>(
        IUnitOfWork unitOfWork,
        Func<Task<T?>> find,
        Func<T> create,
        int maxAttempts = 20) where T : class
    {
        var (row, _) = await GetOrCreateAsyncCore(unitOfWork, find, create, maxAttempts);
        return row;
    }

    /// <summary>
    /// Same as <see cref="GetOrCreateAsync{T}"/>, but also reports whether
    /// *this* call was the one that created the row -- needed when the
    /// caller has to distinguish "I just unlocked this" from "someone else
    /// already had", e.g. AchievementEvaluator only wants to report a badge
    /// as newly unlocked to the request that actually won the race.
    /// </summary>
    internal static Task<(T Row, bool WasCreated)> GetOrCreateWithStatusAsync<T>(
        IUnitOfWork unitOfWork,
        Func<Task<T?>> find,
        Func<T> create,
        int maxAttempts = 20) where T : class =>
        GetOrCreateAsyncCore(unitOfWork, find, create, maxAttempts);

    private static async Task<(T Row, bool WasCreated)> GetOrCreateAsyncCore<T>(
        IUnitOfWork unitOfWork,
        Func<Task<T?>> find,
        Func<T> create,
        int maxAttempts) where T : class
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var existing = await find();
            if (existing is not null)
            {
                return (existing, false);
            }

            var repository = unitOfWork.Repository<T>();
            var created = create();
            await repository.AddAsync(created);
            try
            {
                await unitOfWork.CompleteAsync();
                return (created, true);
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsUniqueIndexViolation(ex))
            {
                // created never reached the database -- Delete() on a still-
                // Added entity just cancels the pending insert in the change
                // tracker, it does not touch any row. A jittered backoff
                // before the next find() spreads out a large batch of
                // racers that all lost in the same instant (e.g. a hot row
                // like a single day's DailyStudySummary, touched by every
                // review a user submits that day) instead of them all
                // immediately re-colliding on the same retry.
                repository.Delete(created);
                await Task.Delay(Random.Shared.Next(1, 5 * attempt));
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve a concurrent {typeof(T).Name} row after {maxAttempts} attempts.");
    }

    private static bool IsUniqueIndexViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
