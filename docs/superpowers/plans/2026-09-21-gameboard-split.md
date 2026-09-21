# Gameboard Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Durum (2026-09-21):** Task 0–10 tamamlandı. Plandan sapmalar: (1) Task 5–6'da taşınan alanlar `PotionBoard`'dan
> erken silindiği için migration `SpecialChain.rocketSpeed` ve `StrikePresentation`'ın 12 alanını kopyalayamadı; değerler
> git HEAD'deki sahneden `GameBoard.unity`'ye elle geri yazıldı. (2) Hammer/Bomb/Cannon kilidi tek `isStrikeActive`
> bayrağında birleşti (`RunStrike` sarmalayıcısı). (3) `BoardInput` `SpecialStrikes` referansı almıyor; vuruş yönlendirmesi
> `PotionBoard.TryTap/TrySwap` içinde. (4) PlayMode test dosyalarındaki `#if UNITY_EDITOR` korumaları kaldı.

**Goal:** `PotionBoard` (2.200 satır) ve `GameManager`'ı tek sorumluluklu parçalara bölmek, kodu özellik bazlı klasörlere ve `Match3` assembly definition'larına taşımak, C# yazım kurallarını uygulamak — oyuncu davranışı birebir aynı kalarak.

**Architecture:** Saf C# çekirdek (`BoardGrid`, `MatchFinder`, `GameSession`) Unity'den bağımsız ve EditMode'da test edilir. Tahta davranışı `PotionBoard` objesindeki bileşenlere dağılır (`BoardEffects`, `BoardRefill`, `SpecialChain`, `StrikePresentation`, `BoardInput`); bağımlılık yönü tek: `PotionBoard` → yardımcılar. Inspector değerleri tek kullanımlık bir Editor menü scriptiyle yeni bileşenlere kopyalanır; eski alanlar en sonda silinir.

**Tech Stack:** Unity 6000.5.0f1, C# 9 (blok namespace; file-scoped namespace YOK), Unity Test Framework, Input System, TextMeshPro, Spine, Cartoon FX Remaster, Firebase.

**Spec:** `docs/superpowers/specs/2026-09-21-gameboard-split-design.md`

## Global Constraints

