using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Donc.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class ListasExternas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListasExternas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Urls = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Formato = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervaloHoras = table.Column<int>(type: "INTEGER", nullable: false),
                    LimiteEntradas = table.Column<int>(type: "INTEGER", nullable: false),
                    Modo = table.Column<int>(type: "INTEGER", nullable: false),
                    AtualizacaoSolicitada = table.Column<bool>(type: "INTEGER", nullable: false),
                    UltimaVerificacaoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimaAtualizacaoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quantidade = table.Column<int>(type: "INTEGER", nullable: false),
                    Removidas = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimoErro = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListasExternas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoincidenciasListasExternas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListaExternaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Quantidade = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimeiraEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaFonte = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimoSite = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UltimaUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UltimoCodigoStatus = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoincidenciasListasExternas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoincidenciasListasExternas_ListasExternas_ListaExternaId",
                        column: x => x.ListaExternaId,
                        principalTable: "ListasExternas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntradasListasExternas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListaExternaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Faixa = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InicioChave = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FimChave = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasListasExternas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntradasListasExternas_ListasExternas_ListaExternaId",
                        column: x => x.ListaExternaId,
                        principalTable: "ListasExternas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoincidenciasListasExternas_ListaExternaId_Ip",
                table: "CoincidenciasListasExternas",
                columns: new[] { "ListaExternaId", "Ip" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoincidenciasListasExternas_UltimaEm",
                table: "CoincidenciasListasExternas",
                column: "UltimaEm");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasListasExternas_ListaExternaId_InicioChave",
                table: "EntradasListasExternas",
                columns: new[] { "ListaExternaId", "InicioChave" });

            migrationBuilder.CreateIndex(
                name: "IX_ListasExternas_Nome",
                table: "ListasExternas",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoincidenciasListasExternas");

            migrationBuilder.DropTable(
                name: "EntradasListasExternas");

            migrationBuilder.DropTable(
                name: "ListasExternas");
        }
    }
}
