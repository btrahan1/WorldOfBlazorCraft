using System;
using System.Collections.Generic;
using WorldOfBlazorCraft.Shared.Types;

namespace WorldOfBlazorCraft.Shared.Engine.Physics
{
    public class TerrainZoneHub
    {
        public double X { get; set; }
        public double Z { get; set; }
        public double Radius { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public struct TerrainLake
    {
        public double X { get; set; }
        public double Z { get; set; }
        public double Radius { get; set; }
    }

    public class TerrainZone
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double ZMin { get; set; }
        public double ZMax { get; set; }
        public string Biome { get; set; } = string.Empty;
        public TerrainZoneHub Hub { get; set; } = new TerrainZoneHub();
        public List<TerrainLake> Lakes { get; set; } = new List<TerrainLake>();
    }

    public class TerrainCamp
    {
        public double X { get; set; }
        public double Z { get; set; }
        public double Radius { get; set; }
    }

    public class TerrainRidge
    {
        public double Z { get; set; }
        public double PassX { get; set; }
    }

    public static class Terrain
    {
        public const double WaterLevel = -4.5;
        private const double HillScale = 0.013;
        private const double DetailScale = 0.05;
        private const double RidgeHeight = 22.0;
        private const double RidgeSigma = 18.0;
        private const double PassHalfWidth = 10.0;
        private const double PassShoulder = 34.0;

        public const double DungeonXThreshold = 600.0;
        public const double DungeonFloorY = 0.0;

        public const double WorldSize = 360.0;
        public const double WorldMinX = -WorldSize / 2.0;
        public const double WorldMaxX = WorldSize / 2.0;

        public static readonly List<TerrainZone> Zones = new List<TerrainZone>
        {
            new TerrainZone
            {
                Id = "eastbrook_vale",
                Name = "Eastbrook Vale",
                ZMin = -180,
                ZMax = 180,
                Biome = "vale",
                Hub = new TerrainZoneHub { X = 0, Z = 0, Radius = 26, Name = "Eastbrook" },
                Lakes = new List<TerrainLake> { new TerrainLake { X = -92, Z = 88, Radius = 30 } }
            },
            new TerrainZone
            {
                Id = "mirefen_marsh",
                Name = "Mirefen Marsh",
                ZMin = 180,
                ZMax = 540,
                Biome = "marsh",
                Hub = new TerrainZoneHub { X = 0, Z = 300, Radius = 20, Name = "Fenbridge" },
                Lakes = new List<TerrainLake>
                {
                    new TerrainLake { X = -110, Z = 310, Radius = 35 },
                    new TerrainLake { X = 60, Z = 380, Radius = 25 },
                    new TerrainLake { X = -40, Z = 450, Radius = 20 }
                }
            },
            new TerrainZone
            {
                Id = "thornpeak_heights",
                Name = "Thornpeak Heights",
                ZMin = 540,
                ZMax = 900,
                Biome = "peaks",
                Hub = new TerrainZoneHub { X = 0, Z = 660, Radius = 20, Name = "Highwatch" },
                Lakes = new List<TerrainLake> { new TerrainLake { X = -70, Z = 760, Radius = 18 } }
            }
        };

        public static readonly double WorldMinZ = Zones[0].ZMin;
        public static readonly double WorldMaxZ = Zones[Zones.Count - 1].ZMax;

        private static readonly Dictionary<string, (double hill, double @base, double hubHeight)> BiomeShapes =
            new Dictionary<string, (double hill, double @base, double hubHeight)>
            {
                { "vale", (26.0, 0.0, 1.5) },
                { "marsh", (11.0, -1.0, 1.2) },
                { "peaks", (34.0, 7.0, 9.0) }
            };

        private static readonly List<TerrainRidge> ZoneRidges = new List<TerrainRidge>
        {
            new TerrainRidge { Z = 180.0, PassX = 0.0 },
            new TerrainRidge { Z = 540.0, PassX = 0.0 }
        };

