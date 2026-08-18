using System.Collections.Generic;
using UnityEngine;

// Tüm levellerin sıralı listesi. "Sıradaki level hangisi?" sorusunun tek cevap yeri.
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Scriptable Objects/LevelCatalog")]
public class LevelCatalog : ScriptableObject
{
    public List<LevelData> levels = new();

    public LevelData GetByNumber(int levelNumber)
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
}
