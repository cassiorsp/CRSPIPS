# CRSPIPS — Sistema de Prevenção de Intrusões para Windows Server / IIS

Solução `Donc.IPS.slnx` (.NET 10). Inspirado em IPBan, IPBan Pro, fail2ban, RdpGuard e FireMon.

## Arquitetura

```
src/
  Donc.IPS.Domain          Entidades, EnderecoIp, FaixaIp (CIDR/intervalo), PoliticaProgressao. Sem dependências.
  Donc.IPS.Application     Casos de uso (Servicos/) e motor (Motor/): detecção, sincronização do firewall, política de países.
  Donc.IPS.Infrastructure  EF Core + SQLite (WAL), Windows Firewall (COM), leitores W3C/HTTPERR/Event Log, MaxMind.
  Donc.IPS.Web             Painel Razor Pages + Bootstrap 5 + Chart.js, Cookie Auth, pt-BR / en.
  Donc.IPS.Worker          Serviço Windows "CRSPIPS".
tests/
  Donc.IPS.Tests           Unidade + integração (SQLite real, firewall falso) + renderização das telas.
```

**Regra central:** o painel web **nunca** altera o firewall. Ele grava o estado desejado no banco
(bloqueios, listas, política de países) e o Worker, que roda como LocalSystem, reconcilia o Windows Firewall
a cada 2 segundos. O Worker também corrige alterações manuais feitas no firewall (verificação completa a cada 5 min).

### Motor

```
Fontes (IFonteEventos)          ServicoDeteccao                        ServicoSincronizacaoFirewall
  LeitorLogIis   (u_ex*.log)  →   lista branca / servidor / admins  →    bloqueios ativos + lista negra
  LeitorHttpErr  (httperr*.log)   política de países (reativa)            → regras CRSPIPS_Bloqueio_NNN (1000 IPs/regra)
  LeitorEventosWindows            regras (janela deslizante em memória)   expiração
    4625 Security                 punição progressiva (1h, 24h, 7d...)  ServicoPoliticaPaisesFirewall
    140  RdpCoreTS                modo simulação                           → regras CRSPIPS_Pais_* (portas TCP/UDP)
    18456 SQL Server
```

- **Regras de detecção** no modelo *jail*: fonte + critério (código HTTP, regex de URL, motivo HTTPERR, ID de evento) + limite em uma janela.
- **Punição progressiva**: cada reincidência dentro da janela sobe um nível na lista de tempos.
- **Modo simulação** (ligado na instalação): bloqueios automáticos são registrados, mas não aplicados.
- **Proteções**: lista branca, IPs do próprio servidor e IPs usados por administradores do painel nos últimos 7 dias nunca são bloqueados.
- **Política de países**: *Permitir somente* ou *Bloquear* países listados, aplicada de forma **reativa** (bloqueia o IP no primeiro evento) ou **no firewall por portas** (ex.: RDP 3389 só do Brasil).

## Pré-requisitos

- Windows Server 2016+ com IIS, .NET 10 Hosting Bundle.
- Windows Firewall ativo com **ação padrão de entrada = Bloquear** (padrão do Windows).
- Logs do IIS em formato W3C (padrão). Campos mínimos: `date time c-ip cs-method cs-uri-stem sc-status`.
- Conta gratuita MaxMind GeoLite2 (maxmind.com). Em **Configurações > Geolocalização**, informe o **ID da conta** e uma
  **chave de licença** (Account > Manage License Keys). O Worker baixa City, ASN e Country CSV, confere o SHA-256 e instala em
  `C:\ProgramData\CRSPIPS\GeoIP`, verificando atualizações a cada 24h. A chave fica no banco criptografada com DPAPI (escopo da máquina).
  O servidor precisa de acesso HTTPS de saída a `download.maxmind.com`.

## Instalação

```powershell
dotnet publish src/Donc.IPS.Worker -c Release -o C:\Servicos\CRSPIPS
dotnet publish src/Donc.IPS.Web    -c Release -o C:\inetpub\CRSPIPS

# Em PowerShell como administrador. Se o servico ja existir: sc.exe stop CRSPIPS; sc.exe delete CRSPIPS
sc.exe create CRSPIPS binPath= "C:\Servicos\CRSPIPS\CRSPIPS.Worker.exe" start= delayed-auto obj= LocalSystem
sc.exe description CRSPIPS "CRSPIPS - Sistema de Prevenção de Intrusões"
sc.exe failure CRSPIPS reset= 86400 actions= restart/5000/restart/5000/restart/30000
sc.exe start CRSPIPS
```

Não rode o Worker pelo Visual Studio/terminal ao mesmo tempo que o serviço: os dois disputariam o firewall.
Se o serviço não iniciar, veja `C:\ProgramData\CRSPIPS\logs\worker-falhas.log` e o Visualizador de Eventos
(Logs do Windows > Aplicativo, origem CRSPIPS).

No IIS, crie um site para `C:\inetpub\CRSPIPS` (de preferência com HTTPS e acessível somente pela rede interna/VPN)
e dê ao usuário do app pool (`IIS AppPool\<nome>`) permissão de **modificação** em `C:\ProgramData\CRSPIPS`
(o SQLite em modo WAL cria os arquivos `-wal` e `-shm`).

Web e Worker precisam apontar para o mesmo banco (`CRSPIPS:CaminhoBanco` nos dois `appsettings.json`).

### Primeiro acesso

Abra o painel **no próprio servidor** (`https://localhost/...`): enquanto não existe usuário, a tela de primeiro acesso
cria o administrador e só aceita requisições de localhost.

### Colocando em produção

1. Deixe em **modo simulação** por alguns dias e acompanhe Dashboard, Monitor e a grid de Bloqueios (situação *Simulado*).
2. Inclua na lista branca o IP fixo da empresa, VPN, monitoramento e parceiros.
3. Ajuste limites das regras e desligue o modo simulação em Configurações.

> Se o servidor está atrás de NAT com tradução de origem ou de um proxy reverso, os logs mostram o IP interno para todo
> o tráfego. Nesse caso mantenha as redes privadas na lista branca (padrão) e confirme o `c-ip` dos logs antes de desligar a simulação.

## Desenvolvimento

```powershell
dotnet build Donc.IPS.slnx
dotnet test                                  # inclui leitura (somente leitura) do Windows Firewall real
dotnet tool restore
dotnet ef migrations add Nome --project src/Donc.IPS.Infrastructure --output-dir Persistencia/Migracoes
```

O Worker exige administrador (manifest `requireAdministrator`), pois altera o firewall.
Para simular ataques sem IIS: aponte a pasta de logs do IIS para `C:\ProgramData\CRSPIPS\simulacao` e rode `ferramentas\SimularAtaque.ps1`.
Traduções: `src/Donc.IPS.Web/Recursos/Textos.en.resx` (a chave é o próprio texto em pt-BR).
