namespace Donc.IPS.Domain.Politicas;

/// <summary>
/// Punicao progressiva: cada reincidencia dentro da janela sobe um nivel na lista de tempos.
/// Ao atingir o ultimo nivel, permanece nele (diferente do IPBan, que volta ao primeiro).
/// </summary>
public static class PoliticaProgressao
{
    public static (int Nivel, TimeSpan Duracao) Calcular(IReadOnlyList<TimeSpan> tempos, int bloqueiosAnterioresNaJanela)
    {
        if (tempos.Count == 0)
            throw new ArgumentException("Informe ao menos um tempo de bloqueio.", nameof(tempos));

        var nivel = Math.Clamp(bloqueiosAnterioresNaJanela + 1, 1, tempos.Count);
        return (nivel, tempos[nivel - 1]);
    }
}
