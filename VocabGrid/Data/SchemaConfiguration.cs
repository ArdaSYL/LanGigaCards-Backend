using Microsoft.EntityFrameworkCore;
using VocabGrid.Entities;

namespace VocabGrid.Data;

/// <summary>
/// Sütun uzunlukları, arama indeksleri ve CHECK kısıtları.
///
/// Bunlar başlangıçta yoktu: her metin sütunu <c>nvarchar(max)</c> olarak
/// oluşmuştu ve tek bir CHECK kısıtı bile bulunmuyordu. İkisinin de bedeli var:
///
/// <c>nvarchar(max)</c> indekslenemez, sayfa dışında saklanır ve sınırsız girdi
/// kabul eder — yani kelime araması tam tarama yapar ve bir istemci hatası
/// megabaytlarca metni tabloya yazabilir.
///
/// CHECK kısıtı olmayınca da tüm doğrulama C# katmanında kalır. Oradaki bir
/// atlama, bir SQL scripti ya da ileride yazılacak ikinci bir servis veriyi
/// sessizce bozabilir. Kısıtlar aynı kuralı veritabanının kendisinde tekrar
/// eder; DTO doğrulaması kullanıcıya düzgün bir 400 döndürmek için, kısıt ise
/// oraya hiç gelmeyen yollar için vardır.
///
/// Değer kümeleri uydurulmadı — koddan çıkarıldı (örneğin
/// <c>SubmitReviewDto.Rating</c> regex'i, onboarding'in gönderdiği seviyeler)
/// ve kısıtlar eklenmeden önce mevcut veriye karşı sınandı.
/// </summary>
internal static class SchemaConfiguration
{
    internal static void Apply(ModelBuilder modelBuilder)
    {
        ApplyMaxLengths(modelBuilder);
        ApplySearchIndexes(modelBuilder);
        ApplyCheckConstraints(modelBuilder);
    }

