# Level Sistemi — Tasarım Dokümanı

**Tarih:** 2026-08-18
**Durum:** Onaylandı (tasarım sohbet içinde bölüm bölüm onaylandı)
**Kapsam:** Match3 oyununa level sistemi: tilemap tabanlı tahta şekilleri, hedefler (puan + renk toplama), local kayıt, ana menü Play akışı.

## Amaç

Oyunda şu an level kavramı yok: `GameManager.Initialize()` hiç çağrılmıyor, hedef/hamle değerleri sahnede elle giriliyor, ana menüden oyuna geçiş yok. Bu tasarım, tekrar oynanabilir ve ilerlemesi kaydedilen bir bölüm sistemi kurar. İlerideki fazların (Firebase auth, clan, shop) üzerine oturacağı temel budur.

**Bilinçli kapsam dışı:** engeller (buz/jöle/sandık), kırılacak objeler, yıldız sistemi, dünya haritası, Firebase senkronu. Bunlar sonraki fazlarda.

## 1. Veri Modeli

### LevelData (ScriptableObject)

Her bölüm için bir asset: `Assets/Data/Levels/Level_01.asset` …

```
LevelData
├─ levelNumber: int          (1, 2, 3…)
├─ moves: int                (hamle hakkı)
├─ bonusPerMove: int         (varsayılan 50 — bölüm bitince kalan hamle başına eklenen puan)
├─ boardTilemapPrefab: GameObject   (tahta şeklini tanımlayan Tilemap prefab'ı)
└─ goals: List<LevelGoal>    (1-3 hedef)
```

### LevelGoal ([Serializable] sınıf)

```
LevelGoal
├─ type: GoalType            (Score | Collect)
├─ potionType: PotionType    (yalnız Collect için anlamlı)
└─ amount: int               (500 puan / 15 kırmızı gibi)
```

Bir bölüm, listedeki **tüm** hedefler tamamlanınca kazanılır.

### LevelCatalog (ScriptableObject)

Tüm `LevelData` asset'lerinin sıralı listesi. "Sıradaki bölüm hangisi?" sorusunun tek cevap yeri. Ana menü ve GameBoard sahnesi buradan okur.

### LevelLoader (statik taşıyıcı)

MainMenu → GameBoard sahne geçişinde seçilen bölümü taşır: `static LevelData selectedLevel`. DontDestroyOnLoad'lu bir nesne gerekmez.

## 2. Tahtanın Tilemap'ten Türetilmesi

- GameBoard sahnesi açılınca `PotionBoard`, aktif `LevelData.boardTilemapPrefab`'ı sahnedeki `Grid` altına instantiate eder.
- Her görünür hücre `(x, y)` için mevcut konum matematiği (`(x - spacingX) * cellSize`) ile dünya konumu hesaplanır, `tilemap.WorldToCell` + `tilemap.HasTile` ile sorgulanır:
  - **Boyalı hücre → `isUsable = true`; boş hücre → `isUsable = false`.**
- `ArrayLayout` + `CustPropertyDrawer` devre dışı kalır (dosyalar ilk etapta silinmez, kullanım kalkar).
- Yeni bölüm tasarlamak = tilemap prefab'ı boyamak + LevelData doldurmak. Kod değişikliği yok.

### Zorunlu eşlik eden düzeltmeler

1. **Sabit `8` literal'leri → `[SerializeField] visibleHeight`.** `CheckBoard`, `CheckDirection`, `BombExploding`, `SuperBombExplod` içindeki hardcoded `8`'ler tek alandan okunur.
2. **Refill `isUsable`'a saygı duyar.** Mevcut `RefillPotion` yalnız `potion == null` kontrol ediyor — kapalı hücreleri de doldurmaya çalışır (gizli bug). Yeni davranış: yukarı arama kapalı hücreleri atlar (taşlar kapalı hücrelerin üzerinden düşer), kapalı hücreye asla taş yerleşmez. `FindIndexOfLowestNull` yalnız açık hücreleri sayar.
3. **Spawn kontrolü.** Görünür kısmında hiç açık hücre olmayan sütuna üstten taş doğmaz; spawn satırları (görünür alanın üstündeki mantıksal satırlar) sütunun açıklığına göre ayarlanır.

**Ölü hücre riski:** İzole tek hücre gibi tuhaf şekiller taş sıkıştırabilir. İlk sürümde sorumluluk level tasarımcısında ("izole cep boyama" kuralı). Otomatik doğrulama sonraki iş.

