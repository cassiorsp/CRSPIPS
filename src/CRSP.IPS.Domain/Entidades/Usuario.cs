namespace CRSP.IPS.Domain.Entidades;

public class Usuario
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string HashSenha { get; private set; } = string.Empty;
    public bool Ativo { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? UltimoAcessoEm { get; private set; }
    public string? UltimoIp { get; private set; }
    public int TentativasFalhas { get; private set; }
    public DateTime? BloqueadoAte { get; private set; }

    protected Usuario() { }

    public static Usuario Criar(string nome, string email, string hashSenha, DateTime agoraUtc) => new()
    {
        Nome = nome.Trim(),
        Email = NormalizarEmail(email),
        HashSenha = hashSenha,
        Ativo = true,
        CriadoEm = agoraUtc
    };

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    public bool EstaTemporariamenteBloqueado(DateTime agoraUtc) => BloqueadoAte > agoraUtc;

    public void RegistrarAcesso(DateTime agoraUtc, string? ip)
    {
        UltimoAcessoEm = agoraUtc;
        UltimoIp = ip;
        TentativasFalhas = 0;
        BloqueadoAte = null;
    }

    /// <summary>Protege a tela de login contra forca bruta: 5 falhas seguidas travam a conta por 15 minutos.</summary>
    public void RegistrarFalhaLogin(DateTime agoraUtc)
    {
        TentativasFalhas++;
        if (TentativasFalhas >= 5)
        {
            BloqueadoAte = agoraUtc.AddMinutes(15);
            TentativasFalhas = 0;
        }
    }

    public void AlterarSenha(string novoHash) => HashSenha = novoHash;

    public void AlterarDados(string nome, string email)
    {
        Nome = nome.Trim();
        Email = NormalizarEmail(email);
    }

    public void DefinirAtivo(bool ativo) => Ativo = ativo;
}
