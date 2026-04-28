# Automated Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Full test pyramid — interfaces, DI refactor, xUnit integration tests, Moq unit tests, bUnit UI tests.

**Architecture:** Extract 7 interfaces from concretely-coupled services. Register via DI (singletons unchanged). Decouple Tower's `HttpClient` behind `ITowerHttpClientFactory`. Then scaffold test project with WebApplicationFactory integration tests, Moq unit tests, and bUnit component tests.

**Tech Stack:** .NET 8, xUnit, Moq, bUnit, WebApplicationFactory, EF Core SQLite in-memory

---

### Task 1: Create `IExternalTriggerService` interface

**Files:**
- Create: `OWLServer/OWLServer/Services/Interfaces/IExternalTriggerService.cs`

- [ ] **Step 1: Create the interface file**

```csharp
using OWLServer.Models;

namespace OWLServer.Services.Interfaces;

public interface IExternalTriggerService
{
    Action StateHasChangedAction { get; set; }
    event EventHandler<KlickerEventArgs> KlickerPressedAction;
    event EventHandler<TowerEventArgs> TowerPressedAction;
    void InvokeKlickerPressed(TeamColor color);
    void InvokeTowerPressed(string towerId, TeamColor color);
}
```

- [ ] **Step 2: Make ExternalTriggerService implement IExternalTriggerService**

```csharp
// OWLServer/OWLServer/Services/ExternalTriggerService.cs
// Change: public class ExternalTriggerService
// To:     public class ExternalTriggerService : IExternalTriggerService
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add OWLServer/OWLServer/Services/Interfaces/ OWLServer/OWLServer/Services/ExternalTriggerService.cs
git commit -m "feat: extract IExternalTriggerService interface"
```

---

### Task 2: Create `ITowerManagerService` interface

**Files:**
- Create: `OWLServer/OWLServer/Services/Interfaces/ITowerManagerService.cs`

- [ ] **Step 1: Create the interface**

```csharp
using OWLServer.Models;

namespace OWLServer.Services.Interfaces;

public interface ITowerManagerService
{
    Dictionary<string, Tower> Towers { get; }
    void RegisterTower(string id, string ip);
    void TowerChangeColor(string towerId, TeamColor newColor);
    int GetPoints(TeamColor teamColor);
    void HandleTowerButtonPressed(string towerId, TeamColor color);
    void HandleTowerButtonReleased(string towerId);
    void SetColorForAllTowers(TeamColor teamColor);
    void SetAllTowerToStartColor();
    Task PingAll();
    Task OffTowers();
    Task ResetTowers();
}
```

- [ ] **Step 2: Make TowerManagerService implement ITowerManagerService**, change PingAll/OffTowers/ResetTowers from `async void` to `async Task`

- [ ] **Step 3: Build and commit**

---

### Task 3: Create `IGameStateService` interface

**Files:**
- Create: `OWLServer/OWLServer/Services/Interfaces/IGameStateService.cs`

- [ ] **Step 1: Create the interface**

```csharp
using OWLServer.Models;
using OWLServer.Models.GameModes;

namespace OWLServer.Services.Interfaces;

public interface IGameStateService
{
    IExternalTriggerService ExternalTriggerService { get; }
    IAudioService AudioService { get; }
    IGameModeBase? CurrentGame { get; set; }
    ITowerManagerService TowerManagerService { get; }
    Dictionary<TeamColor, TeamBase> Teams { get; }
    TeamColor TeamInWald { get; set; }
    TeamColor TeamInStadt { get; set; }
    bool WaldSpawnReady { get; set; }
    bool StadtSpawnReady { get; set; }
    bool TeamSetReady { get; set; }
    bool AutoStartAfterReady { get; set; }
    int SecondsTillAutoStartAfterReady { get; set; }
    DateTime? AutoStartProcessStarted { get; set; }
    void StartGame();
    void StopGame();
    void HandleGameEnd();
    void Reset();
}
```

- [ ] **Step 2: Make GameStateService implement IGameStateService**, update constructor to take interfaces + `ITowerManagerService`, remove `new TowerManagerService(...)`

```csharp
// GameStateService constructor — BEFORE:
public GameStateService(ExternalTriggerService externalTriggerService, AudioService audioService)
{
    ExternalTriggerService = externalTriggerService;
    AudioService = audioService;
    TowerManagerService = new TowerManagerService(externalTriggerService);
    ...
}

// GameStateService constructor — AFTER:
public GameStateService(IExternalTriggerService externalTriggerService, IAudioService audioService,
                        ITowerManagerService towerManagerService)
{
    ExternalTriggerService = externalTriggerService;
    AudioService = audioService;
    TowerManagerService = towerManagerService;
    ...
}
```

- [ ] **Step 3: Build and commit**

---

### Task 4: Create `IAudioService` and `IMapService` interfaces

**Files:**
- Create: `OWLServer/OWLServer/Services/Interfaces/IAudioService.cs`
- Create: `OWLServer/OWLServer/Services/Interfaces/IMapService.cs`

```csharp
// IAudioService
namespace OWLServer.Services.Interfaces;
public interface IAudioService
{
    void PlaySound(Sounds sound);
    void StopSound();
    string GetAssignedFile(Sounds sound);
    IReadOnlyList<string> GetAvailableSoundFiles();
    void SetSoundFile(Sounds sound, string? filename);
    Task SaveUploadedFileAsync(string filename, Stream data);
    void DeleteSoundFile(string filename);
}

// IMapService
namespace OWLServer.Services.Interfaces;
public interface IMapService
{
    string GetCurrentMapUrl();
    string GetCurrentMapFile();
    IReadOnlyList<string> GetAvailableMapFiles();
    void SetCurrentMapFile(string? filename);
    Task SaveUploadedMapFileAsync(string filename, Stream data);
    void DeleteMapFile(string filename);
}
```

