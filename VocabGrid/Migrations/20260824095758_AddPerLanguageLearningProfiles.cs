using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabGrid.Migrations
{
    /// <inheritdoc />
    public partial class AddPerLanguageLearningProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserCategories",
                table: "UserCategories");

            migrationBuilder.DropIndex(
                name: "IX_Decks_UserId_CreatedAt",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_DailyStudySummaries_UserId_Day",
                table: "DailyStudySummaries");

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "UserCategories",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "StudyActivities",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "Decks",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "DailyStudySummaries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            // Var olan satırların dili: kategori destelerinde anahtarın sonundaki
            // kod ("category_music_de" → "de"), diğer her şeyde sahibinin o
            // günkü hedef dili. Boş bırakılsalardı kitaplık ve istatistik dil
            // süzgeci uygulandığı anda boşalırdı.
            migrationBuilder.Sql(@"
UPDATE d
SET    LanguageCode = LOWER(RIGHT(d.StarterKey, CHARINDEX('_', REVERSE(d.StarterKey)) - 1))
FROM   Decks AS d
WHERE  d.LanguageCode IS NULL
       AND d.StarterKey LIKE 'category!_%' ESCAPE '!'
       AND CHARINDEX('_', REVERSE(d.StarterKey)) BETWEEN 2 AND 9;");

            migrationBuilder.Sql(@"
UPDATE d
SET    LanguageCode = LOWER(LTRIM(RTRIM(u.TargetLanguageCode)))
FROM   Decks AS d
       INNER JOIN Users AS u ON u.Id = d.UserId
WHERE  d.LanguageCode IS NULL
       AND LTRIM(RTRIM(ISNULL(u.TargetLanguageCode, ''))) <> '';");

            // Aktivitenin dili önce destesinden okunuyor: deste başka bir dil
            // çalışılırken kurulmuş olabilir ve o çalışma o dile aittir.
            migrationBuilder.Sql(@"
UPDATE a
SET    LanguageCode = d.LanguageCode
FROM   StudyActivities AS a
       INNER JOIN Decks AS d ON d.Id = a.DeckId
WHERE  a.LanguageCode IS NULL
       AND d.LanguageCode IS NOT NULL;");

            migrationBuilder.Sql(@"
UPDATE a
SET    LanguageCode = LOWER(LTRIM(RTRIM(u.TargetLanguageCode)))
FROM   StudyActivities AS a
       INNER JOIN Users AS u ON u.Id = a.UserId
WHERE  a.LanguageCode IS NULL
       AND LTRIM(RTRIM(ISNULL(u.TargetLanguageCode, ''))) <> '';");

            // Günlük özet geriye dönük bölünemez — o günün hangi kısmı hangi
            // dildeydi bilgisi özet satırında yok. Tamamı kullanıcının o günkü
            // hedef diline yazılıyor; ham aktiviteler yerinde durduğu için
            // gerekirse yeniden üretilebilir.
            migrationBuilder.Sql(@"
UPDATE s
SET    LanguageCode = LOWER(LTRIM(RTRIM(u.TargetLanguageCode)))
FROM   DailyStudySummaries AS s
       INNER JOIN Users AS u ON u.Id = s.UserId
WHERE  s.LanguageCode = ''
       AND LTRIM(RTRIM(ISNULL(u.TargetLanguageCode, ''))) <> '';");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserCategories",
                table: "UserCategories",
                columns: new[] { "UserId", "CategoryId", "LanguageCode" });

            // Kategori seçimleri de sahibinin o günkü hedef diline taşınıyor.
            // Anahtarın parçası olan bir sütunu güncellemek sorun değil:
            // (UserId, CategoryId) zaten benzersizdi, dil eklenince de öyle
            // kalıyor.
            migrationBuilder.Sql(@"
UPDATE uc
SET    LanguageCode = LOWER(LTRIM(RTRIM(u.TargetLanguageCode)))
FROM   UserCategories AS uc
       INNER JOIN Users AS u ON u.Id = uc.UserId
WHERE  uc.LanguageCode = ''
       AND LTRIM(RTRIM(ISNULL(u.TargetLanguageCode, ''))) <> '';");

            migrationBuilder.CreateTable(
                name: "UserLanguageProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    LanguageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProficiencyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DifficultyMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSetupCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentStreak = table.Column<int>(type: "int", nullable: false),
                    LongestStreak = table.Column<int>(type: "int", nullable: false),
                    TotalXp = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    LastStudiedDeckId = table.Column<int>(type: "int", nullable: true),
                    LastStudiedWordId = table.Column<int>(type: "int", nullable: true),
                    LastStudiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLanguageProfiles", x => x.Id);
                    table.CheckConstraint("CK_UserLanguageProfile_Counters", "[TotalXp] >= 0 AND [CurrentStreak] >= 0 AND [LongestStreak] >= 0");
                    table.CheckConstraint("CK_UserLanguageProfile_Level", "[Level] >= 1");
                    table.CheckConstraint("CK_UserLanguageProfile_ProficiencyLevel", "[ProficiencyLevel] IN ('Just Starting', 'Beginner', 'Intermediate', 'Advanced')");
                    table.ForeignKey(
                        name: "FK_UserLanguageProfiles_Decks_LastStudiedDeckId",
                        column: x => x.LastStudiedDeckId,
                        principalTable: "Decks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserLanguageProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLanguageProfiles_Vocabularies_LastStudiedWordId",
                        column: x => x.LastStudiedWordId,
                        principalTable: "Vocabularies",
                        principalColumn: "WordID");
                });

            // Mevcut kullanıcıların şu an çalıştıkları dil için profil satırı.
            // Kurulumu tamamlanmış sayılıyorlar: onboarding'i çoktan geçtiler
            // ve uygulamaya döndüklerinde seviye ölçümü penceresiyle
            // karşılaşmaları anlamsız olurdu. Sayaçlar hesap satırından
            // devralınıyor — o güne kadarki tüm çalışma zaten bu tek dile ait.
            //
            // CEFR tavanı ayarlardan alınıyor, ama oradaki değer CEFR olmak
            // zorunda değil ("Adaptive" gibi eski değerler var); o durumda
            // yeterlilik seviyesinden türetiliyor.
            migrationBuilder.Sql(@"
INSERT INTO UserLanguageProfiles
       (UserId, LanguageCode, LanguageName, ProficiencyLevel, DifficultyMode,
        IsSetupCompleted, CurrentStreak, LongestStreak, TotalXp, Level, CreatedAt)
SELECT u.Id,
       LOWER(LTRIM(RTRIM(u.TargetLanguageCode))),
       ISNULL(u.TargetLanguage, ''),
       u.TargetProficiencyLevel,
       CASE
           WHEN s.DifficultyMode IN ('A1', 'A2', 'B1', 'B1+', 'B2', 'C1', 'C2') THEN s.DifficultyMode
           WHEN u.TargetProficiencyLevel = 'Just Starting' THEN 'A1'
           WHEN u.TargetProficiencyLevel = 'Beginner' THEN 'A2'
           WHEN u.TargetProficiencyLevel = 'Intermediate' THEN 'B2'
           WHEN u.TargetProficiencyLevel = 'Advanced' THEN 'C2'
           ELSE 'B1'
       END,
       1,
       u.CurrentStreak,
       u.LongestStreak,
       u.TotalXp,
       u.Level,
       SYSUTCDATETIME()
FROM   Users AS u
       LEFT JOIN UserSettings AS s ON s.UserId = u.Id
WHERE  LTRIM(RTRIM(ISNULL(u.TargetLanguageCode, ''))) <> '';");

            migrationBuilder.CreateIndex(
                name: "IX_StudyActivities_UserId_LanguageCode_OccurredAt",
                table: "StudyActivities",
                columns: new[] { "UserId", "LanguageCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Decks_UserId_LanguageCode_CreatedAt",
                table: "Decks",
                columns: new[] { "UserId", "LanguageCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudySummaries_UserId_LanguageCode_Day",
                table: "DailyStudySummaries",
                columns: new[] { "UserId", "LanguageCode", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_LastStudiedDeckId",
                table: "UserLanguageProfiles",
                column: "LastStudiedDeckId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_LastStudiedWordId",
                table: "UserLanguageProfiles",
                column: "LastStudiedWordId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_UserId_LanguageCode",
                table: "UserLanguageProfiles",
                columns: new[] { "UserId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLanguageProfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserCategories",
                table: "UserCategories");

            migrationBuilder.DropIndex(
                name: "IX_StudyActivities_UserId_LanguageCode_OccurredAt",
                table: "StudyActivities");

            migrationBuilder.DropIndex(
                name: "IX_Decks_UserId_LanguageCode_CreatedAt",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_DailyStudySummaries_UserId_LanguageCode_Day",
                table: "DailyStudySummaries");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "UserCategories");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "StudyActivities");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "DailyStudySummaries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserCategories",
                table: "UserCategories",
                columns: new[] { "UserId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Decks_UserId_CreatedAt",
                table: "Decks",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudySummaries_UserId_Day",
                table: "DailyStudySummaries",
                columns: new[] { "UserId", "Day" },
                unique: true);
        }
    }
}
