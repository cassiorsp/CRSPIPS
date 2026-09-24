using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRSP.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class SiteDoEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Site",
                table: "Eventos",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Site",
                table: "Eventos");
        }
    }
}
