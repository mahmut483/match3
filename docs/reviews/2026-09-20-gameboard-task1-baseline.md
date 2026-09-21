# GameBoard Task 1 — Baseline ve Davranış Envanteri

**Tarih:** 2026-09-20  
**Kapsam:** `PotionBoard`, `Potion`, `GameManager`, `SpecialStrikes`, `GameBoardUI`, level/layout sözleşmesi ve `GameBoard.unity` bağlantıları  
**Sınır:** Kullanıcının isteğiyle test dosyası oluşturulmadı ve kaynak kod değiştirilmedi.

## 1. Derleme ve çalışma ağacı baseline'ı

### Çalışma ağacı

İnceleme `main` dalında yapıldı. İnceleme başlamadan önce var olan aşağıdaki değişikliklere dokunulmadı:

```text
M  Assets/Scenes/MainMenu.unity
M  Assets/Scripts/CustPropertyDrawer.cs
M  Assets/Scripts/Levels/LevelsData/Leve_002/Level_002.asset
M  Assets/Scripts/Levels/LevelsData/Leve_002/Tilemap_002.prefab
D  Assets/Tests/Editor/LevelCatalogTests.cs
D  Assets/Tests/Editor/LevelCatalogTests.cs.meta
D  Assets/Tests/Editor/MainMenuResourceBarUITests.cs
D  Assets/Tests/Editor/MainMenuResourceBarUITests.cs.meta
M  Assets/TextMesh Pro/Resources/Fonts & Materials/MyFont.asset
M  match3.slnx
?? docs/superpowers/plans/2026-09-20-gameboard-review-refactor.md
```

### Derleme sonucu

Ana proje açık bir Unity Editor örneği tarafından kilitlendiği için ilk batchmode denemesi derlemeye başlamadan `exit 1` ile durdu. Açık editöre müdahale etmeden taze kanıt almak için yalnızca `Assets`, `Packages` ve `ProjectSettings` klasörleri `/private/tmp` altında izole bir projeye kopyalandı.

İzole kopyada çalıştırılan Unity 6000.5.0f1 batchmode işlemi:

- `exit code: 0`
- Log sonucu: `Finished compiling graph`
- Log sonucu: `Exiting batchmode successfully now!`
- `error CS`, `compilation failed` veya fatal script compilation kaydı: yok

Derleme sonrasında geçici proje kopyası kaldırıldı. Bu sonuç C# derleme/import baseline'ını doğrular; oynanış senaryolarının Play Mode sonucu değildir.

## 2. Sahne bağımlılık envanteri

### `PotionBoard`

| Sınıf | Referanslar | Sahne durumu | Değerlendirme |
|---|---|---|---|
| Zorunlu oyun kuralı | `potionPrefabs`, `potionParent`, `specialStrikes` | 4 prefab ve iki sahne referansı bağlı | Normal board kurulumu için gerekli. `specialStrikes` olmadan normal swap çalışabilir fakat booster akışı devre dışı kalır. |
| Doğrudan runtime bağımlılığı | `GameManager.Instance.ActiveLevel` | Serialized değil; singleton üzerinden | `GameManager.Awake` başarısızsa `PotionBoard.Start/Update` null-reference üretir. |
| Ses | `matchSource`, `superMatchSource`, `explodingSource`, normal match/super/explosion klipleri | Bağlı | Birçok çağrıda null kontrolü olmadığı için fiilen zorunlu. |
| Resources fallback | `bombClip`, `doubleRocketClip` | Sahnede boş | `Resources/SFX/superBombSound.wav` ve `Resources/SFX/duableRocket.wav` mevcut; `Awake` fallback'i geçerli. |
| Board sunumu | `boardGrid`, `boardPresentation` | Bağlı | Tilemap yükleme ve Cannon kaydırması için kullanılıyor. |
| Runtime fallback | `boardVfxRoot` | Boş | `BoardPresentation/BoardVFX` aranıyor, yoksa çalışma anında oluşturuluyor. Beklenen boş referans. |
| Cannon sunumu | anchor, entry prefab, projectile, iki mask | Bağlı | `HasCannonPresentation` eksik referansta işlemi reddediyor. |
| Hammer/Bomb sunumu | iki strike prefabı | Bağlı | Eksik prefab ilgili vuruşu reddediyor ve hak düşmüyor. |
| İsteğe bağlı efekt | destroy/super/rocket particle prefabları | Bağlı | Bazı yardımcılar null-safe; ses kaynakları kadar güvenli değiller. |
| Runtime state | `secondSelectedPotion` | Boş | Inspector verisi değil; `[SerializeField]` kaldırılmaya aday. |

