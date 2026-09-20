# Gameboard Review and Refactor Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Match-3 gameboard'ının davranışını koruyarak doğru, test edilebilir ve küçük sorumluluklara ayrılmış bir yapıya taşımak.

**Architecture:** Önce mevcut davranışın sözleşmesi, tahta invariant'ları ve kritik senaryolar testlerle sabitlenir. Ardından saf domain mantığı (`BoardGrid`, eşleşme ve refill planlaması) Unity/animasyon kodundan ayrılır; `PotionBoard` yalnızca orkestrasyon ve sunum adaptörü olur. Girdi, tur çözümü ve özel-vuruşlar ortak bir kabul katmanından geçer fakat mevcut arcade davranışı gereği güvenli işlemler eşzamanlı çalışabilir; her işlem kendi bağlamını ve tamamlanma durumunu taşır. `Potion`, yalnızca taşın görsel/hareket yaşam döngüsünden sorumlu kalır.

**Tech Stack:** Unity 6.5.0f1, C#, Unity Test Framework 1.7.0, Input System 1.19.0, TextMeshPro.

**Spec:** Bu doküman; `Assets/Scripts/PotionBoard.cs`, `Potion.cs`, `GameManager.cs`, `SpecialStrikes.cs`, `Node.cs`, `ArrayLayout.cs`, `CustPropertyDrawer.cs`, level verileri ve `GameBoard.unity` için review/refactor sözleşmesidir.

## Global Constraints

- Oynanabilir tahtanın mevcut sahnedeki boyutu **6 x 8**, görünmez spawn alanı ile toplam yüksekliği **15** olarak korunacak; bunlar tek bir veri kaynağından türetilecek.
- Normal geçerli/ geçersiz swap, cascade, bomba, roket, çift roket, süper bomba ve üç özel vuruşun oyuncu davranışı değişmeyecek.
- Oyun sırasında `LevelData` ScriptableObject varlıkları değiştirilmeyecek.
- Cascade/refill sırasında hareket etmeyen ve hâlâ gridde bulunan taşlara yeni swap/tap kabul eden mevcut arcade davranışı korunacak. Eşzamanlı işlemler ortak operation-local koleksiyon kullanmayacak; aynı taş veya hücre iki aktif işlem tarafından sahiplenilemeyecek.
- Unity sahne/prefab referansları korunacak; serialize edilmiş alan adı değişiklikleri `FormerlySerializedAs` veya açık bir asset migration adımı olmadan yapılmayacak.
- Kullanıcının mevcut çalışma ağacı değişiklikleri ve silinmiş testleri geri alınmayacak ya da üzerine yazılmayacak.

---

## Mevcut harita ve erken riskler

| Alan | İlgili dosyalar | Review önceliği | Neden |
|---|---|---:|---|
| Tahta orkestrasyonu | `PotionBoard.cs` (2.433 satır) | P0 | Girdi, grid, match, refill, özel-vuruş, VFX/SFX ve coroutine'ler tek sınıfta. |
| İşlem eşzamanlılığı | `PotionBoard.cs`, `SpecialStrikes.cs` | P0 | `BoardState` yazılıyor ancak işlem girişini kapatmak için okunmuyor; aynı anda birden çok çözüm başlatılabiliyor. |
| Boyut/level sözleşmesi | `PotionBoard.cs`, `ArrayLayout.cs`, `CustPropertyDrawer.cs`, `LevelData.cs` | P0 | Varsayılan genişlik 8, sahne ve drawer genişliği 6; görünür yükseklik 8 çok yerde sabit sayı. |
| Taş yaşam döngüsü | `Potion.cs`, havuz kodu | P1 | Havuz, coroutine, özel görünüm ve transform resetleri aynı nesnede doğrulanmalı. |
| Oynanabilirlik | match/swap/refill kodu | P1 | İlk üretim eşleşmeyi engelliyor ancak en az bir yasal hamleyi garanti etmiyor; cascade sonrası dead-board kontrolü yok. |
| Oyun kuralları ve UI | `GameManager.cs`, `GameBoardUI.cs` | P1 | Skor/hedef/hamle durumu UI ve sahne geçişi ile iç içe. |
| Sunum/özel vuruşlar | `SpecialStrikes.cs`, strike view'ları | P1 | Kural etkisi, hak düşümü ve sinematik tamamlanması ayrı sahiplerde. |

