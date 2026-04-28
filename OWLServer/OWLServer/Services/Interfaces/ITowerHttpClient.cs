namespace OWLServer.Services.Interfaces;

public interface ITowerHttpClient
{
    Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content);
    Task<HttpResponseMessage> PingAsync(string? requestUri);
}
