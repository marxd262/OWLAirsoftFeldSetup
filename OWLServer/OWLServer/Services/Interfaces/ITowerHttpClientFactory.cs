namespace OWLServer.Services.Interfaces;

public interface ITowerHttpClientFactory
{
    ITowerHttpClient Create(string ip);
}
