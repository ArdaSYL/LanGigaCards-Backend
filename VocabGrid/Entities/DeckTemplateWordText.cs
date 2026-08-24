using System.ComponentModel.DataAnnotations;

namespace VocabGrid.Entities;

/// <summary>
/// Bir kavramın tek bir dildeki karşılığı.
///
/// Deste kurulurken hedef dildeki metin <c>Vocabulary.Term</c>, öğrenenin
/// kendi dilindeki metin <c>Vocabulary.Translation</c> olur.
/// </summary>
public class DeckTemplateWordText
{
    public int DeckTemplateWordId { get; set; }
    public DeckTemplateWord DeckTemplateWord { get; set; } = null!;

    /// <summary><see cref="Language.Code"/> ile aynı ISO kodu.</summary>
    [MaxLength(10)]
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary><c>Vocabulary.Term</c>/<c>Translation</c> ile aynı sınırda.</summary>
    [MaxLength(200)]
    public string Text { get; set; } = string.Empty;
}
