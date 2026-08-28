using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Infrastructure.Services;

/// <summary>
/// Retriever minimo pra buscar um JWKS cru (so o array de chaves, sem documento de discovery OIDC completo).
/// A biblioteca Microsoft.IdentityModel.Protocols nao tem um pronto pra esse caso - so pra
/// OpenIdConnectConfiguration inteiro (via OpenIdConnectConfigurationRetriever), entao implemento
/// a minima aqui: baixo o JSON com o IDocumentRetriever padrao (HttpDocumentRetriever) e desserializo
/// direto como JsonWebKeySet.
/// </summary>
public class JsonWebKeySetRetriever : IConfigurationRetriever<JsonWebKeySet>
{
    public async Task<JsonWebKeySet> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
    {
        var document = await retriever.GetDocumentAsync(address, cancel).ConfigureAwait(false);
        return new JsonWebKeySet(document);
    }
}