İlk incelemede doğrulanacak somut noktalar:

- `PotionBoard.currentState` atama alıyor fakat karar kapısı olarak kullanılmıyor; yorumlarda eşzamanlı çözümleme özellikle serbest bırakılmış.
- Görünür satır sınırı `8`, yalnızca kodlanmış değer olarak match, roket, patlama ve komşu tarama yollarında tekrar ediyor.
- `PotionBoard.width` varsayılanı `8`; `GameBoard.unity` içinde `6`, custom drawer içinde `6`.
- Refill yalnızca mevcut inaktif havuzdan spawn ediyor; roket izi nedeniyle havuza dönüş gecikirse `SpawnPotionAtTop` başarısız olup açık hücre bırakabiliyor.
- Başlangıç üretimi anlık üçlüleri engelliyor fakat yasal swap aramıyor; cascade sonunda da tahta oynanabilirliği doğrulanmıyor.
- Çalışma ağacında `Assets/Tests/Editor` altındaki iki test dosyası silinmiş; yeni review testleri, bu durumu yok saymadan ayrı dosyalar olarak eklenmeli.

## Review sırası

### Task 1: Güvenli başlangıç ve davranış envanteri

**Files:**

- Inspect: `Assets/Scenes/GameBoard.unity`, `Assets/Scripts/PotionBoard.cs`, `Assets/Scripts/Potion.cs`, `Assets/Scripts/GameManager.cs`
- Inspect: `Assets/Scripts/SpecialStrikes.cs`, `Assets/Scripts/Levels/{LevelData,LevelLoader,LevelCatalog}.cs`
- Inspect: `Assets/Scripts/{Node,ArrayLayout,CustPropertyDrawer,GameBoardUI}.cs`
- Create: `Assets/Tests/EditMode/Gameboard/` (yalnızca yeni testler)

**Produces:** Değişmez davranış listesi, sahne referansı kontrol listesi, tekrarlanabilir smoke-test kaydı.

**Task 1 raporu:** `docs/reviews/2026-09-20-gameboard-task1-baseline.md`

- [x] **Step 1: Çalışma ağacı ve derleme başlangıç kaydını al**

  Çalıştır:

  ```bash
  git status --short
  /Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -logFile Logs/gameboard-baseline.log
  ```

  Beklenen: Derleme hatası yoksa Unity `0` ile çıkar; hata varsa ilk refactor'dan önce hata metni review bulgusu olarak kaydedilir. Mevcut silinmiş test dosyaları geri getirilmez.

- [x] **Step 2: Sahnede seri hale getirilmiş bağımlılıkları doğrula**

  `GameBoard.unity` içinden `PotionBoard`, `GameManager`, `SpecialStrikes` ve `GameBoardUI` referanslarını denetle. Her `SerializeField` için şu sınıflandırmayı yap: zorunlu oyun-kuralı bağımlılığı, isteğe bağlı sunum bağımlılığı veya Resources fallback'i.

- [x] **Step 3: Davranış matrisi oluştur ve elle kaydet**

  Aşağıdaki akışların her birinde hamle, puan, hedef sayacı, aktif taş sayısı ve oyun sonu panelini kaydet:

  | Senaryo | Beklenen sözleşme |
  |---|---|
  | Geçerli normal swap | 1 hamle düşer, yalnızca swap'a bağlı match temizlenir, ardından cascade biter. |
  | Geçersiz swap | Taşlar geri döner, hamle/puan/hedef değişmez. |
  | Tek bomba/roket dokunuşu | Zincir ve cascade biter, tam 1 hamle düşer. |
  | Bomba+bomba | Alan patlaması ve cascade biter, tam 1 hamle düşer. |
  | Roket+roket | Satır+sütun temizliği, kesişimde çift tetik yok, tam 1 hamle düşer. |
  | Hammer/Bomb/Cannon | Hak yalnızca başarıyla başlarsa 1 azalır; etkiler bittikten sonra tahta tekrar oynanabilir. |
  | Son hamlede hedef tamamlama | Kazanma/kaybetme panelleri birlikte açılmaz. |
  | Cascade/refill sırasında swap veya özel-taş tap | Hareket etmeyen ve hâlâ gridde bulunan taşlarda yeni işlem kabul edilir; işlemler birbirinin match/zincir/tamamlanma verisini değiştirmez. |