- Oyuncu davranışı birebir korunur: geçerli/geçersiz swap, bomba/roket tap, bomba+bomba, roket+roket, Hammer/Bomb/Cannon, son hamlede kazanma, cascade sırasında giriş.
- Kelimeler değişmez; yalnızca yazım düzelir (PascalCase metot/özellik, camelCase parametre, alt çizgi yok, public alan → özellik, namespace = klasör). Bariz yazım hataları düzeltilir (`SetIndicies` → `SetIndices`, `elaspeed` → `elapsed`).
- Serialize edilen **alan adları değişmez** (`potionType`, `potionPrefabs`, `ArrayLayout.rows`, `LevelData.*`, `PotionGoal.*` …). Özellik eklenirken backing alan adı aynen kalır; böylece sahne/prefab/asset değerleri korunur, `FormerlySerializedAs` gerekmez.
- Eşzamanlılık modeli aynen kalır (arcade; `ChainContext` işlem-yerel). Hücre sahipliği, işlem reddi, event tabanlı session **yok**.
- Kapsam dışı: hamlesiz tahta, havuz boşken delik, B4, `Menu/`/`Backend/` kodu (yalnızca `namespace` + `using` satırları).
- Dosya taşımaları `git mv` ile `.meta` dosyasıyla birlikte, **Unity kapalıyken**. Sınıf adı = dosya adı korunur.
- Her task sonunda: iki assembly derlenir, EditMode ve PlayMode testleri yeşil, oyun içi kısa deneme, ayrı commit (kullanıcı commit'ler; mesaj plan içinde verilir).
- Yorum standardı: sınıf başında 1–2 satır, metot üstünde 1–3 satır ("ne yapar; belli değilse nasıl"), satır içi yalnızca Unity tuzakları. Yeni yazılan her kod bu standarda uyar.

---

## Dosya yapısı (hedef)

```
Assets/Scripts/
  Match3.asmdef
  Gameplay/
    Board/     ArrayLayout.cs  BoardDefinition.cs  Node.cs  MatchResult.cs  BoardGrid.cs  MatchFinder.cs
               BoardGeometry.cs  BoardEffects.cs  BoardRefill.cs  SpecialChain.cs  BoardInput.cs  PotionBoard.cs
    Potions/   Potion.cs  BombMaskBinder.cs  BombShadow.cs
    Strikes/   SpecialStrikes.cs  StrikePresentation.cs  HammerStrikeView.cs  BombStrikeView.cs  CannonEntryView.cs
    Session/   GameSession.cs  GameManager.cs  GameBoardUI.cs  OutOfMoveController.cs  CharacterAnimator.cs
  Levels/      LevelData.cs  LevelCatalog.cs  LevelLoader.cs
  Menu/        (28 dosya, yerinde)
  Backend/     (7 dosya, yerinde)
  Shared/      ButtonControl.cs  GameAudioSettings.cs
  Editor/
    Match3.Editor.asmdef  CustPropertyDrawer.cs  PotionBoardMigration.cs (Task 3–8 arası geçici)
Assets/Tests/
  EditMode/    Match3.Tests.EditMode.asmdef  Gameboard/{BoardDefinitionTests, BoardGridTests, MatchFinderTests, GameSessionTests}.cs
  PlayMode/    Match3.Tests.PlayMode.asmdef  Gameboard/{PotionBoardTestBase, PotionBoardCascadeTests}.cs
```

**Test koşturma (tüm task'larda aynı):** Editor açıkken batchmode çalışmaz; izole kopya kullanılır.

```bash
ISO=/private/tmp/match3-iso            # bir kez: mkdir -p $ISO && cp -R Assets Packages ProjectSettings $ISO/
rsync -a --delete Assets/Scripts/ $ISO/Assets/Scripts/ && rsync -a --delete Assets/Tests/ $ISO/Assets/Tests/ \
  && rsync -a ProjectSettings/ $ISO/ProjectSettings/ && cp Assets/Scripts.meta Assets/Tests.meta $ISO/Assets/ 2>/dev/null
UNITY=/Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -nographics -projectPath $ISO -runTests -testPlatform EditMode -testResults /tmp/em.xml -logFile /tmp/em.log; echo exit=$?
$UNITY -batchmode -nographics -projectPath $ISO -runTests -testPlatform PlayMode -testResults /tmp/pm.xml -logFile /tmp/pm.log; echo exit=$?
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' /tmp/em.xml /tmp/pm.xml
grep -c "error CS" /tmp/em.log   # 0 olmalı
```

Derleme kontrolü: Unity açıldığında `Match3.csproj`, `Match3.Editor.csproj`, `Match3.Tests.EditMode.csproj`, `Match3.Tests.PlayMode.csproj` üretilir; `dotnet build <csproj> -nologo -v q -p:WarningLevel=0` ile saniyeler içinde derlenir. csproj'lar yoksa Unity'de **Edit → Preferences → External Tools → Regenerate project files**.

---

### Task 0: Klasörler, assembly'ler, namespace'ler (mantık değişikliği yok)

**Files:**
- Move (git mv, .meta ile): aşağıdaki liste
- Create: `Assets/Scripts/Match3.asmdef`, `Assets/Scripts/Editor/Match3.Editor.asmdef`, `Assets/Tests/EditMode/Match3.Tests.EditMode.asmdef`, `Assets/Tests/PlayMode/Match3.Tests.PlayMode.asmdef`
- Delete: `Assets/Scripts/UIManager.cs` (+ `.meta`) — boş şablon, hiçbir sahnede yok
- Modify: her `.cs` dosyasına `namespace` + gereken `using` satırları; `ProjectSettings/ProjectSettings.asset` (`playModeTestRunnerEnabled: 0`); `Assets/Scenes/MainMenu.unity` (UnityEvent tür adı)

**Interfaces:**
- Produces: namespace'ler `Match3.Gameplay.Board`, `Match3.Gameplay.Potions`, `Match3.Gameplay.Strikes`, `Match3.Gameplay.Session`, `Match3.Levels`, `Match3.Menu`, `Match3.Backend`, `Match3.Shared`, `Match3.Editor`; assembly adları `Match3`, `Match3.Editor`, `Match3.Tests.EditMode`, `Match3.Tests.PlayMode`.

- [ ] **Step 1: Unity'yi kapat, temiz ağaçtan başla**

`git status` yalnızca önceden var olan ilgisiz değişiklikleri (MainMenu.unity, Tilemap_002, MyFont, slnx, silinmiş eski testler) göstermeli. Unity Editor kapalı olmalı (`ls Temp/UnityLockfile` → yok).

- [ ] **Step 2: Dosyaları taşı**

```bash
cd Assets/Scripts
mkdir -p Gameplay/Board Gameplay/Potions Gameplay/Strikes Gameplay/Session Shared Editor
mv() { git mv "$1" "$2/$(basename "$1")" && git mv "$1.meta" "$2/$(basename "$1").meta"; }
for f in ArrayLayout.cs Board/BoardDefinition.cs Node.cs PotionBoard.cs; do mv "$f" Gameplay/Board; done
for f in Potion.cs BombMaskBinder.cs BombShadow.cs; do mv "$f" Gameplay/Potions; done
for f in SpecialStrikes.cs HammerStrikeView.cs BombStrikeView.cs CannonEntryView.cs; do mv "$f" Gameplay/Strikes; done
for f in GameManager.cs GameBoardUI.cs OutOfMoveController.cs CharacterAnimator.cs; do mv "$f" Gameplay/Session; done
for f in ButtonControl.cs GameAudioSettings.cs; do mv "$f" Shared; done
mv CustPropertyDrawer.cs Editor
git rm -q UIManager.cs UIManager.cs.meta
git rm -q Board.meta && rmdir Board          # eski Board klasörü boşaldı
cd ../Tests
git mv Editor EditMode && git mv Editor.meta EditMode.meta
```

Yeni klasörlerin `.meta`'larını Unity ilk açılışta üretir (klasör meta'ları GUID taşımaz, sorun yok).

- [ ] **Step 3: asmdef dosyalarını yaz**

`Assets/Scripts/Match3.asmdef`:
```json
{
    "name": "Match3",
    "rootNamespace": "Match3",
    "references": [
        "Unity.TextMeshPro",
        "Unity.InputSystem",
        "spine-unity",
        "CFXRRuntime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```
Firebase DLL'leri (`Assets/Firebase/Plugins/*.dll`) auto-reference; Unity "Firebase.* bulunamadı" derse `Firebase.App.Internal` referansa eklenir.

`Assets/Scripts/Editor/Match3.Editor.asmdef`:
```json
{
    "name": "Match3.Editor",
    "rootNamespace": "Match3.Editor",
    "references": [ "Match3" ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/Tests/EditMode/Match3.Tests.EditMode.asmdef`:
```json
{
    "name": "Match3.Tests.EditMode",
    "rootNamespace": "Match3.Tests",
    "references": [ "Match3", "UnityEngine.TestRunner", "UnityEditor.TestRunner", "Unity.TextMeshPro" ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/Tests/PlayMode/Match3.Tests.PlayMode.asmdef` (Editor-dışı platform içermeli, yoksa Unity onu EditMode sayar; bu yüzden `AssetDatabase`/`SerializedObject` kullanan harness `#if UNITY_EDITOR` içinde **kalır**):
```json
{
    "name": "Match3.Tests.PlayMode",
    "rootNamespace": "Match3.Tests",
    "references": [ "Match3", "UnityEngine.TestRunner", "UnityEditor.TestRunner", "Unity.TextMeshPro", "Unity.InputSystem" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`ProjectSettings/ProjectSettings.asset`: `playModeTestRunnerEnabled: 1` → `0`.

- [ ] **Step 4: Namespace ve using satırlarını ekle**

Her dosya blok namespace ile sarılır (C# 9; içerik 4 boşluk içeri). Şablon:
```csharp
using UnityEngine;              // mevcut using'ler namespace DIŞINDA kalır
using Match3.Gameplay.Potions;  // gereken yeni using'ler

namespace Match3.Gameplay.Board
{
    public class Node
    {
        ...
    }
}
```

Aşağıdaki script bunu bir kerede uygular (kök `Assets/Scripts`; `Editor/CustPropertyDrawer.cs` de dahil):

```python
import re, pathlib
S = pathlib.Path("Assets/Scripts")
plan = {
 "Match3.Gameplay.Board":   ["Gameplay/Board/ArrayLayout.cs","Gameplay/Board/BoardDefinition.cs","Gameplay/Board/Node.cs","Gameplay/Board/PotionBoard.cs"],
 "Match3.Gameplay.Potions": ["Gameplay/Potions/Potion.cs","Gameplay/Potions/BombMaskBinder.cs","Gameplay/Potions/BombShadow.cs"],
 "Match3.Gameplay.Strikes": ["Gameplay/Strikes/SpecialStrikes.cs","Gameplay/Strikes/HammerStrikeView.cs","Gameplay/Strikes/BombStrikeView.cs","Gameplay/Strikes/CannonEntryView.cs"],
 "Match3.Gameplay.Session": ["Gameplay/Session/GameManager.cs","Gameplay/Session/GameBoardUI.cs","Gameplay/Session/OutOfMoveController.cs","Gameplay/Session/CharacterAnimator.cs"],
 "Match3.Levels":  ["Levels/LevelData.cs","Levels/LevelCatalog.cs","Levels/LevelLoader.cs"],
 "Match3.Shared":  ["Shared/ButtonControl.cs","Shared/GameAudioSettings.cs"],
 "Match3.Editor":  ["Editor/CustPropertyDrawer.cs"],
 "Match3.Menu":    [str(p.relative_to(S)) for p in (S/"Menu").glob("*.cs")],
 "Match3.Backend": [str(p.relative_to(S)) for p in (S/"Backend").glob("*.cs")],
}
usings = {  # dosya → gereken ek using'ler (kod taraması ile çıkarıldı)
 "Editor/CustPropertyDrawer.cs": ["Match3.Gameplay.Board"],
 "Gameplay/Board/Node.cs": ["Match3.Gameplay.Potions"],
 "Gameplay/Board/PotionBoard.cs": ["Match3.Gameplay.Potions","Match3.Gameplay.Session","Match3.Gameplay.Strikes","Match3.Levels"],
 "Gameplay/Session/GameBoardUI.cs": ["Match3.Gameplay.Board","Match3.Shared"],
 "Gameplay/Session/GameManager.cs": ["Match3.Gameplay.Potions","Match3.Levels","Match3.Shared"],
 "Gameplay/Strikes/SpecialStrikes.cs": ["Match3.Gameplay.Board","Match3.Gameplay.Potions","Match3.Gameplay.Session","Match3.Levels"],
 "Levels/LevelData.cs": ["Match3.Gameplay.Board","Match3.Gameplay.Potions"],
 "Menu/MainMenuPlayButton.cs": ["Match3.Backend","Match3.Levels","Match3.Shared"],
 "Menu/MainMenuSettingsUI.cs": ["Match3.Shared"],
}
for f in ["AvatarImage","ChatBubbleUI","ClanChatPanel","ClanCreatePanel","ClanEditPanel","ClanHeaderUI","ClanInfoPanelUI",
          "ClanListPanel","ClanMemberRowUI","ClanPageController","ClanRowUI","ClanSearchPanel","LeaderboardPanel",
          "LifeRequestUI","MainMenuResourceBarUI","ProfilePanel"]:
    usings.setdefault(f"Menu/{f}.cs", []).insert(0, "Match3.Backend")

for ns, files in plan.items():
    for rel in files:
        p = S / rel; src = p.read_text(encoding="utf-8")
        if re.search(r"^\s*namespace ", src, re.M): continue
        lines = src.splitlines()
        head = []  # using satırları + #if guard'lar dosya başında
        i = 0
        while i < len(lines) and (lines[i].startswith("using ") or lines[i].startswith("#") or lines[i].strip() == ""):
            head.append(lines[i]); i += 1
        body = lines[i:]
        extra = [f"using {u};" for u in usings.get(rel, []) if f"using {u};" not in src]
        # #if UNITY_EDITOR ... #endif bloğu using'leri sarıyorsa extra'yı onun ÖNÜNE koy
        insert_at = next((k for k, l in enumerate(head) if l.startswith("#if")), len(head))
        head = head[:insert_at] + extra + head[insert_at:]
        while head and head[-1].strip() == "": head.pop()
        out = head + ["", f"namespace {ns}", "{"] + ["    " + l if l.strip() else "" for l in body] + ["}"]
        p.write_text("\n".join(out) + "\n", encoding="utf-8")
print("done")
```

Sonra elle kontrol: `GameManager.cs` başındaki `#if UNITY_EDITOR / using UnityEditor; / #endif` bloğu namespace'in dışında kaldı mı; `Potion.cs` başındaki boş satır sorun değil.

Testler: `Assets/Tests/EditMode/Gameboard/*.cs` ve `Assets/Tests/PlayMode/Gameboard/*.cs` dosyalarının başına `using Match3.Gameplay.Board; using Match3.Gameplay.Potions; using Match3.Gameplay.Session; using Match3.Levels;` eklenir (kullanmayan dosyada fazla using derleme hatası değildir). `BoardDefinitionTests` reflection ile `typeof(PotionBoard).Assembly.GetType("BoardDefinition")` arıyor → `"Match3.Gameplay.Board.BoardDefinition"` olarak düzeltilir (ya da reflection kaldırılıp doğrudan `BoardDefinition.IsPlayable(...)` çağrılır — tercih edilen).

- [ ] **Step 5: MainMenu sahnesindeki UnityEvent tür adını güncelle**

`Assets/Scenes/MainMenu.unity` içinde `ClanTabButtons, Assembly-CSharp` (7 yer) → `Match3.Menu.ClanTabButtons, Match3`:
```bash
sed -i '' 's/ClanTabButtons, Assembly-CSharp/Match3.Menu.ClanTabButtons, Match3/g' Assets/Scenes/MainMenu.unity
grep -c "Match3.Menu.ClanTabButtons, Match3" Assets/Scenes/MainMenu.unity   # 7
```
Not: MainMenu.unity'de kullanıcının önceden var olan, commit'lenmemiş değişiklikleri var; bu sed yalnızca 7 satıra dokunur.

- [ ] **Step 6: Unity'yi aç, derlemeyi doğrula**

Unity açılınca Console'da hata olmamalı. Sık hatalar ve çözümleri:
- `The type or namespace name 'Firebase' could not be found` → `Match3.asmdef` references'a `"Firebase.App.Internal"` ekle.
- `CFXR_Effect could not be found` → asmdef adı `CFXRRuntime` (boşluksuz) olmalı.
- `Spine.Unity` bulunamadı → `spine-unity` referansı.
- Test asmdef'inde `LogAssert`/`UnityTest` bulunamadı → `UnityEngine.TestRunner` referansı ve `UNITY_INCLUDE_TESTS` define'ı.

**Edit → Preferences → External Tools → Regenerate project files**, sonra:
```bash
for p in Match3 Match3.Editor Match3.Tests.EditMode Match3.Tests.PlayMode; do dotnet build $p.csproj -nologo -v q -p:WarningLevel=0 | tail -2; done
```
Beklenen: dört assembly 0 hata.

- [ ] **Step 7: Testleri koş**

Yukarıdaki izole komutlarla EditMode (12/12) ve PlayMode (9/9) yeşil.

- [ ] **Step 8: Sahneleri gözle doğrula**

GameBoard: bir takas, bir bomba, bir Hammer. MainMenu: klan sekmesindeki üç buton (`CreatePage/JoinPage/SearchPage`) tıklanınca sayfa değişiyor (UnityEvent tür adı güncellemesinin kanıtı). Inspector'da hiçbir component "Missing script" değil.

- [ ] **Step 9: Commit**

```
refactor: move scripts into feature folders with Match3 assembly definitions

- Feature-based folders under Scripts/Gameplay, Shared, Editor; namespaces
  mirror folders (block-scoped, C# 9).
- Match3, Match3.Editor and EditMode/PlayMode test assembly definitions;
  CustPropertyDrawer leaves the runtime assembly (it broke player builds).
- Drop "playmode tests for all assemblies"; delete the empty UIManager.
- MainMenu UnityEvents now name Match3.Menu.ClanTabButtons, Match3.
No logic changes; git mv keeps GUIDs.
```

---

### Task 1: `Potion` yazım kuralları

**Files:**
- Modify: `Assets/Scripts/Gameplay/Potions/Potion.cs`
- Modify: `Assets/Scripts/Gameplay/Board/PotionBoard.cs`, `Assets/Scripts/Gameplay/Board/Node.cs`, `Assets/Scripts/Gameplay/Potions/BombMaskBinder.cs`, `Assets/Scripts/Gameplay/Strikes/SpecialStrikes.cs`, `Assets/Tests/PlayMode/Gameboard/*.cs`

**Interfaces:**
- Produces (`Potion`): `PotionType PotionType { get; set; }` (backing alan `potionType` — serialize adı aynı), `int XIndex { get; private set; }`, `int YIndex { get; private set; }`, `bool IsMoving { get; private set; }`, `void SetIndices(int x, int y)`, `void SetSelectedVisual(bool isPressing)`. Diğer public üyeler zaten PascalCase.

- [ ] **Step 1: Potion alanlarını özellik yap**

`Potion.cs` başı:
```csharp
    [SerializeField] private PotionType potionType;   // prefab'da atanır; serialize adı değişmez
    private PotionType originalPotionType;

    public PotionType PotionType { get => potionType; set => potionType = value; }
    public int XIndex { get; private set; }
    public int YIndex { get; private set; }

    // Hareket bitene kadar true; tahta bu taşı eşleşmeye ve refill'e katmaz.
    public bool IsMoving { get; private set; }
```
`xIndex/yIndex/isMoving` alanları silinir; sınıf içindeki tüm `isMoving = …` → `IsMoving = …`, `xIndex` → `XIndex`, `yIndex` → `YIndex`, `potionType` okumaları aynı kalır (backing alan). `SetIndicies` → `SetIndices`, `setSelectedVisual` → `SetSelectedVisual`, `MoveCoroutine` içindeki `elaspeed` → `elapsed`, parametre adlarındaki alt çizgiler kalkar (`_targetPos` → `targetPos`; yerel `Vector3 targetPos` ile çakışan yerde yerel `target` olur).

- [ ] **Step 2: Çağıran yerleri güncelle**

Proje genelinde (Scripts + Tests):
```bash
grep -rln "\.xIndex\|\.yIndex\|\.isMoving\|\.potionType\|SetIndicies\|setSelectedVisual" Assets/Scripts Assets/Tests
```
Her eşleşme: `.xIndex` → `.XIndex`, `.yIndex` → `.YIndex`, `.isMoving` → `.IsMoving`, `.potionType` → `.PotionType`, `SetIndicies(` → `SetIndices(`, `setSelectedVisual(` → `SetSelectedVisual(`. `PotionBoard.DoSwap` içindeki doğrudan `xIndex = …` atamaları `SetIndices(...)` çağrısına dönüşür; `ReturnPotionToPool` içindeki `item.isMoving = false;` satırı silinir (`OnDisable` zaten sıfırlıyor, hemen ardından `SetActive(false)` geliyor).

- [ ] **Step 3: Derle ve test et**

Dört csproj 0 hata; EditMode 12/12, PlayMode 9/9.

- [ ] **Step 4: Commit**

```
refactor(potions): expose Potion state as properties, fix casing

PotionType/XIndex/YIndex/IsMoving become properties (backing field names and
prefab serialization unchanged); SetIndices/SetSelectedVisual casing fixed.
```

---

### Task 2: `Node`, `MatchResult`, `BoardGrid`, `MatchFinder` + EditMode testleri

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/MatchResult.cs`, `BoardGrid.cs`, `MatchFinder.cs`
- Modify: `Assets/Scripts/Gameplay/Board/Node.cs`, `PotionBoard.cs`
- Create: `Assets/Tests/EditMode/Gameboard/BoardGridTests.cs`, `MatchFinderTests.cs`
- Delete: `Assets/Tests/PlayMode/Gameboard/PotionBoardMatchTests.cs` (EditMode'a taşınır)

**Interfaces:**
- Consumes: `Potion.PotionType/XIndex/YIndex/IsMoving/SetIndices` (Task 1), `BoardDefinition`.
- Produces:
  - `Node { bool IsUsable {get;} ; Potion Potion {get;set;} ; Node(bool isUsable) }`
  - `MatchResult { List<Potion> ConnectedPotions; MatchDirection Direction; Potion ProtectedPotion; bool IsSuperMatch }`, `enum MatchDirection { Vertical, Horizontal, LongVertical, LongHorizontal, Super, None }`
  - `BoardGrid(ArrayLayout layout)`; `Node this[int x, int y]`; `Potion PotionAt(Vector2Int)`; `bool IsUsable(Vector2Int)`; `void Place(Potion, Vector2Int)`; `void Clear(Vector2Int)`; `void Swap(Potion, Potion)`; `bool AnyPotionMoving()`; `IEnumerable<Potion> Potions`
  - `MatchFinder(BoardGrid)`; `List<MatchResult> FindAll()`; `List<MatchResult> FindAround(Potion first, Potion second)`

- [ ] **Step 1: `Node.cs`**

```csharp
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
```

- [ ] **Step 2: `MatchResult.cs`** — `PotionBoard.cs` sonundaki `MatchResult` sınıfı ve `MatchDirection` enum'u buraya taşınır, alanlar özellik olur:

```csharp
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
```

- [ ] **Step 3: Başarısız `BoardGridTests` yaz** (`Assets/Tests/EditMode/Gameboard/BoardGridTests.cs`)

```csharp
using NUnit.Framework;
using UnityEngine;
using Match3.Gameplay.Board;
using Match3.Gameplay.Potions;

public class BoardGridTests
{
    private readonly System.Collections.Generic.List<GameObject> spawned = new();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned) Object.DestroyImmediate(go);
        spawned.Clear();
    }

    private Potion NewPotion(PotionType type = PotionType.Red)
    {
        GameObject go = new("potion");
        spawned.Add(go);
        Potion potion = go.AddComponent<Potion>();   // EditMode: Awake koşmaz
        potion.PotionType = type;
        return potion;
    }

    private static ArrayLayout OpenLayout() => new();   // ArrayLayout varsayılanı 15x6, hepsi açık

    private static ArrayLayout LayoutWithBlocked(int x, int y)
    {
        ArrayLayout layout = new();
        layout.rows[y].row[x] = true;
        return layout;
    }

    [Test]
    public void Constructor_BuildsBlockedAndOpenNodesFromLayout()
    {
        BoardGrid grid = new(LayoutWithBlocked(2, 3));

        Assert.That(grid[2, 3].IsUsable, Is.False);
        Assert.That(grid[0, 0].IsUsable, Is.True);
        Assert.That(grid.IsUsable(new Vector2Int(2, 3)), Is.False);
    }

    [Test]
    public void Place_SetsNodeAndPotionIndices()
    {
        BoardGrid grid = new(OpenLayout());
        Potion potion = NewPotion();

        grid.Place(potion, new Vector2Int(4, 6));

        Assert.That(grid[4, 6].Potion, Is.SameAs(potion));
        Assert.That(potion.XIndex, Is.EqualTo(4));
        Assert.That(potion.YIndex, Is.EqualTo(6));
        Assert.That(grid.PotionAt(new Vector2Int(4, 6)), Is.SameAs(potion));
    }

    [Test]
    public void PotionAt_ReturnsNullOutsideStorageAndOnBlockedOrEmptyCells()
    {
        BoardGrid grid = new(LayoutWithBlocked(1, 1));
        grid.Place(NewPotion(), new Vector2Int(0, 0));

        Assert.That(grid.PotionAt(new Vector2Int(-1, 0)), Is.Null);
        Assert.That(grid.PotionAt(new Vector2Int(0, 15)), Is.Null);
        Assert.That(grid.PotionAt(new Vector2Int(1, 1)), Is.Null);
        Assert.That(grid.PotionAt(new Vector2Int(5, 7)), Is.Null);
    }

    [Test]
    public void Swap_ExchangesCellsAndIndices()
    {
        BoardGrid grid = new(OpenLayout());
        Potion a = NewPotion(PotionType.Red);
        Potion b = NewPotion(PotionType.Blue);
        grid.Place(a, new Vector2Int(0, 0));
        grid.Place(b, new Vector2Int(1, 0));

        grid.Swap(a, b);

        Assert.That(grid[0, 0].Potion, Is.SameAs(b));
        Assert.That(grid[1, 0].Potion, Is.SameAs(a));
        Assert.That(a.XIndex, Is.EqualTo(1));
        Assert.That(b.XIndex, Is.EqualTo(0));
    }

    [Test]
    public void Clear_EmptiesCellWithoutTouchingPotion()
    {
        BoardGrid grid = new(OpenLayout());
        Potion potion = NewPotion();
        grid.Place(potion, new Vector2Int(3, 3));

        grid.Clear(new Vector2Int(3, 3));

        Assert.That(grid[3, 3].Potion, Is.Null);
        Assert.That(potion.XIndex, Is.EqualTo(3));
    }

    [Test]
    public void Potions_EnumeratesOnlyOccupiedCells()
    {
        BoardGrid grid = new(OpenLayout());
        grid.Place(NewPotion(), new Vector2Int(0, 0));
        grid.Place(NewPotion(), new Vector2Int(5, 14));

        Assert.That(grid.Potions, Has.Exactly(2).Items);
    }
}
```

- [ ] **Step 4: Testi koş, derleme hatasıyla düştüğünü gör** (`BoardGrid` yok).

- [ ] **Step 5: `BoardGrid.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Node[,] dizisinin tek sahibi. Hücre içeriğini ve taşın indekslerini
    // birlikte günceller; tahta kodu diziye doğrudan dokunmaz.
    public sealed class BoardGrid
    {
        private readonly Node[,] cells;

        // Layout'ta true olan hücre kapalıdır (taş almaz).
        public BoardGrid(ArrayLayout layout)
        {
            cells = new Node[BoardDefinition.VisibleWidth, BoardDefinition.TotalHeight];

            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
                {
                    cells[x, y] = new Node(!layout.rows[y].row[x]);
                }
            }
        }

        public Node this[int x, int y] => cells[x, y];

        public bool IsUsable(Vector2Int cell)
        {
            return BoardDefinition.IsWithinStorage(cell) && cells[cell.x, cell.y].IsUsable;
        }

        // Depo dışı, kapalı veya boş hücre için null.
        public Potion PotionAt(Vector2Int cell)
        {
            return IsUsable(cell) ? cells[cell.x, cell.y].Potion : null;
        }

        public void Place(Potion potion, Vector2Int cell)
        {
            cells[cell.x, cell.y].Potion = potion;
            potion.SetIndices(cell.x, cell.y);
        }

        public void Clear(Vector2Int cell)
        {
            cells[cell.x, cell.y].Potion = null;
        }

        // İki taşın hücrelerini ve indekslerini değiştirir; dünya konumu çağıranın işi.
        public void Swap(Potion a, Potion b)
        {
            Vector2Int cellA = new(a.XIndex, a.YIndex);
            Vector2Int cellB = new(b.XIndex, b.YIndex);

            Place(a, cellB);
            Place(b, cellA);
        }

        public bool AnyPotionMoving()
        {
            foreach (Potion potion in Potions)
            {
                if (potion.IsMoving) return true;
            }

            return false;
        }

        public IEnumerable<Potion> Potions
        {
            get
            {
                foreach (Node node in cells)
                {
                    if (node.Potion != null) yield return node.Potion;
                }
            }
        }
    }
}
```

- [ ] **Step 6: `BoardGridTests` yeşil.**

- [ ] **Step 7: Başarısız `MatchFinderTests` yaz** — bugünkü `PotionBoardMatchTests` senaryoları, sahnesiz:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Match3.Gameplay.Board;
using Match3.Gameplay.Potions;

public class MatchFinderTests
{
    private readonly List<GameObject> spawned = new();
    private BoardGrid grid;
    private MatchFinder finder;

    [SetUp]
    public void SetUp()
    {
        grid = new BoardGrid(new ArrayLayout());
        finder = new MatchFinder(grid);

        // Eşleşmesiz desen: (x + 2y) % 3 → Red/Blue/Yellow; Green desende yok.
        for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
        {
            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                grid.Place(NewPotion((PotionType)((x + 2 * y) % 3)), new Vector2Int(x, y));
            }
        }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned) Object.DestroyImmediate(go);
        spawned.Clear();
    }

    private Potion NewPotion(PotionType type)
    {
        GameObject go = new("potion");
        spawned.Add(go);
        Potion potion = go.AddComponent<Potion>();
        potion.PotionType = type;
        return potion;
    }

    private void SetType(PotionType type, params Vector2Int[] cells)
    {
        foreach (Vector2Int cell in cells) grid[cell.x, cell.y].Potion.PotionType = type;
    }

    private static IEnumerable<Vector2Int> CellsOf(MatchResult group)
    {
        return group.ConnectedPotions.Select(p => new Vector2Int(p.XIndex, p.YIndex));
    }

    [Test]
    public void NoMatchPattern_ProducesNoGroups()
    {
        Assert.That(finder.FindAll(), Is.Empty);
    }

    [Test]
    public void ThreeInARow_IsHorizontalMatch()
    {
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.Horizontal));
        Assert.That(groups[0].IsSuperMatch, Is.False);
        Assert.That(CellsOf(groups[0]), Is.EquivalentTo(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }));
    }

    [Test]
    public void FourInAColumn_IsLongVerticalWithProtectedMember()
    {
        SetType(PotionType.Green, new(5, 2), new(5, 3), new(5, 4), new(5, 5));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.LongVertical));
        Assert.That(groups[0].ConnectedPotions, Has.Count.EqualTo(4));
        Assert.That(groups[0].ProtectedPotion, Is.Not.Null);
        Assert.That(groups[0].ConnectedPotions, Has.Member(groups[0].ProtectedPotion));
    }

    [Test]
    public void TShape_IsSuperMatchWithAllFiveCells()
    {
        SetType(PotionType.Green, new(0, 3), new(1, 3), new(2, 3), new(1, 4), new(1, 5));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.Super));
        Assert.That(groups[0].ConnectedPotions, Has.Count.EqualTo(5));
    }

    [Test]
    public void LShape_IsSuperMatch()
    {
        SetType(PotionType.Green, new(0, 4), new(0, 5), new(0, 6), new(1, 4), new(2, 4));

        List<MatchResult> groups = finder.FindAll();

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.Super));
        Assert.That(groups[0].ConnectedPotions, Has.Count.EqualTo(5));
    }

    [Test]
    public void TwoSeparateLines_AreTwoGroups()
    {
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0), new(3, 6), new(4, 6), new(5, 6));

        Assert.That(finder.FindAll(), Has.Count.EqualTo(2));
    }

    [Test]
    public void SpecialPotions_DoNotMatchEachOther()
    {
        SetType(PotionType.Bomb, new(0, 0), new(1, 0), new(2, 0));
        SetType(PotionType.Rocket, new(3, 1), new(3, 2), new(3, 3));

        Assert.That(finder.FindAll(), Is.Empty);
    }

    [Test]
    public void SpawnRows_AreNotScanned()
    {
        SetType(PotionType.Green, new(0, 8), new(1, 8), new(2, 8));

        Assert.That(finder.FindAll(), Is.Empty);
    }

    [Test]
    public void FindAround_OnlyReportsGroupsOfTheTwoPotions_AndProtectsTheSwappedOne()
    {
        // Sağ uçta swap taşı: (3,0) grubun parçası; ayrıca (0,5..7) ilgisiz bir dikey üçlü.
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(0, 5), new(0, 6), new(0, 7));
        Potion swapped = grid[3, 0].Potion;
        Potion other = grid[3, 1].Potion;

        List<MatchResult> groups = finder.FindAround(swapped, other);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Direction, Is.EqualTo(MatchDirection.LongHorizontal));
        Assert.That(groups[0].ProtectedPotion, Is.SameAs(swapped));
    }
}
```

