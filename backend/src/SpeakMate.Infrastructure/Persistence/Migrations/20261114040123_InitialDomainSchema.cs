using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeakMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDomainSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearnerProfiles",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SupportLanguage = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    DailyTargetMinutes = table.Column<int>(type: "int", nullable: false),
                    OnboardingCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Goals = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSessions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SaveTranscript = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TranscriptCleared = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationTurns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Speaker = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    InputMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SpeechConfidence = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationTurns_PracticeSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "PracticeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionFeedback",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RepeatTask = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WasHelpful = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsefulPhrases = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Corrections = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionFeedback_PracticeSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "PracticeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserId_Type",
                schema: "app",
                table: "ConsentRecords",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTurns_SessionId_Sequence",
                schema: "app",
                table: "ConversationTurns",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerProfiles_UserId",
                schema: "app",
                table: "LearnerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_UserId_Active",
                schema: "app",
                table: "PracticeSessions",
                columns: new[] { "UserId", "Status" },
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_UserId_StartedAt",
                schema: "app",
                table: "PracticeSessions",
                columns: new[] { "UserId", "StartedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SessionFeedback_SessionId",
                schema: "app",
                table: "SessionFeedback",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_SessionId",
                schema: "app",
                table: "UsageEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_UserId_CreatedAt",
                schema: "app",
                table: "UsageEvents",
                columns: new[] { "UserId", "CreatedAt" })
                .Annotation("SqlServer:Include", new[] { "InputTokens", "OutputTokens" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ConversationTurns",
                schema: "app");

            migrationBuilder.DropTable(
                name: "LearnerProfiles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "SessionFeedback",
                schema: "app");

            migrationBuilder.DropTable(
                name: "UsageEvents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "PracticeSessions",
                schema: "app");
        }
    }
}
