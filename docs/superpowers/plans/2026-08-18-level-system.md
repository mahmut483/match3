# Level Sistemi — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Hedef:** Tilemap tabanlı tahta şekilleri, puan + renk toplama hedefleri, local kayıt ve ana menü Play akışıyla tam bir level sistemi.

**Mimari:** Level verisi ScriptableObject'lerde (`LevelData`, `LevelCatalog`), tahta şekli tilemap prefab'ından türetilir. `GameManager` hedef takibini tek noktadan (`RegisterClearedPotion`) yapar; kazanma/kaybetme cascade bittikten sonra değerlendirilir. İlerleme local JSON'a kaydedilir.

**Tech Stack:** Unity 6 (6000.5.0f1), URP 2D, TextMeshPro, JsonUtility. Test framework'ü yok — her task Play Mode'da elle doğrulanır.

**Spec:** `docs/superpowers/specs/2026-08-18-level-system-design.md`

## Global Kısıtlar

- Engel/kırılacak obje YOK — yalnız Score + Collect hedefleri (spec kararı).
- Yıldız sistemi YOK — kalan hamle × `bonusPerMove` puana eklenir.
- Kayıt bu fazda yalnızca local (`save.json`); Firebase sonraki faz.
- Yeni script'ler `Assets/Scripts/Levels/` altına; mevcut kod stiline (Türkçe yorum, mevcut adlandırma) uyulur.
- Her task sonunda proje derlenir durumda olmalı ve commit atılmalı.

---

### Task 1: Veri Katmanı (LevelGoal, LevelData, LevelCatalog, LevelLoader)

**Files:**
- Create: `Assets/Scripts/Levels/LevelGoal.cs`
- Create: `Assets/Scripts/Levels/LevelData.cs`
- Create: `Assets/Scripts/Levels/LevelCatalog.cs`
- Create: `Assets/Scripts/Levels/LevelLoader.cs`

**Interfaces:**
- Produces: `LevelData` (alanlar: `levelNumber`, `moves`, `bonusPerMove`, `boardTilemapPrefab`, `goals`), `LevelGoal` (`type`, `potionType`, `amount`), `GoalType` enum, `LevelCatalog.levels` listesi, `LevelLoader.selectedLevel` static alanı. Sonraki tüm task'lar bunları kullanır.

- [ ] **Adım 1: Dört script'i oluştur**

`Assets/Scripts/Levels/LevelGoal.cs`:
```csharp
using UnityEngine;

public enum GoalType
{
    Score,   // X puana ulaş
    Collect  // N tane belirli renk taş topla
}

[System.Serializable]
public class LevelGoal
{
    public GoalType type;
    public PotionType potionType; // yalnız Collect için anlamlı
    public int amount;
}
```

`Assets/Scripts/Levels/LevelData.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Match3/Level Data", fileName = "Level_00")]
public class LevelData : ScriptableObject
{
    public int levelNumber = 1;
    public int moves = 20;
    public int bonusPerMove = 50; // bölüm bitince kalan hamle başına bonus puan
    public GameObject boardTilemapPrefab; // tahta şeklini tanımlayan Tilemap prefab'ı
    public List<LevelGoal> goals = new();
}
```

`Assets/Scripts/Levels/LevelCatalog.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Match3/Level Catalog", fileName = "LevelCatalog")]
public class LevelCatalog : ScriptableObject
{
    public List<LevelData> levels = new();

    public LevelData GetByNumber(int levelNumber)
    {
        return levels.Find(l => l != null && l.levelNumber == levelNumber);
    }
}
```

`Assets/Scripts/Levels/LevelLoader.cs`:
```csharp
// MainMenu -> GameBoard sahne geçişinde seçilen bölümü taşır.
public static class LevelLoader
{
    public static LevelData selectedLevel;
}
```

- [ ] **Adım 2: Unity'de derlemeyi bekle, hata olmadığını doğrula**

- [ ] **Adım 3: İlk asset'leri oluştur**

Unity'de: `Assets/Data/Levels/` klasörü oluştur. Sağ tık → Create → Match3 → Level Data → adı `Level_01`. Inspector'da: levelNumber=1, moves=20, goals'a 2 kayıt ekle: `{Score, amount:500}` ve `{Collect, Red, amount:10}`. (boardTilemapPrefab şimdilik boş — Task 4'te gelecek.)
Sonra Create → Match3 → Level Catalog → adı `LevelCatalog`, levels listesine `Level_01`'i ekle.

- [ ] **Adım 4: Commit**

```bash
git add Assets/Scripts/Levels Assets/Data
git commit -m "feat: add level data layer (LevelData, LevelGoal, LevelCatalog, LevelLoader)"
```

---

