using Microsoft.EntityFrameworkCore;

namespace VocabGrid.Services;

/// <summary>
/// Kullanıcının kategori seçimini kitaplığındaki kategori desteleriyle
/// eşitler: seçilen her kategori için şablondan bir deste kurar, seçimden
/// çıkan kategorinin destesini kaldırır.
///
/// <para>
/// Eşleşme birebirdir: kitaplıkta yalnızca seçili kategorilerin desteleri
/// bulunur. Hiçbir konuya bağlı olmayan "genel" desteler de yok — eskiden
/// istemci herkese beş tane kurardı, artık kurmuyor ve eski hesaplardakiler
/// eşitleme sırasında temizleniyor.
/// </para>
///
/// <para>
/// Eşitleme her zaman <em>tek bir hedef dille</em> sınırlıdır: hem istenen
/// anahtarlar hem de karşılaştırıldığı mevcut desteler
/// <c>category_&lt;slug&gt;_&lt;hedefDil&gt;</c> biçiminde o dile aittir.
/// Öğrenen Almancadan Japoncaya geçtiğinde Almanca desteleri silinmez,
/// yalnızca kitaplık listesinden düşer (bkz. <c>Deck.LanguageCode</c>) ve
/// geri dönüldüğünde ilerlemesiyle birlikte yerinde durur. Dil başına
/// istatistik tutulurken destelerin yok olması, sayıların dayanağını
/// ortadan kaldırırdı.
/// </para>
///
/// <para>
/// Kaldırma koşulsuzdur ve bedeli açıktır: seçimden çıkan kategorinin
/// destesindeki ilerleme kartlarla birlikte gider. Eskiden "üzerinde tekrar
/// kaydı varsa bırak" kuralı vardı; sonucu, kullanıcının kitaplıktan
/// çıkaramadığı desteler oldu. Kategoriyi seçimden çıkarmak "bu desteyi
/// istemiyorum" demektir ve öyle davranıyor. Kullanıcının kendi kurduğu
/// desteler bundan hiç etkilenmez — burası yalnızca uygulamanın ürettiği
/// <c>category_</c> önekli destelere bakar.
/// </para>
/// </summary>
internal static class CategoryDeckSynchronizer
{
    /// <summary>
    /// Kategori destelerini starter destelerden ayıran önek. Flutter tarafı
    /// <c>starter_</c> kullanır; ikisinin ayrı kalması, istemcinin kurduğu
    /// desteleri buranın yanlışlıkla silmemesini sağlar.
    /// </summary>
    internal const string StarterKeyPrefix = "category_";

    /// <summary>
    /// Flutter'ın kendi kurduğu destelerin öneki. Katalogda karşılığı olmayan
    /// slug'lar (basics, everyday, numbers, colours, time) istemciye ait kalır
    /// ve buranın onlara işi olmaz; karşılığı olanlar
    /// <see cref="AbsorbClientDecksAsync"/> tarafından devralınır.
    /// </summary>
    internal const string ClientKeyPrefix = "starter_";

    /// <summary>Bir şablon + hedef dil çiftinin kitaplıktaki kimliği.</summary>
    internal static string StarterKeyFor(string slug, string targetLanguageCode) =>
        $"{StarterKeyPrefix}{slug}_{targetLanguageCode.ToLowerInvariant()}";

    /// <summary>
    /// CEFR seviyeleri, kolaydan zora. İstemcideki <c>DifficultyMode</c>
    /// enum'ıyla aynı etiketler ve aynı sıra.
    /// </summary>
    private static readonly string[] CefrOrder = { "A1", "A2", "B1", "B1+", "B2", "C1", "C2" };

    private static int RankOf(string? level)
    {
        var index = Array.FindIndex(CefrOrder, l => string.Equals(l, level?.Trim(), StringComparison.OrdinalIgnoreCase));
        return index < 0 ? -1 : index;
    }

