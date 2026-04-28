using System.Net;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Helpers;

public class MockTowerHttpClient : ITowerHttpClient, IDisposable
{
    private readonly HttpClient _client;

    public List<string> PostedUrls { get; } = new();

    public MockTowerHttpClient()
    {
        var handler = new FakeHandler(this);
        _client = new HttpClient(handler) { BaseAddress = new Uri("http://mock-tower.local/") };
    }

    public Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content)
    {
        PostedUrls.Add(requestUri ?? "");
        return _client.PostAsync(requestUri, content);
    }

    public Task<HttpResponseMessage> PingAsync(string? requestUri)
    {
        PostedUrls.Add(requestUri ?? "");
        return _client.PostAsync(requestUri, null);
    }

    public void Dispose() => _client.Dispose();

    private class FakeHandler(MockTowerHttpClient parent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            parent.PostedUrls.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