- [x] **Step 4: Test altyapısı kararını kaydet**

  Yeni testleri silinmiş dosyalarla aynı adlara koyma. Saf C# kurallarını EditMode testlerine, input/animasyon/coroutine entegrasyonunu PlayMode testlerine ayır.

- [x] **Step 5: Eşzamanlı input karar kapısını kapat**

  **Karar: mevcut eşzamanlı arcade davranışı korunacak.** Cascade/refill sırasında hareket etmeyen ve grid sahipliği geçerli taşlara swap/tap kabul edilecek. `currentMatchGroups`, special trigger kümeleri, sayaçlar ve tamamlanma bilgisi işlem bağlamına taşınacak; çakışan taş/hücre sahipliği kabul katmanında reddedilecek.

### Task 2: Boyut ve level-verisi sözleşmesi review'u

**Files:**

- Modify after approval: `Assets/Scripts/Board/BoardDefinition.cs` (new)
- Modify after approval: `Assets/Scripts/{PotionBoard,ArrayLayout,CustPropertyDrawer}.cs`
- Test: `Assets/Tests/EditMode/Gameboard/BoardDefinitionTests.cs`

**Consumes:** Task 1'de onaylanmış 6 görünür sütun, 8 görünür satır, 15 toplam satır sözleşmesi.

**Produces:** Tek kaynaklı boyut modeli ve level layout doğrulama hataları.

- [x] **Step 1: Sabit boyutları listele**

  `width`, `height`, `8`, `15`, `6` ve `ColumnCount` kullanımını sınıflandır: görünür alan sınırı, spawn alanı, grid çizimi, vuruş etkisi veya dünya-koordinatı hesaplaması.

- [x] **Step 2: Önce başarısız sınır testlerini yaz**

  `Validate_RejectsLayoutWithWrongRowCount` testinde 14 satırlı layout oluştur, validator'ı çağır ve satır sayısını içeren tek bir doğrulama hatası döndüğünü doğrula. `Validate_RejectsLayoutWithWrongColumnCount` testinde bir satırı 5 sütunlu kur ve hatanın satır/sütun koordinatını içerdiğini doğrula. `VisibleCells_ExcludeSpawnRows` testinde `y=7` için `IsPlayable=true`, `y=8` ve `y=14` için `IsPlayable=false`, fakat iki spawn hücresi için de `IsWithinStorage=true` bekle.

- [x] **Step 3: `BoardDefinition` ile tek kaynak kur**

  `VisibleWidth`, `VisibleHeight`, `SpawnHeight`, `TotalHeight`, `IsPlayable(cell)` ve `IsWithinStorage(cell)` sunulacak. `PotionBoard` dışındaki kod hiçbir sayı literal'i ile görünür sınır belirlemeyecek.

- [x] **Step 4: Inspector ve asset doğrulaması ekle**

  Drawer, boyutları `BoardDefinition`dan alacak. `LevelData.OnValidate` veya editor validator; yanlış satır/sütun sayısında level adıyla anlaşılır hata vermeli, diziyi sessizce veri kaybettirecek şekilde yeniden boyutlandırmamalı.

- [x] **Step 5: Testleri çalıştır ve örnek levelleri doğrula**

  ```bash
  /Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testFilter Gameboard -logFile Logs/gameboard-editmode.log
  ```

  Beklenen: Mevcut `Level_001` ve `Level_002` layoutları geçer; hatalı örnekler anlaşılır şekilde reddedilir.

### Task 3: Tahta invariant'ları ve saf kural katmanı

**Files:**

- Create: `Assets/Scripts/Board/BoardCell.cs`
- Create: `Assets/Scripts/Board/BoardGrid.cs`
- Create: `Assets/Scripts/Board/MatchFinder.cs`
- Create: `Assets/Tests/EditMode/Gameboard/{BoardGridTests,MatchFinderTests}.cs`
- Modify after approval: `Assets/Scripts/{Node,PotionBoard}.cs`

**Consumes:** Task 2 boyut ve layout sözleşmesi.