### Task 2: GameManager Yeniden Yazımı + PotionBoard Entegrasyonu

Bu task bölünemez: `ProcessTurn` imzası değişiyor, `PotionBoard`'daki çağrı yerleri aynı anda güncellenmezse proje derlenmez.

**Files:**
- Modify: `Assets/Scripts/GameManager.cs` (büyük ölçüde yeniden yazım)
- Modify: `Assets/Scripts/PotionBoard.cs` (`ReturnPotionToPool`, `ProcessMatches` içindeki 3 `ProcessTurn` çağrısı)

**Interfaces:**
- Consumes: `LevelData`, `LevelGoal`, `GoalType`, `LevelLoader.selectedLevel` (Task 1).
- Produces: `GameManager.ActiveLevel` (LevelData, get-only property), `Initialize(LevelData level)`, `RegisterClearedPotion(PotionType type)`, `ProcessTurn(bool _subtractMoves)`, `EvaluateGameState()`. Task 4/6/7/8 bunlara dayanır.

- [ ] **Adım 1: GameManager.cs'i şu içerikle değiştir**

```csharp
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Level")]
    // Menüden gelinmediyse (editörde direkt GameBoard açıldıysa) bu level oynanır.
    [SerializeField] private LevelData defaultLevel;
    public LevelData ActiveLevel { get; private set; }

    [SerializeField] private int pointsPerPotion = 10; // temizlenen taş başına puan

    public GameObject backgroundPanel;
    public GameObject victoryPanel;
    public GameObject losePanel;

    public int moves;
    public int points;
    public bool isGameEnded;
    private bool isPlayedlast3MovesClip = false;

    // ActiveLevel.goals ile aynı sıradaki ilerlemeler (Collect sayaçları).
    private readonly List<int> goalProgress = new();

    public TMP_Text pointsTXT;
    public TMP_Text movesTXT;
    public TMP_Text goalTXT;

    [SerializeField] private GameObject outOfMovesPanel;
    [SerializeField] private Animator charAnimCtrl;
    [SerializeField] private GameObject confetti;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip last3MoveClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip lostClip;

    private void Awake()
    {
        Instance = this;
        ActiveLevel = LevelLoader.selectedLevel != null ? LevelLoader.selectedLevel : defaultLevel;
    }

    private void Start()
    {
        Initialize(ActiveLevel);
    }

    public void Initialize(LevelData level)
    {
        moves = level.moves;
        points = 0;
        isGameEnded = false;
        isPlayedlast3MovesClip = false;

        goalProgress.Clear();
        for (int i = 0; i < level.goals.Count; i++)
        {
            goalProgress.Add(0);
        }

        UpdateHud();
    }

    // Temizlenen her taş PotionBoard.ReturnPotionToPool üzerinden buraya rapor edilir.
    public void RegisterClearedPotion(PotionType type)
    {
        points += pointsPerPotion;

        for (int i = 0; i < ActiveLevel.goals.Count; i++)
        {
            LevelGoal goal = ActiveLevel.goals[i];

            if (goal.type == GoalType.Collect && goal.potionType == type)
            {
                goalProgress[i] = Mathf.Min(goalProgress[i] + 1, goal.amount);
            }
        }

        UpdateHud();
    }

    // Artık puan almaz; yalnızca hamle düşürür. Kazanma/kaybetme kararını vermez —
    // o karar cascade bittikten sonra EvaluateGameState'te verilir.
    public void ProcessTurn(bool _subtractMoves)
    {
        if (isGameEnded) return;

        if (_subtractMoves)
        {
            moves--;
        }

        if (moves <= 3 && moves > 0 && !isPlayedlast3MovesClip)
        {
            charAnimCtrl.SetTrigger("LowMove");
            audioSource.clip = last3MoveClip;
            audioSource.Play();
            isPlayedlast3MovesClip = true;
        }

        UpdateHud();
    }

    // PotionBoard, hamlenin TÜM zincirlemesi bittiğinde çağırır.
    public void EvaluateGameState()
    {
        if (isGameEnded) return;

        if (AreAllGoalsComplete())
        {
            WinGame();
            return;
        }

        if (moves <= 0)
        {
            LoseGame();
        }
    }

    private bool AreAllGoalsComplete()
    {
        for (int i = 0; i < ActiveLevel.goals.Count; i++)
        {
            LevelGoal goal = ActiveLevel.goals[i];
            int current = goal.type == GoalType.Score ? points : goalProgress[i];

            if (current < goal.amount) return false;
        }

        return true;
    }

    public int GetGoalProgress(int goalIndex)
    {
        LevelGoal goal = ActiveLevel.goals[goalIndex];
        return goal.type == GoalType.Score ? points : goalProgress[goalIndex];
    }

    private void WinGame()
    {
        isGameEnded = true;

        // Kalan hamleler bonus puana dönüşür.
        points += moves * ActiveLevel.bonusPerMove;
        UpdateHud();

        charAnimCtrl.SetTrigger("Win");
        backgroundPanel.SetActive(true);
        confetti.SetActive(true);
        StartCoroutine(WaitForConfetti());
        audioSource.clip = winClip;
        audioSource.Play();
    }

    private void LoseGame()
    {
        isGameEnded = true;
        charAnimCtrl.SetTrigger("Lose");
        backgroundPanel.SetActive(true);
        outOfMovesPanel.SetActive(true);
        audioSource.clip = lostClip;
        audioSource.Play();
    }

    // Update() içindeki her-frame yazımın yerini aldı; yalnızca değişiklik anında çağrılır.
    private void UpdateHud()
    {
        pointsTXT.text = points.ToString();
        movesTXT.text = moves.ToString();

        // Geçici: ilk Score hedefini gösterir. Task 6'da gerçek hedef HUD'ı gelecek.
        LevelGoal scoreGoal = ActiveLevel.goals.Find(g => g.type == GoalType.Score);
        goalTXT.text = scoreGoal != null ? scoreGoal.amount.ToString() : "-";
    }

    private IEnumerator WaitForConfetti()
    {
        yield return new WaitForSeconds(2);
        victoryPanel.SetActive(true);
    }
}
```

