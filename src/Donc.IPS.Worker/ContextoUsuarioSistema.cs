using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Motor;

namespace Donc.IPS.Worker;

internal sealed class ContextoUsuarioSistema : IContextoUsuario
{
    public string Nome => ServicoDeteccao.Sistema;
    public string? Ip => null;
}
