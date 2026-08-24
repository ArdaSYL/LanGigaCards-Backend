using Microsoft.EntityFrameworkCore;
using VocabGrid.Entities;

namespace VocabGrid.Data;

/// <summary>
/// Dil ve etiket kataloğunun ilişkileri ile başlangıç verisi.
///
/// <see cref="CurriculumSeedData"/> ders içeriğini taşır; burası ise içeriğin
/// sınıflandırıldığı sabit listeleri. İkisini ayrı tutmanın nedeni değişme
/// hızları: müfredat sürekli büyür, bu listeler nadiren.
/// </summary>
internal static class CatalogSeedData
{
    internal static void Apply(ModelBuilder modelBuilder)
    {
        ConfigureRelations(modelBuilder);
        SeedLanguages(modelBuilder);
        SeedTags(modelBuilder);
        SeedVocabularyTags(modelBuilder);
    }

    private static void ConfigureRelations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Language>(e =>
        {
            // Ad üzerinde benzersizlik: kod farklı ama adı aynı iki satır,
            // seçim listesinde ayırt edilemeyen bir çift üretirdi.
            e.HasIndex(l => l.Name).IsUnique();
            e.HasIndex(l => new { l.IsActive, l.SortOrder });
            e.ToTable(t => t.HasCheckConstraint("CK_Language_SortOrder", "[SortOrder] >= 0"));
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.ToTable(t => t.HasCheckConstraint(
                "CK_Tag_Kind",
                "[Kind] IN ('Grammar', 'Register', 'Difficulty')"));
        });

        modelBuilder.Entity<VocabularyTag>(e =>
        {
            e.HasKey(vt => new { vt.WordID, vt.TagId });

            e.HasOne(vt => vt.Vocabulary)
                .WithMany(v => v.VocabularyTags)
                .HasForeignKey(vt => vt.WordID)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(vt => vt.Tag)
                .WithMany(t => t.VocabularyTags)
                .HasForeignKey(vt => vt.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            // "Bu etiketteki kelimeler" sorgusu birleşik anahtarın ikinci
            // sütunuyla başlıyor, yani anahtarın kendisi bu yönde işe yaramaz.
            e.HasIndex(vt => vt.TagId);
        });

        modelBuilder.Entity<DailyStudySummary>(e =>
        {
            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bir kullanıcının bir günü ve bir dili için tek satır.
            // Benzersizlik burada yalnızca bir kural değil, doğruluk şartı:
            // ikinci bir satır oluşabilseydi aynı gün iki kez sayılırdı.
            //
            // Dil de anahtara dahil, çünkü istatistik dil başına ayrıldı: aynı
            // gün hem Almanca hem Japonca çalışan biri iki satır üretir ve
            // ekran hangi dili gösteriyorsa onu okur.
            e.HasIndex(s => new { s.UserId, s.LanguageCode, s.Day }).IsUnique();

            e.ToTable(t => t.HasCheckConstraint(
                "CK_DailyStudySummary_Counters",
                "[ReviewCount] >= 0 AND [CorrectCount] >= 0 AND [QuizCount] >= 0 AND [LessonCount] >= 0 " +
                "AND [StudySeconds] >= 0 AND [XpEarned] >= 0 AND [CorrectCount] <= [ReviewCount]"));
        });
    }

