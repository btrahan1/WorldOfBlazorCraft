namespace WorldOfBlazorCraft.Shared.Types
{
    public enum PlayerClass
    {
        Warrior,
        Paladin,
        Hunter,
        Rogue,
        Priest,
        Shaman,
        Mage,
        Warlock,
        Druid
    }

    public static class PlayerClassExtensions
    {
        public static string ToWireString(this PlayerClass playerClass)
        {
            return playerClass switch
            {
                PlayerClass.Warrior => "warrior",
                PlayerClass.Paladin => "paladin",
                PlayerClass.Hunter => "hunter",
                PlayerClass.Rogue => "rogue",
                PlayerClass.Priest => "priest",
                PlayerClass.Shaman => "shaman",
                PlayerClass.Mage => "mage",
                PlayerClass.Warlock => "warlock",
                PlayerClass.Druid => "druid",
                _ => "warrior"
            };
        }

        public static PlayerClass FromWireString(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "warrior" => PlayerClass.Warrior,
                "paladin" => PlayerClass.Paladin,
                "hunter" => PlayerClass.Hunter,
                "rogue" => PlayerClass.Rogue,
                "priest" => PlayerClass.Priest,
                "shaman" => PlayerClass.Shaman,
                "mage" => PlayerClass.Mage,
                "warlock" => PlayerClass.Warlock,
                "druid" => PlayerClass.Druid,
                _ => PlayerClass.Warrior
            };
        }
    }
}
