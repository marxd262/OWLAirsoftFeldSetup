namespace OWLServer.Models;

public class TowerControlLayout
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TowerControlLink> Links { get; set; } = new();
}
