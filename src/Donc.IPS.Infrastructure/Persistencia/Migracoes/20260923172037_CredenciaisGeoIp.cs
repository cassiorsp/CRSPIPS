using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Donc.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CredenciaisGeoIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracaoGeoIp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ContaId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ChaveProtegida = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FinalChave = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    AtualizacaoAutomatica = table.Column<bool>(type: "INTEGER", nullable: false),
                    AtualizacaoSolicitada = table.Column<bool>(type: "INTEGER", nullable: false),
                    UltimaVerificacaoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimaAtualizacaoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimoResultadoSucesso = table.Column<bool>(type: "INTEGER", nullable: false),
                    UltimoResultado = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracaoGeoIp", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracaoGeoIp");
        }
    }
}
