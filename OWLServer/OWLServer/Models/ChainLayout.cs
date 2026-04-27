namespace OWLServer.Models;

public class ChainLayout
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ChainLink> Links { get; set; } = new();
}