- [ ] **Step 8: Testin derleme hatasıyla düştüğünü gör.**

- [ ] **Step 9: `MatchFinder.cs`** — `PotionBoard`'daki `CheckBoard`, `CollectSwapMatches`, `IsConnected`, `CollectLine`, `SuperMatch`, `CheckDirection`, `ChooseSuperMatchTarget` buraya taşınır. `currentMatchGroups` alanı yerine sonuç **döndürülür**:

```csharp
using System.Collections.Generic;
using UnityEngine;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Tahtadaki eşleşmeleri bulur. Durumsuz; her çağrı grid'i yeniden okur.
    public sealed class MatchFinder
    {
        private readonly BoardGrid grid;

        public MatchFinder(BoardGrid grid)
        {
            this.grid = grid;
        }

        // Görünür alandaki tüm grupları bulur. Havadaki taşlar eşleşmeye girmez;
        // bir gruba girmiş taş ikinci kez taranmaz ve başka grubun koluna katılmaz.
        public List<MatchResult> FindAll()
        {
            List<MatchResult> groups = new();
            HashSet<Potion> matched = new();

            for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
            {
                for (int y = 0; y < BoardDefinition.VisibleHeight; y++)
                {
                    Potion potion = grid[x, y].Potion;

                    if (potion == null || potion.IsMoving || matched.Contains(potion)) continue;

                    MatchResult line = IsConnected(potion, matched);

                    if (line.ConnectedPotions.Count < 3) continue;

                    MatchResult group = SuperMatch(line, matched);

                    if (group.IsSuperMatch) group.ProtectedPotion = ChooseProtected(group, null);

                    groups.Add(group);
                    matched.UnionWith(group.ConnectedPotions);
                }
            }

            return groups;
        }

        // Yalnızca takas edilen iki taşın gruplarını bulur; tahtanın geri kalanı
        // cascade'in işidir. Süper eşleşmede eşleşmeyi yapan takas taşı korunur.
        public List<MatchResult> FindAround(Potion first, Potion second)
        {
            List<MatchResult> groups = new();
            HashSet<Potion> matched = new();

            foreach (Potion potion in new[] { first, second })
            {
                if (potion == null || matched.Contains(potion)) continue;

                MatchResult line = IsConnected(potion, matched);

                if (line.ConnectedPotions.Count < 3) continue;

                MatchResult group = SuperMatch(line, matched);

                if (group.IsSuperMatch) group.ProtectedPotion = ChooseProtected(group, potion);

                groups.Add(group);
                matched.UnionWith(group.ConnectedPotions);
            }

            return groups;
        }

        // Tercih edilen taş gruptaysa o, değilse rastgele bir üye.
        private static Potion ChooseProtected(MatchResult group, Potion preferred)
        {
            if (preferred != null && group.ConnectedPotions.Contains(preferred)) return preferred;

            return group.ConnectedPotions[Random.Range(0, group.ConnectedPotions.Count)];
        }

        // (IsConnected, CollectLine, SuperMatch, CheckDirection gövdeleri PotionBoard'dan
        //  aynen taşınır; potionBoard[x, y].potion → grid[x, y].Potion,
        //  .potionType → .PotionType, .isMoving → .IsMoving, .xIndex → .XIndex,
        //  MatchResult alanları → özellikler.)
    }
}
```

