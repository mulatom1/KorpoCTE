using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App01.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Lotto");

            migrationBuilder.EnsureSchema(
                name: "Portal");

            migrationBuilder.CreateTable(
                name: "DrawTypes",
                schema: "Lotto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    TicketPrize = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UserNumbersCountMin = table.Column<int>(type: "int", nullable: false),
                    UserNumbersCountMax = table.Column<int>(type: "int", nullable: false),
                    NumbersCount = table.Column<int>(type: "int", nullable: false),
                    NumbersMaxValue = table.Column<int>(type: "int", nullable: false),
                    SpecialsCount = table.Column<int>(type: "int", nullable: false),
                    SpecialsMaxValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrawTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mails",
                schema: "Portal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Topic = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "Portal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Draws",
                schema: "Lotto",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrawSystemId = table.Column<long>(type: "bigint", nullable: false),
                    DrawDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DrawTypeId = table.Column<int>(type: "int", nullable: false),
                    NumbersLow = table.Column<long>(type: "bigint", nullable: false),
                    NumbersHigh = table.Column<long>(type: "bigint", nullable: false),
                    SpecialsLow = table.Column<long>(type: "bigint", nullable: false),
                    SpecialsHigh = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Draws", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Draws_DrawTypes_DrawTypeId",
                        column: x => x.DrawTypeId,
                        principalSchema: "Lotto",
                        principalTable: "DrawTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrawTypeWinTiers",
                schema: "Lotto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrawTypeId = table.Column<int>(type: "int", nullable: false),
                    WinTier = table.Column<int>(type: "int", nullable: false),
                    NumbersMatchCount = table.Column<int>(type: "int", nullable: false),
                    NumbersMatchSelected = table.Column<int>(type: "int", nullable: false),
                    SpecialsMatchCount = table.Column<int>(type: "int", nullable: false),
                    SpecialsMatchSelected = table.Column<int>(type: "int", nullable: false),
                    PotentialWinPrize = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrawTypeWinTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrawTypeWinTiers_DrawTypes_DrawTypeId",
                        column: x => x.DrawTypeId,
                        principalSchema: "Lotto",
                        principalTable: "DrawTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "Lotto",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DrawTypeId = table.Column<int>(type: "int", nullable: false),
                    GroupName = table.Column<string>(type: "varchar(100)", nullable: true),
                    NumbersLow = table.Column<long>(type: "bigint", nullable: false),
                    NumbersHigh = table.Column<long>(type: "bigint", nullable: false),
                    SpecialsLow = table.Column<long>(type: "bigint", nullable: false),
                    SpecialsHigh = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_DrawTypes_DrawTypeId",
                        column: x => x.DrawTypeId,
                        principalSchema: "Lotto",
                        principalTable: "DrawTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Portal",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Draws_DrawDate",
                schema: "Lotto",
                table: "Draws",
                column: "DrawDate");

            migrationBuilder.CreateIndex(
                name: "IX_Draws_DrawSystemId",
                schema: "Lotto",
                table: "Draws",
                column: "DrawSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Draws_DrawTypeId_DrawDate",
                schema: "Lotto",
                table: "Draws",
                columns: new[] { "DrawTypeId", "DrawDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Draws_DrawTypeId_DrawSystemId_DrawDate",
                schema: "Lotto",
                table: "Draws",
                columns: new[] { "DrawTypeId", "DrawSystemId", "DrawDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrawTypeWinTiers_DrawTypeId",
                schema: "Lotto",
                table: "DrawTypeWinTiers",
                column: "DrawTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedAt",
                schema: "Lotto",
                table: "Tickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DrawTypeId",
                schema: "Lotto",
                table: "Tickets",
                column: "DrawTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UserId",
                schema: "Lotto",
                table: "Tickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "Portal",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Draws",
                schema: "Lotto");

            migrationBuilder.DropTable(
                name: "DrawTypeWinTiers",
                schema: "Lotto");

            migrationBuilder.DropTable(
                name: "Mails",
                schema: "Portal");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "Lotto");

            migrationBuilder.DropTable(
                name: "DrawTypes",
                schema: "Lotto");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "Portal");
        }
    }
}
