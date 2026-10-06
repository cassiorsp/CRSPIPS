using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRSP.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class MetricasIis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetricasEndpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HoraUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Site = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Metodo = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    Sucesso = table.Column<int>(type: "INTEGER", nullable: false),
                    Redirecionamento = table.Column<int>(type: "INTEGER", nullable: false),
                    ErroCliente = table.Column<int>(type: "INTEGER", nullable: false),
                    ErroServidor = table.Column<int>(type: "INTEGER", nullable: false),
                    TempoTotalMs = table.Column<long>(type: "INTEGER", nullable: false),
                    TempoMaximoMs = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricasEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricasProcessosIis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InicioUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Pool = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Amostras = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoriaMinima = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoriaMaxima = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoriaSoma = table.Column<long>(type: "INTEGER", nullable: false),
                    CpuMaxima = table.Column<double>(type: "REAL", nullable: false),
                    CpuSoma = table.Column<double>(type: "REAL", nullable: false),
                    ProcessosMaximo = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricasProcessosIis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SitesIis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Pool = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitesIis", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetricasEndpoints_HoraUtc_Site_Metodo_Endpoint",
                table: "MetricasEndpoints",
                columns: new[] { "HoraUtc", "Site", "Metodo", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricasEndpoints_Site_HoraUtc",
                table: "MetricasEndpoints",
                columns: new[] { "Site", "HoraUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricasProcessosIis_InicioUtc",
                table: "MetricasProcessosIis",
                column: "InicioUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MetricasProcessosIis_Pool_InicioUtc",
                table: "MetricasProcessosIis",
                columns: new[] { "Pool", "InicioUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SitesIis_Nome",
                table: "SitesIis",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetricasEndpoints");

            migrationBuilder.DropTable(
                name: "MetricasProcessosIis");

            migrationBuilder.DropTable(
                name: "SitesIis");
        }
    }
}
