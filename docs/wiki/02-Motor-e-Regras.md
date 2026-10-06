# ⚙️ Motor e Regras de Detecção

O motor de segurança do **CRSPIPS** opera sob uma arquitetura de janelas deslizantes em memória (*Sliding Windows*), minimizando o I/O de banco de dados e executando inspeções de padrões em tempo linear com proteção total contra ataques ReDoS (*Regular Expression Denial of Service*).

---

## 1. Como Funciona a Avaliação de Regras

Cada evento extraído dos logs passa pelo seguinte funil de decisão:

1. **Checagem de Imunidade:** O IP pertence à Lista Branca, sub-rede local, interface do servidor ou administrador recente? $\to$ Se sim, **ignorado**.
2. **Threat Intelligence Reativa:** O IP pertence a uma lista externa maliciosa em modo *Reativo*? $\to$ Se sim, **bloqueio imediato na 1ª tentativa suspeita**, sem tolerância.
3. **Casamento com Regras Ativas:** O evento corresponde ao critério de alguma regra ativa?
   * O caminho casa com o padrão da regra?
   * O caminho NÃO casa com a expressão de `UrlsIgnoradas` da regra?
4. **Contador em Janela Deslizante:** O evento soma ocorrências para o par `(RegraId, IP)` na memória.
5. **Limite Atingido:** Se `Ocorrências >= LimiteOcorrencias` dentro da janela configurada, o IP é enviado para cálculo de punição progressiva e registrado para bloqueio.

---

## 2. Catálogo das 16 Regras Nativas

O CRSPIPS já é distribuído com 16 regras especializadas prontas para uso:

| Nome da Regra | Fonte | Critério | Limite Padrão | Janela | Descrição Técnica |
|---|---|---|---|---|---|
| **Excesso de 404** | Log IIS | Código de Status | 30 ocorrências | 60 seg | Identifica varreduras de diretórios e links quebrados deliberados. |
| **Varredura de URLs sensíveis** | Log IIS | Padrão de URL | 3 ocorrências | 300 seg | Busca por `.env`, `.git`, `.aws`, `.docker`, `wp-login.php`, `phpmyadmin`, `actuator`. |
| **Excesso de 401/403** | Log IIS | Código de Status | 50 ocorrências | 60 seg | Negações contínuas de acesso (desativada por padrão se usar Auth Windows). |
| **Requisições malformadas** | HTTPERR | Motivo HTTPERR | 20 ocorrências | 60 seg | Rejeições de verbos inválidos, cabeçalhos gigantes ou bad requests no HTTP.sys. |
| **Falha de login RDP / Windows** | Event Log | ID do Evento | 5 ocorrências | 300 seg | Força bruta contra RDP e SMB (Eventos 4625 e 140). |
| **Falha de login SQL Server** | Event Log | ID do Evento | 5 ocorrências | 300 seg | Tentativas consecutivas de credenciais inválidas no MSSQL (Evento 18456). |
| **Scanner de PHP/JSP** | Log IIS | Padrão de URL | 3 ocorrências | 300 seg | Procura por scripts `.php`, `.jsp`, `.cgi` em servidores IIS que não usam essas tecnologias. |
| **Injeção SQL na URL** | Log IIS | Padrão de URL | 2 ocorrências | 600 seg | Detecção de `UNION SELECT`, `xp_cmdshell`, `sleep(`, `waitfor delay`, `OR 1=1`. |
| **Path Traversal** | Log IIS | Padrão de URL | 1 ocorrência | 600 seg | Tentativas de escape de diretório (`../`, `%2e%2e`, `/etc/passwd`, `win.ini`). |
| **Execução remota / Log4Shell** | Log IIS | Padrão de URL | 1 ocorrência | 600 seg | Tentativas de RCE via JNDI (`${jndi:`), `cmd.exe`, `wget`, `curl` ou webshells. |
| **XSS na URL** | Log IIS | Padrão de URL | 2 ocorrências | 600 seg | Injeção de scripts na query string (`<script`, `javascript:`, `onerror=`). |
| **Arquivos sensíveis e backups** | Log IIS | Padrão de URL | 2 ocorrências | 600 seg | Varredura de `web.config`, `appsettings.json`, `id_rsa`, `dump.sql`, `backup.zip`. |
| **Painéis de terceiros** | Log IIS | Padrão de URL | 2 ocorrências | 600 seg | Varredura de Tomcat, Jenkins, Solr, Fortinet, GlobalProtect e Pulse VPN. |
| **Varredura de Exchange/OWA** | Log IIS | Padrão de URL | 3 ocorrências | 600 seg | Tentativas contra vulnerabilidades ProxyShell, `/owa/` e `/ecp/`. |
| **Falha de login RDP (lenta)** | Event Log | ID do Evento | 10 ocorrências | 3600 seg | Força bruta dispersa ao longo de 1 hora para burlar a regra rápida. |
| **Host inexistente (HTTPERR)** | HTTPERR | Motivo HTTPERR | 50 ocorrências | 300 seg | Scanners que atacam o servidor direto pelo IP ou por domínios não hospedados no IIS. |

---

## 3. URLs Ignoradas (Exceções de Negócio)

Se uma aplicação legítima devolve códigos de erro como parte da sua regra de negócio (ex.: uma API REST que retorna `404 Not Found` quando um registro não é encontrado no banco), utilize o campo **URLs Ignoradas** da regra.

A expressão é testada tanto contra a **URL bruta** quanto contra o par **"MÉTODO URL"**:

* Ignorar uma rota específica para qualquer método:
  ```regex
  ^/api/consultas/clientes
  ```
* Ignorar apenas chamadas via `POST`:
  ```regex
  ^POST /api/consultas/clientes
  ```
* Ignorar múltiplos endpoints legítimos:
  ```regex
  ^/api/v1/status|^/favicon\.ico|^/site\.webmanifest
  ```

---

## 4. Política de Progressão Punitiva

O CRSPIPS utiliza um modelo de escalonamento para desestimular ataques reincidentes.

* **Padrão:** `1h, 24h, 7d, 30d` dentro de uma janela de reincidência de **30 dias**.
* **Como funciona:**
  * **1ª infração:** Bloqueado por 1 hora.
  * **2ª infração (se voltar a atacar após expirar):** Bloqueado por 24 horas.
  * **3ª infração:** Bloqueado por 7 dias.
  * **4ª infração:** Bloqueado por 30 dias.
* Se o invasor passar 30 dias sem gerar novos ataques após o fim do bloqueio, o nível de reincidência é resetado.
