using System.Collections.Generic;
using UnityEngine;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Taş üretimi ve havuz: ilk dolum, boşalan hücrelerin yukarıdan doldurulması,
    // temizlenen taşların havuza alınıp yeniden kullanılması.
    public sealed class BoardRefill : MonoBehaviour
    {
        [SerializeField] private GameObject[] potionPrefabs;
        [SerializeField] private GameObject potionParent;

        [SerializeField, Min(0f)] private float dropStaggerDelay = 0.2f;

        [Tooltip("Bir sütundaki düşüş başlangıçlarının toplamda bekleyebileceği en uzun süre.")]
        [SerializeField, Min(0f)] private float maxDropStagger = 0.1f;

        private readonly List<GameObject> deactivePotionPool = new();
        private BoardGrid grid;
        private BoardGeometry geometry;

        public void Initialize(BoardGrid grid, BoardGeometry geometry)
        {
            this.grid = grid;
            this.geometry = geometry;
        }

        // Her açık hücreye, o anda üçlü oluşturmayacak rastgele bir renk koyar.
        public void CreateInitialPotions()
        {
            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
                {
                    Vector2Int cell = new(x, y);

                    if (!grid.IsUsable(cell)) continue;

                    int randomIndex = GetValidPotionPrefabIndex(x, y);

                    GameObject potionObject = Instantiate(potionPrefabs[randomIndex], geometry.CellToWorld(cell), Quaternion.identity);
                    potionObject.transform.SetParent(potionParent.transform);
                    grid.Place(potionObject.GetComponent<Potion>(), cell);
                }
            }
        }

        // Temizlenen taş: özel görselleri sıfırlanır, kapatılır, havuza girer.
        // Hedef kaydı çağıranın işidir (ClearSpecial tipi orijinaline döndürür).
        public void Release(Potion potion)
        {
            potion.ClearSpecial();
            potion.gameObject.SetActive(false);

            if (!deactivePotionPool.Contains(potion.gameObject))
            {
                deactivePotionPool.Add(potion.gameObject);
            }
        }

        // Her sütunda boş hücreleri alttan yukarı doldurur: önce üstteki taşlar iner,
        // inecek taş kalmayınca havuzdan yeni taş doğar. Aynı sütunun yeni taşları
        // birer hücre daha yukarıdan gelir ki uzun düşüşte üst üste binmesinler.
        public void StartRefill()
        {
            for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
            {
                int dropOrder = 0;
                int spawnOrder = 0;

                for (int y = 0; y < BoardDefinition.TotalHeight; y++)
                {
                    if (!grid[x, y].IsUsable || grid[x, y].Potion != null) continue;

                    float startDelay = Mathf.Min(dropOrder * dropStaggerDelay, maxDropStagger);

                    if (RefillPotion(x, y, startDelay, spawnOrder)) spawnOrder++;

                    dropOrder++;
                }
            }
        }

        // Mevcut hücrede yatay veya dikey üçlü oluşturmayacak prefablardan rastgele biri.
        private int GetValidPotionPrefabIndex(int x, int y)
        {
            List<int> validIndexes = new();

            for (int i = 0; i < potionPrefabs.Length; i++)
            {
                Potion prefabPotion = potionPrefabs[i].GetComponent<Potion>();

                if (prefabPotion == null) continue;

                if (!WouldCreateInitialMatch(x, y, prefabPotion.PotionType)) validIndexes.Add(i);
            }

            if (validIndexes.Count == 0) return Random.Range(0, potionPrefabs.Length);

            return validIndexes[Random.Range(0, validIndexes.Count)];
        }

        private bool WouldCreateInitialMatch(int x, int y, PotionType candidateType)
        {
            bool horizontalMatch = IsSamePotionType(x - 1, y, candidateType) && IsSamePotionType(x - 2, y, candidateType);
            bool verticalMatch = IsSamePotionType(x, y - 1, candidateType) && IsSamePotionType(x, y - 2, candidateType);

            return horizontalMatch || verticalMatch;
        }

        private bool IsSamePotionType(int x, int y, PotionType candidateType)
        {
            Potion potion = grid.PotionAt(new Vector2Int(x, y));

            return potion != null && potion.PotionType == candidateType;
        }

        // (x, y) boş; üstteki ilk taşı indirir, yoksa havuzdan doğurur.
        // Kapalı ve boş hücreler atlanır: taşlar kapalı hücrelerin üzerinden düşer.
        // true: havuzdan yeni taş doğdu.
        private bool RefillPotion(int x, int y, float startDelay, int spawnOrder)
        {
            int yOffset = 1;

            while (y + yOffset < BoardDefinition.TotalHeight &&
                   (!grid[x, y + yOffset].IsUsable || grid[x, y + yOffset].Potion == null))
            {
                yOffset++;
            }

            if (y + yOffset < BoardDefinition.TotalHeight)
            {
                Potion potion = grid[x, y + yOffset].Potion;

                potion.MoveToDown(DropTarget(x, y, potion.transform.position.z), startDelay);

                grid.Clear(new Vector2Int(x, y + yOffset));
                grid.Place(potion, new Vector2Int(x, y));

                return false;
            }

            return SpawnPotionAtTop(x, y, startDelay, spawnOrder);
        }

        // Havuzdan bir taş alıp tahtanın üstünden (x, y)'ye düşürür. Renk prefab
        // listesinden eşit olasılıkla seçilir; o renkten taş havuzda yoksa herhangi
        // bir taş alınır. Havuz boşsa hücre boş kalır.
        private bool SpawnPotionAtTop(int x, int y, float startDelay, int spawnOrder)
        {
            if (deactivePotionPool.Count == 0) return false;

            PotionType wantedType = potionPrefabs[Random.Range(0, potionPrefabs.Length)]
                .GetComponent<Potion>().PotionType;

            GameObject newPotionObject =
                deactivePotionPool.Find(pooled => pooled.GetComponent<Potion>().PotionType == wantedType)
                ?? deactivePotionPool[Random.Range(0, deactivePotionPool.Count)];

            newPotionObject.transform.position = geometry.CellToWorld(new Vector2Int(x, BoardDefinition.TotalHeight + spawnOrder));
            newPotionObject.SetActive(true);
            deactivePotionPool.Remove(newPotionObject);

            Potion newPotion = newPotionObject.GetComponent<Potion>();
            grid.Place(newPotion, new Vector2Int(x, y));
            newPotion.MoveToDown(DropTarget(x, y, newPotionObject.transform.position.z), startDelay);

            return true;
        }

        // Hedef CellToWorld'den gelir: Cannon board'u kaydırmışken de taş görsel
        // hücre merkezine iner.
        private Vector3 DropTarget(int x, int y, float z)
        {
            Vector2 cell = geometry.CellToWorld(new Vector2Int(x, y));

            return new Vector3(cell.x, cell.y, z);
        }
    }
}
