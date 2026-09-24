namespace CRSP.IPS.Application.Motor;

/// <summary>
/// Conta ocorrencias por (regra, IP) dentro de uma janela deslizante, usando o horario do proprio evento.
/// Mantido em memoria no Worker: gravar cada requisicao no banco nao escala.
/// </summary>
public sealed class ContadorJanelaDeslizante
{
    private readonly Dictionary<(int RegraId, string Ip), Queue<DateTime>> _ocorrencias = new();
    private readonly Lock _trava = new();
    private DateTime _ultimaLimpeza = DateTime.MinValue;

    public int Registrar(int regraId, string ip, DateTime momentoUtc, TimeSpan janela)
    {
        lock (_trava)
        {
            var chave = (regraId, ip);
            if (!_ocorrencias.TryGetValue(chave, out var fila))
            {
                fila = new Queue<DateTime>();
                _ocorrencias[chave] = fila;
            }

            fila.Enqueue(momentoUtc);
            var limite = momentoUtc - janela;
            while (fila.Count > 0 && fila.Peek() < limite)
                fila.Dequeue();

            LimparSeNecessario(momentoUtc);
            return fila.Count;
        }
    }

    public void Zerar(int regraId, string ip)
    {
        lock (_trava)
            _ocorrencias.Remove((regraId, ip));
    }

    public int QuantidadeChaves
    {
        get
        {
            lock (_trava)
                return _ocorrencias.Count;
        }
    }

    /// <summary>Remove IPs sem ocorrencias recentes para limitar o uso de memoria. A maior janela aceita e de 24h.</summary>
    private void LimparSeNecessario(DateTime referenciaUtc)
    {
        if (referenciaUtc - _ultimaLimpeza < TimeSpan.FromMinutes(5))
            return;

        _ultimaLimpeza = referenciaUtc;
        var limite = referenciaUtc.AddHours(-24);
        foreach (var (chave, fila) in _ocorrencias.ToList())
        {
            while (fila.Count > 0 && fila.Peek() < limite)
                fila.Dequeue();
            if (fila.Count == 0)
                _ocorrencias.Remove(chave);
        }
    }
}

/// <summary>Limita as amostras gravadas no banco a N eventos por (regra, IP) por hora.</summary>
public sealed class LimitadorAmostras
{
    public const int MaximoPorHora = 20;

    private readonly Dictionary<(int? RegraId, string Ip, DateTime Hora), int> _contagem = new();
    private readonly Lock _trava = new();

    public bool DeveRegistrar(int? regraId, string ip, DateTime momentoUtc)
    {
        var hora = new DateTime(momentoUtc.Year, momentoUtc.Month, momentoUtc.Day, momentoUtc.Hour, 0, 0, DateTimeKind.Utc);
        lock (_trava)
        {
            if (_contagem.Count > 50_000)
            {
                foreach (var chave in _contagem.Keys.Where(k => k.Hora < hora).ToList())
                    _contagem.Remove(chave);
            }

            var chaveAtual = (regraId, ip, hora);
            var atual = _contagem.GetValueOrDefault(chaveAtual);
            if (atual >= MaximoPorHora)
                return false;
            _contagem[chaveAtual] = atual + 1;
            return true;
        }
    }
}
