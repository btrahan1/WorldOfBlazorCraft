namespace WorldOfBlazorCraft.Shared.Engine.Entities
{
    public class HealthComponent : IComponent
    {
        public double Hp { get; set; }
        public double MaxHp { get; set; }
        public double Resource { get; set; }
        public double MaxResource { get; set; }
        public string ResourceType { get; set; } = string.Empty; // mana, rage, energy
    }
}
