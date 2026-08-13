using System.Net;

namespace SiteGuardian.Api.Tests.Fakes;

/// <summary>Handler HTTP de test : répond selon une fonction fournie, sans réseau réel.</summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    /// <summary>Associe chaque URL (préfixe) à une réponse JSON avec le code de statut donné.</summary>
    public static FakeHttpMessageHandler MapJson(Dictionary<string, (HttpStatusCode Status, string Json)> routes) =>
        new(request =>
        {
            var url = request.RequestUri!.ToString();
            var match = routes.FirstOrDefault(r => url.StartsWith(r.Key, StringComparison.OrdinalIgnoreCase));
            if (match.Key is null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            return new HttpResponseMessage(match.Value.Status)
            {
                Content = new StringContent(match.Value.Json, System.Text.Encoding.UTF8, "application/json"),
            };
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }

    public HttpClient ToHttpClient() => new(this);
}