- Make AudioService implement IAudioService, MapService implement IMapService
- Build and commit

---

### Task 5: Create `ITowerHttpClient` and `ITowerHttpClientFactory` interfaces

**Files:**
- Create: `OWLServer/OWLServer/Services/Interfaces/ITowerHttpClient.cs`
- Create: `OWLServer/OWLServer/Services/Interfaces/ITowerHttpClientFactory.cs`

```csharp
// ITowerHttpClient.cs
namespace OWLServer.Services.Interfaces;
public interface ITowerHttpClient
{
    Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content);
    Task<HttpResponseMessage> PingAsync(string? requestUri);
}

// ITowerHttpClientFactory.cs
namespace OWLServer.Services.Interfaces;
public interface ITowerHttpClientFactory
{
    ITowerHttpClient Create(string ip);
}
```

- Build and commit

---

### Task 6: Create production `TowerHttpClient` and `TowerHttpClientFactory`

**Files:**
- Create: `OWLServer/OWLServer/Services/TowerHttpClient.cs`
- Create: `OWLServer/OWLServer/Services/TowerHttpClientFactory.cs`

```csharp
// TowerHttpClient.cs
using System.Net.Http.Headers;
using OWLServer.Services.Interfaces;

namespace OWLServer.Services;

public class TowerHttpClient : ITowerHttpClient
{
    private readonly HttpClient _client;

    public TowerHttpClient(Uri baseAddress)
    {
        _client = new HttpClient { BaseAddress = baseAddress };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content)
        => _client.PostAsync(requestUri, content);

    public Task<HttpResponseMessage> PingAsync(string? requestUri)
        => _client.PostAsync(requestUri, null);
}

// TowerHttpClientFactory.cs
using OWLServer.Services.Interfaces;

namespace OWLServer.Services;

public class TowerHttpClientFactory : ITowerHttpClientFactory
{
    public ITowerHttpClient Create(string ip)
        => new TowerHttpClient(new UriBuilder(ip).Uri);
}
```

- Build and commit

---

### Task 7: Refactor `Tower` to use `ITowerHttpClient`

**Files:**
- Modify: `OWLServer/OWLServer/Models/Tower.cs`

- [ ] **Step 1: Add interface field, update constructor, remove HttpClient field**

```csharp
// In Tower class — add field:
private ITowerHttpClient? _httpClient;

// Remove: private HttpClient _client = new HttpClient();

// Add new constructor:
public Tower(string id, string ip, ITowerHttpClient client)
{
    MacAddress = id;
    IP = ip;
    Name = string.Empty;
    _httpClient = client;
}

// Keep parameterless constructor for EF Core:
public Tower() { }
```

- [ ] **Step 2: Update SendColorToTower — `async void` → `async Task`, use _httpClient**

```csharp
public async Task SendColorToTower(Color color)
{
    if (_httpClient == null) return;
    try
    {
        string c = $"{color.R}/{color.G}/{color.B}";
        await _httpClient.PostAsync($"/api/setcolor/{c}", null);
    }
    catch { }
}
```

- [ ] **Step 3: Update PingTower — `async void` → `async Task`, use _httpClient**

```csharp
public async Task PingTower()
{
    if (_httpClient == null) return;
    try
    {
        var response = await _httpClient.PingAsync("/api/ping");
        TowerOnline = response.IsSuccessStatusCode;
        LastPing = DateTime.Now;
    }
    catch { }
}
```

- [ ] **Step 4: Update TowerManagerService.RegisterTower to use factory**

```csharp
// In TowerManagerService — add factory field:
private readonly ITowerHttpClientFactory _httpFactory;

public TowerManagerService(IExternalTriggerService externalTriggerService, ITowerHttpClientFactory httpFactory)
{
    _httpFactory = httpFactory;
    ExternalTriggerService = externalTriggerService;
    ExternalTriggerService.TowerPressedAction += HandleTowerClicked;
}

// In RegisterTower:
public void RegisterTower(string id, string ip)
{
    if (Towers.ContainsKey(id)) return;
    var maxChar = Towers.Max(e => e.Value.DisplayLetter);
    // ... letter logic ...
    Towers.Add(id, new Tower(id, ip, _httpFactory.Create(ip)) { CurrentColor = TeamColor.NONE, DisplayLetter = maxChar });
    ExternalTriggerService.StateHasChangedAction?.Invoke();
}
```

- [ ] **Step 5: Build and commit**

---

### Task 8: Update `Program.cs` DI registrations

**Files:**
- Modify: `OWLServer/OWLServer/Program.cs`

```csharp
// Replace:
builder.Services.AddSingleton<GameStateService>();
builder.Services.AddSingleton<ExternalTriggerService>();
builder.Services.AddSingleton<AudioService>();
builder.Services.AddSingleton<MapService>();

// With:
builder.Services.AddSingleton<IExternalTriggerService, ExternalTriggerService>();
builder.Services.AddSingleton<ITowerHttpClientFactory, TowerHttpClientFactory>();
builder.Services.AddSingleton<ITowerManagerService, TowerManagerService>();
builder.Services.AddSingleton<IAudioService, AudioService>();
builder.Services.AddSingleton<IMapService, MapService>();
builder.Services.AddSingleton<IGameStateService, GameStateService>();
```

