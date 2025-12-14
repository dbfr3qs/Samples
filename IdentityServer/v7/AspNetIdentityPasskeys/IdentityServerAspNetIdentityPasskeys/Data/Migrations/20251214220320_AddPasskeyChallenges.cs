using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServerAspNetIdentityPasskeys.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasskeyChallenges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Challenge = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    ClientType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Used = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasskeyChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyChallenges_ExpiresAt",
                table: "PasskeyChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyChallenges_UserId",
                table: "PasskeyChallenges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasskeyChallenges");
        }
    }
}
