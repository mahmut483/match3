# Gameboard Ayrıştırma Tasarımı

**Tarih:** 2026-09-21
**Kapsam:** `PotionBoard`, `Potion`, `GameManager`, `SpecialStrikes` ve yardımcıları; test ve assembly altyapısı.
**Amaç:** 2.200 satırlık `PotionBoard`'u tek sorumluluklu parçalara bölmek, kodu özellik bazlı klasörlere ve
assembly definition'lara taşımak, C# yazım kurallarını uygulamak. **Oyuncu davranışı birebir korunur.**

## Kararlar

| Konu | Karar |
|---|---|
| Klasör yapısı | Özellik bazlı (`Gameplay/Board`, `Gameplay/Potions`, `Gameplay/Strikes`, `Gameplay/Session`) |
| Assembly | `Match3.asmdef` (runtime), `Match3.Editor.asmdef`, `Match3.Tests.EditMode.asmdef`, `Match3.Tests.PlayMode.asmdef`; "Enable playmode tests for all assemblies" kapatılır |
| İsimlendirme | Kelimeler değişmez; yazım düzelir (PascalCase metot/özellik, camelCase parametre, alt çizgi yok, public alan → özellik, namespace = klasör). Bariz yazım hataları (`SetIndicies` → `SetIndices`) düzeltilir. Serialize edilen veri sınıflarının alan adları (`ArrayLayout.rows`, `LevelData.*`, `PotionGoal.*`) olduğu gibi kalır |
| Ayrıştırma derinliği | Yaklaşım 1: sorumluluğa göre böl, davranışı koru. Hücre sahipliği / işlem reddi / event tabanlı session **yok** |
| SOLID | Hafif: sınıf başına tek sorumluluk; arayüz yalnızca iki uygulaması olan yerde (şimdilik yok); DI konteyneri yok |
| Eşzamanlılık | Mevcut arcade modeli aynen kalır; `ChainContext` zaten işlem-yerel |
| Yorumlar | Ayrıştırma sırasında yeniden yazılır (aşağıdaki standart); ayrı bir ön geçiş yok |
| Kapsam dışı | Hamlesiz tahta, havuz boşken delik, B4 (süper bomba + eşzamanlı roket), `Menu/` ve `Backend/` kodu (yalnızca namespace satırı eklenir) |

## 1. Klasörler, assembly'ler, namespace'ler

```
Assets/Scripts/
  Match3.asmdef
  Gameplay/
    Board/     BoardDefinition, ArrayLayout, Node, BoardGrid, MatchResult (+MatchDirection),
               MatchFinder, BoardGeometry, BoardEffects, BoardRefill, SpecialChain, BoardInput, PotionBoard
    Potions/   Potion, BombMaskBinder, BombShadow
    Strikes/   SpecialStrikes, StrikePresentation, HammerStrikeView, BombStrikeView, CannonEntryView
    Session/   GameSession, GameManager, GameBoardUI, OutOfMoveController, CharacterAnimator
  Levels/      LevelData, LevelCatalog, LevelLoader (yerinde)
  Menu/        (yerinde)
  Backend/     (yerinde)
  Shared/      ButtonControl, GameAudioSettings
  Editor/
    Match3.Editor.asmdef
    CustPropertyDrawer                   ← bugün runtime assembly'de; player build'i kırıyor
Assets/Tests/
  EditMode/    Match3.Tests.EditMode.asmdef  (mevcut Editor/ testleri buraya)
  PlayMode/    Match3.Tests.PlayMode.asmdef  (#if UNITY_EDITOR korumaları kalkar)
```

- Namespace = klasör: `Match3.Gameplay.Board`, `Match3.Gameplay.Potions`, `Match3.Gameplay.Strikes`,
  `Match3.Gameplay.Session`, `Match3.Levels`, `Match3.Menu`, `Match3.Backend`, `Match3.Shared`.
- `Match3.asmdef` referansları: `Unity.TextMeshPro`, `Unity.InputSystem`, `spine-unity`, `CFXR Runtime`,
  `KinoBloom.Runtime`, Firebase precompiled DLL'leri (auto-reference değilse açıkça eklenir).
- Taşımalar `git mv` ile `.meta` dosyasıyla birlikte yapılır; GUID'ler korunur, sahne/prefab referansları kopmaz.
  Sınıf adları değişmediği için dosya adları da değişmez. Taşımalar Unity kapalıyken yapılır.