    /// <summary>
    /// Öğrenenin destede görebileceği en yüksek seviye.
    ///
    /// Ayarlardaki <c>DifficultyMode</c> asıl kaynak, ama eski hesaplarda orada
    /// hâlâ CEFR olmayan <c>"Adaptive"</c> yazıyor olabilir — o durumda profilin
    /// yeterlilik alanına düşülür. İkisi de tanınmazsa B1: seviyesi bilinmeyen
    /// birine C2 kelimesi göstermektense biraz dar bir deste vermek yeğdir.
    /// </summary>
    private static int CeilingFor(string? difficultyMode, string? proficiency)
    {
        var explicitRank = RankOf(difficultyMode);
        if (explicitRank >= 0)
        {
            return explicitRank;
        }

        return (proficiency?.Trim().ToLowerInvariant()) switch
        {
            "just starting" => RankOf("A1"),
            "beginner" => RankOf("A2"),
            "intermediate" => RankOf("B2"),
            "advanced" => RankOf("C2"),
            "fluent" => RankOf("C2"),
            _ => RankOf("B1"),
        };
    }

    internal sealed record SyncReport(
        IReadOnlyList<int> CreatedDeckIds,
        IReadOnlyList<int> RemovedDeckIds,
        int ToppedUpDeckCount)
    {
        internal bool ChangedAnything =>
            CreatedDeckIds.Count > 0 || RemovedDeckIds.Count > 0 || ToppedUpDeckCount > 0;

        internal static readonly SyncReport Empty = new(Array.Empty<int>(), Array.Empty<int>(), 0);
    }

