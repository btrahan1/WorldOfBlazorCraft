using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WorldOfBlazorCraft.Shared.Engine.Entities;
using WorldOfBlazorCraft.Shared.Engine.Physics;
using WorldOfBlazorCraft.Shared.Engine.Sim.Systems;
using WorldOfBlazorCraft.Shared.Types;

namespace WorldOfBlazorCraft.Engine
{
    public class WorldManager
    {
        private readonly object _lock = new object();
        private readonly Dictionary<WebSocket, Entity> _players = new Dictionary<WebSocket, Entity>();
        private readonly List<Entity> _entities = new List<Entity>();
        private int _nextEntityId = 1;
        private uint _tickCount = 0;

        private readonly ColliderGrid _emptyGrid = new ColliderGrid(new List<Collider>());
        private readonly List<FenceSegment> _emptyFences = new List<FenceSegment>();

        public Entity AddPlayer(WebSocket ws)
        {
            lock (_lock)
            {
                var id = _nextEntityId++;
                var player = new Entity
                {
                    Id = id,
                    Kind = "player",
                    TemplateId = "player",
                    Name = $"Player_{id}",
                    Level = 1,
                    Pos = new Vec3(2, 0, -2), // Starts at PLAYER_START
                    Facing = 0
                };

                // Clamp to terrain ground height
                player.Pos = new Vec3(player.Pos.X, Terrain.GroundHeight(player.Pos.X, player.Pos.Z, 42), player.Pos.Z);
                player.PrevPos = player.Pos;

                player.AddComponent(new PhysicsComponent
                {
                    OnGround = true,
                    FallStartY = player.Pos.Y
                });
                player.AddComponent(new InputComponent());
                player.AddComponent(new HealthComponent
                {
                    Hp = 100,
                    MaxHp = 100
                });

                _players[ws] = player;
                _entities.Add(player);
                return player;
            }
        }

        public void RemovePlayer(WebSocket ws)
        {
            lock (_lock)
            {
                if (_players.TryGetValue(ws, out var player))
                {
                    _entities.Remove(player);
                    _players.Remove(ws);
                }
            }
        }

        private static bool GetBool(JsonElement elem, string name, string compact)
        {
            if (elem.TryGetProperty(name, out var p))
            {
                return p.ValueKind == JsonValueKind.True || (p.ValueKind == JsonValueKind.Number && p.GetInt32() == 1);
            }
            if (elem.TryGetProperty(compact, out p))
            {
                return p.ValueKind == JsonValueKind.True || (p.ValueKind == JsonValueKind.Number && p.GetInt32() == 1);
            }
            return false;
        }

        public void ProcessInput(WebSocket ws, string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (root.TryGetProperty("t", out var tProp) && tProp.GetString() == "input")
                {
                    lock (_lock)
                    {
                        if (_players.TryGetValue(ws, out var player))
                        {
                            var inputComp = player.GetComponent<InputComponent>();
                            if (inputComp != null)
                            {
                                if (root.TryGetProperty("mi", out var miProp) && miProp.ValueKind == JsonValueKind.Object)
                                {
                                    var mi = inputComp.MoveInput;
                                    mi.Forward = GetBool(miProp, "forward", "f");
                                    mi.Back = GetBool(miProp, "back", "b");
                                    mi.TurnLeft = GetBool(miProp, "turnLeft", "tl");
                                    mi.TurnRight = GetBool(miProp, "turnRight", "tr");
                                    mi.StrafeLeft = GetBool(miProp, "strafeLeft", "sl");
                                    mi.StrafeRight = GetBool(miProp, "strafeRight", "sr");
                                    mi.Jump = GetBool(miProp, "jump", "j");
                                }
                                if (root.TryGetProperty("facing", out var facingProp) && facingProp.ValueKind == JsonValueKind.Number)
                                {
                                    player.Facing = facingProp.GetDouble();
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silence json parse errors
            }
        }

        public async Task Tick(double dt)
        {
            List<KeyValuePair<WebSocket, Entity>> activePlayers;
            lock (_lock)
            {
                _tickCount++;
                foreach (var e in _entities)
                {
                    if (e.Dead) continue;
                    var phys = e.GetComponent<PhysicsComponent>();
                    var input = e.GetComponent<InputComponent>();
                    if (phys != null && input != null)
                    {
                        MovementSystem.Update(e, dt, 42, _emptyGrid, _emptyFences);
                    }
                }

                activePlayers = new List<KeyValuePair<WebSocket, Entity>>(_players);
            }

            // Broadcast snapshots to all active WebSockets outside the lock to prevent deadlocks
            foreach (var kvp in activePlayers)
            {
                var ws = kvp.Key;
                var player = kvp.Value;

                if (ws.State == WebSocketState.Open)
                {
                    var snapJson = GetSnapshotsJson(player);
                    var bytes = Encoding.UTF8.GetBytes(snapJson);
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        // Cleaned up when the receive loop fails
                    }
                }
            }
        }

        private string GetSnapshotsJson(Entity selfPlayer)
        {
            var entsList = new List<string>();
            foreach (var e in _entities)
            {
                if (e.Id == selfPlayer.Id) continue;
                var itemJson = $"{{\"id\":{e.Id},\"k\":\"{e.Kind}\",\"tid\":\"{e.TemplateId}\",\"nm\":\"{e.Name}\",\"lv\":{e.Level},\"x\":{Math.Round(e.Pos.X, 2)},\"y\":{Math.Round(e.Pos.Y, 2)},\"z\":{Math.Round(e.Pos.Z, 2)},\"f\":{Math.Round(e.Facing, 2)},\"hp\":100,\"mhp\":100}}";
                entsList.Add(itemJson);
            }

            var selfJson = $"{{\"id\":{selfPlayer.Id},\"k\":\"{selfPlayer.Kind}\",\"tid\":\"{selfPlayer.TemplateId}\",\"nm\":\"{selfPlayer.Name}\",\"lv\":{selfPlayer.Level},\"x\":{Math.Round(selfPlayer.Pos.X, 2)},\"y\":{Math.Round(selfPlayer.Pos.Y, 2)},\"z\":{Math.Round(selfPlayer.Pos.Z, 2)},\"f\":{Math.Round(selfPlayer.Facing, 2)},\"hp\":100,\"mhp\":100}}";

            return $"{{\"t\":\"snap\",\"tick\":{_tickCount},\"time\":{Math.Round(_tickCount * 0.05, 2)},\"self\":{selfJson},\"ents\":[{string.Join(",", entsList)}]}}";
        }
    }
}