        // Mirefen Impact Crater parameters
        private const double CraterX = 149.5;
        private const double CraterZ = 295.0;
        private const double CraterBowlRadius = 20.0;
        private const double CraterRadius = 30.0;
        private const double CraterDepth = 2.6;
        private const double CraterRimHeight = 0.95;

        // Flatten camps center + radius list
        public static readonly List<TerrainCamp> Camps = new List<TerrainCamp>
        {
            new TerrainCamp { X = -15, Z = 55, Radius = 22 },
            new TerrainCamp { X = 20, Z = 70, Radius = 20 },
            new TerrainCamp { X = 0, Z = 95, Radius = 8 },
            new TerrainCamp { X = 55, Z = 12, Radius = 22 },
            new TerrainCamp { X = 80, Z = -15, Radius = 18 },
            new TerrainCamp { X = 104, Z = 24, Radius = 4 },
            new TerrainCamp { X = 118, Z = -26, Radius = 5 },
            new TerrainCamp { X = -60, Z = 5, Radius = 22 },
            new TerrainCamp { X = -72, Z = 28, Radius = 5 },
            new TerrainCamp { X = -75, Z = 57, Radius = 14 },
            new TerrainCamp { X = -82, Z = -62, Radius = 20 },
            new TerrainCamp { X = 65, Z = -65, Radius = 24 },
            new TerrainCamp { X = 90, Z = -90, Radius = 16 },
            new TerrainCamp { X = 92, Z = -92, Radius = 2 },
            new TerrainCamp { X = 80, Z = 78, Radius = 18 },
            new TerrainCamp { X = 92, Z = 90, Radius = 4 },
            new TerrainCamp { X = -8, Z = 126, Radius = 22 },
            new TerrainCamp { X = 50, Z = 124, Radius = 18 },
            new TerrainCamp { X = 18, Z = 150, Radius = 16 },
            new TerrainCamp { X = -18, Z = 142, Radius = 16 },
            new TerrainCamp { X = 64, Z = 156, Radius = 16 },
            new TerrainCamp { X = 28, Z = 136, Radius = 18 },
            new TerrainCamp { X = 78, Z = 140, Radius = 18 },
            new TerrainCamp { X = 2, Z = 162, Radius = 16 },
            new TerrainCamp { X = 50, Z = 164, Radius = 14 },
            new TerrainCamp { X = 28, Z = 166, Radius = 12 },
            new TerrainCamp { X = 42, Z = 172, Radius = 4 },
            new TerrainCamp { X = -40, Z = 230, Radius = 22 },
            new TerrainCamp { X = 35, Z = 225, Radius = 20 },
            new TerrainCamp { X = -82, Z = 273, Radius = 15 },
            new TerrainCamp { X = -120, Z = 350, Radius = 13 },
            new TerrainCamp { X = -132, Z = 333, Radius = 5 },
            new TerrainCamp { X = 70, Z = 300, Radius = 20 },
            new TerrainCamp { X = 95, Z = 340, Radius = 16 },
            new TerrainCamp { X = 98, Z = 348, Radius = 3 },
            new TerrainCamp { X = 90, Z = 420, Radius = 20 },
            new TerrainCamp { X = 115, Z = 450, Radius = 16 },
            new TerrainCamp { X = 118, Z = 455, Radius = 5 },
            new TerrainCamp { X = -80, Z = 420, Radius = 22 },
            new TerrainCamp { X = -105, Z = 455, Radius = 18 },
            new TerrainCamp { X = -120, Z = 480, Radius = 8 },
            new TerrainCamp { X = 15, Z = 470, Radius = 20 },
            new TerrainCamp { X = -25, Z = 490, Radius = 16 },
            new TerrainCamp { X = -5, Z = 500, Radius = 12 },
            new TerrainCamp { X = 18, Z = 472, Radius = 8 },
            new TerrainCamp { X = 24, Z = 492, Radius = 5 },
            new TerrainCamp { X = 0, Z = 510, Radius = 2 },
            new TerrainCamp { X = 72, Z = 428, Radius = 11 },
            new TerrainCamp { X = 110, Z = 440, Radius = 11 },
            new TerrainCamp { X = -50, Z = 590, Radius = 22 },
            new TerrainCamp { X = 45, Z = 600, Radius = 20 },
            new TerrainCamp { X = -82, Z = 575, Radius = 5 },
            new TerrainCamp { X = 75, Z = 625, Radius = 18 },
            new TerrainCamp { X = 105, Z = 600, Radius = 14 },
            new TerrainCamp { X = 100, Z = 617, Radius = 5 },
            new TerrainCamp { X = -90, Z = 700, Radius = 22 },
            new TerrainCamp { X = -60, Z = 730, Radius = 18 },
            new TerrainCamp { X = -125, Z = 740, Radius = 18 },
            new TerrainCamp { X = -132, Z = 748, Radius = 2 },
            new TerrainCamp { X = -45, Z = 768, Radius = 4 },
            new TerrainCamp { X = 110, Z = 760, Radius = 20 },
            new TerrainCamp { X = 135, Z = 795, Radius = 16 },
            new TerrainCamp { X = 145, Z = 815, Radius = 8 },
            new TerrainCamp { X = 55, Z = 820, Radius = 20 },
            new TerrainCamp { X = 25, Z = 845, Radius = 16 },
            new TerrainCamp { X = 40, Z = 855, Radius = 14 },
            new TerrainCamp { X = -40, Z = 830, Radius = 20 },
            new TerrainCamp { X = -15, Z = 860, Radius = 16 },
            new TerrainCamp { X = -34, Z = 842, Radius = 5 },
            new TerrainCamp { X = 80, Z = 845, Radius = 4 },
            new TerrainCamp { X = 80, Z = 845, Radius = 7 },
            new TerrainCamp { X = -78, Z = 778, Radius = 16 },
            new TerrainCamp { X = -56, Z = 800, Radius = 14 },
            new TerrainCamp { X = -90, Z = 802, Radius = 16 },
            new TerrainCamp { X = -64, Z = 814, Radius = 12 },
            new TerrainCamp { X = -96, Z = 814, Radius = 3 },
            new TerrainCamp { X = 88, Z = 90, Radius = 6 },
            new TerrainCamp { X = 88, Z = 92, Radius = 3 },
            new TerrainCamp { X = -95, Z = -78, Radius = 4 }
        };

