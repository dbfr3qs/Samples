using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServerAspNetIdentityPasskeys.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "BLOB", nullable: false),
                    DPoPPublicKeyJwk = table.Column<string>(type: "TEXT", nullable: false),
                    PublicKeyThumbprint = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshTokenHandle = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RefreshCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceBindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBindings_PublicKeyThumbprint",
                table: "DeviceBindings",
                column: "PublicKeyThumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBindings_RefreshTokenHandle",
                table: "DeviceBindings",
                column: "RefreshTokenHandle");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBindings_UserId",
                table: "DeviceBindings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceBindings");
        }
    }
}
