using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabGrid.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNativeLanguageCodeFromUserLanguageProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLanguageProfiles_UserId_NativeLanguageCode_LanguageCode",
                table: "UserLanguageProfiles");

            // Dedup before dropping NativeLanguageCode: a user could have
            // more than one row for the same (UserId, LanguageCode) now that
            // native language is no longer part of the key (e.g. German
            // studied once while the account's native language was English,
            // and again after switching native language to Turkish). Keep
            // the row with the most progress -- highest TotalXp, ties broken
            // by IsSetupCompleted then by the most recently created row --
            // and delete the rest, so the new unique index below doesn't hit
            // a duplicate-key error on data this migration itself would
            // otherwise orphan.
            migrationBuilder.Sql(@"
                DELETE FROM UserLanguageProfiles
                WHERE Id NOT IN (
                    SELECT Id FROM (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY UserId, LanguageCode
                                   ORDER BY TotalXp DESC, IsSetupCompleted DESC, CreatedAt DESC, Id DESC
                               ) AS rn
                        FROM UserLanguageProfiles
                    ) AS ranked
                    WHERE ranked.rn = 1
                );
            ");

            migrationBuilder.DropColumn(
                name: "NativeLanguageCode",
                table: "UserLanguageProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_UserId_LanguageCode",
                table: "UserLanguageProfiles",
                columns: new[] { "UserId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLanguageProfiles_UserId_LanguageCode",
                table: "UserLanguageProfiles");

            migrationBuilder.AddColumn<string>(
                name: "NativeLanguageCode",
                table: "UserLanguageProfiles",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_UserId_NativeLanguageCode_LanguageCode",
                table: "UserLanguageProfiles",
                columns: new[] { "UserId", "NativeLanguageCode", "LanguageCode" },
                unique: true);
        }
    }
}
