namespace VocabGrid.Entities;

/// <summary>
/// Kullanıcının bir quiz oturumu (Figma: Q1 of 5, timer, pts).
/// Soru bankası Quiz tablosunda kalır; oturum skoru burada tutulur.
/// </summary>
public class QuizSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public int? DeckId { get; set; }
    public Deck? Deck { get; set; }

    /// <summary>
    /// Oturumun ait olduğu hedef dilin ISO kodu. Kart quizleri kullanıcının
    /// kendi destelerinden üretiliyor ve istatistik dil başına ayrıldığı için
    /// oturumun da hangi dile yazıldığı belli olmalı.
    /// </summary>
    public string? LanguageCode { get; set; }

    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public int SkippedCount { get; set; }
    public int ScorePoints { get; set; }
    public int TimeLimitSeconds { get; set; } = 20;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<QuizSessionAnswer> Answers { get; set; } = new List<QuizSessionAnswer>();
}

public class QuizSessionAnswer
{
    public long Id { get; set; }
    public int QuizSessionId { get; set; }
    public QuizSession QuizSession { get; set; } = null!;

    /// <summary>
    /// Ders soru bankasındaki soru. Kart quizlerinde null: o sorular önceden
    /// yazılmış değil, öğrenenin kendi kartlarından anlık üretiliyor —
    /// karşılığı <see cref="WordId"/>.
    /// </summary>
    public int? QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    /// <summary>
    /// Kart quizinde sorulan kelime. "Tamamlama" ölçüsü buradan çıkıyor:
    /// destenin kaç ayrı kelimesi quizde gösterilmiş.
    /// </summary>
    public int? WordId { get; set; }
    public Vocabulary? Word { get; set; }

    public int? SelectedOptionId { get; set; }
    public QuizOption? SelectedOption { get; set; }

    public bool? IsCorrect { get; set; }
    public bool IsSkipped { get; set; }
    public int TimeSpentSeconds { get; set; }
    public int PointsEarned { get; set; }
}
