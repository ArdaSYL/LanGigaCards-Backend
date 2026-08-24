using System.ComponentModel.DataAnnotations;

namespace VocabGrid.Entities;

/// <summary>
/// Kullanıcıya özel flashcard / kelime SRS ilerlemesi.
/// Aynı Vocabulary kartı birden fazla kullanıcıda farklı state tutabilir.
/// </summary>
public class UserWordProgress
{
    [Key]
    public long UserWordID { get; set; }

    public int UserID { get; set; }
    public User User { get; set; } = null!;

    public int WordID { get; set; }
    public Vocabulary Vocabulary { get; set; } = null!;

    public int MasteryLevel { get; set; } = 0;
    public DateTime? NextReviewDate { get; set; }
    public DateTime LastReviewedAt { get; set; } = DateTime.UtcNow;

    public int ReviewCount { get; set; }

    /// <summary>Legacy SM-2-style fields. No longer written by SubmitReview
    /// (superseded by Stability/Difficulty, see FsrsEngine) — kept only so
    /// existing rows and any external reader of this table aren't broken by
    /// a dropped column. IntervalDays is refreshed to the FSRS-computed
    /// interval for continuity, but EaseFactor is frozen at its last SM-2
    /// value going forward.</summary>
    public double EaseFactor { get; set; } = 2.5;
    public int IntervalDays { get; set; } = 0;

    /// <summary>FSRS memory state (see VocabGrid.Services.FsrsEngine). Zero
    /// means "never reviewed under FSRS" — either a genuinely new word, or
    /// (for rows that existed before this migration) backfilled from the
    /// prior EaseFactor/IntervalDays as a one-time approximation.</summary>
    public double Stability { get; set; } = 0;
    public double Difficulty { get; set; } = 0;

    /// <summary>Son değerlendirme: Again, Hard, Medium, Easy (Figma SRS).</summary>
    public string? LastRating { get; set; }
}
