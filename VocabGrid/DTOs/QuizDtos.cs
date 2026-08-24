using System.ComponentModel.DataAnnotations;

namespace VocabGrid.DTOs;

public sealed class StartQuizSessionDto
{
    [Range(1, int.MaxValue)]
    public int LessonId { get; init; }

    [Range(1, 50)]
    public int QuestionCount { get; init; } = 5;
}
/// <summary>
/// Tamamlanmış bir kart quizi, tek istekte.
///
/// <para>
/// Ders quizleri sunucudaki soru bankasından gelir ve soru soru işlenir.
/// Kart quizi ise öğrenenin kendi kartlarından istemcide üretiliyor: sunucuda
/// karşılığı olan bir soru kaydı yok, dolayısıyla soru başına doğrulanacak
/// bir şey de yok. Bu yüzden sonuç bittiğinde bir bütün olarak gönderiliyor —
/// ara istekler yalnızca gecikme eklerdi.
/// </para>
///
/// <para>
/// Gönderilen tek "ham" bilgi hangi kelimenin sorulduğu ve doğru bilinip
/// bilinmediği. Doğruluk oranı ve tamamlama yüzdesi sunucuda bundan
/// hesaplanır; istemcinin gönderdiği hazır bir yüzdeye güvenilmez.
/// </para>
/// </summary>
public sealed class SubmitCardQuizDto
{
    /// <summary>
    /// Quizin çalışıldığı deste. Null ise quiz tüm kitaplıktan üretilmiştir;
    /// o zaman tamamlama yüzdesi de kitaplığın tamamına göre hesaplanır.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? DeckId { get; init; }

    /// <summary>
    /// Hedef dil. Boş bırakılırsa destenin dili, o da yoksa kullanıcının o an
    /// çalıştığı dil kullanılır.
    /// </summary>
    [MaxLength(8)]
    public string? LanguageCode { get; init; }

    [MinLength(1, ErrorMessage = "A quiz submission must contain at least one answer.")]
    public List<CardQuizAnswerDto> Answers { get; init; } = new();
}

public sealed class CardQuizAnswerDto
{
    [Range(1, int.MaxValue)]
    public int WordId { get; init; }

    public bool IsCorrect { get; init; }

    /// <summary>
    /// Soru cevaplanmadan süre doldu. Doğru sayılmaz ama yanlış da sayılmaz —
    /// doğruluk oranının paydasına girmez.
    /// </summary>
    public bool Skipped { get; init; }

    [Range(0, 3600)]
    public int TimeSpentSeconds { get; init; }
}

public sealed class SubmitQuizAnswerDto
{
    [Range(1, int.MaxValue)]
    public int QuizId { get; init; }

    [Range(1, int.MaxValue)]
    public int? SelectedOptionId { get; init; }

    public bool Skip { get; init; }

    [Range(0, 3600)]
    public int TimeSpentSeconds { get; init; }
}