- [ ] **Step 10: `PotionBoard`'u yeni çekirdeğe bağla**

- `private Node[,] potionBoard` → `private BoardGrid grid; private MatchFinder matchFinder;`
- `InitializeBoard`: `grid = new BoardGrid(levelLayout); matchFinder = new MatchFinder(grid);` — hücre döngüsünde `potionBoard[x, y] = new Node(false, null)` satırı kalkar (grid kurdu); taş üretilince `grid.Place(potion, new Vector2Int(x, y))`.
- `potionBoard[x, y].potion` okumaları → `grid[x, y].Potion`; `= null` yazmaları → `grid.Clear(cell)`; `= newPotion` yazmaları → `grid.Place(newPotion, cell)`.
- `CheckBoard()` çağrıları → `List<MatchResult> groups = matchFinder.FindAll(); hasMatched = groups.Count > 0;` ve `RemoveAndRefill(groups)`; `CollectSwapMatches(a, b)` → `matchFinder.FindAround(a, b)`; `currentMatchGroups` alanı silinir.
- `PotionAt` → `grid.PotionAt`; `IsAnyPotionMoving()` → `grid.AnyPotionMoving()`; `IsSamePotionType/WouldCreateInitialMatch` → `grid.PotionAt(new Vector2Int(x, y)) is { } p && p.PotionType == candidateType`.
- `DoSwap`: grid kısmı `grid.Swap(current, target)`; dünya konumu hesabı aynen kalır.
- `PotionBoard.cs` sonundaki `MatchResult`/`MatchDirection` tanımları silinir.
- `Node(bool, Potion)` kurucusu kullanan yerler kalmaz.

