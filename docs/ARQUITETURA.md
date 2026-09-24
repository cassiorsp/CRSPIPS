# CRSPIPS — Arquitetura e desenvolvimento

Documento técnico para quem vai manter ou estender o CRSPIPS. Para instalar e usar, veja o [README](../README.md).

## Projetos

```
CRSP.IPS.slnx (.NET 10)
src/
  CRSP.IPS.Domain          Entidades, EnderecoIp, FaixaIp (IP/CIDR/intervalo), políticas. Sem dependências externas.
  CRSP.IPS.Application     Casos de uso (Servicos/) e motor (Motor/): detecção, sincronização do firewall,
                           política de países, listas externas, GeoIP.
  CRSP.IPS.Infrastructure  EF Core + SQLite (WAL), Windows Firewall (COM HNetCfg.FwPolicy2), leitores W3C/HTTPERR/Event Log,
                           MaxMind GeoLite2, download de listas externas, DPAPI.
  CRSP.IPS.Web             Painel Razor Pages + Bootstrap 5 + Chart.js. Cookie Auth. pt-BR e inglês.
  CRSP.IPS.Worker          Serviço do Windows "CRSPIPS" (executável CRSPIPS.Worker.exe).
tests/
  CRSP.IPS.Tests           xUnit: unidade, integração (SQLite real, firewall falso) e renderização das telas.
```

Dependências entre projetos: `Web`/`Worker` → `Infrastructure` → `Application` → `Domain`.

## Regra central: o painel nunca mexe no firewall

O site no IIS roda sem privilégio de administrador. Ele só grava no banco o **estado desejado**
(bloqueios, listas, política de países). O Worker, que roda como LocalSystem, compara esse estado com o
Windows Firewall a cada 2 segundos e corrige as diferenças. A cada 5 minutos confere tudo, mesmo sem mudanças,
e desfaz alterações manuais feitas no firewall.

`IServicoFirewall` só é registrado no Worker (`AdicionarInfraestruturaMotor`), então o painel não tem como chamá-lo.

## Motor

```
Fontes (IFonteEventos)           ServicoDeteccao                          ServicoSincronizacaoFirewall
  LeitorLogIis    u_ex*.log   →    proteção (lista branca, servidor,   →    Bloqueios:        CRSPIPS_Bloqueio_NNN
  LeitorHttpErr   httperr*.log     admins)                                   Listas externas:  CRSPIPS_ListaExterna_NNN
  LeitorEventosWindows             política de países                       expiração
    4625 Security                  regras (janela deslizante em memória)  ServicoPoliticaPaisesFirewall
    140  RdpCoreTS                 punição progressiva                       CRSPIPS_Pais_* (portas TCP/UDP)
    18456 SQL Server               coincidências com listas externas
```

- **Leitura incremental**: a posição de cada arquivo (bytes) e canal do Event Log (EventRecordID) fica em `PosicoesLeitura`.
  Arquivos que já existiam na primeira execução são lidos a partir do fim, para não bloquear pelo histórico.
- **Site da requisição**: o log padrão do IIS não grava o hostname. O site vem de `cs-host` (se habilitado) ou do ID
  (pasta `W3SVCn`, campo `s-siteid`) traduzido pelo `applicationHost.config`.
- **Regras** no modelo *jail*: fonte + critério (código HTTP, regex de URL, motivo HTTPERR, ID de evento) + limite em uma janela.
  As regex usam `RegexOptions.NonBacktracking` (tempo linear, sem timeout); padrões não suportados caem no motor tradicional.
- **Contagem em memória** (`ContadorJanelaDeslizante`): o banco recebe só amostras (até 20 por IP, regra e hora) e decisões.
- **Punição progressiva** (`PoliticaProgressao`): cada reincidência dentro da janela sobe um nível na lista de tempos.
- **Modo simulação**: bloqueios automáticos são registrados com `Simulado = true` e não vão para o firewall.
- **Catálogos versionados**: `CatalogoRegrasPadrao` (versão em `Configuracao.VersaoRegrasPadrao`) e `CatalogoListasExternas`
  (inserção por nome). Novas regras e listas entram sozinhas em bancos existentes, e as excluídas não voltam.

## Trabalhadores do Worker

| Trabalhador | Intervalo | Função |
|---|---|---|
| `InicializacaoBanco` | uma vez | Migrations e dados iniciais, depois de o serviço responder ao Windows (evita o erro 1053) |
| `TrabalhadorDeteccao` | 5 s | Lê as fontes e processa os eventos |
| `TrabalhadorFirewall` | 2 s | Sincroniza o firewall, aplica a política de países, retenção (1 h) |
| `TrabalhadorGeoIp` | 15 s | Download das bases MaxMind e preenchimento de país retroativo (10 min) |
| `TrabalhadorListasExternas` | 1 min | Download das listas externas vencidas |

## Banco de dados

SQLite em `C:\ProgramData\CRSPIPS\crspips.db`, compartilhado entre painel e Worker.
`InterceptorSqlite` liga `journal_mode=WAL` e `busy_timeout` em cada conexão. Todas as datas são UTC.

Nova migration:

```powershell
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project src/CRSP.IPS.Infrastructure --startup-project src/CRSP.IPS.Infrastructure --output-dir Persistencia/Migracoes
```

As migrations são aplicadas automaticamente por quem iniciar primeiro (painel ou serviço).

## Segurança

- Senhas com `PasswordHasher` (PBKDF2). 5 falhas seguidas travam a conta por 15 minutos, e o login tem limite por IP.
- O primeiro administrador só pode ser criado a partir de `127.0.0.1`.
- A chave MaxMind fica no banco protegida com DPAPI (escopo da máquina).
- As chaves do cookie de login ficam em `C:\ProgramData\CRSPIPS\chaves`, protegidas com DPAPI.
- Cabeçalhos: CSP sem script inline, `X-Frame-Options: DENY`, `nosniff`, `no-referrer`.

## Traduções

A chave é o próprio texto em pt-BR. `src/CRSP.IPS.Web/Recursos/Textos.en.resx` contém o inglês.
Os nomes de países vêm de `Recursos/paises.json` (gerado a partir do ICU).

## Testes

```powershell
dotnet test CRSP.IPS.slnx
```

Um dos testes lê (sem alterar) o Windows Firewall real para validar a interface COM.
`PaginasWebTestes` renderiza todas as telas em pt-BR e inglês e salva amostras em `%TEMP%\crspips-amostras`.
