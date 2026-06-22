using System;
using System.Collections.Generic;
using WorldOfBlazorCraft.Shared.Engine.Entities;
using WorldOfBlazorCraft.Shared.Engine.Physics;
using WorldOfBlazorCraft.Shared.Types;

namespace WorldOfBlazorCraft.Shared.Engine.Sim.Systems
{
    public static class MovementSystem
    {
        public const double RunSpeed = 7.0; // yards/sec
        public const double TurnSpeed = Math.PI; // rad/sec keyboard turning
        public const double BackpedalMult = 0.65;
        public const double SwimSpeedMult = 0.65;
        public const double Gravity = 16.0;
        public const double JumpVelocity = 6.0;
        public const double FallSafeDistance = 12.0;

        public const double BodyRadius = 0.5;
        public const double MaxClimbSlope = 1.5;
        public const double SwimDepth = 0.8;
        public static readonly double SwimSurfaceY = Terrain.WaterLevel - 0.75; // -5.25

        public static double NormAngle(double angle)
        {
            double a = (angle + Math.PI) % (2.0 * Math.PI);
            if (a < 0.0) a += 2.0 * Math.PI;
            return a - Math.PI;
        }

        public static bool IsSwimming(Vec3 pos, uint seed)
        {
            return Terrain.GroundHeight(pos.X, pos.Z, seed) < Terrain.WaterLevel - SwimDepth
                && pos.Y <= SwimSurfaceY + 0.15;
        }