### `GameManager`

| Sınıf | Referanslar | Sahne durumu | Değerlendirme |
|---|---|---|---|
| Level fallback | `levelData` | `Level_001` bağlı | Menü `LevelLoader.selectedLevel` vermediğinde kullanılıyor. |
| Oyun sonu/UI | background, victory, lose/out-of-moves panelleri; üç TMP sayaç; goal root'ları; confetti | Bağlı | Kodun çoğu null kontrolü yapmadığı için zorunlu. |
| Karakter | `charAnim` | Bağlı | Kod null-safe; sunum bağımlılığı. |
| Oyun sonu butonları | dört Button alanı | Sahnede boş | `TryAgain`, `NextLevel` ve `CrossBTN` isimleriyle panel çocuklarından bulunuyor; isimler sahnede mevcut. İsim değişikliği sessiz kırılma riski taşır. |
| Level catalog | `levelCatalog` | Sahnede boş | Önce `LevelLoader.catalog`, yalnız Editor'da sabit AssetDatabase yolu kullanılıyor. Build'de GameBoard doğrudan açılır ve statik catalog set edilmezse Next Level çalışmaz. |
| Ses | `audioSource`, son 3/win/lost klipleri | Bağlı | Null kontrolü yok; fiilen zorunlu. |
| Goal display | Red, Blue, Yellow, Green, Rocket | Bağlı | Bomb display yok. Mevcut iki level Bomb hedefi kullanmıyor; gelecekte Bomb hedefi eklenirse sayaç UI'da görünmez. |

### `SpecialStrikes`

- `board` bağlı.
- Hammer, Cannon ve Bomb için üç slotun button, count text, selected visual, canvas ve icon referansları bağlı.
- Ortak bilgi paneli, ikon, başlık ve açıklama referansları bağlı.
- UI referansları çoğunlukla null-safe; `board` yoksa vuruş başlatılmıyor.
- Hak yalnız `PotionBoard.TryRunStrike` işlemi kabul ettikten sonra azaltılıyor.

### `GameBoardUI`

- Board, settings paneli, aç/kapat/çıkış butonları ve AudioMixer bağlı.
- Music/SFX `image` alanlarının boş olması beklenen durum; kod button `targetGraphic` alanına fallback yapıyor.
- Tüm buton bağlantıları koddan ekleniyor ve `OnDestroy` içinde kaldırılıyor.
- Settings paneli yalnız yeni input'u kilitliyor; başlamış cascade ve coroutine'ler çalışmaya devam ediyor.

## 3. Board ve level veri sözleşmesi

| Kaynak | Genişlik | Görünür yükseklik | Toplam yükseklik |
|---|---:|---:|---:|
| `GameBoard.unity / PotionBoard` | 6 | Kodda sabit 8 | 15 |
| `PotionBoard.cs` varsayılanı | 8 | Kodda sabit 8 | 15 |
| `CustPropertyDrawer.cs` | 6 | 8 | 15 |
| `Level_001.asset` satır dizileri | 8 | 8 | 15 |
| `Level_002.asset` satır dizileri | 6 | 8 | 15 |

Sonuç: Çalışan sahne 6 sütun kullandığı için `Level_001` satırlarının son iki değeri runtime'da okunmuyor. Bugünkü verilerde bu değerler `false`, dolayısıyla görünür hata üretmiyor; fakat veri sözleşmesi tek kaynaklı değil. `CustPropertyDrawer` açıldığında dizileri yeniden boyutlandırabildiği için asset'in yalnız Inspector'a bakılmasıyla değişmesi de veri kaybı riski oluşturuyor.

Görünür yükseklik `8`; match tarama, roket sınırı, clear sınırı, süper bomba ve komşu tarama yollarında ayrı ayrı literal olarak kullanılıyor.

## 4. Mevcut davranış sözleşmesi

Aşağıdaki matris kod akışından çıkarılmış baseline'dır. Test veya otomatik Play Mode doğrulaması değildir.

