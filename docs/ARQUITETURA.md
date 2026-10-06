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
  CRSP.IPS.Web             Painel Blazor (Interactive Server) + Bootstrap 5. Cookie Auth. pt-BR e inglês.
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

## Painel (Blazor)

- **Renderização**: as telas usam *Interactive Server* (SignalR/WebSocket), com prerender. Login, primeiro acesso e erro
  (`Componentes/Paginas/Conta`) são renderizados no servidor sem interatividade (`[ExcludeFromInteractiveRouting]`),
  porque precisam do `HttpContext` para gravar o cookie. Sair é um POST com token antiforgery para `/Conta/Sair`.
- **Um escopo por operação** (`ExecutorServicos`): no Blazor Server o escopo do circuito dura a sessão inteira;
  cada chamada aos serviços abre um escopo novo, com seu próprio `DbContext`, e recebe a identidade do usuário.
- **Usuário no circuito** (`ContextoUsuario` + `CapturaUsuarioCircuito`): nome e IP vêm da conexão SignalR, pois o
  `HttpContext` não é confiável dentro do circuito. A auditoria e a proteção do próprio IP usam esses dados.
- **Mensagens** (`Avisos`): retorno das ações no topo da tela; `SucessoAposNavegar` sobrevive a uma troca de página.
- **Tabelas sem paginação**: Bloqueios e Auditoria usam `<Virtualize>` com `ItemsProvider` (rolagem infinita, `Fatia<T>` com
  `Pular`/`Quantidade`). Em telas a partir de 1200 px de largura, as telas de tabela usam `.pagina-cheia`/`.cartao-cheio`:
  o cartão vai até o fim da janela e só a tabela rola. Em telas menores a página rola e a tabela tem altura fixa.
- **Gráficos** com Chart.js (`GraficoLinha`, `GraficoRosca`, `GraficoBarras`, `GraficoRequisicoes`, `GraficoMemoria` → `wwwroot/js/graficos.js`, todos sobre `GraficoBase`). O componente
  só chama o JavaScript quando a lista de dados muda; ao trocar o tema, os gráficos são redesenhados com as cores novas.
- **Bibliotecas visuais**: Bootstrap 5 local (`wwwroot/lib`); pelo jsDelivr, Chart.js 4, Bootstrap Icons, flag-icons
  (componente `Bandeira`) e a fonte Inter. Sem internet no navegador, o painel funciona, mas sem ícones, bandeiras e
  gráficos. jQuery não é usado: o Blazor controla o DOM e a validação dos formulários é feita pelo `EditForm`.
- **Dashboard**: `ServicoPainel.ObterIndicadoresAsync(PeriodoDashboard)`. As séries são contadas por hora UTC direto no
  SQLite (`ContagemPorHora`, `substr(data, 1, 13)`) e reagrupadas por hora, dia ou semana no fuso do servidor.
- **Colunas ordenáveis**: o componente `CabecalhoOrdenavel` (clique alterna crescente/decrescente) e `OrdenacaoMetricas` (filtro e ordem de
  Endpoints, sites e pools do IIS) são compartilhados entre as telas e a exportação CSV, então a tela e o arquivo sempre saem iguais.
  Valores ausentes (site sem amostra do pool) ficam sempre no fim.
- **Monitor**: filtros por fonte, país, status e IP (`FiltroEventos.Ip`, prefixo com `StartsWith`). Cada mudança de filtro incrementa
  uma versão; a consulta que terminar depois de o filtro mudar é descartada, para não misturar resultados ao digitar o IP.
- **CSV das listas**: `ArquivoCsvListas` lê o arquivo (`;` ou `,`, aspas, comentários) e `ServicoListas.ImportarAsync`
  valida cada linha como na inclusão manual e grava tudo com um único registro de auditoria.

## Motor

```
Fontes (IFonteEventos)           ServicoDeteccao                          ServicoSincronizacaoFirewall
  LeitorLogIis    u_ex*.log   →    proteção (lista branca, servidor,   →    CRSPIPS_LOGIIS_00001
  LeitorHttpErr   httperr*.log     admins)                                   CRSPIPS_HTTPERR_00001
  LeitorEventosWindows             política de países                       CRSPIPS_EVENTOWINDOWS_00001
    4625 Security                  listas externas em modo Reativa          CRSPIPS_MANUAL_00001
    140  RdpCoreTS                 regras (janela deslizante em memória)    CRSPIPS_LISTANEGRA_00001
    18456 SQL Server               punição progressiva                      CRSPIPS_LISTAEXTERNA_00001 (modo Ativa)
                                   coincidências com listas externas        expiração
                                                                          ServicoPoliticaPaisesFirewall
                                                                            CRSPIPS_PAIS_{BLOQUEAR|PERMITIR}_{TCP|UDP|TODOS}_00001
```

