namespace VocabGrid.Entities;

/// <summary>
/// Ham çalışma aktivitesi. İstatistik, heatmap, accuracy ve study time buradan hesaplanır.
/// </summary>
public class StudyActivity
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Aktivitenin ait olduğu hedef dilin ISO kodu.
    ///
    /// İstatistik dil başına ayrıldığı için bu alan olmadan "bu ayki isabet
    /// oranım" sorusu yanıtlanamaz: Almanca ve Japonca tekrarları aynı
    /// toplamda birikirdi. Kodu aktiviteye yazmak, sonradan desteden
    /// türetmekten sağlamdır — deste silinince <c>DeckId</c> null'a düşer,
    /// yazılmış geçmiş ise olduğu gibi kalır.
    ///
    /// Bu alandan önceki satırlarda null; o kayıtlar dil filtresi
    /// uygulandığında dışarıda kalır ama toplamlarda görünmeye devam eder.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>Review, Quiz, Lesson vb.</summary>
    public string ActivityType { get; set; } = "Review";

    public int? WordId { get; set; }
    public Vocabulary? Vocabulary { get; set; }

    public int? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public int? DeckId { get; set; }
    public Deck? Deck { get; set; }

    /// <summary>Correct, Wrong, Skipped, Again, Hard, Medium, Easy</summary>
    public string? Result { get; set; }

    public int DurationSeconds { get; set; }
    public int XpEarned { get; set; }
}
