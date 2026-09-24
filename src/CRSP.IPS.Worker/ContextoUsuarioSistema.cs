using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Motor;

namespace CRSP.IPS.Worker;

internal sealed class ContextoUsuarioSistema : IContextoUsuario
{
    public string Nome => ServicoDeteccao.Sistema;
    public string? Ip => null;
}
