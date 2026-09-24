using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Donc.IPS.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OcorridoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IpOrigem = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    Acao = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Alvo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Detalhe = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuracao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ModoSimulacao = table.Column<bool>(type: "INTEGER", nullable: false),
                    TemposBloqueio = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    JanelaReincidenciaDias = table.Column<int>(type: "INTEGER", nullable: false),
                    MonitorarLogIis = table.Column<bool>(type: "INTEGER", nullable: false),
                    CaminhoLogIis = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MonitorarHttpErr = table.Column<bool>(type: "INTEGER", nullable: false),
                    CaminhoHttpErr = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MonitorarEventosWindows = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetencaoEventosDias = table.Column<int>(type: "INTEGER", nullable: false),
                    ModoPaises = table.Column<int>(type: "INTEGER", nullable: false),
                    AplicacaoPaises = table.Column<int>(type: "INTEGER", nullable: false),
                    PaisesPolitica = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PortasPolitica = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadaPor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Eventos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Fonte = table.Column<int>(type: "INTEGER", nullable: false),
                    RegraId = table.Column<int>(type: "INTEGER", nullable: true),
                    OcorridoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metodo = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CodigoStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Detalhe = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    PaisCodigo = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eventos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Listas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Faixa = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InicioChave = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FimChave = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Sistema = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CriadaPor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PosicoesLeitura",
                columns: table => new
                {
                    Chave = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Posicao = table.Column<long>(type: "INTEGER", nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosicoesLeitura", x => x.Chave);
                });

            migrationBuilder.CreateTable(
                name: "Regras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Fonte = table.Column<int>(type: "INTEGER", nullable: false),
                    Criterio = table.Column<int>(type: "INTEGER", nullable: false),
                    Padrao = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    LimiteOcorrencias = table.Column<int>(type: "INTEGER", nullable: false),
                    JanelaSegundos = table.Column<int>(type: "INTEGER", nullable: false),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegrasFirewallDesativadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    DesativadaEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasFirewallDesativadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusWorker",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Maquina = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Versao = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IniciadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoSinalEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaSincronizacaoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RegrasNoFirewall = table.Column<int>(type: "INTEGER", nullable: false),
                    EnderecosNoFirewall = table.Column<int>(type: "INTEGER", nullable: false),
                    EventosProcessados = table.Column<long>(type: "INTEGER", nullable: false),
                    UltimoErro = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    UltimoErroEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResumoPoliticaPaises = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusWorker", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HashSenha = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAcessoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimoIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    TentativasFalhas = table.Column<int>(type: "INTEGER", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bloqueios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    IpChave = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Origem = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Simulado = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegraId = table.Column<int>(type: "INTEGER", nullable: true),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Ocorrencias = table.Column<int>(type: "INTEGER", nullable: false),
                    NivelReincidencia = table.Column<int>(type: "INTEGER", nullable: false),
                    BloqueadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AplicadoNoFirewallEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EncerradoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EncerradoPor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PaisCodigo = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    PaisNome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Cidade = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Asn = table.Column<int>(type: "INTEGER", nullable: true),
                    Organizacao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CriadoPor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Comentario = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bloqueios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bloqueios_Regras_RegraId",
                        column: x => x.RegraId,
                        principalTable: "Regras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_OcorridoEm",
                table: "Auditoria",
                column: "OcorridoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueios_BloqueadoEm",
                table: "Bloqueios",
                column: "BloqueadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueios_Ip",
                table: "Bloqueios",
                column: "Ip");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueios_IpChave",
                table: "Bloqueios",
                column: "IpChave");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueios_RegraId",
                table: "Bloqueios",
                column: "RegraId");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueios_Status_Simulado",
                table: "Bloqueios",
                columns: new[] { "Status", "Simulado" });

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_Ip",
                table: "Eventos",
                column: "Ip");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_OcorridoEm",
                table: "Eventos",
                column: "OcorridoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Listas_Tipo_Faixa",
                table: "Listas",
                columns: new[] { "Tipo", "Faixa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regras_Nome",
                table: "Regras",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegrasFirewallDesativadas_Nome",
                table: "RegrasFirewallDesativadas",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Bloqueios");

            migrationBuilder.DropTable(
                name: "Configuracao");

            migrationBuilder.DropTable(
                name: "Eventos");

            migrationBuilder.DropTable(
                name: "Listas");

            migrationBuilder.DropTable(
                name: "PosicoesLeitura");

            migrationBuilder.DropTable(
                name: "RegrasFirewallDesativadas");

            migrationBuilder.DropTable(
                name: "StatusWorker");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Regras");
        }
    }
}
