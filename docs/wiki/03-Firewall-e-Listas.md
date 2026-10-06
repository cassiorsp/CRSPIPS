# 🧱 Firewall e Gestão de Listas de Bloqueio

A integração com o **Windows Defender Firewall** é o coração da camada de mitigação do CRSPIPS. Ela foi projetada para combinar alta velocidade, proteção da pilha de rede do Windows e flexibilidade com feeds de Threat Intelligence.

---

## 1. Arquitetura COM e Agrupamento de IPs

O Windows Defender Firewall sofre lentidão severa caso sejam injetadas dezenas de milhares de regras individuais. O CRSPIPS resolve esse problema agrupando **até 1.000 endereços por regra de entrada**:

```
Windows Firewall (Grupo: CRSPIPS)
 ├── CRSPIPS_LOGIIS_00001        (Até 1.000 IPs detectados nos logs do IIS)
 ├── CRSPIPS_LOGIIS_00002        (Próximos 1.000 IPs)
 ├── CRSPIPS_HTTPERR_00001       (Até 1.000 IPs com erros no HTTP.sys)
 ├── CRSPIPS_EVENTOWINDOWS_00001 (Até 1.000 IPs com falha de login RDP/SQL)
 ├── CRSPIPS_MANUAL_00001        (Bloqueios manuais de administradores)
 ├── CRSPIPS_LISTANEGRA_00001    (Lista negra permanente)
 ├── CRSPIPS_LISTAEXTERNA_00001  (Feeds públicos em modo Ativo)
 └── CRSPIPS_PAIS_...            (Regras de bloqueio ou permissão geográfica)
```

### Ciclo de Reconciliação e Auto-Cura
* **A cada 2 segundos:** O Worker verifica bloqueios novos, modificados ou expirados no SQLite e atualiza as regras correspondentes no Firewall.
* **A cada 5 minutos:** Uma auditoria integral varre o Windows Defender Firewall. Se algum administrador ou processo externo tiver apagado ou alterado uma regra do CRSPIPS manualmente pelo `wf.msc`, o sistema **recria e restaura a regra automaticamente**.

---

## 2. Listas Negras e Brancas

### Lista Branca (Imunidade Total)
Endereços cadastrados na lista branca **nunca** são bloqueados pelo sistema, mesmo que constem em feeds externos ou ultrapassem limites de regras:
* Redes internas privadas (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `127.0.0.1`) já possuem imunidade nativa.
* Cadastre o IP fixo da sede da empresa, links de VPN e servidores de monitoramento externo (ex: Pingdom, UptimeRobot).

### Lista Negra Manual
Permite barrar IPs ou sub-redes inteiras de forma permanente.
* Aceita IP único: `203.0.113.7`
* Aceita formato CIDR: `198.51.100.0/24`
* Aceita intervalo com hífen: `192.0.2.10-192.0.2.50`
* Aceita IPv6: `2001:db8::/32`

### Importação e Exportação por CSV
Tanto a Lista Branca quanto a Lista Negra possuem suporte à importação em lote via CSV:
```csv
faixa;descricao
203.0.113.7;IP suspeito reportado pelo SOC
198.51.100.0/24;Sub-rede de botnet
192.0.2.10-192.0.2.50;Range de scanners
```

---

## 3. Listas Externas (Threat Intelligence Pública)

O CRSPIPS inclui feeds globais de reputação de IPs que são baixados e sincronizados a cada 24 horas:

| Lista Externa | Conteúdo | Modo Padrão | Recomendação |
|---|---|---|---|
| **Spamhaus DROP** | Redes sequestradas operadas por criminosos (~1.500 faixas). | Ativa | Manter **Ativa** |
| **DShield Top 20** | As 20 redes que mais atacam no mundo segundo o SANS Institute. | Ativa | Manter **Ativa** |
| **IPsum nível 3+** | Endereços presentes em 3 ou mais listas de ataques. | Desativada | **Reativa** |
| **CINS Army** | IPs com péssima reputação em sensores da Sentinel IPS. | Desativada | **Reativa** |
| **blocklist.de** | IPs reportados nas últimas 48 horas contra SSH, mail e web. | Desativada | **Reativa** |

### Os 4 Modos Operacionais de Listas:
1. **Desativada:** O feed não é baixado nem processado.
2. **Avaliação:** As faixas são baixadas e o sistema registra quando um IP da lista acessa o servidor (exibido na tela de *Coincidências*), mas **não realiza bloqueios**. Ideal para testar se uma lista bloqueia clientes legítimos.
3. **Reativa:** Não adiciona os IPs diretamente no firewall (evitando sobrecarregá-lo com 50.000 regras). Se algum IP da lista tentar qualquer ação suspeita (404, `.env`, login errado), ele é **bloqueado no primeiro evento**, sem tolerância.
4. **Ativa:** Todas as faixas são inseridas diretamente no firewall antes de qualquer tráfego chegar ao servidor.

---

## 4. Política Geográfica de Países (GeoIP)

Utilizando a base de dados MaxMind GeoLite2, o CRSPIPS permite aplicar políticas geográficas:

* **Modo Recomendado para RDP:**
  * **Modo:** Permitir somente países listados
  * **Países:** Brasil
  * **Aplicação:** Firewall por portas (`3389`)
  * *Resultado:* Conexões de RDP vindas de fora do Brasil são descartadas na camada de rede antes de qualquer tela de login.
* **Modo para Servidores Web (IIS):**
  * Utilize **Reativa (somente eventos suspeitos)** para sites internacionais: visitantes legítimos de outros países navegam normalmente, mas qualquer ação maliciosa gera banimento imediato.
