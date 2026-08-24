namespace VocabGrid.Entities;

/// <summary>
/// Kullanıcının bir gününün özeti: kaç tekrar, kaçı doğru, kaç saniye, kaç XP.
///
/// Aynı bilgi <see cref="StudyActivity"/> satırlarından da hesaplanabilir ve
/// bugüne kadar öyle yapılıyordu. Sorun ölçekte: istatistik ekranındaki ısı
/// haritası bir yılı gösteriyor, yani her açılışta o kullanıcının bir yıllık
/// ham aktivitesi taranıp gruplanıyor. Aktif bir kullanıcıda bu on binlerce
/// satır eder ve ekran her açıldığında tekrarlanır.
///
/// Bu tablo aynı veriyi gün başına tek satıra indirir. Ham aktiviteler
/// silinmiyor — tek tek incelemek, hata ayıklamak ve özeti gerektiğinde
/// yeniden üretmek için orada duruyorlar. Bu tablo türetilmiş veridir:
/// kaybolursa aktivitelerden yeniden hesaplanabilir.
///
/// Gün <see cref="DateOnly"/>, saat bilgisi yok. Isı haritasının sorduğu soru
/// "o gün çalıştı mı" — saat dilimi ayrıntısı burada bilgi değil gürültü olur.
/// </summary>
public class DailyStudySummary
{
    public long Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateOnly Day { get; set; }

    /// <summary>
    /// Özetin ait olduğu hedef dilin ISO kodu. Gün başına değil, gün+dil
    /// başına tek satır: aynı gün iki dil çalışılırsa iki satır oluşur ve
    /// istatistik ekranı hangisini gösteriyorsa onu okur.
    ///
    /// Bu alandan önceki satırlar boş string taşır; toplamda görünürler ama
    /// dil filtresi uygulandığında dışarıda kalırlar.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    public int ReviewCount { get; set; }

    /// <summary>
    /// "Doğru" sayılan tekrarlar: Again dışındaki her değerlendirme. Isı
    /// haritasının yanındaki isabet yüzdesi bundan çıkıyor.
    /// </summary>
    public int CorrectCount { get; set; }

    public int QuizCount { get; set; }
    public int LessonCount { get; set; }

    public int StudySeconds { get; set; }
    public int XpEarned { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
