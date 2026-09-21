using System.Collections.Generic;
using UnityEngine;

using Match3.Gameplay.Board;
using Match3.Gameplay.Potions;

namespace Match3.Levels
{
    // Tek bir toplama hedefi: hangi tipten kaç adet patlatılacak.
    // Bomb da seçilebilir — "3 bomba patlat" gibi hedefler kurulabilir.
    [System.Serializable]
    public class PotionGoal
    {
        public PotionType potionType;
        public int amount;
    }

    [CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/LevelData")]
    public class LevelData : ScriptableObject
    {

      public int level;

      public int moves;

      public int goal;

      // Toplama hedefleri: birden fazla eklenebilir (örn. 24 Red + 3 Bomb).
      public List<PotionGoal> potionGoals = new();

      // Özel vuruş hakları. Seviye başına verilir, seviye bitince sıfırlanır.
      public int hammerCount;
      public int cannonCount;
      public int bombCount;

      public ArrayLayout arrayLayout = new();

      // Tahtanın şeklini tanımlayan boyalı Tilemap prefab'ı.
      // Boyalı hücre = oynanabilir, boş hücre = kapalı.
      public GameObject boardTilemapPrefab;

    #if UNITY_EDITOR
      private void OnValidate()
      {
        string validationError = BoardDefinition.GetLayoutValidationError(arrayLayout);

        if (validationError != null)
        {
          Debug.LogError($"LevelData '{name}' has an invalid board layout: {validationError}", this);
        }
      }
    #endif
    }
}