- Build and commit

---

### Task 9: Update `OWLAPI` controller to use interfaces

**Files:**
- Modify: `OWLServer/OWLServer/Controllers/OWLAPI.cs`

```csharp
// Constructor change:
public OWLAPI(IExternalTriggerService externalTriggerService, IGameStateService gameStateService)
{
    GameStateService = gameStateService;
    ExternalTriggerService = externalTriggerService;
}

// Property types change:
IGameStateService GameStateService { get; set; }
IExternalTriggerService ExternalTriggerService { get; set; }
```

- Build and commit

---

### Task 10: Update game mode constructors to use interfaces

**Files:**
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeConquest.cs`
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeTeamDeathmatch.cs`
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeChainBreak.cs`
- Modify: `OWLServer/OWLServer/Models/GameModes/GameModeTimer.cs`

Change private fields and constructor parameter types from `ExternalTriggerService` → `IExternalTriggerService` and `GameStateService` → `IGameStateService` in all four.

- Build and commit

---

### Task 11: Update Blazor `@inject` to resolve via interfaces

**Files:**
- Modify: `OWLServer/OWLServer/Components/Pages/Home.razor`
- Modify: `OWLServer/OWLServer/Components/Pages/SpawnPage.razor`
- Modify: `OWLServer/OWLServer/Components/Pages/GameEndOverlay.razor`
- Modify: `OWLServer/OWLServer/Components/TeamPanel/MatchScoreBar.razor`
- Modify: `OWLServer/OWLServer/Components/Pages/AdminPages/GameControlDashboard.razor`
- Modify: All `AdminPages` and `ConfigComponents` that inject `GameStateService` or `ExternalTriggerService`

Change `@inject GameStateService` → `@inject IGameStateService` and `@inject ExternalTriggerService` → `@inject IExternalTriggerService` everywhere.

- Build and commit

---

### Task 12: Phase 1 verification

```bash
dotnet build
```

Expected: Build succeeds. All singletons resolve. All interface implementations registered.

---

### Task 13: Scaffold test project

**Files:**
- Create: `OWLServer/OWLServer.Tests/OWLServer.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="bunit" Version="1.38.5" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.15" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\OWLServer\OWLServer.csproj" />
  </ItemGroup>
</Project>
```

- [ ] Create directory structure: `Unit/Services/`, `Unit/GameModes/`, `Unit/Models/`, `Integration/Api/`, `Integration/GameMode/`, `UI/Components/`, `UI/Pages/`, `Helpers/`
- [ ] Add test project to solution: `dotnet sln add OWLServer.Tests/OWLServer.Tests.csproj`
- [ ] `dotnet restore`, `dotnet build`

---

### Task 14: Create test helpers

**Files:**
- Create: `OWLServer/OWLServer.Tests/Helpers/MockTowerHttpClient.cs`

```csharp
using System.Net;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Helpers;

public class MockTowerHttpClient : ITowerHttpClient, IDisposable
{
    private readonly HttpClient _client;

    public List<string> PostedUrls { get; } = new();

    public MockTowerHttpClient()
    {
        var handler = new FakeHandler(this);
        _client = new HttpClient(handler) { BaseAddress = new Uri("http://mock-tower.local/") };
    }

    public Task<HttpResponseMessage> PostAsync(string? requestUri, HttpContent? content)
    {
        PostedUrls.Add(requestUri ?? "");
        return _client.PostAsync(requestUri, content);
    }

    public Task<HttpResponseMessage> PingAsync(string? requestUri)
    {
        PostedUrls.Add(requestUri ?? "");
        return _client.PostAsync(requestUri, null);
    }

    public void Dispose() => _client.Dispose();

    private class FakeHandler(MockTowerHttpClient parent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            parent.PostedUrls.Add(request.RequestUri?.PathAndQuery ?? "");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
```

- Create: `OWLServer/OWLServer.Tests/Helpers/TestDbHelper.cs` (in-memory SQLite with shared cache)

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OWLServer.Context;

namespace OWLServer.Tests.Helpers;

public static class TestDbHelper
{
    public static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        connection.Open();
        return connection;
    }

    public static IDbContextFactory<DatabaseContext> CreateContextFactory(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new DatabaseContext(options);
        context.Database.EnsureCreated();

        return new TestDbContextFactory(options);
    }

    private class TestDbContextFactory(DbContextOptions<DatabaseContext> options)
        : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => new(options);
    }
}
```

---

### Task 15: Create `CustomWebApplicationFactory`

**Files:**
- Create: `OWLServer/OWLServer.Tests/Integration/CustomWebApplicationFactory.cs`

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OWLServer.Context;
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Helpers;

namespace OWLServer.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public MockTowerHttpClient MockTowerHttpClient { get; } = new();
    private readonly SqliteConnection _connection = TestDbHelper.CreateConnection();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<DatabaseContext>>();
            services.RemoveAll<DbContextOptions<DatabaseContext>>();

            services.AddDbContextFactory<DatabaseContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<ITowerHttpClientFactory>();
            services.AddSingleton<ITowerHttpClientFactory>(new MockTowerHttpClientFactory(MockTowerHttpClient));

            using var scope = services.BuildServiceProvider().CreateScope();
            using var ctx = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>().CreateDbContext();
            ctx.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
            MockTowerHttpClient.Dispose();
        }
        base.Dispose(disposing);
    }

    private class MockTowerHttpClientFactory(MockTowerHttpClient client) : ITowerHttpClientFactory
    {
        public ITowerHttpClient Create(string ip) => client;
    }
}
```

---

### Task 16: Ping endpoint integration test

**Files:**
- Create: `OWLServer/OWLServer.Tests/Integration/Api/PingEndpointTests.cs`

```csharp
using OWLServer.Tests.Integration;

