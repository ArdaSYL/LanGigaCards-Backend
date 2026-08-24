namespace VocabGrid.Services;

/// <summary>
/// One natural example sentence per category slug per language, with the
/// vocabulary word dropped into a `{0}` placeholder.
///
/// Not per-word: the catalog has 540+ words across 15 categories in 10
/// languages -- authoring a bespoke, grammatically-checked sentence for
/// every (word, language) pair is thousands of sentences, not something
/// hand-written content can responsibly cover in one pass. A category
/// shares a grammatical role closely enough (Food words are nearly all
/// nouns, Colours nearly all adjectives, Animals nearly all nouns) that one
/// well-formed sentence per category reads naturally for every word in it.
///
/// Every template is deliberately built to avoid gendered
/// articles/adjectives directly touching the placeholder (no "the {0}",
/// no "a new {0}") -- the catalog doesn't record grammatical gender per
/// word, so a construction that would need it agreeing correctly in
/// Spanish/French/German/Italian/Portuguese can't be guaranteed right for
/// an arbitrary word. Bare-object constructions ("I would like some {0}",
/// "I saw {0} at the zoo") sidestep that entirely.
/// </summary>
internal static class ExampleSentenceTemplates
{
    private static readonly Dictionary<string, Dictionary<string, string>> BySlug = new()
    {
        ["food"] = new()
        {
            ["en"] = "I would like some {0}.",
            ["es"] = "Me gustaría un poco de {0}.",
            ["fr"] = "Je voudrais un peu de {0}.",
            ["de"] = "Ich hätte gern etwas {0}.",
            ["it"] = "Vorrei un po' di {0}.",
            ["pt"] = "Eu gostaria de um pouco de {0}.",
            ["ja"] = "{0}をお願いします。",
            ["ko"] = "{0} 주세요.",
            ["zh"] = "我想要一些{0}。",
            ["tr"] = "Biraz {0} istiyorum.",
        },
        ["travel"] = new()
        {
            ["en"] = "I need to find {0}.",
            ["es"] = "Necesito encontrar {0}.",
            ["fr"] = "Je dois trouver {0}.",
            ["de"] = "Ich muss {0} finden.",
            ["it"] = "Devo trovare {0}.",
            ["pt"] = "Preciso encontrar {0}.",
            ["ja"] = "{0}を見つけないといけません。",
            ["ko"] = "{0}을 찾아야 해요.",
            ["zh"] = "我需要找到{0}。",
            ["tr"] = "{0} bulmam gerekiyor.",
        },
        ["business"] = new()
        {
            ["en"] = "We talked about {0} in the meeting.",
            ["es"] = "Hablamos sobre {0} en la reunión.",
            ["fr"] = "Nous avons parlé de {0} pendant la réunion.",
            ["de"] = "Wir haben in der Besprechung über {0} gesprochen.",
            ["it"] = "Abbiamo parlato di {0} durante la riunione.",
            ["pt"] = "Falamos sobre {0} na reunião.",
            ["ja"] = "会議で{0}について話しました。",
            ["ko"] = "회의에서 {0}에 대해 이야기했어요.",
            ["zh"] = "我们在会议上谈到了{0}。",
            ["tr"] = "Toplantıda {0} hakkında konuştuk.",
        },
        ["technology"] = new()
        {
            ["en"] = "I just bought {0}.",
            ["es"] = "Acabo de comprar {0}.",
            ["fr"] = "Je viens d'acheter {0}.",
            ["de"] = "Ich habe gerade {0} gekauft.",
            ["it"] = "Ho appena comprato {0}.",
            ["pt"] = "Acabei de comprar {0}.",
            ["ja"] = "{0}を買ったばかりです。",
            ["ko"] = "저는 방금 {0} 샀어요.",
            ["zh"] = "我刚买了{0}。",
            ["tr"] = "Az önce {0} aldım.",
        },
        ["education"] = new()
        {
            ["en"] = "We are studying {0} today.",
            ["es"] = "Hoy estamos estudiando {0}.",
            ["fr"] = "Aujourd'hui, nous étudions {0}.",
            ["de"] = "Wir lernen heute {0}.",
            ["it"] = "Oggi stiamo studiando {0}.",
            ["pt"] = "Hoje estamos estudando {0}.",
            ["ja"] = "今日は{0}を勉強しています。",
            ["ko"] = "오늘 저희는 {0} 공부하고 있어요.",
            ["zh"] = "我们今天在学习{0}。",
            ["tr"] = "Bugün {0} çalışıyoruz.",
        },
        ["movies"] = new()
        {
            ["en"] = "I really enjoyed {0}.",
            ["es"] = "Disfruté mucho {0}.",
            ["fr"] = "J'ai vraiment aimé {0}.",
            ["de"] = "Mir hat {0} sehr gut gefallen.",
            ["it"] = "Mi è piaciuto molto {0}.",
            ["pt"] = "Eu gostei muito de {0}.",
            ["ja"] = "{0}をとても楽しみました。",
            ["ko"] = "저는 {0} 정말 재미있게 봤어요.",
            ["zh"] = "我很喜欢{0}。",
            ["tr"] = "{0} gerçekten çok beğendim.",
        },
        ["music"] = new()
        {
            ["en"] = "I love listening to {0}.",
            ["es"] = "Me encanta escuchar {0}.",
            ["fr"] = "J'adore écouter {0}.",
            ["de"] = "Ich höre sehr gern {0}.",
            ["it"] = "Adoro ascoltare {0}.",
            ["pt"] = "Eu adoro ouvir {0}.",
            ["ja"] = "{0}を聞くのが大好きです。",
            ["ko"] = "저는 {0} 듣는 걸 정말 좋아해요.",
            ["zh"] = "我很喜欢听{0}。",
            ["tr"] = "{0} dinlemeyi çok seviyorum.",
        },
        ["gaming"] = new()
        {
            ["en"] = "I really enjoy {0}.",
            ["es"] = "Disfruto mucho {0}.",
            ["fr"] = "J'aime beaucoup {0}.",
            ["de"] = "Ich mag {0} sehr.",
            ["it"] = "Mi piace molto {0}.",
            ["pt"] = "Eu gosto muito de {0}.",
            ["ja"] = "{0}がとても好きです。",
            ["ko"] = "저는 {0} 정말 좋아해요.",
            ["zh"] = "我很喜欢{0}。",
            ["tr"] = "{0} gerçekten çok seviyorum.",
        },
        ["sports"] = new()
        {
            ["en"] = "I play {0} every weekend.",
            ["es"] = "Juego {0} todos los fines de semana.",
            ["fr"] = "Je joue à {0} tous les week-ends.",
            ["de"] = "Ich spiele jedes Wochenende {0}.",
            ["it"] = "Gioco a {0} ogni fine settimana.",
            ["pt"] = "Eu jogo {0} todo fim de semana.",
            ["ja"] = "毎週末に{0}をします。",
            ["ko"] = "저는 매주 주말에 {0}을 해요.",
            ["zh"] = "我每个周末都玩{0}。",
            ["tr"] = "Her hafta sonu {0} oynuyorum.",
        },
        ["health"] = new()
        {
            ["en"] = "I need to ask my doctor about {0}.",
            ["es"] = "Necesito preguntarle a mi médico sobre {0}.",
            ["fr"] = "Je dois demander à mon médecin à propos de {0}.",
            ["de"] = "Ich muss meinen Arzt nach {0} fragen.",
            ["it"] = "Devo chiedere al mio medico riguardo a {0}.",
            ["pt"] = "Preciso perguntar ao meu médico sobre {0}.",
            ["ja"] = "{0}について医者に聞かないといけません。",
            ["ko"] = "저는 {0}에 대해 의사 선생님께 물어봐야 해요.",
            ["zh"] = "我需要向医生询问{0}。",
            ["tr"] = "Doktoruma {0} hakkında sormam gerekiyor.",
        },
        ["shopping"] = new()
        {
            ["en"] = "I want to buy {0}.",
            ["es"] = "Quiero comprar {0}.",
            ["fr"] = "Je veux acheter {0}.",
            ["de"] = "Ich möchte {0} kaufen.",
            ["it"] = "Voglio comprare {0}.",
            ["pt"] = "Eu quero comprar {0}.",
            ["ja"] = "{0}を買いたいです。",
            ["ko"] = "저는 {0} 사고 싶어요.",
            ["zh"] = "我想买{0}。",
            ["tr"] = "{0} almak istiyorum.",
        },
        ["nature"] = new()
        {
            ["en"] = "I love looking at {0}.",
            ["es"] = "Me encanta mirar {0}.",
            ["fr"] = "J'adore regarder {0}.",
            ["de"] = "Ich schaue mir gern {0} an.",
            ["it"] = "Adoro guardare {0}.",
            ["pt"] = "Eu adoro olhar para {0}.",
            ["ja"] = "{0}を見るのが大好きです。",
            ["ko"] = "저는 {0} 보는 걸 정말 좋아해요.",
            ["zh"] = "我很喜欢看{0}。",
            ["tr"] = "{0}'a bakmayı çok seviyorum.",
        },
        ["science"] = new()
        {
            ["en"] = "We learned about {0} in class.",
            ["es"] = "Aprendimos sobre {0} en clase.",
            ["fr"] = "Nous avons appris {0} en classe.",
            ["de"] = "Wir haben im Unterricht {0} gelernt.",
            ["it"] = "Abbiamo imparato {0} in classe.",
            ["pt"] = "Aprendemos sobre {0} na aula.",
            ["ja"] = "授業で{0}について学びました。",
            ["ko"] = "저희는 수업에서 {0}에 대해 배웠어요.",
            ["zh"] = "我们在课堂上学习了{0}。",
            ["tr"] = "Derste {0} hakkında bilgi öğrendik.",
        },
        ["animals"] = new()
        {
            ["en"] = "I saw {0} at the zoo.",
            ["es"] = "Vi {0} en el zoológico.",
            ["fr"] = "J'ai vu {0} au zoo.",
            ["de"] = "Ich habe {0} im Zoo gesehen.",
            ["it"] = "Ho visto {0} allo zoo.",
            ["pt"] = "Eu vi {0} no zoológico.",
            ["ja"] = "動物園で{0}を見ました。",
            ["ko"] = "저는 동물원에서 {0}을 봤어요.",
            ["zh"] = "我在动物园看到了{0}。",
            ["tr"] = "Hayvanat bahçesinde {0} gördüm.",
        },
        ["family"] = new()
        {
            ["en"] = "I love spending time with {0}.",
            ["es"] = "Me encanta pasar tiempo con {0}.",
            ["fr"] = "J'adore passer du temps avec {0}.",
            ["de"] = "Ich verbringe gern Zeit mit {0}.",
            ["it"] = "Adoro passare del tempo con {0}.",
            ["pt"] = "Eu adoro passar tempo com {0}.",
            ["ja"] = "{0}と一緒に過ごすのが大好きです。",
            ["ko"] = "저는 {0}와 시간을 보내는 걸 정말 좋아해요.",
            ["zh"] = "我很喜欢和{0}在一起。",
            ["tr"] = "{0} ile vakit geçirmeyi çok seviyorum.",
        },
        // The 5 slugs the Flutter client builds itself (basics/everyday/
        // numbers/colours/time) reuse this same table -- see
        // StarterContent.exampleSentenceFor in the Flutter app, which is
        // kept in sync with these five entries by hand since it has no way
        // to call C#.
        ["basics"] = new()
        {
            ["en"] = "It is polite to say {0}.",
            ["es"] = "Es de buena educación decir {0}.",
            ["fr"] = "C'est poli de dire {0}.",
            ["de"] = "Es ist höflich, {0} zu sagen.",
            ["it"] = "È educato dire {0}.",
            ["pt"] = "É educado dizer {0}.",
            ["ja"] = "「{0}」と言うのが礼儀正しいです。",
            ["ko"] = "\"{0}\"라고 말하는 것이 예의 바른 거예요.",
            ["zh"] = "说\"{0}\"是有礼貌的。",
            ["tr"] = "\"{0}\" demek kibarcadır.",
        },
        ["everyday"] = new()
        {
            ["en"] = "I use {0} every day.",
            ["es"] = "Uso {0} todos los días.",
            ["fr"] = "J'utilise {0} tous les jours.",
            ["de"] = "Ich benutze {0} jeden Tag.",
            ["it"] = "Uso {0} ogni giorno.",
            ["pt"] = "Eu uso {0} todos os dias.",
            ["ja"] = "毎日{0}を使います。",
            ["ko"] = "저는 매일 {0}을 사용해요.",
            ["zh"] = "我每天都用{0}。",
            ["tr"] = "Her gün {0} kullanıyorum.",
        },
        ["numbers"] = new()
        {
            ["en"] = "I have {0} apples.",
            ["es"] = "Tengo {0} manzanas.",
            ["fr"] = "J'ai {0} pommes.",
            ["de"] = "Ich habe {0} Äpfel.",
            ["it"] = "Ho {0} mele.",
            ["pt"] = "Eu tenho {0} maçãs.",
            ["ja"] = "りんごを{0}個持っています。",
            ["ko"] = "저는 사과 {0}개 있어요.",
            ["zh"] = "我有{0}个苹果。",
            ["tr"] = "{0} tane elmam var.",
        },
        ["colours"] = new()
        {
            ["en"] = "My favorite color is {0}.",
            ["es"] = "Mi color favorito es {0}.",
            ["fr"] = "Ma couleur préférée est {0}.",
            ["de"] = "Meine Lieblingsfarbe ist {0}.",
            ["it"] = "Il mio colore preferito è {0}.",
            ["pt"] = "Minha cor favorita é {0}.",
            ["ja"] = "私の好きな色は{0}です。",
            ["ko"] = "제가 가장 좋아하는 색은 {0}이에요.",
            ["zh"] = "我最喜欢的颜色是{0}。",
            ["tr"] = "En sevdiğim renk {0}.",
        },
        ["time"] = new()
        {
            ["en"] = "I think about {0} often.",
            ["es"] = "Pienso en {0} a menudo.",
            ["fr"] = "Je pense souvent à {0}.",
            ["de"] = "Ich denke oft an {0}.",
            ["it"] = "Penso spesso a {0}.",
            ["pt"] = "Eu penso em {0} frequentemente.",
            ["ja"] = "よく{0}のことを考えます。",
            ["ko"] = "저는 {0}에 대해 자주 생각해요.",
            ["zh"] = "我经常想到{0}。",
            ["tr"] = "Sık sık {0} düşünüyorum.",
        },
    };

    /// <summary>
    /// A ready-to-show example sentence for [term] in [slug]'s category,
    /// written in [languageCode]. Falls back to English if the slug or
    /// language isn't covered (a new category added to the catalog without
    /// its templates yet), and to null if English isn't covered either --
    /// callers treat that as "no example," never a placeholder that looks
    /// like real content.
    ///
    /// German gets a real per-word sentence from
    /// <see cref="GermanExampleSentences"/> instead of this class's generic
    /// per-category template, checked first -- see that class's own
    /// docstring for why German specifically.
    /// </summary>
    internal static string? For(string slug, string languageCode, string term)
    {
        if (string.Equals(languageCode.Trim(), "de", StringComparison.OrdinalIgnoreCase))
        {
            var perWord = GermanExampleSentences.For(slug, term);
            if (perWord is not null)
            {
                return perWord;
            }
        }

        if (!BySlug.TryGetValue(slug, out var byLanguage))
        {
            return null;
        }

        var normalized = languageCode.Trim().ToLowerInvariant();
        var template = byLanguage.TryGetValue(normalized, out var exact)
            ? exact
            : byLanguage.GetValueOrDefault("en");

        return template is null ? null : string.Format(template, term);
    }
}