- [ ] **Step 11: PlayMode testlerini güncelle**

`PotionBoardTestBase`: `grid = (Node[,])…GetField("potionBoard")` → `grid = Field<BoardGrid>("grid")`; `grid[x, y].potion.potionType` → `grid[x, y].Potion.PotionType`; `SetType` aynı mantık. `PotionBoardMatchTests.cs` (+ `.meta`) silinir — senaryolar `MatchFinderTests`'te.

- [ ] **Step 12: Derle, test et, oyna**

Dört csproj 0 hata; EditMode (BoardDefinition 9 + BoardGrid 6 + MatchFinder 9 + GameManagerWin 3 = 27) yeşil; PlayMode (cascade 1) yeşil. Oyunda: normal 3'lü, T şekli (bomba), 4'lü (roket), geçersiz takas.

- [ ] **Step 13: Commit**

```
refactor(board): extract BoardGrid and MatchFinder from PotionBoard

BoardGrid owns the Node array and keeps cell contents and potion indices in
step; MatchFinder returns match groups instead of filling a shared list.
Match scan tests move from PlayMode to EditMode (MatchFinderTests).
```

---

### Task 3: `BoardGeometry` + `BoardEffects` + migration v1

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/BoardGeometry.cs`, `BoardEffects.cs`
- Create: `Assets/Scripts/Editor/PotionBoardMigration.cs`
- Modify: `PotionBoard.cs`

**Interfaces:**
- Produces:
  - `BoardGeometry(float cellSize, Transform boardPresentation)`; `Vector2 CellToWorld(Vector2Int cell)`; `float CellSize`; `Vector3 PresentationHome`
  - `BoardEffects : MonoBehaviour` — `void Initialize(Transform boardPresentation)`; `ParticleSystem SpawnBoardVfx(ParticleSystem prefab, Vector3 pos, Quaternion rot)`; `void SpawnDestroyParticle(Potion)`; `void SpawnRocketParticle(Potion)`; `void PlayMatch()`, `PlaySuperMatch()`, `PlayExplosion()`, `PlayBombFuse()`, `PlayDoubleRocketMerge()`; `ParticleSystem ExplodingParticles`, `SuperExplodingParticles`, `RocketFireParticles`, `DoubleRocketParticles` (prefab erişimi; SpecialChain konumu kendisi hesaplar); `static void SetParticleSimulationLocal(GameObject)`

- [ ] **Step 1: `BoardGeometry.cs`**

```csharp
using UnityEngine;

namespace Match3.Gameplay.Board
{
    // Hücre → dünya konumu. Cannon board'u kaydırdığında offset'i ekler; taşlar
    // BoardPresentation'ın çocuğu olduğu için bu her zaman görsel hücre merkezidir.
    public sealed class BoardGeometry
    {
        private readonly Transform boardPresentation;
        private readonly float spacingX;
        private readonly float spacingY;

        public float CellSize { get; }
        public Vector3 PresentationHome { get; }

        public BoardGeometry(float cellSize, Transform boardPresentation)
        {
            CellSize = cellSize;
            this.boardPresentation = boardPresentation;
            PresentationHome = boardPresentation != null ? boardPresentation.position : Vector3.zero;
            spacingX = (BoardDefinition.VisibleWidth - 1) / 2f;
            spacingY = (BoardDefinition.TotalHeight / 2) - 2.5f;   // eski InitializeBoard hesabı: int bölme
        }

        public Vector2 CellToWorld(Vector2Int cell)
        {
            Vector2 world = new((cell.x - spacingX) * CellSize, (cell.y - spacingY) * CellSize);

            if (boardPresentation != null)
            {
                Vector3 offset = boardPresentation.position - PresentationHome;
                world += new Vector2(offset.x, offset.y);
            }

            return world;
        }
    }
}
```
`spacingY`: eski kod `(float)((height) / 2) - 2.5f` — `15 / 2 = 7` (int) → `4.5f`. Aynı sonucu ver.

- [ ] **Step 2: `BoardEffects.cs`** — `PotionBoard`'dan taşınan alanlar (adlar aynı): `destroyParticlesRed/Blue/Green/Yellow`, `explodingPaticles`, `superExplodingParticles`, `rocketSpawnParticles`, `rocketFireParticles`, `doubleRocketParticles`, `matchSource`, `superMatchSource`, `explodingSource`, `matchClip`, `superMatchClip`, `explodingClip`, `bombClip`, `doubleRocketClip`, 5 `*Volume`, `boardVfxRoot`; metotlar: `SpawnBoardVfx`, `GetBoardVfxRoot`, `SetBoardParticleSimulationLocal` (→ `SetParticleSimulationLocal`, public static), `SpawnDestroyParticle`, `SpawnRocketParticle`; `Awake`'teki `Resources.Load` fallback'i (`BombClipResourcePath`, `DoubleRocketClipResourcePath`) buraya.

```csharp
using UnityEngine;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Tahtaya ait görsel ve ses efektleri: particle prefab'ları, ses kaynakları,
    // efektlerin BoardPresentation altında doğması.
    public sealed class BoardEffects : MonoBehaviour
    {
        private const string BombClipResourcePath = "SFX/superBombSound";
        private const string DoubleRocketClipResourcePath = "SFX/duableRocket";

        [SerializeField] private ParticleSystem destroyParticlesRed;
        // … (PotionBoard'daki alanlar Tooltip/Header yorumlarıyla aynen)

        private Transform boardPresentation;

        public ParticleSystem ExplodingParticles => explodingPaticles;
        public ParticleSystem SuperExplodingParticles => superExplodingParticles != null ? superExplodingParticles : explodingPaticles;
        public ParticleSystem RocketFireParticles => rocketFireParticles;
        public ParticleSystem DoubleRocketParticles => doubleRocketParticles;

        private void Awake()
        {
            // Sahne referansı eksikse Resources'taki sabit yoldan yüklenir.
            if (bombClip == null) bombClip = Resources.Load<AudioClip>(BombClipResourcePath);
            if (doubleRocketClip == null) doubleRocketClip = Resources.Load<AudioClip>(DoubleRocketClipResourcePath);
        }

        public void Initialize(Transform boardPresentation)
        {
            this.boardPresentation = boardPresentation;
        }

        public void PlayMatch() => matchSource.PlayOneShot(matchClip, matchVolume);
        public void PlaySuperMatch() => superMatchSource.PlayOneShot(superMatchClip, superMatchVolume);
        public void PlayExplosion() => explodingSource.PlayOneShot(explodingClip, explodingVolume);

        // Süper bomba fitili; klip yoksa sessiz kalır.
        public void PlayBombFuse()
        {
            if (bombClip != null) explodingSource.PlayOneShot(bombClip, bombVolume);
        }

        public void PlayDoubleRocketMerge()
        {
            if (doubleRocketClip != null) explodingSource.PlayOneShot(doubleRocketClip, doubleRocketVolume);
        }

        // (SpawnBoardVfx, GetBoardVfxRoot, SpawnDestroyParticle, SpawnRocketParticle,
        //  SetParticleSimulationLocal gövdeleri PotionBoard'dan aynen taşınır.)
    }
}
```

- [ ] **Step 3: `PotionBoard` bağlantısı**

```csharp
        private BoardGeometry geometry;
        private BoardEffects effects;

        private void Awake()
        {
            effects = GetComponent<BoardEffects>();
            effects.Initialize(boardPresentation);
            geometry = new BoardGeometry(cellSize, boardPresentation);
        }
```
`CellToWorld(...)` → `geometry.CellToWorld(...)`; `boardPresentationHomePosition` → `geometry.PresentationHome`; `spacingX/spacingY` alanları silinir; `SpawnBoardVfx/SpawnDestroyParticle/SpawnRocketParticle/SetBoardParticleSimulationLocal` çağrıları `effects.` üzerinden; ses satırları (`matchSource.PlayOneShot(...)` vb.) → `effects.PlayMatch()` vb.; `Awake`'teki `Resources.Load` bloğu kalkar. Eski alanlar `PotionBoard`'da **kalır** (Task 8'de silinir).

- [ ] **Step 4: Migration scripti (v1)** — `Assets/Scripts/Editor/PotionBoardMigration.cs`

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Match3.Gameplay.Board;

namespace Match3.Editor
{
    // Geçici: PotionBoard'daki Inspector değerlerini yeni bileşenlere kopyalar.
    // Her task'ta bileşen listesi büyür; Task 8'de script silinir.
    public static class PotionBoardMigration
    {
        [MenuItem("Match3/Migrate PotionBoard")]
        private static void Run()
        {
            PotionBoard board = Object.FindFirstObjectByType<PotionBoard>();

            if (board == null)
            {
                Debug.LogError("Sahnede PotionBoard yok.");
                return;
            }

            SerializedObject source = new(board);

            Copy<BoardEffects>(board, source,
                "destroyParticlesRed", "destroyParticlesBlue", "destroyParticlesGreen", "destroyParticlesYellow",
                "explodingPaticles", "superExplodingParticles", "rocketSpawnParticles", "rocketFireParticles",
                "doubleRocketParticles", "matchSource", "superMatchSource", "explodingSource",
                "matchClip", "superMatchClip", "explodingClip", "bombClip", "doubleRocketClip",
                "matchVolume", "superMatchVolume", "explodingVolume", "bombVolume", "doubleRocketVolume",
                "boardVfxRoot");

            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("PotionBoard migration tamam.");
        }

        // Aynı adlı alanları kaynaktan hedef bileşene kopyalar; bileşen yoksa ekler.
        private static void Copy<T>(PotionBoard board, SerializedObject source, params string[] fields) where T : Component
        {
            T target = board.GetComponent<T>();
            if (target == null) target = Undo.AddComponent<T>(board.gameObject);

            SerializedObject destination = new(target);

            foreach (string field in fields)
            {
                SerializedProperty property = source.FindProperty(field);

                if (property == null)
                {
                    Debug.LogWarning($"PotionBoard.{field} bulunamadı (zaten silinmiş olabilir).");
                    continue;
                }

                if (destination.FindProperty(field) == null)
                {
                    Debug.LogError($"{typeof(T).Name}.{field} yok; alan adları aynı olmalı.");
                    continue;
                }

                destination.CopyFromSerializedProperty(property);
            }

            destination.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
```