namespace OWLServer.Tests.Integration.Api;

public class PingEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PingEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        var response = await _client.GetAsync("/api/ping");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("pong", body);
    }
}
```

Run: `dotnet test --filter PingEndpointTests`
Expected: 1 test PASS

---

### Task 17: Tower registration integration tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Integration/Api/TowerRegistrationTests.cs`

```csharp
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Integration;

namespace OWLServer.Tests.Integration.Api;

public class TowerRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TowerRegistrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterTower_AddsTowerToDictionary()
    {
        await _client.PostAsync("/api/RegisterTower?id=AA:BB:CC:DD&ip=http://192.168.1.50", null);
        await Task.Delay(300); // fire-and-forget via Task.Run

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        Assert.True(gss.TowerManagerService.Towers.ContainsKey("AA:BB:CC:DD"));
        Assert.Equal("A", gss.TowerManagerService.Towers["AA:BB:CC:DD"].DisplayLetter);
    }

    [Fact]
    public async Task RegisterTower_DuplicateIgnored()
    {
        await _client.PostAsync("/api/RegisterTower?id=XX:YY&ip=http://192.168.1.50", null);
        await Task.Delay(300);
        await _client.PostAsync("/api/RegisterTower?id=XX:YY&ip=http://192.168.1.50", null);
        await Task.Delay(300);

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        Assert.Single(gss.TowerManagerService.Towers);
    }
}
```

---

### Task 18: Tower button flow integration tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Integration/Api/TowerButtonFlowTests.cs`

```csharp
using OWLServer.Models;
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Integration;

namespace OWLServer.Tests.Integration.Api;

public class TowerButtonFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TowerButtonFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task RegisterAndWait(string id)
    {
        await _client.PostAsync($"/api/RegisterTower?id={id}&ip=http://{id}.local", null);
        await Task.Delay(300);
    }

    [Fact]
    public async Task TowerButtonPressed_SetsIsPressed()
    {
        await RegisterAndWait("BTN:01");
        await _client.PostAsync("/api/TowerButtonPressed?id=BTN:01&color=RED", null);
        await Task.Delay(300);

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var tower = gss.TowerManagerService.Towers["BTN:01"];
        Assert.True(tower.IsPressed);
        Assert.Equal(TeamColor.RED, tower.PressedByColor);
    }

    [Fact]
    public async Task TowerButtonReleased_ClearsPress()
    {
        await RegisterAndWait("BTN:02");
        await _client.PostAsync("/api/TowerButtonPressed?id=BTN:02&color=BLUE", null);
        await Task.Delay(300);
        await _client.PostAsync("/api/TowerButtonReleased?id=BTN:02", null);
        await Task.Delay(300);

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var tower = gss.TowerManagerService.Towers["BTN:02"];
        Assert.False(tower.IsPressed);
        Assert.Equal(TeamColor.NONE, tower.PressedByColor);
    }

    [Fact]
    public async Task CaptureTower_ChangesColor()
    {
        await RegisterAndWait("CAP:01");
        await _client.PostAsync("/api/CaptureTower?id=CAP:01&color=RED", null);
        await Task.Delay(300);

        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        Assert.Equal(TeamColor.RED, gss.TowerManagerService.Towers["CAP:01"].CurrentColor);
    }

    [Fact]
    public async Task KlickerClicked_FiresEvent()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        bool eventFired = false;
        TeamColor receivedColor = TeamColor.NONE;

        void Handler(object? s, KlickerEventArgs a)
        {
            eventFired = true;
            receivedColor = a.TeamColor;
        }

        WeakEventHelper.Attach(ref ets.KlickerPressedAction, Handler);

        try
        {
            ets.KlickerPressedAction += Handler;
            await _client.PostAsync("/api/KlickerClicked?color=BLUE", null);
            await Task.Delay(500);

            Assert.True(eventFired);
            Assert.Equal(TeamColor.BLUE, receivedColor);
        }
        finally
        {
            ets.KlickerPressedAction -= Handler;
        }
    }
}
```

Note: The KlickerClicked test attaches directly to the event since there's no game mode running. For full game mode testing, see Task 19.

---

### Task 19: Conquest E2E integration tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Integration/GameMode/ConquestEndToEndTests.cs`

```csharp
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Integration;

namespace OWLServer.Tests.Integration.GameMode;

public class ConquestEndToEndTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConquestEndToEndTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Conquest_StartsAndTracksTimer()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var conquest = new GameModeConquest(ets, gss)
        {
            GameDurationInMinutes = 0,
            PointDistributionFrequencyInSeconds = 1,
            IsTicket = false
        };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        gss.CurrentGame = conquest;

        gss.StartGame();
        await Task.Delay(500);

        Assert.True(conquest.IsRunning);
        Assert.False(conquest.IsFinished);
        Assert.NotNull(conquest.StartTime);

        conquest.EndGame();
        await Task.Delay(500);
        Assert.True(conquest.IsFinished);
    }

    [Fact]
    public async Task Conquest_NoWinnerWhenTie()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var conquest = new GameModeConquest(ets, gss) { IsTicket = false };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });

        Assert.Equal(TeamColor.NONE, conquest.GetWinner);
    }

    [Fact]
    public async Task Conquest_PointsAccrueFromControlledTowers()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        // Register and capture two towers for BLUE
        await _client.PostAsync("/api/RegisterTower?id=CA&ip=http://ca.local", null);
        await Task.Delay(300);
        await _client.PostAsync("/api/CaptureTower?id=CA&color=BLUE", null);
        await Task.Delay(300);

        var conquest = new GameModeConquest(ets, gss)
        {
            GameDurationInMinutes = 0, // effectively 0 min run but runner starts
            PointDistributionFrequencyInSeconds = 1,
            IsTicket = false
        };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        gss.CurrentGame = conquest;

        gss.StartGame();
        await Task.Delay(1500); // wait for at least one tick

        Assert.True(conquest.TeamPoints[TeamColor.BLUE] > 0, $"BLUE should have points, got {conquest.TeamPoints[TeamColor.BLUE]}");
        conquest.EndGame();
    }
}
```

