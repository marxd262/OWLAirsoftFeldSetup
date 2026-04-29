using OWLServer.Models;
using OWLServer.Models.GameModes;
using Xunit;

namespace OWLServer.Tests.Unit.GameModes;

public class ChainGraphEngineTests
{
    // --- Helpers ---

    private static ChainLayout Layout(params ChainLink[] links) => new() { Links = links.ToList() };

    private static ChainLink Link(string a, string b, bool both = false) => new()
    {
        TowerAMacAddress = a, TowerBMacAddress = b, EntryAtBothEnds = both
    };

    private static Dictionary<string, Tower> Towers(params Tower[] list) =>
        list.ToDictionary(t => t.MacAddress);

    private static Tower T(string mac) => new() { MacAddress = mac };

    // --- Graph Construction ---

    [Fact]
    public void LinearChain_T1toT2toT3_EntryIsT1()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Single(engine.EntryPoints);
        Assert.Contains("T1", engine.EntryPoints);
    }

    [Fact]
    public void LinearChain_T1toT2toT3_CorrectSuccessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(new[] { "T2" }, engine.Successors["T1"]);
        Assert.Equal(new[] { "T3" }, engine.Successors["T2"]);
        Assert.False(engine.Successors.ContainsKey("T3"));
    }

    [Fact]
    public void LinearChain_T1toT2toT3_CorrectPredecessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.Predecessors.ContainsKey("T1"));
        Assert.Equal(new[] { "T1" }, engine.Predecessors["T2"]);
        Assert.Equal(new[] { "T2" }, engine.Predecessors["T3"]);
    }

    [Fact]
    public void LinearChain_T1toT2toT3_CorrectDepthMap()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(0, engine.DepthMap["T1"]);
        Assert.Equal(1, engine.DepthMap["T2"]);
        Assert.Equal(2, engine.DepthMap["T3"]);
    }

    [Fact]
    public void Branch_T1toT2_and_T1toT3_BothChildrenAreSuccessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"), Link("T1", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(2, engine.Successors["T1"].Count);
        Assert.Contains("T2", engine.Successors["T1"]);
        Assert.Contains("T3", engine.Successors["T1"]);
    }

    [Fact]
    public void Merge_T1toT3_and_T2toT3_T3HasTwoPredecessors()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T3"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(2, engine.Predecessors["T3"].Count);
        Assert.Contains("T1", engine.Predecessors["T3"]);
        Assert.Contains("T2", engine.Predecessors["T3"]);
    }

    [Fact]
    public void Bidirectional_BothEndsAreEntryPoints()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = Layout(Link("T1", "T2", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(2, engine.EntryPoints.Count);
        Assert.Contains("T1", engine.EntryPoints);
        Assert.Contains("T2", engine.EntryPoints);
    }

    [Fact]
    public void Bidirectional_BothAreSuccessorsAndPredecessors()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = Layout(Link("T1", "T2", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Contains("T2", engine.Successors["T1"]);
        Assert.Contains("T1", engine.Successors["T2"]);
        Assert.Contains("T2", engine.Predecessors["T1"]);
        Assert.Contains("T1", engine.Predecessors["T2"]);
    }

    [Fact]
    public void Diamond_T1toT2_T1toT3_T2toT4_T3toT4_EntryIsT1()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"), T("T4"));
        var layout = Layout(Link("T1", "T2"), Link("T1", "T3"), Link("T2", "T4"), Link("T3", "T4"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Single(engine.EntryPoints);
        Assert.Contains("T1", engine.EntryPoints);
        Assert.Equal(0, engine.DepthMap["T1"]);
        Assert.Equal(1, engine.DepthMap["T2"]);
        Assert.Equal(1, engine.DepthMap["T3"]);
        Assert.Equal(2, engine.DepthMap["T4"]);
    }

    [Fact]
    public void TowerNotInLayout_NotInGraph()
    {
        var towers = Towers(T("T1"), T("T2"), T("T3"));
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.Successors.ContainsKey("T3"));
        Assert.False(engine.Predecessors.ContainsKey("T3"));
        Assert.DoesNotContain("T3", engine.EntryPoints);
        Assert.False(engine.DepthMap.ContainsKey("T3"));
    }

    [Fact]
    public void EmptyLayout_AllGraphsEmpty()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = new ChainLayout();
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Empty(engine.Successors);
        Assert.Empty(engine.Predecessors);
        Assert.Empty(engine.EntryPoints);
        Assert.Empty(engine.DepthMap);
    }

    [Fact]
    public void NullLayout_AllGraphsEmpty()
    {
        var towers = Towers(T("T1"), T("T2"));
        var engine = new ChainGraphEngine(null, towers);

        Assert.Empty(engine.Successors);
        Assert.Empty(engine.Predecessors);
        Assert.Empty(engine.EntryPoints);
        Assert.Empty(engine.DepthMap);
    }

    [Fact]
    public void SingleTowerInLayout_NoLinks_IsNotEntryPoint()
    {
        var towers = Towers(T("T1"));
        var layout = Layout();
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Empty(engine.EntryPoints);
    }

    // --- CanPress ---

    [Fact]
    public void CanPress_EntryPoint_ReturnsTrue()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, T("T2"));
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T1", TeamColor.RED));
    }

    [Fact]
    public void CanPress_SuccessorWithPredecessorHeld_ReturnsTrue()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T2", TeamColor.RED));
    }

    [Fact]
    public void CanPress_SuccessorWithoutPredecessor_ReturnsFalse()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.CanPress("T2", TeamColor.RED));
    }

    [Fact]
    public void CanPress_CounterCaptureEnemyOwnedTower_ReturnsTrue()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T2", TeamColor.RED));
    }

    [Fact]
    public void CanPress_AlreadyOwnByPressingTeam_ReturnsFalse()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED);
        var towers = Towers(t1, T("T2"));
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.CanPress("T1", TeamColor.RED));
    }

    [Fact]
    public void CanPress_TowerNotInChainLayout_ReturnsTrue()
    {
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.NONE);
        var towers = Towers(T("T1"), T("T2"), t3);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T3", TeamColor.RED));
    }

    [Fact]
    public void CanPress_UnknownMac_ReturnsFalse()
    {
        var towers = Towers(T("T1"), T("T2"));
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.False(engine.CanPress("T_UNKNOWN", TeamColor.RED));
    }

    [Fact]
    public void CanPress_BidirectionalEntry_BothCanBePressed()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T1", TeamColor.RED));
        Assert.True(engine.CanPress("T2", TeamColor.BLUE));
    }

    [Fact]
    public void CanPress_CounterCaptureEnemyOwnedEntryTower_ReturnsTrue()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.BLUE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.RED);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T1", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.True(engine.CanPress("T1", TeamColor.RED));
    }

    // --- CompleteCapture → LockDescendants ---

    [Fact]
    public void CompleteCapture_MidChainLocksDescendants()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.NONE, tC.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_EntryPointIsSkippedDuringLocking()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("C", TeamColor.BLUE);

        Assert.Equal(TeamColor.BLUE, tC.CurrentColor);
        Assert.Equal(TeamColor.NONE, tA.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_PreviousOwnerNONE_LocksAllDescendants()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.NONE, tC.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_RecursiveCascade_DeepChain()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.BLUE);
        var tD = T("D"); tD.SetTowerColor(TeamColor.BLUE);
        var tE = T("E"); tE.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB, tC, tD, tE);
        var layout = Layout(Link("A", "B"), Link("B", "C"), Link("C", "D"), Link("D", "E"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.NONE, tC.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tD.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tE.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_NoSuccessors_NoLocking()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.RED, tA.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_CounterCaptureEntry_NeutralJunctionNotLocked()
    {
        var t1 = T("1"); t1.SetTowerColor(TeamColor.BLUE);
        var t2 = T("2"); t2.SetTowerColor(TeamColor.BLUE);
        var t3 = T("3"); t3.SetTowerColor(TeamColor.NONE);
        var t4 = T("4"); t4.SetTowerColor(TeamColor.RED);
        var t5 = T("5"); t5.SetTowerColor(TeamColor.RED);
        var towers = Towers(t1, t2, t3, t4, t5);
        var layout = Layout(
            Link("1", "2", both: true),
            Link("2", "3", both: true),
            Link("3", "4", both: true),
            Link("4", "5", both: true)
        );
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("5", TeamColor.BLUE);

        Assert.Equal(TeamColor.BLUE, t5.CurrentColor);
        Assert.Equal(TeamColor.NONE, t4.CurrentColor);
        Assert.Equal(TeamColor.NONE, t3.CurrentColor);
        Assert.Equal(TeamColor.BLUE, t2.CurrentColor);
        Assert.Equal(TeamColor.BLUE, t1.CurrentColor);
    }

    // --- CompleteCapture → UnlockSuccessors ---

    [Fact]
    public void CompleteCapture_UnlocksNextTower()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.LOCKED);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("A", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tA.CurrentColor);
        Assert.Equal(TeamColor.NONE, tB.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_UnlocksWhenSingleMergePredecessorHeld()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.LOCKED);
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2, t3);
        var layout = Layout(Link("T1", "T2"), Link("T3", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("T1", TeamColor.RED);

        Assert.Equal(TeamColor.RED, t1.CurrentColor);
        Assert.Equal(TeamColor.NONE, t2.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_UnlocksWhenAnyPredecessorHeld()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.BLUE);
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.LOCKED);
        var towers = Towers(t1, t2, t3);
        var layout = Layout(Link("T1", "T3"), Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("T1", TeamColor.RED);

        Assert.Equal(TeamColor.RED, t1.CurrentColor);
        Assert.Equal(TeamColor.NONE, t3.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_NoSuccessors_NoUnlock()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);
        Assert.Equal(TeamColor.RED, tB.CurrentColor);
    }

    // --- CompleteCapture → Missing Prerequisites ---

    [Fact]
    public void CompleteCapture_MissingPrerequisites_SetsNONE()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.NONE);
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.NONE);
        var t3 = T("T3"); t3.SetTowerColor(TeamColor.NONE);
        var towers = Towers(t1, t2, t3);
        var layout = Layout(Link("T1", "T2"), Link("T3", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("T2", TeamColor.RED);

        Assert.Equal(TeamColor.NONE, t2.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_MissingPrerequisites_LocksDownstream()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.NONE, tB.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tC.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_CounterCaptureLocksPreviousOwnersDescendants()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var tC = T("C"); tC.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tB.CurrentColor);
        Assert.Equal(TeamColor.NONE, tC.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_EntryPointCapture_UnlocksDescendants()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.LOCKED);
        var tC = T("C"); tC.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("A", TeamColor.RED);

        Assert.Equal(TeamColor.RED, tA.CurrentColor);
        Assert.Equal(TeamColor.NONE, tB.CurrentColor);
    }

    [Fact]
    public void CompleteCapture_ForwardFromEntry_LocksDownstreamOfPreviousOwner()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.BLUE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED);
        var tC = T("C"); tC.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        engine.CompleteCapture("B", TeamColor.RED);

        Assert.Equal(TeamColor.NONE, tB.CurrentColor);
        Assert.Equal(TeamColor.LOCKED, tC.CurrentColor);
    }

    // --- GetChainPoints ---

    [Fact]
    public void GetChainPoints_SingleTower_ReturnsMultiplier()
    {
        var t1 = T("A"); t1.SetTowerColor(TeamColor.RED); t1.Multiplier = 2.0;
        var t2 = T("B"); t2.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        int points = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);

        Assert.Equal(2, points);
    }

    [Fact]
    public void GetChainPoints_LinearDepthScaling()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED); tA.Multiplier = 1.0;
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED); tB.Multiplier = 1.0;
        var tC = T("C"); tC.SetTowerColor(TeamColor.RED); tC.Multiplier = 1.0;
        var towers = Towers(tA, tB, tC);
        var layout = Layout(Link("A", "B"), Link("B", "C"));
        var engine = new ChainGraphEngine(layout, towers);

        int points = engine.GetChainPoints(TeamColor.RED, chainFactor: 2.0);

        Assert.Equal(7, points); // 2^0*1 + 2^1*1 + 2^2*1 = 1+2+4 = 7
    }

    [Fact]
    public void GetChainPoints_ChainFactorEffects()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED); tA.Multiplier = 1.0;
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED); tB.Multiplier = 1.0;
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        int pointsChainFactor1 = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);
        int pointsChainFactor3 = engine.GetChainPoints(TeamColor.RED, chainFactor: 3.0);

        Assert.Equal(2, pointsChainFactor1);  // 1^0*1 + 1^1*1 = 2
        Assert.Equal(4, pointsChainFactor3);  // 3^0*1 + 3^1*1 = 4
    }

    [Fact]
    public void GetChainPoints_NoOwnedTowers_ReturnsZero()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.BLUE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        Assert.Equal(0, engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0));
    }

    [Fact]
    public void GetChainPoints_MixedTeamOwnership_OnlyCountsOwnTeam()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED); tA.Multiplier = 1.0;
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE); tB.Multiplier = 1.0;
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        int redPoints = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);
        int bluePoints = engine.GetChainPoints(TeamColor.BLUE, chainFactor: 1.0);

        Assert.Equal(1, redPoints);
        Assert.Equal(1, bluePoints);
    }

    [Fact]
    public void GetChainPoints_TowerNotInDepthMap_UsesDepthZero()
    {
        var t1 = T("T1"); t1.SetTowerColor(TeamColor.RED); t1.Multiplier = 3.0;
        var t2 = T("T2"); t2.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(t1, t2);
        var layout = Layout(Link("T2", "T3"));
        var engine = new ChainGraphEngine(layout, towers);

        int points = engine.GetChainPoints(TeamColor.RED, chainFactor: 1.0);
        Assert.Equal(3, points);
    }

    // --- GetLinkVisualState ---

    [Fact]
    public void GetLinkVisualState_BothSameTeam_SolidTeamColor()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, arrowA, arrowB, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#fc1911", color);
        Assert.False(arrowA);
        Assert.False(arrowB);
        Assert.False(animated);
        Assert.False(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_BothNeutral_Grey()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, _) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#BBBBBB", color);
        Assert.False(animated);
    }

    [Fact]
    public void GetLinkVisualState_OneEndOwnedOtherNONE_Animated()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, arrowA, arrowB, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#fc1911", color);
        Assert.False(arrowA);
        Assert.True(arrowB);
        Assert.True(animated);
        Assert.False(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_BothTeamsContested_WhiteAnimatedBothWays()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#FFFFFF", color);
        Assert.True(animated);
        Assert.True(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_LockedTower_Yellow()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.LOCKED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, _, _) = engine.GetLinkVisualState(layout.Links[0]);
        Assert.Equal("#FFD700", color);
    }

    [Fact]
    public void GetLinkVisualState_BidirectionalBothDifferentTeams_WhiteBothWays()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var tB = T("B"); tB.SetTowerColor(TeamColor.BLUE);
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B", both: true));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, bothWays) = engine.GetLinkVisualState(layout.Links[0]);

        Assert.Equal("#FFFFFF", color);
        Assert.True(animated);
        Assert.True(bothWays);
    }

    [Fact]
    public void GetLinkVisualState_MissingTower_Grey()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.RED);
        var towers = Towers(tA);
        var layout = Layout(Link("A", "T2"));
        var engine = new ChainGraphEngine(layout, towers);

        var (color, _, _, animated, _) = engine.GetLinkVisualState(layout.Links[0]);
        Assert.Equal("#BBBBBB", color);
        Assert.False(animated);
    }

    // --- ProcessTick ---

    [Fact]
    public void ProcessTick_NoPressedTowers_ReturnsEmpty()
    {
        var towers = Towers(T("A"), T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Empty(updates);
    }

    [Fact]
    public void ProcessTick_PressedTowerWithinTime_UpdatesProgress()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        tA.IsPressed = true;
        tA.PressedByColor = TeamColor.RED;
        tA.LastPressed = DateTime.Now;
        tA.TimeToCaptureInSeconds = 5;
        var towers = Towers(tA, T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Single(updates);
        Assert.False(updates[0].CaptureCompleted);
        Assert.True(updates[0].CaptureProgress > 0);
        Assert.True(updates[0].CaptureProgress < 1);
    }

    [Fact]
    public void ProcessTick_PressedTowerTimerExpired_CompletesCapture()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        tA.IsPressed = true;
        tA.PressedByColor = TeamColor.RED;
        tA.LastPressed = DateTime.Now.AddSeconds(-10);
        tA.TimeToCaptureInSeconds = 5;
        var towers = Towers(tA, T("B"));
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Single(updates);
        Assert.True(updates[0].CaptureCompleted);
        Assert.Equal(1, updates[0].CaptureProgress);
        Assert.Equal(TeamColor.RED, tA.CurrentColor);
    }

    [Fact]
    public void ProcessTick_PressDisallowed_ResetsState()
    {
        var tA = T("A"); tA.SetTowerColor(TeamColor.NONE);
        var tB = T("B"); tB.SetTowerColor(TeamColor.NONE);
        tB.IsPressed = true;
        tB.PressedByColor = TeamColor.RED;
        tB.LastPressed = DateTime.Now;
        tB.CaptureProgress = 0.5;
        var towers = Towers(tA, tB);
        var layout = Layout(Link("A", "B"));
        var engine = new ChainGraphEngine(layout, towers);

        var updates = engine.ProcessTick();

        Assert.Single(updates);
        Assert.False(updates[0].CaptureCompleted);
        Assert.Equal(0, updates[0].CaptureProgress);
        Assert.False(tB.IsPressed);
        Assert.Null(tB.LastPressed);
    }
}
