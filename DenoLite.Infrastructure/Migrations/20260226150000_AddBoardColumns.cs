using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenoLite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardColumns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_ProjectId",
                table: "BoardColumns",
                column: "ProjectId");

            migrationBuilder.AddColumn<Guid>(
                name: "BoardColumnId",
                table: "Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_BoardColumnId",
                table: "Tasks",
                column: "BoardColumnId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_BoardColumns_BoardColumnId",
                table: "Tasks",
                column: "BoardColumnId",
                principalTable: "BoardColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Seed: create 3 default columns per project (Todo, In Progress, Done)
            migrationBuilder.Sql(@"
                INSERT INTO ""BoardColumns"" (""Id"", ""ProjectId"", ""Name"", ""SortOrder"", ""CreatedAt"", ""CreatedBy"")
                SELECT gen_random_uuid(), ""Id"", 'Todo', 0, NOW() AT TIME ZONE 'UTC', ""OwnerId"" FROM ""Projects"";
                INSERT INTO ""BoardColumns"" (""Id"", ""ProjectId"", ""Name"", ""SortOrder"", ""CreatedAt"", ""CreatedBy"")
                SELECT gen_random_uuid(), ""Id"", 'In Progress', 1, NOW() AT TIME ZONE 'UTC', ""OwnerId"" FROM ""Projects"";
                INSERT INTO ""BoardColumns"" (""Id"", ""ProjectId"", ""Name"", ""SortOrder"", ""CreatedAt"", ""CreatedBy"")
                SELECT gen_random_uuid(), ""Id"", 'Done', 2, NOW() AT TIME ZONE 'UTC', ""OwnerId"" FROM ""Projects"";
            ");

            // Map existing tasks to column by Status (0=Todo, 1=InProgress, 2=Done)
            migrationBuilder.Sql(@"
                UPDATE ""Tasks"" t
                SET ""BoardColumnId"" = (
                    SELECT bc.""Id"" FROM ""BoardColumns"" bc
                    WHERE bc.""ProjectId"" = t.""ProjectId"" AND bc.""SortOrder"" = t.""Status""
                    LIMIT 1
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_BoardColumns_BoardColumnId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_BoardColumnId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "BoardColumnId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "BoardColumns");
        }
    }
}
