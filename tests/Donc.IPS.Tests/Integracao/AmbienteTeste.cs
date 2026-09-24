using Donc.IPS.Application;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.ObjetosValor;
using Donc.IPS.Infrastructure;
using Donc.IPS.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Donc.IPS.Tests.Integracao;

/// <summary>
/// Monta a aplicacao real (EF Core + SQLite em arquivo temporario, com WAL e migrations) trocando apenas
/// o que depende do sistema operacional: firewall, geolocalizacao, IPs locais e o relogio.
/// </summary>
internal sealed class AmbienteTeste : IAsyncDisposable
{
    private readonly string _caminhoBanco = Path.Combine(Path.GetTempPath(), $"crspips-teste-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provedor;

    public FakeTimeProvider Relogio { get; } = new(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
    public FirewallFalso Firewall { get; } = new();
    public GeolocalizacaoFalsa Geolocalizacao { get; } = new();
    public ContextoUsuarioFalso Usuario { get; } = new();
    public AtualizadorGeoFalso AtualizadorGeo { get; } = new();
    public BaixadorListasFalso BaixadorListas { get; } = new();

    private AmbienteTeste()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CRSPIPS:CaminhoBanco"] = _caminhoBanco,
                ["CRSPIPS:PastaGeoIp"] = Path.GetTempPath()
            })
            .Build();

        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AdicionarInfraestrutura(configuracao).AdicionarAplicacao().AdicionarMotor();
        servicos.AddSingleton<TimeProvider>(Relogio);
        servicos.AddSingleton<IServicoFirewall>(Firewall);
        servicos.AddSingleton<IServicoGeolocalizacao>(Geolocalizacao);
        servicos.AddSingleton<IProvedorEnderecosLocais>(new EnderecosLocaisFalsos());
        servicos.AddSingleton<IContextoUsuario>(Usuario);
        servicos.AddSingleton<IAtualizadorBaseGeo>(AtualizadorGeo);
        servicos.AddSingleton<IBaixadorListasExternas>(BaixadorListas);
        _provedor = servicos.BuildServiceProvider();
    }

    public static async Task<AmbienteTeste> CriarAsync()
    {
        var ambiente = new AmbienteTeste();
        await InicializadorBanco.InicializarAsync(ambiente._provedor);
        return ambiente;
    }

    /// <summary>Executa a acao em um escopo novo (equivalente a uma requisicao ou a um ciclo do Worker).</summary>
    public async Task<T> ExecutarAsync<T>(Func<IServiceProvider, Task<T>> acao)
    {
        await using var escopo = _provedor.CreateAsyncScope();
        return await acao(escopo.ServiceProvider);
    }

    public Task ExecutarAsync(Func<IServiceProvider, Task> acao) =>
        ExecutarAsync(async provedor => { await acao(provedor); return true; });

    public async ValueTask DisposeAsync()
    {
        await _provedor.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var arquivo in new[] { _caminhoBanco, _caminhoBanco + "-wal", _caminhoBanco + "-shm" })
        {
            if (File.Exists(arquivo))
                File.Delete(arquivo);
        }
    }
}

internal sealed class FirewallFalso : IServicoFirewall
{
    public List<FaixaIp> Bloqueados { get; } = [];
    public List<FaixaIp> ListasExternas { get; } = [];
    public int Substituicoes { get; private set; }
    public PlanoRestricaoPaises? PlanoPaises { get; private set; }
    public HashSet<string> RegrasDesabilitadas { get; } = [];
    public List<string> RegrasPermissivas { get; } = ["Remote Desktop - User Mode (TCP-In)"];

    private List<FaixaIp> Conjunto(ConjuntoRegrasFirewall conjunto) =>
        conjunto == ConjuntoRegrasFirewall.ListasExternas ? ListasExternas : Bloqueados;

    public IReadOnlyList<FaixaIp> LerEnderecos(ConjuntoRegrasFirewall conjunto) => Conjunto(conjunto).ToList();

    public int SubstituirEnderecos(ConjuntoRegrasFirewall conjunto, IReadOnlyList<FaixaIp> enderecos)
    {
        Substituicoes++;
        Conjunto(conjunto).Clear();
        Conjunto(conjunto).AddRange(enderecos);
        return enderecos.Count == 0 ? 0 : 1;
    }

    public void AplicarRestricaoPaises(PlanoRestricaoPaises plano) => PlanoPaises = plano;

    public void RemoverRestricaoPaises() => PlanoPaises = null;

    public IReadOnlyList<string> ListarRegrasPermissivasNasPortas(IReadOnlyList<int> portas) =>
        RegrasPermissivas.Where(r => !RegrasDesabilitadas.Contains(r)).ToList();

    public void DefinirRegraHabilitada(string nome, bool habilitada)
    {
        if (habilitada)
            RegrasDesabilitadas.Remove(nome);
        else
            RegrasDesabilitadas.Add(nome);
    }
}

internal sealed class GeolocalizacaoFalsa : IServicoGeolocalizacao
{
    public Dictionary<string, string> Paises { get; } = new();

    public LocalizacaoIp? Localizar(EnderecoIp endereco) =>
        Paises.TryGetValue(endereco.ToString(), out var pais) ? new LocalizacaoIp(pais, pais, null, 64500, "Provedor Teste") : null;

    public StatusBaseGeo ObterStatus() => new("", Paises.Count > 0, null, "", false, "", false);
}

internal sealed class EnderecosLocaisFalsos : IProvedorEnderecosLocais
{
    public IReadOnlyList<FaixaIp> ObterEnderecosDoServidor() => [FaixaIp.Converter("198.51.100.10")];
}

internal sealed class AtualizadorGeoFalso : IAtualizadorBaseGeo
{
    public (string ContaId, string Chave, bool Forcar)? UltimaChamada { get; private set; }

    public Task<ResultadoAtualizacaoGeo> AtualizarAsync(string contaId, string chaveLicenca, bool forcar, CancellationToken ct)
    {
        UltimaChamada = (contaId, chaveLicenca, forcar);
        return Task.FromResult(new ResultadoAtualizacaoGeo(true, 3, "Bases GeoIP atualizadas com sucesso."));
    }
}

internal sealed class ContextoUsuarioFalso : IContextoUsuario
{
    public string Nome { get; set; } = "admin@teste.local";
    public string? Ip { get; set; } = "192.0.2.50";
}
