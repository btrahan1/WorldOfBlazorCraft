using WorldOfBlazorCraft.Client.Pages;
using WorldOfBlazorCraft.Components;
using System.Net.WebSockets;
using System.Text;
using WorldOfBlazorCraft.Engine;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddSingleton<WorldManager>();
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var worldManager = context.RequestServices.GetRequiredService<WorldManager>();
        await HandleWebSocketConnection(webSocket, worldManager);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(WorldOfBlazorCraft.Client._Imports).Assembly);

app.Run();

async Task HandleWebSocketConnection(WebSocket webSocket, WorldManager worldManager)
{
    var player = worldManager.AddPlayer(webSocket);
    var buffer = new byte[1024 * 4];
    while (webSocket.State == WebSocketState.Open)
    {
        try
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (message.Contains("\"t\":\"ping\""))
                {
                    var responseBytes = Encoding.UTF8.GetBytes("{\"t\":\"pong\"}");
                    await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    worldManager.ProcessInput(webSocket, message);
                }
            }
        }
        catch
        {
            break;
        }
    }
    worldManager.RemovePlayer(webSocket);
}
