using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Infrastructure.Firewall;

/// <summary>
/// Windows Defender Firewall via API COM (HNetCfg.FwPolicy2). Mais rapido e confiavel que orquestrar PowerShell.
/// Os IPs sao agrupados em poucas regras (ate 1000 enderecos por regra), padrao usado pelo IPBan:
/// milhares de regras individuais degradam o desempenho do firewall.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class FirewallWindows(ILogger<FirewallWindows> logger) : IServicoFirewall
{
    internal const string Grupo = "CRSPIPS";
    internal const string PrefixoBloqueio = "CRSPIPS_Bloqueio_";
    internal const string PrefixoListasExternas = "CRSPIPS_ListaExterna_";
    internal const string PrefixoPaises = "CRSPIPS_Pais_";
    private const int EnderecosPorRegra = 1000;

    private const int DirecaoEntrada = 1;
    private const int AcaoBloquear = 0;
    private const int AcaoPermitir = 1;
    private const int ProtocoloQualquer = 256;
    private const int ProtocoloTcp = 6;
    private const int ProtocoloUdp = 17;
    private const int TodosPerfis = 0x7FFFFFFF;

    private readonly Lock _trava = new();

    private static (string Prefixo, string Descricao) Identificar(ConjuntoRegrasFirewall conjunto) => conjunto switch
    {
        ConjuntoRegrasFirewall.ListasExternas => (PrefixoListasExternas, "Listas externas do CRSPIPS (Spamhaus, DShield...)"),
        _ => (PrefixoBloqueio, "Bloqueios do CRSPIPS")
    };

    public IReadOnlyList<FaixaIp> LerEnderecos(ConjuntoRegrasFirewall conjunto)
    {
        lock (_trava)
        {
            var politica = CriarPolitica();
            var faixas = new List<FaixaIp>();
            foreach (var regra in ListarRegras(politica, Identificar(conjunto).Prefixo))
                faixas.AddRange(ConversorEnderecosFirewall.Converter((string?)regra.RemoteAddresses));
            return faixas;
        }
    }

    public int SubstituirEnderecos(ConjuntoRegrasFirewall conjunto, IReadOnlyList<FaixaIp> enderecos)
    {
        var (prefixo, descricao) = Identificar(conjunto);
        lock (_trava)
        {
            var politica = CriarPolitica();
            var blocos = enderecos.Chunk(EnderecosPorRegra).ToList();
            var existentes = new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);
            foreach (var regra in (List<dynamic>)ListarRegras(politica, prefixo))
                existentes[(string)regra.Name] = regra;

            for (var indice = 0; indice < blocos.Count; indice++)
            {
                var nome = $"{prefixo}{indice + 1:D3}";
                var remotos = string.Join(',', blocos[indice].Select(f => f.ParaTextoFirewall()));

                if (existentes.Remove(nome, out dynamic? regraExistente) && regraExistente is not null)
                {
                    regraExistente.RemoteAddresses = remotos;
                    regraExistente.Enabled = true;
                    continue;
                }

                politica.Rules.Add(CriarRegra(nome, descricao, AcaoBloquear, ProtocoloQualquer, portas: null, remotos));
            }

            foreach (var sobra in existentes.Keys)
                politica.Rules.Remove(sobra);

            return blocos.Count;
        }
    }

    public void AplicarRestricaoPaises(PlanoRestricaoPaises plano)
    {
        lock (_trava)
        {
            var politica = CriarPolitica();
            RemoverRegras(politica, PrefixoPaises);

            var permitir = plano.Modo == ModoPoliticaPaises.PermitirSomenteListados;
            var enderecos = plano.FaixasPaises.Select(f => f.ParaTextoFirewall()).ToList();
            if (permitir)
            {
                enderecos.AddRange(plano.FaixasSempreLiberadas.Select(f => f.ParaTextoFirewall()));
                enderecos.Insert(0, "LocalSubnet");
            }

            var portas = plano.Portas.Count > 0 ? string.Join(',', plano.Portas) : null;
            var acao = permitir ? AcaoPermitir : AcaoBloquear;
            var rotulo = permitir ? "Permitir" : "Bloquear";
            (int Protocolo, string Nome)[] protocolos = portas is null
                ? [(ProtocoloQualquer, "Todos")]
                : [(ProtocoloTcp, "TCP"), (ProtocoloUdp, "UDP")];

            foreach (var (protocolo, nomeProtocolo) in protocolos)
            {
                var indice = 0;
                foreach (var bloco in enderecos.Chunk(EnderecosPorRegra))
                {
                    indice++;
                    var nome = $"{PrefixoPaises}{rotulo}_{nomeProtocolo}_{indice:D3}";
                    politica.Rules.Add(CriarRegra(nome, "Política de países do CRSPIPS", acao, protocolo, portas, string.Join(',', bloco)));
                }
            }

            logger.LogInformation(
                "Regras de pais criadas: modo {Modo}, portas {Portas}, {Enderecos} entradas",
                plano.Modo, portas ?? "todas", enderecos.Count);
        }
    }

    public void RemoverRestricaoPaises()
    {
        lock (_trava)
            RemoverRegras(CriarPolitica(), PrefixoPaises);
    }

    public IReadOnlyList<string> ListarRegrasPermissivasNasPortas(IReadOnlyList<int> portas)
    {
        lock (_trava)
        {
            var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (dynamic regra in CriarPolitica().Rules)
            {
                string nome = regra.Name ?? string.Empty;
                if (nome.StartsWith(Grupo, StringComparison.OrdinalIgnoreCase))
                    continue;
                if ((int)regra.Direction != DirecaoEntrada || (int)regra.Action != AcaoPermitir || !(bool)regra.Enabled)
                    continue;
                if ((string?)regra.RemoteAddresses is not "*")
                    continue;
                if (ConversorEnderecosFirewall.PortasContemAlguma((string?)regra.LocalPorts, portas))
                    nomes.Add(nome);
            }

            return nomes.ToList();
        }
    }

    public void DefinirRegraHabilitada(string nome, bool habilitada)
    {
        lock (_trava)
        {
            foreach (dynamic regra in CriarPolitica().Rules)
            {
                if (string.Equals((string?)regra.Name, nome, StringComparison.OrdinalIgnoreCase))
                    regra.Enabled = habilitada;
            }
        }
    }

    private static dynamic CriarPolitica()
    {
        var tipo = Type.GetTypeFromProgID("HNetCfg.FwPolicy2", throwOnError: true)!;
        return Activator.CreateInstance(tipo)!;
    }

    private static dynamic CriarRegra(string nome, string descricao, int acao, int protocolo, string? portas, string enderecosRemotos)
    {
        var tipo = Type.GetTypeFromProgID("HNetCfg.FWRule", throwOnError: true)!;
        dynamic regra = Activator.CreateInstance(tipo)!;
        regra.Name = nome;
        regra.Description = descricao;
        regra.Grouping = Grupo;
        regra.Direction = DirecaoEntrada;
        regra.Action = acao;
        regra.Protocol = protocolo;
        if (portas is not null)
            regra.LocalPorts = portas;
        regra.RemoteAddresses = enderecosRemotos;
        regra.Profiles = TodosPerfis;
        regra.InterfaceTypes = "All";
        regra.Enabled = true;
        return regra;
    }

    private static List<dynamic> ListarRegras(dynamic politica, string prefixo)
    {
        var regras = new List<dynamic>();
        foreach (dynamic regra in politica.Rules)
        {
            string? nome = regra.Name;
            if (nome is not null && nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                regras.Add(regra);
        }

        return regras;
    }

    private static void RemoverRegras(dynamic politica, string prefixo)
    {
        foreach (var regra in ListarRegras(politica, prefixo))
        {
            string nome = regra.Name;
            politica.Rules.Remove(nome);
            if (Marshal.IsComObject(regra))
                Marshal.ReleaseComObject(regra);
        }
    }
}