Dikkat: eski `Update()` metodu tamamen silindi; `using System;` de kaldırıldı.

- [ ] **Adım 2: PotionBoard.ReturnPotionToPool'a raporlamayı ekle**

`Bomb(false)` çağrısı tipi orijinaline döndürür — rapor ondan SONRA yapılır ki bomba orijinal rengine sayılsın:

```csharp
    private void ReturnPotionToPool(Potion item)
    {
        item.Bomb(false);

        // Temizlenen taşı hedef takibine bildir (bomba orijinal rengine sayılır).
        GameManager.Instance.RegisterClearedPotion(item.potionType);

        item.isMatched = false;
        item.isMoving = false;
        item.gameObject.SetActive(false);

        if (!deactivePotionPool.Contains(item.gameObject))
        {
            deactivePotionPool.Add(item.gameObject);
        }
    }
```

- [ ] **Adım 3: ProcessMatches içindeki 3 çağrı yerini güncelle**

`PotionBoard.ProcessMatches`'te üç `GameManager.Instance.ProcessTurn(10, true);` satırı var (süper bomba dalı, bomba dalı, normal cascade sonu). Üçünü de şu ikiliyle değiştir:

```csharp
            GameManager.Instance.ProcessTurn(true);
            GameManager.Instance.EvaluateGameState();
```

(Başarısız swap dalına EKLENMEZ — hamle harcanmadı, değerlendirme gerekmez.)

- [ ] **Adım 4: Sahneyi bağla ve doğrula**

GameBoard sahnesinde GameManager objesine `defaultLevel` olarak `Level_01` asset'ini ata. Play Mode: eşleşme yap → puan taş sayısıyla orantılı artmalı (5'li eşleşme 50 puan), hamle 1 azalmalı. Hedefler dolunca (500 puan + 10 kırmızı) win paneli gelmeli ve kalan hamle bonusu puana eklenmeli. Hamle biterse lose akışı çalışmalı.

- [ ] **Adım 5: Commit**

```bash
git add Assets/Scripts/GameManager.cs Assets/Scripts/PotionBoard.cs Assets/Scenes/GameBoard.unity
git commit -m "feat: goal-based GameManager with per-potion scoring and post-cascade evaluation"
```

---

### Task 3: Sabit `8` Literal'leri → `visibleHeight`

**Files:**
- Modify: `Assets/Scripts/PotionBoard.cs`

**Interfaces:**
- Produces: `[SerializeField] private int visibleHeight = 8;` — Task 4 ve 5 bu alanı kullanır.

- [ ] **Adım 1: Alanı ekle**

`width`/`height` tanımlarının hemen altına:

```csharp
    // Tahtanın oynanabilir (görünür) satır sayısı; üstündeki satırlar spawn alanıdır.
    [SerializeField] private int visibleHeight = 8;
```

- [ ] **Adım 2: Dört literal'i değiştir**

- `CheckBoard`: `for (int y = 0; y < 8; y++)` → `for (int y = 0; y < visibleHeight; y++)`
- `CheckDirection`: `while (x >= 0 && x < width && y >= 0 && y < 8)` → `... && y < visibleHeight)`
- `BombExploding`: `yIndex < 0 || yIndex >= 8` → `yIndex < 0 || yIndex >= visibleHeight`
- `SuperBombExplod`: `yIndex < 0 || yIndex >= 8` → `yIndex < 0 || yIndex >= visibleHeight`

