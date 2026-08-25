using Microsoft.EntityFrameworkCore;
using VocabGrid.Entities;

namespace VocabGrid.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<UserLanguageProfile> UserLanguageProfiles { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<QuizOption> QuizOptions { get; set; }
        public DbSet<QuizSession> QuizSessions { get; set; }
        public DbSet<QuizSessionAnswer> QuizSessionAnswers { get; set; }
        public DbSet<LessonVocabulary> LessonVocabularies { get; set; }
        public DbSet<UserProgress> UserProgresses { get; set; }
        public DbSet<UserWordProgress> UserWordProgresses { get; set; }
        public DbSet<Vocabulary> Vocabularies { get; set; }
        public DbSet<Badge> Badges { get; set; }
        public DbSet<UserBadge> UserBadges { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<UserCategory> UserCategories { get; set; }
        public DbSet<LearningPurpose> LearningPurposes { get; set; }
        public DbSet<UserLearningPurpose> UserLearningPurposes { get; set; }
        public DbSet<Deck> Decks { get; set; }
        public DbSet<StudyActivity> StudyActivities { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<VocabularyTag> VocabularyTags { get; set; }
        public DbSet<DailyStudySummary> DailyStudySummaries { get; set; }
        public DbSet<DeckTemplate> DeckTemplates { get; set; }
        public DbSet<DeckTemplateLabel> DeckTemplateLabels { get; set; }
        public DbSet<DeckTemplateWord> DeckTemplateWords { get; set; }
        public DbSet<DeckTemplateWordText> DeckTemplateWordTexts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Sütun uzunlukları, arama indeksleri ve CHECK kısıtları — kendi
            // dosyasında, çünkü burası zaten uzun ve ikisi farklı türde bilgi:
            // burada ilişkiler ve seed, orada veri bütünlüğü kuralları.
            SchemaConfiguration.Apply(modelBuilder);

            // Dil ve etiket katalogları: ilişkileri ve sabit listeleri.
            CatalogSeedData.Apply(modelBuilder);

            // Silinmiş hesaplar hiçbir sorguda görünmez. Süzgeç burada, tek
            // yerde duruyor: her denetleyicide "ve silinmemiş olsun" koşulunu
            // tekrar etmek, bir yerde unutulduğu anda silinmiş bir hesaba
            // giriş yapılabilmesi demekti.
            modelBuilder.Entity<User>()
                .HasQueryFilter(u => !u.IsDeleted);

            // E-posta benzersizliği yalnızca canlı hesaplar arasında. Silinen
            // satır e-postasıyla birlikte duruyor; süzgeçsiz bir indeks, aynı
            // adresle yeniden kayıt olmayı sonsuza dek engellerdi.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.GoogleId)
                .IsUnique()
                .HasFilter("[GoogleId] IS NOT NULL AND [IsDeleted] = 0");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.AppleId)
                .IsUnique()
                .HasFilter("[AppleId] IS NOT NULL AND [IsDeleted] = 0");

            modelBuilder.Entity<UserSettings>()
                .HasOne(s => s.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSettings>()
                .HasIndex(s => s.UserId)
                .IsUnique();

            modelBuilder.Entity<LessonVocabulary>()
                .HasKey(lv => new { lv.LessonID, lv.WordID });

            modelBuilder.Entity<LessonVocabulary>()
                .HasOne(lv => lv.Lesson)
                .WithMany(l => l.LessonVocabularies)
                .HasForeignKey(lv => lv.LessonID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LessonVocabulary>()
                .HasOne(lv => lv.Vocabulary)
                .WithMany(v => v.LessonVocabularies)
                .HasForeignKey(lv => lv.WordID)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction on User: SQL Server rejects Users->UWP and Users->Decks->Vocabularies->UWP both Cascade.
            // Progress rows still cascade-delete when the Vocabulary (card) is removed with the Deck.
            modelBuilder.Entity<UserWordProgress>()
                .HasOne(uwp => uwp.User)
                .WithMany(u => u.UserWordProgresses)
                .HasForeignKey(uwp => uwp.UserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserWordProgress>()
                .HasOne(uwp => uwp.Vocabulary)
                .WithMany(v => v.UserWordProgresses)
                .HasForeignKey(uwp => uwp.WordID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserWordProgress>()
                .HasIndex(uwp => new { uwp.UserID, uwp.WordID })
                .IsUnique();

            modelBuilder.Entity<UserProgress>()
                .HasOne(up => up.User)
                .WithMany(u => u.UserProgresses)
                .HasForeignKey(up => up.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserProgress>()
                .HasOne(up => up.Lesson)
                .WithMany(l => l.UserProgresses)
                .HasForeignKey(up => up.LessonID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserProgress>()
                .HasIndex(up => new { up.UserID, up.LessonID })
                .IsUnique();

            modelBuilder.Entity<UserLanguageProfile>()
                .HasOne(p => p.User)
                .WithMany(u => u.LanguageProfiles)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deste/kelime silinince profil satırı kalır, işaret ettiği kayıt
            // null'a düşer: "en son çalışılan" bilgisi kaybolur ama dilin
            // serisi, XP'si ve seviyesi silinen bir desteyle birlikte gitmez.
            modelBuilder.Entity<UserLanguageProfile>()
                .HasOne(p => p.LastStudiedDeck)
                .WithMany()
                .HasForeignKey(p => p.LastStudiedDeckId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserLanguageProfile>()
                .HasOne(p => p.LastStudiedWord)
                .WithMany()
                .HasForeignKey(p => p.LastStudiedWordId)
                .OnDelete(DeleteBehavior.NoAction);

            // Bir kullanıcının bir hedef dilde tek profili olur; ikinci satır
            // istatistiği ikiye bölerdi. Ana dil artık anahtarın parçası
            // değil -- bkz. UserLanguageProfile'ın kendi belgesindeki gerekçe.
            modelBuilder.Entity<UserLanguageProfile>()
                .HasIndex(p => new { p.UserId, p.LanguageCode })
                .IsUnique();

            // Dil, bileşik anahtarın parçası: aynı kategori iki farklı dilde
            // ayrı ayrı seçilebilmeli.
            modelBuilder.Entity<UserCategory>()
                .HasKey(uc => new { uc.UserId, uc.CategoryId, uc.LanguageCode });

            modelBuilder.Entity<UserCategory>()
                .HasOne(uc => uc.User)
                .WithMany(u => u.UserCategories)
                .HasForeignKey(uc => uc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserCategory>()
                .HasOne(uc => uc.Category)
                .WithMany(c => c.UserCategories)
                .HasForeignKey(uc => uc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserLearningPurpose>()
                .HasKey(ulp => new { ulp.UserId, ulp.LearningPurposeId });

            modelBuilder.Entity<UserLearningPurpose>()
                .HasOne(ulp => ulp.User)
                .WithMany(u => u.UserLearningPurposes)
                .HasForeignKey(ulp => ulp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserLearningPurpose>()
                .HasOne(ulp => ulp.LearningPurpose)
                .WithMany(lp => lp.UserLearningPurposes)
                .HasForeignKey(ulp => ulp.LearningPurposeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserBadge>()
                .HasKey(ub => new { ub.UserId, ub.BadgeId });

            modelBuilder.Entity<UserBadge>()
                .HasOne(ub => ub.User)
                .WithMany(u => u.UserBadges)
                .HasForeignKey(ub => ub.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserBadge>()
                .HasOne(ub => ub.Badge)
                .WithMany(b => b.UserBadges)
                .HasForeignKey(ub => ub.BadgeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Deck>()
                .HasOne(d => d.User)
                .WithMany(u => u.Decks)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Deck>()
                .Property(d => d.Title)
                .HasMaxLength(50);

            // Figma: deck silinince kartlar da silinir. SQL Server multiple-cascade-path
            // kısıtı nedeniyle DB'de NoAction; EF ClientCascade ile kartları önce siler.
            modelBuilder.Entity<Vocabulary>()
                .HasOne(v => v.Deck)
                .WithMany(d => d.Flashcards)
                .HasForeignKey(v => v.DeckId)
                .OnDelete(DeleteBehavior.ClientCascade);

            modelBuilder.Entity<Quiz>()
                .HasOne(q => q.Lesson)
                .WithMany(l => l.Quizzes)
                .HasForeignKey(q => q.LessonID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizOption>()
                .HasOne(o => o.Quiz)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuizID)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction: same SQL Server multiple-cascade-path limit as StudyActivity (via Decks).
            modelBuilder.Entity<QuizSession>()
                .HasOne(s => s.User)
                .WithMany(u => u.QuizSessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<QuizSession>()
                .HasOne(s => s.Lesson)
                .WithMany()
                .HasForeignKey(s => s.LessonId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuizSession>()
                .HasOne(s => s.Deck)
                .WithMany()
                .HasForeignKey(s => s.DeckId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuizSessionAnswer>()
                .HasOne(a => a.QuizSession)
                .WithMany(s => s.Answers)
                .HasForeignKey(a => a.QuizSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizSessionAnswer>()
                .HasOne(a => a.Quiz)
                .WithMany()
                .HasForeignKey(a => a.QuizId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuizSessionAnswer>()
                .HasOne(a => a.Word)
                .WithMany()
                .HasForeignKey(a => a.WordId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuizSessionAnswer>()
                .HasOne(a => a.SelectedOption)
                .WithMany()
                .HasForeignKey(a => a.SelectedOptionId)
                .OnDelete(DeleteBehavior.NoAction);

            // NoAction: SQL Server rejects multiple cascade paths (Users -> StudyActivities and Users -> Decks -> StudyActivities).
            modelBuilder.Entity<StudyActivity>()
                .HasOne(a => a.User)
                .WithMany(u => u.StudyActivities)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<StudyActivity>()
                .HasOne(a => a.Vocabulary)
                .WithMany()
                .HasForeignKey(a => a.WordId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudyActivity>()
                .HasOne(a => a.Lesson)
                .WithMany()
                .HasForeignKey(a => a.LessonId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudyActivity>()
                .HasOne(a => a.Deck)
                .WithMany()
                .HasForeignKey(a => a.DeckId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudyActivity>()
                .HasIndex(a => new { a.UserId, a.OccurredAt });

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => t.Token)
                .IsUnique();

            modelBuilder.Entity<EmailVerificationToken>()
                .HasOne(t => t.User)
                .WithMany(u => u.EmailVerificationTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmailVerificationToken>()
                .Property(t => t.Code)
                .HasMaxLength(6);

            // Deliberately NOT unique: a 6-digit code is short enough that two
            // users can legitimately hold the same one at the same time. Lookups
            // are always scoped by UserId, so this index only needs to make
            // "find this user's live codes" fast.
            modelBuilder.Entity<EmailVerificationToken>()
                .HasIndex(t => new { t.UserId, t.Code });

            // Figma Profile categories (Science dahil) — IconName/ColorHex Flutter mock ile hizalı
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Food", Description = "Food & Dining Vocabulary", IconName = "restaurant", ColorHex = "#F97316" },
                new Category { Id = 2, Name = "Travel", Description = "Travel & Tourism Vocabulary", IconName = "flight", ColorHex = "#3B82F6" },
                new Category { Id = 3, Name = "Business", Description = "Professional & Work Vocabulary", IconName = "work", ColorHex = "#6366F1" },
                new Category { Id = 4, Name = "Technology", Description = "Tech & IT Vocabulary", IconName = "laptop_mac", ColorHex = "#06B6D4" },
                new Category { Id = 5, Name = "Education", Description = "Academic & School Vocabulary", IconName = "school", ColorHex = "#8B5CF6" },
                new Category { Id = 6, Name = "Movies", Description = "Cinema & Entertainment", IconName = "local_movies", ColorHex = "#EC4899" },
                new Category { Id = 7, Name = "Music", Description = "Music & Songs", IconName = "music_note", ColorHex = "#F43F5E" },
                new Category { Id = 8, Name = "Gaming", Description = "Video Games & Gaming Culture", IconName = "sports_esports", ColorHex = "#10B981" },
                new Category { Id = 9, Name = "Sports", Description = "Sports & Fitness", IconName = "sports_soccer", ColorHex = "#22C55E" },
                new Category { Id = 10, Name = "Health", Description = "Health & Medicine", IconName = "favorite", ColorHex = "#EF4444" },
                new Category { Id = 11, Name = "Shopping", Description = "Shopping & Fashion", IconName = "shopping_bag", ColorHex = "#F59E0B" },
                new Category { Id = 12, Name = "Family", Description = "Family & Relationships", IconName = "family_restroom", ColorHex = "#14B8A6" },
                new Category { Id = 13, Name = "Nature", Description = "Nature & Environment", IconName = "park", ColorHex = "#84CC16" },
                new Category { Id = 14, Name = "Science", Description = "Science & Research", IconName = "science", ColorHex = "#0EA5E9" },
                new Category { Id = 15, Name = "Animals", Description = "Animals & Wildlife", IconName = "pets", ColorHex = "#A855F7" }
            );

            // Figma: Learning Purpose · 2 selected (çoklu seçim)
            modelBuilder.Entity<LearningPurpose>().HasData(
                new LearningPurpose { Id = 1, Name = "Travel", Description = "Travel and tourism" },
                new LearningPurpose { Id = 2, Name = "Business", Description = "Work and professional use" },
                new LearningPurpose { Id = 3, Name = "Academic", Description = "School and exams" },
                new LearningPurpose { Id = 4, Name = "Daily Conversation", Description = "Everyday speaking" },
                new LearningPurpose { Id = 5, Name = "Culture", Description = "Media, culture and hobbies" },
                new LearningPurpose { Id = 6, Name = "Relocation", Description = "Living abroad" }
            );

            modelBuilder.Entity<Badge>().HasData(
                new Badge
                {
                    Id = 1,
                    Name = "7-Day Streak",
                    Description = "Study 7 days in a row",
                    Icon = "flame_icon",
                    UnlockCondition = "StreakDays",
                    Threshold = 7
                },
                new Badge
                {
                    Id = 2,
                    Name = "Perfect Score",
                    Description = "Get 100% on a quiz",
                    Icon = "star_icon",
                    UnlockCondition = "PerfectQuiz",
                    Threshold = 100
                },
                new Badge
                {
                    Id = 3,
                    Name = "Word Collector",
                    Description = "Learn 100 words",
                    Icon = "book_icon",
                    UnlockCondition = "WordsLearned",
                    Threshold = 100
                },
                new Badge
                {
                    Id = 4,
                    Name = "Speed Learner",
                    Description = "Finish 20 cards in 5 min",
                    Icon = "zap_icon",
                    UnlockCondition = "CardsInMinutes",
                    Threshold = 20
                },
                new Badge
                {
                    Id = 5,
                    Name = "Polyglot",
                    Description = "Start a 2nd language",
                    Icon = "globe_icon",
                    UnlockCondition = "LanguagesStarted",
                    Threshold = 2
                }
            );

            // Sample lessons + quiz bank so QuizController can be exercised in Dev/Swagger.
            var seedCreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<Lesson>().HasData(
                new Lesson
                {
                    LessonID = 1,
                    Title = "Greetings",
                    Description = "Basic hello and introductions",
                    Level = "A1",
                    OrderIndex = 1,
                    CreatedAt = seedCreatedAt
                },
                new Lesson
                {
                    LessonID = 2,
                    Title = "Food Basics",
                    Description = "Common food and drink words",
                    Level = "A1",
                    OrderIndex = 2,
                    CreatedAt = seedCreatedAt
                }
            );

            // Lessons 3-10 plus the vocabulary behind every lesson, including
            // the two seeded above — see CurriculumSeedData for why it lives in
            // its own file.
            CurriculumSeedData.Apply(modelBuilder);

            // 11-20. dersler ve 120 kelimelik B1-B2 bloğu.
            CurriculumSeedDataB1.Apply(modelBuilder);

            // Kategori deste şablonları: kullanıcının kategori seçimine göre
            // kopyalanan hazır desteler. Müfredattan ayrı durur, çünkü bunlar
            // bir derse değil bir kategoriye bağlıdır ve paylaşılmak yerine
            // kullanıcıya kopyalanırlar.
            DeckTemplateSeedData.Apply(modelBuilder);

            modelBuilder.Entity<Quiz>().HasData(
                new Quiz { QuizID = 1, LessonID = 1, QuestionText = "What does 'Merhaba' mean?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 2, LessonID = 1, QuestionText = "How do you say 'Good morning' in Turkish?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 3, LessonID = 1, QuestionText = "What does 'Teşekkürler' mean?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 4, LessonID = 1, QuestionText = "How do you say 'My name is...' in Turkish?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 5, LessonID = 1, QuestionText = "What does 'Güle güle' mean?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 6, LessonID = 2, QuestionText = "What does 'Elma' mean?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 7, LessonID = 2, QuestionText = "How do you say 'Water' in Turkish?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 8, LessonID = 2, QuestionText = "What does 'Ekmek' mean?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 9, LessonID = 2, QuestionText = "How do you say 'Tea' in Turkish?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 },
                new Quiz { QuizID = 10, LessonID = 2, QuestionText = "What does 'Süt' mean?", QuestionType = "MultipleChoice", Points = 1, TimeLimitSeconds = 20 }
            );

            modelBuilder.Entity<QuizOption>().HasData(
                // Q1 Merhaba
                new QuizOption { OptionID = 1, QuizID = 1, OptionText = "Hello", IsCorrect = true },
                new QuizOption { OptionID = 2, QuizID = 1, OptionText = "Goodbye", IsCorrect = false },
                new QuizOption { OptionID = 3, QuizID = 1, OptionText = "Please", IsCorrect = false },
                new QuizOption { OptionID = 4, QuizID = 1, OptionText = "Sorry", IsCorrect = false },
                // Q2 Good morning
                new QuizOption { OptionID = 5, QuizID = 2, OptionText = "İyi geceler", IsCorrect = false },
                new QuizOption { OptionID = 6, QuizID = 2, OptionText = "Günaydın", IsCorrect = true },
                new QuizOption { OptionID = 7, QuizID = 2, OptionText = "İyi akşamlar", IsCorrect = false },
                new QuizOption { OptionID = 8, QuizID = 2, OptionText = "Hoşça kal", IsCorrect = false },
                // Q3 Teşekkürler
                new QuizOption { OptionID = 9, QuizID = 3, OptionText = "You're welcome", IsCorrect = false },
                new QuizOption { OptionID = 10, QuizID = 3, OptionText = "Thank you", IsCorrect = true },
                new QuizOption { OptionID = 11, QuizID = 3, OptionText = "Excuse me", IsCorrect = false },
                new QuizOption { OptionID = 12, QuizID = 3, OptionText = "Congratulations", IsCorrect = false },
                // Q4 My name is
                new QuizOption { OptionID = 13, QuizID = 4, OptionText = "Benim adım...", IsCorrect = true },
                new QuizOption { OptionID = 14, QuizID = 4, OptionText = "Nasılsın?", IsCorrect = false },
                new QuizOption { OptionID = 15, QuizID = 4, OptionText = "Nerelisin?", IsCorrect = false },
                new QuizOption { OptionID = 16, QuizID = 4, OptionText = "Kaç yaşındasın?", IsCorrect = false },
                // Q5 Güle güle
                new QuizOption { OptionID = 17, QuizID = 5, OptionText = "Welcome", IsCorrect = false },
                new QuizOption { OptionID = 18, QuizID = 5, OptionText = "Goodbye (to someone leaving)", IsCorrect = true },
                new QuizOption { OptionID = 19, QuizID = 5, OptionText = "See you tomorrow", IsCorrect = false },
                new QuizOption { OptionID = 20, QuizID = 5, OptionText = "Good night", IsCorrect = false },
                // Q6 Elma
                new QuizOption { OptionID = 21, QuizID = 6, OptionText = "Banana", IsCorrect = false },
                new QuizOption { OptionID = 22, QuizID = 6, OptionText = "Apple", IsCorrect = true },
                new QuizOption { OptionID = 23, QuizID = 6, OptionText = "Orange", IsCorrect = false },
                new QuizOption { OptionID = 24, QuizID = 6, OptionText = "Grape", IsCorrect = false },
                // Q7 Water
                new QuizOption { OptionID = 25, QuizID = 7, OptionText = "Su", IsCorrect = true },
                new QuizOption { OptionID = 26, QuizID = 7, OptionText = "Çay", IsCorrect = false },
                new QuizOption { OptionID = 27, QuizID = 7, OptionText = "Kahve", IsCorrect = false },
                new QuizOption { OptionID = 28, QuizID = 7, OptionText = "Süt", IsCorrect = false },
                // Q8 Ekmek
                new QuizOption { OptionID = 29, QuizID = 8, OptionText = "Cheese", IsCorrect = false },
                new QuizOption { OptionID = 30, QuizID = 8, OptionText = "Bread", IsCorrect = true },
                new QuizOption { OptionID = 31, QuizID = 8, OptionText = "Rice", IsCorrect = false },
                new QuizOption { OptionID = 32, QuizID = 8, OptionText = "Meat", IsCorrect = false },
                // Q9 Tea
                new QuizOption { OptionID = 33, QuizID = 9, OptionText = "Kahve", IsCorrect = false },
                new QuizOption { OptionID = 34, QuizID = 9, OptionText = "Su", IsCorrect = false },
                new QuizOption { OptionID = 35, QuizID = 9, OptionText = "Çay", IsCorrect = true },
                new QuizOption { OptionID = 36, QuizID = 9, OptionText = "Meyve suyu", IsCorrect = false },
                // Q10 Süt
                new QuizOption { OptionID = 37, QuizID = 10, OptionText = "Milk", IsCorrect = true },
                new QuizOption { OptionID = 38, QuizID = 10, OptionText = "Sugar", IsCorrect = false },
                new QuizOption { OptionID = 39, QuizID = 10, OptionText = "Salt", IsCorrect = false },
                new QuizOption { OptionID = 40, QuizID = 10, OptionText = "Honey", IsCorrect = false }
            );
        }
    }
}
