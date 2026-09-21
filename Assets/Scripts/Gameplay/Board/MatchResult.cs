using System.Collections.Generic;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    public enum MatchDirection { Vertical, Horizontal, LongVertical, LongHorizontal, Super, None }

    // Bir eşleşme grubu: hangi taşlar, hangi şekil, süper eşleşmede hangi taş hayatta kalır.
    public class MatchResult
    {
        public List<Potion> ConnectedPotions { get; set; }
        public MatchDirection Direction { get; set; }
        public Potion ProtectedPotion { get; set; }

        public bool IsSuperMatch =>
            Direction == MatchDirection.LongHorizontal ||
            Direction == MatchDirection.LongVertical ||
            Direction == MatchDirection.Super;
    }
}
