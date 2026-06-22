using System;
using System.Collections.Generic;
using WorldOfBlazorCraft.Shared.Types;

namespace WorldOfBlazorCraft.Shared.Engine.Physics
{
    public struct FenceSegment
    {
        public double X1 { get; set; }
        public double Z1 { get; set; }
        public double X2 { get; set; }
        public double Z2 { get; set; }

        public FenceSegment(double x1, double z1, double x2, double z2)
        {
            X1 = x1;
            Z1 = z1;
            X2 = x2;
            Z2 = z2;
        }
    }

    public class ColliderGrid
    {
        private const double GridCell = 16.0;
        private const double MaxBodyRadius = 0.8;

        public Dictionary<string, List<Collider>> Cells { get; } = new Dictionary<string, List<Collider>>();

        public ColliderGrid(IEnumerable<Collider> colliders)
        {
            foreach (var c in colliders)
            {
                var (minX, maxX, minZ, maxZ) = GetColliderBounds(c);
                int x0 = (int)Math.Floor((minX - MaxBodyRadius) / GridCell);
                int x1 = (int)Math.Floor((maxX + MaxBodyRadius) / GridCell);
                int z0 = (int)Math.Floor((minZ - MaxBodyRadius) / GridCell);
                int z1 = (int)Math.Floor((maxZ + MaxBodyRadius) / GridCell);

                for (int gx = x0; gx <= x1; gx++)
                {
                    for (int gz = z0; gz <= z1; gz++)
                    {
                        string key = $"{gx},{gz}";
                        if (!Cells.TryGetValue(key, out var list))
                        {
                            list = new List<Collider>();
                            Cells[key] = list;
                        }
                        list.Add(c);
                    }
                }
            }
        }

        private (double minX, double maxX, double minZ, double maxZ) GetColliderBounds(Collider c)
        {
            if (c is CircleCollider circle)
            {
                return (c.X - circle.R, c.X + circle.R, c.Z - circle.R, c.Z + circle.R);
            }
            if (c is ObbCollider obb)
            {
                double ext = Math.Sqrt(obb.Hw * obb.Hw + obb.Hd * obb.Hd);
                return (c.X - ext, c.X + ext, c.Z - ext, c.Z + ext);
            }
            return (c.X, c.X, c.Z, c.Z);
        }

        public List<Collider> GetCollidersInCell(double x, double z)
        {
            int gx = (int)Math.Floor(x / GridCell);
            int gz = (int)Math.Floor(z / GridCell);
            string key = $"{gx},{gz}";
            if (Cells.TryGetValue(key, out var list))
            {
                return list;
            }
            return new List<Collider>();
        }
    }

    public static class CollisionSolver
    {
        public static Vec3 RotateY(double lx, double lz, double rot)
        {
            double c = Math.Cos(rot);
            double s = Math.Sin(rot);
            return new Vec3(lx * c + lz * s, 0, -lx * s + lz * c);
        }

        public static Vec3? PushOut(Collider c, double x, double z, double r)
        {
            if (c is CircleCollider circle)
            {
                double dx = x - c.X;
                double dz = z - c.Z;
                double min = circle.R + r;
                double d2 = dx * dx + dz * dz;
                if (d2 >= min * min) return null;
                double d = Math.Sqrt(d2);
                if (d < 1e-6)
                {
                    return new Vec3(c.X + min, 0, c.Z);
                }
                double k = min / d;
                return new Vec3(c.X + dx * k, 0, c.Z + dz * k);
            }
            if (c is ObbCollider obb)
            {
                Vec3 local = RotateY(x - c.X, z - c.Z, -obb.Rot);
                double ex = obb.Hw + r;
                double ez = obb.Hd + r;
                if (Math.Abs(local.X) >= ex || Math.Abs(local.Z) >= ez) return null;

                double pushX = ex - Math.Abs(local.X);
                double pushZ = ez - Math.Abs(local.Z);
                double outX = local.X;
                double outZ = local.Z;

                if (pushX < pushZ)
                {
                    outX = Math.Sign(local.X == 0.0 ? 1.0 : local.X) * ex;
                }
                else
                {
                    outZ = Math.Sign(local.Z == 0.0 ? 1.0 : local.Z) * ez;
                }

                Vec3 world = RotateY(outX, outZ, obb.Rot);
                return new Vec3(c.X + world.X, 0, c.Z + world.Z);
            }
            return null;
        }