- [ ] **Adım 3: Play Mode'da eşleşme + bomba patlat, davranışın aynı olduğunu doğrula**

- [ ] **Adım 4: Commit**

```bash
git add Assets/Scripts/PotionBoard.cs
git commit -m "refactor: replace hardcoded visible board height with serialized field"
```

---

### Task 4: Tahtanın Tilemap'ten Türetilmesi

**Files:**
- Modify: `Assets/Scripts/PotionBoard.cs` (`InitializeBoard` + yeni yardımcılar)
- Create (Unity Editor): `Assets/Prefabs/Boards/Level01_Board.prefab`

**Interfaces:**
- Consumes: `GameManager.Instance.ActiveLevel.boardTilemapPrefab` (Task 2), `visibleHeight` (Task 3).
- Produces: tilemap tabanlı `isUsable` türetimi; `ArrayLayout` kullanımdan kalkar.

- [ ] **Adım 1: Tilemap prefab'ını oluştur**

GameBoard sahnesindeki mevcut `Grid` altındaki Tilemap objesini seç → `Assets/Prefabs/Boards/` klasörüne sürükleyip `Level01_Board` prefab'ı yap → sahnedeki kopyayı SİL (artık runtime'da instantiate edilecek). `Level_01` asset'inin `boardTilemapPrefab` alanına bu prefab'ı ata.

- [ ] **Adım 2: PotionBoard'a alanlar ve yükleme kodu ekle**

Dosya başına `using UnityEngine.Tilemaps;` ekle. Alanlara:

```csharp
    [Header("Level Board")]
    [SerializeField] private Grid boardGrid; // sahnedeki Grid objesi
    private Tilemap boardTilemap;
```

Yeni metotlar:

```csharp
    // Aktif level'ın tilemap prefab'ını sahnedeki Grid altına kurar.
    private void LoadBoardTilemap()
    {
        GameObject tilemapGO = Instantiate(GameManager.Instance.ActiveLevel.boardTilemapPrefab, boardGrid.transform);
        boardTilemap = tilemapGO.GetComponentInChildren<Tilemap>();
    }

    // Board koordinatındaki hücre tilemap'te boyalı mı? Boyalı = oynanabilir.
    private bool IsCellPainted(int x, int y)
    {
        Vector3 worldPos = new Vector3((x - spacingX) * cellSize, (y - spacingY) * cellSize, 0f);
        Vector3Int cell = boardTilemap.WorldToCell(worldPos);
        return boardTilemap.HasTile(cell);
    }
```

- [ ] **Adım 3: InitializeBoard'u tilemap okuyacak şekilde değiştir**

`InitializeBoard`'un başına (spacing hesaplarından sonra) `LoadBoardTilemap();` ekle. Sonra sütun bazında oynanabilirlik çıkar ve `arrayLayout` kontrolünü değiştir:

```csharp
        // Görünür alanında en az bir boyalı hücresi olan sütunlar spawn alabilir.
        bool[] columnHasPlayableCell = new bool[width];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < visibleHeight; y++)
            {
                if (IsCellPainted(x, y))
                {
                    columnHasPlayableCell[x] = true;
                    break;
                }
            }
        }
```

Döngü içindeki `if (arrayLayout.rows[y].row[x])` bloğunun yerine:

```csharp
                // Görünür satırlarda: tilemap'te boyalı değilse kapalı hücre.
                // Spawn satırlarında (y >= visibleHeight): sütun oynanabilirse açık.
                bool usable = y < visibleHeight ? IsCellPainted(x, y) : columnHasPlayableCell[x];

                if (!usable)
                {
                    potionBoard[x, y] = new Node(false, null);
                }
                else
                {
                    // ... mevcut else bloğu (Instantiate + Node ataması) aynen kalır
                }
```

`public ArrayLayout arrayLayout;` alanını ve `CustPropertyDrawer` dosyasını ŞİMDİLİK silme — sadece kullanım kalkıyor. (Temizlik ayrı bir iş.)

- [ ] **Adım 4: Sahneyi bağla ve doğrula**

PotionBoard objesine `boardGrid` referansını ata. Play Mode: tahta eskisi gibi kurulmalı. Sonra `Level01_Board` prefab'ında birkaç hücreyi silip tekrar oyna — o hücreler boş kalmalı, taşlar oraya düşmemeli (düşme davranışı Task 5'te tam düzelecek; bu adımda sadece kurulumun şekli tanıdığını doğrula).

- [ ] **Adım 5: Commit**

