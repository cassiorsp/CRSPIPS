# 🚀 Instalação e Primeira Configuração

Este guia orienta a implantação completa do **CRSPIPS** em ambientes Windows Server.

---

## 1. Pré-Requisitos do Servidor

| Componente | Requisito Mínimo | Observação |
|---|---|---|
| **Sistema Operacional** | Windows Server 2016 ou superior | Compatível com Server 2019, 2022, 2025 e Windows 10/11 (Dev). |
| **IIS** | IIS 10 com recurso WebSocket ativado | O painel Blazor requer WebSocket para atualizações em tempo real sem latência. |
| **.NET Runtime** | ASP.NET Core 10 Hosting Bundle | Instala o runtime do serviço e o módulo ASP.NET Core para o IIS. |
| **Firewall** | Windows Defender Firewall ativado | Ação padrão de entrada configurada como *Bloquear* (padrão de fábrica do Windows). |
| **Formato dos Logs** | Logs do IIS em formato W3C | Habilitar os campos `time-taken`, `sc-status` e preferencialmente `cs-host`. |
| **Conectividade de Saída** | HTTPS (porta 443) | Necessária para download de bases MaxMind GeoIP e listas de ameaças públicas. |

---

## 2. Passo a Passo de Instalação (PowerShell como Administrador)

Execute os comandos abaixo em uma sessão do **PowerShell com privilégios elevados**:

### Passo 1: Definição de Variáveis de Ambiente
```powershell
$Pool     = "CRSPIPS"                     # Nome do App Pool do IIS
$Site     = "CRSPIPS"                     # Nome do Site no IIS
$Dominio  = "ips.suaempresa.com.br"       # Domínio ou FQDN de acesso ao painel
$PastaWeb = "C:\inetpub\CRSPIPS"          # Diretório dos arquivos web
$PastaSvc = "C:\Servicos\CRSPIPS"         # Diretório do serviço de background
$Dados    = "C:\ProgramData\CRSPIPS"      # Diretório de banco SQLite, GeoIP e logs
$AppCmd   = "$env:windir\System32\inetsrv\appcmd.exe"
```

### Passo 2: Instalação dos Recursos do Windows e .NET 10
1. Baixe e instale o **.NET 10 Hosting Bundle** do site oficial da Microsoft.
2. Ative o suporte a WebSockets no IIS e reinicie o serviço:
```powershell
Install-WindowsFeature Web-WebSockets
iisreset
```

### Passo 3: Compilação e Publicação dos Binários
Se estiver compilando a partir do repositório:
```powershell
dotnet publish src\CRSP.IPS.Worker -c Release -o $PastaSvc
dotnet publish src\CRSP.IPS.Web    -c Release -o $PastaWeb
```

### Passo 4: Criação do Diretório de Dados e Permissões do IIS
O SQLite opera em modo WAL e precisa gerar os arquivos temporários `-wal` e `-shm`:
```powershell
New-Item -ItemType Directory -Force $Dados | Out-Null
icacls $Dados /grant "IIS AppPool\${Pool}:(OI)(CI)M" /T
```

### Passo 5: Criação do Application Pool e do Site no IIS
O painel é ASP.NET Core e deve operar em modo sem código gerenciado (*No Managed Code*):
```powershell
& $AppCmd add apppool /name:$Pool /managedRuntimeVersion:"" /managedPipelineMode:Integrated
& $AppCmd add site /name:$Site /physicalPath:$PastaWeb /bindings:"http/*:80:$Dominio"
& $AppCmd set app "$Site/" /applicationPool:$Pool
```

> [!TIP]
> Em produção, configure um certificado HTTPS via IIS Manager ou utilizando clientes ACME automatizados como o [win-acme](https://www.win-acme.com/).

### Passo 6: Criação e Inicialização do Serviço Windows
Crie o serviço `CRSPIPS` executando como `LocalSystem` com política de reinício automático em caso de falha:
```powershell
sc.exe create CRSPIPS binPath= "$PastaSvc\CRSPIPS.Worker.exe" start= delayed-auto obj= LocalSystem
sc.exe description CRSPIPS "CRSPIPS - Sistema de Prevencao de Intrusoes"
sc.exe failure CRSPIPS reset= 86400 actions= restart/5000/restart/5000/restart/30000
sc.exe start CRSPIPS
```

Verifique se o serviço iniciou com sucesso:
```powershell
sc.exe query CRSPIPS
```

---

## 3. Criação do Primeiro Administrador (Bootstrap Seguro)

Por razões de segurança, o primeiro usuário administrador só pode ser criado **localmente a partir do próprio servidor (127.0.0.1)**.

1. Adicione um binding local temporário na porta 8085:
```powershell
& $AppCmd set site /site.name:$Site "/+bindings.[protocol='http',bindingInformation='127.0.0.1:8085:']"
```
2. Abra o navegador do servidor e acesse: `http://127.0.0.1:8085`
3. Preencha os dados do primeiro administrador (senha mínima de 10 caracteres, combinando letras e números).
4. Remova o binding local temporário:
```powershell
& $AppCmd set site /site.name:$Site "/-bindings.[protocol='http',bindingInformation='127.0.0.1:8085:']"
```

A partir deste momento, utilize a URL corporativa normal: `https://ips.suaempresa.com.br`.

---

## 4. Checklist da Primeira Configuração

Após realizar o primeiro login no painel:

* [ ] **Status do Motor:** Verifique se o selo **Motor online** no topo do painel está verde.
* [ ] **Lista Branca:** Cadastre o IP público fixo da empresa, VPNs administrativas e servidores de monitoramento externo (ex: UptimeRobot). Redes locais (`10.x`, `192.168.x`, `172.16.x`) já vêm protegidas por padrão.
* [ ] **Geolocalização (MaxMind):** Crie uma conta gratuita no [MaxMind](https://www.maxmind.com/en/geolite2/signup), gere sua chave de licença e configure em **Configurações $\to$ Geolocalização**. As bases serão baixadas em até 15 segundos.
* [ ] **Período de Simulação:** Mantenha o **Modo Simulação** ativado durante os primeiros 3 a 7 dias. Acompanhe o Dashboard e a lista de bloqueios para validar que nenhum usuário legítimo é afetado.
* [ ] **Ativação dos Bloqueios Reais:** Desative o **Modo Simulação** em **Configurações** para que o Worker comece a inserir as regras reais no Windows Firewall.
