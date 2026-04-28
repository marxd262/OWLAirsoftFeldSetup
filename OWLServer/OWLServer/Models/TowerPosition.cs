namespace OWLServer.Models;

public class TowerPosition
{
    public int Id { get; set; }
    public int TowerPositionLayoutId { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public double Top { get; set; }
    public double Left { get; set; }
}
