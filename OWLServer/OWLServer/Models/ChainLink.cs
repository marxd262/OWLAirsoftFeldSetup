namespace OWLServer.Models;

public class ChainLink
{
    public int Id { get; set; }
    public int ChainLayoutId { get; set; }
    public string TowerAMacAddress { get; set; } = string.Empty;
    public string TowerBMacAddress { get; set; } = string.Empty;
    public bool EntryAtBothEnds { get; set; } = true;
}