    /// <summary>
    /// İstemcinin bugün gömülü olarak taşıdığı on dil. Sıra yaygınlığa göre;
    /// bayrak kodu dil kodundan ayrı tutuluyor çünkü ikisi her zaman
    /// örtüşmüyor (en → gb, ja → jp, ko → kr, zh → cn).
    /// </summary>
    private static void SeedLanguages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Language>().HasData(
            new Language { Code = "en", Name = "English",    NativeName = "English",    FlagCode = "gb", SortOrder = 1 },
            new Language { Code = "tr", Name = "Turkish",    NativeName = "Türkçe",     FlagCode = "tr", SortOrder = 2 },
            new Language { Code = "es", Name = "Spanish",    NativeName = "Español",    FlagCode = "es", SortOrder = 3 },
            new Language { Code = "fr", Name = "French",     NativeName = "Français",   FlagCode = "fr", SortOrder = 4 },
            new Language { Code = "de", Name = "German",     NativeName = "Deutsch",    FlagCode = "de", SortOrder = 5 },
            new Language { Code = "it", Name = "Italian",    NativeName = "Italiano",   FlagCode = "it", SortOrder = 6 },
            new Language { Code = "pt", Name = "Portuguese", NativeName = "Português",  FlagCode = "pt", SortOrder = 7 },
            new Language { Code = "ja", Name = "Japanese",   NativeName = "日本語",      FlagCode = "jp", SortOrder = 8 },
            new Language { Code = "ko", Name = "Korean",     NativeName = "한국어",      FlagCode = "kr", SortOrder = 9 },
            new Language { Code = "zh", Name = "Chinese",    NativeName = "中文",        FlagCode = "cn", SortOrder = 10 }
        );
    }

    private static void SeedTags(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tag>().HasData(
            new Tag { Id = 1,  Name = "Irregular verb",  Slug = "irregular-verb",  Kind = "Grammar",    Description = "Çekimi kurala uymayan fiiller" },
            new Tag { Id = 2,  Name = "Phrasal verb",    Slug = "phrasal-verb",    Kind = "Grammar",    Description = "Edatla anlam değiştiren fiiller" },
            new Tag { Id = 3,  Name = "Noun",            Slug = "noun",            Kind = "Grammar",    Description = "İsim" },
            new Tag { Id = 4,  Name = "Adjective",       Slug = "adjective",       Kind = "Grammar",    Description = "Sıfat" },
            new Tag { Id = 5,  Name = "Adverb",          Slug = "adverb",          Kind = "Grammar",    Description = "Zarf" },
            new Tag { Id = 6,  Name = "Preposition",     Slug = "preposition",     Kind = "Grammar",    Description = "Edat" },
            new Tag { Id = 7,  Name = "Formal",          Slug = "formal",          Kind = "Register",   Description = "Resmî dilde kullanılır" },
            new Tag { Id = 8,  Name = "Informal",        Slug = "informal",        Kind = "Register",   Description = "Günlük konuşma dili" },
            new Tag { Id = 9,  Name = "Slang",           Slug = "slang",           Kind = "Register",   Description = "Argo" },
            new Tag { Id = 10, Name = "Academic",        Slug = "academic",        Kind = "Register",   Description = "Akademik metinlerde geçer" },
            new Tag { Id = 11, Name = "False friend",    Slug = "false-friend",    Kind = "Difficulty", Description = "Ana dile benzeyip anlamı farklı olan kelimeler" },
            new Tag { Id = 12, Name = "Common mistake",  Slug = "common-mistake",  Kind = "Difficulty", Description = "Öğrenenlerin sık karıştırdığı kelimeler" },
            new Tag { Id = 13, Name = "High frequency",  Slug = "high-frequency",  Kind = "Difficulty", Description = "Günlük dilde en sık geçen kelimeler" },
            new Tag { Id = 14, Name = "Advanced",        Slug = "advanced",        Kind = "Difficulty", Description = "İleri seviye kelime dağarcığı" }
        );
    }

    /// <summary>
    /// Müfredat kelimelerinin etiketleri.
    ///
    /// Atamalar kelime kelime değil aralık aralık yapılıyor, çünkü müfredat
    /// zaten derse göre gruplu ve her dersin kelimeleri aynı türden: renkler
    /// sıfat, aile üyeleri isim, "left/right/straight" yön zarfı. Aralıklar
    /// tahmin değil — her dersin kelime listesi tek tek okunup doğrulandı,
    /// tür değiştiren yerlerde aralık bölündü (4. derste gün adları isim ama
    /// "today/tomorrow/yesterday" zarf; 7. derste yön kelimeleri ayrıldı).
    ///
    /// "High frequency" yalnızca A1 derslerine verildi: A2 içeriği tanım
    /// gereği daha seyrek geçen kelimeler.
    /// </summary>
    private static void SeedVocabularyTags(ModelBuilder modelBuilder)
    {
        const int Noun = 3;
        const int Adjective = 4;
        const int Adverb = 5;
        const int Academic = 10;
        const int HighFrequency = 13;

        var links = new List<VocabularyTag>();

        void Link(int firstWordId, int lastWordId, params int[] tagIds)
        {
            for (var wordId = firstWordId; wordId <= lastWordId; wordId++)
            {
                foreach (var tagId in tagIds)
                {
                    links.Add(new VocabularyTag { WordID = wordId, TagId = tagId });
                }
            }
        }

        Link(1001, 1012, HighFrequency);             // Ders 1 — selamlaşma kalıpları
        Link(1013, 1024, Noun, HighFrequency);       // Ders 2 — yiyecek ve içecek
        Link(1025, 1036, HighFrequency);             // Ders 3 — sayılar
        Link(1037, 1039, Adverb, HighFrequency);     // Ders 4 — today / tomorrow / yesterday
        Link(1040, 1048, Noun, HighFrequency);       // Ders 4 — gün adları, week, month
        Link(1049, 1060, Noun, HighFrequency);       // Ders 5 — aile
        Link(1061, 1070, Adjective, HighFrequency);  // Ders 6 — renkler
        Link(1071, 1074, Noun);                      // Ders 7 — airport, station, ticket, hotel
        Link(1075, 1077, Adverb);                    // Ders 7 — left, right, straight
        Link(1078, 1082, Noun);                      // Ders 7 — map, luggage, passport, road, bridge
        Link(1083, 1094, Noun, Academic);            // Ders 8 — iş ve okul
        Link(1095, 1106, Noun);                      // Ders 9 — vücut ve sağlık
        Link(1107, 1118, Noun);                      // Ders 10 — ev ve günlük hayat

        modelBuilder.Entity<VocabularyTag>().HasData(links);
    }
}
