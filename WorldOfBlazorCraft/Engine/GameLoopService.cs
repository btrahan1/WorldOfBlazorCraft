using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace WorldOfBlazorCraft.Engine
{
    public class GameLoopService : BackgroundService
    {
        private readonly WorldManager _worldManager;

        public GameLoopService(WorldManager worldManager)
        {
            _worldManager = worldManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _worldManager.Tick(0.05); // Enforce deterministic sim tick duration (50ms)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Game loop exception: {ex.Message}");
                }
            }
        }
    }
}