**Produces:** Unity nesnesine bağlı olmayan grid/match API'si.

- [ ] **Step 1: İnvariant'ları yazılı hale getir**

  1. Kapalı hücre taş içermez.
  2. Her aktif `Potion` için gridde tam bir hücre bulunur.
  3. Bir hücrede en fazla bir aktif taş vardır.
  4. Görünür alan dışındaki taş eşleşme, hedef ve vuruş etkisine katılmaz.
  5. Griddeki taş koordinatı, taşın mantıksal koordinatıyla eşittir.

- [ ] **Step 2: Saf grid testlerini yaz**

  `Move_UpdatesSourceDestinationAndCoordinates` testinde tek occupant'ı açık bir komşu hücreye taşı; kaynak hücrenin boş, hedefin aynı occupant'a sahip ve occupant koordinatının hedefle eşit olduğunu doğrula. `Clear_RejectsBlockedAndEmptyCells` testinde kapalı ve boş hücre denemelerinin grid sürümünü ve occupant sayısını değiştirmediğini doğrula. `FindMatches_ReturnsHorizontalVerticalAndSuperGroups` için ayrı test vakalarında yatay/dikey üçlü, dörtlü, beşli ve T/L şekillerinin benzersiz koordinat kümesini ve doğru yön/special sonucunu doğrula.

- [ ] **Step 3: `Node` yerine anlamlı değer modelini uygula**

  Hücrenin kapalı/açık durumunu ve occupant'ını açık adlarla sun; doğrudan `Node.potion` yazımları yalnızca grid API'si içinde kalacak.

- [ ] **Step 4: Eşleşme tespitini ayır**

  `PotionBoard.CheckBoard`, `IsConnected`, `CheckDirection`, `CollectSwapMatches`, `SuperMatch` bölümlerini `MatchFinder`a taşı. API koordinat ve renk/özel-tip verisi döndürmeli; `Potion`, particle veya coroutine referansı döndürmemeli.

- [ ] **Step 5: Kural testlerini çalıştır**

  Beklenen: Aynı grupta iki kez puan/hedef yazılmasını önleyen unique-cell testleri de geçer.

- [ ] **Step 6: Yasal hamle ve dead-board sözleşmesini ekle**

  `HasAnyLegalMove` yalnızca görünür, kullanılabilir ve komşu hücreleri değerlendirmeli; kontrol sırasında grid'i kalıcı olarak değiştirmemeli. Başlangıç üretimi ve her tamamlanmış cascade sonunda en az bir yasal hamle bulunmalı; yoksa skor, hedef ve hamle sayısını değiştirmeden deterministik bir reshuffle planlanmalı. EditMode testleri en az şu durumları kapsamalı: tek yasal swap, hiç yasal swap olmayan tahta, kapalı hücrenin iki yanındaki sahte swap ve reshuffle sonrasında korunmuş özel taş sözleşmesi.

### Task 4: Eşzamanlı işlem koordinasyonu ve coroutine review'u

**Files:**

- Create: `Assets/Scripts/Board/BoardTurnCoordinator.cs`
- Create: `Assets/Tests/PlayMode/Gameboard/BoardTurnCoordinatorTests.cs`
- Modify after approval: `Assets/Scripts/PotionBoard.cs`
- Modify after approval: `Assets/Scripts/SpecialStrikes.cs`

**Consumes:** Task 3 saf grid/match sonuçları.

**Produces:** Swap, tap detonation ve strike için ortak kabul sözleşmesi; güvenli işlemler için izole eşzamanlı çözümleme.

- [ ] **Step 1: İşlem bağlamını ve kabul durumlarını tanımla**

  Her swap/tap için benzersiz kimlikli `BoardOperationContext` tanımla; sahiplenilen taş/hücreler, match grupları, special trigger kümesi, çalışan coroutine sayacı ve `Resolving -> Refilling -> Completed` durumu context içinde yaşasın. Global kabul kuralları `GameEnded`, `InputLocked`, presentation kilidi, hareket eden taş, grid sahipliği kaybı ve başka context tarafından sahiplenilmiş taş/hücre koşullarını reddetsin. Cannon hedefleme global presentation alt durumu olarak kalsın.