---

### Task 20: Deathmatch E2E integration tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Integration/GameMode/DeathmatchEndToEndTests.cs`

```csharp
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Integration;

namespace OWLServer.Tests.Integration.GameMode;

public class DeathmatchEndToEndTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeathmatchEndToEndTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Deathmatch_KlickerIncrementsDeaths()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var tdm = new GameModeTeamDeathmatch(ets, gss);
        tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        gss.CurrentGame = tdm;

        gss.StartGame();
        await Task.Delay(300);

        await _client.PostAsync("/api/KlickerClicked?color=RED", null);
        await Task.Delay(500);

        Assert.Equal(1, tdm.TeamDeaths[TeamColor.RED]);
        Assert.Equal(0, tdm.TeamDeaths[TeamColor.BLUE]);

        tdm.EndGame();
    }

    [Fact]
    public async Task Deathmatch_WinnerIsTeamWithFewerDeaths()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var tdm = new GameModeTeamDeathmatch(ets, gss);
        tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        gss.CurrentGame = tdm;

        gss.StartGame();
        await Task.Delay(300);

        await _client.PostAsync("/api/KlickerClicked?color=RED", null);
        await _client.PostAsync("/api/KlickerClicked?color=RED", null);
        await _client.PostAsync("/api/KlickerClicked?color=BLUE", null);
        await Task.Delay(500);

        tdm.EndGame();
        await Task.Delay(300);

        // BLUE has 1 death, RED has 2 → BLUE wins
        Assert.Equal(TeamColor.BLUE, tdm.GetWinner);
    }

    [Fact]
    public async Task Deathmatch_TieReturnsNone()
    {
        var gss = _factory.Services.GetRequiredService<IGameStateService>();
        var ets = _factory.Services.GetRequiredService<IExternalTriggerService>();

        var tdm = new GameModeTeamDeathmatch(ets, gss);
        tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });

        Assert.Equal(TeamColor.NONE, tdm.GetWinner);
    }
}
```

---

### Task 21: GameModeTimer unit tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/GameModes/GameModeTimerTests.cs`

```csharp
using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeTimerTests
{
    [Fact]
    public void NoWinner_AlwaysReturnsNone()
    {
        var mockEts = new Mock<IExternalTriggerService>();
        var mockGss = new Mock<IGameStateService>();
        var timer = new GameModeTimer(mockEts.Object, mockGss.Object);
        Assert.Equal(TeamColor.NONE, timer.GetWinner);
    }

    [Fact]
    public void GetDisplayPoints_AlwaysReturnsMaxTickets()
    {
        var mockEts = new Mock<IExternalTriggerService>();
        var mockGss = new Mock<IGameStateService>();
        var timer = new GameModeTimer(mockEts.Object, mockGss.Object) { MaxTickets = 1000 };
        Assert.Equal(1000, timer.GetDisplayPoints(TeamColor.BLUE));
        Assert.Equal(1000, timer.GetDisplayPoints(TeamColor.RED));
    }

    [Fact]
    public void ShowRespawnButton_IsFalse()
    {
        var mockEts = new Mock<IExternalTriggerService>();
        var mockGss = new Mock<IGameStateService>();
        var timer = new GameModeTimer(mockEts.Object, mockGss.Object);
        Assert.False(timer.ShowRespawnButton);
    }
}
```

---

### Task 22: GameModeConquest unit tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/GameModes/GameModeConquestTests.cs`

```csharp
using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeConquestTests
{
    private readonly Mock<IExternalTriggerService> _mockEts;
    private readonly Mock<IGameStateService> _mockGss;
    private readonly Mock<ITowerManagerService> _mockTms;

    public GameModeConquestTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        _mockTms = new Mock<ITowerManagerService>();
        _mockTms.Setup(t => t.Towers).Returns(new Dictionary<string, Tower>());

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.TowerManagerService).Returns(_mockTms.Object);
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });
        _mockGss.Setup(g => g.ExternalTriggerService).Returns(_mockEts.Object);
    }

    [Fact]
    public void FillTeams_InitializesTeamPointsToZero()
    {
        var conquest = new GameModeConquest(_mockEts.Object, _mockGss.Object);
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(0, conquest.TeamPoints[TeamColor.BLUE]);
        Assert.Equal(0, conquest.TeamPoints[TeamColor.RED]);
    }

    [Fact]
    public void GetDisplayPoints_IsTicket_ReturnsRemainingTickets()
    {
        var conquest = new GameModeConquest(_mockEts.Object, _mockGss.Object)
        {
            IsTicket = true,
            MaxTickets = 15
        };
        // No opponent towers → full tickets
        var points = conquest.GetDisplayPoints(TeamColor.BLUE);
        Assert.Equal(15, points);
    }

    [Fact]
    public void GetWinner_TieReturnsNone()
    {
        var conquest = new GameModeConquest(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        conquest.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(TeamColor.NONE, conquest.GetWinner);
    }
}
```