```bash
git add Assets/Scripts/PotionBoard.cs Assets/Prefabs/Boards Assets/Data Assets/Scenes/GameBoard.unity
git commit -m "feat: derive board shape from level tilemap prefab"
```

---

### Task 5: Refill ve Spawn'ın Kapalı Hücrelere Saygı Duyması

**Files:**
- Modify: `Assets/Scripts/PotionBoard.cs` (`RefillPotion`, `FindIndexOfLowestNull`, `SpawnPotionAtTop` + 3 refill döngüsü)

**Interfaces:**
- Consumes: `Node.isUsable`, `visibleHeight` (Task 3).

- [ ] **Adım 1: Üç refill döngüsünü güncelle**

`RemoveAndRefill`, `BombExploding` ve `SuperBombExplod` içindeki üç özdeş döngüde `if (potionBoard[x, y].potion == null)` koşulunu şuna çevir:

```csharp
                if (potionBoard[x, y].isUsable && potionBoard[x, y].potion == null)
```

- [ ] **Adım 2: RefillPotion'ı kapalı hücre atlayacak şekilde değiştir**

```csharp
    private void RefillPotion(int x, int y, float startDelay)
    {
        // Kapalı hücre asla doldurulmaz.
        if (!potionBoard[x, y].isUsable) return;

        int yOffset = 1;

        // Üste doğru ararken kapalı VE boş hücreleri atla — taşlar kapalı hücrelerin üzerinden düşer.
        while (y + yOffset < height &&
               (!potionBoard[x, y + yOffset].isUsable || potionBoard[x, y + yOffset].potion == null))
        {
            yOffset++;
        }

        if (y + yOffset < height && potionBoard[x, y + yOffset].potion != null)
        {
            Potion potion = potionBoard[x, y + yOffset].potion;

            Vector3 targetPos = new Vector3((x - spacingX) * cellSize, (y - spacingY) * cellSize, potion.transform.position.z);

            potion.SetIndicies(x, y);
            potion.MoveToDown(targetPos, startDelay);

            potionBoard[x, y] = potionBoard[x, y + yOffset];
            potionBoard[x, y + yOffset] = new Node(true, null);
        }

        if (y + yOffset == height)
        {
            SpawnPotionAtTop(x, startDelay);
        }
    }
```

- [ ] **Adım 3: FindIndexOfLowestNull yalnız açık hücreleri saysın + SpawnPotionAtTop guard**

```csharp
    private int FindIndexOfLowestNull(int x)
    {
        int lowestNull = 99;

        for (int y = height - 1; y >= 0; y--)
        {
            if (potionBoard[x, y].isUsable && potionBoard[x, y].potion == null)
            {
                lowestNull = y;
            }
        }

        return lowestNull;
    }
```

`SpawnPotionAtTop`'un başına iki guard ekle:

```csharp
        int index = FindIndexOfLowestNull(x);
        if (index == 99) return;                    // sütunda doldurulacak açık hücre yok
        if (deactivePotionPool.Count == 0) return;  // havuz boş — crash koruması
```

(`int locationToMoveTo = height - index;` satırı kullanılmıyor, silinebilir.)

- [ ] **Adım 4: Doğrula**

`Level01_Board` prefab'ında ortadan ve kenardan hücreler silip oyna: taşlar deliklerin üzerinden akmalı, kapalı hücrelere taş yerleşmemeli, tamamen kapalı sütun boş kalmalı, eşleşme/bomba/cascade akışı takılmamalı.

- [ ] **Adım 5: Commit**

```bash
git add Assets/Scripts/PotionBoard.cs
git commit -m "fix: refill and spawn respect blocked cells for shaped boards"
```

---

### Task 6: Hedef HUD'ı

**Files:**
- Create: `Assets/Scripts/Levels/GoalHud.cs`
- Create: `Assets/Scripts/Levels/GoalHudItem.cs`
- Modify: `Assets/Scripts/GameManager.cs` (`Initialize` + `UpdateHud` içine 2 çağrı)
- Create (Unity Editor): `Assets/Prefabs/UI/GoalHudItem.prefab` + sahnede HUD container

**Interfaces:**
- Consumes: `GameManager.ActiveLevel`, `GameManager.GetGoalProgress(int)` (Task 2).
- Produces: `GoalHud.Build(LevelData level)`, `GoalHud.Refresh()`.

- [ ] **Adım 1: Script'leri yaz**

`Assets/Scripts/Levels/GoalHudItem.cs`:
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoalHudItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;

    public void Setup(Sprite iconSprite)
    {
        icon.sprite = iconSprite;
    }

    public void SetRemaining(int remaining)
    {
        countText.text = remaining > 0 ? remaining.ToString() : "✓";
    }
}
```

`Assets/Scripts/Levels/GoalHud.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