## 2. Tahta çekirdeği (saf C#)

**`Node`** — `IsUsable { get; }`, `Potion { get; set; }`.

**`BoardGrid`** — `Node[,]`'nin tek sahibi; `PotionBoard` diziye doğrudan dokunmaz.
```
BoardGrid(ArrayLayout layout)
Node this[int x, int y]
Potion PotionAt(Vector2Int cell)          // depo dışı / kapalı / boş → null
bool IsUsable(Vector2Int cell)
void Place(Potion potion, Vector2Int cell) // node + potion.SetIndices birlikte
void Clear(Vector2Int cell)
void Swap(Potion a, Potion b)
bool AnyPotionMoving()
IEnumerable<Potion> Potions
```

**`MatchFinder`** — durumsuz; bir `BoardGrid` okur.
```
MatchFinder(BoardGrid grid)
List<MatchResult> FindAll()                          // eski CheckBoard
List<MatchResult> FindAround(Potion first, Potion second)  // eski CollectSwapMatches
// private: IsConnected, CollectLine, SuperMatch, CheckDirection, ChooseProtected
```
Hayatta kalan taş (`ProtectedPotion`) seçimi sonuç kurulurken yapılır: `FindAll` rastgele üye,
`FindAround` eşleşmeyi yapan takas taşı.

**`MatchResult`** — kendi dosyasında, `MatchDirection` ile; `ConnectedPotions`, `Direction`,
`ProtectedPotion`, `IsSuperMatch`.

## 3. Runtime bileşenleri (hepsi `PotionBoard` objesinde)

Bağımlılık yönü tek: `PotionBoard` → yardımcılar. Yardımcılar `PotionBoard`'u geri çağırmaz.

