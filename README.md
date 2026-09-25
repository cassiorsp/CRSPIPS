<div align="center">

# 🛡️ CRSPIPS

**Sistema de Prevenção de Intrusões para servidores Windows com IIS**

Detecta ataques nos logs do seu servidor e bloqueia os atacantes automaticamente no Windows Firewall.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows Server](https://img.shields.io/badge/Windows%20Server-2016%2B-0078D4)
![IIS](https://img.shields.io/badge/IIS-10-0078D4)
![Licença GPL v3](https://img.shields.io/badge/licen%C3%A7a-GPL%20v3-blue)

Desenvolvido por **crsp.dev**

</div>

---

## Índice

- [O que é o CRSPIPS](#o-que-é-o-crspips)
- [Como funciona](#como-funciona)
- [Funcionalidades](#funcionalidades)
- [Requisitos](#requisitos)
- [Instalação passo a passo](#instalação-passo-a-passo)
- [Primeira configuração](#primeira-configuração)
- [Listas negras](#listas-negras)
- [Testando a instalação](#testando-a-instalação)
- [Comandos do dia a dia](#comandos-do-dia-a-dia)
- [Atualizando para uma nova versão](#atualizando-para-uma-nova-versão)
- [Emergência: me bloqueei](#emergência-me-bloqueei)
- [Solução de problemas](#solução-de-problemas)
- [Desinstalação](#desinstalação)
- [Para desenvolvedores](#para-desenvolvedores)
- [Créditos e licença](#créditos-e-licença)

---

## O que é o CRSPIPS

Todo servidor ligado à internet recebe, o dia inteiro, tentativas de invasão: robôs que testam senhas no
Área de Trabalho Remota (RDP), que procuram páginas de WordPress ou arquivos de configuração esquecidos,
que tentam invadir o SQL Server.

O CRSPIPS fica de olho nisso por você:

1. **Observa** os logs do IIS, os erros do HTTP.sys e as falhas de login do Windows.
2. **Reconhece** o comportamento de ataque, por exemplo 5 senhas erradas no RDP em 5 minutos, ou alguém procurando `/.env`.
3. **Bloqueia** o IP do atacante direto no Windows Firewall, por um tempo que aumenta a cada reincidência.
4. **Mostra tudo** em um painel web: quem atacou, de onde (país, cidade, provedor), o quê e quando.

Ele começa em **modo simulação**: registra o que bloquearia, mas não bloqueia nada. Assim você confere por alguns dias,
ajusta o que for preciso e só depois liga os bloqueios de verdade.

## Como funciona

```
  Internet
     │
     ▼
┌─────────────┐     logs      ┌──────────────────────┐   regras de bloqueio   ┌──────────────────┐
│  IIS / RDP  │ ────────────► │  Serviço CRSPIPS     │ ─────────────────────► │ Windows Firewall │
│ SQL Server  │               │  (detecta e decide)  │                        └──────────────────┘
└─────────────┘               └──────────┬───────────┘
                                         │ banco de dados
                                         ▼
                              ┌──────────────────────┐
                              │  Painel web CRSPIPS  │  ← você acompanha e configura por aqui
                              └──────────────────────┘
```

São duas partes:

| Parte | O que faz | Onde roda |
|---|---|---|
| **Serviço CRSPIPS** | Lê os logs, detecta ataques e mexe no firewall | Serviço do Windows, como LocalSystem |
| **Painel web** | Dashboard, bloqueios, regras, listas e configurações | Site no IIS |

O painel **nunca** mexe no firewall diretamente. Ele só registra o que você quer, e o serviço aplica em até 2 segundos.
Assim o site no IIS não precisa de permissão de administrador.

## Funcionalidades

- **Dashboard** com ataques, países de origem, IPs mais agressivos e URLs mais atacadas, atualizado sozinho a cada 30 segundos. Um filtro de período (**Hoje, 7, 14, 30, 90, 120, 180, 360 dias ou Tudo**) vale para todos os cards, gráficos e tabelas.
- **Monitor em tempo real** dos eventos suspeitos, com país, cidade, provedor e qual site foi atacado. Filtra por fonte, país e status HTTP.
- **Proteção de RDP e SQL Server** pelas falhas de login do Windows (eventos 4625, 140 e 18456).
- **Proteção dos sites**: varreduras, páginas inexistentes, injeção SQL, XSS, path traversal, Log4Shell, busca de backups e arquivos de configuração.
- **16 regras prontas**, fáceis de ligar, desligar e ajustar, e regras próprias com expressões regulares.
- **Punição progressiva**: 1 hora na primeira vez, depois 24 horas, 7 dias, 30 dias.
- **Política de países**: permitir só o Brasil no RDP, por exemplo.
- **Listas negras**: manual e **listas públicas atualizadas todo dia** (Spamhaus DROP, DShield e outras), no modo **Ativa** (direto no firewall) ou **Reativa** (bloqueia o IP da lista na primeira tentativa suspeita, sem encher o firewall). Você também pode cadastrar **suas próprias listas externas** (qualquer URL HTTPS).
- **Regras do firewall separadas pela origem**: `CRSPIPS_LOGIIS_*`, `CRSPIPS_HTTPERR_*`, `CRSPIPS_EVENTOWINDOWS_*`, manuais, lista negra e listas externas.
- **Lista branca**: IPs que nunca são bloqueados. Os IPs do servidor e dos administradores do painel já são protegidos automaticamente.
- **Importação por CSV** na lista branca e na lista negra, com modelo pronto para baixar.
- **Geolocalização** com MaxMind GeoLite2, baixada e atualizada automaticamente.
- **Auditoria** de tudo que os administradores fazem.
- Painel em **português e inglês**, com tema claro e escuro.

## Requisitos

| Item | Detalhe |
|---|---|
| Sistema | Windows Server 2016 ou superior |
| IIS | Instalado, com o **.NET 10 Hosting Bundle** e o recurso **WebSocket Protocol** (o painel usa WebSocket para atualizar as telas) |
| Firewall | Windows Firewall ligado, com a ação padrão de entrada **Bloquear** (é o padrão do Windows) |
| Logs do IIS | Formato W3C (o padrão) |
| Internet (saída HTTPS) | `download.maxmind.com`, `www.spamhaus.org`, `feeds.dshield.org` |
| Conta MaxMind | Gratuita, para a geolocalização: [maxmind.com/en/geolite2/signup](https://www.maxmind.com/en/geolite2/signup) |

---

## Instalação passo a passo

> Todos os comandos são para o **PowerShell aberto como administrador** no servidor.

### 1. Defina os nomes usados na instalação

Rode este bloco primeiro. Os comandos seguintes usam essas variáveis. Troque o domínio pelo seu.

```powershell
$Pool     = "CRSPIPS"                     # nome do pool de aplicativos do IIS
$Site     = "CRSPIPS"                     # nome do site no IIS
$Dominio  = "ips.suaempresa.com.br"       # endereço do painel
$PastaWeb = "C:\inetpub\CRSPIPS"          # onde fica o painel
$PastaSvc = "C:\Servicos\CRSPIPS"         # onde fica o serviço
$Dados    = "C:\ProgramData\CRSPIPS"      # banco, bases GeoIP e logs
$AppCmd   = "$env:windir\System32\inetsrv\appcmd.exe"
```

### 2. Instale o .NET 10 Hosting Bundle

Baixe e instale o **ASP.NET Core Runtime 10 — Windows Hosting Bundle** em
[dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).
Ele instala o runtime usado pelo painel e pelo serviço. Ative também o WebSocket do IIS (o painel é feito em Blazor
e usa uma conexão WebSocket; sem ele as telas ficam lentas) e reinicie o IIS:

```powershell
Install-WindowsFeature Web-WebSockets
iisreset
```

### 3. Gere os arquivos do sistema

Em uma máquina com o **.NET 10 SDK** (pode ser o próprio servidor), dentro da pasta do projeto:

```powershell
dotnet publish src\CRSP.IPS.Worker -c Release -o $PastaSvc
dotnet publish src\CRSP.IPS.Web    -c Release -o $PastaWeb
```

Se gerou em outra máquina, copie as duas pastas para os mesmos caminhos no servidor.

### 4. Crie a pasta de dados

```powershell
New-Item -ItemType Directory -Force $Dados | Out-Null
```

### 5. Crie o pool de aplicativos

O painel é ASP.NET Core, então o pool fica **sem código gerenciado** (No Managed Code):

```powershell
& $AppCmd add apppool /name:$Pool /managedRuntimeVersion:"" /managedPipelineMode:Integrated
```

### 6. Crie o site

```powershell
& $AppCmd add site /name:$Site /physicalPath:$PastaWeb /bindings:"http/*:80:$Dominio"
& $AppCmd set app "$Site/" /applicationPool:$Pool
```

> **Recomendado:** adicione um certificado HTTPS (por exemplo com o [win-acme](https://www.win-acme.com/))
> e deixe o painel acessível só pela rede interna ou VPN.

### 7. Dê permissão ao pool na pasta de dados

O painel precisa **gravar** no banco (o SQLite cria os arquivos `-wal` e `-shm` ao lado do banco):

```powershell
icacls $Dados /grant "IIS AppPool\${Pool}:(OI)(CI)M" /T
```

Confira. Deve aparecer uma linha `IIS APPPOOL\CRSPIPS:(OI)(CI)(M)`:

```powershell
icacls $Dados
```

### 8. Crie e inicie o serviço

```powershell
sc.exe create CRSPIPS binPath= "$PastaSvc\CRSPIPS.Worker.exe" start= delayed-auto obj= LocalSystem
sc.exe description CRSPIPS "CRSPIPS - Sistema de Prevencao de Intrusoes"
sc.exe failure CRSPIPS reset= 86400 actions= restart/5000/restart/5000/restart/30000
sc.exe start CRSPIPS
```

O `failure` faz o Windows reiniciar o serviço sozinho se ele cair.

Confira se ficou **RUNNING**:

```powershell
sc.exe query CRSPIPS
```

### 9. Crie o primeiro administrador

Por segurança, o primeiro usuário só pode ser criado **no próprio servidor**. Crie um acesso local temporário:

```powershell
& $AppCmd set site /site.name:$Site "/+bindings.[protocol='http',bindingInformation='127.0.0.1:8085:']"
```

Abra **no navegador do servidor**: <http://127.0.0.1:8085>

Crie o administrador (senha com no mínimo 10 caracteres, letras e números). Depois disso, o acesso local pode ser removido:

```powershell
& $AppCmd set site /site.name:$Site "/-bindings.[protocol='http',bindingInformation='127.0.0.1:8085:']"
```

A partir daqui, use o endereço normal: `http://ips.suaempresa.com.br` (ou `https://`).

---

## Primeira configuração

Siga esta ordem no painel:

### ✅ 1. Confira se o motor está online

No topo do painel deve aparecer o selo verde **Motor online**. Se estiver vermelho, veja [Solução de problemas](#solução-de-problemas).

### ✅ 2. Lista branca: quem nunca pode ser bloqueado

Em **Listas → Lista branca**, inclua:

- o IP fixo da empresa e da VPN;
- serviços de monitoramento (UptimeRobot e similares);
- parceiros e integrações que acessam suas APIs.

As redes internas (`10.x`, `172.16.x`, `192.168.x`) já vêm cadastradas.

> **Servidor atrás de NAT ou proxy?** Abra um log do IIS (`C:\inetpub\logs\LogFiles\W3SVC*\u_ex*.log`) e veja a coluna
> de IP do cliente (`c-ip`). Se aparecer sempre o mesmo IP interno, o CRSPIPS não enxerga o IP real do atacante. Nesse
> caso mantenha as redes privadas na lista branca e fale com quem administra o roteador ou proxy.

### ✅ 3. Geolocalização (país, cidade e provedor)

1. Crie uma conta gratuita em [maxmind.com](https://www.maxmind.com/en/geolite2/signup).
2. No site da MaxMind: **Account → Manage License Keys → Generate new license key**. Anote o **Account ID** e a **chave**.
3. No painel: **Configurações → Geolocalização**, preencha os dois campos e clique em **Salvar e baixar**.

O serviço baixa as bases em até 15 segundos e as atualiza sozinho a cada 24 horas.
A chave fica guardada criptografada. Use a chave de licença, **nunca** a senha da conta.

### ✅ 4. Revise as regras

Em **Regras de detecção** estão as 16 regras prontas. Os ajustes mais comuns:

| Regra | Padrão | Quando ajustar |
|---|---|---|
| Falha de login RDP / Windows | 5 falhas em 5 min | Se usuários legítimos erram muito a senha |
| Excesso de 404 | 30 em 1 min | Se seus sites têm muitos links quebrados |
| Scanner de PHP/JSP | 3 em 5 min | **Desative** se o IIS hospeda algum site PHP |
| Varredura de Exchange/OWA | 3 em 10 min | **Desative** se o servidor tem Exchange |
| Excesso de 401/403 | desativada | Deixe desligada se usa autenticação Windows ou app com token |

**Rotas que devolvem 404 de propósito** (ex.: uma API que responde 404 quando o registro não existe): edite a regra
**Excesso de 404** e preencha **URLs ignoradas** com uma expressão regular. Ela é testada contra a URL e contra
"MÉTODO URL":

| Quero ignorar | URLs ignoradas |
|---|---|
| Só o POST da rota | `^POST /api/configuracao/empresa` |
| A rota com qualquer método | `^/api/configuracao/empresa` |
| Várias rotas | `^/api/configuracao/empresa\|^/favicon\.ico\|^/img/site\.webmanifest` |

O campo existe em todas as regras de Log IIS e HTTPERR e passa a valer em até 5 segundos (o serviço relê as regras a cada ciclo).

### ✅ 5. Política de países (recomendado para RDP)

Se só pessoas do Brasil acessam o RDP, em **Países** escolha:

- **Modo:** Permitir somente países listados
- **Países:** Brasil
- **Como aplicar:** Firewall por portas
- **Portas:** `3389`

Isso barra o RDP de qualquer outro país **antes** da tela de login. Para os sites, prefira
**Reativa (somente eventos suspeitos)**: só é bloqueado quem, vindo de fora, fizer algo suspeito.
Visitantes, Google e integrações não são afetados.

### ✅ 6. Deixe em simulação por alguns dias

Acompanhe o **Dashboard**, o **Monitor** e a tela **Bloqueios** (situação *Simulado*). Confira se:

- os IPs bloqueados são mesmo atacantes (varreduras, senhas erradas em sequência);
- nenhum cliente, parceiro ou funcionário aparece entre os bloqueados.

### ✅ 7. Ligue os bloqueios

Em **Configurações**, desligue **Modo simulação** e clique em **Salvar configurações do motor**.
Se quiser recomeçar as estatísticas do zero, use antes **Configurações → Limpar histórico**.

---

## Listas negras

O CRSPIPS tem dois tipos de lista negra:

### Lista negra manual

Em **Listas → Lista negra**, você cadastra IPs ou redes que devem ficar bloqueados **para sempre**.
Aceita IP (`203.0.113.7`), rede (`203.0.113.0/24`) ou intervalo (`203.0.113.1-203.0.113.50`).

### Importar várias faixas por CSV (lista negra e lista branca)

Na tela da lista, clique em **Baixar modelo CSV**, preencha e envie em **Importar arquivo CSV**. O formato é:

```csv
faixa;descricao
203.0.113.7;IP único
198.51.100.0/24;Rede em CIDR
192.0.2.10-192.0.2.50;Intervalo de endereços
2001:db8::/32;Faixa IPv6
```

- A descrição é opcional. O separador pode ser ponto e vírgula (padrão do Excel em português) ou vírgula.
- Até 5.000 linhas e 1 MB por arquivo. Linhas repetidas ou já cadastradas são ignoradas.
- Cada linha passa pelas mesmas regras da inclusão manual. As linhas com erro aparecem na tela com o número da linha,
  e as demais são gravadas.

### Listas externas (atualizadas todo dia)

Em **Listas → Listas externas** ficam as listas públicas de IPs maliciosos. O serviço baixa cada uma a cada 24 horas.

| Lista | O que contém | Padrão | Recomendação |
|---|---|---|---|
| **Spamhaus DROP** | Redes de criminosos, sem nenhum uso legítimo (~1.500 faixas) | Ativa | Manter **Ativa** |
| **DShield Top 20** | As 20 redes que mais atacam no mundo | Ativa | Manter **Ativa** |
| **IPsum nível 3+** | IPs em 3 ou mais listas de ataque (dezenas de milhares) | Desativada | **Reativa** |
| **CINS Army** | IPs com pior reputação em sensores de segurança | Desativada | **Reativa** |
| **blocklist.de** | IPs reportados nas últimas 48 h (mais ruído) | Desativada | **Reativa** |

Cada lista tem quatro modos (botões no cartão da lista e campo **Modo inicial** ao cadastrar):

| Modo | Baixa? | Vai para o firewall? | O que acontece com um IP da lista |
|---|---|---|---|
| **Desativada** | não | não | nada |
| **Avaliação** | sim | não | só aparece nas **coincidências** (quais IPs da lista acessaram seus sites). **Não bloqueia.** |
| **Reativa** | sim | **não** | é bloqueado na **primeira tentativa suspeita** (qualquer evento que case com uma regra ativa: 404, `/.env`, senha errada no RDP…), sem esperar o limite da regra. Uma visita normal ao site não bloqueia. |
| **Ativa** | sim | **sim** | todas as faixas são bloqueadas no firewall antes de chegar ao servidor |

**Ativa ou Reativa?**

- **Ativa** para listas pequenas e de altíssima confiança (Spamhaus, DShield): bloqueiam até o primeiro pacote e
  custam quase nada ao firewall.
- **Reativa** para listas grandes de IPs individuais (IPsum, CINS, blocklist.de): não enchem o firewall com dezenas
  de milhares de endereços e não derrubam quem só navega. Essas listas trazem IPs dinâmicos e CGNAT de operadoras,
  que depois vão para usuários comuns; na Reativa, só é bloqueado quem realmente tentar atacar.
- O bloqueio da Reativa é um bloqueio comum do CRSPIPS: aparece em **Bloqueios** com a origem *Lista externa* e o
  motivo `Lista externa: <nome>`, segue a punição progressiva e vai para a regra da fonte do evento
  (`CRSPIPS_LOGIIS_*`, `CRSPIPS_HTTPERR_*` ou `CRSPIPS_EVENTOWINDOWS_*`).

**Como avaliar uma lista antes de usar:**

1. Coloque a lista em **Avaliação**.
2. Espere alguns dias e veja a tabela **Coincidências com o tráfego real** (a coluna **Bloqueio** mostra a situação
   de cada IP).
3. Se só aparecerem varreduras e ataques, pode passar para **Reativa** ou **Ativa**.
4. Se aparecerem acessos normais de operadoras brasileiras (Vivo, Claro, TIM) ou de parceiros, prefira **Reativa**
   (ou deixe desativada): na **Ativa** a lista bloquearia usuários legítimos.

**Proteções das listas:**

- Nada que esteja na lista branca, nas redes internas, nos IPs do servidor ou nos IPs dos administradores é
  bloqueado, mesmo que venha na lista (vale para Reativa e Ativa).
- Se um download falhar ou vier corrompido, a versão anterior continua valendo.
- As listas em modo **Ativa** ficam em regras próprias no firewall (`CRSPIPS_LISTAEXTERNA_*`), separadas dos
  bloqueios do motor.

**Adicionar uma lista própria:** em **Listas → Listas externas → Nova lista**, informe o nome, uma ou mais URLs HTTPS,
o formato (texto com um IP ou faixa por linha, Spamhaus JSON ou DShield), o intervalo de atualização e o **Modo
inicial** (Desativada, Avaliação, Reativa ou Ativa). Comece em **Avaliação**. As listas cadastradas por você podem ser
excluídas; as do catálogo só podem ser desativadas.

> Em **modo simulação**, as listas são baixadas, mas nada vai para o firewall.

---

## Testando a instalação

### Simular ataques sem precisar de um ataque real

O projeto traz um script que grava um log no formato do IIS com ataques simulados. Ele usa só IPs reservados
para documentação, que não pertencem a ninguém.

1. No painel: **Configurações → Pasta de logs do IIS** = `C:\ProgramData\CRSPIPS\simulacao` → **Salvar configurações do motor**.
2. Aguarde uns 5 segundos e rode:

```powershell
powershell -ExecutionPolicy Bypass -File .\ferramentas\SimularAtaque.ps1
```

3. Em até 5 segundos devem aparecer no **Monitor** e em **Bloqueios** (como *Simulado*):
   - `203.0.113.7`: *Excesso de 404*
   - `198.51.100.23`: *Varredura de URLs sensíveis*
   - `203.0.113.50`: tráfego normal, **não** deve ser bloqueado
4. Ao terminar, volte a pasta de logs para `C:\inetpub\logs\LogFiles`.

### Ver as regras criadas no firewall

Todas as regras ficam no grupo **CRSPIPS**, são de entrada e têm até 1.000 endereços cada. O número no final cresce
conforme a quantidade de endereços (`_00001`, `_00002`…):

| Regra | O que contém |
|---|---|
| `CRSPIPS_LOGIIS_00001` | IPs bloqueados por regras do **Log IIS** (404, varreduras, injeção SQL…) |
| `CRSPIPS_HTTPERR_00001` | IPs bloqueados por regras do **HTTPERR** (host inexistente, requisições malformadas) |
| `CRSPIPS_EVENTOWINDOWS_00001` | IPs bloqueados por **eventos do Windows** (falha de login RDP, SQL Server) |
| `CRSPIPS_MANUAL_00001` | Bloqueios manuais feitos no painel |
| `CRSPIPS_LISTANEGRA_00001` | Lista negra manual |
| `CRSPIPS_LISTAEXTERNA_00001` | Listas externas em modo **Ativa** |
| `CRSPIPS_PAIS_BLOQUEAR_TCP_00001` / `_UDP_` | Política de países em "Firewall por portas", modo Bloquear países listados |
| `CRSPIPS_PAIS_PERMITIR_TCP_00001` / `_UDP_` | Política de países em "Firewall por portas", modo Permitir somente |

Os bloqueios por país (modos reativos) e por lista externa **Reativa** vão para a regra da fonte do evento que os
gerou (LOGIIS, HTTPERR ou EVENTOWINDOWS). Ao atualizar de uma versão anterior, as regras com os nomes antigos
(`CRSPIPS_Bloqueio_001`, `CRSPIPS_ListaExterna_001`, `CRSPIPS_Pais_…`) são apagadas e recriadas com os nomes novos
na primeira conferência do serviço (em até 5 minutos).

```powershell
Get-NetFirewallRule -Group CRSPIPS | Sort-Object DisplayName | Format-Table DisplayName, Enabled, Action
```

Quantos endereços há em cada regra:

```powershell
Get-NetFirewallRule -Group CRSPIPS | ForEach-Object { [pscustomobject]@{ Regra = $_.DisplayName; Enderecos = ($_ | Get-NetFirewallAddressFilter).RemoteAddress.Count } }
```

Endereços de uma regra:

```powershell
Get-NetFirewallRule -DisplayName CRSPIPS_LOGIIS_00001 | Get-NetFirewallAddressFilter | Select-Object -ExpandProperty RemoteAddress
```

Você também pode abrir `wf.msc` → **Regras de Entrada** e filtrar pelo grupo **CRSPIPS**.

### Testes automatizados

```powershell
dotnet test CRSP.IPS.slnx
```

---

## Comandos do dia a dia

| O que fazer | Comando |
|---|---|
| Ver o estado do serviço | `sc.exe query CRSPIPS` |
| Ver a configuração do serviço | `sc.exe qc CRSPIPS` |
| Parar o serviço | `sc.exe stop CRSPIPS` |
| Iniciar o serviço | `sc.exe start CRSPIPS` |
| Reiniciar o serviço | `Restart-Service CRSPIPS` |
| Parar o painel | `& $AppCmd stop apppool /apppool.name:$Pool` |
| Iniciar o painel | `& $AppCmd start apppool /apppool.name:$Pool` |
| Reciclar o painel | `& $AppCmd recycle apppool /apppool.name:$Pool` |

### Analisar o serviço (logs)

Últimos eventos do serviço no Visualizador de Eventos:

```powershell
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'CRSPIPS' } -MaxEvents 30 | Format-Table TimeCreated, LevelDisplayName, Message -Wrap
```

Falhas de inicialização (o arquivo só existe se houve falha):

```powershell
if (Test-Path "$Dados\logs\worker-falhas.log") { Get-Content "$Dados\logs\worker-falhas.log" -Tail 50 } else { "Nenhuma falha de inicializacao registrada." }
```

No painel:

- **Dashboard → Motor (Worker):** último sinal, última sincronização e último erro.
- **Configurações → Leitura das fontes:** quais logs o serviço está lendo e quando leu pela última vez.

---

## Atualizando para uma nova versão

```powershell
sc.exe stop CRSPIPS
& $AppCmd stop apppool /apppool.name:$Pool

dotnet publish src\CRSP.IPS.Worker -c Release -o $PastaSvc
dotnet publish src\CRSP.IPS.Web    -c Release -o $PastaWeb

sc.exe start CRSPIPS
& $AppCmd start apppool /apppool.name:$Pool
```

O banco é atualizado sozinho na inicialização. Regras e listas novas entram automaticamente, e as suas configurações são mantidas.

---

## Emergência: me bloqueei

O CRSPIPS evita isso: nunca bloqueia a lista branca, os IPs do servidor e os IPs de quem entrou no painel nos últimos 7 dias.
Mesmo assim, se perder o acesso, entre no servidor pelo **console** (Hyper-V, VMware, iDRAC/iLO ou o painel do provedor) e rode:

```powershell
sc.exe stop CRSPIPS
Get-NetFirewallRule -Group CRSPIPS | Remove-NetFirewallRule
```

Isso para o serviço e remove todas as regras criadas por ele. **Pare o serviço antes**, senão ele recria as regras em 2 segundos.

Depois, no painel, coloque seu IP na **lista branca** (ou desative a política de países) e inicie o serviço de novo:

```powershell
sc.exe start CRSPIPS
```

> Se a política de países estava em **Firewall por portas**, ela desativa as regras de permissão de outros programas
> nessas portas (como a do RDP). Com o serviço rodando, basta mudar a política para **Desativada** no painel que
> ele as reativa. Sem o serviço, reative a regra do RDP em `wf.msc` → Regras de Entrada → **Área de Trabalho Remota**.

---

## Solução de problemas

| Sintoma | Causa provável | Solução |
|---|---|---|
| `StartService FAILED 1053` | Serviço não respondeu a tempo | Veja `C:\ProgramData\CRSPIPS\logs\worker-falhas.log` e os eventos do serviço |
| Painel mostra **Motor offline** | Serviço parado ou com erro | `sc.exe query CRSPIPS` e os logs acima |
| Erro 500 ao salvar no painel | Pool sem permissão na pasta de dados | Refaça o passo 7 (`icacls`) e recicle o pool |
| Tela de primeiro acesso diz que só aceita localhost | Acesso não veio de `127.0.0.1` | Use o endereço `http://127.0.0.1:8085` **no próprio servidor** (passo 9) |
| Nada aparece no Monitor | Nenhum log sendo lido | **Configurações → Leitura das fontes** e confira a pasta de logs do IIS |
| Sem país, cidade e provedor | Bases GeoIP ausentes | **Configurações → Geolocalização**, confira o resultado do download |
| `Could not load file or assembly ...` | Publicação misturada com arquivos antigos | Pare serviço e pool, apague a pasta publicada e publique de novo |
| Lista externa com erro | Servidor sem acesso à internet | Libere HTTPS de saída para `www.spamhaus.org` e `feeds.dshield.org` |

---

## Desinstalação

```powershell
sc.exe stop CRSPIPS
sc.exe delete CRSPIPS
Get-NetFirewallRule -Group CRSPIPS | Remove-NetFirewallRule

& $AppCmd delete site $Site
& $AppCmd delete apppool $Pool

Remove-Item $PastaSvc, $PastaWeb -Recurse -Force
# Opcional (apaga banco, histórico e bases GeoIP):
# Remove-Item $Dados -Recurse -Force
```

---

## Para desenvolvedores

- Solução: `CRSP.IPS.slnx` (.NET 10), em Clean Architecture: Domain, Application, Infrastructure, Web e Worker.
- Arquitetura, motor, banco e migrations: [docs/ARQUITETURA.md](docs/ARQUITETURA.md).
- Rodar localmente: o painel roda sem privilégios (`dotnet run --project src\CRSP.IPS.Web`); o serviço exige terminal de administrador (`dotnet run --project src\CRSP.IPS.Worker`).

## Créditos e licença

CRSPIPS é desenvolvido e mantido por **crsp.dev**.

Inspirado em projetos como IPBan, fail2ban, RdpGuard e FireMon. Geolocalização por MaxMind GeoLite2;
listas públicas de Spamhaus, SANS DShield, IPsum, CINS Army e blocklist.de, cada uma com os próprios termos de uso.

Distribuído sob a licença **GNU GPL v3**. Veja [`LICENSE.txt`](LICENSE.txt).
