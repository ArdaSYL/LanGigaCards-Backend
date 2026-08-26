using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabGrid.Migrations
{
    /// <inheritdoc />
    public partial class AddNativeLanguageCodeToUserLanguageProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            // Backfill: every existing profile row predates NativeLanguageCode
            // being part of its identity, so there's no historical value to
            // recover -- the owning user's *current* native language code is
            // the best available approximation, same approach already used
            // for Deck.LanguageCode's own backfill. Safe against the unique
            // index about to be created below: the old (UserId, LanguageCode)
            // index already guaranteed at most one row per user+target, so no
            // two rows for the same user can collide by picking up the same
            // native code here.
            migrationBuilder.Sql(@"
                UPDATE ulp
                SET ulp.NativeLanguageCode = u.NativeLanguageCode
                FROM UserLanguageProfiles ulp
                JOIN Users u ON u.Id = ulp.UserId;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_UserId_NativeLanguageCode_LanguageCode",
                table: "UserLanguageProfiles",
                columns: new[] { "UserId", "NativeLanguageCode", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLanguageProfiles_UserId_NativeLanguageCode_LanguageCode",
                table: "UserLanguageProfiles");

            migrationBuilder.DropColumn(
                name: "NativeLanguageCode",
                table: "UserLanguageProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_UserId_LanguageCode",
                table: "UserLanguageProfiles",
                columns: new[] { "UserId", "LanguageCode" },
                unique: true);
        }
    }
}
