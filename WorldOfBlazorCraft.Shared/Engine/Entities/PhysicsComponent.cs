namespace WorldOfBlazorCraft.Shared.Engine.Entities
{
    public class PhysicsComponent : IComponent
    {
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Vz { get; set; }
        public bool OnGround { get; set; } = true;
        public bool Jumping { get; set; }
        public double FallStartY { get; set; }
    }
}
