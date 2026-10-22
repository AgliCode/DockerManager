using Docker.DotNet;
using DockerManager.Components;
using DockerManager.Services;
using MudBlazor.Services;
using System.Runtime.InteropServices;
using DockerManager.Data;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
//Nasłuchiwanie na żywo
builder.Services.AddSingleton<ContainerMonitorService>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<ContainerMonitorService>());
//Baza danych do zbudowania historii
var historyDbPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "history.db");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={historyDbPath}"));

DockerClientConfiguration dockerConfig;
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    dockerConfig = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine"));
}
else
{
    dockerConfig = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"));
}
builder.Services.AddSingleton<IDockerClient>(dockerConfig.CreateClient());

var app = builder.Build();
//Utworzenie bazy danych
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
