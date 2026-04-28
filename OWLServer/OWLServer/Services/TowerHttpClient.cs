using System.Net.Http.Headers;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services;

public class TowerHttpClient : ITowerHttpClient
{
    private readonly HttpClient _client;

    public TowerHttpClient(Uri baseAddress)
    {
        _client = new HttpClient { BaseAddress = baseAddress };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content)
        => _client.PostAsync(requestUri, content);

    public Task<HttpResponseMessage> PingAsync(string? requestUri)
        => _client.PostAsync(requestUri, null);
}
