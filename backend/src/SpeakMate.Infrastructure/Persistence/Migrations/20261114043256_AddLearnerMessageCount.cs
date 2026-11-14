using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnerMessageCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LearnerMessageCount",
                schema: "app",
                table: "PracticeSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill existing sessions from their stored learner turns.
            migrationBuilder.Sql("""
                UPDATE s
                SET s.LearnerMessageCount = t.LearnerTurns
                FROM app.PracticeSessions AS s
                CROSS APPLY (SELECT COUNT(*) AS LearnerTurns
                             FROM app.ConversationTurns AS ct
                             WHERE ct.SessionId = s.Id AND ct.Speaker = 'Learner') AS t;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LearnerMessageCount",
                schema: "app",
                table: "PracticeSessions");
        }
    }
}
