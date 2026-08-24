using Microsoft.EntityFrameworkCore;
using VocabGrid.Entities;

namespace VocabGrid.Data;

/// <summary>
/// Kategori destelerinin şablon kataloğu.
///
/// <see cref="CurriculumSeedData"/> paylaşılan ders kelimelerini taşır; burası
/// ise kullanıcının kategori seçimine göre kopyalanan deste şablonlarını.
/// İkisi de migration ile seed edilir, yani backend kurulduğunda içerik hazır
/// gelir — kullanıcı bir kategori seçtiğinde <c>CategoryDeckSynchronizer</c>
/// buradan ona ait desteyi üretir.
///
/// <para>
/// On beş kategorinin tamamı burada. Food, Travel, Business ve Family bir süre
/// Flutter'daki <c>starter_content.dart</c> tarafından kuruluyordu; ikisi aynı
/// anda çalışsa aynı desteden iki tane üretirdi, bu yüzden istemci tarafındaki
/// karşılıkları kaldırıldı. Orada yalnızca kategorisi olmayan beş genel deste
/// kaldı: basics, everyday, numbers, colours, time.
/// </para>
///
/// <para>
/// Devralınan dört kategoride ilk on kelimenin çevirileri eski listelerden
/// birebir alındı. Bunu yapmasak, desteyi zaten çalışmış bir kullanıcının
/// kartının yanına aynı anlamın biraz farklı yazılmış ikinci bir kopyası
/// düşerdi — tamamlama karşılaştırması terim metni üzerinden yapılıyor.
/// </para>
///
/// <para>
/// Her şablon 36 kelime taşır ve kelimeler CEFR seviyesine ayrılmıştır:
/// A1 10, A2 7, B1 6, B1+ 4, B2 4, C1 3, C2 2. Dağılım bilerek tabana ağırlık
/// verir; seviye birikimli okunduğu için (bkz. <see cref="DeckTemplateWord"/>)
/// alt seviyeler her destenin zeminini oluşturur. Böylece A1 çalışan biri 10,
/// B1 çalışan 27, C2 çalışan 36 kart alır.
/// </para>
///
/// <para>
/// Veri, okunabilirlik için sıkışık dizilerle yazılır ve <see cref="Apply"/>
/// içinde <c>HasData</c> satırlarına açılır. Her kelime tek satırda seviyesi ve
/// on dildeki karşılığıyla durur; dil sırası <see cref="LanguageOrder"/> ile
/// sabittir.
/// </para>
/// </summary>
internal static class DeckTemplateSeedData
{
    /// <summary>
    /// Sözlük satırlarındaki dil sırası. <see cref="Language.Code"/> ile aynı
    /// ISO kodları — Flutter tarafı bunları kendi bayrak kodlarına çevirir
    /// (<c>en -> GB</c>, <c>ja -> JP</c>), bu yüzden burada çeviri yapılmaz.
    /// </summary>
    internal static readonly string[] LanguageOrder =
        { "en", "tr", "es", "fr", "de", "it", "pt", "ja", "ko", "zh" };

    private sealed record Spec(
        int Id,
        string Slug,
        int CategoryId,
        string Emoji,
        string ColorHex,
        string[] Titles,
        string[] Descriptions,
        (string Level, string[] Texts)[] Words);

