namespace VocabGrid.Services;

/// <summary>Everything about a card's memory state after a review.</summary>
public sealed record FsrsReview(double Stability, double Difficulty, DateTime NextReviewDate, int MasteryLevel);

/// <summary>
/// FSRS ("Free Spaced Repetition Scheduler") — replaces the hand-tuned
/// SM-2-style formula that used to live in StudyEngine.CalculateReviewSchedule.
/// See docs/research/2026-08-17-review-and-onboarding-algorithms.md (LanGigaCard
/// repo) for the full research behind this choice.
///
/// Models each card with two numbers: Difficulty (1-10, how inherently hard
/// this item is) and Stability (days until predicted recall probability
/// decays to 90%). Retrievability derives the actual recall probability at
/// any elapsed time from Stability.
///
/// Formulas and default weights transcribed from the open-spaced-repetition
/// project's published FSRS-6 specification and its py-fsrs reference
/// implementation (github.com/open-spaced-repetition/py-fsrs), the same
/// algorithm Anki itself now schedules with by default. The weights are
/// trained on millions of real reviews and are deliberately used as-is,
/// unmodified — per the project's own guidance, per-user optimization isn't
/// worth attempting until a learner has several hundred reviews logged, far
/// more than this app has for any one account today.
///
/// Deliberately not implemented: FSRS-6's same-day-review stability formula
/// (weights w17-w19, for a card reviewed more than once on the same day).
/// "Again" here keeps this app's existing behavior instead — an immediate
/// short relearn step, independent of the long-term interval FSRS would
/// otherwise compute — mirroring how Anki itself separates short-term
/// "learning steps" from the long-term FSRS scheduler rather than modeling
/// same-day repetition as part of the stability curve.
/// </summary>
public static class FsrsEngine
{
    // FSRS-6 default parameters (21 weights), verbatim from py-fsrs's
    // published defaults.
    private static readonly double[] W =
    {
        0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334, 3.0194, 0.001, 1.8722,
        0.1666, 0.796, 1.4835, 0.0614, 0.2629, 1.6483, 0.6014, 1.8729, 0.5425,
        0.0912, 0.0658, 0.1542,
    };

    /// <summary>Target recall probability when scheduling the next review —
    /// the same default py-fsrs itself ships with. Higher means shorter,
    /// more frequent reviews and less forgetting tolerated. Not currently
    /// driven by any per-user preference (the client's old Easy/Adaptive/
    /// Hard "Difficulty Mode" setting was repurposed into a CEFR level
    /// picker — see ProficiencyDifficultyOffsetFor below), but kept as an
    /// explicit ReviewCard parameter rather than inlined, so a real
    /// per-user retention preference has somewhere to plug in later without
    /// changing this method's shape again.</summary>
    public const double DefaultRequestRetention = 0.90;

    /// <summary>
    /// A small first-review-only nudge to initial difficulty, from the
    /// learner's self-reported CEFR level (their `DifficultyMode` picker,
    /// values A1..C2 -- see UserSettings.DifficultyMode). A total beginner
    /// (A1) is nudged to treat a brand-new word as somewhat harder than the
    /// rating alone implies; a near-fluent learner (C2) is nudged the other
    /// way. This only touches the *first* review of a word -- every review
    /// after that is driven entirely by the learner's own actual performance
    /// on that specific word, same as before. B1 (the field's own default)
    /// intentionally nudges by zero, so an account that's never touched
    /// this setting sees no behavior change from adding it.
    ///
    /// Magnitudes are deliberately smaller than a naive A1..C2 spread might
    /// suggest: InitialDifficulty(Medium) is ~2.12 and InitialDifficulty(Easy)
    /// is already clamped to the 1.0 floor before any offset is even
    /// applied (verified empirically against the live engine, not just
    /// derived on paper). A larger swing would push most first ratings for
    /// every level straight to the floor, collapsing exactly the
    /// distinction this exists to draw. These values keep A1..C2 spread out
    /// without saturating on Medium, the most common first-touch rating.
    /// </summary>
    public static double ProficiencyDifficultyOffsetFor(string? cefrLevel) => cefrLevel?.Trim().ToUpperInvariant() switch
    {
        "A1" => 0.75,
        "A2" => 0.5,
        "B1" => 0,
        "B1+" => -0.15,
        "B2" => -0.35,
        "C1" => -0.6,
        "C2" => -0.85,
        _ => 0,
    };

    private const double MinStability = 0.1;

