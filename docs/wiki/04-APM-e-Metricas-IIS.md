# 📊 APM e Métricas de Desempenho do IIS

Além da segurança perimetral, o CRSPIPS funciona como uma ferramenta de **APM leve (Application Performance Monitoring)**, fornecendo visibilidade profunda sobre a saúde das aplicações hospedadas no IIS sem a necessidade de agentes pesados.

---

## 1. Observabilidade de Endpoints

Na tela **Endpoints**, o sistema analisa o volume de requisições, erros e tempos de resposta agregados por site e rota:

### Normalização Inteligente de Rotas
Para evitar a fragmentação das métricas com URLs dinâmicas, o motor aplica regras de normalização:
* **Remoção de Query Strings:** `/produtos?id=99&filtro=1` vira `/produtos`.
* **Substituição de Parâmetros Dinâmicos:** IDs numéricos, GUIDs e tokens hexadecimais longos são agrupados sob a máscara `{id}`:
  * `/api/pedidos/10294/itens` $\to$ `/api/pedidos/{id}/itens`
  * `/cliente/550e8400-e29b-41d4-a716-446655440000` $\to$ `/cliente/{id}`
* **Agrupamento de Arquivos Estáticos:** Extensões conhecidas (`.css`, `.js`, `.png`, `.jpg`, `.woff2`, `.svg`, etc.) são consolidadas no rótulo especial `(arquivos estáticos)`.
* **Blindagem do Banco de Dados:** É mantido um teto de no máximo 500 endpoints distintos por site a cada hora. Requisições excedentes (como as geradas por scanners aleatórios) caem no grupo `(outros)`.

### Métricas Coletadas por Endpoint:
* **Total de Requisições:** Volume global trafegado.
* **Sem Erro:** Requisições com status HTTP 1xx, 2xx e 3xx.
* **Erros de Cliente (4xx):** Requisições com status 400 a 499.
* **Erros de Servidor (5xx):** Falhas internas com status 500 a 599.
* **Taxa de Erro (%):** Porcentagem de requisições com falha.
* **Tempo Médio e Máximo de Resposta:** Calculado em milissegundos a partir do campo `time-taken` do log W3C.

---

## 2. Monitoramento de Application Pools (`w3wp.exe`)

A tela **IIS** correlaciona o consumo de infraestrutura com os sites hospedados:

* **Amostragem WMI a cada 30 segundos:** O Worker consulta instâncias em execução do processo de trabalho `w3wp.exe`, extraindo:
  * Memória Privada (Working Set / Private Bytes).
  * Consumo percentual de CPU.
* **Mapeamento Automático Site $\to$ Pool:** O Worker lê o arquivo `applicationHost.config` do IIS e estabelece a relação entre o site e seu respectivo Application Pool.
* **Consolidação em Janelas de 5 Minutos:** As métricas são gravadas agregadas por pool com valores mínimo, médio e máximo, evitando sobrecarga de escrita em disco.

> [!NOTE]
> Um pool só passa a registrar métricas após receber sua primeira requisição HTTP, pois o IIS inicializa os processos de trabalho sob demanda.

---

## 3. Exportação de Dados para CSV

Tabelas de **Endpoints**, **Sites do IIS** e **Bloqueios** contam com o botão **Exportar CSV**:

* **Compatibilidade com Excel:** Exportado em UTF-8 com BOM e separador de ponto e vírgula (`;`), abrindo com acentos perfeitos no Excel em português.
* **Respeito aos Filtros:** O arquivo CSV exporta exatamente as linhas filtradas na tela, na mesma ordem de classificação visual.
* **Proteção contra CSV Formula Injection:** Textos iniciados por `=`, `+`, `-` ou `@` são automaticamente prefixados com apóstrofo (`'`) para impedir execução arbitrária de fórmulas no leitor de planilhas.
