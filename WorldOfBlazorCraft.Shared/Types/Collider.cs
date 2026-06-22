namespace WorldOfBlazorCraft.Shared.Types
{
    public enum ColliderType
    {
        Circle,
        Obb
    }

    public abstract class Collider
    {
        public ColliderType Type { get; protected set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double? CameraTopY { get; set; }
        public bool? CamGhost { get; set; }
    }

    public class CircleCollider : Collider
    {
        public double R { get; set; }

        public CircleCollider()
        {
            Type = ColliderType.Circle;
        }

        public CircleCollider(double x, double z, double r, double? cameraTopY = null, bool? camGhost = null)
        {
            Type = ColliderType.Circle;
            X = x;
            Z = z;
            R = r;
            CameraTopY = cameraTopY;
            CamGhost = camGhost;
        }
    }

    public class ObbCollider : Collider
    {
        public double Hw { get; set; } // half width
        public double Hd { get; set; } // half depth
        public double Rot { get; set; } // yaw rotation
        public bool? IsFence { get; set; }

        public ObbCollider()
        {
            Type = ColliderType.Obb;
        }

        public ObbCollider(double x, double z, double hw, double hd, double rot, double? cameraTopY = null, bool? camGhost = null, bool? isFence = null)
        {
            Type = ColliderType.Obb;
            X = x;
            Z = z;
            Hw = hw;
            Hd = hd;
            Rot = rot;
            CameraTopY = cameraTopY;
            CamGhost = camGhost;
            IsFence = isFence;
        }
    }
}
