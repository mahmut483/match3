using System.Collections.Generic;
using UnityEngine;

namespace Match3.Levels
{
    // Tüm levellerin sıralı listesi. "Sıradaki level hangisi?" sorusunun tek cevap yeri.
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Scriptable Objects/LevelCatalog")]
    public class LevelCatalog : ScriptableObject
    {
        public List<LevelData> levels = new();

        private LevelData GetByNumber(int levelNumber)
        {
            return levels.Find(l => l != null && l.level == levelNumber);
        }

        // Listede current'tan sonra gelen level; son leveldeysek null.
        public LevelData GetNext(LevelData current)
        {
            int index = levels.IndexOf(current);

            if (index >= 0 && index + 1 < levels.Count)
            {
                return levels[index + 1];
            }

            return null;
        }

        // Oyuncunun tamamladığı son bölüme göre oynanacak bölümü seçer.
        // Katalog bittiyse veya kayıt geçersizse ilk bölüme döner.
        public LevelData GetPlayableLevel(int highestCompletedLevel)
        {
            LevelData firstLevel = levels.Find(level => level != null);

            if (firstLevel == null || highestCompletedLevel <= 0)
            {
                return firstLevel;
            }

            LevelData completedLevel = GetByNumber(highestCompletedLevel);

            if (completedLevel == null)
            {
                return firstLevel;
            }

            return GetNext(completedLevel) ?? firstLevel;
        }
    }
}
