using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OWLServer.Components;
using OWLServer.Context;
using OWLServer.Services;
using OWLServer.Services.Interfaces;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<DatabaseContext>(options => options.UseSqlite("Data Source=OWLAirsoft.db"));

builder.Services.AddSingleton<ExternalTriggerService>();
builder.Services.AddSingleton<IExternalTriggerService>(sp => sp.GetRequiredService<ExternalTriggerService>());
builder.Services.AddSingleton<AudioService>();
builder.Services.AddSingleton<IAudioService>(sp => sp.GetRequiredService<AudioService>());
builder.Services.AddSingleton<TowerManagerService>();
builder.Services.AddSingleton<ITowerManagerService>(sp => sp.GetRequiredService<TowerManagerService>());
builder.Services.AddSingleton<MapService>();
builder.Services.AddSingleton<IMapService>(sp => sp.GetRequiredService<MapService>());
builder.Services.AddSingleton<GameStateService>();
builder.Services.AddSingleton<IGameStateService>(sp => sp.GetRequiredService<GameStateService>());
builder.Services.AddSingleton<ITowerHttpClientFactory, TowerHttpClientFactory>();
builder.Services.AddControllers();

builder.Services.AddRadzenComponents();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions()
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();


app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();


