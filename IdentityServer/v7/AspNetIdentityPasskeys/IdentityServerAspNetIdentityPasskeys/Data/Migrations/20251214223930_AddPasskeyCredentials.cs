using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityServerAspNetIdentityPasskeys.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasskeyCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SignatureCounter = table.Column<uint>(type: "INTEGER", nullable: false),
                    CredType = table.Column<string>(type: "TEXT", nullable: false),
                    AaGuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttestationFormat = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasskeyCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyCredentials_CredentialId",
                table: "PasskeyCredentials",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyCredentials_UserId",
                table: "PasskeyCredentials",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasskeyCredentials");
        }
    }
}
