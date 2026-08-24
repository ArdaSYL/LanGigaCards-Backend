using System.ComponentModel.DataAnnotations;

namespace VocabGrid.Entities;

/// <summary>
/// Şablondaki tek bir kavram — "kelime"nin dilden bağımsız hâli.
///
/// Metnin kendisi <see cref="DeckTemplateWordText"/> satırlarında durur; burada
/// yalnızca kavramın kimliği, deste içindeki sırası ve seviyesi vardır. Kavramı
/// bu şekilde tutmak, aynı tablodan herhangi bir dil çiftini üretmeyi mümkün
/// kılar: Türkçe konuşan biri "Keyboard / Klavye", İngilizce konuşan biri
/// "キーボード / Keyboard" alır.
/// </summary>
public class DeckTemplateWord
{
    public int Id { get; set; }

    public int DeckTemplateId { get; set; }
    public DeckTemplate DeckTemplate { get; set; } = null!;

    /// <summary>Deste içindeki sıra (1'den başlar).</summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// Kelimenin CEFR seviyesi: <c>A1</c>, <c>A2</c>, <c>B1</c>, <c>B1+</c>,
    /// <c>B2</c>, <c>C1</c>, <c>C2</c>. İstemcideki <c>DifficultyMode</c>
    /// enum'ıyla aynı etiketler (bkz. <c>text_size_option.dart</c>).
    ///
    /// Deste kurulurken öğrenenin seçtiği seviye <em>ve altı</em> alınır:
    /// CEFR birikimlidir, B2 çalışan biri A1 kelimesini de bilmek durumundadır.
    /// Bu yüzden alt seviyelerde daha çok kelime var — onlar her deste için
    /// taban oluşturur.
    /// </summary>
    [MaxLength(4)]
    public string CefrLevel { get; set; } = "A1";

    public ICollection<DeckTemplateWordText> Texts { get; set; } = new List<DeckTemplateWordText>();
}
