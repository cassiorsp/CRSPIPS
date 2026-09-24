using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Application.Abstracoes;

/// <summary>
/// Acesso ao Windows Firewall. Registrado SOMENTE no Worker (que roda com privilegio de administrador).
/// O painel web nunca altera o firewall diretamente: ele altera o estado no banco e o Worker reconcilia.
/// </summary>
public interface IServicoFirewall
{
    IReadOnlyList<FaixaIp> LerEnderecos(ConjuntoRegrasFirewall conjunto);

    /// <summary>Substitui os enderecos bloqueados do conjunto. Retorna a quantidade de regras usadas.</summary>
    int SubstituirEnderecos(ConjuntoRegrasFirewall conjunto, IReadOnlyList<FaixaIp> enderecos);

    void AplicarRestricaoPaises(PlanoRestricaoPaises plano);

    void RemoverRestricaoPaises();

    /// <summary>Regras de entrada de terceiros, habilitadas, que permitem trafego em alguma das portas informadas.</summary>
    IReadOnlyList<string> ListarRegrasPermissivasNasPortas(IReadOnlyList<int> portas);

    void DefinirRegraHabilitada(string nome, bool habilitada);
}

public interface IServicoGeolocalizacao
{
    LocalizacaoIp? Localizar(EnderecoIp endereco);
    StatusBaseGeo ObterStatus();
}

/// <summary>Faixas de IP por pais (base GeoLite2 Country em CSV).</summary>
public interface IProvedorFaixasPais
{
    bool EstaDisponivel { get; }
    IReadOnlyList<FaixaIp> ObterFaixas(IReadOnlySet<string> paises);
}

public interface IFonteEventos
{
    TipoFonte Fonte { get; }
    bool EstaHabilitada(Configuracao configuracao);
    Task<IReadOnlyList<EventoDetectado>> LerNovosEventosAsync(Configuracao configuracao, CancellationToken ct);
}

/// <summary>Baixa e instala as bases GeoLite2 (City, ASN e Country CSV). Usado somente pelo Worker.</summary>
public interface IAtualizadorBaseGeo
{
    /// <param name="forcar">True baixa todas as bases; false baixa somente as ausentes ou com mais de 3 dias.</param>
    Task<ResultadoAtualizacaoGeo> AtualizarAsync(string contaId, string chaveLicenca, bool forcar, CancellationToken ct);
}

/// <summary>Baixa o conteudo das URLs de uma lista externa. Usado somente pelo Worker.</summary>
public interface IBaixadorListasExternas
{
    /// <summary>Retorna o texto de cada URL. Lanca excecao em qualquer falha (a lista anterior e mantida).</summary>
    Task<IReadOnlyList<string>> BaixarAsync(IReadOnlyList<string> urls, CancellationToken ct);
}

/// <summary>Protege segredos gravados no banco (ex.: chave de licenca MaxMind).</summary>
public interface IProtetorSegredos
{
    string Proteger(string valor);
    string? Desproteger(string valorProtegido);
}

public interface IHashSenha
{
    string Gerar(string senha);
    bool Verificar(string hash, string senha);
}

/// <summary>Quem esta executando a acao (usuario logado no painel ou o proprio Worker).</summary>
public interface IContextoUsuario
{
    string Nome { get; }
    string? Ip { get; }
}

public interface IProvedorEnderecosLocais
{
    IReadOnlyList<FaixaIp> ObterEnderecosDoServidor();
}
