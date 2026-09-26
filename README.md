Pirate Match
====
#### Unity 6 ve Firebase ile geliştirdiğim, korsan temalı bir mobil match-3 oyunu.
**Projeyi çalıştırmak için [Başlarken](#başlarken) bölümündeki adımları izleyebilirsin.**

Giriş
------

Pirate Match, Unity ve C# ile geliştirdiğim, korsan temalı bir mobil match-3 oyunu. Bölümleri geçmek için taşları eşleştirir, roket ve bomba oluşturur, bunları birleştirerek daha geniş alanları temizlersin. Hamlelerini dikkatli kullanman gerekir; her bölümün kendi tahtası ve hedefleri vardır.

Bir noktada yardıma ihtiyacın olursa çekiç, bomba veya top kullanabilirsin. Çekiç seçtiğin taşın çevresini kırar, bomba bir alanı temizler, top ise seçtiğin satıra ateş eder. Her birinin kendine ait kısa bir animasyonu vardır.

Bölümler arasında bir klan kurabilir, diğer oyuncularla sohbet edebilir, can isteyebilir ve bağış yapabilirsin. Bölüm ilerlemen ve puanların genel sıralamada da yerini belirler.

Kodun yapısı da oynarken karşılaştığın kavramları izler: bölüm, tahta, hücre, taş, eşleşme ve zincir. Aşağıdaki açıklamalar bu parçaların ne yaptığını ve nasıl bir araya geldiğini anlatır.

### Motivasyon

Bir match-3 oyununda eşleşmeyi bulmak işin bir kısmı. Taşların nasıl düştüğü, bir patlamanın diğerini ne zaman tetiklediği ve oyunun dokunuşa ne kadar çabuk karşılık verdiği de en az bunun kadar önemli.

Pirate Match’i geliştirirken bu ayrıntılar üzerinde durdum. Taşlar düşerken hızlanır, yere inerken esner, takas sırasında arkalarında duman bırakır. Hareket etmeyen taşlarla oynamak için tahtanın geri kalanının durmasını beklemen gerekmez.

Bu davranışlar arttıkça tahta kodu da büyüdü. Başlangıçta 2.200 satıra ulaşan sınıfı; doldurma, giriş, efektler ve özel taş zincirleri gibi ayrı işler üstlenen bileşenlere ayırdım. Bu düzenlemenin ayrıntılarını [tasarım dokümanında](docs/superpowers/specs/2026-09-21-gameboard-split-design.md) bulabilirsin.

Bölüm hazırlarken ise kodla uğraşmana gerek yok. Tahtanın şeklini, hamle sayısını ve hedefleri bir bölüm asset’i üzerinden ayarlayabilirsin. Sosyal özellikler Firebase üzerinde çalışır; klan, sohbet ve can bağışı için ayrıca Cloud Functions kullanılmaz.

------
Teknoloji
======
Unity
------

Oyunun temelinde **Unity 6000.5** ve 2D Universal Render Pipeline bulunur. Dokunuşlar Input System üzerinden alınır; arayüzde uGUI ve TextMesh Pro kullanılır. Korsan karakterinin animasyonlarını Spine, patlama ve iz efektlerini Cartoon FX Remaster sağlar.

Oyun kodları `Match3`, editör araçları `Match3.Editor` assembly’sinde yer alır. Namespace’ler klasörlerle aynı düzeni izler. Örneğin tahta kodunu `Match3.Gameplay.Board`, menü kodunu `Match3.Menu`, Firebase servislerini `Match3.Backend` altında bulabilirsin.

### Tahta şekli ve maske

Ekranda gördüğün tahta 6 sütun ve 8 satırdır. Bunun üzerinde, oyuncunun görmediği 7 satır daha bulunur. Yeni taşlar bu alanda oluşturulur ve boşalan hücrelere doğru düşer.

Tahtanın dikdörtgen olması gerekmez. Bir bölümde artı, diğerinde baklava veya L şeklinde bir alan kullanabilirsin. Hangi hücrelerin açık kalacağını bölüm verisi belirler.

`BoardMask`, bu veriden oyun sırasında bir doku üretir. Her hücre bir pikseldir: kapalı hücreler opak, açık hücreler şeffaf olur. Doku bir `SpriteMask` üzerinde kullanılır ve opak kısımlardaki taşlar gizlenir. Böylece her tahta şekli için ayrı bir çerçeve çizmek gerekmez.

### Hareket ve his

Bir taşın bir hücre düşmesiyle tahtanın tepesinden düşmesi aynı görünmemeli. Bu yüzden düşüş hızı yerçekimiyle artar ve belirlenen üst sınırda kalır. Taş ne kadar hızlanırsa o kadar esner.

Yere ulaştığında iniş animasyonu oynatılır. Bu animasyon bitene kadar taş hareket halinde sayılır; eşleşmiş olsa bile kırılmak için inişini tamamlaması gerekir.

### Eşzamanlı çözümleme

Tahtanın bir tarafı dolarken diğer tarafta hamle yapabilirsin. Kabul edilen dokunuşlar ve takaslar, tüm taşların durmasını bekleyen bir kuyruğa alınmaz. Buradaki sınır taşın kendisidir: hareket eden bir taşı takas edemezsin.

Birden fazla işlemin birlikte ilerlemesi iki kurala dayanır:
- Tahtayı değiştiren kod, coroutine'in ilk `yield`'inden önce biter.
- Her çözümleme kendi zincir durumunu (`ChainContext`) taşır.

Her zincir kendi durumunu taşır, dolayısıyla bir zincirin sayacı diğerini etkilemez. `IsMoving` durumundaki taşlar da eşleşme ve takas kontrollerine katılmaz.

### Proje yapısı

```
Assets/
├─ Scripts/
│  ├─ Gameplay/
│  │  ├─ Board/      Tahta modeli, eşleşme, doldurma ve havuz, zincirler, giriş, maske
│  │  ├─ Potions/    Taş bileşeni ve özel taş görselleri
│  │  ├─ Strikes/    Çekiç / Bomba / Top güçlendiricileri ve sinematikleri
│  │  └─ Session/    Bölüm kuralları, sahne adaptörü, HUD ve oyun sonu panelleri
│  ├─ Levels/        Bölüm verisi, katalog ve bölüm asset'leri
│  ├─ Menu/          Ana menü: sayfalar, profil, clan, sohbet, sıralama, ayarlar
│  ├─ Backend/       Firebase başlatma, clan ve sohbet servisleri, Firestore modelleri
│  ├─ Shared/        Sahne adları, ses ayarları
│  └─ Editor/        Bölüm şekli editörü
├─ Scenes/           MainMenu, GameBoard
└─ Prefabs/          Taşlar, güçlendiriciler, menü satırları
docs/                Tasarım dokümanları ve Firebase notları
firestore.rules      Firestore güvenlik kuralları
```

Firebase
------

Hesap işlemlerini **Firebase Authentication**, veri saklamayı **Cloud Firestore** üstlenir. Projede Firebase Unity SDK 13.15.0 kullanılır.

Oyuncunun başlamadan önce kayıt olması gerekmez. İlk açılışta anonim bir hesap oluşturulur; cihazdaki oturum bilgileri korunduğu sürece sonraki açılışlarda aynı hesapla devam edilir.

### Veri modeli

Oyuncu, klan ve mesaj verileri şu koleksiyonlarda tutulur:

| Yol | İçerik |
|---|---|
| `users/{uid}` | ad, avatar, en yüksek bölüm, toplam puan, bölüm rekorları, can, altın, clan |
| `clans/{clanId}` | ad, açıklama, amblem, lider, üye sayısı, toplam puan, katılım kuralları |
| `clans/{clanId}/messages/{id}` | sohbet mesajları ve can istekleri |
| `clanNames/{nameLower}` | clan adlarını benzersiz tutan isim rezervasyonu |

İki klanın aynı adı kullanmasını önlemek için `clanNames` içinde bir isim rezervasyonu tutulur. Klan oluşturulurken bu kayıt da işlemin bir parçasıdır.

### Transaction'lar

Bir klan kurduğunda yalnızca yeni bir klan kaydı oluşmaz. Oyuncunun üyeliği değişir, altını azalır ve klan adı rezerve edilir. Bu değişikliklerin birlikte tamamlanması için transaction kullanılır:
- **Klan kurma:** İsim rezervasyonu, klan kaydı, oyuncunun üyelik bilgisi ve altın kesintisi aynı işlemde kaydediliyor.
- **Klana katılma:** Kapasite ve mevcut üyelik, sunucudaki güncel veriler üzerinden kontrol ediliyor. Böylece katılma butonuna iki kez basılması üye sayısını iki kez artırmıyor.
- **Klandan ayrılma:** Ayrılan kişi liderse görev en yüksek seviyeli üyeye devrediliyor. Devir sırasında bu oyuncunun hâlâ klan üyesi olduğu tekrar kontrol ediliyor.

### Can yenilenmesi ve bağış

Oyuncu oyunu kapattığında canların yenilenmesi için çalışan bir sayaç gerekmez. Can sayısı ve son güncelleme zamanı saklanır. Oyuna dönüldüğünde geçen süre hesaplanır ve hak edilen canlar eklenir.

Can bağışında ise küçük bir ayrıntı var: bir oyuncu, başka bir oyuncunun belgesine yazamaz. Bu yüzden bağış yapan kişi isteğe yalnızca kendi kimliğini ekler. Canı isteyen oyuncu bu bağışı kendi hesabına aktarır ve isteği kapatır. Son iki değişiklik aynı batch içinde kaydedilir.

Sohbet mesajları, 7 günlük süre için ayarlanmış Firestore TTL politikasıyla temizlenir. Sıralamadaki yerini bulmak için de bütün oyuncu belgelerini indirmek gerekmez; bunun için bir `count()` sorgusu kullanılır.

### Güvenlik kuralları

Veriye kimin erişebileceği ve hangi değerleri yazabileceği [`firestore.rules`](firestore.rules) içinde tanımlıdır:
- Oyuncu yalnızca kendi dokümanına yazabilir.
- Puan ve bölüm geri gidemez, can 0–5 arasında kalır.
- Sohbeti yalnızca o clanın üyeleri okuyabilir.
- Bağışçı istek mesajında yalnızca kendi kimliğini ekleyebilir.

------
Kavramlar
======

Pirate Match nesne yönelimli bir yapı kullanır. Sınıflar, oyunda karşılığı olan parçaları temsil eder. Bir bölümün verisi, tahtanın hücreleri, taşın hareketi ve oyuncunun kalan hamleleri kendi sorumlulukları içinde ele alınır.

Normalde ana menüden bir bölüm açar, taşları eşleştirir ve hamlelerin bitmeden hedefleri tamamlamaya çalışırsın. Kod da aynı akışı takip eder: bölüm yüklenir, tahta kurulur, takaslar çözülür ve sonuç oyun oturumuna işlenir.

Bölüm
------

Bölüm, oynayacağın tahtanın tarifidir. Kaç hamlen var? Hangi taşları toplaman gerekiyor? Tahtada hangi hücreler kapalı? Kaç güçlendirici kullanabilirsin? Bunların hepsi bir `LevelData` asset’inde tutulur.

`LevelCatalog` bu bölümleri sıraya koyar. `LevelLoader` ise menüde seçilen bölümü oyun sahnesine taşır. Yeni bir bölüm eklemek için yeni bir `LevelData` oluşturup kataloğa yerleştirmen yeterlidir.

Yeni bir bölüm hazırlamak için:

1. **Assets → Create → Scriptable Objects → LevelData** ile bir asset oluştur.
2. Hamle sayısını, puan hedefini ve toplama hedeflerini (taş rengi ya da `Bomb`, adediyle) gir.
3. `arrayLayout` ızgarasında kapatmak istediğin hücreleri işaretle. **İşaretli hücre kapalıdır.** Inspector görünür 8 satırı gösterir; en alttaki satır tahtanın alt satırıdır.
4. Asset'i `LevelCatalog` listesine oynanış sırasıyla ekle.

Kullanım:

```csharp
// Ana menüde Play: oyuncunun geçtiği son bölümden sonrakini seç
LevelData level = catalog.GetPlayableLevel(user.highestCompletedLevel);

LevelLoader.selectedLevel = level;
SceneManager.LoadScene(ButtonControl.GameBoardScene);

// Bölüm kazanılınca: sıradakine geç (son bölümse null döner)
LevelData next = catalog.GetNext(level);
```

Tahta
------

Tahta, taşların yer değiştirdiği ve eşleşmelerin çözüldüğü alandır. Bu akışı `PotionBoard` yönetir: takası alır, eşleşmeleri buldurur, taşları temizletir ve boşalan hücreleri yeniden doldurur.

Bütün bu işleri tek başına yapmaz. Aynı GameObject üzerindeki bileşenler, akışın farklı kısımlarını üstlenir:

| Bileşen | Görevi |
|---|---|
| `BoardRefill` | İlk dolum (başlangıçta hazır eşleşme olmaz), nesne havuzu, sütun doldurma |
| `SpecialChain` | Bomba ve roket zincirleri, kombolar |
| `StrikePresentation` | Güçlendirici sinematikleri |
| `BoardInput` | Dokunuşu takasa ya da patlatmaya çevirir |
| `BoardEffects` | Parçacık efektleri ve sesler |
| `BoardMask` | Bölüm şeklinden maske üretir |

`PotionBoard` bu bileşenleri çağırır; bileşenler onu geri çağırmaz. Akışın yönetimi böylece tek yerde kalır.

Kullanım:

```csharp
// BoardInput dokunuşları tahtaya iletir; kabul kuralları tahtadadır
if (board.AcceptsInput)
{
    board.TrySwap(first, second);   // komşu, duran ve kendi hücresindeki iki taş; değilse false
    board.TryTap(potion);           // özel taşsa patlatır, güçlendirici seçiliyse ona iletir
}

// Bir UI paneli açıkken tahta dokunuş almaz
board.InputLocked = true;
```

Izgara
------

Tahta üzerindeki her taş bir hücreye aittir. `BoardGrid`, bu hücrelerin kaydını tutar ve hücre dizisine erişimi sağlar. Bir taşın yerini değiştirdiğinde hem hücre kaydı hem de taşın koordinatları birlikte güncellenir. Diğer sınıfların diziyi doğrudan değiştirmesi gerekmez.

Boyutlar `BoardDefinition` içinde tanımlanır: `VisibleWidth` 6, `VisibleHeight` 8, `SpawnHeight` 7.

Kullanım:

```csharp
BoardGrid grid = new BoardGrid(level.arrayLayout);    // layout'ta true = kapalı hücre

Potion potion = grid.PotionAt(new Vector2Int(2, 0));  // kapalı, boş ya da tahta dışıysa null
grid.Swap(first, second);                             // hücreler ve koordinatlar birlikte değişir

bool settled = !grid.AnyPotionMoving();
```

Eşleşme
------

Yan yana üç taş normal bir eşleşmedir. Düz bir hatta dört veya daha fazla taş bir roket oluşturur. Hattaki bir taştan dik yönde iki taş daha uzanıp T veya L şekli oluştuğunda ise bomba elde edersin. Bu grupları `MatchFinder` bulur. Her çağrıda ızgaranın o anki durumunu okur; önceki aramanın durumunu saklamaz.

Grubun özel taşa dönüşecek üyesi `ProtectedPotion` olarak tutulur. Takas yaptıysan bu, eşleşmeyi yapan taştır. Eşleşme bir zincir sırasında oluştuysa gruptan rastgele bir taş seçilir.

Takastan sonra önce yer değiştiren iki taşın çevresi kontrol edilir. Tahtanın geri kalanında oluşan eşleşmeler, taşlar durduğunda zincirleme çözümleme sırasında bulunur.

Kullanım:

```csharp
MatchFinder finder = new MatchFinder(grid);

List<MatchResult> groups = finder.FindAround(first, second);  // takastan sonra
groups = finder.FindAll();                                     // tahta durunca

foreach (MatchResult group in groups)
{
    // Uzun hat → roket, T/L → bomba
    if (group.IsSuperMatch) Debug.Log($"{group.Direction}: {group.ProtectedPotion.name}");
}
```

Taş
------

`Potion`, tahtada gördüğün taşın bileşenidir. Taşın türünü, bulunduğu hücreyi ve hareketini tutar. Bir rokete veya bombaya dönüştüğünde görselini de değiştirir.

Taşın konumu ve oyun mantığı ana objede, sprite ile animasyon alt objede bulunur. Bu ayrım sayesinde kodla verilen düşüş esnemesi veya kırılma küçülmesi, animasyonun ölçeğiyle birlikte uygulanabilir.

Bir taş kırıldığında objesi silinmez. Havuza döner ve tahta doldurulurken tekrar kullanılır.

Kullanım:

```csharp
PotionType type = potion.PotionType;   // Red, Blue, Yellow, Green, Bomb, Rocket

potion.BecomeRocket(vertical: true);   // dikey roket sütun temizler
potion.BecomeBomb();

potion.MoveToTarget(cellCenter);       // takas: arkasında duman izi bırakır
potion.MoveToDown(cellCenter);         // düşüş: yerçekimiyle hızlanır, inince esner

if (!potion.IsMoving) { /* hareket ve iniş animasyonu bitti */ }
```

Zincir
------

Bir roketin önüne bomba koyduğunda, roketin bütün hattı temizlemesini beklemek istemezsin. Bomba temas anında patlamalı, kendi alanındaki özel taşları da tetikleyebilmelidir. Zincir sistemi bu şekilde çalışır; her patlama kendi coroutine’iyle ilerler.

`ChainContext`, hangi hücrelerin tetiklendiğini ve kaç patlamanın hâlâ sürdüğünü tutar. Aynı hücre bir zincirde iki kez tetiklenmez. Son patlama da tamamlandığında zincir biter ve tahta yeniden doldurulur.

İki özel taşı birleştirdiğinde de aynı sistem kullanılır:
- **Bomba + Bomba:** birleşme animasyonundan sonra 7 × 7 alanı merkezden dışa doğru halka halka temizler.
- **Roket + Roket:** artı şeklinde patlar; bir roket satırı, diğeri sütunu aynı anda süpürür.

Kullanım:

```csharp
// PotionBoard'da bir özel taşa dokunulduğunda
private IEnumerator TapDetonate(Potion special)
{
    yield return specialChain.ExplodeChain(special);   // zincirin tüm halkaları bitene kadar
    yield return RefillAndCascade();                   // boşlukları doldur, yeni eşleşmeleri çöz

    GameManager.Instance.ProcessTurn();                // bir hamle düş
}
```

Güçlendirici
------

Güçlendirici kullanmak için alt bardan birini seçip tahtadaki hedefe dokunursun. Her bölümün verdiği kullanım hakkı ayrıdır ve bu işlemler hamle harcamaz.

- **Çekiç:** butondan hedefe uçar, taşı ve dört komşusunu kırar.
- **Bomba:** hedefe fırlatılır, 3 × 3 alanı patlatır.
- **Top:** tahta bir hücre kenara kayar, oyuncu bir satır seçer, gülle o satırı baştan sona süpürür.

`SpecialStrikes`, hangi güçlendiricinin seçildiğini, kaç hakkın kaldığını ve hangi hücrelerin etkileneceğini belirler. Tahta ise bu hücreleri temizler. Alanın içinde bir bomba veya roket varsa o da mevcut zincir sistemiyle patlar. Böylece güçlendiriciler için ayrı bir patlama sistemi kurmak gerekmez.

Kullanım:

```csharp
// Çekiç: hücre listesini SpecialStrikes hesaplar (merkez + dört komşu)
board.TryRunStrike(StrikeKind.Hammer, origin, hammerCells, hammerButton.transform);

// Bomba: 3x3 alanı tahta kendisi temizler
board.TryRunStrike(StrikeKind.Bomb, origin, null, bombButton.transform);

// Top: önce tahta kayar, oyuncu bir satıra dokununca ateşlenir
board.TryBeginCannonAim();
board.TryRunStrike(StrikeKind.Cannon, origin, null);

// false dönerse vuruş başlamadı: hak düşmez, seçim açık kalır
```

Oturum
------

Tahta taşlarla ilgilenirken, oturum bölümün gidişatını takip eder. Puanın, kalan hamlelerin, toplama hedeflerin ve bölüm sonucu `GameSession` içinde tutulur. Bu, Unity’den bağımsız bir C# sınıfıdır.

Son hedefi bir takasla da tamamlayabilirsin, bir bombayla veya güçlendiriciyle de. Hedeflerin tamamlanması bölümü kazandırır. Hedefler bitmeden hamlelerin tükenirse bölüm kaybedilir.

`GameManager`, oturumdaki durumu sahneye aktarır. HUD, karakter animasyonları, konfeti ve sonuç panelleri bu sınıf üzerinden güncellenir.

Kullanım:

```csharp
GameSession session = new GameSession(level);   // hedefler kopyalanır, asset değişmez

session.AddPoints(10);
session.RegisterCleared(PotionType.Red);        // kırmızı toplama hedefini bir düşürür
session.EndTurn();                              // hamle 0 olursa sonuç Lost olur

if (session.Outcome == SessionOutcome.Won) { /* tüm hedefler tamam */ }
```

Oyuncu
------

Oyuncu, Firebase’deki anonim hesabıyla temsil edilir. `FirebaseBootstrap` açılışta Firebase’i hazırlar, giriş yapar ve oyuncunun belgesini yükler. İlk kez oynuyorsan belgeyi oluşturur.

Bu nesne sahne değiştiğinde korunur. Canlar, bölüm ilerlemesi ve profil güncellemeleri de buradan yönetilir. Veriler değiştiğinde bir olay yayımlanır; açık arayüzler bu olayı dinleyerek kendini günceller.

Kullanım:

```csharp
// Kullanıcı verisi her değiştiğinde (giriş, can, altın, profil) tetiklenir
FirebaseBootstrap.UserReady += OnUserChanged;

FirebaseBootstrap player = FirebaseBootstrap.Instance;

player.CompleteLevel(level.level, points, goldReward: 50);   // ilerleme, altın, clan puanı: tek batch
player.SpendLife();                                           // bölüm kaybedilince ya da bırakılınca
player.RegenerateLives();                                     // süresi dolan canları ekler
```

Klan
------

Klan, oyuncuların bir araya geldiği takımdır. Altın harcayarak kendi klanını kurabilir veya adıyla aradığın açık bir klana, seviye şartını karşılıyorsan katılabilirsin. Lider ayrılırsa liderlik devredilir. Son üye de ayrıldığında klan silinir.

Klan sohbeti gerçek zamanlıdır. Can istekleri de aynı sohbetin içinde özel bir mesaj türü olarak görünür. İkisini ayrı ayrı takip etmek yerine tek bir liste ve tek bir dinleyici kullanılır.

Bu işlerin iki statik servisi vardır: `ClanService` üyeliği, `ClanChatService` sohbeti ve can isteklerini yönetir.

Kullanım:

```csharp
ClanService.CreateClan(new ClanData { name = "Kara Bayrak", maxMembers = 30 }, (ok, message) => { });
ClanService.SearchClans("kara", 30, clans => resultList.Show(clans));
ClanService.JoinClan(clan, (ok, message) => { });
ClanService.LeaveClan((ok, message) => { });

ListenerRegistration chat = ClanChatService.Listen(clanId, 50, Render);
ClanChatService.SendChat("Selam!");
ClanChatService.SendLifeRequest();
ClanChatService.DonateLife(request);                  // bağışçı yalnızca kendi kimliğini ekler
ClanChatService.ClaimLives(request, gained => { });   // canları isteği atan toplar
chat.Stop();
```

------
Başlarken
======
Gereksinimler
------

- Unity Hub ve **Unity 6000.5.0f1**, iOS ve/veya Android build desteği modülleriyle
- Node.js (yalnızca Firestore kurallarını yayınlamak için)

Kurulum
------

**1. Klonla ve aç.**

```bash
git clone git@github.com:mahmut483/match3.git
```

İndirdiğin proje klasörünü Unity Hub üzerinden aç.

**2. Firebase masaüstü kütüphanelerini ekle.**

`Assets/Firebase/Plugins/x86_64/` klasörü, GitHub’ın dosya boyutu sınırı nedeniyle repoya dahil edilmedi. Firebase’in editörde çalışması için [Firebase Unity SDK 13.15.0](https://firebase.google.com/docs/unity/setup) içindeki `FirebaseAuth.unitypackage` ve `FirebaseFirestore.unitypackage` paketlerini projeye import et.

**3. Firebase projesini bağla.**

Repo, `match3-3dc9b` projesine göre yapılandırılmış durumda. Kendi Firebase projenle çalışmak için:
1. Firebase'de iOS (`com.MahmutCompany.match3`) ve Android uygulamalarını ekle.
2. `GoogleService-Info.plist` ve `google-services.json` dosyalarını `Assets/` altına koy.
3. **Authentication**'da **Anonymous** girişi aç ve bir **Firestore** veritabanı oluştur.
4. Kuralları repo kökünden yayınla:
   ```bash
   npx firebase-tools login
   npx firebase-tools deploy --only firestore:rules
   ```
5. **Firestore → Time-to-live**'da `messages` koleksiyon grubu ve `expireAt` alanı için bir politika ekle.

Firebase kurulumu hakkında ek bilgi için [`docs/firebase.md`](docs/firebase.md) dosyasına bakabilirsin.

Çalıştırma
------

`Assets/Scenes/MainMenu.unity` sahnesini açıp editörde **Play**’e bas. Anonim giriş tamamlanıp oyuncu verisi yüklendiğinde menüdeki Play butonu kullanılabilir hale geliyor.

Tahtayı doğrudan denemek için `Assets/Scenes/GameBoard.unity` sahnesini de açabilirsin. Bu durumda **GameManager → Level Data** alanındaki test bölümü yükleniyor. Can ve ilerleme gibi Firebase’e bağlı özellikler bu kullanımda devre dışı kalıyor.

------
Proje hakkında
======
Tasarım dokümanları
------

- [Bölüm sistemi](docs/superpowers/specs/2026-08-18-level-system-design.md)
- [Firestore backend](docs/superpowers/specs/2026-08-19-firestore-backend-design.md)
- [Özel vuruşlar (güçlendiriciler)](docs/superpowers/specs/2026-09-08-special-strikes-design.md)
- [Oyun tahtası ayrıştırma refactor'ü](docs/superpowers/specs/2026-09-21-gameboard-split-design.md)

Bilinen sınırlamalar
------

- **Oyun sonuçları sunucuda doğrulanmıyor.** Güvenlik kuralları veri biçimini ve erişimi kontrol ediyor; oynanan hamlelerin geçerliliğini doğrulamıyor. Bu nedenle değiştirilmiş bir istemci puan veya altın değerlerine müdahale edebilir. Gerçek para içeren özellikler eklenmeden önce ekonomi işlemlerinin Cloud Functions gibi bir sunucu tarafı yapıya taşınması gerekiyor.
- **Bekleme süreleri geliştirme için kısa tutuldu:**
  - Can yenilenmesi: 60 sn (`LifeRules.RegenSeconds`)
  - Can isteği bekleme süresi: 10 sn (`ClanChatService.RequestCooldownSeconds`)
- **Android** için gereken `google-services.json` repoda yok.
- Projede henüz otomatik test bulunmuyor.

Üçüncü parti asset'ler
------

Projede kullanılan üçüncü parti paket ve asset’ler aşağıda listeleniyor. Her biri kendi lisans koşullarına tabi.

- [Spine Runtimes](https://esotericsoftware.com/spine-runtimes) (spine-unity), Esoteric Software
- Cartoon FX Remaster (Free), Jean Moreno (JMO)
- Gem Hunter Match örnek asset'leri, Unity Technologies
- Hyper Casual FX, Lana Studio
- Farm Game UI – Simple 2D UI, maanetorn
- 2D Casual UI
- Firebase Unity SDK ve External Dependency Manager, Google
