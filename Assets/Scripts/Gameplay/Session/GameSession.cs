using System.Collections.Generic;
using Match3.Gameplay.Potions;
using Match3.Levels;

namespace Match3.Gameplay.Session
{
    public enum SessionOutcome { Playing, Won, Lost }

    // Bir bölümün kuralları: puan, hamle, toplama hedefleri, kazanma/kaybetme.
    // Unity'den bağımsız; GameManager sahneyi buna göre günceller.
    public sealed class GameSession
    {
        private readonly List<PotionGoal> goals = new();

        public int Points { get; private set; }
        public int Moves { get; private set; }
        public int Goal { get; }
        public IReadOnlyList<PotionGoal> Goals => goals;
        public SessionOutcome Outcome { get; private set; } = SessionOutcome.Playing;
        public bool IsEnded => Outcome != SessionOutcome.Playing;

        // Hedefler KOPYALANIR; asset oyun sırasında değişmez.
        public GameSession(LevelData level)
        {
            Moves = level.moves;
            Goal = level.goal;

            foreach (PotionGoal source in level.potionGoals)
            {
                goals.Add(new PotionGoal { potionType = source.potionType, amount = source.amount });
            }
        }

        public void AddPoints(int amount)
        {
            Points += amount;
            TryWin();
        }

        // Tipi eşleşen ve hâlâ açık olan tüm hedefler birer düşer.
        public void RegisterCleared(PotionType type)
        {
            foreach (PotionGoal goal in goals)
            {
                if (goal.potionType == type && goal.amount > 0) goal.amount--;
            }

            TryWin();
        }

        // Hamle harcandı; oyun bitmişse dokunulmaz, sıfıra inince kaybedilir.
        public void EndTurn()
        {
            if (IsEnded) return;

            Moves--;

            if (Moves == 0) Outcome = SessionOutcome.Lost;
        }

        // Puan hedefi ve TÜM toplama hedefleri tamamlandığı anda kazanılır; hangi
        // yoldan (takas, dokunma, özel vuruş) geldiği fark etmez.
        private void TryWin()
        {
            if (IsEnded || Points < Goal) return;

            foreach (PotionGoal goal in goals)
            {
                if (goal.amount > 0) return;
            }

            Outcome = SessionOutcome.Won;
        }
    }
}