- **Regras do firewall por origem** (`ConjuntoRegrasFirewall`, `FirewallWindows.Identificar`): cada bloqueio vai para o
  conjunto da fonte da regra que o gerou (`Bloqueio.RegraId` → `RegraDeteccao.Fonte`); sem regra, `MANUAL`. Bloqueios por
  país e por lista externa reativa guardam a regra do evento suspeito, então caem em LOGIIS/HTTPERR/EVENTOWINDOWS.
  Até 1.000 endereços por regra, número com 5 dígitos. Na primeira verificação completa, `RemoverRegrasObsoletas` apaga
  as regras com nomes antigos (`CRSPIPS_Bloqueio_`, `CRSPIPS_ListaExterna_`, `CRSPIPS_Pais_`, comparação exata de
  maiúsculas) e o estado em memória é zerado para todos os conjuntos serem recriados.
- **Modos das listas externas** (`ModoListaExterna`): Desativada, Avaliação (só coincidências), **Reativa** (não vai ao
  firewall; `MapaListasExternas.ObterListaReativa` + `ServicoDeteccao` bloqueiam o IP no primeiro evento que casar com
  qualquer regra ativa, com `OrigemBloqueio.ListaExterna` e motivo `Lista externa: <nome>`) e Ativa (conjunto
  `CRSPIPS_LISTAEXTERNA_*`, fora do modo simulação).

- **Leitura incremental**: a posição de cada arquivo (bytes) e canal do Event Log (EventRecordID) fica em `PosicoesLeitura`.
  Arquivos que já existiam na primeira execução são lidos a partir do fim, para não bloquear pelo histórico.
- **Site da requisição**: o log padrão do IIS não grava o hostname. O site vem de `cs-host` (se habilitado) ou do ID
  (pasta `W3SVCn`, campo `s-siteid`) traduzido pelo `applicationHost.config`.
- **Regras** no modelo *jail*: fonte + critério (código HTTP, regex de URL, motivo HTTPERR, ID de evento) + limite em uma janela.
  As regex usam `RegexOptions.NonBacktracking` (tempo linear, sem timeout); padrões não suportados caem no motor tradicional.
  Regras web podem ter `UrlsIgnoradas` (regex testada contra a URL e contra "MÉTODO URL"): requisições que casam não
  contam, ex.: `^POST /api/configuracao/empresa` para uma API que responde 404 como resposta de negócio.
- **Contagem em memória** (`ContadorJanelaDeslizante`): o banco recebe só amostras (até 20 por IP, regra e hora) e decisões.
- **Punição progressiva** (`PoliticaProgressao`): cada reincidência dentro da janela sobe um nível na lista de tempos.
- **Modo simulação**: bloqueios automáticos são registrados com `Simulado = true` e não vão para o firewall.
- **Catálogos versionados**: `CatalogoRegrasPadrao` (versão em `Configuracao.VersaoRegrasPadrao`) e `CatalogoListasExternas`
  (inserção por nome). Novas regras e listas entram sozinhas em bancos existentes, e as regras excluídas não voltam.
  Listas externas do catálogo não podem ser excluídas (só desativadas); as cadastradas pelo administrador têm
  `Personalizada = true` e podem ser excluídas (entradas e coincidências saem em cascata).
  O catálogo só insere regras que ainda não existem pelo nome: alterar o padrão de uma regra já entregue vale para instalações novas,
  e quem já tem a regra precisa editá-la na tela **Regras**.

## Trabalhadores do Worker

| Trabalhador | Intervalo | Função |
|---|---|---|
| `InicializacaoBanco` | uma vez | Migrations e dados iniciais, depois de o serviço responder ao Windows (evita o erro 1053) |
| `TrabalhadorDeteccao` | 5 s | Lê as fontes e processa os eventos |
| `TrabalhadorFirewall` | 2 s | Sincroniza o firewall, aplica a política de países; a cada 1 h aplica a retenção (eventos e métricas) |
| `TrabalhadorGeoIp` | 15 s | Download das bases MaxMind e preenchimento de país retroativo (10 min) |
| `TrabalhadorListasExternas` | 1 min | Download das listas externas vencidas |
| `TrabalhadorMetricasIis` | 30 s | Memória e CPU dos processos `w3wp` por application pool, e o mapa site → pool |

## Monitoramento do IIS (telas Endpoints e IIS)

Além de detectar ataques, o Worker mede o que acontece no IIS. Só agregados vão para o banco, nunca uma linha por requisição.

- **Requisições**: o `TrabalhadorDeteccao` já lê todo o log do IIS. Depois de processar o ciclo, `ServicoMetricasIis.RegistrarRequisicoesAsync`
  soma cada requisição em `MetricasEndpoints` (uma linha por hora UTC, site, método e endpoint). Status 1xx-3xx contam como sem erro,
  4xx como erro do cliente e 5xx como erro do servidor; `time-taken` alimenta o tempo médio e máximo. Roda em escopo próprio, depois de salvar
  as posições de leitura: uma falha perde contagens em vez de duplicá-las.
