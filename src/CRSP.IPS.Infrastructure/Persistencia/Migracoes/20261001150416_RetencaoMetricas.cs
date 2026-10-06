using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRSP.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class RetencaoMetricas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetencaoMetricasEndpointsDias",
                table: "Configuracao",
                type: "INTEGER",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<int>(
                name: "RetencaoMetricasProcessosDias",
                table: "Configuracao",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetencaoMetricasEndpointsDias",
                table: "Configuracao");

            migrationBuilder.DropColumn(
                name: "RetencaoMetricasProcessosDias",
                table: "Configuracao");
        }
    }
}