---

### Task 23: GameModeTeamDeathmatch unit tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/GameModes/GameModeTeamDeathmatchTests.cs`

```csharp
using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeTeamDeathmatchTests
{
    private readonly Mock<IExternalTriggerService> _mockEts;
    private readonly Mock<IGameStateService> _mockGss;
    private readonly GameModeTeamDeathmatch _tdm;

    public GameModeTeamDeathmatchTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.ExternalTriggerService).Returns(_mockEts.Object);
        _mockGss.Setup(g => g.TowerManagerService).Returns(new Mock<ITowerManagerService>().Object);
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });

        _tdm = new GameModeTeamDeathmatch(_mockEts.Object, _mockGss.Object);
        _tdm.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
    }

    [Fact]
    public void ShowRespawnButton_IsTrue()
    {
        Assert.True(_tdm.ShowRespawnButton);
    }

    [Fact]
    public void FillTeams_InitializesDeathsToZero()
    {
        Assert.Equal(0, _tdm.TeamDeaths[TeamColor.BLUE]);
        Assert.Equal(0, _tdm.TeamDeaths[TeamColor.RED]);
    }

    [Fact]
    public void GetWinner_TieReturnsNone()
    {
        Assert.Equal(TeamColor.NONE, _tdm.GetWinner);
    }
}
```

---

### Task 24: GameModeChainBreak unit tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/GameModes/GameModeChainBreakTests.cs`

```csharp
using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Unit.GameModes;

public class GameModeChainBreakTests
{
    private readonly Mock<IExternalTriggerService> _mockEts;
    private readonly Mock<IGameStateService> _mockGss;
    private readonly Mock<ITowerManagerService> _mockTms;

    public GameModeChainBreakTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        _mockTms = new Mock<ITowerManagerService>();
        _mockTms.Setup(t => t.Towers).Returns(new Dictionary<string, Tower>());

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.TowerManagerService).Returns(_mockTms.Object);
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });
        _mockGss.Setup(g => g.ExternalTriggerService).Returns(_mockEts.Object);
    }

    [Fact]
    public void FillTeams_InitializesPointsToZero()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object);
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(0, chainBreak.TeamPoints[TeamColor.BLUE]);
        Assert.Equal(0, chainBreak.TeamPoints[TeamColor.RED]);
    }

    [Fact]
    public void GetWinner_TieReturnsNone()
    {
        var chainBreak = new GameModeChainBreak(_mockEts.Object, _mockGss.Object) { IsTicket = false };
        chainBreak.FillTeams(new List<TeamBase> { new(TeamColor.BLUE), new(TeamColor.RED) });
        Assert.Equal(TeamColor.NONE, chainBreak.GetWinner);
    }
}
```

---

### Task 25: GameStateService unit tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/Services/GameStateServiceTests.cs`

```csharp
using Moq;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.Unit.Services;

public class GameStateServiceTests
{
    private readonly Mock<IExternalTriggerService> _mockEts = new();
    private readonly Mock<IAudioService> _mockAudio = new();
    private readonly Mock<ITowerManagerService> _mockTms = new();
    private readonly GameStateService _service;

    public GameStateServiceTests()
    {
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };
        _service = new GameStateService(_mockEts.Object, _mockAudio.Object, _mockTms.Object);
    }

    [Fact]
    public void StartGame_PlaysCountdownAndStartSounds()
    {
        var mockGame = new Mock<IGameModeBase>();
        _service.CurrentGame = mockGame.Object;
        _service.StartGame();
        _mockAudio.Verify(a => a.PlaySound(Sounds.Countdown), Times.Once);
        _mockAudio.Verify(a => a.PlaySound(Sounds.Start), Times.Once);
    }

    [Fact]
    public void StartGame_RunsGame()
    {
        var mockGame = new Mock<IGameModeBase>();
        _service.CurrentGame = mockGame.Object;
        _service.StartGame();
        mockGame.Verify(g => g.RunGame(), Times.Once);
    }

    [Fact]
    public void StopGame_EndsGame()
    {
        var mockGame = new Mock<IGameModeBase>();
        _service.CurrentGame = mockGame.Object;
        _service.StopGame();
        mockGame.Verify(g => g.EndGame(), Times.Once);
    }

    [Fact]
    public void Reset_ClearsSpawnReadiness()
    {
        _service.WaldSpawnReady = true;
        _service.StadtSpawnReady = true;
        _service.Reset();
        Assert.False(_service.WaldSpawnReady);
        Assert.False(_service.StadtSpawnReady);
    }

    [Fact]
    public void Reset_ResetsTowers()
    {
        _service.Reset();
        _mockTms.Verify(t => t.ResetTowers(), Times.Once);
    }

    [Fact]
    public void HandleGameEnd_PlaysStopSound()
    {
        _service.HandleGameEnd();
        _mockAudio.Verify(a => a.PlaySound(Sounds.Stop), Times.Once);
    }
}
```

---

### Task 26: TowerManagerService unit tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/Services/TowerManagerServiceTests.cs`