| Senaryo | Mevcut kod davranışı |
|---|---|
| Geçerli normal swap | Taşlar duruyorsa ve griddeyse swap edilir. Yalnız swap edilen taşların oluşturduğu ilk gruplar kabul edilir. Clear/refill/cascade bittikten sonra `ProcessTurn(10, true)` çağrılır: bir hamle ve ayrıca 10 puan işlenir. Her temizlenen normal/süper grup cascade sırasında ayrıca 10/15 puan verir. |
| Geçersiz swap | Swap geri alınır; hamle, puan ve hedef sayaçları değiştirilmez. |
| Tek Bomb/Rocket tap | Zincir, refill ve cascade tamamlanır; sonra bir hamle düşer. Her bomba/roket tetiklemesi `bombPoints` kadar puan verir. |
| Bomb + Bomb | 7x7 halka temizliği, refill ve cascade sonrası bir hamle düşer. Ana süper bomba olayı `bombPoints` kadar puan verir. Alan içindeki diğer özel taşlar `ReturnPotionToPool` ile temizlenir; ayrı zincir olarak tetiklenmez. |
| Rocket + Rocket | Aynı merkezden yatay ve dikey sweep başlar; ortak `triggered` kümesi hücre tekrarını engeller. İki sweep ayrı ayrı `bombPoints` ekler; tamamlanınca bir hamle düşer. |
| Rocket + Bomb veya özel + normal | Roket öncelikli seçilir. Sweep yolundaki bomba ortak zincir üzerinden tetiklenir; refill/cascade sonunda bir hamle düşer. |
| Hammer | Merkez ve dört yönde `hammerReach` kadar hücre temizlenir. Board kabul ederse bir booster hakkı düşer; normal hamle düşmez. |
| Strike Bomb | 3x3 alan temizlenir. Board kabul ederse bir booster hakkı düşer; normal hamle düşmez. |
| Cannon | Hedef satır mermi geçtikçe temizlenir. Sunum/zincir/refill tamamlanır; booster hakkı düşer, normal hamle düşmez. |
| Son hamlede hedef tamamlanması | `GameManager.ProcessTurn` önce kazanmayı kontrol eder; şartlar sağlanırsa aynı çağrıda kaybetme yolu çalışmaz. |
| Settings açıkken | Yeni board input'u ve strike seçimi kilitlenir. Devam eden cascade/animasyon durdurulmaz. |
| Cascade/refill sırasında input | `currentState` input kapısı değildir. Hareket etmeyen ve hâlâ gridde bulunan taşlara yeni swap/tap kabul edilebilir; Hammer/Bomb/Cannon kendi boolean kilitlerini kullanır. |

### Hedef sayacı davranışı

- Her havuza dönen normal taş özgün rengine bir clear olarak yazılır.
- Bomb havuza dönerken hem özgün rengine hem `PotionType.Bomb` hedefine yazılır.
- Rocket havuza dönerken yalnız özgün rengine yazılır; `PotionType.Rocket` hedefine ayrıca yazılmaz.
- Aynı potion tipinden birden fazla `PotionGoal` tanımlanırsa tek clear olayı eşleşen bütün goal satırlarını birer azaltır.

Bu maddeler refactor sırasında korunacak davranış mı yoksa düzeltilecek kural mı oldukları ayrıca kararlaştırılmalıdır.

## 5. Öncelikli review bulguları

### P0 — İşlem eşzamanlılığı ve ortak mutable durum

`currentState` yalnız yazılıyor; hiçbir yerde `Idle`/`Resolving` kabul kapısı olarak okunmuyor. Buna karşılık `currentMatchGroups`, `potionBoard`, pool ve `GameManager` sayaçları birden fazla coroutine tarafından paylaşılabiliyor. Kod yorumları cascade sırasında yeni swap/tap davranışını kasıtlı olarak açık tutuyor. Ürün kararı bu davranışın korunması yönünde verildi; risk Task 4'te operation-local context ve taş/hücre sahipliği ile giderilecek.

### P0 — Board ölçüsü ve level layout sözleşmesi

6/8/15 değerleri sahne, drawer, asset ve runtime içinde ayrışmış durumda. `Level_001` ile `Level_002` satır genişlikleri bile aynı değil. Task 2'nin ilk konusu olmalı.

### P1 — Pool boşken eksik refill