- [ ] **Step 5: Kullanıcı: Unity'de GameBoard sahnesi açıkken `Match3 → Migrate PotionBoard`.** Inspector'da `PotionBoard` objesinde `Board Effects` bileşeni, tüm alanları dolu. Sahne kaydedildi (`GameBoard.unity` diff'inde yeni MonoBehaviour bloğu).

- [ ] **Step 6: PlayMode harness'ı güncelle** — `PotionBoardTestBase.SetUpBoard`: `BoardEffects effects = boardObject.AddComponent<BoardEffects>();` ve `matchSource/superMatchSource/explodingSource/matchClip/superMatchClip/explodingClip` artık `new SerializedObject(effects)` üzerinden atanır; `PotionBoard` eklenmeden **önce** eklenir (Awake'te `GetComponent` bulsun).

- [ ] **Step 7: Derle, test et, oyna** (efektler ve sesler eskisi gibi).

- [ ] **Step 8: Commit** — `refactor(board): extract BoardGeometry and BoardEffects; add scene migration menu`

---

### Task 4: `BoardRefill` + migration v2

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/BoardRefill.cs`
- Modify: `PotionBoard.cs`, `Editor/PotionBoardMigration.cs`, `Tests/PlayMode/Gameboard/PotionBoardTestBase.cs`

**Interfaces:**
- Consumes: `BoardGrid`, `BoardGeometry`.
- Produces: `BoardRefill : MonoBehaviour` — `void Initialize(BoardGrid grid, BoardGeometry geometry)`; `void CreateInitialPotions()`; `void StartRefill()`; `void Release(Potion potion)` (ClearSpecial + kapat + havuza koy; hedef kaydı **çağıranın** işi); alanlar `potionPrefabs`, `potionParent`, `dropStaggerDelay`, `maxDropStagger`.

- [ ] **Step 1: `BoardRefill.cs`** — taşınanlar: `deactivePotionPool`, `InitializeBoard`'daki taş üretme döngüsü (→ `CreateInitialPotions`), `GetValidPotionPrefabIndex`, `WouldCreateInitialMatch`, `StartRefill`, `RefillPotion`, `SpawnPotionAtTop`; `ReturnPotionToPool`'un havuz kısmı → `Release`. Sınıf başlığı:

```csharp
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

        public void Initialize(BoardGrid grid, BoardGeometry geometry) { this.grid = grid; this.geometry = geometry; }

        // Temizlenen taş: özel görselleri sıfırlanır, kapatılır, havuza girer.
        public void Release(Potion potion)
        {
            potion.ClearSpecial();
            potion.gameObject.SetActive(false);
            if (!deactivePotionPool.Contains(potion.gameObject)) deactivePotionPool.Add(potion.gameObject);
        }
        // … taşınan metotlar
    }
```

- [ ] **Step 2: `PotionBoard`**: `refill = GetComponent<BoardRefill>(); refill.Initialize(grid, geometry);` (grid `Start`'ta kurulduğu için `Initialize` de `Start`'ta, `CreateInitialPotions` öncesi). `ReturnPotionToPool` şu hale gelir:

```csharp
        // Temizlenen taş hedeflere sayılır (özel taş ayrıca kendi hedefine) ve havuza döner.
        private void ReturnPotionToPool(Potion item)
        {
            PotionType clearedType = item.PotionType;

            refill.Release(item);   // ClearSpecial tipi orijinaline döndürür

            GameManager.Instance.RegisterClearedPotion(item.PotionType);
            if (clearedType != item.PotionType) GameManager.Instance.RegisterClearedPotion(clearedType);
        }
```
`StartRefill()` çağrıları → `refill.StartRefill()`.

- [ ] **Step 3: Migration v2** — `Run()` içine: `Copy<BoardRefill>(board, source, "potionPrefabs", "potionParent", "dropStaggerDelay", "maxDropStagger");`

- [ ] **Step 4: Kullanıcı migration'ı çalıştırır;** Inspector'da `Board Refill` dolu.

- [ ] **Step 5: Harness**: `potionPrefabs`/`potionParent` artık `BoardRefill`'in `SerializedObject`'ine yazılır.

- [ ] **Step 6: Derle, test et, oyna** (cascade ve refill).

- [ ] **Step 7: Commit** — `refactor(board): extract BoardRefill (initial fill, refill, pool)`

---

### Task 5: `SpecialChain` + migration v3

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/SpecialChain.cs`
- Modify: `PotionBoard.cs`, `Editor/PotionBoardMigration.cs`

**Interfaces:**
- Consumes: `BoardGrid`, `BoardGeometry`, `BoardEffects`, `BoardRefill` değil — taşı serbest bırakma **callback** ile.
- Produces: `SpecialChain : MonoBehaviour` — `void Initialize(BoardGrid grid, BoardGeometry geometry, BoardEffects effects, Action<Potion> releasePotion)`; `ChainContext` (public nested class); `void ClearCell(Vector2Int cell, ChainContext chain)`; `void BlastAround(Vector2Int center, ChainContext chain)`; `IEnumerator ExplodeChain(Potion first)` (zincir bitene kadar; refill YOK); `IEnumerator SuperBombExplode(Potion target, Potion merged)`; `IEnumerator DoubleRocketExplode(Potion horizontal, Potion vertical)`; alanlar `bombPoints`, `mergedBombHideDelay`, `explosionSettleDelay`, `superBombRingDelay`, `doubleRocketDelay`, `rocketSpeed`.

- [ ] **Step 1: `SpecialChain.cs`** — taşınanlar: `SpecialTrigger`, `ChainContext`, `TriggerSpecial`, `RunSweep`, `BlastAround`, `SweepLine`, `FlyOutAndPool`, `AnyAlive`, `ClearCell`, `HideAfter`, `SuperBombExplod` (→ `SuperBombExplode`, sondaki `yield return RefillAndCascade()` **kalkar**), `DoubleRocketExplode` (aynı şekilde), `ExplodeChain` (aynı). `ReturnPotionToPool(x)` çağrıları → `releasePotion(x)`; `explodingSource.PlayOneShot(...)` → `effects.PlayExplosion()`; VFX çağrıları `effects.` üzerinden; `CellToWorld` → `geometry.CellToWorld`; grid yazmaları `grid.Clear(cell)` / `grid.Place`. `GameManager.Instance.AddPoints(bombPoints)` aynen (session adaptörü).

