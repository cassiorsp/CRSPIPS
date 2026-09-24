<#
.SYNOPSIS
    Gera um log no formato W3C do IIS com trafego de ataque simulado, para testar o CRSPIPS sem IIS.

.DESCRIPTION
    Usa somente IPs das faixas reservadas para documentacao (RFC 5737: 203.0.113.0/24 e 198.51.100.0/24),
    que nao pertencem a ninguem. Por isso esses IPs aparecem sem pais no painel.

    Antes de rodar:
      1. Painel > Configuracoes > "Pasta de logs do IIS" = C:\ProgramData\CRSPIPS\simulacao  (salvar)
      2. Aguarde ~5 segundos para o Worker registrar a pasta.

.EXAMPLE
    .\ferramentas\SimularAtaque.ps1
    .\ferramentas\SimularAtaque.ps1 -Cenario varredura
#>
param(
    [string]$Pasta = 'C:\ProgramData\CRSPIPS\simulacao\W3SVC1',
    [ValidateSet('todos', 'varredura', 'wordpress', 'legitimo')]
    [string]$Cenario = 'todos'
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Pasta | Out-Null

$agora = [DateTime]::UtcNow
$arquivo = Join-Path $Pasta ("u_ex{0:yyMMdd}.log" -f $agora)
$linhas = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path $arquivo)) {
    $linhas.Add('#Software: Microsoft Internet Information Services 10.0')
    $linhas.Add('#Version: 1.0')
    $linhas.Add(('#Date: {0:yyyy-MM-dd HH:mm:ss}' -f $agora))
    $linhas.Add('#Fields: date time s-ip cs-method cs-uri-stem cs-uri-query s-port cs-username c-ip cs(User-Agent) cs(Referer) sc-status sc-substatus sc-win32-status time-taken')
}

function Adicionar([int]$segundos, [string]$ip, [string]$url, [int]$status, [string]$agente) {
    $momento = $agora.AddSeconds($segundos)
    $linhas.Add(('{0:yyyy-MM-dd HH:mm:ss} 10.0.0.5 GET {1} - 443 - {2} {3} - {4} 0 0 12' -f $momento, $url, $ip, $agente, $status))
}

if ($Cenario -in 'todos', 'varredura') {
    # 40 paginas inexistentes em 40 segundos: dispara "Excesso de 404" (30 em 60s).
    1..40 | ForEach-Object { Adicionar $_ '203.0.113.7' "/admin/backup-$_.zip" 404 'Mozilla/5.0+(compatible;+Scanner/1.0)' }
}

if ($Cenario -in 'todos', 'wordpress') {
    # Caminhos tipicos de ataque: dispara "Varredura de URLs sensiveis" (3 em 5 min).
    '/wp-login.php', '/.env', '/.git/config', '/phpmyadmin/index.php' | ForEach-Object -Begin { $i = 0 } -Process {
        $i++
        Adicionar $i '198.51.100.23' $_ 404 'python-requests/2.31'
    }
}

if ($Cenario -in 'todos', 'legitimo') {
    # Trafego normal: nao deve gerar bloqueio.
    1..10 | ForEach-Object { Adicionar $_ '203.0.113.50' '/produtos' 200 'Mozilla/5.0+(Windows+NT+10.0)' }
}

[System.IO.File]::AppendAllText($arquivo, ($linhas -join "`r`n") + "`r`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Gravadas $($linhas.Count) linhas em $arquivo"
Write-Host 'Em ate 5 segundos os eventos aparecem no Monitor e os bloqueios (Simulado) na tela Bloqueios.'
