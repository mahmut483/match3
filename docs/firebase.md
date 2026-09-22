# Firebase (Firestore) kurulumu

Proje: `match3-3dc9b`. Kurallar repoda `firestore.rules` dosyasında tutulur; console'da
elle düzenleme yapma, burayı değiştirip yayınla.

## Kuralları yayınlama

Firebase CLI kurulu değil; `npx` ile çalışır (Node var). Proje kökünde:

```
npx firebase-tools login          # bir kez, tarayıcıda Google hesabıyla
npx firebase-tools deploy --only firestore:rules
```

Alternatif: Firebase Console → Firestore Database → Rules → `firestore.rules` içeriğini
yapıştır → Publish.

## Mesajların otomatik silinmesi (TTL)

Sohbet mesajları `expireAt` alanıyla yazılır (gönderimden 7 gün sonra). Firestore bu alanı
yalnızca TTL politikası açıksa siler; politika açık değilse mesajlar sonsuza kadar kalır.

Firebase Console → Firestore Database → **Time-to-live (TTL)** sekmesi → Create policy:

- Collection group: `messages`
- Timestamp field: `expireAt`

Silme, süre dolduktan sonra 24 saat içinde olur; sorgular zaten `limit` ile son 50 mesajı
çektiği için oyunda fark edilmez.

## Kuralların özeti

| Koleksiyon | Okuma | Yazma |
|---|---|---|
| `users/{uid}` | giriş yapmış herkes | yalnızca sahibi; puan/bölüm geri gitmez, can 0–5, altın ≥ 0 |
| `clans/{id}` | giriş yapmış herkes | lider her alanı; diğerleri sadece `memberCount`/`totalScore` (katılma, ayrılma, bölüm puanı); silme lider |
| `clans/{id}/messages/{m}` | yalnızca o clanın üyeleri | üye gönderir (`senderUid` = kendisi); bağış sadece `donorUids`; `claimed` sadece isteğin sahibi |
| `clanNames/{name}` | giriş yapmış herkes | oluşturma herkes (kurma/yeniden adlandırma); silme o clanın lideri |