public class GoalHud : MonoBehaviour
{
    [System.Serializable]
    public class PotionIcon
    {
        public PotionType type;
        public Sprite sprite;
    }

    [SerializeField] private GoalHudItem itemPrefab;
    [SerializeField] private Transform itemsParent;
    [SerializeField] private List<PotionIcon> potionIcons;
    [SerializeField] private Sprite scoreIcon;

    private readonly List<GoalHudItem> items = new();
    private LevelData level;

    public void Build(LevelData _level)
    {
        level = _level;

        foreach (GoalHudItem item in items)
        {
            Destroy(item.gameObject);
        }
        items.Clear();

        foreach (LevelGoal goal in level.goals)
        {
            GoalHudItem item = Instantiate(itemPrefab, itemsParent);
            item.Setup(goal.type == GoalType.Score ? scoreIcon : GetIcon(goal.potionType));
            items.Add(item);
        }

        Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < items.Count; i++)
        {
            int remaining = level.goals[i].amount - GameManager.Instance.GetGoalProgress(i);
            items[i].SetRemaining(remaining);
        }
    }

    private Sprite GetIcon(PotionType type)
    {
        PotionIcon icon = potionIcons.Find(p => p.type == type);
        return icon != null ? icon.sprite : null;
    }
}
```

- [ ] **Adım 2: GameManager'a bağla**

Alan ekle: `[SerializeField] private GoalHud goalHud;`
`Initialize`'ın sonuna (UpdateHud'dan ÖNCE): `goalHud.Build(level);`
`UpdateHud`'ın sonuna: `goalHud.Refresh();` — ve artık gereksizleşen geçici `goalTXT` satırlarını `goalTXT.text = ...` olarak eski haliyle bırakmak yerine istersen kaldırabilirsin (GoalTxt objesini HUD'dan çıkarma kararı sana ait; kaldırırsan `goalTXT` alanını ve sahnedeki objeyi birlikte kaldır).

- [ ] **Adım 3: Unity'de UI'ı kur**

GameBoard sahnesinde HUD'a yatay `LayoutGroup`'lu bir `GoalHudContainer` ekle, `GoalHud` script'ini tak. Küçük bir `GoalHudItem` prefab'ı yap (Image + TMP_Text), referansları ve `potionIcons` listesini (4 renk sprite'ı `Assets/Spirites/potions/` içinden) doldur.

- [ ] **Adım 4: Doğrula**

Play Mode: hedef ikonları kalan sayılarla görünmeli, kırmızı taş patlattıkça kırmızı sayacı düşmeli, biten hedef "✓" olmalı.

- [ ] **Adım 5: Commit**

```bash
git add Assets/Scripts/Levels Assets/Scripts/GameManager.cs Assets/Prefabs/UI Assets/Scenes/GameBoard.unity
git commit -m "feat: goal HUD with per-goal icons and remaining counts"
```

---

### Task 7: Kayıt Sistemi (SaveManager)

**Files:**
- Create: `Assets/Scripts/Levels/SaveManager.cs`
- Modify: `Assets/Scripts/GameManager.cs` (`WinGame` içine 1 satır)

**Interfaces:**
- Produces: `SaveManager.Data` (SaveData: `highestCompletedLevel`, `bestScores`), `SaveManager.RecordLevelResult(int levelNumber, int score)`, `SaveManager.GetBestScore(int levelNumber)`. Task 9 bunları kullanır.

- [ ] **Adım 1: SaveManager.cs'i yaz**

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class LevelScore
{
    public int levelNumber;
    public int score;
}

// Düz tutuluyor: backend fazında Firestore dökümanına birebir taşınacak.
[System.Serializable]
public class SaveData
{
    public int highestCompletedLevel;
    public List<LevelScore> bestScores = new();
}

public static class SaveManager
{
    private static SaveData cached;

    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    public static SaveData Data
    {
        get
        {
            if (cached == null)
            {
                cached = File.Exists(SavePath)
                    ? JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath))
                    : new SaveData();
            }
            return cached;
        }
    }

    public static void RecordLevelResult(int levelNumber, int score)
    {
        if (levelNumber > Data.highestCompletedLevel)
        {
            Data.highestCompletedLevel = levelNumber;
        }

        LevelScore existing = Data.bestScores.Find(s => s.levelNumber == levelNumber);

        if (existing == null)
        {
            Data.bestScores.Add(new LevelScore { levelNumber = levelNumber, score = score });
        }
        else if (score > existing.score)
        {
            existing.score = score;
        }

        File.WriteAllText(SavePath, JsonUtility.ToJson(Data));
    }

    public static int GetBestScore(int levelNumber)
    {
        LevelScore found = Data.bestScores.Find(s => s.levelNumber == levelNumber);
        return found != null ? found.score : 0;
    }
}
```

