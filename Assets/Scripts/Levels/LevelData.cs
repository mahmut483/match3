using System.Collections.Generic;
using UnityEngine;

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

  public ArrayLayout arrayLayout;

  // Tahtanın şeklini tanımlayan boyalı Tilemap prefab'ı.
  // Boyalı hücre = oynanabilir, boş hücre = kapalı.
  public GameObject boardTilemapPrefab;
}
