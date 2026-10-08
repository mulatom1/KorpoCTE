using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App01.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoursesFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Flags",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Criteria = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flags_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "Courses",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserFlags",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    FlagId = table.Column<int>(type: "int", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFlags_Flags_FlagId",
                        column: x => x.FlagId,
                        principalSchema: "Courses",
                        principalTable: "Flags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFlags_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Portal",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Flags_Code",
                schema: "Courses",
                table: "Flags",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Flags_CourseId",
                schema: "Courses",
                table: "Flags",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFlags_FlagId",
                schema: "Courses",
                table: "UserFlags",
                column: "FlagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFlags_UserId_FlagId",
                schema: "Courses",
                table: "UserFlags",
                columns: new[] { "UserId", "FlagId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFlags",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "Flags",
                schema: "Courses");
        }
    }
}