- [ ] **Step 2: Önce PlayMode yarış testlerini yaz**

  `AllowsIndependentSwapWhileFirstOperationResolves` ilk işlemi cascade/refill aşamasında tutarken çakışmayan, durgun iki taşla ikinci swap'ı başlatır ve iki context'in ayrı tamamlandığını doğrular. `RejectsOverlappingOperationOwnership` aktif context'in taşı/hücresiyle ikinci komut yollar ve grid, hamle ve hedef sayaçlarının değişmediğini doğrular. `ConcurrentOperationsKeepMatchAndChainStateIsolated` iki işlemde farklı match/special zincirleri başlatıp her hücrenin tek kez temizlendiğini ve her kabul edilen normal işlemin hamleyi yalnız bir kez düşürdüğünü doğrular. `RejectsTapDuringPresentationLockWithoutSpendingMove` Hammer/Bomb/Cannon sunumu sırasında ek tap yollar ve ek hak/hamle harcanmadığını doğrular.

- [ ] **Step 3: `TryStartOperation` kabul katmanını uygula**

  Tüm `StartCoroutine(ProcessMatches/TapDetonate/ExplodeChain/StrikeRoutine)` girişleri context oluşturan ortak kabul metodundan geçsin. Board genel olarak resolve oluyor diye komut reddedilmesin; yalnız Step 1'deki gerçek çakışma/lock koşulları reddetsin. Reddedilen girdi hiçbir seçili görseli, hamleyi, hedefi veya grid'i değiştirmemeli.

- [ ] **Step 4: Ortak mutable çözümleme verisini kaldır**

  `currentMatchGroups` gibi işleme ait koleksiyonlar operation-local olmalı. Her context kendi `MatchResult`, triggered-cell kümesi ve chain counter verisini taşır; sonraki input önceki işlemi değiştiremez. Grid mutasyonları hücre sürümü/sahipliği doğrulandıktan sonra commit edilmeli.

- [ ] **Step 5: Tamamlanma garantisini ekle**

  `try/finally` veya eşdeğer bir kontrol akışı ile tamamlanan/iptal edilen her context'in taş ve hücre sahipliğini bıraktığını kanıtla. Bir işlem hata verdiğinde diğer aktif işlemler ve input kalıcı kilitlenmemeli. Sahne unload olduğunda çalışan coroutine, context ve event subscription davranışını da denetle.

### Task 5: Refill, havuz ve `Potion` yaşam döngüsü

**Files:**

- Create: `Assets/Scripts/Board/PotionPool.cs`
- Create: `Assets/Tests/PlayMode/Gameboard/PotionPoolTests.cs`
- Modify after approval: `Assets/Scripts/{PotionBoard,Potion}.cs`

**Consumes:** Task 3 grid API'si, Task 4 eşzamanlı işlem koordinatörü.

**Produces:** Açık sahiplikli spawn/despawn/reset akışı.

- [ ] **Step 1: Havuz sözleşmesini doğrula**

  `Acquire(type, spawnPosition)` aktif, temizlenmiş bir taş; `Release(potion)` tekil ve inaktif bir taş vermeli. Bir obje aynı anda hem aktif gridde hem inaktif havuzda bulunamaz.

- [ ] **Step 2: Önce havuz reset testlerini yaz**

  `ReleasedBomb_ReacquiresAsOriginalColorWithCleanVisuals` bombaya çevrilmiş taşı release/acquire döngüsünden geçirip özgün rengin, normal sprite'ın ve prefab ölçeğinin döndüğünü; bomb, gölge, roket parçaları, seçim ve swap smoke objelerinin kapalı olduğunu doğrular. `ReleasedMovingPotion_HasNoResidualCoroutineOrScale` hareketin ortasında release edilen taşı yeniden alıp `isMoving=false`, aktif hareket rutini yok, prefab ölçeği ve yeni hedefe tek hareket rutini koşullarını doğrular.

- [ ] **Step 3: `Potion`u daralt**

  `Potion`un sorumlulukları: tip/köken tip, koordinat görünümü, tek hareket rutini, özel görsel reseti. Tahta hücresini değiştirmesi veya puan/hedef bilgisi taşıması engellensin. `setSelectedVisual` ve `SetIndicies` isimleri .NET/C# biçemine uygun olacak şekilde geçiş uyumluluğu korunarak değiştirilsin.