    /// <summary>
    /// Sınırlar mevcut verinin en uzun değerine göre değil, alanın makul
    /// üst sınırına göre seçildi; ölçülen değerler (en uzun terim 12,
    /// en uzun örnek cümle 36 karakter) bunların çok altında kalıyor.
    /// </summary>
    private static void ApplyMaxLengths(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.Username).HasMaxLength(100);
            e.Property(u => u.NativeLanguage).HasMaxLength(100);
            e.Property(u => u.TargetLanguage).HasMaxLength(100);
            e.Property(u => u.TargetProficiencyLevel).HasMaxLength(20);
            e.Property(u => u.AvatarUrl).HasMaxLength(500);
            // 512: bugünkü token 88 karakter (64 baytın base64'ü), ama üretim
            // biçimi değişirse diye pay bırakıldı.
            e.Property(u => u.RefreshToken).HasMaxLength(512);
        });

        modelBuilder.Entity<Vocabulary>(e =>
        {
            e.Property(v => v.Term).HasMaxLength(200);
            e.Property(v => v.Translation).HasMaxLength(200);
            e.Property(v => v.ExampleSentence).HasMaxLength(500);
            e.Property(v => v.ImageUrl).HasMaxLength(500);
            e.Property(v => v.AudioUrl).HasMaxLength(500);
        });

        // Uzunluklar DTO'lardaki [MaxLength] ile aynı: DTO kamuya açık sözleşme,
        // sütun ondan dar olursa istemci doğrulamadan geçen bir isteği
        // veritabanı reddeder ve düzgün bir 400 yerine 500 alınır.
        modelBuilder.Entity<Deck>(e =>
        {
            e.Property(d => d.Description).HasMaxLength(500);
            e.Property(d => d.CoverImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Lesson>(e =>
        {
            e.Property(l => l.Title).HasMaxLength(200);
            e.Property(l => l.Description).HasMaxLength(1000);
            e.Property(l => l.Level).HasMaxLength(20);
        });

        modelBuilder.Entity<Quiz>(e =>
        {
            e.Property(q => q.QuestionText).HasMaxLength(400);
            e.Property(q => q.QuestionType).HasMaxLength(30);
            e.Property(q => q.ImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<QuizOption>()
            .Property(o => o.OptionText).HasMaxLength(250);

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(60);
            e.Property(c => c.Description).HasMaxLength(200);
        });

        modelBuilder.Entity<LearningPurpose>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(60);
            e.Property(p => p.Description).HasMaxLength(200);
        });

        modelBuilder.Entity<Badge>(e =>
        {
            e.Property(b => b.Name).HasMaxLength(80);
            e.Property(b => b.Description).HasMaxLength(250);
            e.Property(b => b.Icon).HasMaxLength(60);
            e.Property(b => b.UnlockCondition).HasMaxLength(40);
        });

        modelBuilder.Entity<StudyActivity>(e =>
        {
            e.Property(a => a.ActivityType).HasMaxLength(30);
            e.Property(a => a.Result).HasMaxLength(30);
            e.Property(a => a.LanguageCode).HasMaxLength(8);
        });

        modelBuilder.Entity<DailyStudySummary>()
            .Property(s => s.LanguageCode).HasMaxLength(8);

        modelBuilder.Entity<QuizSession>()
            .Property(s => s.LanguageCode).HasMaxLength(8);

        // Anahtarın parçası olduğu için uzunluk zorunlu: SQL Server
        // nvarchar(max) bir sütunu birincil anahtara alamaz.
        modelBuilder.Entity<UserCategory>()
            .Property(uc => uc.LanguageCode).HasMaxLength(8);

        modelBuilder.Entity<UserSettings>(e =>
        {
            e.Property(s => s.ThemeColor).HasMaxLength(20);
            e.Property(s => s.TextSize).HasMaxLength(20);
            e.Property(s => s.DifficultyMode).HasMaxLength(20);
        });

        modelBuilder.Entity<UserWordProgress>()
            .Property(p => p.LastRating).HasMaxLength(20);
    }

    /// <summary>
    /// Kart kütüphanesi araması hem ön hem arka yüzde eşleşme arıyor
    /// (<c>FlashcardController</c>), ama iki sütun da <c>nvarchar(max)</c>
    /// olduğu için indekslenemiyordu ve her arama tam tarama yapıyordu.
    /// Uzunluk verildikten sonra indeks mümkün hale geliyor.
    ///
    /// <c>DeckId</c> indekse dahil: aramalar neredeyse her zaman bir desteyle
    /// sınırlı ve zaten tek başına <c>DeckId</c> indeksi var — birleşik indeks
    /// "bu destede şu terim" sorgusunu tek geçişte karşılıyor.
    /// </summary>
    private static void ApplySearchIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vocabulary>(e =>
        {
            e.HasIndex(v => new { v.DeckId, v.Term });
            e.HasIndex(v => v.Term);
            e.HasIndex(v => v.Translation);
        });

        // Ders listesi her zaman OrderIndex'e göre sıralanıyor.
        modelBuilder.Entity<Lesson>()
            .HasIndex(l => l.OrderIndex);

        // "Bu kullanıcının şu dildeki deste listesi, en yeni önce" — kitaplık
        // artık her zaman dile göre süzülüyor, o yüzden dil indeksin ikinci
        // sütunu. Dilsiz sorgular (dışa aktarma, hesap silme) yine ilk sütundan
        // yararlanır.
        modelBuilder.Entity<Deck>()
            .HasIndex(d => new { d.UserId, d.LanguageCode, d.CreatedAt });

        // Aynı gerekçe istatistik tarafında: aralık sorguları dil filtresiyle
        // birlikte geliyor.
        modelBuilder.Entity<StudyActivity>()
            .HasIndex(a => new { a.UserId, a.LanguageCode, a.OccurredAt });

        // Süresi geçmiş kodların temizliği ve "bu kullanıcının canlı kodu var mı"
        // sorgusu ExpiresAt üzerinden gidiyor.
        modelBuilder.Entity<EmailVerificationToken>()
            .HasIndex(t => t.ExpiresAt);
    }

    /// <summary>
    /// Her kısıt eklenmeden önce mevcut veriye karşı sayıldı; hiçbirinde ihlal
    /// yoktu. Metin kümeleri tahmin değil: <c>Rating</c> zaten
    /// <c>SubmitReviewDto</c>'daki regex ile, seviye listesi de onboarding
    /// ekranının gönderdiği değerlerle aynı.
    ///
    /// <c>UserSettings.TextSize</c>, <c>DifficultyMode</c> ve <c>ThemeColor</c>
    /// bilerek kısıtsız bırakıldı — bunları istemci serbestçe gönderiyor ve
    /// kapalı bir küme olduklarını doğrulayamadım. Yanlış bir küme, geçerli bir
    /// isteği anlaşılmaz bir 500'e çevirirdi.
    /// </summary>
    private static void ApplyCheckConstraints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Users_Level", "[Level] >= 1");
            t.HasCheckConstraint("CK_Users_DailyGoalMinutes", "[DailyGoalMinutes] BETWEEN 1 AND 600");
            t.HasCheckConstraint(
                "CK_Users_Counters",
                "[TotalXp] >= 0 AND [CurrentStreak] >= 0 AND [LongestStreak] >= 0");
            t.HasCheckConstraint(
                "CK_Users_TargetProficiencyLevel",
                "[TargetProficiencyLevel] IN ('Just Starting', 'Beginner', 'Intermediate', 'Advanced', 'Fluent')");
        });

        // Kullanıcı satırındaki kısıtların dil bazındaki karşılıkları: aynı
        // sayaçlar, aynı seviye kümesi.
        modelBuilder.Entity<UserLanguageProfile>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_UserLanguageProfile_Level", "[Level] >= 1");
            t.HasCheckConstraint(
                "CK_UserLanguageProfile_Counters",
                "[TotalXp] >= 0 AND [CurrentStreak] >= 0 AND [LongestStreak] >= 0");
            t.HasCheckConstraint(
                "CK_UserLanguageProfile_ProficiencyLevel",
                "[ProficiencyLevel] IN ('Just Starting', 'Beginner', 'Intermediate', 'Advanced', 'Fluent')");
        });

        modelBuilder.Entity<UserWordProgress>().ToTable(t =>
        {
            // 0-5: ProgressController'ın ustalık ölçeği.
            t.HasCheckConstraint("CK_UserWordProgress_MasteryLevel", "[MasteryLevel] BETWEEN 0 AND 5");
            t.HasCheckConstraint(
                "CK_UserWordProgress_Counters",
                "[ReviewCount] >= 0 AND [IntervalDays] >= 0");
            // SM-2 algoritmasının kolaylık katsayısı bu aralığın dışına çıkmaz;
            // çıktıysa hesap hatalıdır ve kaydı yazmamak doğrusudur.
            t.HasCheckConstraint("CK_UserWordProgress_EaseFactor", "[EaseFactor] BETWEEN 1.3 AND 3.0");
            t.HasCheckConstraint(
                "CK_UserWordProgress_LastRating",
                "[LastRating] IS NULL OR [LastRating] IN ('Again', 'Hard', 'Medium', 'Easy')");
        });

        modelBuilder.Entity<UserProgress>().ToTable(t =>
            t.HasCheckConstraint("CK_UserProgress_Score", "[Score] BETWEEN 0 AND 100"));

        modelBuilder.Entity<StudyActivity>().ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_StudyActivity_ActivityType",
                "[ActivityType] IN ('Review', 'Lesson', 'Quiz')");
            t.HasCheckConstraint(
                "CK_StudyActivity_Counters",
                "[DurationSeconds] >= 0 AND [XpEarned] >= 0");
        });

        modelBuilder.Entity<Lesson>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Lesson_Level", "[Level] IN ('A1', 'A2', 'B1', 'B2', 'C1', 'C2')");
            t.HasCheckConstraint("CK_Lesson_OrderIndex", "[OrderIndex] >= 0");
        });

        modelBuilder.Entity<Quiz>().ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Quiz_QuestionType",
                "[QuestionType] IN ('MultipleChoice', 'TrueFalse', 'FillInTheBlank')");
            t.HasCheckConstraint("CK_Quiz_Points", "[Points] >= 1");
            t.HasCheckConstraint("CK_Quiz_TimeLimitSeconds", "[TimeLimitSeconds] BETWEEN 5 AND 600");
        });

        modelBuilder.Entity<QuizSession>().ToTable(t =>
            t.HasCheckConstraint(
                "CK_QuizSession_Counters",
                "[TotalQuestions] >= 0 AND [CorrectCount] >= 0 AND [WrongCount] >= 0 AND [SkippedCount] >= 0 AND [ScorePoints] >= 0"));

        // Boş bir kart hiçbir işe yaramaz ama uygulamada görünür ve tekrar
        // kuyruğunda yer kaplar.
        modelBuilder.Entity<Vocabulary>().ToTable(t =>
            t.HasCheckConstraint(
                "CK_Vocabulary_NotBlank",
                "LEN(LTRIM(RTRIM([Term]))) > 0 AND LEN(LTRIM(RTRIM([Translation]))) > 0"));

        modelBuilder.Entity<Deck>().ToTable(t =>
            t.HasCheckConstraint("CK_Deck_TitleNotBlank", "LEN(LTRIM(RTRIM([Title]))) > 0"));

        modelBuilder.Entity<Badge>().ToTable(t =>
            t.HasCheckConstraint("CK_Badge_Threshold", "[Threshold] >= 0"));
    }
}