    internal static async Task<SyncReport> SyncAsync(IUnitOfWork unitOfWork, int userId)
    {
        var user = await unitOfWork.Repository<User>().GetByIdAsync(userId);
        if (user is null)
        {
            return SyncReport.Empty;
        }

        var targetCode = Normalize(user.TargetLanguageCode);
        var nativeCode = Normalize(user.NativeLanguageCode);

        // Aynı dili öğrenmek diye bir şey yok: her kart terim ve çevirisiyle
        // aynı olurdu, aşağıdaki filtre hepsini eleyip boş deste bırakırdı.
        if (targetCode == nativeCode)
        {
            return SyncReport.Empty;
        }

        // Seviye önce o dilin kendi profilinden okunuyor: aynı kişi Almancada
        // B2, yeni başladığı Japoncada A1 olabilir ve deste o dilin seviyesine
        // göre kurulmalı. Profil yoksa (dil ilk kez seçiliyor, satır henüz
        // açılmadı) eski davranışa —hesap geneli ayar + profil yeterliliği—
        // düşülür.
        var languageProfile = (await unitOfWork.Repository<UserLanguageProfile>()
            .FindAsync(p => p.UserId == userId && p.NativeLanguageCode == nativeCode && p.LanguageCode == targetCode)).FirstOrDefault();
        var settings = (await unitOfWork.Repository<UserSettings>()
            .FindAsync(s => s.UserId == userId)).FirstOrDefault();
        var levelCeiling = languageProfile is null
            ? CeilingFor(settings?.DifficultyMode, user.TargetProficiencyLevel)
            : CeilingFor(languageProfile.DifficultyMode, languageProfile.ProficiencyLevel);

        // Kategori seçimi de dile bağlı. Dilsiz satırlar (alan eklenmeden önce
        // yapılmış seçimler) her dilde geçerli sayılır — geçiş onları
        // kullanıcının o günkü hedef diline taşıdı, geriye kalanlar yalnızca
        // dili belirlenemeyenler.
        var selectedCategoryIds = await unitOfWork.Repository<UserCategory>().Query()
            .Where(link => link.UserId == userId
                           && (link.LanguageCode == targetCode || link.LanguageCode == ""))
            .Select(link => link.CategoryId)
            .Distinct()
            .ToListAsync();

        var templates = await unitOfWork.Repository<DeckTemplate>().Query()
            .Include(t => t.Labels)
            .Include(t => t.Words).ThenInclude(w => w.Texts)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        // Eski istemcilerin kurduğu desteleri önce içeri alıyoruz, yoksa
        // aşağıdaki adımlar onları görmez ve kitaplıkta aynı destenin iki
        // kopyası kalır.
        await AbsorbClientDecksAsync(unitOfWork, userId, templates);

        var wanted = templates
            .Where(t => selectedCategoryIds.Contains(t.CategoryId))
            .ToDictionary(t => StarterKeyFor(t.Slug, targetCode));

        // Yalnızca <em>bu dilin</em> kategori desteleri. Dil ayrımından önce
        // burası tüm kategori destelerini topluyordu ve aşağıdaki temizlik
        // adımı, başka dilin destelerini "artık istenmiyor" sayıp siliyordu:
        // Almancadan Japoncaya geçen biri Almanca kitaplığını kaybediyordu.
        // Artık her dil kendi kitaplığını koruyor, geri dönüldüğünde yerinde
        // duruyor.
        var languageSuffix = "_" + targetCode;
        var existing = await unitOfWork.Repository<Deck>().Query()
            .Where(d => d.UserId == userId
                        && d.StarterKey != null
                        && d.StarterKey.StartsWith(StarterKeyPrefix)
                        && d.StarterKey.EndsWith(languageSuffix))
            .ToListAsync();

        var createdDecks = new List<Deck>();
        var removed = new List<int>();
        var toppedUpDecks = 0;

        // Kartsız kalmış kendi destelerimizi burada topluyoruz. Böyle bir deste
        // öğrenene hiçbir şey vermiyor ve kitaplıkta gerçek destenin kopyası
        // gibi duruyor; sildiğimizde aşağıdaki döngü yerine sağlamını kurar.
        // Kartsız kalabilmesinin sebebi giderildi (bkz. BuildDeckAsync), ama
        // daha önce oluşmuş olanların temizlenmesi gerekiyor.
        var cardCounts = await unitOfWork.Repository<Vocabulary>().Query()
            .Where(card => card.DeckId != null)
            .GroupBy(card => card.DeckId!.Value)
            .Select(group => new { DeckId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.DeckId, row => row.Count);

        var empty = existing.Where(deck => !cardCounts.ContainsKey(deck.Id)).ToList();
        foreach (var deck in empty)
        {
            unitOfWork.Repository<Deck>().Delete(deck);
            removed.Add(deck.Id);
        }

        existing = existing.Except(empty).ToList();

        // Seçimden çıkan kategorinin destesi kaldırılır — koşulsuz.
        //
        // Burada eskiden "dokunulmamışsa" koşulu vardı: üzerinde tekrar kaydı
        // olan ya da yeniden adlandırılmış deste bırakılıyordu. Niyet iyiydi
        // ama sonucu kitaplığın seçimle örtüşmemesiydi: yalnızca "Teknoloji"
        // seçen biri, aylar önce bir kez açtığı "Yemek" destesini kitaplıkta
        // görmeye devam ediyordu ve onu oradan çıkarmanın bir yolu yoktu.
        //
        // Bedeli açık: o destedeki ilerleme kartlarla birlikte siliniyor ve
        // kategori yeniden seçilirse deste şablondan sıfırdan kurulur. Bu,
        // kullanıcının verdiği kararın doğrudan sonucu — kategoriyi seçimden
        // çıkarmak "bu desteyi istemiyorum" demek.
        //
        // Kullanıcının kendi kurduğu desteler bu döngüye hiç girmiyor:
        // `existing` yalnızca `category_` önekli, uygulamanın ürettiği
        // desteleri içeriyor.
        foreach (var deck in existing)
        {
            if (wanted.ContainsKey(deck.StarterKey!))
            {
                continue;
            }

            await RemoveDeckAsync(unitOfWork, deck);
            removed.Add(deck.Id);
        }

        var alreadyPresent = existing
            .Select(d => d.StarterKey!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (starterKey, template) in wanted.OrderBy(pair => pair.Value.SortOrder))
        {
            if (alreadyPresent.Contains(starterKey))
            {
                // Zaten duran deste, öğrenen o zamandan beri seviyesini
                // yükseltmiş olabilir. Eksik kalan kartları tamamlıyoruz —
                // seviye düşerse hiçbir şey silinmiyor, çünkü o kartlarda
                // ilerleme olabilir ve fazladan kart, kaybolan ilerlemeden
                // daha ucuz.
                var toppedUp = await TopUpDeckAsync(
                    unitOfWork,
                    existing.First(d => string.Equals(d.StarterKey, starterKey, StringComparison.OrdinalIgnoreCase)),
                    template,
                    targetCode,
                    nativeCode,
                    levelCeiling);

                if (toppedUp > 0)
                {
                    toppedUpDecks++;
                }

                continue;
            }

            var deck = await BuildDeckAsync(unitOfWork, userId, template, starterKey, targetCode, nativeCode, levelCeiling);
            if (deck is not null)
            {
                createdDecks.Add(deck);
            }
        }

        // Tek yazma: kurulan desteler kartlarıyla birlikte, kaldırılanlar ve
        // tamamlananlarla aynı işlemde gider. Kimlikler ancak burada oluşur,
        // bu yüzden rapor bundan sonra derleniyor.
        await unitOfWork.CompleteAsync();

        return new SyncReport(
            createdDecks.Select(deck => deck.Id).ToList(),
            removed,
            toppedUpDecks);
    }

    /// <summary>
    /// İstemcinin kurduğu ama artık katalogda karşılığı olan desteleri devralır.
    ///
    /// <para>
    /// Food, Travel, Business ve Family bir zamanlar Flutter tarafından
    /// kuruluyordu ve <c>starter_food_DE</c> gibi anahtarlar taşıyor. Migration
    /// bunları bir kez çevirdi, ama güncellenmemiş bir istemci hâlâ aynı
    /// desteleri kurmaya devam eder — kullanıcı uygulamayı her açtığında
    /// kitaplıkta ikinci bir kopya belirir. Sahada her zaman güncellenmemiş
    /// sürümler olacağı için bu, tek seferlik bir taşıma değil kalıcı bir
    /// kural olmalı.
    /// </para>
    ///
    /// <para>
    /// Her deste kendi dilinde devralınır: <c>starter_food_DE</c>
    /// <c>category_food_de</c> olur, kullanıcının şimdiki hedef diline
    /// çevrilmez. Ne olduğunu koruyoruz; istenip istenmediğine sonraki adımlar
    /// karar verir.
    /// </para>
    ///
    /// <para>
    /// Karşılığı zaten varsa istemcinin kopyası fazlalıktır. Üzerinde tekrar
    /// kaydı varsa bunlar terim eşleşmesiyle kalıcı destedeki eşdeğer karta
    /// taşınır, ancak ondan sonra silinir — çalışılmış bir kartı sırf anahtarı
    /// eski diye atmak, düzeltmeye çalıştığımız sorundan beterdir.
    /// </para>
    /// </summary>
    private static async Task AbsorbClientDecksAsync(
        IUnitOfWork unitOfWork,
        int userId,
        IReadOnlyList<DeckTemplate> templates)
    {
        var slugs = templates.Select(t => t.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clientDecks = await unitOfWork.Repository<Deck>().Query()
            .Where(d => d.UserId == userId
                        && d.StarterKey != null
                        && d.StarterKey.StartsWith(ClientKeyPrefix))
            .ToListAsync();

        var ownDecks = await unitOfWork.Repository<Deck>().Query()
            .Where(d => d.UserId == userId
                        && d.StarterKey != null
                        && d.StarterKey.StartsWith(StarterKeyPrefix))
            .ToListAsync();

        var ownByKey = ownDecks
            .GroupBy(d => d.StarterKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var deck in clientDecks)
        {
            var body = deck.StarterKey![ClientKeyPrefix.Length..];
            var cut = body.LastIndexOf('_');
            if (cut <= 0) continue;

            var slug = body[..cut];
            if (!slugs.Contains(slug))
            {
                // Katalogda karşılığı olmayan slug'lar (basics, everyday,
                // numbers, colours, time) uygulamanın eskiden herkese kurduğu
                // "genel" destelerdi: hiçbir çalışma konusuna bağlı değiller.
                //
                // Kitaplık artık yalnızca seçilen konulardan oluşuyor, yani
                // bunların yeri yok — ve istemci de artık kurmuyor. Burada
                // temizleniyorlar; aksi hâlde eski hesaplarda kalıcı olarak
                // durur ve "sadece seçtiğim konular" sözünü bozarlardı.
                await RemoveDeckAsync(unitOfWork, deck);
                continue;
            }

            var iso = IsoForFlag(body[(cut + 1)..]);
            if (iso is null) continue;

            var catalogKey = StarterKeyFor(slug, iso);

            if (!ownByKey.TryGetValue(catalogKey, out var keeper))
            {
                deck.StarterKey = catalogKey;
                // Anahtardan çıkan dil, destenin gerçekten öğrettiği dil —
                // kullanıcının şimdikinden farklı olabilir ve öyle kalmalı.
                deck.LanguageCode = iso;
                unitOfWork.Repository<Deck>().Update(deck);
                ownByKey[catalogKey] = deck;
                continue;
            }

            await MoveProgressAsync(unitOfWork, userId, fromDeckId: deck.Id, toDeckId: keeper.Id);
            await RemoveDeckAsync(unitOfWork, deck);
        }

        await unitOfWork.CompleteAsync();
    }

    /// <summary>
    /// İki destedeki aynı terimli kartlar arasında tekrar geçmişini taşır.
    ///
    /// Kartlar kopyalandığı için aralarında kimlik bağı yok; terim ikisinde de
    /// aynı yazıldığından eşleşme onun üzerinden kuruluyor. Hedef kartta zaten
    /// bir kayıt varsa kaynaktaki bırakılır: <c>(UserID, WordID)</c> benzersiz
    /// ve ikisini birleştirmenin doğru yolu yok — hangi aralığın geçerli
    /// olduğuna karar vermek uydurma olurdu.
    /// </summary>
    private static async Task MoveProgressAsync(IUnitOfWork unitOfWork, int userId, int fromDeckId, int toDeckId)
    {
        var source = await unitOfWork.Repository<Vocabulary>().Query()
            .Where(card => card.DeckId == fromDeckId)
            .ToListAsync();
        if (source.Count == 0) return;

        var targetByTerm = (await unitOfWork.Repository<Vocabulary>().Query()
                .Where(card => card.DeckId == toDeckId)
                .ToListAsync())
            .GroupBy(card => card.Term, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().WordID, StringComparer.OrdinalIgnoreCase);

        var sourceIds = source.Select(card => card.WordID).ToList();
        var progress = await unitOfWork.Repository<UserWordProgress>().Query()
            .Where(row => row.UserID == userId && sourceIds.Contains(row.WordID))
            .ToListAsync();
        if (progress.Count == 0) return;

        var takenWordIds = (await unitOfWork.Repository<UserWordProgress>().Query()
                .Where(row => row.UserID == userId)
                .Select(row => row.WordID)
                .ToListAsync())
            .ToHashSet();

        var termById = source.ToDictionary(card => card.WordID, card => card.Term);

        foreach (var row in progress)
        {
            if (!termById.TryGetValue(row.WordID, out var term)) continue;
            if (!targetByTerm.TryGetValue(term, out var targetWordId)) continue;
            if (takenWordIds.Contains(targetWordId)) continue;

            row.WordID = targetWordId;
            unitOfWork.Repository<UserWordProgress>().Update(row);
            takenWordIds.Add(targetWordId);
        }
    }

    /// <summary>
    /// İstemcinin bayrak kodundan katalog ISO koduna. Languages tablosundaki
    /// <c>FlagCode</c> eşlemesiyle aynı; tanınmayan kod için <c>null</c>.
    /// </summary>
    private static string? IsoForFlag(string flag) => flag.ToUpperInvariant() switch
    {
        "GB" => "en",
        "TR" => "tr",
        "ES" => "es",
        "FR" => "fr",
        "DE" => "de",
        "IT" => "it",
        "PT" => "pt",
        "JP" => "ja",
        "KR" => "ko",
        "CN" => "zh",
        _ => null,
    };

    /// <summary>
    /// Var olan desteye, öğrenenin şimdiki seviyesine giren ama destede
    /// bulunmayan kartları ekler. Karşılaştırma terim metni üzerinden yapılır:
    /// şablon kelimesi ile kullanıcının kartı arasında kalıcı bir bağ yok
    /// (kart kopyalanınca bağımsızlaşır), terim ise ikisinde de aynıdır.
    /// </summary>
    private static async Task<int> TopUpDeckAsync(
        IUnitOfWork unitOfWork,
        Deck deck,
        DeckTemplate template,
        string targetCode,
        string nativeCode,
        int levelCeiling)
    {
        var existingTerms = (await unitOfWork.Repository<Vocabulary>().Query()
                .Where(card => card.DeckId == deck.Id)
                .Select(card => card.Term)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var now = DateTime.UtcNow;

        foreach (var word in InRange(template, levelCeiling))
        {
            var term = TextFor(word, targetCode);
            var translation = TextFor(word, nativeCode);

            if (term is null || translation is null) continue;
            if (string.Equals(term, translation, StringComparison.OrdinalIgnoreCase)) continue;
            if (existingTerms.Contains(term)) continue;

            await unitOfWork.Repository<Vocabulary>().AddAsync(new Vocabulary
            {
                DeckId = deck.Id,
                Term = term,
                Translation = translation,
                ExampleSentence = ExampleSentenceTemplates.For(template.Slug, targetCode, term) ?? string.Empty,
                CreatedAt = now,
            });
            added++;
        }

        return added;
    }

    /// <summary>
    /// Şablonun, öğrenenin seviyesinde ve altında kalan kelimeleri — deste
    /// içindeki sırasıyla. Seviyesi tanınmayan bir kelime dışarıda bırakılır:
    /// yanlış seviyede kart göstermektense hiç göstermemek yeğdir.
    /// </summary>
    private static IEnumerable<DeckTemplateWord> InRange(DeckTemplate template, int levelCeiling) =>
        template.Words
            .Where(w => RankOf(w.CefrLevel) >= 0 && RankOf(w.CefrLevel) <= levelCeiling)
            .OrderBy(w => w.Ordinal);

    private static async Task RemoveDeckAsync(IUnitOfWork unitOfWork, Deck deck)
    {
        // Vocabularies -> Decks ilişkisi NO_ACTION: kartlar dururken deste
        // silinemez. UserWordProgress ve VocabularyTag kartlardan CASCADE ile
        // gider; StudyActivity ve QuizSession SET_NULL ile bağını kaybeder ama
        // satır olarak kalır, yani geçmiş istatistikler bozulmaz.
        var cards = await unitOfWork.Repository<Vocabulary>().Query()
            .Where(card => card.DeckId == deck.Id)
            .ToListAsync();

        foreach (var card in cards)
        {
            unitOfWork.Repository<Vocabulary>().Delete(card);
        }

        unitOfWork.Repository<Deck>().Delete(deck);
    }

    private static async Task<Deck?> BuildDeckAsync(
        IUnitOfWork unitOfWork,
        int userId,
        DeckTemplate template,
        string starterKey,
        string targetCode,
        string nativeCode,
        int levelCeiling)
    {
        var label = LabelFor(template, targetCode);
        var now = DateTime.UtcNow;

        var cards = new List<Vocabulary>();
        foreach (var word in InRange(template, levelCeiling))
        {
            var term = TextFor(word, targetCode);
            var translation = TextFor(word, nativeCode);

            if (term is null || translation is null)
            {
                continue;
            }

            // İki dilde aynı yazılan kelime (Software/Software) öğretecek bir
            // şey taşımaz; kartı hiç kurmuyoruz.
            if (string.Equals(term, translation, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cards.Add(new Vocabulary
            {
                Term = term,
                Translation = translation,
                ExampleSentence = ExampleSentenceTemplates.For(template.Slug, targetCode, term) ?? string.Empty,
                CreatedAt = now,
            });
        }

        // Bu dil çifti için tek bir kart bile çıkmadıysa desteyi hiç kurmuyoruz.
        if (cards.Count == 0)
        {
            return null;
        }

        // Kartlar desteye gezinme özelliğinden bağlanıyor, DeckId elle
        // verilmiyor: EF ikisini tek SaveChanges içinde, doğru sırayla yazar ve
        // yabancı anahtarı kendisi doldurur.
        //
        // Bunun önceki hâli desteyi önce kaydedip kimliğini alıyor, kartları
        // sonra ekliyordu. Aradaki her hata — araya giren başka bir yazma, bir
        // kısıt ihlali — kartsız bir deste bırakıyordu ve gerçekten bıraktı da.
        // Tek kayıt olunca böyle bir ara durum kalmıyor.
        var deck = new Deck
        {
            UserId = userId,
            Title = label.Title,
            Description = label.Description,
            StarterKey = starterKey,
            LanguageCode = targetCode,
            CreatedAt = now,
            Flashcards = cards,
        };

        await unitOfWork.Repository<Deck>().AddAsync(deck);
        return deck;
    }

    /// <summary>
    /// Hedef dildeki etiket; yoksa İngilizce. Dil listesine yeni bir dil
    /// eklemek, o dilin metni yazılana kadar adsız deste üretmesin diye.
    /// </summary>
    private static DeckTemplateLabel LabelFor(DeckTemplate template, string languageCode) =>
        template.Labels.FirstOrDefault(l => string.Equals(l.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
        ?? template.Labels.First(l => string.Equals(l.LanguageCode, "en", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Kelimenin bu dildeki karşılığı; yoksa <c>null</c> — etiketin aksine
    /// burada İngilizceye düşmüyoruz, çünkü bu bir kartı sessizce yanlış dilde
    /// göstermek olurdu. Karşılığı olmayan kelime atlanır.
    /// </summary>
    private static string? TextFor(DeckTemplateWord word, string languageCode) =>
        word.Texts.FirstOrDefault(t => string.Equals(t.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))?.Text;

    /// <summary>
    /// <c>category_music_de</c> -> <c>music</c>. Dil kodu son alt çizgiden
    /// sonra durur; slug'ın kendisi alt çizgi içerebileceği için sondan aranır.
    /// </summary>
    /// <summary>
    /// Deste anahtarindan sablon slug'u: <c>category_music_de</c> -> <c>music</c>.
    /// Bos string, anahtarin bu sablon sisteminden gelmedigi anlamina gelir.
    /// </summary>
    internal static string SlugFrom(string? starterKey)
    {
        if (string.IsNullOrEmpty(starterKey) || !starterKey.StartsWith(StarterKeyPrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var body = starterKey[StarterKeyPrefix.Length..];
        var lastSeparator = body.LastIndexOf('_');
        return lastSeparator <= 0 ? body : body[..lastSeparator];
    }

    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant();
}