- [ ] **Adım 2: WinGame'e kaydı ekle**

`GameManager.WinGame` içinde, bonus puan eklendikten SONRA:

```csharp
        SaveManager.RecordLevelResult(ActiveLevel.levelNumber, points);
```

- [ ] **Adım 3: Doğrula**

Bölümü kazan → `~/Library/Application Support/DefaultCompany/match3/save.json` oluşmalı ve doğru değerleri içermeli. Daha düşük skorla tekrar kazan → best score değişmemeli.

- [ ] **Adım 4: Commit**

```bash
git add Assets/Scripts/Levels/SaveManager.cs Assets/Scripts/GameManager.cs
git commit -m "feat: local JSON save for level progress and best scores"
```

---

### Task 8: Build Settings + Sahne Geçiş Butonları

**Files:**
- Modify: `ProjectSettings/EditorBuildSettings.asset` (Unity Editor üzerinden)
- Modify: `Assets/Scripts/ButtonControl.cs`

**Interfaces:**
- Consumes: `LevelCatalog`, `LevelLoader.selectedLevel` (Task 1), `GameManager.ActiveLevel` (Task 2).
- Produces: `ButtonControl.MainMenuScene` / `GameBoardScene` sabitleri — Task 9 kullanır.

- [ ] **Adım 1: Build Settings'i düzelt**

File → Build Profiles → Scene List: mevcut (silinmiş GemHunterMatch) girdilerinin hepsini kaldır. `Assets/Scenes/MainMenu.unity` (index 0) ve `Assets/Scenes/GameBoard.unity` (index 1) ekle.

- [ ] **Adım 2: ButtonControl.cs'i yeniden yaz**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonControl : MonoBehaviour
{
    public const string MainMenuScene = "MainMenu";
    public const string GameBoardScene = "GameBoard";

    [SerializeField] private LevelCatalog catalog;

    // Aynı bölümü yeniden başlatır (LevelLoader.selectedLevel değişmez).
    public void TryAgain()
    {
        SceneManager.LoadScene(GameBoardScene);
    }

    // Katalogdan sıradaki bölümü yükler; son bölümse menüye döner.
    public void NextLevel()
    {
        LevelData current = GameManager.Instance.ActiveLevel;
        int index = catalog.levels.IndexOf(current);

        if (index >= 0 && index + 1 < catalog.levels.Count)
        {
            LevelLoader.selectedLevel = catalog.levels[index + 1];
            SceneManager.LoadScene(GameBoardScene);
        }
        else
        {
            SceneManager.LoadScene(MainMenuScene);
        }
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene(MainMenuScene);
    }
}
```

- [ ] **Adım 3: Sahnede butonları yeniden bağla**

Eski `WinGame`/`LoseGame` OnClick bağlantıları kırılacak. GameBoard sahnesinde: NextLevel butonu → `NextLevel()`, TryAgain butonu → `TryAgain()`, BackToMenu butonu → `BackToMenu()`. ButtonControl objesine `catalog` referansını ata.

- [ ] **Adım 4: Doğrula**

Kazan → NextLevel → (tek level varken) MainMenu açılmalı. Kaybet → TryAgain → aynı bölüm yeniden başlamalı.

- [ ] **Adım 5: Commit**

```bash
git add ProjectSettings/EditorBuildSettings.asset Assets/Scripts/ButtonControl.cs Assets/Scenes
git commit -m "fix: build scene list and scene-name-based navigation buttons"
```

---

### Task 9: Ana Menü — Play Butonu + Level Listesi

**Files:**
- Create: `Assets/Scripts/Menu/HomePageController.cs`
- Create: `Assets/Scripts/Menu/LevelListItem.cs`
- Create (Unity Editor): `Assets/Prefabs/UI/LevelListItem.prefab` + MainMenu sahnesinde UI

**Interfaces:**
- Consumes: `LevelCatalog`, `LevelLoader.selectedLevel` (Task 1), `SaveManager` (Task 7), `ButtonControl.GameBoardScene` (Task 8).

- [ ] **Adım 1: Script'leri yaz**

`Assets/Scripts/Menu/LevelListItem.cs`:
```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text levelNumberText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Button button;

    public void Setup(LevelData level, bool unlocked, int bestScore, Action onClick)
    {
        levelNumberText.text = level.levelNumber.ToString();
        bestScoreText.text = bestScore > 0 ? bestScore.ToString() : "";
        lockIcon.SetActive(!unlocked);
        button.interactable = unlocked;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
    }
}
```

`Assets/Scripts/Menu/HomePageController.cs`:
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomePageController : MonoBehaviour
{
    [SerializeField] private LevelCatalog catalog;
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Text playButtonLabel;
    [SerializeField] private Transform levelListContent;
    [SerializeField] private LevelListItem levelItemPrefab;

    private void Start()
    {
        playButton.onClick.AddListener(PlayNextLevel);
        BuildUI();
    }

    // Sıradaki (henüz geçilmemiş) bölüm; hepsi bittiyse son bölüm.
    private LevelData GetNextLevel()
    {
        LevelData next = catalog.GetByNumber(SaveManager.Data.highestCompletedLevel + 1);
        return next != null ? next : catalog.levels[catalog.levels.Count - 1];
    }

    private void PlayNextLevel()
    {
        StartLevel(GetNextLevel());
    }

    private void StartLevel(LevelData level)
    {
        LevelLoader.selectedLevel = level;
        SceneManager.LoadScene(ButtonControl.GameBoardScene);
    }

    private void BuildUI()
    {
        playButtonLabel.text = "Level " + GetNextLevel().levelNumber;

        foreach (LevelData level in catalog.levels)
        {
            bool unlocked = level.levelNumber <= SaveManager.Data.highestCompletedLevel + 1;
            LevelListItem item = Instantiate(levelItemPrefab, levelListContent);
            item.Setup(level, unlocked, SaveManager.GetBestScore(level.levelNumber), () => StartLevel(level));
        }
    }
}
```

