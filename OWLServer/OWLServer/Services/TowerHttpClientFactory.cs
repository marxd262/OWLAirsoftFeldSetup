using OWLServer.Services.Interfaces;

namespace OWLServer.Services;

public class TowerHttpClientFactory : ITowerHttpClientFactory
{
    public ITowerHttpClient Create(string ip)
        => new TowerHttpClient(new UriBuilder(ip).Uri);
}
