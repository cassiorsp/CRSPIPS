using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.AspNetCore.Identity;

namespace CRSP.IPS.Infrastructure.Seguranca;

/// <summary>PBKDF2 com salt (PasswordHasher do ASP.NET Core Identity), com rehash transparente em versoes futuras.</summary>
internal sealed class HashSenhaIdentity : IHashSenha
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    public string Gerar(string senha) => _hasher.HashPassword(null!, senha);

    public bool Verificar(string hash, string senha) =>
        _hasher.VerifyHashedPassword(null!, hash, senha) != PasswordVerificationResult.Failed;
}

/// <summary>
/// DPAPI com escopo de maquina: o painel (identidade do app pool) protege e o Worker (LocalSystem) le,
/// ambos no mesmo servidor. Copiar o banco para outra maquina nao expoe a chave.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class ProtetorSegredosDpapi : IProtetorSegredos
{
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("CRSPIPS.Segredos.v1");

    public string Proteger(string valor) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(valor), Entropia, DataProtectionScope.LocalMachine));

    public string? Desproteger(string valorProtegido)
    {
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(valorProtegido), Entropia, DataProtectionScope.LocalMachine));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}

/// <summary>IPs configurados nas interfaces de rede do servidor. Cache de 5 minutos.</summary>
internal sealed class ProvedorEnderecosLocais : IProvedorEnderecosLocais
{
    private readonly Lock _trava = new();
    private IReadOnlyList<FaixaIp> _cache = [];
    private DateTime _atualizadoEm = DateTime.MinValue;

    public IReadOnlyList<FaixaIp> ObterEnderecosDoServidor()
    {
        lock (_trava)
        {
            if (DateTime.UtcNow - _atualizadoEm < TimeSpan.FromMinutes(5))
                return _cache;

            _cache = NetworkInterface.GetAllNetworkInterfaces()
                .Where(i => i.OperationalStatus == OperationalStatus.Up)
                .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                .Select(u => FaixaIp.DeEndereco(EnderecoIp.Criar(u.Address)))
                .Distinct()
                .ToList();
            _atualizadoEm = DateTime.UtcNow;
            return _cache;
        }
    }
}
