using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabGrid.Migrations
{
    /// <inheritdoc />
    public partial class AllowFluentProficiencyLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_TargetProficiencyLevel",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserLanguageProfile_ProficiencyLevel",
                table: "UserLanguageProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_TargetProficiencyLevel",
                table: "Users",
                sql: "[TargetProficiencyLevel] IN ('Just Starting', 'Beginner', 'Intermediate', 'Advanced', 'Fluent')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserLanguageProfile_ProficiencyLevel",
                table: "UserLanguageProfiles",
                sql: "[ProficiencyLevel] IN ('Just Starting', 'Beginner', 'Intermediate', 'Advanced', 'Fluent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_TargetProficiencyLevel",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserLanguageProfile_ProficiencyLevel",
                table: "UserLanguageProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_TargetProficiencyLevel",
                table: "Users",
                sql: "[TargetProficiencyLevel] IN ('Just Starting', 'Beginner', 'Intermediate', 'Advanced')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserLanguageProfile_ProficiencyLevel",
                table: "UserLanguageProfiles",
                sql: "[ProficiencyLevel] IN ('Just Starting', 'Beginner', 'Intermediate', 'Advanced')");
        }
    }
}
