namespace VocabGrid.Entities;

/// <summary>
/// Kullanıcının bir hedef dilde seçtiği ilgi alanı.
///
/// Seçim dile bağlı: Japonca çalışırken "Anime/Kültür", Almanca çalışırken
/// "İş" istemek tutarsızlık değil, olağan durumdur. Kategori desteleri de
/// zaten dil başına kuruluyor (<c>category_&lt;slug&gt;_&lt;dil&gt;</c>), yani
/// seçimi dilden bağımsız tutmak ikisini birbirinden koparıyordu.
/// </summary>
public class UserCategory
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Seçimin geçerli olduğu hedef dilin ISO kodu. Boş string, bu alandan
    /// önce yapılmış ve hangi dile ait olduğu bilinmeyen seçimleri işaret
    /// eder; geçişte kullanıcının o günkü hedef diline taşındılar.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;
}