- [ ] **Step 4: Refill planlamasını saflaştır**

  Önce gridde hangi taşın hangi hücreye ineceğini ve kaç taş doğacağını hesapla; sonra `PotionPool` ve `Potion` animasyonlarını uygula. Kapalı hücreler, spawn satırları ve birden fazla boşluk için test ekle.

  Pool geçici olarak boşsa refill sessizce eksik tahta üretmemeli: ihtiyaç kadar güvenli büyüme veya bütün gerekli taşlar hazır olana kadar kontrollü bekleme politikalarından biri seçilip test edilmeli. Roket izi hâlâ çalışırken başlayan refill için açık hücre kalmadığını doğrulayan PlayMode testi eklenmeli.

- [ ] **Step 5: Leak ve çift-release denetimi yap**

  Uzun cascade, roket satır/sütun, bomba zinciri ve scene restart sonrası havuz aktif/inaktif sayısını log ile karşılaştır.

### Task 6: Özel taşlar ve özel vuruşların kuralları

**Files:**

- Create: `Assets/Scripts/Board/SpecialEffectResolver.cs`
- Create: `Assets/Tests/EditMode/Gameboard/SpecialEffectResolverTests.cs`
- Modify after approval: `Assets/Scripts/{PotionBoard,SpecialStrikes}.cs`
- Inspect: `Assets/Scripts/{HammerStrikeView,BombStrikeView,CannonEntryView}.cs`

**Consumes:** Task 3 grid, Task 4 eşzamanlı işlem koordinatörü, Task 5 pool.

**Produces:** Sunumdan bağımsız hücre-etki planı ve güvenilir hak tüketimi.

- [ ] **Step 1: Etki tablosunu testle sabitle**

  | Etki | Grid sonucu |
  |---|---|
  | Bomb | Merkez dahil oynanabilir 3x3 hücreleri bir kez temizler. |
  | Yatay/dikey roket | Başlangıç hücresi dahil görünür satır/sütunu bir kez temizler. |
  | Çift roket | Satır ve sütun birleşimi; kesişim bir kez işlenir. |
  | Hammer | Merkez ve `hammerReach` kadar dört yön. |
  | Strike bomb | Merkez dahil 3x3. |
  | Cannon | Seçilen görünür satırdaki hücreler, sunum anında sırayla temizlenir. |

- [ ] **Step 2: Zincir-dedup testlerini yaz**

  Aynı hücreye iki tetik ulaştığında sadece bir despawn, bir hedef bildirimi ve tanımlı tek puan olayı üretilmesini denetle.

- [ ] **Step 3: Hak tüketimini commit noktasına taşı**

  `SpecialStrikes` hakkı, `BoardTurnCoordinator` işleme kabul ettikten sonra düşürmeli. Prefab/referans eksikliği veya işlem kapısı reddi; hakkı, hamleyi ve seçimi deterministik biçimde korumalı.

- [ ] **Step 4: Presentation adaptörlerini ayır**

  Hammer/Bomb/Cannon coroutine'lerinde bulunan kural mutasyonlarını resolver/coordinator'a al; view sınıfları yalnızca hareket/animasyon bitiş sinyali üretsin.

### Task 7: `GameManager` ve UI sınırları

**Files:**

- Create: `Assets/Scripts/Game/GameSession.cs`
- Create: `Assets/Tests/EditMode/Gameboard/GameSessionTests.cs`
- Modify after approval: `Assets/Scripts/GameManager.cs`
- Modify after approval: `Assets/Scripts/{GameBoardUI,SpecialStrikes}.cs`

**Consumes:** Turn sonuçları (`points`, cleared types, turn spent) ve Task 4 tamamlanma sinyali.

**Produces:** Test edilebilir skor/hedef/hamle kuralları ile ince UI adapter'ı.

