using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.ObjetosValor;
using MaxMind.Db;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Donc.IPS.Infrastructure.Geolocalizacao;

/// <summary>
/// Geolocalizacao offline com MaxMind GeoLite2 (City + ASN). Sem chamadas externas, sem limite de consultas.
/// Recarrega automaticamente quando o arquivo e atualizado. Se a base nao existir, retorna null.
/// </summary>
internal sealed class ServicoGeolocalizacaoMaxMind(IOptions<OpcoesCrspips> opcoes, ILogger<ServicoGeolocalizacaoMaxMind> logger)
    : IServicoGeolocalizacao, IDisposable
{
    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromMinutes(2);

    private readonly Lock _trava = new();
    private readonly string _pasta = opcoes.Value.PastaGeoIp;
    private DatabaseReader? _leitorCidade;
    private DatabaseReader? _leitorAsn;
    private DateTime _versaoCidade;
    private DateTime _versaoAsn;
    private DateTime _ultimaVerificacao = DateTime.MinValue;

    public LocalizacaoIp? Localizar(EnderecoIp endereco)
    {
        if (endereco.Valor is null)
            return null;

        GarantirCarregado();
        var (leitorCidade, leitorAsn) = (_leitorCidade, _leitorAsn);
        if (leitorCidade is null && leitorAsn is null)
            return null;

        string? paisCodigo = null, paisNome = null, cidade = null, organizacao = null;
        int? asn = null;

        try
        {
            if (leitorCidade?.TryCity(endereco.Valor, out var respostaCidade) == true && respostaCidade is not null)
            {
                paisCodigo = respostaCidade.Country.IsoCode ?? respostaCidade.RegisteredCountry.IsoCode;
                paisNome = respostaCidade.Country.Name ?? respostaCidade.RegisteredCountry.Name;
                cidade = respostaCidade.City.Name;
            }

            if (leitorAsn?.TryAsn(endereco.Valor, out var respostaAsn) == true && respostaAsn is not null)
            {
                asn = respostaAsn.AutonomousSystemNumber is { } numero ? (int)numero : null;
                organizacao = respostaAsn.AutonomousSystemOrganization;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao geolocalizar {Ip}", endereco);
            return null;
        }

        return paisCodigo is null && asn is null ? null : new LocalizacaoIp(paisCodigo, paisNome, cidade, asn, organizacao);
    }

    public StatusBaseGeo ObterStatus()
    {
        var cidade = LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoCidade);
        var asn = LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoAsn);
        var paises = LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoBlocosIPv4);
        return new StatusBaseGeo(
            cidade?.FullName ?? Path.Combine(_pasta, LocalizadorArquivosGeo.ArquivoCidade),
            cidade is not null,
            cidade?.LastWriteTimeUtc,
            asn?.FullName ?? Path.Combine(_pasta, LocalizadorArquivosGeo.ArquivoAsn),
            asn is not null,
            paises?.DirectoryName ?? Path.Combine(_pasta, LocalizadorArquivosGeo.ArquivoBlocosIPv4),
            paises is not null);
    }

    private void GarantirCarregado()
    {
        if (DateTime.UtcNow - _ultimaVerificacao < IntervaloVerificacao)
            return;

        lock (_trava)
        {
            if (DateTime.UtcNow - _ultimaVerificacao < IntervaloVerificacao)
                return;
            _ultimaVerificacao = DateTime.UtcNow;

            _leitorCidade = Recarregar(LocalizadorArquivosGeo.ArquivoCidade, _leitorCidade, ref _versaoCidade);
            _leitorAsn = Recarregar(LocalizadorArquivosGeo.ArquivoAsn, _leitorAsn, ref _versaoAsn);
        }
    }

    private DatabaseReader? Recarregar(string nomeArquivo, DatabaseReader? atual, ref DateTime versaoAtual)
    {
        var arquivo = LocalizadorArquivosGeo.Encontrar(_pasta, nomeArquivo);
        if (arquivo is null)
        {
            if (atual is null)
                logger.LogWarning("Base de geolocalizacao {Arquivo} nao encontrada em {Pasta}", nomeArquivo, _pasta);
            return atual;
        }

        if (atual is not null && arquivo.LastWriteTimeUtc == versaoAtual)
            return atual;

        try
        {
            // Em memoria: nao mantem o arquivo aberto, permitindo que o Worker substitua a base com o painel rodando.
            var novo = new DatabaseReader(arquivo.FullName, FileAccessMode.Memory);
            atual?.Dispose();
            versaoAtual = arquivo.LastWriteTimeUtc;
            logger.LogInformation("Base de geolocalizacao carregada: {Arquivo}", arquivo.FullName);
            return novo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao carregar a base de geolocalizacao {Arquivo}", arquivo.FullName);
            return atual;
        }
    }

    public void Dispose()
    {
        _leitorCidade?.Dispose();
        _leitorAsn?.Dispose();
    }
}
