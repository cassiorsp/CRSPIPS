# 🛠️ Operação, Diagnóstico e Troubleshooting

Guia de manutenção diária, procedimentos de emergência e resolução de problemas comuns no CRSPIPS.

---

## 1. Comandos Operacionais do Dia a Dia

Todos os comandos devem ser executados no **PowerShell como Administrador**:

### Gerenciamento do Serviço do Windows
```powershell
# Consultar status do serviço
sc.exe query CRSPIPS

# Iniciar o serviço
sc.exe start CRSPIPS

# Parar o serviço
sc.exe stop CRSPIPS

# Reiniciar o serviço
Restart-Service CRSPIPS
```

### Gerenciamento do Painel no IIS
```powershell
$AppCmd = "$env:windir\System32\inetsrv\appcmd.exe"

# Reciclar o pool do painel
& $AppCmd recycle apppool /apppool.name:CRSPIPS

# Parar o site
& $AppCmd stop site /site.name:CRSPIPS

# Iniciar o site
& $AppCmd start site /site.name:CRSPIPS
```

---

## 2. Inspeção de Regras Ativas no Firewall

Para verificar se o CRSPIPS está aplicando regras corretamente no Windows Defender Firewall:

### Listar todas as regras criadas pelo CRSPIPS
```powershell
Get-NetFirewallRule -Group CRSPIPS | Sort-Object DisplayName | Format-Table DisplayName, Enabled, Action
```

### Contar a quantidade de IPs bloqueados por regra
```powershell
Get-NetFirewallRule -Group CRSPIPS | ForEach-Object {
    [PSCustomObject]@{
        Regra     = $_.DisplayName
        Enderecos = ($_ | Get-NetFirewallAddressFilter).RemoteAddress.Count
    }
}
```

### Listar os IPs de uma regra específica
```powershell
Get-NetFirewallRule -DisplayName CRSPIPS_LOGIIS_00001 | Get-NetFirewallAddressFilter | Select-Object -ExpandProperty RemoteAddress
```

---

## 3. Emergência: "Me Bloqueei do Servidor"

O sistema possui proteções para impedir o auto-bloqueio, mas caso ocorra perda de acesso remoto por configuração incorreta de política de países:

1. Acesse o servidor pelo **Console de Gerenciamento do Datacenter/Hypervisor** (Hyper-V, VMware, iDRAC, iLO ou VNC do provedor).
2. Execute o comando de emergência para parar o serviço e remover as regras:
```powershell
sc.exe stop CRSPIPS
Get-NetFirewallRule -Group CRSPIPS | Remove-NetFirewallRule
```
> [!IMPORTANT]
> É fundamental parar o serviço **antes** de remover as regras; caso contrário, o Worker recriará todas as regras em até 2 segundos.

3. Acesse o painel, adicione o seu IP na **Lista Branca** (ou desative temporariamente a política de países) e reinicie o serviço:
```powershell
sc.exe start CRSPIPS
```

---

## 4. Matriz de Resolução de Problemas (Troubleshooting)

| Sintoma | Causa Mais Provável | Ação de Resolução |
|---|---|---|
| **Erro 1053 ao iniciar o serviço** | Serviço demorou mais de 30s para responder ao SCM. | Verifique `C:\ProgramData\CRSPIPS\logs\worker-falhas.log`. Assegure que as migrations do SQLite rodam em background. |
| **Painel mostra "Motor offline"** | O serviço `CRSPIPS` está parado ou em estado de falha. | Execute `sc.exe query CRSPIPS` e inspecione o Visualizador de Eventos (`Application` com fonte `CRSPIPS`). |
| **Erro HTTP 500 no Painel** | O IIS AppPool não possui permissão de escrita na pasta de dados. | Reexecute: `icacls C:\ProgramData\CRSPIPS /grant "IIS AppPool\CRSPIPS:(OI)(CI)M" /T` e recicle o pool. |
| **Primeiro acesso diz "Apenas Localhost"** | O navegador não acessou via `127.0.0.1`. | Abra o navegador local no console do servidor e acesse via `http://127.0.0.1:8085`. |
| **Eventos não aparecem no Monitor** | Caminho dos logs do IIS ou HTTPERR configurado incorretamente. | Em **Configurações $\to$ Leitura das fontes**, verifique o status de leitura dos arquivos e confirme se o IIS está gerando logs em formato W3C. |
| **Sem bandeiras ou nomes de países** | Bases de dados MaxMind GeoIP ausentes ou vencidas. | Em **Configurações $\to$ Geolocalização**, configure o Account ID e Chave de Licença MaxMind e clique em **Salvar e baixar**. |
| **Endpoints ou IIS sem métricas** | O serviço acabou de iniciar ou o pool não recebeu tráfego. | A coleta começa a partir do início do serviço. Pools do IIS só geram processo `w3wp.exe` após a primeira requisição. |
| **`dotnet publish` dá "Access Denied"** | Arquivos travados pelo serviço ou pelo pool em execução. | Pare o serviço (`sc.exe stop CRSPIPS`) e o pool (`appcmd stop apppool`) antes de rodar o comando de publish. |

---

## 5. Procedimento de Atualização de Versão

Para atualizar o CRSPIPS sem perder históricos ou configurações:

```powershell
# 1. Pare o serviço e a aplicação web
sc.exe stop CRSPIPS
& $AppCmd stop apppool /apppool.name:CRSPIPS

# 2. Publique os novos binários sobre as pastas existentes
dotnet publish src\CRSP.IPS.Worker -c Release -o C:\Servicos\CRSPIPS
dotnet publish src\CRSP.IPS.Web    -c Release -o C:\inetpub\CRSPIPS

# 3. Reinicie o serviço e a aplicação web
sc.exe start CRSPIPS
& $AppCmd start apppool /apppool.name:CRSPIPS
```

O banco SQLite é atualizado automaticamente pelo inicializador na subida da nova versão.
