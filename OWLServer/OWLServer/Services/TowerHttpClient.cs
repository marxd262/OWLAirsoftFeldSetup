using System.Net.Http.Headers;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services;

public class TowerHttpClient : ITowerHttpClient
{
    private readonly HttpClient _client;

    public TowerHttpClient(Uri baseAddress)
    {
        // towers sit on the local WiFi: fail fast instead of piling up 100 s requests to an offline tower
        _client = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(5) };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content)
        => _client.PostAsync(requestUri, content);

    public Task<HttpResponseMessage> PingAsync(string? requestUri)
        => _client.PostAsync(requestUri, null);
}