`SpawnPotionAtTop`, inaktif pool boşsa `false` dönüyor. Özellikle Rocket objesi trail sönene kadar pool'a dönmediği için refill geçici olarak yeterli obje bulamayabilir. Ortak refill akışı daha sonra bu hücreyi tekrar doldurmayı garanti etmiyor.

### P1 — Dead-board kontrolü yok

İlk üretim anlık üçlüleri engelliyor fakat en az bir yasal swap olduğunu kontrol etmiyor. Cascade sonrasında da oynanabilirlik taraması veya reshuffle bulunmuyor.

### P1 — Build'e bağlı catalog fallback'i

Editor doğrudan GameBoard açılışında AssetDatabase fallback'i var; build'de aynı fallback yok. `LevelLoader.catalog` kurulmadan GameBoard'a girilirse Next Level yolu catalog bulamıyor.

### P1 — Null güvenliği tutarsız

Bazı VFX/presentation bağımlılıkları güvenli şekilde reddedilirken temel audio source, TMP, panel ve singleton erişimleri null kontrolü olmadan kullanılıyor. Sahne bugün doğru bağlı, fakat prefab/sahne varyantlarında hata tanısı zayıf.

### P2 — Runtime state'in serialize edilmesi ve her-kare UI yazımı

`secondSelectedPotion`, `potionBoard` ve `currentState` gibi runtime alanlarının bazıları serialize ediliyor. `GameManager.Update` puan, hamle, hedef ve goal metinlerini değişiklik olmasa da her kare yeniden yazıyor.

## 6. Test yaklaşımı kararı

Bu Task 1 uygulamasında kullanıcı isteğiyle test dosyası oluşturulmadı. İleride test eklenirse önerilen ayrım değişmedi:

- Saf grid, match, ölçü ve session kuralları: EditMode.
- Input, coroutine, animation, pool ve scene entegrasyonu: PlayMode.
- Silinmiş mevcut test dosyaları geri getirilmeyecek; yeni GameBoard testleri ayrı klasör ve adlarla açılacak.

## 7. Ürün kararı: eşzamanlı arcade davranışı korunacak

Kullanıcı mevcut eşzamanlı arcade davranışının korunmasını seçti. Cascade/refill sırasında hareket etmeyen ve hâlâ gridde bulunan taşlara yeni swap/tap kabul edilmeye devam edecek.

Refactor sözleşmesi:

- Her kabul edilen swap/tap benzersiz bir operation context taşır.
- Match grupları, special trigger kümesi, chain counter ve tamamlanma durumu context'e özeldir.
- Aynı taş veya hücre iki aktif işlem tarafından sahiplenilemez.
- Board'un genel olarak `Resolving`/`Refilling` olması tek başına yeni komutu reddetme sebebi değildir.
- Hammer/Bomb/Cannon presentation kilitleri mevcut görsel davranışı korumak için global kalabilir.
- Bir context'in hata/iptali diğer aktif context'leri veya input'u kalıcı kilitlemez.

## Task 1 durumu

- [x] Çalışma ağacı baseline'ı kaydedildi.
- [x] Taze izole Unity derlemesi alındı.
- [x] Sahne bağımlılıkları ve fallback'ler sınıflandırıldı.
- [x] Koddan türetilen davranış matrisi kaydedildi.
- [x] Test yaklaşımı kaydedildi; kullanıcı isteğiyle test oluşturulmadı.
- [x] Eşzamanlı arcade davranışının korunacağı kararlaştırıldı.

## Güncelleme (2026-09-21)

- §5 P0 boyut sözleşmesi: `BoardDefinition` ile çözüldü. §5 P1 dead-board ve havuz: **ertelendi**. §5 P1 catalog fallback ve null güvenliği: dokunulmadı.
- §4 matrisindeki "her grup 10 + tur sonu 10 puan" davranışı değiştirildi: yalnızca olay başına puan (D1).
- §4 hedef sayacı: roket artık `Rocket` hedefine de sayılır (D5).
- Kazanma artık hedef tamamlandığı anda ilan edilir (özel vuruşla da); `GameSession` içinde.
- §7 eşzamanlılık kararı korundu; hücre sahipliği uygulanmadı. Bulunan ve düzeltilen yarış: `AreAllMatchedPotionsDestroyed` (B1).
- Kod yapısı: `docs/superpowers/specs/2026-09-21-gameboard-split-design.md`.
