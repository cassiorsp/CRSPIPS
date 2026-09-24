using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Application.Servicos;

/// <summary>
/// Monta o conjunto de enderecos que nunca podem ser bloqueados: lista branca, IPs do proprio servidor
/// e IPs usados pelos administradores no painel nos ultimos dias (evita o administrador se trancar para fora).
/// </summary>
public sealed class ServicoProtecao(
    IRepositorioListas listas,
    IRepositorioUsuarios usuarios,
    IProvedorEnderecosLocais enderecosLocais,
    TimeProvider relogio)
{
    private static readonly TimeSpan JanelaIpsAdministradores = TimeSpan.FromDays(7);

    public async Task<ConjuntoProtecao> ObterAsync(CancellationToken ct = default)
    {
        var branca = (await listas.ListarAsync(TipoLista.Branca, ct)).Select(e => e.ObterFaixa());

        var desde = relogio.GetUtcNow().UtcDateTime - JanelaIpsAdministradores;
        var administradores = (await usuarios.ListarIpsDeAcessoDesdeAsync(desde, ct))
            .Select(ip => EnderecoIp.TentarConverter(ip, out var endereco) ? FaixaIp.DeEndereco(endereco) : null)
            .OfType<FaixaIp>();

        return new ConjuntoProtecao(
            new ConjuntoFaixas(branca),
            new ConjuntoFaixas(enderecosLocais.ObterEnderecosDoServidor()),
            new ConjuntoFaixas(administradores));
    }
}

public sealed class ConjuntoProtecao(ConjuntoFaixas listaBranca, ConjuntoFaixas servidor, ConjuntoFaixas administradores)
{
    public bool Contem(EnderecoIp ip) => ObterMotivo(ip) is not null;

    /// <summary>Texto em pt-BR (chave de traducao) do motivo da protecao, ou null.</summary>
    public string? ObterMotivo(EnderecoIp ip)
    {
        if (listaBranca.Contem(ip))
            return "Lista branca";
        if (servidor.Contem(ip))
            return "Endereço do próprio servidor";
        if (administradores.Contem(ip))
            return "IP recente de administrador do painel";
        return null;
    }

    public bool Sobrepoe(FaixaIp faixa) =>
        Todas.Any(f => f.Contem(faixa.Inicio) || f.Contem(faixa.Fim) || faixa.Contem(f.Inicio));

    public IReadOnlyList<FaixaIp> Todas =>
        FaixaIp.Mesclar(listaBranca.Faixas.Concat(servidor.Faixas).Concat(administradores.Faixas));
}