    /// <summary>0=Again, 1=Hard, 2=Medium("Good"), 3=Easy — this app's own
    /// four ratings, in FSRS's own grade order.</summary>
    private static int GradeIndex(string rating) => rating switch
    {
        "Again" => 0,
        "Hard" => 1,
        "Medium" => 2,
        "Easy" => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(rating), "Unsupported review rating."),
    };

    /// <summary>Probability of recall after [daysElapsed] days at the given
    /// stability. 0 for a card that's never been reviewed.</summary>
    public static double Retrievability(double stability, double daysElapsed)
    {
        if (stability <= 0) return 0;
        var w20 = W[20];
        var factor = Math.Pow(0.9, -1.0 / w20) - 1.0;
        return Math.Pow(1 + factor * daysElapsed / stability, -w20);
    }

    /// <summary>Days until predicted recall probability drops to
    /// [requestRetention], for a card at the given stability.</summary>
    public static double IntervalForRetention(double stability, double requestRetention)
    {
        var w20 = W[20];
        var factor = Math.Pow(0.9, -1.0 / w20) - 1.0;
        return (stability / factor) * (Math.Pow(requestRetention, -1.0 / w20) - 1.0);
    }

    /// <summary>
    /// Computes the new memory state and next review date after a review.
    /// [currentStability] &lt;= 0 is treated as "never reviewed" — the card's
    /// very first review, regardless of any legacy EaseFactor/ReviewCount.
    /// </summary>
    public static FsrsReview ReviewCard(
        double currentStability,
        double currentDifficulty,
        DateTime? lastReviewedAt,
        string rating,
        DateTime reviewedAt,
        double requestRetention = DefaultRequestRetention,
        string? cefrLevel = null)
    {
        var g = GradeIndex(rating);
        var isFirstReview = currentStability <= 0;

        double stability;
        double difficulty;

        if (isFirstReview)
        {
            stability = Math.Max(W[g], MinStability);
            difficulty = Math.Clamp(InitialDifficulty(g) + ProficiencyDifficultyOffsetFor(cefrLevel), 1, 10);
        }
        else
        {
            var elapsedDays = lastReviewedAt is null ? 0 : Math.Max(0, (reviewedAt - lastReviewedAt.Value).TotalDays);
            var retrievability = Retrievability(currentStability, elapsedDays);

            difficulty = NextDifficulty(currentDifficulty, g);
            stability = g == 0
                ? NextForgetStability(difficulty, currentStability, retrievability)
                : NextRecallStability(difficulty, currentStability, retrievability, g);
        }

        stability = Math.Max(stability, MinStability);
        var intervalDays = Math.Max(1, (int)Math.Round(IntervalForRetention(stability, requestRetention)));

        var nextReviewDate = g == 0
            ? reviewedAt.AddMinutes(10)
            : reviewedAt.AddDays(intervalDays);

        return new FsrsReview(stability, difficulty, nextReviewDate, MasteryLevelFrom(stability));
    }

    private static double InitialDifficulty(int g) => Math.Clamp(W[4] - Math.Exp(g * W[5]) + 1, 1, 10);

    private static double NextDifficulty(double difficulty, int g)
    {
        // g is 0-indexed (Again=0..Easy=3); FSRS's own "(G-3)" term uses a
        // 1-indexed grade (Again=1..Easy=4), so g-2 here is that same term.
        var deltaD = -W[6] * (g - 2);
        var dampened = deltaD * (10 - difficulty) / 9;
        var reverted = W[7] * InitialDifficulty(3) + (1 - W[7]) * (difficulty + dampened);
        return Math.Clamp(reverted, 1, 10);
    }

    private static double NextRecallStability(double difficulty, double stability, double retrievability, int g)
    {
        var hardPenalty = g == 1 ? W[15] : 1.0;
        var easyBonus = g == 3 ? W[16] : 1.0;
        var factor = Math.Exp(W[8])
            * (11 - difficulty)
            * Math.Pow(stability, -W[9])
            * (Math.Exp(W[10] * (1 - retrievability)) - 1)
            * hardPenalty
            * easyBonus;
        return stability * (factor + 1);
    }

    private static double NextForgetStability(double difficulty, double stability, double retrievability) =>
        W[11] * Math.Pow(difficulty, -W[12]) * (Math.Pow(stability + 1, W[13]) - 1) * Math.Exp(W[14] * (1 - retrievability));

    /// <summary>
    /// Maps FSRS stability onto the app's existing 0-5 MasteryLevel scale,
    /// so everything downstream (achievements, statistics, the Flutter
    /// client's deriveMemoryStrength) keeps working against the same
    /// contract it already has. Stability is measured in days and isn't the
    /// same unit the old +1/-1/+2-per-review counter used, so this buckets
    /// by stability milestones instead of porting the old delta logic:
    /// a memory stable for months is "mastered" regardless of how many
    /// reviews it took to get there.
    /// </summary>
    private static int MasteryLevelFrom(double stability)
    {
        if (stability < 1) return 0;
        if (stability < 3) return 1;
        if (stability < 10) return 2;
        if (stability < 30) return 3;
        if (stability < 90) return 4;
        return 5;
    }
}
