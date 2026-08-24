using System.ComponentModel.DataAnnotations;

namespace VocabGrid.DTOs;

public class UserProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string NativeLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string NativeLanguageCode { get; set; } = string.Empty;
    public string TargetLanguageCode { get; set; } = string.Empty;
    public string TargetProficiencyLevel { get; set; } = string.Empty;
    public int DailyGoalMinutes { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int Level { get; set; }
    public int TotalXp { get; set; }
    public bool IsPremium { get; set; }

    /// <summary>
    /// E-posta doğrulandı mı.
    ///
    /// Doğrulama zorunlu değil: kullanıcı kayıt sonrası adımı atlayıp
    /// uygulamayı kullanmaya devam edebiliyor. Bu yüzden durumun profilde
    /// görünmesi gerekiyor — hem "hesabım onaysız" bilgisini vermek, hem de
    /// sonradan tamamlamanın yolunu açık tutmak için.
    /// </summary>
    public bool IsEmailVerified { get; set; }
}

public class UpdateUserProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? NativeLanguage { get; set; }
    public string? TargetLanguage { get; set; }
    public string? NativeLanguageCode { get; set; }
    public string? TargetLanguageCode { get; set; }
    // Onboarding sihirbazının gönderdiği dört değer. Aynı küme veritabanında da
    // CHECK olarak duruyor; burası istemciye anlaşılır bir hata döndürmek için.
    //
    // Boş string bilerek kabul ediliyor: bu kısmi bir güncelleme DTO'su ve
    // denetleyici boş/null gelen alanı "değiştirme" olarak yorumluyor
    // (UserController.UpdateProfile). Boşu reddetseydik, yalnızca ana dilini
    // değiştiren bir istek de reddedilirdi.
    [RegularExpression(
        "^$|^(Just Starting|Beginner|Intermediate|Advanced|Fluent)$",
        ErrorMessage = "TargetProficiencyLevel must be Just Starting, Beginner, Intermediate, Advanced, or Fluent.")]
    public string? TargetProficiencyLevel { get; set; }

    // Alt sınır 0, 1 değil — aynı nedenle: denetleyici 0'ı "bu alana dokunma"
    // olarak okuyor. Üst sınır ise gerçek bir doğrulama; onsuz 700 gibi bir
    // değer sütundaki CHECK'e çarpıp anlaşılmaz bir 500 üretirdi.
    [Range(0, 600)]
    public int DailyGoalMinutes { get; set; }
}

public class UserSettingsDto
{
    public bool DarkMode { get; set; }
    public bool DailyReminders { get; set; }
    public bool SoundEffects { get; set; }
    public string ThemeColor { get; set; } = "Purple";
    public string TextSize { get; set; } = "Medium";
    public string DifficultyMode { get; set; } = "Adaptive";
}