- [ ] **Step 2: `PotionBoard`**: `chain = GetComponent<SpecialChain>(); chain.Initialize(grid, geometry, effects, ReturnPotionToPool);` `ProcessMatches` içindeki özel çözüm:
```csharp
            IEnumerator specialResolution =
                bothBombs ? chain.SuperBombExplode(currentPotion, targetPotion)
                : bothRockets ? chain.DoubleRocketExplode(currentPotion, targetPotion)
                : specialToTrigger != null ? chain.ExplodeChain(specialToTrigger)
                : null;

            if (specialResolution != null)
            {
                yield return specialResolution;
                yield return RefillAndCascade();     // zincirden sonra refill PotionBoard'un işi
            }
```
`TapDetonate`: `yield return chain.ExplodeChain(special); yield return RefillAndCascade(); GameManager.Instance.ProcessTurn();`. Hammer/Bomb/Cannon rutinleri (henüz PotionBoard'da) `ClearCell/BlastAround` çağrılarını `chain.` üzerinden yapar; `ChainContext` → `SpecialChain.ChainContext`.

- [ ] **Step 3: Migration v3** — `Copy<SpecialChain>(board, source, "bombPoints", "mergedBombHideDelay", "explosionSettleDelay", "superBombRingDelay", "doubleRocketDelay", "rocketSpeed");`

- [ ] **Step 4: Kullanıcı migration'ı çalıştırır.**

- [ ] **Step 5: Harness**: `boardObject.AddComponent<SpecialChain>()` (PotionBoard'dan önce).

- [ ] **Step 6: Derle, test et, oyna** — bomba tap, roket tap, bomba+bomba (7x7), roket+roket (artı), roket süpürmesinin yoldaki bombayı tetiklemesi.

- [ ] **Step 7: Commit** — `refactor(board): extract SpecialChain (bomb/rocket chains, super bomb, double rocket)`

---

### Task 6: `StrikePresentation` + migration v4

**Files:**
- Create: `Assets/Scripts/Gameplay/Strikes/StrikePresentation.cs`
- Modify: `PotionBoard.cs`, `Editor/PotionBoardMigration.cs`

**Interfaces:**
- Consumes: `BoardGeometry`, `SpecialChain` (+`ChainContext`), `BoardEffects.SetParticleSimulationLocal`.
- Produces: `StrikePresentation : MonoBehaviour` — `void Initialize(BoardGeometry geometry, SpecialChain chain, Transform boardPresentation)`; `bool HasCannonPresentation()`; `IEnumerator PlayHammer(Vector2Int origin, IEnumerable<Vector2Int> cells, Transform strikeSource)`; `IEnumerator PlayBomb(Vector2Int origin, Transform strikeSource)`; `IEnumerator AimCannon()` (slide, `IsAwaitingTarget = true`); `IEnumerator CancelCannonAim()`; `IEnumerator FireCannon(Vector2Int origin)`; `bool IsCannonCinematic { get; }`, `bool IsAwaitingTarget { get; }`, `bool IsCannonFiring { get; }`, `event Action CannonStrikeFinished`; tüm Hammer/Bomb/Cannon alanları.

Her `Play*` coroutine'i sinematiği oynatır, hücreleri `chain.ClearCell/BlastAround` ile temizler, `chain.running == 0` olana kadar bekler ve **döner**; refill ve bayrak yönetimi (`isHammerStrikeActive` vb.) `PotionBoard`'da kalır.

- [ ] **Step 1: `StrikePresentation.cs`** — taşınanlar: `HammerStrikeRoutine` (→ `PlayHammer`; `try/finally`'deki `isHammerStrikeActive = false` **çıkarılır**, `Destroy(travelRoot)` kalır), `BombStrikeRoutine` (→ `PlayBomb`), `GetStrikeStartPosition`, `CannonAimRoutine` (→ `AimCannon`), `CancelCannonAimRoutine` (→ `CancelCannonAim`), `CannonStrikeRoutine` (→ `FireCannon`; `RefillAndCascade` satırı kalkar; `finally`'deki bayrak sıfırlamaları `PotionBoard`'a), `CannonballSweep`, `MovePresentationTo`, `SetCannonMasks`, `HasCannonPresentation`; `isCannonCinematic/isCannonAwaitingTarget/isCannonFiring` bu sınıfta özellik olur.

- [ ] **Step 2: `PotionBoard`**: `strikes = GetComponent<StrikePresentation>(); strikes.Initialize(geometry, chain, boardPresentation);`

```csharp
        public bool IsCannonPresentationActive => strikes.IsCannonCinematic;
        public event System.Action CannonStrikeFinished
        {
            add => strikes.CannonStrikeFinished += value;
            remove => strikes.CannonStrikeFinished -= value;
        }

        // Özel vuruş: sinematik oynar, zincir biter, sonra tahta dolar. false = hak düşmez.
        public bool TryRunStrike(StrikeKind kind, Vector2Int origin, IEnumerable<Vector2Int> cells, Transform strikeSource = null)
        {
            if (kind != StrikeKind.Cannon && (strikes.IsCannonCinematic || isHammerStrikeActive || isBombStrikeActive)) return false;

            switch (kind)
            {
                case StrikeKind.Cannon:
                    if (!strikes.IsCannonCinematic || !strikes.IsAwaitingTarget) return false;
                    StartCoroutine(RunStrike(strikes.FireCannon(origin), () => { }));
                    return true;
                case StrikeKind.Hammer:
                    if (cells == null || !strikes.HasHammer) return false;
                    isHammerStrikeActive = true;
                    StartCoroutine(RunStrike(strikes.PlayHammer(origin, cells, strikeSource), () => isHammerStrikeActive = false));
                    return true;
                case StrikeKind.Bomb:
                    if (!strikes.HasBomb) return false;
                    isBombStrikeActive = true;
                    StartCoroutine(RunStrike(strikes.PlayBomb(origin, strikeSource), () => isBombStrikeActive = false));
                    return true;
            }

            return false;
        }

        // Sinematik bitince tahta dolar; bayrak ne olursa olsun bırakılır.
        private IEnumerator RunStrike(IEnumerator presentation, System.Action release)
        {
            try
            {
                yield return presentation;
                yield return RefillAndCascade();
            }
            finally
            {
                release();
            }
        }

        public bool TryBeginCannonAim()
        {
            if (strikes.IsCannonCinematic || isHammerStrikeActive || isBombStrikeActive || !strikes.HasCannonPresentation()) return false;
            StartCoroutine(strikes.AimCannon());
            return true;
        }

        public bool TryCancelCannonAim()
        {
            if (!strikes.IsCannonCinematic || strikes.IsCannonFiring) return false;
            StartCoroutine(strikes.CancelCannonAim());
            return true;
        }
```
`HasHammer`/`HasBomb`: `hammerStrikePrefab != null` / `bombStrikePrefab != null`. Cannon'ın `isCannonFiring`/`isCannonCinematic` sıfırlaması `FireCannon`'ın `finally`'sinde kalır (sunumun kendi durumu).

`Update`'teki kapı: `(strikes.IsCannonCinematic && !strikes.IsAwaitingTarget) || isHammerStrikeActive || isBombStrikeActive || InputLocked`; oyun bitince `if (strikes.IsAwaitingTarget) TryCancelCannonAim();`.

- [ ] **Step 3: Migration v4** — `Copy<StrikePresentation>(board, source, "cannonLeftAnchor", "cannonEntryPrefab", "cannonballProjectilePrefab", "cannonMaskRight", "cannonMaskRightDuplicate", "hammerStrikePrefab", "hammerTravelDuration", "hammerImpactDelay", "hammerReturnDelay", "hammerReturnDuration", "hammerStrikeDuration", "bombStrikePrefab", "bombTravelDuration", "bombImpactDelay", "boardSlideDuration", "boardReturnDuration", "cannonBoardSlideCells", "cannonFireDelay", "cannonballSpeed", "cannonballExitPadding");`

- [ ] **Step 4: Kullanıcı migration'ı çalıştırır.**

- [ ] **Step 5: Harness**: `AddComponent<StrikePresentation>()`.

- [ ] **Step 6: Derle, test et, oyna** — Hammer, Bomb, Cannon (nişan, iptal, ateş), cascade sırasında Cannon.

- [ ] **Step 7: Commit** — `refactor(strikes): extract StrikePresentation (hammer, bomb, cannon cinematics)`

---

### Task 7: `BoardInput`

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/BoardInput.cs`
- Modify: `PotionBoard.cs`, `Assets/Scenes/GameBoard.unity` (bileşen ekleme — migration v5)

**Interfaces:**
- Consumes: `PotionBoard.AcceptsInput`, `PotionBoard.IsAwaitingCannonTarget`, `PotionBoard.PotionAt(Vector2Int)`, `PotionBoard.TrySwap(Potion, Potion)`, `PotionBoard.TryTap(Potion)`, `SpecialStrikes.TryUseOn`.
- Produces: `BoardInput : MonoBehaviour` (`[SerializeField] PotionBoard board; SpecialStrikes specialStrikes;` — migration v5 bunları `GetComponent<PotionBoard>()` ve PotionBoard'un `specialStrikes` alanından doldurur).

- [ ] **Step 1: `PotionBoard` giriş API'si**

```csharp
        // BoardInput bu kapıya bakar; true ise tahta yeni dokunuş kabul eder.
        public bool AcceptsInput =>
            !GameManager.Instance.IsGameEnded &&
            !(strikes.IsCannonCinematic && !strikes.IsAwaitingTarget) &&
            !isHammerStrikeActive && !isBombStrikeActive && !InputLocked;

        public bool IsAwaitingCannonTarget => strikes.IsAwaitingTarget;
        public Potion PotionAt(Vector2Int cell) => grid.PotionAt(cell);

        // Komşu, duran ve hâlâ kendi hücresinde iki taşı takas eder. false: yok sayıldı.
        public bool TrySwap(Potion current, Potion target)   // eski SwapPotion + CanSwapNow + BeginSwap
        public void TryTap(Potion tapped)                     // eski Update'in "else" dalı: strike → yoksa özel taş patlat
```

- [ ] **Step 2: `BoardInput.cs`** — `PotionBoard.Update`'in tamamı, `ClearPointerSelection`, `FinishPointerSwap`, `firstSelectedPotion/secondSelectedPotion/waitForPointerRelease` buraya taşınır; `Update` başı:

```csharp
        private void Update()
        {
            if (Pointer.current == null) return;

            if (!board.AcceptsInput)
            {
                ClearPointerSelection();
                return;
            }
            // … eski Update gövdesi; SwapPotion(...) → board.TrySwap(...) (true dönerse FinishPointerSwap),
            //   tapped-akışı → board.TryTap(tapped)
        }
```
`PotionBoard.Update` silinir.

- [ ] **Step 3: Migration v5** — `Run()` sonuna:
```csharp
            BoardInput input = board.GetComponent<BoardInput>();
            if (input == null) input = Undo.AddComponent<BoardInput>(board.gameObject);
            SerializedObject inputObject = new(input);
            inputObject.FindProperty("board").objectReferenceValue = board;
            inputObject.FindProperty("specialStrikes").objectReferenceValue = source.FindProperty("specialStrikes").objectReferenceValue;
            inputObject.ApplyModifiedPropertiesWithoutUndo();
```

- [ ] **Step 4: Kullanıcı migration'ı çalıştırır.** Harness'ta `BoardInput` **eklenmez** (sahte fare gereksinimi kalkar; `InputSystem.AddDevice<Mouse>` satırları silinir).

- [ ] **Step 5: Derle, test et, oyna** — seçim çerçevesi, sürükleme, tap, strike seçiliyken tap, ayarlar paneli açıkken kilit.

- [ ] **Step 6: Commit** — `refactor(board): extract BoardInput from PotionBoard.Update`

---

### Task 8: Eski alanları ve migration scriptini sil

**Files:**
- Modify: `PotionBoard.cs`
- Delete: `Assets/Scripts/Editor/PotionBoardMigration.cs` (+ `.meta`)
- Modify: `Assets/Scenes/GameBoard.unity` (Unity yeniden kaydeder; eski alan satırları düşer)

- [ ] **Step 1:** `PotionBoard`'dan Task 3–7'de taşınan tüm `[SerializeField]` alanlar (Effects 23, Refill 4, Chain 6, Strikes 21) ve `cellSize` dışında kalan artık kullanılmayan alanlar silinir. Kalanlar: `matchPoints`, `superMatchPoints`, `matchSettleDelay`, `superMatchMergeSpeed/MinDuration/MaxDuration`, `boardGrid`, `boardPresentation`, `specialStrikes`, `cellSize`.
- [ ] **Step 2:** Migration scripti silinir.
- [ ] **Step 3:** Unity'de sahneyi aç, `PotionBoard` Inspector'ında yalnızca kalan alanlar; diğer bileşenlerde boş referans yok. Sahneyi kaydet.
- [ ] **Step 4:** Derle, test et, oyna (tam matris). `wc -l PotionBoard.cs` ≤ ~400.
- [ ] **Step 5: Commit** — `refactor(board): drop migrated Inspector fields from PotionBoard, remove migration menu`

---

### Task 9: `GameSession` + `GameManager` inceltme

**Files:**
- Create: `Assets/Scripts/Gameplay/Session/GameSession.cs`
- Create: `Assets/Tests/EditMode/Gameboard/GameSessionTests.cs`
- Modify: `Assets/Scripts/Gameplay/Session/GameManager.cs`
- Delete: `Assets/Tests/EditMode/Gameboard/GameManagerWinTests.cs` (+ `.meta`)

**Interfaces:**
- Produces: `GameSession(LevelData level)`; `int Points`, `int Moves`, `int Goal`; `IReadOnlyList<PotionGoal> Goals`; `SessionOutcome Outcome`; `bool IsEnded`; `void AddPoints(int)`; `void RegisterCleared(PotionType)`; `void EndTurn()`; `enum SessionOutcome { Playing, Won, Lost }`.
- `GameManager`: `bool IsGameEnded => session.IsEnded` (eski `isGameEnded` kaldırılır; çağıran `PotionBoard.AcceptsInput` ve `SpecialStrikes.Toggle` güncellenir), `AddPoints/RegisterClearedPotion/ProcessTurn` sarmalayıcıları.

- [ ] **Step 1: Başarısız `GameSessionTests`**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Match3.Gameplay.Potions;
using Match3.Gameplay.Session;
using Match3.Levels;

public class GameSessionTests
{
    private LevelData level;

    [TearDown]
    public void TearDown() { if (level != null) Object.DestroyImmediate(level); }

    private GameSession NewSession(int goal, int moves, params PotionGoal[] goals)
    {
        level = ScriptableObject.CreateInstance<LevelData>();
        level.goal = goal;
        level.moves = moves;
        level.potionGoals = new List<PotionGoal>(goals);
        return new GameSession(level);
    }

    [Test]
    public void ReachingPointGoal_WinsImmediately()
    {
        GameSession session = NewSession(goal: 10, moves: 5);

        session.AddPoints(10);

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Won));
    }

    [Test]
    public void CompletingCollectGoal_WinsImmediately()
    {
        GameSession session = NewSession(goal: 0, moves: 5, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        session.RegisterCleared(PotionType.Red);

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Won));
    }

    [Test]
    public void UnfinishedCollectGoal_DoesNotWin()
    {
        GameSession session = NewSession(goal: 10, moves: 5, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        session.AddPoints(10);

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Playing));
    }

    [Test]
    public void LastMoveWithoutGoal_Loses()
    {
        GameSession session = NewSession(goal: 100, moves: 1);

        session.EndTurn();

        Assert.That(session.Moves, Is.EqualTo(0));
        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Lost));
    }

    [Test]
    public void WinBeforeLastTurnEnds_StaysWon()
    {
        GameSession session = NewSession(goal: 10, moves: 1);

        session.AddPoints(10);
        session.EndTurn();

        Assert.That(session.Outcome, Is.EqualTo(SessionOutcome.Won));
        Assert.That(session.Moves, Is.EqualTo(1), "Oyun bittikten sonra hamle düşmez.");
    }

    [Test]
    public void Goals_AreCopied_LevelAssetIsNotMutated()
    {
        PotionGoal source = new() { potionType = PotionType.Blue, amount = 2 };
        GameSession session = NewSession(goal: 0, moves: 5, source);

        session.RegisterCleared(PotionType.Blue);

        Assert.That(session.Goals[0].amount, Is.EqualTo(1));
        Assert.That(source.amount, Is.EqualTo(2));
    }

    [Test]
    public void RegisterCleared_IgnoresTypesWithoutGoalAndGoalsAtZero()
    {
        GameSession session = NewSession(goal: 100, moves: 5, new PotionGoal { potionType = PotionType.Red, amount = 1 });

        session.RegisterCleared(PotionType.Green);
        session.RegisterCleared(PotionType.Red);
        session.RegisterCleared(PotionType.Red);

        Assert.That(session.Goals[0].amount, Is.EqualTo(0));
    }
}
```

- [ ] **Step 2: Derleme hatasıyla düştüğünü gör.**

- [ ] **Step 3: `GameSession.cs`**

```csharp
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

        // Puan hedefi ve TÜM toplama hedefleri tamamlandığı anda kazanılır; yolu fark etmez.
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
```

- [ ] **Step 4: `GameSessionTests` yeşil.**

- [ ] **Step 5: `GameManager` inceltme**

`goal/moves/points/potionGoals/isGameEnded` alanları → `private GameSession session;` `Initialize(level)` → `session = new GameSession(level); SetupGoalSlots(); RefreshHud();`. Sarmalayıcılar:

```csharp
        public bool IsGameEnded => session.IsEnded;

        public void AddPoints(int amount)
        {
            session.AddPoints(amount);
            RefreshHud();
            PresentOutcome();
        }

        public void RegisterClearedPotion(PotionType type)
        {
            session.RegisterCleared(type);
            RefreshHud();
            PresentOutcome();
        }

        public void ProcessTurn()
        {
            if (session.IsEnded) return;

            session.EndTurn();
            RefreshHud();

            if (session.Moves <= 3 && session.Moves != 0 && !isPlayedlast3MovesClip)
            {
                audioSource.PlayOneShot(last3MoveClip, last3MoveVolume);
                isPlayedlast3MovesClip = true;
            }

            PresentOutcome();
        }

        // Sonuç sunumu bir kez: kazanmada konfeti + panel + ses, kaybetmede panel + ses.
        private void PresentOutcome()
        {
            if (outcomePresented || !session.IsEnded) return;

            outcomePresented = true;
            backgroundPanel.SetActive(true);

            if (session.Outcome == SessionOutcome.Won)
            {
                if (charAnim != null) charAnim.PlayWin();
                confetti.SetActive(true);
                StartCoroutine(WaitForConfetti());
                audioSource.PlayOneShot(winClip, winVolume);
            }
            else
            {
                if (charAnim != null) charAnim.PlayLose();
                outOfMovesPanel.SetActive(true);
                audioSource.PlayOneShot(lostClip, loseVolume);
            }
        }
```
`RefreshHud` `session.Points/Moves/Goal/Goals[i].amount` okur. `isGameEnded` kullanan yerler (`PotionBoard.AcceptsInput`, `SpecialStrikes.Toggle`) → `IsGameEnded`. `GameManagerWinTests.cs` silinir.

- [ ] **Step 6: Derle, test et, oyna** — kazanma (takasla ve strike ile), kaybetme, son 3 hamle sesi, HUD.

- [ ] **Step 7: Commit** — `refactor(session): extract GameSession rules from GameManager; add GameSessionTests`

---

### Task 10: Yorum geçişi ve dokümanlar

**Files:**
- Modify: `Assets/Scripts/Gameplay/**/*.cs` (yorumlar), `docs/superpowers/plans/2026-09-20-gameboard-review-refactor.md`, `docs/reviews/2026-09-20-gameboard-task1-baseline.md`

- [ ] **Step 1:** Her gameboard dosyasında standart uygulanır: sınıf başlığı 1–2 satır; metot üstü 1–3 satır; satır-satır tutorial yorumları silinir; korunacak satır içi notlar: kapalı Animator'de `HasState`, CFXR `clearBehavior`, `worldPositionStays`, mikser grubu / `PlayOneShot`, `Play On Awake`–`OnEnable`, roket parçalarının taşın çocuğu olması, `Animator.Rebind` gerekçesi, `IsAlive` fake-null.
- [ ] **Step 2:** Plan dokümanında Task 1–8 durumları ve baseline §5/§7 güncellenir: yapılanlar (boyutlar, ölü kod, kazanma tespiti, B1, B3), kararlar (D1–D5, D4 atlandı), ertelenenler (hamlesiz tahta, havuz, B4).
- [ ] **Step 3:** Tam oynanış matrisi (Global Constraints) manuel; dört csproj derleme; tüm testler; `find Assets/Scripts/Gameplay -name '*.cs' | xargs wc -l` — hiçbir dosya > ~500 satır.
- [ ] **Step 4: Commit** — `docs(gameboard): comment pass and plan/baseline status update`

---

## Self-review notları

- Spec kapsamı: §1 → Task 0; §2 → Task 2 (+ Task 1 önkoşul); §3 → Task 3–8; §4 → Task 9; §5 sıra → Task 0–10; §6 yorumlar → yeni kodda her task, kalan kodda Task 10; §7 testler → Task 2/9 (EditMode), Task 3–7 (PlayMode harness); §8 kabul → Task 8 Step 4 ve Task 10 Step 3.
- Spec'ten sapma: PlayMode test asmdef'i Editor-dışı platform içermek zorunda olduğu için harness'taki `#if UNITY_EDITOR` guard'ları **kalır** (spec §1 "kalkar" demişti).
- Tip tutarlılığı: `SpecialChain.ChainContext` Task 5'te tanımlanır, Task 6 `chain.ClearCell(cell, chainContext)` ile kullanır; `BoardEffects.SetParticleSimulationLocal` (static) Task 3'te tanımlanır, Task 5/6 kullanır; `Potion.PotionType/XIndex/YIndex/IsMoving/SetIndices` Task 1'de tanımlanır, Task 2+ kullanır; `GameManager.IsGameEnded` Task 9'da gelir — Task 7'deki `AcceptsInput` o ana kadar `isGameEnded` kullanır ve Task 9'da güncellenir.