## 3. Oyun Akışı ve Hedef Takibi

### Başlatma

`GameManager.Initialize(LevelData)` sahne açılışında çağrılır. `LevelLoader.selectedLevel` doluysa o; boşsa Inspector'a atanmış `defaultLevel` (editörde sahneyi tek başına test edebilmek için).

### Taş sayımı — tek nokta

Temizlenen her taş zaten `PotionBoard.ReturnPotionToPool`'dan geçiyor. Oraya tek satır: `GameManager.Instance.RegisterClearedPotion(type)`. Normal/süper eşleşme, bomba, süper bomba — hepsi otomatik sayılır.
İnce detay: `Bomb(false)` tipi sıfırlamadan **önce** raporlanır; bomba orijinal rengine sayılır.

### Puanlama

Sabit "tur başına 10 puan" kalkar → **temizlenen taş başına 10 puan** (`RegisterClearedPotion` içinde). Böylece büyük eşleşme, cascade ve bombalar doğal olarak daha çok kazandırır. `ProcessTurn` yalnız hamle düşürür; puan parametresi kalkar.

### Hedef durumu

`GameManager` her hedef için ilerleme tutar (`Score: 340/500`, `Red: 12/15`). Taş raporlandıkça ilgili sayaç artar.

### Kazanma / kaybetme

- Tüm hedefler tamam → **kazanma**: `bonus = kalanHamle * bonusPerMove` puana eklenir, win paneli açılır. (Görsel "kalan hamleler patlıyor" şovu yok; direkt eklenir. Şov sonraki iş.)
- Hamle 0 ve hedefler eksik → kaybetme.
- **Değerlendirme cascade tamamen bittikten sonra** yapılır (board `Idle`'a dönerken) — son hamlenin zincirlemesi hedefi tamamlayabilir.
- `ProcessTurn`'e `if (isGameEnded) return;` ve `moves <= 0` düzeltmeleri dahildir.

### UI

- HUD'a hedef göstergesi: her Collect hedefi için taş ikonu + kalan sayı; yanında puan ve hamle.
- Metin güncellemeleri `Update()`'ten çıkar, olay anında yapılır (mevcut her-frame string üretimi de düzelmiş olur).
- Win panelindeki **NextLevel** → `LevelCatalog`'dan sıradaki bölüm.

## 4. Kayıt Sistemi

- `SaveManager` + `SaveData`: JSON, `Application.persistentDataPath/save.json`, `JsonUtility` (paket yok).
- İçerik minimal:
  ```
  SaveData
  ├─ highestCompletedLevel: int
  └─ bestScores: List<LevelScore { levelNumber, score }>
  ```
- Bölüm kazanılınca güncellenir ve diske yazılır.
- **Firebase'e hazırlık:** Bu fazda kayıt tamamen local. Backend fazında `SaveManager` arkasına "local + Firestore senkron" katmanı eklenecek; `SaveData` düz tutulduğu için Firestore dökümanına birebir taşınır. Veritabanı kararı: **Firestore (NoSQL)** — Unity SDK, offline cache ve auth entegrasyonu hazır; ilişkisel sorgu ihtiyacı yok.

## 5. Ana Menü Entegrasyonu

- **Play butonu** (HomePage): üzerinde sıradaki bölüm numarası ("Level 7"). Basınca `LevelLoader.selectedLevel` set edilir, `GameBoard` yüklenir.
- **Level listesi:** Play'in altında kaydırılabilir grid. Hücre: bölüm no, kilitli/açık, geçilmişse en iyi skor. Geçilmiş bölüme basınca tekrar oynanır. `LevelCatalog` + `SaveData`'dan doldurulur.
- **Sahne geçiş düzeltmeleri dahildir:** Build Settings'e `MainMenu` + `GameBoard` eklenir, ölü GemHunterMatch girdileri silinir. `ButtonControl` sahne adıyla yükler: TryAgain → aktif bölümü yeniden başlat, NextLevel → kataloktan sıradaki, BackToMenu → MainMenu.

## 6. Doğrulama

3 örnek bölüm (farklı tahta şekilleri + farklı hedef kombinasyonları) ile uçtan uca akış:
menü → bölüm seç → oyna → kazan (bonus puan) → kayıt → menüye dön → listede güncel durum → sıradaki bölüm. Kaybetme akışı ve TryAgain da test edilir.
