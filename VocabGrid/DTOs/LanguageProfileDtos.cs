using System.ComponentModel.DataAnnotations;

namespace VocabGrid.DTOs;

/// <summary>
/// Kullanıcının tek bir hedef dildeki durumu: seviyesi, kurulumu tamamlanmış
/// mı ve o dile ait sayaçlar.
/// </summary>
public class LanguageProfileDto
{
    public string LanguageCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public string ProficiencyLevel { get; set; } = string.Empty;

    /// <summary>Kelime seçiminin tavanı (CEFR: A1..C2).</summary>
    public string DifficultyMode { get; set; } = string.Empty;

    /// <summary>
    /// Seviye ölçümü ve kategori seçimi yapıldı mı. False ise istemci bu dile
    /// geçişte kurulum penceresini açar.
    /// </summary>
    public bool IsSetupCompleted { get; set; }

    /// <summary>Bu dilde seçilmiş kategori kimlikleri.</summary>
    public List<int> CategoryIds { get; set; } = new();

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; }

    /// <summary>Bu dilde en son çalışılan deste; hiç çalışılmadıysa null.</summary>
    public int? LastStudiedDeckId { get; set; }

    /// <summary>Bu dilde en son çalışılan kelime; hiç çalışılmadıysa null.</summary>
    public int? LastStudiedWordId { get; set; }

    public DateTime? LastStudiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Yeni bir dil ilk kez seçildiğinde açılan kurulum penceresinin sonucu:
/// seviye ölçümü ve ilgi alanları, tek istekte.
///
/// İkisi birlikte gönderiliyor çünkü ikisi de aynı sonucu belirliyor — hangi
/// destelerin hangi kelimelerle kurulacağını. Ayrı ayrı gönderilseydi arada
/// yarım kurulmuş bir kitaplık durumu olurdu.
/// </summary>
public class CompleteLanguageSetupDto
{
    /// <summary>Just Starting / Beginner / Intermediate / Advanced / Fluent.</summary>
    [Required]
    [RegularExpression(
        "^(Just Starting|Beginner|Intermediate|Advanced|Fluent)$",
        ErrorMessage = "ProficiencyLevel must be Just Starting, Beginner, Intermediate, Advanced, or Fluent.")]
    public string ProficiencyLevel { get; set; } = string.Empty;

    /// <summary>
    /// İsteğe bağlı CEFR tavanı. Boş bırakılırsa seviyeden türetilir
    /// (Just Starting → A1, Beginner → A2, Intermediate → B2, Advanced → C2).
    /// </summary>
    [RegularExpression(
        "^$|^(A1|A2|B1|B1\\+|B2|C1|C2)$",
        ErrorMessage = "DifficultyMode must be a CEFR level between A1 and C2.")]
    public string? DifficultyMode { get; set; }

    /// <summary>
    /// Bu dilde çalışılacak kategoriler. Boş liste geçerlidir: kullanıcı hiç
    /// kategori seçmeyebilir ve o zaman kategori destesi kurulmaz.
    /// </summary>
    public List<int> CategoryIds { get; set; } = new();
}

/// <summary>
/// Hedef dili değiştirme isteği. Dil profili yoksa açılır ve
/// <see cref="LanguageProfileDto.IsSetupCompleted"/> false döner — istemcinin
/// kurulum penceresini açması gerektiğinin işareti budur.
/// </summary>
public class SwitchTargetLanguageDto
{
    [Required]
    [MaxLength(8)]
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Dilin görünen adı. Boş bırakılırsa dil katalogundan okunur; katalogda da
    /// yoksa kod olduğu gibi ad olarak saklanır.
    /// </summary>
    [MaxLength(100)]
    public string? LanguageName { get; set; }
}
