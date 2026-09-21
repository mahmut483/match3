using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Tahtadaki bir hücre: kapalı mı, üzerinde hangi taş var.
    public class Node
    {
        public bool IsUsable { get; }
        public Potion Potion { get; set; }

        public Node(bool isUsable)
        {
            IsUsable = isUsable;
        }
    }
}