        public static Vec3 ResolveAgainst(IEnumerable<Collider> colliders, Vec3 pos, double r, bool ignoreFences = false)
        {
            double px = pos.X;
            double pz = pos.Z;

            for (int iter = 0; iter < 3; iter++)
            {
                bool moved = false;
                foreach (var c in colliders)
                {
                    if (ignoreFences && c is ObbCollider obb && obb.IsFence == true)
                    {
                        continue;
                    }

                    var res = PushOut(c, px, pz, r);
                    if (res.HasValue)
                    {
                        px = res.Value.X;
                        pz = res.Value.Z;
                        moved = true;
                    }
                }
                if (!moved) break;
            }

            return new Vec3(px, pos.Y, pz);
        }

        public static Vec3 ResolvePosition(ColliderGrid grid, Vec3 pos, double r = 0.5, bool ignoreFences = false)
        {
            var list = grid.GetCollidersInCell(pos.X, pos.Z);
            if (list.Count == 0)
            {
                return pos;
            }
            return ResolveAgainst(list, pos, r, ignoreFences);
        }

        public static bool CrossesFence(IEnumerable<FenceSegment> fences, double fromX, double fromZ, double toX, double toZ, double r)
        {
            const double fenceEndPad = 0.35;
            foreach (var f in fences)
            {
                double dx = f.X2 - f.X1;
                double dz = f.Z2 - f.Z1;
                double len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-6) continue;

                double ux = dx / len;
                double uz = dz / len;
                double nx = -uz;
                double nz = ux;

                double fromRelX = fromX - f.X1;
                double fromRelZ = fromZ - f.Z1;
                double toRelX = toX - f.X1;
                double toRelZ = toZ - f.Z1;

                double fromSide = fromRelX * nx + fromRelZ * nz;
                double toSide = toRelX * nx + toRelZ * nz;

                if (fromSide == 0 && toSide == 0) continue;
                if (fromSide * toSide > 0) continue;

                double denom = fromSide - toSide;
                double t = Math.Abs(denom) < 1e-6 ? 0 : fromSide / denom;
                if (t < 0 || t > 1) continue;

                double hitX = fromX + (toX - fromX) * t;
                double hitZ = fromZ + (toZ - fromZ) * t;
                double along = (hitX - f.X1) * ux + (hitZ - f.Z1) * uz;

                if (along >= -fenceEndPad - r && along <= len + fenceEndPad + r) return true;
            }
            return false;
        }

        public static Vec3 ResolveMovement(ColliderGrid grid, IEnumerable<FenceSegment> fences, Vec3 from, Vec3 to, double r = 0.5, bool ignoreFences = false)
        {
            double dx = to.X - from.X;
            double dz = to.Z - from.Z;
            double d = Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-6) return ResolvePosition(grid, to, r, ignoreFences);

            int steps = (int)Math.Max(1, Math.Ceiling(d / 0.2));
            double x = from.X;
            double z = from.Z;

            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                double nextX = from.X + dx * t;
                double nextZ = from.Z + dz * t;

                if (!ignoreFences && CrossesFence(fences, x, z, nextX, nextZ, r)) break;

                var resolved = ResolvePosition(grid, new Vec3(nextX, to.Y, nextZ), r, ignoreFences);
                x = resolved.X;
                z = resolved.Z;

                double currentDx = x - nextX;
                double currentDz = z - nextZ;
                if (Math.Sqrt(currentDx * currentDx + currentDz * currentDz) > r * 0.25)
                {
                    double remainingX = to.X - nextX;
                    double remainingZ = to.Z - nextZ;
                    double correctionX = x - nextX;
                    double correctionZ = z - nextZ;
                    if (remainingX * correctionX + remainingZ * correctionZ < 0) break;
                }
            }

            return new Vec3(x, to.Y, z);
        }
    }
}