        public static void Update(Entity entity, double dt, uint seed, ColliderGrid grid, List<FenceSegment> fences)
        {
            if (entity.Dead) return;

            // Save previous state
            entity.PrevPos = entity.Pos;
            entity.PrevFacing = entity.Facing;

            var phys = entity.GetComponent<PhysicsComponent>();
            var input = entity.GetComponent<InputComponent>();
            if (phys == null || input == null) return;

            var inp = input.MoveInput;

            // Keyboard turning
            bool isStunned = false; // Status hooks can be added here later
            bool isRooted = false;

            if (!isStunned)
            {
                if (inp.TurnLeft)
                {
                    entity.Facing = NormAngle(entity.Facing + TurnSpeed * dt);
                }
                if (inp.TurnRight)
                {
                    entity.Facing = NormAngle(entity.Facing - TurnSpeed * dt);
                }
            }

            double mx = 0.0;
            double mz = 0.0;
            if (inp.Forward) mz += 1.0;
            if (inp.Back) mz -= 1.0;
            if (inp.StrafeLeft) mx -= 1.0;
            if (inp.StrafeRight) mx += 1.0;

            bool hasMoveInput = (mx != 0.0 || mz != 0.0);
            bool moving = hasMoveInput && !isRooted;
            bool swimming = IsSwimming(entity.Pos, seed);

            double wishX = 0.0;
            double wishZ = 0.0;
            double wishSpeed = 0.0;

            if (moving)
            {
                double len = Math.Sqrt(mx * mx + mz * mz);
                mx /= len;
                mz /= len;

                double speed = RunSpeed;
                if (mz < 0.0) speed *= BackpedalMult;
                if (swimming) speed *= SwimSpeedMult;

                double sin = Math.Sin(entity.Facing);
                double cos = Math.Cos(entity.Facing);

                // wx = mz * sin - mx * cos
                // wz = mz * cos + mx * sin
                wishX = mz * sin - mx * cos;
                wishZ = mz * cos + mx * sin;
                wishSpeed = speed;
            }

            bool movingOnGround = moving && (phys.OnGround || swimming);
            if (movingOnGround || (!phys.OnGround && (phys.Vx != 0.0 || phys.Vz != 0.0)))
            {
                double stepX = movingOnGround ? wishX * wishSpeed : phys.Vx;
                double stepZ = movingOnGround ? wishZ * wishSpeed : phys.Vz;

                double nx = entity.Pos.X + stepX * dt;
                double nz = entity.Pos.Z + stepZ * dt;

                // Cliffs are walls, not ramps
                if (phys.OnGround && !swimming)
                {
                    double h0 = Terrain.GroundHeight(entity.Pos.X, entity.Pos.Z, seed);
                    double h1 = Terrain.GroundHeight(nx, nz, seed);
                    double run = Math.Sqrt((nx - entity.Pos.X) * (nx - entity.Pos.X) + (nz - entity.Pos.Z) * (nz - entity.Pos.Z));

                    if (h1 > h0 && run > 1e-5 && (h1 - h0) / run > MaxClimbSlope)
                    {
                        nx = entity.Pos.X;
                        nz = entity.Pos.Z;
                        if (!phys.OnGround)
                        {
                            phys.Vx = 0.0;
                            phys.Vz = 0.0;
                        }
                    }
                }

                // Pass through fences while airborne during a jump
                bool clearFences = !phys.OnGround && phys.Jumping;

                Vec3 resolved = CollisionSolver.ResolveMovement(grid, fences, entity.Pos, new Vec3(nx, entity.Pos.Y, nz), BodyRadius, clearFences);
                entity.Pos = new Vec3(resolved.X, entity.Pos.Y, resolved.Z);

                if (!phys.OnGround && (resolved.X != nx || resolved.Z != nz))
                {
                    phys.Vx = (resolved.X - entity.PrevPos.X) / dt;
                    phys.Vz = (resolved.Z - entity.PrevPos.Z) / dt;
                }
            }

            // Vertical: jumping, gravity, swimming, fall damage
            double ground = Terrain.GroundHeight(entity.Pos.X, entity.Pos.Z, seed);
            bool deepWater = ground < Terrain.WaterLevel - SwimDepth;

            if (deepWater && entity.Pos.Y <= SwimSurfaceY + 0.05)
            {
                entity.Pos = new Vec3(entity.Pos.X, SwimSurfaceY, entity.Pos.Z);
                phys.Vy = 0.0;
                phys.Vx = 0.0;
                phys.Vz = 0.0;
                phys.OnGround = true;
                phys.Jumping = false;
                phys.FallStartY = entity.Pos.Y;

                if (inp.Jump && !isRooted)
                {
                    // Small hop to climb onto shores and docks
                    phys.Vy = JumpVelocity * 0.7;
                    phys.Vx = wishX * wishSpeed;
                    phys.Vz = wishZ * wishSpeed;
                    phys.OnGround = false;
                    phys.Jumping = true;
                }
                return;
            }

            if (inp.Jump && phys.OnGround && !isRooted)
            {
                phys.Vy = JumpVelocity;
                phys.Vx = wishX * wishSpeed;
                phys.Vz = wishZ * wishSpeed;
                phys.OnGround = false;
                phys.Jumping = true;
                phys.FallStartY = entity.Pos.Y;
            }

            if (!phys.OnGround)
            {
                phys.Vy -= Gravity * dt;
                entity.Pos = new Vec3(entity.Pos.X, entity.Pos.Y + phys.Vy * dt, entity.Pos.Z);
                phys.FallStartY = Math.Max(phys.FallStartY, entity.Pos.Y);

                if (deepWater && entity.Pos.Y <= SwimSurfaceY)
                {
                    entity.Pos = new Vec3(entity.Pos.X, SwimSurfaceY, entity.Pos.Z);
                    phys.Vy = 0.0;
                    phys.Vx = 0.0;
                    phys.Vz = 0.0;
                    phys.OnGround = true;
                    phys.Jumping = false;
                    phys.FallStartY = entity.Pos.Y;
                    return;
                }

                if (entity.Pos.Y <= ground)
                {
                    entity.Pos = new Vec3(entity.Pos.X, ground, entity.Pos.Z);
                    phys.Vy = 0.0;
                    phys.Vx = 0.0;
                    phys.Vz = 0.0;
                    phys.OnGround = true;
                    phys.Jumping = false;

                    double drop = phys.FallStartY - ground;
                    if (drop > FallSafeDistance)
                    {
                        var hp = entity.GetComponent<HealthComponent>();
                        if (hp != null)
                        {
                            int dmg = (int)Math.Round(hp.MaxHp * (drop - FallSafeDistance) * 0.07);
                            if (dmg > 0)
                            {
                                hp.Hp = Math.Max(0.0, hp.Hp - dmg);
                                if (hp.Hp == 0.0)
                                {
                                    entity.Dead = true;
                                }
                            }
                        }
                    }
                    phys.FallStartY = ground;
                }
            }
            else
            {
                double run = Math.Sqrt((entity.Pos.X - entity.PrevPos.X) * (entity.Pos.X - entity.PrevPos.X) + (entity.Pos.Z - entity.PrevPos.Z) * (entity.Pos.Z - entity.PrevPos.Z));
                double maxStepDown = 0.4 + run * MaxClimbSlope;

                if (ground < entity.Pos.Y - maxStepDown)
                {
                    phys.OnGround = false;
                    phys.Jumping = false;
                    phys.Vx = 0.0;
                    phys.Vz = 0.0;
                    phys.Vy = 0.0;
                    phys.FallStartY = entity.Pos.Y;
                }
                else
                {
                    entity.Pos = new Vec3(entity.Pos.X, ground, entity.Pos.Z);
                    phys.FallStartY = ground;
                }
            }
        }
    }
}
