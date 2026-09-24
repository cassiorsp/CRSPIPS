using System.Text;
using CRSP.IPS.Application.Abstracoes;

namespace CRSP.IPS.Infrastructure.Fontes;

/// <summary>Baixa o texto das listas externas, somente por HTTPS e com tamanho maximo por arquivo.</summary>
internal sealed class BaixadorListasExternas(IHttpClientFactory fabricaHttp) : IBaixadorListasExternas
{
    public const string NomeClienteHttp = "ListasExternas";
    private const long TamanhoMaximoBytes = 20 * 1024 * 1024;

    public async Task<IReadOnlyList<string>> BaixarAsync(IReadOnlyList<string> urls, CancellationToken ct)
    {
        var cliente = fabricaHttp.CreateClient(NomeClienteHttp);
        var conteudos = new List<string>();
        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"URL recusada (somente HTTPS): {url}");

            using var resposta = await cliente.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            resposta.EnsureSuccessStatusCode();
            if (resposta.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"Redirecionamento para endereço não seguro recusado: {url}");
            if (resposta.Content.Headers.ContentLength > TamanhoMaximoBytes)
                throw new InvalidOperationException($"Arquivo grande demais: {url}");

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var memoria = new MemoryStream();
            var buffer = new byte[81920];
            int lidos;
            while ((lidos = await fluxo.ReadAsync(buffer, ct)) > 0)
            {
                if (memoria.Length + lidos > TamanhoMaximoBytes)
                    throw new InvalidOperationException($"Arquivo grande demais: {url}");
                memoria.Write(buffer, 0, lidos);
            }

            conteudos.Add(Encoding.UTF8.GetString(memoria.ToArray()));
        }

        return conteudos;
    }
}
