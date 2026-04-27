namespace OWLServer.Models;

public class TowerControlLink
{
    public int Id { get; set; }
    public int TowerControlLayoutId { get; set; }
    public string ControllerTowerMacAddress { get; set; } = string.Empty;
    public string ControlledTowerMacAddress { get; set; } = string.Empty;
}