- [ ] **Step 1: `GameSession` testlerini yaz**

  `ClearedPotion_DecrementsOnlyMatchingPositiveGoal` kırmızı ve bomba hedefli session'a mavi, kırmızı ve bomba clear olayları verip yalnız eşleşen pozitif sayaçların birer azaldığını doğrular. `WinningTurn_PrecedesLossAtZeroMoves` son hamlede puan ve tüm toplama hedefleri tamamlandığında yalnız `Won` sonucunu ve tek sonuç event'ini doğrular. `LevelDataGoals_AreCopiedAndNeverMutated` session içinde hedefleri tüketip kaynak `LevelData.potionGoals` değerlerinin başlangıçtaki adetlerle aynı kaldığını doğrular.

- [ ] **Step 2: Oyunun sonuç modelini tanımla**

  `TurnResult` en az `PointsAdded`, `ClearedTypes`, `MoveSpent`, `BoardSettled` alanlarını taşısın. `GameManager`, animation ortasında değil, tamamlanmış işlemden sonra session'ı güncellesin.

- [ ] **Step 3: UI güncellemelerini event tabanlı yap**

  `GameManager.Update` içindeki her-kare metin atamalarını session değişimi event'ine bağla. Zorunlu TMP/referanslar Awake/OnValidate sırasında açık hata vermeli; isteğe bağlı efekt/sesler null-güvenli kalmalı.

- [ ] **Step 4: Singleton ve sahne geçişi denetimini yap**

  `GameManager.Instance`, `PotionBoard.Instance`, `LevelLoader.selectedLevel` için duplicate instance, destroyed instance ve restart/next-level/main-menu geçişleri test edilmeli. Sahne değişiminde seçili level'ın kasıtlı kalıcılığı belgelenmeli.

### Task 8: Son performans, regresyon ve refactor kabulü

**Files:**

- Test: `Assets/Tests/{EditMode,PlayMode}/Gameboard/`
- Inspect: `Assets/Scenes/GameBoard.unity`, örnek LevelData varlıkları
- Modify if needed: Gameboard dosyalarındaki XML yorumları ve README/review notu

**Consumes:** Önceki tüm görevler.

**Produces:** Birleşmeye hazır refactor ve kanıt paketi.

- [ ] **Step 1: Tam test paketini çalıştır**

  ```bash
  /Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -logFile Logs/editmode.log
  /Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -logFile Logs/playmode.log
  ```

  Beklenen: Yeni gameboard testleri geçer; mevcut silinmiş test dosyalarının geri getirilmesi bu planın parçası değildir.

- [ ] **Step 2: Oynanış regresyon matrisiyle manuel doğrulama yap**

  Task 1 matrisi her level için tekrar çalıştırılır. Özellikle cascade sırasında hızlı dokunuş, panel aç/kapa, cannon iptali ve son hamlede özel vuruş denetlenir.

- [ ] **Step 3: Profiler ile allocation ve coroutine sayısını karşılaştır**

  Uzun cascade ve üst üste özel etki sırasında frame allocation, aktif coroutine sayısı, particle GameObject sayısı ve havuz büyümesi baseline ile karşılaştırılır. Gözle görünür davranışa karşılık gelmeyen mikro-optimizasyon yapılmaz.

- [ ] **Step 4: Küçük ve bağımsız commit'ler oluştur**

  Önerilen sıralama: `test: capture gameboard rules`; `refactor: centralize board dimensions`; `refactor: extract grid and match finder`; `refactor: isolate concurrent board operations`; `refactor: isolate potion pool`; `refactor: separate session from UI`.

## Kabul kriterleri

- Gameboard derlenir; EditMode ve PlayMode gameboard testleri geçer.
- Çakışmayan board işlemleri cascade/refill sırasında eşzamanlı resolve olabilir; match/zincir/context verileri birbirine sızmaz ve aynı taş/hücre iki işlem tarafından sahiplenilmez.
- Boyutlar tek kaynaklıdır; görünür alan dışında match/strike/goal işlemi yoktur.
- Başlangıçta ve her tamamlanmış cascade sonunda tahta en az bir yasal hamle içerir; dead-board çözümü skor/hedef/hamle sayaçlarını değiştirmez.
- Her clear işlemi havuzu, grid'i, hedefleri ve puanı yalnızca bir kez günceller.
- Havuzdan geri gelen taş; eski bomb/roket/scale/coroutine/selection kalıntısı taşımaz.
- `LevelData` çalışma sırasında değişmez.
- `PotionBoard` grid, kural, input, sunum ve ses sorumluluklarını tek başına taşımaz.
