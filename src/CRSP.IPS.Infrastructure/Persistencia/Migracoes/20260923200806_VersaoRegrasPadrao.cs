using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRSP.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class VersaoRegrasPadrao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VersaoRegrasPadrao",
                table: "Configuracao",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1); // Bancos existentes ja possuem as regras da versao 1 do catalogo.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VersaoRegrasPadrao",
                table: "Configuracao");
        }
    }
}