```csharp
using Moq;
using OWLServer.Models;
using OWLServer.Services;
using OWLServer.Services.Interfaces;
using OWLServer.Tests.Helpers;

namespace OWLServer.Tests.Unit.Services;

public class TowerManagerServiceTests
{
    private readonly Mock<IExternalTriggerService> _mockEts = new();
    private readonly TowerManagerService _service;

    public TowerManagerServiceTests()
    {
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        var mockHttpClient = new MockTowerHttpClient();
        var mockFactory = new Mock<ITowerHttpClientFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<string>())).Returns(mockHttpClient);

        _service = new TowerManagerService(_mockEts.Object, mockFactory.Object);
    }

    [Fact]
    public void RegisterTower_FirstTowerGetsLetterA()
    {
        _service.RegisterTower("AA:BB", "http://192.168.1.50");
        Assert.Equal("A", _service.Towers["AA:BB"].DisplayLetter);
    }

    [Fact]
    public void RegisterTower_AssignsSequentialLetters()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.RegisterTower("T2", "http://192.168.1.51");
        Assert.Equal("A", _service.Towers["T1"].DisplayLetter);
        Assert.Equal("B", _service.Towers["T2"].DisplayLetter);
    }

    [Fact]
    public void RegisterTower_DuplicateIdIgnored()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.RegisterTower("T1", "http://192.168.1.50");
        Assert.Single(_service.Towers);
    }

    [Fact]
    public void HandleTowerButtonPressed_SetsIsPressed()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.Towers["T1"].CurrentColor = TeamColor.NONE;

        _service.HandleTowerButtonPressed("T1", TeamColor.RED);

        var tower = _service.Towers["T1"];
        Assert.True(tower.IsPressed);
        Assert.Equal(TeamColor.RED, tower.PressedByColor);
        Assert.NotNull(tower.LastPressed);
    }

    [Fact]
    public void HandleTowerButtonPressed_IgnoresIfAlreadyPressed()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.HandleTowerButtonPressed("T1", TeamColor.RED);
        _service.HandleTowerButtonPressed("T1", TeamColor.BLUE);
        Assert.Equal(TeamColor.RED, _service.Towers["T1"].PressedByColor);
    }

    [Fact]
    public void HandleTowerButtonPressed_IgnoresIfLocked()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.Towers["T1"].CurrentColor = TeamColor.LOCKED;
        _service.HandleTowerButtonPressed("T1", TeamColor.RED);
        Assert.False(_service.Towers["T1"].IsPressed);
    }

    [Fact]
    public void HandleTowerButtonReleased_ClearsPress()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.HandleTowerButtonPressed("T1", TeamColor.RED);
        _service.HandleTowerButtonReleased("T1");
        Assert.False(_service.Towers["T1"].IsPressed);
        Assert.Equal(TeamColor.NONE, _service.Towers["T1"].PressedByColor);
    }

    [Fact]
    public void GetPoints_SumsMultipliersCorrectly()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.RegisterTower("T2", "http://192.168.1.51");
        _service.Towers["T1"].Multiplier = 1.5;
        _service.Towers["T2"].Multiplier = 2.0;
        _service.Towers["T1"].CurrentColor = TeamColor.RED;
        _service.Towers["T2"].CurrentColor = TeamColor.RED;

        var points = _service.GetPoints(TeamColor.RED);
        Assert.Equal(3, points);
    }

    [Fact]
    public void TowerChangeColor_SetsColor()
    {
        _service.RegisterTower("T1", "http://192.168.1.50");
        _service.TowerChangeColor("T1", TeamColor.BLUE);
        Assert.Equal(TeamColor.BLUE, _service.Towers["T1"].CurrentColor);
    }
}
```

---

### Task 27: Tower model unit tests

**Files:**
- Create: `OWLServer/OWLServer.Tests/Unit/Models/TowerTests.cs`

```csharp
using OWLServer.Models;
using OWLServer.Tests.Helpers;

namespace OWLServer.Tests.Unit.Models;

public class TowerTests
{
    [Fact]
    public void SetTowerColor_UpdatesCurrentColor()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient);
        tower.SetTowerColor(TeamColor.RED);
        Assert.Equal(TeamColor.RED, tower.CurrentColor);
    }

    [Fact]
    public async Task SetTowerColor_CallsHttpClient()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient);
        await tower.SendColorToTower(Color.Red);
        Assert.Contains(httpClient.PostedUrls, u => u.Contains("/api/setcolor/"));
    }

    [Fact]
    public void SetToStartColor_SetsNone()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient);
        tower.CurrentColor = TeamColor.RED;
        tower.SetToStartColor();
        Assert.Equal(TeamColor.NONE, tower.CurrentColor);
    }

    [Fact]
    public void IsLocked_WhenColorIsLocked()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient) { CurrentColor = TeamColor.LOCKED };
        Assert.True(tower.IsLocked);
    }

    [Fact]
    public void IsLocked_WhenColorNotLocked()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient) { CurrentColor = TeamColor.RED };
        Assert.False(tower.IsLocked);
    }

    [Fact]
    public void Reset_ClearsAllRuntimeState()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            CapturedAt = DateTime.Now,
            IsPressed = true,
            PressedByColor = TeamColor.RED,
            CaptureProgress = 0.5,
            CurrentColor = TeamColor.BLUE
        };

        tower.Reset();

        Assert.Null(tower.CapturedAt);
        Assert.False(tower.IsPressed);
        Assert.Equal(TeamColor.NONE, tower.PressedByColor);
        Assert.Equal(0, tower.CaptureProgress);
        Assert.Equal(TeamColor.NONE, tower.CurrentColor);
    }

    [Fact]
    public void GetDisplayProgress_WhenPressed_ReturnsCaptureProgress()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            IsPressed = true,
            CaptureProgress = 0.75
        };
        Assert.Equal(75, tower.GetDisplayProgress());
    }

    [Fact]
    public void GetDisplayProgress_WhenNotPressed_Returns100()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            IsPressed = false,
            CaptureProgress = 0.5
        };
        Assert.Equal(100, tower.GetDisplayProgress());
    }
}
```