| Bileşen | Inspector alanları | İşi | Kullandığı |
|---|---|---|---|
| `BoardGeometry` (saf C#) | — | `CellToWorld`, board kaydırma offset'i | — |
| `BoardEffects` | kırılma/patlama/roket particle'ları, 3 AudioSource, klipler, ses seviyeleri, `boardVfxRoot` | `SpawnBoardVfx`, `SpawnDestroyParticle`, `SpawnRocketParticle`, ses çalma, particle root, local-simulation | — |
| `BoardRefill` | `potionPrefabs`, `potionParent`, düşüş stagger ayarları | İlk taşları üretir (eşleşmesiz), havuz (`Acquire/Release`), `StartRefill(grid)` | Grid, Geometry |
| `SpecialChain` | `bombPoints`, süper bomba / çift roket zamanlamaları, `rocketSpeed` | `ClearCell`, `TriggerSpecial`, `SweepLine`, `BlastAround`, `ExplodeChain`, `SuperBombExplod`, `DoubleRocketExplode`, `ChainContext`; zinciri bitene kadar çalıştırır, refill yapmaz | Grid, Geometry, Effects, Refill, `GameManager` |
| `StrikePresentation` | Hammer/Bomb/Cannon prefab'ları, anchor, maskeler, süre ayarları | Üç sinematik; hücreleri temizler, zincir bitince döner; refill yapmaz | Geometry, Effects, SpecialChain |
| `BoardInput` | — | Pointer/seçim/tap/strike yönlendirmesi; `board.TrySwap(a, b)` / `board.TryTap(p)` | PotionBoard, SpecialStrikes |
| `PotionBoard` | `matchPoints`, `superMatchPoints`, `matchSettleDelay`, `superMatchMerge*`, `boardGrid`, `boardPresentation`, `specialStrikes` | Kurulum + orkestrasyon: swap akışı, `RemoveAndRefill`, `RefillAndCascade`, `ReturnPotionToPool` (hedef kaydı + havuz), `TryRunStrike` (sinematik → refill), giriş kapıları | hepsi |

Dışa açık yüzey değişmez: `SpecialStrikes` ve `GameBoardUI` bugün ne çağırıyorsa aynısını çağırır
(`InputLocked`, `IsCannonPresentationActive`, `CannonStrikeFinished`, `TryRunStrike`, `TryBeginCannonAim`,
`TryCancelCannonAim`).

### Sahne geçişi
1. Yeni bileşen eklenir; `PotionBoard`'daki eski alanlar henüz silinmez.
2. Tek kullanımlık Editor menü öğesi (`Match3/Migrate PotionBoard`) `SerializedObject` ile eski alanları
   okur, bileşeni objeye ekler (yoksa), yalnızca **boş** yeni alanları doldurur; sahne kaydedilir.
   İdempotent: her adımdan sonra aynı öğe tekrar çalıştırılır.
3. Tüm bileşenler bitince eski alanlar `PotionBoard`'dan, migration scripti projeden silinir.

## 4. Oyun kuralları

**`GameSession`** (saf C#): `Points`, `Moves`, `Goal`, `Goals` (kopya), `Outcome` (Playing/Won/Lost),
`IsEnded`; `AddPoints`, `RegisterCleared`, `EndTurn`. Kazanma `AddPoints`/`RegisterCleared` içinde,
kaybetme `EndTurn` içinde — bugünkü `TryWin`/`ProcessTurn` mantığı birebir. Event yok.

**`GameManager`** (MonoBehaviour): sahne adaptörü — `Instance`, `ActiveLevel`, paneller/TMP/ses/karakter/
butonlar, sahne geçişleri. Session'ı `Awake`'te kurar; `AddPoints`/`RegisterClearedPotion`/`ProcessTurn`
sarmalayıcıları session'ı çağırıp `RefreshHud()` ve sonuç sunumunu yapar. `isGameEnded` → `IsGameEnded`.

## 5. Adım sırası

Her adım tek başına derlenir, testler yeşil, oyun oynanır, ayrı commit.

| # | Adım | Unity tarafı |
|---|---|---|
| 0 | Klasörler + `git mv` + asmdef'ler + namespace'ler + `CustPropertyDrawer` → `Editor/` + test asmdef'leri + playmode ayarı kapanır. Sıfır mantık değişikliği | Unity kapalıyken; açınca import + derleme |
| 1 | `MatchResult`, `Node`, `BoardGrid`, `MatchFinder` + EditMode testleri; `PotionBoard` kullanır | — |
| 2 | `BoardGeometry` + `BoardEffects`; migration v1 | Migration çalıştır |
| 3 | `BoardRefill`; migration v2 | Migration |
| 4 | `SpecialChain`; migration v3 | Migration |
| 5 | `StrikePresentation`; migration v4 | Migration |
| 6 | `BoardInput` | — |
| 7 | Eski alanlar ve migration scripti silinir | Inspector kontrolü |
| 8 | `GameSession` + `GameSessionTests`; `GameManager` inceltilir; `GameManagerWinTests` kaldırılır | — |
| 9 | Yorum geçişi, plan/baseline dokümanları güncellenir | Oynanış matrisi |

## 6. Yorum standardı

- Sınıf başında 1–2 satır: bu ne, kim kullanır.
- Her metot üstünde 1–3 satır: ne yapar; belli değilse nasıl.
- Satır içi yorum yalnızca Unity tuzakları için (kapalı Animator'de `HasState`, CFXR child silme,
  `worldPositionStays`, `PlayOneShot` mikser grubu, Play On Awake/OnEnable gibi). Satır-satır anlatan
  tutorial yorumları silinir.

## 7. Testler

- EditMode: `BoardDefinitionTests` (mevcut), `BoardGridTests`, `MatchFinderTests` (bugünkü PlayMode eşleşme
  testleri buraya taşınır; `Potion` EditMode'da `AddComponent` ile oluşur), `GameSessionTests`.
- PlayMode: `PotionBoardCascadeTests` (harness yeni bileşenlere göre güncellenir).
- Her adımda: `dotnet build` (iki assembly) + izole batchmode EditMode/PlayMode koşusu.

## 8. Kabul kriterleri

1. Baseline oynanış matrisi aynı: geçerli/geçersiz swap, bomba/roket tap, bomba+bomba, roket+roket,
   Hammer/Bomb/Cannon, son hamlede kazanma, cascade sırasında giriş.
2. Tüm testler yeşil.
3. Player build derlenir (Editor kodu runtime assembly'de değil).
4. `PotionBoard.cs` ≤ ~400 satır; hiçbir dosya > ~500 satır.
5. Sahne/prefab referansları kopmadı; `PotionBoard` objesindeki bileşenlerde boş zorunlu alan yok.