- [ ] **Adım 2: Unity'de UI'ı kur**

MainMenu sahnesinde PlayPage içine: büyük Play butonu (TMP label'lı) + altına `GridLayoutGroup`'lu bir ScrollView. `LevelListItem` prefab'ı yap (numara, best skor, kilit ikonu — kilit görseli için `maanetorn` paketindeki hazır UI sprite'ları kullanılabilir). `HomePageController`'ı PlayPage'e tak, tüm referansları ve `catalog`'u ata.

- [ ] **Adım 3: Doğrula**

Play Mode (MainMenu'den): Play butonunda "Level 1" yazmalı, listede Level 1 açık. Play → GameBoard, Level 1 açılmalı. Kazan → menüye dön → best skor listede görünmeli, buton "Level 2" göstermeli (Level 2 varsa).

- [ ] **Adım 4: Commit**

```bash
git add Assets/Scripts/Menu Assets/Prefabs/UI Assets/Scenes/MainMenu.unity
git commit -m "feat: main menu play flow with level list"
```

---

### Task 10: 3 Örnek Bölüm + Uçtan Uca Doğrulama

**Files:**
- Create (Unity Editor): `Assets/Prefabs/Boards/Level02_Board.prefab`, `Level03_Board.prefab`, `Assets/Data/Levels/Level_02.asset`, `Level_03.asset`

- [ ] **Adım 1: İki yeni bölüm tasarla**

- `Level_02`: farklı tahta şekli (örn. köşeleri boyanmamış), moves=18, goals: `{Collect, Red, 12}` + `{Collect, Blue, 12}` (puan hedefi yok — salt toplama bölümü).
- `Level_03`: ortası delikli şekil, moves=15, goals: `{Score, 800}` + `{Collect, Green, 15}`.
- İkisini de `LevelCatalog.levels`'e sırayla ekle.

- [ ] **Adım 2: Uçtan uca test listesi**

- [ ] Menü → Play → Level 1 → kazan → bonus puan eklendi → NextLevel → Level 2 açıldı
- [ ] Level 2'nin farklı şekli doğru kuruldu, taşlar delik üzerinden akıyor
- [ ] Kaybet → TryAgain → aynı bölüm sıfırdan başladı (hamle/hedefler resetlendi)
- [ ] BackToMenu → listede Level 1-2 skorları ve Level 3 kilidi doğru
- [ ] Oyunu kapat/aç → ilerleme korunuyor (save.json)
- [ ] Bomba + süper bomba şekilli tahtada sorunsuz patlıyor

- [ ] **Adım 3: Commit**

```bash
git add Assets/Data Assets/Prefabs/Boards Assets/Scenes
git commit -m "feat: add levels 2-3 with shaped boards and collect goals"
```

---

## Bilinen Sınırlar (bilinçli, bu fazda yapılmıyor)

- Kapalı hücre şekillerinde "izole cep" doğrulaması yok — tasarımcı sorumluluğu.
- Kalan hamlelerin bonusu görsel şovsuz, direkt eklenir.
- `ArrayLayout`/`CustPropertyDrawer` dosyaları duruyor (kullanım kalktı); temizlik + `CustPropertyDrawer`'ın `Editor/` klasörüne taşınması ayrı hijyen işi.
- Hamle-kalmadı (deadlock) tespiti ve shuffle yok — checklist Faz 4'te.
