using Microsoft.AspNetCore.Components;
using OWLServer.Models;
using OWLServer.Services.Interfaces;

namespace OWLServer.Components.ConfigComponents;

public partial class ChainGraphPreview : ComponentBase
{
    [Parameter] public List<ChainLink> Links { get; set; } = new();

    [Inject] public IGameStateService GameStateService { get; set; } = null!;

    private List<(string mac, double x, double y)> _nodePositions = new();
    private List<(string aMac, string bMac, bool bothWays)> _renderLinks = new();
    private double _width = 300;
    private double _height = 200;
    private const double Padding = 36;
    private const double LevelSpacing = 55;
    private const double NodeSpacing = 52;

    protected override void OnParametersSet() => LayoutGraph();

    private void LayoutGraph()
    {
        _nodePositions.Clear();
        _renderLinks.Clear();

        var links = Links;
        if (!links.Any()) { _width = 300; _height = 100; return; }

        var towers = GameStateService.TowerManagerService.Towers;

        var adj = new Dictionary<string, List<string>>();
        var inDeg = new Dictionary<string, int>();
        var allMacs = new HashSet<string>();

        foreach (var l in links)
        {
            allMacs.Add(l.TowerAMacAddress);
            allMacs.Add(l.TowerBMacAddress);
        }

        foreach (var mac in allMacs)
        {
            if (!towers.ContainsKey(mac)) continue;
            adj[mac] = new List<string>();
            inDeg[mac] = 0;
        }

        foreach (var l in links)
        {
            if (!adj.ContainsKey(l.TowerAMacAddress) || !adj.ContainsKey(l.TowerBMacAddress)) continue;
            adj[l.TowerAMacAddress].Add(l.TowerBMacAddress);
            if (!inDeg.ContainsKey(l.TowerBMacAddress)) inDeg[l.TowerBMacAddress] = 0;
            inDeg[l.TowerBMacAddress]++;

            if (l.EntryAtBothEnds)
            {
                adj[l.TowerBMacAddress].Add(l.TowerAMacAddress);
                if (!inDeg.ContainsKey(l.TowerAMacAddress)) inDeg[l.TowerAMacAddress] = 0;
                inDeg[l.TowerAMacAddress]++;
            }

            _renderLinks.Add((l.TowerAMacAddress, l.TowerBMacAddress, l.EntryAtBothEnds));
        }

        var levels = new Dictionary<string, int>();
        var queue = new Queue<string>();
        foreach (var (mac, deg) in inDeg)
        {
            if (deg == 0)
            {
                levels[mac] = 0;
                queue.Enqueue(mac);
            }
        }
        if (!queue.Any() && inDeg.Any())
        {
            var first = inDeg.Keys.First();
            levels[first] = 0;
            queue.Enqueue(first);
        }

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!adj.ContainsKey(cur)) continue;
            foreach (var nxt in adj[cur])
            {
                if (!levels.ContainsKey(nxt))
                {
                    levels[nxt] = levels[cur] + 1;
                    queue.Enqueue(nxt);
                }
                else
                {
                    levels[nxt] = Math.Max(levels[nxt], levels[cur] + 1);
                }
            }
        }

        var groups = levels.GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key).ToList();

        var maxLevel = groups.Any() ? groups.Max(g => g.Key) : 0;
        var maxPerLevel = groups.Any() ? groups.Max(g => g.Count()) : 1;

        _width = Math.Max(300, Padding * 2 + maxPerLevel * NodeSpacing);
        _height = Padding * 2 + maxLevel * LevelSpacing;

        foreach (var g in groups)
        {
            var nodes = g.ToList();
            var totalWidth = nodes.Count * NodeSpacing;
            var startX = (_width - totalWidth) / 2 + NodeSpacing / 2;
            for (int i = 0; i < nodes.Count; i++)
                _nodePositions.Add((nodes[i].Key, startX + i * NodeSpacing,
                    Padding + g.Key * LevelSpacing));
        }
    }

    private static string NodeFill(string mac) => "#16213e";

    private string ShortLabel(string mac)
    {
        if (GameStateService.TowerManagerService.Towers.TryGetValue(mac, out var t))
            return t.DisplayLetter;
        return mac.Length > 4 ? mac[..4] : mac;
    }

    private static string Fmt(double v) =>
        v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
}
