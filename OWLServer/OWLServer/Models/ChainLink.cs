namespace OWLServer.Models;

public class ChainLink
{
    public int Id { get; set; }
    public int ChainLayoutId { get; set; }
    public string FromTowerMacAddress { get; set; } = string.Empty;
    public string ToTowerMacAddress { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; }
}
