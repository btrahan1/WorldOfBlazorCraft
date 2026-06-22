using WorldOfBlazorCraft.Shared.Types;

namespace WorldOfBlazorCraft.Shared.Engine.Entities
{
    public class InputComponent : IComponent
    {
        public MoveInput MoveInput { get; set; } = MoveInput.CreateEmpty();
    }
}
