using System.ComponentModel.DataAnnotations;

namespace VocabGrid.Entities;

/// <summary>
/// Bir kullanıcının <em>tek bir hedef dildeki</em> öğrenme durumu.
///
/// Bugüne kadar seviye, seri, XP ve seviye numarası doğrudan
/// <see cref="User"/> satırında duruyordu. Tek dil öğrenildiği sürece bu
/// yeterliydi; ikinci bir dile geçildiğinde ise ilk dilin serisi ve XP'si
/// yeni dile devrediyor, seviye ölçümü de tek bir alan üzerine yazılıyordu.
/// Öğrenenin Almancada B2, yeni başladığı Japoncada A1 olması mümkün
/// değildi.
///
/// Bu tablo o durumu dil başına tek satıra ayırır. <see cref="User"/>
/// üzerindeki alanlar silinmedi: hesabın tamamına ait toplamlar (tüm
/// dillerdeki XP, en uzun seri) orada kalır ve rozet değerlendirmesi
/// oradan okur. Buradaki sayılar tek bir dile aittir.
///
/// Satır, o dil ilk kez hedef seçildiğinde oluşur ve
/// <see cref="IsSetupCompleted"/> false başlar — istemci bunu görünce
/// seviye ölçümü ve kategori seçimi penceresini açar.
/// </summary>
public class UserLanguageProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Hedef dilin ISO kodu — <c>de</c>, <c>ja</c>.</summary>
    [MaxLength(8)]
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Dilin görünen adı, satır oluşturulduğu andaki hâliyle. Dil katalogda
    /// da var; burada tekrar edilmesinin nedeni, kullanıcının artık aktif
    /// olmayan bir dili öğrenmiş olması durumunda listenin yine de okunabilir
    /// kalması.
    /// </summary>
    [MaxLength(100)]
    public string LanguageName { get; set; } = string.Empty;

    /// <summary>Just Starting / Beginner / Intermediate / Advanced / Fluent.</summary>
    [MaxLength(20)]
    public string ProficiencyLevel { get; set; } = "Beginner";

    /// <summary>
    /// Bu dildeki kelime seçimi tavanı (CEFR: A1..C2). Ayarlardaki genel
    /// <c>UserSettings.DifficultyMode</c> ile aynı ölçek, ama dile özel:
    /// aynı kişi bir dilde A1, diğerinde B2 olabilir.
    /// </summary>
    [MaxLength(20)]
    public string DifficultyMode { get; set; } = "B1";

    /// <summary>
    /// Seviye ölçümü ve kategori seçimi tamamlandı mı. False olduğu sürece
    /// istemci bu dile geçişte kurulum penceresini açar.
    /// </summary>
    public bool IsSetupCompleted { get; set; }

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalXp { get; set; }
    public int Level { get; set; } = 1;

    /// <summary>
    /// Bu dilde en son çalışılan deste. Ana ekrandaki "öğrenmeye devam et"
    /// kartı buradan okunur; deste silinirse alan null'a düşer ve kart
    /// kitaplıktaki ilk desteye geri döner.
    /// </summary>
    public int? LastStudiedDeckId { get; set; }
    public Deck? LastStudiedDeck { get; set; }

    /// <summary>Bu dilde en son çalışılan kelime — tekrar listesinin başlangıcı.</summary>
    public int? LastStudiedWordId { get; set; }
    public Vocabulary? LastStudiedWord { get; set; }

    public DateTime? LastStudiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
