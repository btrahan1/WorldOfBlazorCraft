namespace WorldOfBlazorCraft.Shared.Types
{
    public class MoveInput
    {
        public bool Forward { get; set; }
        public bool Back { get; set; }
        public bool TurnLeft { get; set; }
        public bool TurnRight { get; set; }
        public bool StrafeLeft { get; set; }
        public bool StrafeRight { get; set; }
        public bool Jump { get; set; }

        public static MoveInput CreateEmpty()
        {
            return new MoveInput
            {
                Forward = false,
                Back = false,
                TurnLeft = false,
                TurnRight = false,
                StrafeLeft = false,
                StrafeRight = false,
                Jump = false
            };
        }
    }
}
