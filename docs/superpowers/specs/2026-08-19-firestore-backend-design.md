# Firestore Backend — Tasarım Dokümanı

**Tarih:** 2026-08-19
**Durum:** Onaylandı (tasarım sohbet içinde bölüm bölüm onaylandı)
**Kapsam:** Ana menünün Play / Clan / Rank sayfalarını besleyen Firestore veri modeli, sorgular ve güvenlik kuralları.

## Amaç

Oyunun ilerlemesi şu an yalnızca cihazda. Ana menüye clan, profil ve sıralama sayfaları ekleniyor; bunların hepsi paylaşılan bir veri katmanı gerektiriyor. Bu doküman o katmanın yapısını tanımlar.

**Bilinçli kapsam dışı:** Shop ve IAP (ayrı bir fazda ele alınacak), Cloud Functions, push bildirimleri, clan savaşları, arkadaş sistemi.

## Kararlar

| Konu | Karar |
|---|---|
| Veritabanı | Firestore (NoSQL) |
| Giriş | Anonim başlangıç + sonradan Google/Apple bağlama |
| Clan işlevi | Üyelik + sohbet + can isteği |
| Sıralama | Global, toplam ilerlemeye göre (`totalScore`), sıfırlama yok |
| Ekonomi | Can (maks 5, zamanla dolar) + altın |
| Sunucu kodu | Yok — Cloud Functions Blaze planı gerektirdiği için bu fazda kullanılmıyor |

## 1. `users` koleksiyonu

```
users/{uid}                      ← uid Firebase Auth'tan gelir
├─ displayName: "mahmut"
├─ avatarIndex: 3                ← hazır avatar listesinden index (Storage kullanılmaz)
├─ isLinked: false               ← anonim mi, Google/Apple'a bağlandı mı
├─ createdAt: <timestamp>
├─ lastSeenAt: <timestamp>
│
├─ highestCompletedLevel: 7
├─ totalScore: 12450             ← Rank sayfası buna göre sıralar
├─ bestScores: { "1": 620, "2": 540 }   ← map: level no → en iyi skor
│
├─ lives: 4
├─ livesUpdatedAt: <timestamp>
├─ gold: 250
│
└─ clanId: "abc123" | null
```

**Can dolumu:** Arka planda zamanlayıcı yok. Yalnızca `lives` ve `livesUpdatedAt` saklanır; istemci açılışta geçen süreden anlık canı hesaplar (`geçenSüre / dolumSüresi`, maks 5 ile sınırlı). Oyuncu offline'ken de can dolmuş olur, sunucuda tek yazma yapılmaz.

**`bestScores` neden map:** Skorlar hep profille birlikte okunuyor; alt koleksiyon olsaydı her açılışta ek sorgu gerekirdi. Döküman 1MB sınırı birkaç bin level'a yeter.

**`clanId` neden users'ta:** Oyuncunun tek clan'ı var. "Bu clan'ın üyeleri kim?" sorusu `users where clanId == X` ile cevaplanır; üye listesi clan dökümanında tekrarlanmaz (iki kopya kaçınılmaz olarak ayrışır).

## 2. `clans` koleksiyonu

```
clans/{clanId}
├─ name: "Ejderhalar"
├─ description: "..."
├─ emblemIndex: 2
├─ leaderUid: "abc"
├─ memberCount: 12               ← sayarak değil, increment(±1) ile
├─ maxMembers: 30
├─ totalScore: 148000            ← üyelerin puan toplamı, increment ile
├─ minLevel: 0                   ← katılım şartı
└─ createdAt: <timestamp>
```

### Sohbet ve can istekleri — tek akış

```
clans/{clanId}/messages/{messageId}
├─ type: "chat" | "lifeRequest" | "system"
├─ senderUid / senderName / senderAvatarIndex   ← isim kopyalanır (join yok)
├─ text: "selam"
├─ createdAt: <timestamp>
├─ expireAt: <timestamp>         ← TTL: 7 gün sonra otomatik silinir
│
└─ (yalnız lifeRequest için)
   ├─ donorUids: ["uid1", "uid2"]
   └─ claimed: false
```

Can isteği ayrı koleksiyon değil, sohbet akışında özel bir mesaj tipi — tek dinleyici, tek liste.

