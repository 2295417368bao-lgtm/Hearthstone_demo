using HearthstoneServer.Web.Hubs;
using HearthstoneServer;
using HearthstoneServer.Dto;

using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Initialize Serilog before anything else
var logFile = Environment.GetEnvironmentVariable("HS_LOG_FILE");
if (string.IsNullOrWhiteSpace(logFile) && OperatingSystem.IsWindows())
	logFile = @"d:\hs\fireplace\fireplace\debug.log";

if (!string.IsNullOrWhiteSpace(logFile))
{
	var dir = Path.GetDirectoryName(logFile);
	if (!string.IsNullOrWhiteSpace(dir))
		Directory.CreateDirectory(dir);
}

HearthstoneServer.LogConfig.Init(
	logFile: string.IsNullOrWhiteSpace(logFile) ? null : logFile,
	minimumLevel: LogEventLevel.Information,
	enableConsole: true);

// Load card database at startup
HearthstoneServer.CardDatabase.Initialize();

builder.Services.AddSignalR();

var app = builder.Build();

// Serve static files from wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

// SignalR game hub
app.MapHub<GameHub>("/gamehub");

// Health check
app.MapGet("/api/health", () => "ok");

// All card data (for frontend caching — too large for SignalR)
app.MapGet("/api/cards", () => Results.Json(GameHub.GetAllCardDataStatic()));

// Debug: create a test game and return state JSON
app.MapGet("/api/debug/state", () =>
{
	var ctrl = GameSetup.CreateAIGame("Mage", "Hunter");
	var state = ctrl.GetState(ctrl.Player1);
	return Results.Json(state);
});

Console.WriteLine("🎮 Hearthstone Web Server starting...");
Console.WriteLine("   Open http://localhost:5000 in your browser");

app.Run("http://localhost:5000");
