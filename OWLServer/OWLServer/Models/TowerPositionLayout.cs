namespace OWLServer.Models;

public class TowerPositionLayout
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TowerPosition> Positions { get; set; } = new();
}
