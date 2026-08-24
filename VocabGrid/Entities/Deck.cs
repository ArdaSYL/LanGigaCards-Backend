using System.ComponentModel.DataAnnotations;

namespace VocabGrid.Entities;

/// <summary>
/// Kullanıcıya ait öğrenme destesi.
/// CardCount, DueCount, MasteryPercentage, ReviewsCount hesaplanır; saklanmaz.
/// </summary>
public class Deck
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [MaxLength(50)]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Opsiyonel kapak görseli.</summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// Uygulamanın hesap açılışında oluşturduğu başlangıç destelerini işaretler:
    /// "basics_DE", "food_TR" gibi, konu kısaltması ve hedef dilin kodu.
    /// Kullanıcının kendi oluşturduğu destelerde null.
    ///
    /// Buna ihtiyaç var çünkü hedef dil değiştiğinde başlangıç destelerinin de
    /// yeni dille değişmesi gerekiyor ve hangi destenin uygulamadan geldiğini
    /// başlıktan anlamak güvenilmez: "Numbers" ve "Food &amp; Drink" dil adı
    /// taşımıyor, üstelik kullanıcı desteyi yeniden adlandırabiliyor.
    ///
    /// İstemci bu alanı yazar; sunucu yalnızca saklar ve geri verir. Anahtarın
    /// biçimini sunucu doğrulamaz — hangi konuların gönderileceği uygulamanın
    /// içeriğine bağlı ve burada tekrar edilmesi ikisini birbirine bağlardı.
    /// </summary>
    [MaxLength(40)]
    public string? StarterKey { get; set; }

    /// <summary>
    /// Destenin öğrettiği hedef dilin ISO kodu — <c>de</c>, <c>ja</c>.
    ///
    /// Kitaplık artık dile göre ayrılıyor: Almanca çalışan biri Japonca
    /// destelerini görmemeli, o destelerdeki tekrarlar da Almancanın
    /// istatistiğine yazılmamalı.
    ///
    /// Kod destenin üzerinde saklanıyor çünkü kullanıcının o anki hedef
    /// dilinden türetilemez — deste aylar önce, başka bir hedef dil
    /// seçiliyken kurulmuş olabilir. Bu alandan önce oluşmuş destelere geçiş
    /// sırasında sahibinin o günkü hedef dili yazıldı; null yalnızca dili
    /// belirlenemeyen satırlarda kalır ve o desteler her dilde görünür.
    /// </summary>
    [MaxLength(8)]
    public string? LanguageCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Vocabulary> Flashcards { get; set; } = new List<Vocabulary>();
}