        private static double Smoothstep(double edge0, double edge1, double x)
        {
            double t = Math.Max(0.0, Math.Min(1.0, (x - edge0) / (edge1 - edge0)));
            return t * t * (3.0 - 2.0 * t);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        public static double MirefenImpactCraterOffset(double x, double z)
        {
            double dx = x - CraterX;
            double dz = z - CraterZ;
            double d = Math.Sqrt(dx * dx + dz * dz);
            if (d >= CraterRadius) return 0.0;

            double bowlT = d / CraterBowlRadius;
            double bowl = d < CraterBowlRadius
                ? -CraterDepth * (1.0 - Smoothstep(0.0, 1.0, bowlT))
                : 0.0;

            double rimStart = CraterBowlRadius * 0.82;
            if (d <= rimStart) return bowl;
            double rimT = (d - rimStart) / (CraterRadius - rimStart);
            double rim = CraterRimHeight
                * Smoothstep(0.0, 0.35, rimT)
                * (1.0 - Smoothstep(0.72, 1.0, rimT));
            return bowl + rim;
        }

        private static (double hill, double @base) ShapeAt(double z)
        {
            var initialShape = BiomeShapes[Zones[0].Biome];
            double hill = initialShape.hill;
            double @base = initialShape.@base;

            for (int i = 0; i + 1 < Zones.Count; i++)
            {
                double boundary = Zones[i].ZMax;
                double t = Smoothstep(boundary - 30.0, boundary + 35.0, z);
                var next = BiomeShapes[Zones[i + 1].Biome];
                hill = Lerp(hill, next.hill, t);
                @base = Lerp(@base, next.@base, t);
            }
            return (hill, @base);
        }

        private static double BaseHeight(double x, double z, uint seed)
        {
            var shape = ShapeAt(z);
            double h = (Noise.Fbm2(x * HillScale + 100.0, z * HillScale + 100.0, seed, 4) - 0.5) * shape.hill + shape.@base;
            h += (Noise.Fbm2(x * DetailScale, z * DetailScale, seed + 7, 2) - 0.5) * 2.2;

            // Flatten each zone's hub settlement into a plateau
            foreach (var zone in Zones)
            {
                double dx = x - zone.Hub.X;
                double dz = z - zone.Hub.Z;
                double dHub = Math.Sqrt(dx * dx + dz * dz);
                if (dHub < zone.Hub.Radius * 1.6)
                {
                    double blend = Smoothstep(zone.Hub.Radius * 0.7, zone.Hub.Radius * 1.6, dHub);
                    h = h * blend + BiomeShapes[zone.Biome].hubHeight * (1.0 - blend);
                }
            }

            // Keep dry land everywhere: soft-floor low dips above the water level...
            double minLand = WaterLevel + 1.4;
            if (h < minLand) h = minLand - (minLand - h) * 0.12;

            // ...except the carved lake basins
            foreach (var zone in Zones)
            {
                foreach (var lake in zone.Lakes)
                {
                    double dLake = Math.Sqrt((x - lake.X) * (x - lake.X) + (z - lake.Z) * (z - lake.Z));
                    if (dLake < lake.Radius * 1.6)
                    {
                        double lakeBlend = Smoothstep(lake.Radius * 0.55, lake.Radius * 1.6, dLake);
                        h = h * lakeBlend + (WaterLevel - 4.0) * (1.0 - lakeBlend);
                    }
                }
            }
            return h;
        }

        public static double GroundHeight(double x, double z, uint seed)
        {
            if (x > DungeonXThreshold) return DungeonFloorY;
            return TerrainHeight(x, z, seed);
        }

        public static double TerrainHeight(double x, double z, uint seed)
        {
            double h = BaseHeight(x, z, seed);

            // Flatten each camp a little so mobs don't stand on cliffs
            foreach (var camp in Camps)
            {
                double dx = x - camp.X;
                double dz = z - camp.Z;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d < camp.Radius * 1.8)
                {
                    double ch = BaseHeight(camp.X, camp.Z, seed);
                    double blend = Smoothstep(camp.Radius * 0.8, camp.Radius * 1.8, d);
                    h = h * blend + ch * (1.0 - blend);
                }
            }

            // Mountain ridge walls between zones, pierced by the road pass
            foreach (var ridge in ZoneRidges)
            {
                double dz = Math.Abs(z - ridge.Z);
                if (dz < RidgeSigma * 3.0)
                {
                    double profile = Math.Exp(-(dz * dz) / (2.0 * RidgeSigma * RidgeSigma));
                    double pass = Smoothstep(PassHalfWidth, PassShoulder, Math.Abs(x - ridge.PassX));
                    // jagged crest so the wall reads as mountains, not a berm
                    double crest = 1.0 + (Noise.Fbm2(x * 0.03, ridge.Z * 0.03, seed + 19, 2) - 0.5) * 0.7;
                    h += RidgeHeight * crest * profile * pass;
                }
            }

            // Raise the world rim so the player naturally stays in bounds
            double rimX = Smoothstep(WorldMaxX - 30.0, WorldMaxX, Math.Abs(x));
            double rimS = Smoothstep(WorldMinZ + 30.0, WorldMinZ, z);
            double rimN = Smoothstep(WorldMaxZ - 30.0, WorldMaxZ, z);
            double rim = Math.Max(rimX, Math.Max(rimS, rimN));
            h += rim * 40.0;
            h += MirefenImpactCraterOffset(x, z);
            return h;
        }
    }
}
