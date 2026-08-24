using System.ComponentModel.DataAnnotations;

namespace VocabGrid.DTOs;

public class CreateDeckDto
{
    [Required]
    [MaxLength(50)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// Yalnızca uygulamanın oluşturduğu başlangıç desteleri doldurur
    /// ("basics_DE"). Kullanıcının kendi destesinde boş kalır.
    /// </summary>
    [MaxLength(40)]
    public string? StarterKey { get; set; }

    /// <summary>
    /// Destenin öğrettiği hedef dil. Boş bırakılırsa kullanıcının o anki
    /// hedef dili yazılır — istemcinin her deste oluşturmada dili tekrar
    /// göndermesi gerekmesin diye. Açıkça göndermek, dil değiştirdikten
    /// hemen sonra oluşturulan destenin doğru dile düşmesini garanti eder.
    /// </summary>
    [MaxLength(8)]
    public string? LanguageCode { get; set; }
}

public class UpdateDeckDto
{
    [Required]
    [MaxLength(50)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }
}