    /// <summary>
    /// Şablon kimlikleri elle verilir: <c>HasData</c> satırlarının migration'lar
    /// arasında yerinde kalması için sabit olmaları gerekir. Renk ve emoji,
    /// Categories tablosundaki <c>ColorHex</c> ile hizalıdır.
    /// </summary>
    private static readonly Spec[] Specs =
    {
        new(1, "technology", 4, "💻", "#06B6D4",
            new[] { "Technology", "Teknoloji", "Tecnología", "Technologie", "Technik", "Tecnologia", "Tecnologia", "テクノロジー", "기술", "科技" },
            new[] { "Computers, phones and the internet", "Bilgisayarlar, telefonlar ve internet", "Ordenadores, teléfonos e internet", "Ordinateurs, téléphones et internet", "Computer, Handys und das Internet", "Computer, telefoni e internet", "Computadores, telefones e internet", "コンピューター、電話、インターネット", "컴퓨터, 전화 그리고 인터넷", "电脑、手机和互联网" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Computer", "Bilgisayar", "Ordenador", "Ordinateur", "Computer", "Computer", "Computador", "コンピューター", "컴퓨터", "电脑" }),
                ("A1",  new[] { "Phone", "Telefon", "Teléfono", "Téléphone", "Telefon", "Telefono", "Telefone", "電話", "전화", "手机" }),
                ("A1",  new[] { "Screen", "Ekran", "Pantalla", "Écran", "Bildschirm", "Schermo", "Tela", "画面", "화면", "屏幕" }),
                ("A1",  new[] { "Keyboard", "Klavye", "Teclado", "Clavier", "Tastatur", "Tastiera", "Teclado", "キーボード", "키보드", "键盘" }),
                ("A1",  new[] { "Password", "Şifre", "Contraseña", "Mot de passe", "Passwort", "Password", "Senha", "パスワード", "비밀번호", "密码" }),
                ("A1",  new[] { "File", "Dosya", "Archivo", "Fichier", "Datei", "File", "Arquivo", "ファイル", "파일", "文件" }),
                ("A1",  new[] { "Battery", "Pil", "Batería", "Batterie", "Batterie", "Batteria", "Bateria", "電池", "배터리", "电池" }),
                ("A1",  new[] { "Search", "Aramak", "Buscar", "Chercher", "Suchen", "Cercare", "Procurar", "検索", "검색", "搜索" }),
                ("A1",  new[] { "Camera", "Kamera", "Cámara", "Appareil photo", "Kamera", "Fotocamera", "Câmera", "カメラ", "카메라", "相机" }),
                ("A1",  new[] { "Message", "Mesaj", "Mensaje", "Message", "Nachricht", "Messaggio", "Mensagem", "メッセージ", "메시지", "消息" }),
                ("A2",  new[] { "Download", "İndirmek", "Descargar", "Télécharger", "Herunterladen", "Scaricare", "Baixar", "ダウンロード", "다운로드", "下载" }),
                ("A2",  new[] { "Update", "Güncelleme", "Actualización", "Mise à jour", "Aktualisierung", "Aggiornamento", "Atualização", "アップデート", "업데이트", "更新" }),
                ("A2",  new[] { "Network", "Ağ", "Red", "Réseau", "Netzwerk", "Rete", "Rede", "ネットワーク", "네트워크", "网络" }),
                ("A2",  new[] { "Charger", "Şarj aleti", "Cargador", "Chargeur", "Ladegerät", "Caricabatterie", "Carregador", "充電器", "충전기", "充电器" }),
                ("A2",  new[] { "Website", "Web sitesi", "Sitio web", "Site web", "Webseite", "Sito web", "Site", "ウェブサイト", "웹사이트", "网站" }),
                ("A2",  new[] { "Printer", "Yazıcı", "Impresora", "Imprimante", "Drucker", "Stampante", "Impressora", "プリンター", "프린터", "打印机" }),
                ("A2",  new[] { "Speaker", "Hoparlör", "Altavoz", "Haut-parleur", "Lautsprecher", "Altoparlante", "Alto-falante", "スピーカー", "스피커", "扬声器" }),
                ("B1",  new[] { "Software", "Yazılım", "Software", "Logiciel", "Software", "Software", "Software", "ソフトウェア", "소프트웨어", "软件" }),
                ("B1",  new[] { "Hardware", "Donanım", "Hardware", "Matériel", "Hardware", "Hardware", "Hardware", "ハードウェア", "하드웨어", "硬件" }),
                ("B1",  new[] { "Backup", "Yedek", "Copia de seguridad", "Sauvegarde", "Sicherung", "Backup", "Backup", "バックアップ", "백업", "备份" }),
                ("B1",  new[] { "Device", "Cihaz", "Dispositivo", "Appareil", "Gerät", "Dispositivo", "Dispositivo", "端末", "기기", "设备" }),
                ("B1",  new[] { "Setting", "Ayar", "Ajuste", "Réglage", "Einstellung", "Impostazione", "Configuração", "設定", "설정", "设置" }),
                ("B1",  new[] { "Browser", "Tarayıcı", "Navegador", "Navigateur", "Browser", "Browser", "Navegador", "ブラウザ", "브라우저", "浏览器" }),
                ("B1+", new[] { "Cloud", "Bulut", "Nube", "Nuage", "Cloud", "Nuvola", "Nuvem", "クラウド", "클라우드", "云" }),
                ("B1+", new[] { "Server", "Sunucu", "Servidor", "Serveur", "Server", "Server", "Servidor", "サーバー", "서버", "服务器" }),
                ("B1+", new[] { "Database", "Veritabanı", "Base de datos", "Base de données", "Datenbank", "Database", "Banco de dados", "データベース", "데이터베이스", "数据库" }),
                ("B1+", new[] { "Wireless", "Kablosuz", "Inalámbrico", "Sans fil", "Drahtlos", "Senza fili", "Sem fio", "無線", "무선", "无线" }),
                ("B2",  new[] { "Encryption", "Şifreleme", "Cifrado", "Chiffrement", "Verschlüsselung", "Crittografia", "Criptografia", "暗号化", "암호화", "加密" }),
                ("B2",  new[] { "Bandwidth", "Bant genişliği", "Ancho de banda", "Bande passante", "Bandbreite", "Larghezza di banda", "Largura de banda", "帯域幅", "대역폭", "带宽" }),
                ("B2",  new[] { "Interface", "Arayüz", "Interfaz", "Interface", "Schnittstelle", "Interfaccia", "Interface", "インターフェース", "인터페이스", "界面" }),
                ("B2",  new[] { "Firewall", "Güvenlik duvarı", "Cortafuegos", "Pare-feu", "Firewall", "Firewall", "Firewall", "ファイアウォール", "방화벽", "防火墙" }),
                ("C1",  new[] { "Algorithm", "Algoritma", "Algoritmo", "Algorithme", "Algorithmus", "Algoritmo", "Algoritmo", "アルゴリズム", "알고리즘", "算法" }),
                ("C1",  new[] { "Compatibility", "Uyumluluk", "Compatibilidad", "Compatibilité", "Kompatibilität", "Compatibilità", "Compatibilidade", "互換性", "호환성", "兼容性" }),
                ("C1",  new[] { "Deployment", "Dağıtım", "Despliegue", "Déploiement", "Bereitstellung", "Distribuzione", "Implantação", "展開", "배포", "部署" }),
                ("C2",  new[] { "Redundancy", "Yedeklilik", "Redundancia", "Redondance", "Redundanz", "Ridondanza", "Redundância", "冗長性", "이중화", "冗余" }),
                ("C2",  new[] { "Latency", "Gecikme", "Latencia", "Latence", "Latenz", "Latenza", "Latência", "遅延", "지연 시간", "延迟" }),
            }),

        new(2, "education", 5, "🎓", "#8B5CF6",
            new[] { "Education", "Eğitim", "Educación", "Éducation", "Bildung", "Istruzione", "Educação", "教育", "교육", "教育" },
            new[] { "School, study and exams", "Okul, ders ve sınavlar", "Escuela, estudio y exámenes", "École, études et examens", "Schule, Lernen und Prüfungen", "Scuola, studio ed esami", "Escola, estudo e provas", "学校、勉強、そして試験", "학교, 공부 그리고 시험", "学校、学习和考试" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Teacher", "Öğretmen", "Profesor", "Professeur", "Lehrer", "Insegnante", "Professor", "先生", "선생님", "老师" }),
                ("A1",  new[] { "Student", "Öğrenci", "Estudiante", "Étudiant", "Schüler", "Studente", "Estudante", "学生", "학생", "学生" }),
                ("A1",  new[] { "School", "Okul", "Escuela", "École", "Schule", "Scuola", "Escola", "学校", "학교", "学校" }),
                ("A1",  new[] { "Book", "Kitap", "Libro", "Livre", "Buch", "Libro", "Livro", "本", "책", "书" }),
                ("A1",  new[] { "Pen", "Kalem", "Bolígrafo", "Stylo", "Stift", "Penna", "Caneta", "ペン", "펜", "笔" }),
                ("A1",  new[] { "Notebook", "Defter", "Cuaderno", "Cahier", "Heft", "Quaderno", "Caderno", "ノート", "공책", "笔记本" }),
                ("A1",  new[] { "Question", "Soru", "Pregunta", "Question", "Frage", "Domanda", "Pergunta", "質問", "질문", "问题" }),
                ("A1",  new[] { "Answer", "Cevap", "Respuesta", "Réponse", "Antwort", "Risposta", "Resposta", "答え", "대답", "回答" }),
                ("A1",  new[] { "Lesson", "Ders", "Lección", "Leçon", "Unterricht", "Lezione", "Aula", "授業", "수업", "课" }),
                ("A1",  new[] { "Classroom", "Sınıf", "Aula", "Salle de classe", "Klassenzimmer", "Aula", "Sala de aula", "教室", "교실", "教室" }),
                ("A2",  new[] { "Homework", "Ödev", "Deberes", "Devoirs", "Hausaufgaben", "Compiti", "Dever de casa", "宿題", "숙제", "作业" }),
                ("A2",  new[] { "Exam", "Sınav", "Examen", "Examen", "Prüfung", "Esame", "Prova", "試験", "시험", "考试" }),
                ("A2",  new[] { "Library", "Kütüphane", "Biblioteca", "Bibliothèque", "Bibliothek", "Biblioteca", "Biblioteca", "図書館", "도서관", "图书馆" }),
                ("A2",  new[] { "Grade", "Not", "Nota", "Note", "Note", "Voto", "Nota", "成績", "성적", "成绩" }),
                ("A2",  new[] { "Subject", "Konu", "Asignatura", "Matière", "Fach", "Materia", "Matéria", "科目", "과목", "科目" }),
                ("A2",  new[] { "Dictionary", "Sözlük", "Diccionario", "Dictionnaire", "Wörterbuch", "Dizionario", "Dicionário", "辞書", "사전", "词典" }),
                ("A2",  new[] { "Break", "Teneffüs", "Recreo", "Récréation", "Pause", "Ricreazione", "Recreio", "休み時間", "쉬는 시간", "课间休息" }),
                ("B1",  new[] { "University", "Üniversite", "Universidad", "Université", "Universität", "Università", "Universidade", "大学", "대학교", "大学" }),
                ("B1",  new[] { "Degree", "Diploma", "Título", "Diplôme", "Abschluss", "Laurea", "Diploma", "学位", "학위", "学位" }),
                ("B1",  new[] { "Research", "Araştırma", "Investigación", "Recherche", "Forschung", "Ricerca", "Pesquisa", "研究", "연구", "研究" }),
                ("B1",  new[] { "Essay", "Deneme", "Ensayo", "Dissertation", "Aufsatz", "Saggio", "Redação", "小論文", "에세이", "论文" }),
                ("B1",  new[] { "Attendance", "Devam", "Asistencia", "Présence", "Anwesenheit", "Presenza", "Frequência", "出席", "출석", "出勤" }),
                ("B1",  new[] { "Scholarship", "Burs", "Beca", "Bourse", "Stipendium", "Borsa di studio", "Bolsa", "奨学金", "장학금", "奖学金" }),
                ("B1+", new[] { "Curriculum", "Müfredat", "Plan de estudios", "Programme", "Lehrplan", "Programma di studi", "Currículo", "カリキュラム", "교육과정", "课程" }),
                ("B1+", new[] { "Deadline", "Son teslim tarihi", "Fecha límite", "Date limite", "Frist", "Scadenza", "Prazo", "締め切り", "마감일", "截止日期" }),
                ("B1+", new[] { "Semester", "Dönem", "Semestre", "Semestre", "Semester", "Semestre", "Semestre", "学期", "학기", "学期" }),
                ("B1+", new[] { "Tutor", "Özel öğretmen", "Tutor", "Tuteur", "Nachhilfelehrer", "Tutor", "Tutor", "家庭教師", "과외 교사", "家教" }),
                ("B2",  new[] { "Thesis", "Tez", "Tesis", "Thèse", "Abschlussarbeit", "Tesi", "Tese", "論文", "논문", "论文" }),
                ("B2",  new[] { "Lecture", "Konferans", "Conferencia", "Cours magistral", "Vorlesung", "Lezione magistrale", "Palestra", "講義", "강의", "讲座" }),
                ("B2",  new[] { "Assessment", "Değerlendirme", "Evaluación", "Évaluation", "Bewertung", "Valutazione", "Avaliação", "評価", "평가", "评估" }),
                ("B2",  new[] { "Enrolment", "Kayıt", "Matrícula", "Inscription", "Einschreibung", "Iscrizione", "Matrícula", "登録", "등록", "注册" }),
                ("C1",  new[] { "Pedagogy", "Pedagoji", "Pedagogía", "Pédagogie", "Pädagogik", "Pedagogia", "Pedagogia", "教育学", "교육학", "教育学" }),
                ("C1",  new[] { "Accreditation", "Akreditasyon", "Acreditación", "Accréditation", "Akkreditierung", "Accreditamento", "Acreditação", "認定", "인증", "认证" }),
                ("C1",  new[] { "Dissertation", "Doktora tezi", "Tesis doctoral", "Thèse de doctorat", "Dissertation", "Tesi di dottorato", "Dissertação", "博士論文", "박사 논문", "博士论文" }),
                ("C2",  new[] { "Cognition", "Biliş", "Cognición", "Cognition", "Kognition", "Cognizione", "Cognição", "認知", "인지", "认知" }),
                ("C2",  new[] { "Epistemology", "Epistemoloji", "Epistemología", "Épistémologie", "Erkenntnistheorie", "Epistemologia", "Epistemologia", "認識論", "인식론", "认识论" }),
            }),

        new(3, "movies", 6, "🎬", "#EC4899",
            new[] { "Movies", "Filmler", "Películas", "Films", "Filme", "Film", "Filmes", "映画", "영화", "电影" },
            new[] { "Cinema, series and what to watch", "Sinema, diziler ve ne izlesek", "Cine, series y qué ver", "Cinéma, séries et quoi regarder", "Kino, Serien und was man schaut", "Cinema, serie e cosa guardare", "Cinema, séries e o que assistir", "映画、ドラマ、何を観るか", "영화, 드라마 그리고 무엇을 볼까", "电影、剧集和看什么" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Movie", "Film", "Película", "Film", "Film", "Film", "Filme", "映画", "영화", "电影" }),
                ("A1",  new[] { "Actor", "Oyuncu", "Actor", "Acteur", "Schauspieler", "Attore", "Ator", "俳優", "배우", "演员" }),
                ("A1",  new[] { "Ticket", "Bilet", "Entrada", "Billet", "Karte", "Biglietto", "Ingresso", "チケット", "티켓", "票" }),
                ("A1",  new[] { "Cinema", "Sinema", "Cine", "Cinéma", "Kino", "Cinema", "Cinema", "映画館", "영화관", "电影院" }),
                ("A1",  new[] { "Story", "Hikâye", "Historia", "Histoire", "Geschichte", "Storia", "História", "物語", "이야기", "故事" }),
                ("A1",  new[] { "Comedy", "Komedi", "Comedia", "Comédie", "Komödie", "Commedia", "Comédia", "コメディ", "코미디", "喜剧" }),
                ("A1",  new[] { "Series", "Dizi", "Serie", "Série", "Serie", "Serie", "Série", "ドラマ", "드라마", "电视剧" }),
                ("A1",  new[] { "Seat", "Koltuk", "Asiento", "Siège", "Sitzplatz", "Posto", "Assento", "座席", "좌석", "座位" }),
                ("A1",  new[] { "Popcorn", "Patlamış mısır", "Palomitas", "Pop-corn", "Popcorn", "Popcorn", "Pipoca", "ポップコーン", "팝콘", "爆米花" }),
                ("A1",  new[] { "Poster", "Afiş", "Cartel", "Affiche", "Plakat", "Locandina", "Cartaz", "ポスター", "포스터", "海报" }),
                ("A2",  new[] { "Director", "Yönetmen", "Director", "Réalisateur", "Regisseur", "Regista", "Diretor", "監督", "감독", "导演" }),
                ("A2",  new[] { "Scene", "Sahne", "Escena", "Scène", "Szene", "Scena", "Cena", "シーン", "장면", "场景" }),
                ("A2",  new[] { "Subtitle", "Altyazı", "Subtítulo", "Sous-titre", "Untertitel", "Sottotitolo", "Legenda", "字幕", "자막", "字幕" }),
                ("A2",  new[] { "Award", "Ödül", "Premio", "Prix", "Preis", "Premio", "Prêmio", "賞", "상", "奖" }),
                ("A2",  new[] { "Character", "Karakter", "Personaje", "Personnage", "Figur", "Personaggio", "Personagem", "登場人物", "등장인물", "角色" }),
                ("A2",  new[] { "Horror", "Korku", "Terror", "Horreur", "Horror", "Horror", "Terror", "ホラー", "공포", "恐怖" }),
                ("A2",  new[] { "Trailer", "Fragman", "Tráiler", "Bande-annonce", "Trailer", "Trailer", "Trailer", "予告編", "예고편", "预告片" }),
                ("B1",  new[] { "Script", "Senaryo", "Guion", "Scénario", "Drehbuch", "Sceneggiatura", "Roteiro", "脚本", "각본", "剧本" }),
                ("B1",  new[] { "Producer", "Yapımcı", "Productor", "Producteur", "Produzent", "Produttore", "Produtor", "プロデューサー", "프로듀서", "制片人" }),
                ("B1",  new[] { "Plot", "Olay örgüsü", "Trama", "Intrigue", "Handlung", "Trama", "Enredo", "筋書き", "줄거리", "情节" }),
                ("B1",  new[] { "Soundtrack", "Film müziği", "Banda sonora", "Bande originale", "Filmmusik", "Colonna sonora", "Trilha sonora", "サウンドトラック", "사운드트랙", "原声带" }),
                ("B1",  new[] { "Review", "Eleştiri", "Reseña", "Critique", "Kritik", "Recensione", "Crítica", "レビュー", "리뷰", "影评" }),
                ("B1",  new[] { "Sequel", "Devam filmi", "Secuela", "Suite", "Fortsetzung", "Sequel", "Continuação", "続編", "속편", "续集" }),
                ("B1+", new[] { "Documentary", "Belgesel", "Documental", "Documentaire", "Dokumentarfilm", "Documentario", "Documentário", "ドキュメンタリー", "다큐멘터리", "纪录片" }),
                ("B1+", new[] { "Cast", "Oyuncu kadrosu", "Reparto", "Distribution", "Besetzung", "Cast", "Elenco", "キャスト", "출연진", "演员阵容" }),
                ("B1+", new[] { "Premiere", "Gala", "Estreno", "Première", "Premiere", "Prima", "Estreia", "初公開", "개봉", "首映" }),
                ("B1+", new[] { "Genre", "Tür", "Género", "Genre", "Genre", "Genere", "Gênero", "ジャンル", "장르", "类型" }),
                ("B2",  new[] { "Cinematography", "Sinematografi", "Cinematografía", "Photographie", "Kameraführung", "Fotografia", "Fotografia", "撮影技術", "촬영 기법", "摄影" }),
                ("B2",  new[] { "Adaptation", "Uyarlama", "Adaptación", "Adaptation", "Verfilmung", "Adattamento", "Adaptação", "脚色", "각색", "改编" }),
                ("B2",  new[] { "Box office", "Gişe", "Taquilla", "Box-office", "Kasse", "Botteghino", "Bilheteria", "興行収入", "흥행 수입", "票房" }),
                ("B2",  new[] { "Editing", "Kurgu", "Montaje", "Montage", "Schnitt", "Montaggio", "Montagem", "編集", "편집", "剪辑" }),
                ("C1",  new[] { "Protagonist", "Başkahraman", "Protagonista", "Protagoniste", "Protagonist", "Protagonista", "Protagonista", "主人公", "주인공", "主角" }),
                ("C1",  new[] { "Narrative", "Anlatı", "Narrativa", "Récit", "Erzählung", "Narrazione", "Narrativa", "語り", "서사", "叙事" }),
                ("C1",  new[] { "Cameo", "Konuk oyunculuk", "Cameo", "Caméo", "Cameo-Auftritt", "Cameo", "Participação especial", "カメオ出演", "카메오", "客串" }),
                ("C2",  new[] { "Allegory", "Alegori", "Alegoría", "Allégorie", "Allegorie", "Allegoria", "Alegoria", "寓意", "우화", "寓言" }),
                ("C2",  new[] { "Denouement", "Çözülme", "Desenlace", "Dénouement", "Auflösung", "Scioglimento", "Desenlace", "大詰め", "대단원", "结局" }),
            }),

        new(4, "music", 7, "🎵", "#F43F5E",
            new[] { "Music", "Müzik", "Música", "Musique", "Musik", "Musica", "Música", "音楽", "음악", "音乐" },
            new[] { "Songs, instruments and listening", "Şarkılar, çalgılar ve dinlemek", "Canciones, instrumentos y escuchar", "Chansons, instruments et écoute", "Lieder, Instrumente und Zuhören", "Canzoni, strumenti e ascolto", "Canções, instrumentos e ouvir", "歌、楽器、そして聴くこと", "노래, 악기 그리고 듣기", "歌曲、乐器和聆听" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Song", "Şarkı", "Canción", "Chanson", "Lied", "Canzone", "Canção", "歌", "노래", "歌曲" }),
                ("A1",  new[] { "Singer", "Şarkıcı", "Cantante", "Chanteur", "Sänger", "Cantante", "Cantor", "歌手", "가수", "歌手" }),
                ("A1",  new[] { "Guitar", "Gitar", "Guitarra", "Guitare", "Gitarre", "Chitarra", "Guitarra", "ギター", "기타", "吉他" }),
                ("A1",  new[] { "Piano", "Piyano", "Piano", "Piano", "Klavier", "Pianoforte", "Piano", "ピアノ", "피아노", "钢琴" }),
                ("A1",  new[] { "Drum", "Davul", "Tambor", "Tambour", "Trommel", "Tamburo", "Tambor", "太鼓", "드럼", "鼓" }),
                ("A1",  new[] { "Voice", "Ses", "Voz", "Voix", "Stimme", "Voce", "Voz", "声", "목소리", "嗓音" }),
                ("A1",  new[] { "Concert", "Konser", "Concierto", "Concert", "Konzert", "Concerto", "Concerto", "コンサート", "콘서트", "音乐会" }),
                ("A1",  new[] { "Band", "Grup", "Banda", "Groupe", "Band", "Gruppo", "Banda", "バンド", "밴드", "乐队" }),
                ("A1",  new[] { "Violin", "Keman", "Violín", "Violon", "Geige", "Violino", "Violino", "バイオリン", "바이올린", "小提琴" }),
                ("A1",  new[] { "Flute", "Flüt", "Flauta", "Flûte", "Flöte", "Flauto", "Flauta", "フルート", "플루트", "长笛" }),
                ("A2",  new[] { "Lyrics", "Söz", "Letra", "Paroles", "Liedtext", "Testo", "Letra", "歌詞", "가사", "歌词" }),
                ("A2",  new[] { "Rhythm", "Ritim", "Ritmo", "Rythme", "Rhythmus", "Ritmo", "Ritmo", "リズム", "리듬", "节奏" }),
                ("A2",  new[] { "Album", "Albüm", "Álbum", "Album", "Album", "Album", "Álbum", "アルバム", "앨범", "专辑" }),
                ("A2",  new[] { "Stage", "Sahne", "Escenario", "Scène", "Bühne", "Palco", "Palco", "舞台", "무대", "舞台" }),
                ("A2",  new[] { "Melody", "Melodi", "Melodía", "Mélodie", "Melodie", "Melodia", "Melodia", "旋律", "멜로디", "旋律" }),
                ("A2",  new[] { "Audience", "Seyirci", "Público", "Public", "Publikum", "Pubblico", "Plateia", "観客", "관객", "观众" }),
                ("A2",  new[] { "Dance", "Dans", "Baile", "Danse", "Tanz", "Danza", "Dança", "ダンス", "춤", "舞蹈" }),
                ("B1",  new[] { "Composer", "Besteci", "Compositor", "Compositeur", "Komponist", "Compositore", "Compositor", "作曲家", "작곡가", "作曲家" }),
                ("B1",  new[] { "Orchestra", "Orkestra", "Orquesta", "Orchestre", "Orchester", "Orchestra", "Orquestra", "オーケストラ", "오케스트라", "管弦乐团" }),
                ("B1",  new[] { "Chorus", "Nakarat", "Estribillo", "Refrain", "Refrain", "Ritornello", "Refrão", "サビ", "후렴", "副歌" }),
                ("B1",  new[] { "Instrument", "Enstrüman", "Instrumento", "Instrument", "Instrument", "Strumento", "Instrumento", "楽器", "악기", "乐器" }),
                ("B1",  new[] { "Rehearsal", "Prova", "Ensayo", "Répétition", "Probe", "Prova", "Ensaio", "リハーサル", "리허설", "排练" }),
                ("B1",  new[] { "Tune", "Ezgi", "Tonada", "Air", "Weise", "Motivo", "Melodia", "曲調", "곡조", "曲调" }),
                ("B1+", new[] { "Harmony", "Armoni", "Armonía", "Harmonie", "Harmonie", "Armonia", "Harmonia", "ハーモニー", "화음", "和声" }),
                ("B1+", new[] { "Genre", "Tür", "Género", "Genre", "Genre", "Genere", "Gênero", "ジャンル", "장르", "流派" }),
                ("B1+", new[] { "Performance", "Performans", "Actuación", "Représentation", "Auftritt", "Esibizione", "Apresentação", "演奏", "공연", "演出" }),
                ("B1+", new[] { "Recording", "Kayıt", "Grabación", "Enregistrement", "Aufnahme", "Registrazione", "Gravação", "録音", "녹음", "录音" }),
                ("B2",  new[] { "Improvisation", "Doğaçlama", "Improvisación", "Improvisation", "Improvisation", "Improvvisazione", "Improvisação", "即興", "즉흥 연주", "即兴" }),
                ("B2",  new[] { "Acoustics", "Akustik", "Acústica", "Acoustique", "Akustik", "Acustica", "Acústica", "音響", "음향", "声学" }),
                ("B2",  new[] { "Arrangement", "Düzenleme", "Arreglo", "Arrangement", "Arrangement", "Arrangiamento", "Arranjo", "編曲", "편곡", "编曲" }),
                ("B2",  new[] { "Pitch", "Perde", "Tono", "Hauteur", "Tonhöhe", "Intonazione", "Afinação", "音程", "음높이", "音高" }),
                ("C1",  new[] { "Counterpoint", "Kontrpuan", "Contrapunto", "Contrepoint", "Kontrapunkt", "Contrappunto", "Contraponto", "対位法", "대위법", "对位法" }),
                ("C1",  new[] { "Timbre", "Tını", "Timbre", "Timbre", "Klangfarbe", "Timbro", "Timbre", "音色", "음색", "音色" }),
                ("C1",  new[] { "Resonance", "Rezonans", "Resonancia", "Résonance", "Resonanz", "Risonanza", "Ressonância", "共鳴", "공명", "共鸣" }),
                ("C2",  new[] { "Polyphony", "Çokseslilik", "Polifonía", "Polyphonie", "Polyphonie", "Polifonia", "Polifonia", "多声音楽", "다성음악", "复调" }),
                ("C2",  new[] { "Cadence", "Kadans", "Cadencia", "Cadence", "Kadenz", "Cadenza", "Cadência", "終止形", "종지", "终止式" }),
            }),

        new(5, "gaming", 8, "🎮", "#10B981",
            new[] { "Gaming", "Oyun", "Videojuegos", "Jeux vidéo", "Gaming", "Videogiochi", "Games", "ゲーム", "게임", "电子游戏" },
            new[] { "Video games and playing online", "Video oyunları ve çevrimiçi oynamak", "Videojuegos y jugar en línea", "Jeux vidéo et jouer en ligne", "Videospiele und Online-Spielen", "Videogiochi e giocare online", "Videogames e jogar online", "ビデオゲームとオンライン対戦", "비디오 게임과 온라인 플레이", "电子游戏和在线游玩" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Game", "Oyun", "Juego", "Jeu", "Spiel", "Gioco", "Jogo", "ゲーム", "게임", "游戏" }),
                ("A1",  new[] { "Player", "Oyuncu", "Jugador", "Joueur", "Spieler", "Giocatore", "Jogador", "プレイヤー", "플레이어", "玩家" }),
                ("A1",  new[] { "Level", "Seviye", "Nivel", "Niveau", "Level", "Livello", "Nível", "レベル", "레벨", "关卡" }),
                ("A1",  new[] { "Score", "Puan", "Puntuación", "Score", "Punktzahl", "Punteggio", "Pontuação", "スコア", "점수", "分数" }),
                ("A1",  new[] { "Win", "Kazanmak", "Ganar", "Gagner", "Gewinnen", "Vincere", "Ganhar", "勝つ", "이기다", "赢" }),
                ("A1",  new[] { "Lose", "Kaybetmek", "Perder", "Perdre", "Verlieren", "Perdere", "Perder", "負ける", "지다", "输" }),
                ("A1",  new[] { "Team", "Takım", "Equipo", "Équipe", "Team", "Squadra", "Equipe", "チーム", "팀", "队伍" }),
                ("A1",  new[] { "Map", "Harita", "Mapa", "Carte", "Karte", "Mappa", "Mapa", "マップ", "지도", "地图" }),
                ("A1",  new[] { "Controller", "Kumanda", "Mando", "Manette", "Controller", "Controller", "Controle", "コントローラー", "컨트롤러", "手柄" }),
                ("A1",  new[] { "Character", "Karakter", "Personaje", "Personnage", "Charakter", "Personaggio", "Personagem", "キャラクター", "캐릭터", "角色" }),
                ("A2",  new[] { "Match", "Maç", "Partida", "Partie", "Partie", "Partita", "Partida", "試合", "경기", "比赛" }),
                ("A2",  new[] { "Weapon", "Silah", "Arma", "Arme", "Waffe", "Arma", "Arma", "武器", "무기", "武器" }),
                ("A2",  new[] { "Mission", "Görev", "Misión", "Mission", "Mission", "Missione", "Missão", "ミッション", "미션", "任务" }),
                ("A2",  new[] { "Health", "Can", "Salud", "Santé", "Leben", "Salute", "Vida", "体力", "체력", "生命值" }),
                ("A2",  new[] { "Speed", "Hız", "Velocidad", "Vitesse", "Geschwindigkeit", "Velocità", "Velocidade", "速度", "속도", "速度" }),
                ("A2",  new[] { "Reward", "Ödül", "Recompensa", "Récompense", "Belohnung", "Ricompensa", "Recompensa", "報酬", "보상", "奖励" }),
                ("A2",  new[] { "Server", "Sunucu", "Servidor", "Serveur", "Server", "Server", "Servidor", "サーバー", "서버", "服务器" }),
                ("B1",  new[] { "Achievement", "Başarım", "Logro", "Succès", "Erfolg", "Obiettivo", "Conquista", "実績", "업적", "成就" }),
                ("B1",  new[] { "Inventory", "Envanter", "Inventario", "Inventaire", "Inventar", "Inventario", "Inventário", "持ち物", "인벤토리", "背包" }),
                ("B1",  new[] { "Checkpoint", "Kontrol noktası", "Punto de control", "Point de contrôle", "Kontrollpunkt", "Checkpoint", "Ponto de salvamento", "チェックポイント", "체크포인트", "存档点" }),
                ("B1",  new[] { "Skill", "Yetenek", "Habilidad", "Compétence", "Fähigkeit", "Abilità", "Habilidade", "スキル", "스킬", "技能" }),
                ("B1",  new[] { "Upgrade", "Yükseltme", "Mejora", "Amélioration", "Verbesserung", "Potenziamento", "Melhoria", "アップグレード", "업그레이드", "升级" }),
                ("B1",  new[] { "Lag", "Gecikme", "Retardo", "Latence", "Verzögerung", "Ritardo", "Atraso", "ラグ", "렉", "延迟" }),
                ("B1+", new[] { "Strategy", "Strateji", "Estrategia", "Stratégie", "Strategie", "Strategia", "Estratégia", "戦略", "전략", "策略" }),
                ("B1+", new[] { "Tournament", "Turnuva", "Torneo", "Tournoi", "Turnier", "Torneo", "Torneio", "トーナメント", "토너먼트", "锦标赛" }),
                ("B1+", new[] { "Difficulty", "Zorluk", "Dificultad", "Difficulté", "Schwierigkeit", "Difficoltà", "Dificuldade", "難易度", "난이도", "难度" }),
                ("B1+", new[] { "Respawn", "Yeniden doğma", "Reaparición", "Réapparition", "Wiedereinstieg", "Rinascita", "Renascimento", "復活", "리스폰", "重生" }),
                ("B2",  new[] { "Matchmaking", "Eşleştirme", "Emparejamiento", "Matchmaking", "Spielersuche", "Matchmaking", "Emparelhamento", "マッチメイキング", "매치메이킹", "匹配" }),
                ("B2",  new[] { "Leaderboard", "Sıralama tablosu", "Clasificación", "Classement", "Bestenliste", "Classifica", "Placar", "ランキング", "순위표", "排行榜" }),
                ("B2",  new[] { "Expansion", "Genişleme paketi", "Expansión", "Extension", "Erweiterung", "Espansione", "Expansão", "拡張版", "확장팩", "资料片" }),
                ("B2",  new[] { "Cooperative", "İşbirlikçi", "Cooperativo", "Coopératif", "Kooperativ", "Cooperativo", "Cooperativo", "協力プレイ", "협동", "合作" }),
                ("C1",  new[] { "Immersion", "Kendini kaptırma", "Inmersión", "Immersion", "Immersion", "Immersione", "Imersão", "没入感", "몰입", "沉浸感" }),
                ("C1",  new[] { "Mechanics", "Oyun mekanikleri", "Mecánicas", "Mécaniques", "Spielmechanik", "Meccaniche", "Mecânicas", "ゲーム性", "게임 메커니즘", "玩法机制" }),
                ("C1",  new[] { "Rendering", "Görüntüleme", "Renderizado", "Rendu", "Darstellung", "Rendering", "Renderização", "描画", "렌더링", "渲染" }),
                ("C2",  new[] { "Procedural", "Yordamsal", "Procedimental", "Procédural", "Prozedural", "Procedurale", "Procedural", "自動生成の", "절차적", "程序化" }),
                ("C2",  new[] { "Emergent", "Kendiliğinden beliren", "Emergente", "Émergent", "Emergent", "Emergente", "Emergente", "創発的", "창발적", "涌现的" }),
            }),

        new(6, "sports", 9, "⚽", "#22C55E",
            new[] { "Sports", "Spor", "Deportes", "Sport", "Sport", "Sport", "Esportes", "スポーツ", "스포츠", "运动" },
            new[] { "Games, training and keeping fit", "Maçlar, antrenman ve forma girmek", "Partidos, entrenamiento y estar en forma", "Matchs, entraînement et forme physique", "Spiele, Training und Fitness", "Partite, allenamento e forma fisica", "Jogos, treino e ficar em forma", "試合、トレーニング、体づくり", "경기, 훈련 그리고 체력 관리", "比赛、训练和保持健康" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Football", "Futbol", "Fútbol", "Football", "Fußball", "Calcio", "Futebol", "サッカー", "축구", "足球" }),
                ("A1",  new[] { "Ball", "Top", "Pelota", "Balle", "Ball", "Palla", "Bola", "ボール", "공", "球" }),
                ("A1",  new[] { "Run", "Koşmak", "Correr", "Courir", "Laufen", "Correre", "Correr", "走る", "달리다", "跑" }),
                ("A1",  new[] { "Swim", "Yüzmek", "Nadar", "Nager", "Schwimmen", "Nuotare", "Nadar", "泳ぐ", "수영하다", "游泳" }),
                ("A1",  new[] { "Team", "Takım", "Equipo", "Équipe", "Mannschaft", "Squadra", "Time", "チーム", "팀", "球队" }),
                ("A1",  new[] { "Match", "Maç", "Partido", "Match", "Spiel", "Partita", "Jogo", "試合", "경기", "比赛" }),
                ("A1",  new[] { "Player", "Oyuncu", "Jugador", "Joueur", "Spieler", "Giocatore", "Jogador", "選手", "선수", "球员" }),
                ("A1",  new[] { "Jump", "Zıplamak", "Saltar", "Sauter", "Springen", "Saltare", "Pular", "跳ぶ", "뛰다", "跳" }),
                ("A1",  new[] { "Goal", "Gol", "Gol", "But", "Tor", "Gol", "Gol", "ゴール", "골", "进球" }),
                ("A1",  new[] { "Bicycle", "Bisiklet", "Bicicleta", "Vélo", "Fahrrad", "Bicicletta", "Bicicleta", "自転車", "자전거", "自行车" }),
                ("A2",  new[] { "Coach", "Antrenör", "Entrenador", "Entraîneur", "Trainer", "Allenatore", "Treinador", "コーチ", "코치", "教练" }),
                ("A2",  new[] { "Stadium", "Stat", "Estadio", "Stade", "Stadion", "Stadio", "Estádio", "スタジアム", "경기장", "体育场" }),
                ("A2",  new[] { "Training", "Antrenman", "Entrenamiento", "Entraînement", "Training", "Allenamento", "Treino", "トレーニング", "훈련", "训练" }),
                ("A2",  new[] { "Victory", "Zafer", "Victoria", "Victoire", "Sieg", "Vittoria", "Vitória", "勝利", "승리", "胜利" }),
                ("A2",  new[] { "Referee", "Hakem", "Árbitro", "Arbitre", "Schiedsrichter", "Arbitro", "Árbitro", "審判", "심판", "裁判" }),
                ("A2",  new[] { "Muscle", "Kas", "Músculo", "Muscle", "Muskel", "Muscolo", "Músculo", "筋肉", "근육", "肌肉" }),
                ("A2",  new[] { "Race", "Yarış", "Carrera", "Course", "Rennen", "Corsa", "Corrida", "レース", "경주", "赛跑" }),
                ("B1",  new[] { "Championship", "Şampiyona", "Campeonato", "Championnat", "Meisterschaft", "Campionato", "Campeonato", "選手権", "선수권", "锦标赛" }),
                ("B1",  new[] { "Injury", "Sakatlık", "Lesión", "Blessure", "Verletzung", "Infortunio", "Lesão", "けが", "부상", "受伤" }),
                ("B1",  new[] { "Fitness", "Kondisyon", "Forma física", "Condition physique", "Fitness", "Forma fisica", "Condicionamento", "体力づくり", "체력", "健身" }),
                ("B1",  new[] { "Defence", "Savunma", "Defensa", "Défense", "Verteidigung", "Difesa", "Defesa", "守備", "수비", "防守" }),
                ("B1",  new[] { "Attack", "Hücum", "Ataque", "Attaque", "Angriff", "Attacco", "Ataque", "攻撃", "공격", "进攻" }),
                ("B1",  new[] { "Endurance", "Dayanıklılık", "Resistencia", "Endurance", "Ausdauer", "Resistenza", "Resistência", "持久力", "지구력", "耐力" }),
                ("B1+", new[] { "Tournament", "Turnuva", "Torneo", "Tournoi", "Turnier", "Torneo", "Torneio", "トーナメント", "토너먼트", "锦标赛" }),
                ("B1+", new[] { "Substitute", "Yedek oyuncu", "Suplente", "Remplaçant", "Auswechselspieler", "Riserva", "Reserva", "控え選手", "교체 선수", "替补" }),
                ("B1+", new[] { "Penalty", "Ceza", "Penalti", "Pénalité", "Strafstoß", "Rigore", "Pênalti", "ペナルティ", "페널티", "点球" }),
                ("B1+", new[] { "Warm-up", "Isınma", "Calentamiento", "Échauffement", "Aufwärmen", "Riscaldamento", "Aquecimento", "ウォームアップ", "준비 운동", "热身" }),
                ("B2",  new[] { "Stamina", "Dayanma gücü", "Aguante", "Endurance physique", "Kondition", "Vigore", "Vigor", "スタミナ", "스태미나", "体能" }),
                ("B2",  new[] { "Tactics", "Taktik", "Táctica", "Tactique", "Taktik", "Tattica", "Tática", "戦術", "전술", "战术" }),
                ("B2",  new[] { "Qualification", "Eleme", "Clasificación", "Qualification", "Qualifikation", "Qualificazione", "Classificação", "予選", "예선", "资格赛" }),
                ("B2",  new[] { "Doping", "Doping", "Dopaje", "Dopage", "Doping", "Doping", "Doping", "ドーピング", "도핑", "兴奋剂" }),
                ("C1",  new[] { "Rehabilitation", "Rehabilitasyon", "Rehabilitación", "Rééducation", "Rehabilitation", "Riabilitazione", "Reabilitação", "リハビリ", "재활", "康复" }),
                ("C1",  new[] { "Aerobic", "Aerobik", "Aeróbico", "Aérobie", "Aerob", "Aerobico", "Aeróbico", "有酸素の", "유산소", "有氧" }),
                ("C1",  new[] { "Momentum", "İvme", "Impulso", "Élan", "Schwung", "Slancio", "Impulso", "勢い", "기세", "势头" }),
                ("C2",  new[] { "Periodisation", "Periyotlama", "Periodización", "Périodisation", "Periodisierung", "Periodizzazione", "Periodização", "期分け", "주기화", "周期化" }),
                ("C2",  new[] { "Biomechanics", "Biyomekanik", "Biomecánica", "Biomécanique", "Biomechanik", "Biomeccanica", "Biomecânica", "生体力学", "생체역학", "生物力学" }),
            }),

        new(7, "health", 10, "❤️", "#EF4444",
            new[] { "Health", "Sağlık", "Salud", "Santé", "Gesundheit", "Salute", "Saúde", "健康", "건강", "健康" },
            new[] { "The body, feeling ill and the doctor", "Vücut, hastalık ve doktor", "El cuerpo, la enfermedad y el médico", "Le corps, la maladie et le médecin", "Körper, Krankheit und Arztbesuch", "Il corpo, la malattia e il medico", "O corpo, a doença e o médico", "体調と病院で使う言葉", "몸, 아플 때 그리고 병원", "身体、生病和看医生" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Doctor", "Doktor", "Médico", "Médecin", "Arzt", "Medico", "Médico", "医者", "의사", "医生" }),
                ("A1",  new[] { "Hospital", "Hastane", "Hospital", "Hôpital", "Krankenhaus", "Ospedale", "Hospital", "病院", "병원", "医院" }),
                ("A1",  new[] { "Medicine", "İlaç", "Medicina", "Médicament", "Medikament", "Medicina", "Remédio", "薬", "약", "药" }),
                ("A1",  new[] { "Pain", "Ağrı", "Dolor", "Douleur", "Schmerz", "Dolore", "Dor", "痛み", "통증", "疼痛" }),
                ("A1",  new[] { "Fever", "Ateş", "Fiebre", "Fièvre", "Fieber", "Febbre", "Febre", "熱", "열", "发烧" }),
                ("A1",  new[] { "Nurse", "Hemşire", "Enfermera", "Infirmière", "Krankenschwester", "Infermiera", "Enfermeira", "看護師", "간호사", "护士" }),
                ("A1",  new[] { "Sleep", "Uyku", "Sueño", "Sommeil", "Schlaf", "Sonno", "Sono", "睡眠", "잠", "睡眠" }),
                ("A1",  new[] { "Blood", "Kan", "Sangre", "Sang", "Blut", "Sangue", "Sangue", "血", "피", "血" }),
                ("A1",  new[] { "Head", "Baş", "Cabeza", "Tête", "Kopf", "Testa", "Cabeça", "頭", "머리", "头" }),
                ("A1",  new[] { "Tooth", "Diş", "Diente", "Dent", "Zahn", "Dente", "Dente", "歯", "이", "牙齿" }),
                ("A2",  new[] { "Healthy", "Sağlıklı", "Sano", "Sain", "Gesund", "Sano", "Saudável", "健康な", "건강한", "健康的" }),
                ("A2",  new[] { "Appointment", "Randevu", "Cita", "Rendez-vous", "Termin", "Appuntamento", "Consulta", "予約", "예약", "预约" }),
                ("A2",  new[] { "Cough", "Öksürük", "Tos", "Toux", "Husten", "Tosse", "Tosse", "せき", "기침", "咳嗽" }),
                ("A2",  new[] { "Pharmacy", "Eczane", "Farmacia", "Pharmacie", "Apotheke", "Farmacia", "Farmácia", "薬局", "약국", "药店" }),
                ("A2",  new[] { "Injury", "Yaralanma", "Herida", "Blessure", "Verletzung", "Ferita", "Ferimento", "けが", "부상", "受伤" }),
                ("A2",  new[] { "Diet", "Beslenme", "Dieta", "Régime", "Ernährung", "Dieta", "Dieta", "食事", "식단", "饮食" }),
                ("A2",  new[] { "Exercise", "Egzersiz", "Ejercicio", "Exercice", "Bewegung", "Esercizio", "Exercício", "運動", "운동", "锻炼" }),
                ("B1",  new[] { "Treatment", "Tedavi", "Tratamiento", "Traitement", "Behandlung", "Trattamento", "Tratamento", "治療", "치료", "治疗" }),
                ("B1",  new[] { "Symptom", "Belirti", "Síntoma", "Symptôme", "Symptom", "Sintomo", "Sintoma", "症状", "증상", "症状" }),
                ("B1",  new[] { "Surgery", "Ameliyat", "Cirugía", "Chirurgie", "Operation", "Chirurgia", "Cirurgia", "手術", "수술", "手术" }),
                ("B1",  new[] { "Vaccine", "Aşı", "Vacuna", "Vaccin", "Impfstoff", "Vaccino", "Vacina", "ワクチン", "백신", "疫苗" }),
                ("B1",  new[] { "Allergy", "Alerji", "Alergia", "Allergie", "Allergie", "Allergia", "Alergia", "アレルギー", "알레르기", "过敏" }),
                ("B1",  new[] { "Recovery", "İyileşme", "Recuperación", "Guérison", "Genesung", "Guarigione", "Recuperação", "回復", "회복", "康复" }),
                ("B1+", new[] { "Diagnosis", "Teşhis", "Diagnóstico", "Diagnostic", "Diagnose", "Diagnosi", "Diagnóstico", "診断", "진단", "诊断" }),
                ("B1+", new[] { "Prescription", "Reçete", "Receta", "Ordonnance", "Rezept", "Ricetta", "Receita", "処方箋", "처방전", "处方" }),
                ("B1+", new[] { "Infection", "Enfeksiyon", "Infección", "Infection", "Infektion", "Infezione", "Infecção", "感染", "감염", "感染" }),
                ("B1+", new[] { "Immune", "Bağışıklık", "Inmune", "Immunitaire", "Immun", "Immune", "Imune", "免疫の", "면역", "免疫" }),
                ("B2",  new[] { "Chronic", "Kronik", "Crónico", "Chronique", "Chronisch", "Cronico", "Crônico", "慢性の", "만성", "慢性" }),
                ("B2",  new[] { "Therapy", "Terapi", "Terapia", "Thérapie", "Therapie", "Terapia", "Terapia", "療法", "치료법", "疗法" }),
                ("B2",  new[] { "Nutrition", "Beslenme bilimi", "Nutrición", "Nutrition", "Ernährungslehre", "Nutrizione", "Nutrição", "栄養", "영양", "营养" }),
                ("B2",  new[] { "Dosage", "Doz", "Dosis", "Dosage", "Dosierung", "Dosaggio", "Dosagem", "用量", "용량", "剂量" }),
                ("C1",  new[] { "Prognosis", "Prognoz", "Pronóstico", "Pronostic", "Prognose", "Prognosi", "Prognóstico", "予後", "예후", "预后" }),
                ("C1",  new[] { "Inflammation", "İltihap", "Inflamación", "Inflammation", "Entzündung", "Infiammazione", "Inflamação", "炎症", "염증", "炎症" }),
                ("C1",  new[] { "Metabolism", "Metabolizma", "Metabolismo", "Métabolisme", "Stoffwechsel", "Metabolismo", "Metabolismo", "代謝", "신진대사", "新陈代谢" }),
                ("C2",  new[] { "Pathology", "Patoloji", "Patología", "Pathologie", "Pathologie", "Patologia", "Patologia", "病理学", "병리학", "病理学" }),
                ("C2",  new[] { "Epidemiology", "Epidemiyoloji", "Epidemiología", "Épidémiologie", "Epidemiologie", "Epidemiologia", "Epidemiologia", "疫学", "역학", "流行病学" }),
            }),

        new(8, "shopping", 11, "🛍️", "#F59E0B",
            new[] { "Shopping", "Alışveriş", "Compras", "Achats", "Einkaufen", "Shopping", "Compras", "買い物", "쇼핑", "购物" },
            new[] { "Buying, paying and what to wear", "Almak, ödemek ve ne giymek", "Comprar, pagar y qué ponerse", "Acheter, payer et quoi porter", "Kaufen, bezahlen und was man anzieht", "Comprare, pagare e cosa indossare", "Comprar, pagar e o que vestir", "買う、払う、何を着るか", "사고, 계산하고, 무엇을 입을까", "购买、付款和穿什么" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Price", "Fiyat", "Precio", "Prix", "Preis", "Prezzo", "Preço", "値段", "가격", "价格" }),
                ("A1",  new[] { "Money", "Para", "Dinero", "Argent", "Geld", "Denaro", "Dinheiro", "お金", "돈", "钱" }),
                ("A1",  new[] { "Shop", "Mağaza", "Tienda", "Magasin", "Geschäft", "Negozio", "Loja", "店", "가게", "商店" }),
                ("A1",  new[] { "Buy", "Satın almak", "Comprar", "Acheter", "Kaufen", "Comprare", "Comprar", "買う", "사다", "买" }),
                ("A1",  new[] { "Shirt", "Gömlek", "Camisa", "Chemise", "Hemd", "Camicia", "Camisa", "シャツ", "셔츠", "衬衫" }),
                ("A1",  new[] { "Shoes", "Ayakkabı", "Zapatos", "Chaussures", "Schuhe", "Scarpe", "Sapatos", "靴", "신발", "鞋" }),
                ("A1",  new[] { "Size", "Beden", "Talla", "Taille", "Größe", "Taglia", "Tamanho", "サイズ", "사이즈", "尺码" }),
                ("A1",  new[] { "Bag", "Çanta", "Bolso", "Sac", "Tasche", "Borsa", "Bolsa", "かばん", "가방", "包" }),
                ("A1",  new[] { "Cheap", "Ucuz", "Barato", "Bon marché", "Billig", "Economico", "Barato", "安い", "싸다", "便宜" }),
                ("A1",  new[] { "Expensive", "Pahalı", "Caro", "Cher", "Teuer", "Costoso", "Caro", "高い", "비싸다", "贵" }),
                ("A2",  new[] { "Discount", "İndirim", "Descuento", "Réduction", "Rabatt", "Sconto", "Desconto", "割引", "할인", "折扣" }),
                ("A2",  new[] { "Receipt", "Fiş", "Recibo", "Reçu", "Quittung", "Scontrino", "Recibo", "レシート", "영수증", "收据" }),
                ("A2",  new[] { "Cash", "Nakit", "Efectivo", "Espèces", "Bargeld", "Contanti", "Dinheiro vivo", "現金", "현금", "现金" }),
                ("A2",  new[] { "Customer", "Müşteri", "Cliente", "Client", "Kunde", "Cliente", "Cliente", "客", "손님", "顾客" }),
                ("A2",  new[] { "Market", "Pazar", "Mercado", "Marché", "Markt", "Mercato", "Mercado", "市場", "시장", "市场" }),
                ("A2",  new[] { "Trousers", "Pantolon", "Pantalones", "Pantalon", "Hose", "Pantaloni", "Calça", "ズボン", "바지", "裤子" }),
                ("A2",  new[] { "Jacket", "Ceket", "Chaqueta", "Veste", "Jacke", "Giacca", "Jaqueta", "ジャケット", "재킷", "夹克" }),
                ("B1",  new[] { "Refund", "İade", "Reembolso", "Remboursement", "Rückerstattung", "Rimborso", "Reembolso", "返金", "환불", "退款" }),
                ("B1",  new[] { "Brand", "Marka", "Marca", "Marque", "Marke", "Marca", "Marca", "ブランド", "브랜드", "品牌" }),
                ("B1",  new[] { "Quality", "Kalite", "Calidad", "Qualité", "Qualität", "Qualità", "Qualidade", "品質", "품질", "质量" }),
                ("B1",  new[] { "Delivery", "Teslimat", "Entrega", "Livraison", "Lieferung", "Consegna", "Entrega", "配達", "배송", "配送" }),
                ("B1",  new[] { "Exchange", "Değişim", "Cambio", "Échange", "Umtausch", "Cambio", "Troca", "交換", "교환", "换货" }),
                ("B1",  new[] { "Fitting room", "Deneme kabini", "Probador", "Cabine d'essayage", "Umkleidekabine", "Camerino", "Provador", "試着室", "탈의실", "试衣间" }),
                ("B1+", new[] { "Warranty", "Garanti", "Garantía", "Garantie", "Garantie", "Garanzia", "Garantia", "保証", "보증", "保修" }),
                ("B1+", new[] { "Bargain", "Kelepir", "Ganga", "Bonne affaire", "Schnäppchen", "Affare", "Pechincha", "掘り出し物", "특가품", "便宜货" }),
                ("B1+", new[] { "Invoice", "Fatura", "Factura", "Facture", "Rechnung", "Fattura", "Fatura", "請求書", "청구서", "发票" }),
                ("B1+", new[] { "Instalment", "Taksit", "Cuota", "Versement", "Rate", "Rata", "Prestação", "分割払い", "할부", "分期" }),
                ("B2",  new[] { "Retail", "Perakende", "Venta al por menor", "Vente au détail", "Einzelhandel", "Vendita al dettaglio", "Varejo", "小売", "소매", "零售" }),
                ("B2",  new[] { "Wholesale", "Toptan", "Venta al por mayor", "Vente en gros", "Großhandel", "Vendita all'ingrosso", "Atacado", "卸売", "도매", "批发" }),
                ("B2",  new[] { "Inventory", "Stok", "Inventario", "Inventaire", "Bestand", "Inventario", "Estoque", "在庫", "재고", "库存" }),
                ("B2",  new[] { "Consumer", "Tüketici", "Consumidor", "Consommateur", "Verbraucher", "Consumatore", "Consumidor", "消費者", "소비자", "消费者" }),
                ("C1",  new[] { "Merchandising", "Ürün yerleştirme", "Merchandising", "Merchandisage", "Warenpräsentation", "Merchandising", "Merchandising", "商品化計画", "머천다이징", "商品企划" }),
                ("C1",  new[] { "Depreciation", "Değer kaybı", "Depreciación", "Dépréciation", "Wertminderung", "Deprezzamento", "Depreciação", "減価", "감가상각", "折旧" }),
                ("C1",  new[] { "Procurement", "Tedarik", "Adquisición", "Approvisionnement", "Beschaffung", "Approvvigionamento", "Aquisição", "調達", "조달", "采购" }),
                ("C2",  new[] { "Elasticity", "Esneklik", "Elasticidad", "Élasticité", "Elastizität", "Elasticità", "Elasticidade", "弾力性", "탄력성", "弹性" }),
                ("C2",  new[] { "Arbitrage", "Arbitraj", "Arbitraje", "Arbitrage", "Arbitrage", "Arbitraggio", "Arbitragem", "裁定取引", "차익거래", "套利" }),
            }),

        new(9, "nature", 13, "🌳", "#84CC16",
            new[] { "Nature", "Doğa", "Naturaleza", "Nature", "Natur", "Natura", "Natureza", "自然", "자연", "大自然" },
            new[] { "Weather, landscape and the outdoors", "Hava, manzara ve açık hava", "Clima, paisaje y aire libre", "Météo, paysage et plein air", "Wetter, Landschaft und Natur", "Tempo, paesaggio e aria aperta", "Clima, paisagem e ar livre", "天気、風景、そして戸外", "날씨, 풍경 그리고 야외", "天气、风景和户外" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Tree", "Ağaç", "Árbol", "Arbre", "Baum", "Albero", "Árvore", "木", "나무", "树" }),
                ("A1",  new[] { "River", "Nehir", "Río", "Rivière", "Fluss", "Fiume", "Rio", "川", "강", "河" }),
                ("A1",  new[] { "Mountain", "Dağ", "Montaña", "Montagne", "Berg", "Montagna", "Montanha", "山", "산", "山" }),
                ("A1",  new[] { "Sea", "Deniz", "Mar", "Mer", "Meer", "Mare", "Mar", "海", "바다", "海" }),
                ("A1",  new[] { "Rain", "Yağmur", "Lluvia", "Pluie", "Regen", "Pioggia", "Chuva", "雨", "비", "雨" }),
                ("A1",  new[] { "Sun", "Güneş", "Sol", "Soleil", "Sonne", "Sole", "Sol", "太陽", "태양", "太阳" }),
                ("A1",  new[] { "Forest", "Orman", "Bosque", "Forêt", "Wald", "Foresta", "Floresta", "森", "숲", "森林" }),
                ("A1",  new[] { "Flower", "Çiçek", "Flor", "Fleur", "Blume", "Fiore", "Flor", "花", "꽃", "花" }),
                ("A1",  new[] { "Wind", "Rüzgâr", "Viento", "Vent", "Wind", "Vento", "Vento", "風", "바람", "风" }),
                ("A1",  new[] { "Sky", "Gökyüzü", "Cielo", "Ciel", "Himmel", "Cielo", "Céu", "空", "하늘", "天空" }),
                ("A2",  new[] { "Snow", "Kar", "Nieve", "Neige", "Schnee", "Neve", "Neve", "雪", "눈", "雪" }),
                ("A2",  new[] { "Lake", "Göl", "Lago", "Lac", "See", "Lago", "Lago", "湖", "호수", "湖" }),
                ("A2",  new[] { "Beach", "Plaj", "Playa", "Plage", "Strand", "Spiaggia", "Praia", "浜辺", "해변", "海滩" }),
                ("A2",  new[] { "Cloud", "Bulut", "Nube", "Nuage", "Wolke", "Nuvola", "Nuvem", "雲", "구름", "云" }),
                ("A2",  new[] { "Leaf", "Yaprak", "Hoja", "Feuille", "Blatt", "Foglia", "Folha", "葉", "잎", "叶子" }),
                ("A2",  new[] { "Stone", "Taş", "Piedra", "Pierre", "Stein", "Pietra", "Pedra", "石", "돌", "石头" }),
                ("A2",  new[] { "Island", "Ada", "Isla", "Île", "Insel", "Isola", "Ilha", "島", "섬", "岛" }),
                ("B1",  new[] { "Climate", "İklim", "Clima", "Climat", "Klima", "Clima", "Clima", "気候", "기후", "气候" }),
                ("B1",  new[] { "Desert", "Çöl", "Desierto", "Désert", "Wüste", "Deserto", "Deserto", "砂漠", "사막", "沙漠" }),
                ("B1",  new[] { "Valley", "Vadi", "Valle", "Vallée", "Tal", "Valle", "Vale", "谷", "계곡", "山谷" }),
                ("B1",  new[] { "Soil", "Toprak", "Suelo", "Sol", "Boden", "Suolo", "Solo", "土壌", "토양", "土壤" }),
                ("B1",  new[] { "Species", "Tür", "Especie", "Espèce", "Art", "Specie", "Espécie", "種", "종", "物种" }),
                ("B1",  new[] { "Waterfall", "Şelale", "Cascada", "Cascade", "Wasserfall", "Cascata", "Cachoeira", "滝", "폭포", "瀑布" }),
                ("B1+", new[] { "Environment", "Çevre", "Medio ambiente", "Environnement", "Umwelt", "Ambiente", "Meio ambiente", "環境", "환경", "环境" }),
                ("B1+", new[] { "Pollution", "Kirlilik", "Contaminación", "Pollution", "Verschmutzung", "Inquinamento", "Poluição", "汚染", "오염", "污染" }),
                ("B1+", new[] { "Glacier", "Buzul", "Glaciar", "Glacier", "Gletscher", "Ghiacciaio", "Geleira", "氷河", "빙하", "冰川" }),
                ("B1+", new[] { "Habitat", "Yaşam alanı", "Hábitat", "Habitat", "Lebensraum", "Habitat", "Habitat", "生息地", "서식지", "栖息地" }),
                ("B2",  new[] { "Ecosystem", "Ekosistem", "Ecosistema", "Écosystème", "Ökosystem", "Ecosistema", "Ecossistema", "生態系", "생태계", "生态系统" }),
                ("B2",  new[] { "Erosion", "Erozyon", "Erosión", "Érosion", "Erosion", "Erosione", "Erosão", "侵食", "침식", "侵蚀" }),
                ("B2",  new[] { "Renewable", "Yenilenebilir", "Renovable", "Renouvelable", "Erneuerbar", "Rinnovabile", "Renovável", "再生可能な", "재생 가능", "可再生" }),
                ("B2",  new[] { "Drought", "Kuraklık", "Sequía", "Sécheresse", "Dürre", "Siccità", "Seca", "干ばつ", "가뭄", "干旱" }),
                ("C1",  new[] { "Biodiversity", "Biyoçeşitlilik", "Biodiversidad", "Biodiversité", "Biodiversität", "Biodiversità", "Biodiversidade", "生物多様性", "생물 다양성", "生物多样性" }),
                ("C1",  new[] { "Sustainability", "Sürdürülebilirlik", "Sostenibilidad", "Durabilité", "Nachhaltigkeit", "Sostenibilità", "Sustentabilidade", "持続可能性", "지속 가능성", "可持续性" }),
                ("C1",  new[] { "Sediment", "Tortu", "Sedimento", "Sédiment", "Sediment", "Sedimento", "Sedimento", "堆積物", "퇴적물", "沉积物" }),
                ("C2",  new[] { "Symbiosis", "Ortak yaşam", "Simbiosis", "Symbiose", "Symbiose", "Simbiosi", "Simbiose", "共生", "공생", "共生" }),
                ("C2",  new[] { "Anthropogenic", "İnsan kaynaklı", "Antropogénico", "Anthropique", "Anthropogen", "Antropogenico", "Antropogênico", "人為的な", "인위적", "人为的" }),
            }),

        new(10, "science", 14, "🔬", "#0EA5E9",
            new[] { "Science", "Bilim", "Ciencia", "Science", "Wissenschaft", "Scienza", "Ciência", "科学", "과학", "科学" },
            new[] { "Research, experiments and discovery", "Araştırma, deney ve keşif", "Investigación, experimentos y descubrimiento", "Recherche, expériences et découverte", "Forschung, Experimente und Entdeckung", "Ricerca, esperimenti e scoperta", "Pesquisa, experimentos e descoberta", "研究、実験、そして発見", "연구, 실험 그리고 발견", "研究、实验和发现" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Water", "Su", "Agua", "Eau", "Wasser", "Acqua", "Água", "水", "물", "水" }),
                ("A1",  new[] { "Fire", "Ateş", "Fuego", "Feu", "Feuer", "Fuoco", "Fogo", "火", "불", "火" }),
                ("A1",  new[] { "Light", "Işık", "Luz", "Lumière", "Licht", "Luce", "Luz", "光", "빛", "光" }),
                ("A1",  new[] { "Air", "Hava", "Aire", "Air", "Luft", "Aria", "Ar", "空気", "공기", "空气" }),
                ("A1",  new[] { "Heat", "Isı", "Calor", "Chaleur", "Wärme", "Calore", "Calor", "熱", "열", "热" }),
                ("A1",  new[] { "Star", "Yıldız", "Estrella", "Étoile", "Stern", "Stella", "Estrela", "星", "별", "星星" }),
                ("A1",  new[] { "Moon", "Ay", "Luna", "Lune", "Mond", "Luna", "Lua", "月", "달", "月亮" }),
                ("A1",  new[] { "Earth", "Dünya", "Tierra", "Terre", "Erde", "Terra", "Terra", "地球", "지구", "地球" }),
                ("A1",  new[] { "Number", "Sayı", "Número", "Nombre", "Zahl", "Numero", "Número", "数", "숫자", "数字" }),
                ("A1",  new[] { "Sound", "Ses", "Sonido", "Son", "Schall", "Suono", "Som", "音", "소리", "声音" }),
                ("A2",  new[] { "Energy", "Enerji", "Energía", "Énergie", "Energie", "Energia", "Energia", "エネルギー", "에너지", "能量" }),
                ("A2",  new[] { "Planet", "Gezegen", "Planeta", "Planète", "Planet", "Pianeta", "Planeta", "惑星", "행성", "行星" }),
                ("A2",  new[] { "Experiment", "Deney", "Experimento", "Expérience", "Experiment", "Esperimento", "Experimento", "実験", "실험", "实验" }),
                ("A2",  new[] { "Result", "Sonuç", "Resultado", "Résultat", "Ergebnis", "Risultato", "Resultado", "結果", "결과", "结果" }),
                ("A2",  new[] { "Measure", "Ölçmek", "Medir", "Mesurer", "Messen", "Misurare", "Medir", "測る", "측정하다", "测量" }),
                ("A2",  new[] { "Metal", "Metal", "Metal", "Métal", "Metall", "Metallo", "Metal", "金属", "금속", "金属" }),
                ("A2",  new[] { "Machine", "Makine", "Máquina", "Machine", "Maschine", "Macchina", "Máquina", "機械", "기계", "机器" }),
                ("B1",  new[] { "Research", "Araştırma", "Investigación", "Recherche", "Forschung", "Ricerca", "Pesquisa", "研究", "연구", "研究" }),
                ("B1",  new[] { "Gravity", "Yerçekimi", "Gravedad", "Gravité", "Schwerkraft", "Gravità", "Gravidade", "重力", "중력", "重力" }),
                ("B1",  new[] { "Cell", "Hücre", "Célula", "Cellule", "Zelle", "Cellula", "Célula", "細胞", "세포", "细胞" }),
                ("B1",  new[] { "Theory", "Teori", "Teoría", "Théorie", "Theorie", "Teoria", "Teoria", "理論", "이론", "理论" }),
                ("B1",  new[] { "Discovery", "Keşif", "Descubrimiento", "Découverte", "Entdeckung", "Scoperta", "Descoberta", "発見", "발견", "发现" }),
                ("B1",  new[] { "Evidence", "Kanıt", "Evidencia", "Preuve", "Beweis", "Prova", "Evidência", "証拠", "증거", "证据" }),
                ("B1+", new[] { "Atom", "Atom", "Átomo", "Atome", "Atom", "Atomo", "Átomo", "原子", "원자", "原子" }),
                ("B1+", new[] { "Molecule", "Molekül", "Molécula", "Molécule", "Molekül", "Molecola", "Molécula", "分子", "분자", "分子" }),
                ("B1+", new[] { "Hypothesis", "Hipotez", "Hipótesis", "Hypothèse", "Hypothese", "Ipotesi", "Hipótese", "仮説", "가설", "假设" }),
                ("B1+", new[] { "Laboratory", "Laboratuvar", "Laboratorio", "Laboratoire", "Labor", "Laboratorio", "Laboratório", "実験室", "실험실", "实验室" }),
                ("B2",  new[] { "Analysis", "Analiz", "Análisis", "Analyse", "Analyse", "Analisi", "Análise", "分析", "분석", "分析" }),
                ("B2",  new[] { "Radiation", "Radyasyon", "Radiación", "Radiation", "Strahlung", "Radiazione", "Radiação", "放射線", "방사선", "辐射" }),
                ("B2",  new[] { "Evolution", "Evrim", "Evolución", "Évolution", "Evolution", "Evoluzione", "Evolução", "進化", "진화", "进化" }),
                ("B2",  new[] { "Compound", "Bileşik", "Compuesto", "Composé", "Verbindung", "Composto", "Composto", "化合物", "화합물", "化合物" }),
                ("C1",  new[] { "Catalyst", "Katalizör", "Catalizador", "Catalyseur", "Katalysator", "Catalizzatore", "Catalisador", "触媒", "촉매", "催化剂" }),
                ("C1",  new[] { "Entropy", "Entropi", "Entropía", "Entropie", "Entropie", "Entropia", "Entropia", "エントロピー", "엔트로피", "熵" }),
                ("C1",  new[] { "Isotope", "İzotop", "Isótopo", "Isotope", "Isotop", "Isotopo", "Isótopo", "同位体", "동위원소", "同位素" }),
                ("C2",  new[] { "Quantum", "Kuantum", "Cuántico", "Quantique", "Quantenhaft", "Quantistico", "Quântico", "量子", "양자", "量子" }),
                ("C2",  new[] { "Thermodynamics", "Termodinamik", "Termodinámica", "Thermodynamique", "Thermodynamik", "Termodinamica", "Termodinâmica", "熱力学", "열역학", "热力学" }),
            }),

        new(11, "animals", 15, "🐾", "#A855F7",
            new[] { "Animals", "Hayvanlar", "Animales", "Animaux", "Tiere", "Animali", "Animais", "動物", "동물", "动物" },
            new[] { "Pets, wild animals and their world", "Evcil hayvanlar, yabani hayvanlar ve dünyaları", "Mascotas, animales salvajes y su mundo", "Animaux de compagnie, animaux sauvages et leur monde", "Haustiere, wilde Tiere und ihre Welt", "Animali domestici, animali selvatici e il loro mondo", "Animais de estimação, animais selvagens e seu mundo", "ペット、野生動物、そしてその世界", "반려동물, 야생동물 그리고 그들의 세계", "宠物、野生动物和它们的世界" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Dog", "Köpek", "Perro", "Chien", "Hund", "Cane", "Cachorro", "犬", "개", "狗" }),
                ("A1",  new[] { "Cat", "Kedi", "Gato", "Chat", "Katze", "Gatto", "Gato", "猫", "고양이", "猫" }),
                ("A1",  new[] { "Bird", "Kuş", "Pájaro", "Oiseau", "Vogel", "Uccello", "Pássaro", "鳥", "새", "鸟" }),
                ("A1",  new[] { "Horse", "At", "Caballo", "Cheval", "Pferd", "Cavallo", "Cavalo", "馬", "말", "马" }),
                ("A1",  new[] { "Fish", "Balık", "Pez", "Poisson", "Fisch", "Pesce", "Peixe", "魚", "물고기", "鱼" }),
                ("A1",  new[] { "Cow", "İnek", "Vaca", "Vache", "Kuh", "Mucca", "Vaca", "牛", "소", "牛" }),
                ("A1",  new[] { "Sheep", "Koyun", "Oveja", "Mouton", "Schaf", "Pecora", "Ovelha", "羊", "양", "羊" }),
                ("A1",  new[] { "Rabbit", "Tavşan", "Conejo", "Lapin", "Kaninchen", "Coniglio", "Coelho", "うさぎ", "토끼", "兔子" }),
                ("A1",  new[] { "Mouse", "Fare", "Ratón", "Souris", "Maus", "Topo", "Rato", "ねずみ", "쥐", "老鼠" }),
                ("A1",  new[] { "Chicken", "Tavuk", "Pollo", "Poulet", "Huhn", "Pollo", "Frango", "鶏", "닭", "鸡" }),
                ("A2",  new[] { "Lion", "Aslan", "León", "Lion", "Löwe", "Leone", "Leão", "ライオン", "사자", "狮子" }),
                ("A2",  new[] { "Elephant", "Fil", "Elefante", "Éléphant", "Elefant", "Elefante", "Elefante", "象", "코끼리", "大象" }),
                ("A2",  new[] { "Bear", "Ayı", "Oso", "Ours", "Bär", "Orso", "Urso", "熊", "곰", "熊" }),
                ("A2",  new[] { "Wing", "Kanat", "Ala", "Aile", "Flügel", "Ala", "Asa", "翼", "날개", "翅膀" }),
                ("A2",  new[] { "Tail", "Kuyruk", "Cola", "Queue", "Schwanz", "Coda", "Cauda", "尾", "꼬리", "尾巴" }),
                ("A2",  new[] { "Monkey", "Maymun", "Mono", "Singe", "Affe", "Scimmia", "Macaco", "猿", "원숭이", "猴子" }),
                ("A2",  new[] { "Snake", "Yılan", "Serpiente", "Serpent", "Schlange", "Serpente", "Cobra", "蛇", "뱀", "蛇" }),
                ("B1",  new[] { "Wildlife", "Yaban hayatı", "Fauna salvaje", "Faune sauvage", "Wildtiere", "Fauna selvatica", "Vida selvagem", "野生動物", "야생 동물", "野生动物" }),
                ("B1",  new[] { "Feather", "Tüy", "Pluma", "Plume", "Feder", "Piuma", "Pena", "羽", "깃털", "羽毛" }),
                ("B1",  new[] { "Nest", "Yuva", "Nido", "Nid", "Nest", "Nido", "Ninho", "巣", "둥지", "巢" }),
                ("B1",  new[] { "Insect", "Böcek", "Insecto", "Insecte", "Insekt", "Insetto", "Inseto", "昆虫", "곤충", "昆虫" }),
                ("B1",  new[] { "Reptile", "Sürüngen", "Reptil", "Reptile", "Reptil", "Rettile", "Réptil", "爬虫類", "파충류", "爬行动物" }),
                ("B1",  new[] { "Mammal", "Memeli", "Mamífero", "Mammifère", "Säugetier", "Mammifero", "Mamífero", "哺乳類", "포유류", "哺乳动物" }),
                ("B1+", new[] { "Predator", "Yırtıcı", "Depredador", "Prédateur", "Raubtier", "Predatore", "Predador", "捕食者", "포식자", "捕食者" }),
                ("B1+", new[] { "Prey", "Av", "Presa", "Proie", "Beute", "Preda", "Presa", "獲物", "먹이", "猎物" }),
                ("B1+", new[] { "Herd", "Sürü", "Manada", "Troupeau", "Herde", "Mandria", "Rebanho", "群れ", "무리", "兽群" }),
                ("B1+", new[] { "Endangered", "Nesli tükenmekte", "En peligro", "En voie de disparition", "Bedroht", "In via di estinzione", "Ameaçado", "絶滅危惧の", "멸종 위기", "濒危" }),
                ("B2",  new[] { "Migration", "Göç", "Migración", "Migration", "Wanderung", "Migrazione", "Migração", "渡り", "이동", "迁徙" }),
                ("B2",  new[] { "Camouflage", "Kamuflaj", "Camuflaje", "Camouflage", "Tarnung", "Mimetismo", "Camuflagem", "擬態", "위장", "伪装" }),
                ("B2",  new[] { "Extinction", "Yok oluş", "Extinción", "Extinction", "Aussterben", "Estinzione", "Extinção", "絶滅", "멸종", "灭绝" }),
                ("B2",  new[] { "Domestic", "Evcil", "Doméstico", "Domestique", "Zahm", "Domestico", "Doméstico", "家畜の", "가축의", "家养的" }),
                ("C1",  new[] { "Hibernation", "Kış uykusu", "Hibernación", "Hibernation", "Winterschlaf", "Ibernazione", "Hibernação", "冬眠", "겨울잠", "冬眠" }),
                ("C1",  new[] { "Nocturnal", "Gececil", "Nocturno", "Nocturne", "Nachtaktiv", "Notturno", "Noturno", "夜行性の", "야행성", "夜行性" }),
                ("C1",  new[] { "Habitat loss", "Yaşam alanı kaybı", "Pérdida de hábitat", "Perte d'habitat", "Lebensraumverlust", "Perdita di habitat", "Perda de habitat", "生息地の減少", "서식지 감소", "栖息地丧失" }),
                ("C2",  new[] { "Metamorphosis", "Başkalaşım", "Metamorfosis", "Métamorphose", "Metamorphose", "Metamorfosi", "Metamorfose", "変態", "변태", "变态" }),
                ("C2",  new[] { "Ethology", "Etoloji", "Etología", "Éthologie", "Ethologie", "Etologia", "Etologia", "動物行動学", "동물행동학", "动物行为学" }),
            }),

        // 12-15: Flutter'daki starter_content.dart'tan devralınan dört kategori.
        // İlk on kelimenin çevirileri oradaki listelerden birebir alındı — aynı
        // desteyi zaten çalışmış kullanıcıların kartlarıyla eşleşsin ve
        // tamamlama sırasında "Süt" yanına ikinci bir "Süt" düşmesin diye.
        new(12, "food", 1, "🍎", "#F97316",
            new[] { "Food & Drink", "Yiyecek ve İçecek", "Comida y bebida", "Nourriture et boissons", "Essen & Trinken", "Cibo e bevande", "Comida e bebida", "食べ物と飲み物", "음식과 음료", "食物和饮品" },
            new[] { "What you order, buy and cook", "Sipariş ettiğin, aldığın ve pişirdiğin şeyler", "Lo que pides, compras y cocinas", "Ce que vous commandez, achetez et cuisinez", "Was man bestellt, kauft und kocht", "Quello che ordini, compri e cucini", "O que você pede, compra e cozinha", "注文する、買う、そして作るもの", "주문하고, 사고, 요리하는 것", "你点的、买的和做的食物" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Milk", "Süt", "Leche", "Lait", "Milch", "Latte", "Leite", "牛乳", "우유", "牛奶" }),
                ("A1",  new[] { "Coffee", "Kahve", "Café", "Café", "Kaffee", "Caffè", "Café", "コーヒー", "커피", "咖啡" }),
                ("A1",  new[] { "Tea", "Çay", "Té", "Thé", "Tee", "Tè", "Chá", "お茶", "차", "茶" }),
                ("A1",  new[] { "Apple", "Elma", "Manzana", "Pomme", "Apfel", "Mela", "Maçã", "りんご", "사과", "苹果" }),
                ("A1",  new[] { "Cheese", "Peynir", "Queso", "Fromage", "Käse", "Formaggio", "Queijo", "チーズ", "치즈", "奶酪" }),
                ("A1",  new[] { "Egg", "Yumurta", "Huevo", "Œuf", "Ei", "Uovo", "Ovo", "卵", "계란", "鸡蛋" }),
                ("A1",  new[] { "Fish", "Balık", "Pescado", "Poisson", "Fisch", "Pesce", "Peixe", "魚", "생선", "鱼" }),
                ("A1",  new[] { "Meat", "Et", "Carne", "Viande", "Fleisch", "Carne", "Carne", "肉", "고기", "肉" }),
                ("A1",  new[] { "Rice", "Pirinç", "Arroz", "Riz", "Reis", "Riso", "Arroz", "ご飯", "밥", "米饭" }),
                ("A1",  new[] { "Salt", "Tuz", "Sal", "Sel", "Salz", "Sale", "Sal", "塩", "소금", "盐" }),
                ("A2",  new[] { "Bread", "Ekmek", "Pan", "Pain", "Brot", "Pane", "Pão", "パン", "빵", "面包" }),
                ("A2",  new[] { "Water", "Su", "Agua", "Eau", "Wasser", "Acqua", "Água", "水", "물", "水" }),
                ("A2",  new[] { "Sugar", "Şeker", "Azúcar", "Sucre", "Zucker", "Zucchero", "Açúcar", "砂糖", "설탕", "糖" }),
                ("A2",  new[] { "Vegetable", "Sebze", "Verdura", "Légume", "Gemüse", "Verdura", "Legume", "野菜", "채소", "蔬菜" }),
                ("A2",  new[] { "Fruit", "Meyve", "Fruta", "Fruit", "Obst", "Frutta", "Fruta", "果物", "과일", "水果" }),
                ("A2",  new[] { "Restaurant", "Restoran", "Restaurante", "Restaurant", "Restaurant", "Ristorante", "Restaurante", "レストラン", "식당", "餐厅" }),
                ("A2",  new[] { "Menu", "Menü", "Menú", "Menu", "Speisekarte", "Menù", "Cardápio", "メニュー", "메뉴", "菜单" }),
                ("B1",  new[] { "Recipe", "Tarif", "Receta", "Recette", "Rezept", "Ricetta", "Receita", "レシピ", "조리법", "食谱" }),
                ("B1",  new[] { "Dessert", "Tatlı", "Postre", "Dessert", "Nachtisch", "Dolce", "Sobremesa", "デザート", "디저트", "甜点" }),
                ("B1",  new[] { "Breakfast", "Kahvaltı", "Desayuno", "Petit-déjeuner", "Frühstück", "Colazione", "Café da manhã", "朝食", "아침 식사", "早餐" }),
                ("B1",  new[] { "Flavour", "Lezzet", "Sabor", "Saveur", "Geschmack", "Sapore", "Sabor", "味", "맛", "味道" }),
                ("B1",  new[] { "Ingredient", "Malzeme", "Ingrediente", "Ingrédient", "Zutat", "Ingrediente", "Ingrediente", "材料", "재료", "食材" }),
                ("B1",  new[] { "Waiter", "Garson", "Camarero", "Serveur", "Kellner", "Cameriere", "Garçom", "ウェイター", "웨이터", "服务员" }),
                ("B1+", new[] { "Portion", "Porsiyon", "Ración", "Portion", "Portion", "Porzione", "Porção", "一人前", "1인분", "份量" }),
                ("B1+", new[] { "Vegetarian", "Vejetaryen", "Vegetariano", "Végétarien", "Vegetarisch", "Vegetariano", "Vegetariano", "ベジタリアン", "채식주의자", "素食者" }),
                ("B1+", new[] { "Spice", "Baharat", "Especia", "Épice", "Gewürz", "Spezia", "Tempero", "香辛料", "향신료", "香料" }),
                ("B1+", new[] { "Roast", "Kızartmak", "Asar", "Rôtir", "Braten", "Arrostire", "Assar", "焼く", "굽다", "烤" }),
                ("B2",  new[] { "Cuisine", "Mutfak", "Cocina", "Cuisine", "Küche", "Cucina", "Culinária", "料理", "요리", "菜系" }),
                ("B2",  new[] { "Seasoning", "Çeşni", "Condimento", "Assaisonnement", "Würzung", "Condimento", "Temperagem", "味付け", "양념", "调味" }),
                ("B2",  new[] { "Nutrient", "Besin", "Nutriente", "Nutriment", "Nährstoff", "Nutriente", "Nutriente", "栄養素", "영양소", "营养素" }),
                ("B2",  new[] { "Marinate", "Marine etmek", "Marinar", "Mariner", "Marinieren", "Marinare", "Marinar", "漬け込む", "재우다", "腌制" }),
                ("C1",  new[] { "Fermentation", "Fermantasyon", "Fermentación", "Fermentation", "Gärung", "Fermentazione", "Fermentação", "発酵", "발효", "发酵" }),
                ("C1",  new[] { "Palate", "Damak", "Paladar", "Palais", "Gaumen", "Palato", "Paladar", "味覚", "미각", "味觉" }),
                ("C1",  new[] { "Garnish", "Süsleme", "Guarnición", "Garniture", "Garnitur", "Guarnizione", "Guarnição", "付け合わせ", "고명", "配菜" }),
                ("C2",  new[] { "Umami", "Umami", "Umami", "Umami", "Umami", "Umami", "Umami", "うま味", "감칠맛", "鲜味" }),
                ("C2",  new[] { "Emulsion", "Emülsiyon", "Emulsión", "Émulsion", "Emulsion", "Emulsione", "Emulsão", "乳化", "유화", "乳化液" }),
            }),

        new(13, "travel", 2, "✈️", "#0EA5E9",
            new[] { "Travel & Directions", "Seyahat ve Yön Tarifi", "Viajes y direcciones", "Voyage et itinéraires", "Reisen & Wegbeschreibung", "Viaggi e indicazioni", "Viagem e direções", "旅行と道案内", "여행과 길 안내", "旅行和问路" },
            new[] { "Getting around a city you do not know yet", "Henüz tanımadığın bir şehirde yol bulmak", "Moverte por una ciudad que aún no conoces", "Se déplacer dans une ville qu'on ne connaît pas encore", "Sich in einer fremden Stadt zurechtfinden", "Muoversi in una città che non conosci ancora", "Circular por uma cidade que você ainda não conhece", "まだ知らない街での移動", "아직 모르는 도시에서 길 찾기", "在陌生城市里通行" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Airport", "Havalimanı", "Aeropuerto", "Aéroport", "Flughafen", "Aeroporto", "Aeroporto", "空港", "공항", "机场" }),
                ("A1",  new[] { "Station", "İstasyon", "Estación", "Gare", "Bahnhof", "Stazione", "Estação", "駅", "역", "车站" }),
                ("A1",  new[] { "Ticket", "Bilet", "Billete", "Billet", "Fahrkarte", "Biglietto", "Bilhete", "切符", "표", "票" }),
                ("A1",  new[] { "Hotel", "Otel", "Hotel", "Hôtel", "Hotel", "Hotel", "Hotel", "ホテル", "호텔", "酒店" }),
                ("A1",  new[] { "Map", "Harita", "Mapa", "Carte", "Karte", "Mappa", "Mapa", "地図", "지도", "地图" }),
                ("A1",  new[] { "Left", "Sol", "Izquierda", "Gauche", "Links", "Sinistra", "Esquerda", "左", "왼쪽", "左" }),
                ("A1",  new[] { "Right", "Sağ", "Derecha", "Droite", "Rechts", "Destra", "Direita", "右", "오른쪽", "右" }),
                ("A1",  new[] { "Car", "Araba", "Coche", "Voiture", "Auto", "Auto", "Carro", "車", "자동차", "汽车" }),
                ("A1",  new[] { "Road", "Yol", "Camino", "Route", "Straße", "Strada", "Estrada", "道", "길", "路" }),
                ("A1",  new[] { "Bus", "Otobüs", "Autobús", "Bus", "Bus", "Autobus", "Ônibus", "バス", "버스", "公共汽车" }),
                ("A2",  new[] { "Passport", "Pasaport", "Pasaporte", "Passeport", "Reisepass", "Passaporto", "Passaporte", "パスポート", "여권", "护照" }),
                ("A2",  new[] { "Luggage", "Bavul", "Equipaje", "Bagages", "Gepäck", "Bagaglio", "Bagagem", "荷物", "짐", "行李" }),
                ("A2",  new[] { "Train", "Tren", "Tren", "Train", "Zug", "Treno", "Trem", "電車", "기차", "火车" }),
                ("A2",  new[] { "Flight", "Uçuş", "Vuelo", "Vol", "Flug", "Volo", "Voo", "フライト", "항공편", "航班" }),
                ("A2",  new[] { "Border", "Sınır", "Frontera", "Frontière", "Grenze", "Confine", "Fronteira", "国境", "국경", "边境" }),
                ("A2",  new[] { "Straight on", "Düz", "Recto", "Tout droit", "Geradeaus", "Dritto", "Em frente", "まっすぐ", "직진", "一直走" }),
                ("A2",  new[] { "Address", "Adres", "Dirección", "Adresse", "Adresse", "Indirizzo", "Endereço", "住所", "주소", "地址" }),
                ("B1",  new[] { "Reservation", "Rezervasyon", "Reserva", "Réservation", "Reservierung", "Prenotazione", "Reserva", "予約", "예약", "预订" }),
                ("B1",  new[] { "Departure", "Kalkış", "Salida", "Départ", "Abfahrt", "Partenza", "Partida", "出発", "출발", "出发" }),
                ("B1",  new[] { "Arrival", "Varış", "Llegada", "Arrivée", "Ankunft", "Arrivo", "Chegada", "到着", "도착", "到达" }),
                ("B1",  new[] { "Journey", "Yolculuk", "Viaje", "Trajet", "Reise", "Viaggio", "Viagem", "旅", "여정", "旅程" }),
                ("B1",  new[] { "Currency", "Para birimi", "Moneda", "Devise", "Währung", "Valuta", "Moeda", "通貨", "통화", "货币" }),
                ("B1",  new[] { "Guide", "Rehber", "Guía", "Guide", "Reiseführer", "Guida", "Guia", "ガイド", "가이드", "导游" }),
                ("B1+", new[] { "Itinerary", "Güzergâh", "Itinerario", "Itinéraire", "Reiseroute", "Itinerario", "Itinerário", "旅程表", "여행 일정", "行程" }),
                ("B1+", new[] { "Accommodation", "Konaklama", "Alojamiento", "Hébergement", "Unterkunft", "Alloggio", "Hospedagem", "宿泊", "숙박", "住宿" }),
                ("B1+", new[] { "Customs", "Gümrük", "Aduana", "Douane", "Zoll", "Dogana", "Alfândega", "税関", "세관", "海关" }),
                ("B1+", new[] { "Delay", "Rötar", "Retraso", "Retard", "Verspätung", "Ritardo", "Atraso", "遅延", "지연", "延误" }),
                ("B2",  new[] { "Visa", "Vize", "Visado", "Visa", "Visum", "Visto", "Visto", "ビザ", "비자", "签证" }),
                ("B2",  new[] { "Destination", "Varış yeri", "Destino", "Destination", "Reiseziel", "Destinazione", "Destino", "目的地", "목적지", "目的地" }),
                ("B2",  new[] { "Insurance", "Sigorta", "Seguro", "Assurance", "Versicherung", "Assicurazione", "Seguro", "保険", "보험", "保险" }),
                ("B2",  new[] { "Transit", "Aktarma", "Tránsito", "Transit", "Transit", "Transito", "Trânsito", "乗り継ぎ", "환승", "中转" }),
                ("C1",  new[] { "Excursion", "Gezi", "Excursión", "Excursion", "Ausflug", "Escursione", "Excursão", "遠足", "소풍", "短途旅行" }),
                ("C1",  new[] { "Jet lag", "Saat farkı yorgunluğu", "Desfase horario", "Décalage horaire", "Jetlag", "Fuso orario", "Jet lag", "時差ぼけ", "시차증", "时差反应" }),
                ("C1",  new[] { "Landmark", "Simge yapı", "Punto de referencia", "Point de repère", "Wahrzeichen", "Punto di riferimento", "Ponto de referência", "名所", "랜드마크", "地标" }),
                ("C2",  new[] { "Wanderlust", "Gezme tutkusu", "Pasión por viajar", "Envie d'ailleurs", "Fernweh", "Voglia di viaggiare", "Vontade de viajar", "旅への憧れ", "방랑벽", "旅行癖" }),
                ("C2",  new[] { "Expatriate", "Gurbetçi", "Expatriado", "Expatrié", "Auswanderer", "Espatriato", "Expatriado", "駐在員", "국외 거주자", "侨居者" }),
            }),

        new(14, "business", 3, "💼", "#6366F1",
            new[] { "Business Basics", "İş Hayatı Temelleri", "Fundamentos de negocios", "Bases du monde professionnel", "Business-Grundlagen", "Basi del business", "Fundamentos de negócios", "ビジネスの基本", "비즈니스 기초", "商务基础" },
            new[] { "The office, meetings, and getting work done", "Ofis, toplantılar ve günlük iş", "La oficina, las reuniones y el trabajo diario", "Le bureau, les réunions et le travail au quotidien", "Büro, Besprechungen und die tägliche Arbeit", "Ufficio, riunioni e lavoro quotidiano", "O escritório, as reuniões e o trabalho do dia a dia", "オフィス、会議、日々の仕事", "사무실, 회의 그리고 업무", "办公室、会议和日常工作" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Office", "Ofis", "Oficina", "Bureau", "Büro", "Ufficio", "Escritório", "オフィス", "사무실", "办公室" }),
                ("A1",  new[] { "Meeting", "Toplantı", "Reunión", "Réunion", "Besprechung", "Riunione", "Reunião", "会議", "회의", "会议" }),
                ("A1",  new[] { "Email", "E-posta", "Correo electrónico", "E-mail", "E-Mail", "Email", "E-mail", "メール", "이메일", "电子邮件" }),
                ("A1",  new[] { "Boss", "Patron", "Jefe", "Patron", "Chef", "Capo", "Chefe", "上司", "상사", "老板" }),
                ("A1",  new[] { "Company", "Şirket", "Empresa", "Entreprise", "Firma", "Azienda", "Empresa", "会社", "회사", "公司" }),
                ("A1",  new[] { "Desk", "Masa", "Escritorio", "Bureau", "Schreibtisch", "Scrivania", "Mesa", "机", "책상", "桌子" }),
                ("A1",  new[] { "Team", "Ekip", "Equipo", "Équipe", "Team", "Squadra", "Equipe", "チーム", "팀", "团队" }),
                ("A1",  new[] { "Job", "İş", "Empleo", "Emploi", "Stelle", "Impiego", "Emprego", "職", "직업", "职位" }),
                ("A1",  new[] { "Manager", "Yönetici", "Gerente", "Directeur", "Manager", "Direttore", "Gerente", "マネージャー", "매니저", "经理" }),
                ("A1",  new[] { "Client", "Müşteri", "Cliente", "Client", "Kunde", "Cliente", "Cliente", "クライアント", "고객", "客户" }),
                ("A2",  new[] { "Colleague", "Meslektaş", "Colega", "Collègue", "Kollege", "Collega", "Colega", "同僚", "동료", "同事" }),
                ("A2",  new[] { "Salary", "Maaş", "Salario", "Salaire", "Gehalt", "Stipendio", "Salário", "給料", "급여", "工资" }),
                ("A2",  new[] { "Report", "Rapor", "Informe", "Rapport", "Bericht", "Rapporto", "Relatório", "報告書", "보고서", "报告" }),
                ("A2",  new[] { "Schedule", "Program", "Horario", "Emploi du temps", "Zeitplan", "Orario", "Agenda", "予定", "일정", "日程" }),
                ("A2",  new[] { "Project", "Proje", "Proyecto", "Projet", "Projekt", "Progetto", "Projeto", "プロジェクト", "프로젝트", "项目" }),
                ("A2",  new[] { "Interview", "Mülakat", "Entrevista", "Entretien", "Vorstellungsgespräch", "Colloquio", "Entrevista", "面接", "면접", "面试" }),
                ("A2",  new[] { "Document", "Belge", "Documento", "Document", "Dokument", "Documento", "Documento", "書類", "서류", "文件" }),
                ("B1",  new[] { "Contract", "Sözleşme", "Contrato", "Contrat", "Vertrag", "Contratto", "Contrato", "契約", "계약", "合同" }),
                ("B1",  new[] { "Deadline", "Son tarih", "Fecha límite", "Date limite", "Frist", "Scadenza", "Prazo", "締め切り", "마감일", "截止日期" }),
                ("B1",  new[] { "Presentation", "Sunum", "Presentación", "Présentation", "Präsentation", "Presentazione", "Apresentação", "プレゼン", "발표", "演示" }),
                ("B1",  new[] { "Budget", "Bütçe", "Presupuesto", "Budget", "Budget", "Bilancio", "Orçamento", "予算", "예산", "预算" }),
                ("B1",  new[] { "Department", "Departman", "Departamento", "Service", "Abteilung", "Reparto", "Departamento", "部署", "부서", "部门" }),
                ("B1",  new[] { "Profit", "Kâr", "Beneficio", "Bénéfice", "Gewinn", "Profitto", "Lucro", "利益", "이익", "利润" }),
                ("B1+", new[] { "Negotiation", "Müzakere", "Negociación", "Négociation", "Verhandlung", "Trattativa", "Negociação", "交渉", "협상", "谈判" }),
                ("B1+", new[] { "Promotion", "Terfi", "Ascenso", "Promotion", "Beförderung", "Promozione", "Promoção", "昇進", "승진", "晋升" }),
                ("B1+", new[] { "Supplier", "Tedarikçi", "Proveedor", "Fournisseur", "Lieferant", "Fornitore", "Fornecedor", "仕入先", "공급업체", "供应商" }),
                ("B1+", new[] { "Revenue", "Gelir", "Ingresos", "Chiffre d'affaires", "Umsatz", "Ricavo", "Receita", "収益", "매출", "收入" }),
                ("B2",  new[] { "Stakeholder", "Paydaş", "Parte interesada", "Partie prenante", "Interessengruppe", "Portatore d'interesse", "Parte interessada", "利害関係者", "이해관계자", "利益相关者" }),
                ("B2",  new[] { "Compliance", "Uyum", "Cumplimiento", "Conformité", "Regeltreue", "Conformità", "Conformidade", "法令遵守", "준법", "合规" }),
                ("B2",  new[] { "Merger", "Birleşme", "Fusión", "Fusion", "Fusion", "Fusione", "Fusão", "合併", "합병", "合并" }),
                ("B2",  new[] { "Outsourcing", "Dış kaynak kullanımı", "Externalización", "Externalisation", "Auslagerung", "Esternalizzazione", "Terceirização", "外部委託", "외주", "外包" }),
                ("C1",  new[] { "Liquidity", "Likidite", "Liquidez", "Liquidité", "Liquidität", "Liquidità", "Liquidez", "流動性", "유동성", "流动性" }),
                ("C1",  new[] { "Leverage", "Kaldıraç", "Apalancamiento", "Effet de levier", "Hebelwirkung", "Leva finanziaria", "Alavancagem", "レバレッジ", "레버리지", "杠杆" }),
                ("C1",  new[] { "Due diligence", "Durum tespiti", "Diligencia debida", "Diligence raisonnable", "Sorgfaltsprüfung", "Dovuta diligenza", "Auditoria prévia", "デューデリジェンス", "실사", "尽职调查" }),
                ("C2",  new[] { "Amortisation", "İtfa", "Amortización", "Amortissement", "Tilgung", "Ammortamento", "Amortização", "償却", "상각", "摊销" }),
                ("C2",  new[] { "Fiduciary", "Mütevelli", "Fiduciario", "Fiduciaire", "Treuhänderisch", "Fiduciario", "Fiduciário", "受託者の", "수탁의", "受托的" }),
            }),

        new(15, "family", 12, "👨‍👩‍👧", "#14B8A6",
            new[] { "Family & People", "Aile ve İnsanlar", "Familia y personas", "Famille et personnes", "Familie & Menschen", "Famiglia e persone", "Família e pessoas", "家族と人々", "가족과 사람들", "家人与他人" },
            new[] { "The people around you", "Etrafındaki insanlar", "Las personas que te rodean", "Les gens qui vous entourent", "Die Menschen um dich herum", "Le persone intorno a te", "As pessoas à sua volta", "あなたのまわりの人たち", "당신 주변의 사람들", "你身边的人" },
            new (string, string[])[]
            {
                ("A1",  new[] { "Mother", "Anne", "Madre", "Mère", "Mutter", "Madre", "Mãe", "母", "어머니", "母亲" }),
                ("A1",  new[] { "Father", "Baba", "Padre", "Père", "Vater", "Padre", "Pai", "父", "아버지", "父亲" }),
                ("A1",  new[] { "Sister", "Kız kardeş", "Hermana", "Sœur", "Schwester", "Sorella", "Irmã", "姉妹", "자매", "姐妹" }),
                ("A1",  new[] { "Brother", "Erkek kardeş", "Hermano", "Frère", "Bruder", "Fratello", "Irmão", "兄弟", "형제", "兄弟" }),
                ("A1",  new[] { "Child", "Çocuk", "Niño", "Enfant", "Kind", "Bambino", "Criança", "子供", "아이", "孩子" }),
                ("A1",  new[] { "Family", "Aile", "Familia", "Famille", "Familie", "Famiglia", "Família", "家族", "가족", "家庭" }),
                ("A1",  new[] { "Grandmother", "Büyükanne", "Abuela", "Grand-mère", "Großmutter", "Nonna", "Avó", "祖母", "할머니", "祖母" }),
                ("A1",  new[] { "Grandfather", "Büyükbaba", "Abuelo", "Grand-père", "Großvater", "Nonno", "Avô", "祖父", "할아버지", "祖父" }),
                ("A1",  new[] { "Man", "Adam", "Hombre", "Homme", "Mann", "Uomo", "Homem", "男", "남자", "男人" }),
                ("A1",  new[] { "Woman", "Kadın", "Mujer", "Femme", "Frau", "Donna", "Mulher", "女", "여자", "女人" }),
                ("A2",  new[] { "Friend", "Arkadaş", "Amigo", "Ami", "Freund", "Amico", "Amigo", "友達", "친구", "朋友" }),
                ("A2",  new[] { "Baby", "Bebek", "Bebé", "Bébé", "Baby", "Neonato", "Bebê", "赤ちゃん", "아기", "婴儿" }),
                ("A2",  new[] { "Son", "Oğul", "Hijo", "Fils", "Sohn", "Figlio", "Filho", "息子", "아들", "儿子" }),
                ("A2",  new[] { "Daughter", "Kız evlat", "Hija", "Fille", "Tochter", "Figlia", "Filha", "娘", "딸", "女儿" }),
                ("A2",  new[] { "Aunt", "Teyze", "Tía", "Tante", "Tante", "Zia", "Tia", "おば", "이모", "姑姑" }),
                ("A2",  new[] { "Uncle", "Amca", "Tío", "Oncle", "Onkel", "Zio", "Tio", "おじ", "삼촌", "叔叔" }),
                ("A2",  new[] { "Neighbour", "Komşu", "Vecino", "Voisin", "Nachbar", "Vicino", "Vizinho", "隣人", "이웃", "邻居" }),
                ("B1",  new[] { "Cousin", "Kuzen", "Primo", "Cousin", "Cousin", "Cugino", "Primo", "いとこ", "사촌", "表亲" }),
                ("B1",  new[] { "Marriage", "Evlilik", "Matrimonio", "Mariage", "Ehe", "Matrimonio", "Casamento", "結婚", "결혼", "婚姻" }),
                ("B1",  new[] { "Relative", "Akraba", "Pariente", "Parent", "Verwandter", "Parente", "Parente", "親戚", "친척", "亲戚" }),
                ("B1",  new[] { "Couple", "Çift", "Pareja", "Couple", "Paar", "Coppia", "Casal", "夫婦", "부부", "夫妻" }),
                ("B1",  new[] { "Twin", "İkiz", "Gemelo", "Jumeau", "Zwilling", "Gemello", "Gêmeo", "双子", "쌍둥이", "双胞胎" }),
                ("B1",  new[] { "Generation", "Kuşak", "Generación", "Génération", "Generation", "Generazione", "Geração", "世代", "세대", "一代" }),
                ("B1+", new[] { "Household", "Hane", "Hogar", "Foyer", "Haushalt", "Nucleo familiare", "Domicílio", "世帯", "가구", "家户" }),
                ("B1+", new[] { "Nephew", "Erkek yeğen", "Sobrino", "Neveu", "Neffe", "Nipote", "Sobrinho", "甥", "조카", "侄子" }),
                ("B1+", new[] { "Mother-in-law", "Kayınvalide", "Suegra", "Belle-mère", "Schwiegermutter", "Suocera", "Sogra", "義母", "시어머니", "婆婆" }),
                ("B1+", new[] { "Upbringing", "Yetiştirilme", "Crianza", "Éducation", "Erziehung", "Educazione", "Criação", "育ち", "양육", "教养" }),
                ("B2",  new[] { "Ancestor", "Ata", "Antepasado", "Ancêtre", "Vorfahre", "Antenato", "Ancestral", "先祖", "조상", "祖先" }),
                ("B2",  new[] { "Sibling", "Kardeş", "Hermano o hermana", "Frère ou sœur", "Geschwister", "Fratello o sorella", "Irmão ou irmã", "兄弟姉妹", "형제자매", "兄弟姐妹" }),
                ("B2",  new[] { "Guardian", "Vasi", "Tutor legal", "Tuteur", "Vormund", "Tutore", "Tutor legal", "保護者", "보호자", "监护人" }),
                ("B2",  new[] { "Inheritance", "Miras", "Herencia", "Héritage", "Erbe", "Eredità", "Herança", "相続", "상속", "遗产" }),
                ("C1",  new[] { "Kinship", "Akrabalık", "Parentesco", "Parenté", "Verwandtschaft", "Parentela", "Parentesco", "親族関係", "친족 관계", "亲属关系" }),
                ("C1",  new[] { "Descendant", "Soydan gelen", "Descendiente", "Descendant", "Nachkomme", "Discendente", "Descendente", "子孫", "후손", "后代" }),
                ("C1",  new[] { "Estrangement", "Küslük", "Distanciamiento", "Éloignement", "Entfremdung", "Allontanamento", "Afastamento", "疎遠", "소원", "疏远" }),
                ("C2",  new[] { "Lineage", "Soy", "Linaje", "Lignée", "Abstammung", "Lignaggio", "Linhagem", "血筋", "혈통", "血统" }),
                ("C2",  new[] { "Matriarch", "Aile büyüğü kadın", "Matriarca", "Matriarche", "Matriarchin", "Matriarca", "Matriarca", "女家長", "여가장", "女族长" }),
            }),
    };