- **Endpoint** (`NormalizadorEndpoint`): sem query string, em minúsculas, com números, GUIDs e tokens trocados por `{id}`
  (`/api/os/123` → `/api/os/{id}`); arquivos estáticos viram `(arquivos estáticos)`. Máximo de 500 endpoints distintos por site e hora;
  o excedente cai em `(outros)`.
- **Memória e CPU**: `AmostradorProcessosIis` lê `w3wp.exe` por WMI (o pool vem do argumento `-ap`) e a memória privada/CPU do processo.
  As amostras de cada pool (soma dos processos) viram janelas de 5 minutos em `MetricasProcessosIis` com mínimo, máximo e média.
  O pool só aparece depois da primeira requisição, pois o IIS inicia o `w3wp` sob demanda.
- **Site → pool**: `ResolvedorSitesIis` lê o `applicationHost.config`; o Worker grava o mapa em `SitesIis`, porque o painel não lê o arquivo.
  Com `cs-host` habilitado no log, o nome do site nas requisições é o hostname e pode não casar com o nome do site no IIS
  (a memória do pool deixa de aparecer na linha daquele site).
- **Retenção**: ajustável em **Configurações** (`Configuracao.RetencaoMetricasEndpointsDias`, padrão 90, e `RetencaoMetricasProcessosDias`, padrão 30).
  `ServicoManutencao.AplicarRetencaoAsync`, chamado pelo `TrabalhadorFirewall` a cada hora, também remove os eventos antigos.
- **Telas**: `/Endpoints` (KPIs, gráfico de requisições por hora/dia, tabela ordenável por qualquer coluna, até 500 linhas na tela, com filtro e exportação CSV) e `/Iis` (sites com acessos, erros,
  memória mínima/média/máxima e CPU, ordenáveis e exportáveis; tabela de pools; gráfico de memória do pool e de requisições dos sites do pool).
  Atualizam a cada 30 s.

## Exportação CSV

`Infra/ExportacaoCsv.cs` (`MapearExportacoesCsv`) expõe, com login obrigatório, `/Endpoints/exportar.csv`, `/Iis/sites.csv` e
`/Bloqueios/exportar.csv`. Os botões da tela são links com os mesmos parâmetros da tela (período, site, busca, ordem, filtros de
Bloqueios na query string). Cada endpoint repete o filtro e a ordenação da tela (`OrdenacaoMetricas`, `MontarFiltroBloqueios`) e devolve todas as
linhas, sem o limite de linhas visíveis; Bloqueios exporta no máximo `ServicoBloqueios.LimiteExportacao` (50.000).

Formato: `;` como separador, UTF-8 com BOM (Excel pt-BR), datas no fuso do servidor, cabeçalhos no idioma do usuário. Textos que
começam com `= + - @` ganham um apóstrofo (injeção de fórmula: URLs e motivos vêm do tráfego).

## Banco de dados

SQLite em `C:\ProgramData\CRSPIPS\crspips.db`, compartilhado entre painel e Worker.
`InterceptorSqlite` liga `journal_mode=WAL` e `busy_timeout` em cada conexão. Todas as datas são UTC.

Nova migration:

```powershell
dotnet dnx dotnet-ef@10.0.12 --yes migrations add NomeDaMigration -p src/CRSP.IPS.Infrastructure -s src/CRSP.IPS.Infrastructure -o Persistencia/Migracoes
```

As migrations são aplicadas automaticamente por quem iniciar primeiro (painel ou serviço).

## Segurança

- Senhas com `PasswordHasher` (PBKDF2). 5 falhas seguidas travam a conta por 15 minutos, e o login tem limite por IP.
- O primeiro administrador só pode ser criado a partir de `127.0.0.1`.
- A chave MaxMind fica no banco protegida com DPAPI (escopo da máquina).
- As chaves do cookie de login ficam em `C:\ProgramData\CRSPIPS\chaves`, protegidas com DPAPI.
- Cabeçalhos: CSP sem script inline, `X-Frame-Options: DENY`, `nosniff`, `no-referrer`.
  Scripts, estilos, fontes e imagens externos só do `cdn.jsdelivr.net`. O CSP padrão de frame-ancestors do Blazor é desligado porque o nosso já envia `frame-ancestors 'none'`.

## Traduções

A chave é o próprio texto em pt-BR. `src/CRSP.IPS.Web/Recursos/Textos.en.resx` contém o inglês.
Os nomes de países vêm de `Recursos/paises.json` (gerado a partir do ICU).

## Testes

```powershell
dotnet test CRSP.IPS.slnx
```

Um dos testes lê (sem alterar) o Windows Firewall real para validar a interface COM.
`PaginasWebTestes` renderiza todas as telas em pt-BR e inglês e salva amostras em `%TEMP%\crspips-amostras`.
Também cobre as exportações CSV (cabeçalho, filtros, autenticação), o filtro por IP do Monitor e a ordenação das tabelas de métricas.