**Can teslimi (Cloud Functions'sız):** Bir oyuncu başkasının dökümanına yazamaz. Bu yüzden **alıcı toplar**: bağışçı yalnızca mesajın `donorUids` dizisine kendi uid'ini `arrayUnion` ile ekler; alıcı oyunu açtığında toplanmamış isteklerini görüp kendi `lives` alanını artırır ve `claimed = true` yapar.

**`senderName` neden kopyalanıyor:** 50 mesajlık sohbette 50 ayrı kullanıcı dökümanı okumamak için. Bedeli: isim değişirse eski mesajlarda eski ad görünür — kabul edilen maliyet.

**TTL:** Firestore'un yerleşik TTL özelliği `expireAt` alanına bakarak eski mesajları otomatik siler. Ücretsiz, temizlik kodu gerektirmez.

### Clan adı benzersizliği

Firestore'da unique kısıtı yok. Çözüm: küçük bir yardımcı koleksiyon.

```
clanNames/{kucukHarfliAd}
└─ clanId: "abc123"
```

Clan kurulurken bu döküman ve clan dökümanı **aynı transaction içinde** oluşturulur; döküman zaten varsa isim alınmış demektir.

## 3. Rank sayfası

- **Top 100 oyuncu:** `users` → `orderBy totalScore desc` → `limit 100`. 100 okuma.
- **Top 100 clan:** `clans` → `orderBy totalScore desc` → `limit 100`.
- **"Benim sıram kaçıncı?":** Firestore döküman sırasını doğrudan vermez. `users where totalScore > benimSkorum` üzerinde `count()` toplama sorgusu çalıştırılır, sonuca 1 eklenir. Toplama sorgusu dökümanları tek tek okumaz; 1000 eşleşme başına 1 okuma faturalanır.

Ayrı bir `leaderboard` koleksiyonu tutulmaz — kopyalanan veri senkron sorunu ve ek yazma maliyeti getirir. Sıralama canlı veriden okunur.

## 4. Görünürlük

Liderlik tablosunun çalışması için kullanıcı dökümanları giriş yapmış herkese **okunabilir**. Orada kişisel veri yok: e-posta ve kimlik bilgisi Firestore'da değil, Firebase Auth tarafında durur. Profili "herkese açık / özel" diye ikiye bölmek her kayıtta iki yazma demek olurdu; bu aşamada gereksiz.

## 5. Güvenlik kuralları

**Dürüst sınır:** Oyuncu kendi dökümanına yazdığı için `totalScore`'u kurcalayabilir. Güvenlik kuralları oyun mantığını doğrulayamaz, yalnızca yazmanın şeklini denetler. Tam çözüm sunucu otoritesidir (Cloud Functions → Blaze planı). Bu faz için aşağıdaki kurallar yeterli kabul edilmiştir; **para söz konusu olduğunda (Shop fazı) altın işlemleri sunucuya taşınacaktır.**

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    function isSignedIn() {
      return request.auth != null;
    }

    function isOwner(uid) {
      return isSignedIn() && request.auth.uid == uid;
    }

    function unchanged(field) {
      return request.resource.data[field] == resource.data[field];
    }

    match /users/{uid} {
      allow read: if isSignedIn();

      allow create: if isOwner(uid)
                    && request.resource.data.totalScore == 0
                    && request.resource.data.highestCompletedLevel == 0
                    && request.resource.data.gold == 0;

      allow update: if isOwner(uid)
                    && unchanged('createdAt')
                    // puan yalnız artar, tek yazmada makul sınırla
                    && request.resource.data.totalScore >= resource.data.totalScore
                    && request.resource.data.totalScore <= resource.data.totalScore + 5000
                    // level yalnız artar, seferde +1
                    && request.resource.data.highestCompletedLevel >= resource.data.highestCompletedLevel
                    && request.resource.data.highestCompletedLevel <= resource.data.highestCompletedLevel + 1
                    && request.resource.data.lives >= 0
                    && request.resource.data.lives <= 5;

      allow delete: if false;
    }

    match /clanNames/{nameKey} {
      allow read: if isSignedIn();
      allow create: if isSignedIn();
      allow delete: if isSignedIn();
    }

    match /clans/{clanId} {
      allow read: if isSignedIn();

      allow create: if isSignedIn()
                    && request.resource.data.leaderUid == request.auth.uid
                    && request.resource.data.memberCount == 1;

      allow update: if isSignedIn() && (
                      // lider her ayarı değiştirebilir
                      resource.data.leaderUid == request.auth.uid
                      // diğerleri yalnız katılma/ayrılma sayaçlarını
                      || (
                        unchanged('name')
                        && unchanged('leaderUid')
                        && unchanged('minLevel')
                        && request.resource.data.memberCount >= resource.data.memberCount - 1
                        && request.resource.data.memberCount <= resource.data.memberCount + 1
                      )
                    );

      allow delete: if isSignedIn() && resource.data.leaderUid == request.auth.uid;

      match /messages/{messageId} {
        allow read: if isSignedIn();

        allow create: if isSignedIn()
                      && request.resource.data.senderUid == request.auth.uid
                      && get(/databases/$(database)/documents/users/$(request.auth.uid)).data.clanId == clanId;

        allow update: if isSignedIn() && (
                        // can bağışı: yalnız kendi uid'ini ekler
                        (
                          request.resource.data.donorUids == resource.data.donorUids.concat([request.auth.uid])
                          && unchanged('text')
                          && unchanged('senderUid')
                          && unchanged('claimed')
                        )
                        // alıcı canları topladı
                        || (
                          resource.data.senderUid == request.auth.uid
                          && resource.data.claimed == false
                          && request.resource.data.claimed == true
                          && unchanged('donorUids')
                        )
                      );

        allow delete: if false;
      }
    }
  }
}
```

**Maliyet notu:** Mesaj oluşturma kuralındaki `get()` her mesaj için 1 ek okuma sayılır. Kabul edilebilir; alternatifi clan üyeliğini mesaja gömüp doğrulamayı zayıflatmak olurdu.

## 6. İndeksler

Aşağıdakiler Firestore'un otomatik tek alan indeksleriyle karşılanır, elle iş gerektirmez:

- `users` → `totalScore` (sıralama)
- `users` → `clanId` (üye listesi)
- `clans` → `totalScore`
- `messages` → `createdAt`

**Tek elle oluşturulacak bileşik indeks:** Clan üye listesini puana göre sıralı göstermek istenirse (`users where clanId == X orderBy totalScore desc`) → `clanId ASC, totalScore DESC`. Firestore ilk çalıştırmada konsola oluşturma bağlantısı yazdırır.

## 7. Firebase Console kurulumu

1. **Firestore Database → Create database** → *Production mode* (kurallar kilitli başlar) → bölge: **eur3 (europe-west)**. Bölge sonradan değiştirilemez.
2. **Authentication → Sign-in method** → **Anonymous**, **Google**, **Apple** sağlayıcılarını etkinleştir.
3. **Firestore → Rules** → yukarıdaki kuralları yapıştır → Publish.
4. **Firestore → Indexes** → gerekirse yukarıdaki bileşik indeksi ekle (ya da uygulama ilk çalıştığında konsoldaki bağlantıyı kullan).
5. **Firestore → TTL** → koleksiyon grubu `messages`, alan `expireAt` için TTL politikası oluştur.
6. Android build'i için `google-services.json` indirilip `Assets/` içine konmalı (iOS için `GoogleService-Info.plist` zaten mevcut).

**Şema oluşturmak gerekmez:** Firestore şemasız çalışır; koleksiyonlar ilk döküman yazıldığında kendiliğinden oluşur. Konsolda elle koleksiyon açmaya gerek yoktur.

## 8. Doğrulama

- Oyun ilk açılışta anonim giriş yapıp `users/{uid}` dökümanını oluşturuyor.
- Bölüm kazanınca `totalScore`, `highestCompletedLevel`, `bestScores` güncelleniyor; ikinci cihazda aynı hesapla giriş yapınca ilerleme geliyor.
- Clan kurulup ikinci hesapla katılınıyor; `memberCount` doğru artıyor.
- Sohbet mesajı iki istemcide de anlık görünüyor.
- Can isteği gönderiliyor, ikinci hesap bağışlıyor, ilk hesap topladığında canı artıyor.
- Rank sayfası ilk 100'ü ve oyuncunun kendi sırasını doğru gösteriyor.
- Kural testi: bir hesapla başka bir kullanıcının dökümanına yazmayı dene → reddedilmeli. `totalScore`'u 1.000.000 yapmayı dene → reddedilmeli.
