namespace VocabGrid.Services;

/// <summary>
/// Hız sınırı politikalarının adları. Sabit olarak duruyorlar çünkü aynı ad
/// iki yerde geçiyor — <c>Program.cs</c>'te tanımlandığı yerde ve
/// <c>[EnableRateLimiting]</c> ile işaretlenen uç noktada. Yazım hatası
/// yapıldığında hata çalışma zamanında çıkar, üstelik sınır uygulanmamış
/// görünmez: politika bulunamadığı için istek reddedilir.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Parola ve doğrulama kodu deneyen uç noktalar.</summary>
    public const string Credentials = "credentials";

    /// <summary>Hesap açan ya da e-posta gönderen uç noktalar.</summary>
    public const string Registration = "registration";
}