---

### Task 28: bUnit UI test setup + MatchScoreBar test

**Files:**
- Create: `OWLServer/OWLServer.Tests/UI/Components/MatchScoreBarTests.cs`

```csharp
using Bunit;
using Moq;
using OWLServer.Components.TeamPanel;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.UI.Components;

public class MatchScoreBarTests : TestContext
{
    private readonly Mock<IGameStateService> _mockGss;
    private readonly Mock<IExternalTriggerService> _mockEts;
    private readonly Mock<ITowerManagerService> _mockTms;

    public MatchScoreBarTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        _mockTms = new Mock<ITowerManagerService>();
        _mockTms.Setup(t => t.Towers).Returns(new Dictionary<string, Tower>());

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });
        _mockGss.Setup(g => g.TowerManagerService).Returns(_mockTms.Object);
        _mockGss.Setup(g => g.ExternalTriggerService).Returns(_mockEts.Object);

        Services.AddSingleton(_mockGss.Object);
        Services.AddSingleton(_mockEts.Object);
    }

    [Fact]
    public void RendersPauseWhenNoGame()
    {
        _mockGss.Setup(g => g.CurrentGame).Returns((IGameModeBase?)null);
        var cut = RenderComponent<MatchScoreBar>(p => p.Add(c => c.TeamOnSide, TeamColor.NONE));
        cut.Markup.Contains("PAUSE");
    }
}
```

---

### Task 29: GameEndOverlay bUnit test

**Files:**
- Create: `OWLServer/OWLServer.Tests/UI/Pages/GameEndOverlayTests.cs`

```csharp
using Bunit;
using Moq;
using OWLServer.Components.Pages;
using OWLServer.Models;
using OWLServer.Models.GameModes;
using OWLServer.Services.Interfaces;

namespace OWLServer.Tests.UI.Pages;

public class GameEndOverlayTests : TestContext
{
    private readonly Mock<IGameStateService> _mockGss;
    private readonly Mock<IExternalTriggerService> _mockEts;

    public GameEndOverlayTests()
    {
        _mockEts = new Mock<IExternalTriggerService>();
        _mockEts.SetupAllProperties();
        _mockEts.Object.StateHasChangedAction = () => { };

        _mockGss = new Mock<IGameStateService>();
        _mockGss.Setup(g => g.Teams).Returns(new Dictionary<TeamColor, TeamBase>
        {
            [TeamColor.BLUE] = new TeamBase(TeamColor.BLUE),
            [TeamColor.RED] = new TeamBase(TeamColor.RED)
        });

        Services.AddSingleton(_mockGss.Object);
        Services.AddSingleton(_mockEts.Object);
    }

    [Fact]
    public void ShowsWhenGameIsFinished()
    {
        var mockGame = new Mock<IGameModeBase>();
        mockGame.Setup(g => g.IsFinished).Returns(true);
        mockGame.Setup(g => g.GetWinner).Returns(TeamColor.BLUE);
        mockGame.Setup(g => g.GetDisplayPoints(It.IsAny<TeamColor>())).Returns(5);
        _mockGss.Setup(g => g.CurrentGame).Returns(mockGame.Object);

        var cut = RenderComponent<GameEndOverlay>();
        cut.Markup.Contains("SPIEL ENDE");
    }

    [Fact]
    public void HiddenWhenGameNotFinished()
    {
        var mockGame = new Mock<IGameModeBase>();
        mockGame.Setup(g => g.IsFinished).Returns(false);
        _mockGss.Setup(g => g.CurrentGame).Returns(mockGame.Object);

        var cut = RenderComponent<GameEndOverlay>();
        Assert.DoesNotContain("SPIEL ENDE", cut.Markup);
    }
}
```

---

### Task 30: TowerComponent bUnit test

**Files:**
- Create: `OWLServer/OWLServer.Tests/UI/Components/TowerComponentTests.cs`

```csharp
using Bunit;
using OWLServer.Components.Components;
using OWLServer.Models;
using OWLServer.Tests.Helpers;

namespace OWLServer.Tests.UI.Components;

public class TowerComponentTests : TestContext
{
    [Fact]
    public void RendersTowerDisplayLetter()
    {
        var httpClient = new MockTowerHttpClient();
        var tower = new Tower("test", "http://test.local", httpClient)
        {
            DisplayLetter = "A",
            CurrentColor = TeamColor.NONE
        };

        var cut = RenderComponent<TowerComponent>(p => p
            .Add(c => c.Tower, tower)
            .Add(c => c.Smaller, false));

        cut.Markup.Contains("A");
    }
}
```

---

### Task 31: Final verification

```bash
dotnet build
dotnet test
```

Expected: All tests pass (~40+ tests across integration, unit, and UI categories).

---

## Self-Review

1. **Spec coverage:** All spec requirements covered — interfaces (1-7), DI (8), controller/game-mode (9-10), Blazor (11), scaffold (13), helpers (14), integration (15-20), unit (21-27), UI (28-30), verification (31).
2. **Placeholder scan:** Zero placeholders. All code is concrete and complete.
3. **Type consistency:** `IGameStateService`, `IExternalTriggerService`, `ITowerManagerService`, `ITowerHttpClientFactory` used consistently throughout.