    internal static void Apply(ModelBuilder modelBuilder)
    {
        ConfigureRelations(modelBuilder);
        SeedTemplates(modelBuilder);
    }

    private static void ConfigureRelations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeckTemplate>(e =>
        {
            // Slug istemcinin desteyi tanıdığı ad; iki şablonun aynı slug'ı
            // taşıması, kullanıcıda çakışan StarterKey üretirdi.
            e.HasIndex(t => t.Slug).IsUnique();

            // "Bu kategorinin şablonu var mı" sorgusu senkronizasyonun sıcak
            // yolunda, kullanıcının seçtiği her kategori için çalışır.
            e.HasIndex(t => t.CategoryId);

            e.HasOne(t => t.Category)
                .WithMany()
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeckTemplateLabel>(e =>
        {
            e.HasKey(l => new { l.DeckTemplateId, l.LanguageCode });

            e.HasOne(l => l.DeckTemplate)
                .WithMany(t => t.Labels)
                .HasForeignKey(l => l.DeckTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            // Dil satırı Languages tablosuna bağlanmaz: katalog dilleri
            // (IsActive=false olabilir) ile içerik dilleri ayrı yaşamalı,
            // yoksa bir dili listeden kaldırmak şablon metnini de silerdi.
        });

        modelBuilder.Entity<DeckTemplateWord>(e =>
        {
            e.HasIndex(w => new { w.DeckTemplateId, w.Ordinal }).IsUnique();

            // Deste kurarken tek sorgu "bu şablonun şu seviyeye kadarki
            // kelimeleri" biçiminde geliyor.
            e.HasIndex(w => new { w.DeckTemplateId, w.CefrLevel });

            e.ToTable(t => t.HasCheckConstraint(
                "CK_DeckTemplateWord_CefrLevel",
                "[CefrLevel] IN ('A1', 'A2', 'B1', 'B1+', 'B2', 'C1', 'C2')"));

            e.HasOne(w => w.DeckTemplate)
                .WithMany(t => t.Words)
                .HasForeignKey(w => w.DeckTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeckTemplateWordText>(e =>
        {
            e.HasKey(t => new { t.DeckTemplateWordId, t.LanguageCode });

            e.HasOne(t => t.DeckTemplateWord)
                .WithMany(w => w.Texts)
                .HasForeignKey(t => t.DeckTemplateWordId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void SeedTemplates(ModelBuilder modelBuilder)
    {
        var templates = new List<DeckTemplate>();
        var labels = new List<DeckTemplateLabel>();
        var words = new List<DeckTemplateWord>();
        var texts = new List<DeckTemplateWordText>();

        foreach (var spec in Specs)
        {
            templates.Add(new DeckTemplate
            {
                Id = spec.Id,
                Slug = spec.Slug,
                CategoryId = spec.CategoryId,
                Emoji = spec.Emoji,
                ColorHex = spec.ColorHex,
                SortOrder = spec.Id,
            });

            for (var i = 0; i < LanguageOrder.Length; i++)
            {
                labels.Add(new DeckTemplateLabel
                {
                    DeckTemplateId = spec.Id,
                    LanguageCode = LanguageOrder[i],
                    Title = spec.Titles[i],
                    Description = spec.Descriptions[i],
                });
            }

            for (var w = 0; w < spec.Words.Length; w++)
            {
                // Kelime kimliği şablondan türetilir (şablon 3'ün 4. kelimesi
                // 304) — böylece yeni bir şablon eklemek mevcut kimlikleri
                // kaydırmaz ve migration yalnızca eklenen satırları içerir.
                var wordId = spec.Id * 100 + (w + 1);
                var (level, textsForWord) = spec.Words[w];

                words.Add(new DeckTemplateWord
                {
                    Id = wordId,
                    DeckTemplateId = spec.Id,
                    Ordinal = w + 1,
                    CefrLevel = level,
                });

                for (var i = 0; i < LanguageOrder.Length; i++)
                {
                    texts.Add(new DeckTemplateWordText
                    {
                        DeckTemplateWordId = wordId,
                        LanguageCode = LanguageOrder[i],
                        Text = textsForWord[i],
                    });
                }
            }
        }

        modelBuilder.Entity<DeckTemplate>().HasData(templates);
        modelBuilder.Entity<DeckTemplateLabel>().HasData(labels);
        modelBuilder.Entity<DeckTemplateWord>().HasData(words);
        modelBuilder.Entity<DeckTemplateWordText>().HasData(texts);
    }
}
