using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRSP.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class UrlsIgnoradasRegra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UrlsIgnoradas",
                table: "Regras",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UrlsIgnoradas",
                table: "Regras");
        }
    }
}
