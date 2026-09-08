# Özel Vuruşlar (Special Strikes)

Tab bar'daki üç buton oyuncuya tahtaya doğrudan müdahale hakkı verir.
Bir vuruş seçilir, sonra bir taşa dokunulur ve vuruş o taşı merkez alarak uygulanır.

## Davranış

Seçim bir aç/kapa. Seçili vuruşa tekrar basmak seçimi bırakır, başka bir vuruşa
basmak ona geçer. Vuruş seçiliyken takas yapılamaz. Vuruş kullanıldıktan sonra
seçim kendiliğinden kalkar: bir seçim, bir vuruş. Açık kalsaydı sonraki dokunuş
farkında olmadan ikinci hakkı harcardı.

| Vuruş | Kapsadığı hücreler |
|---|---|
| Hammer | Merkez ve dört komşusu. Kol uzunluğu Inspector'dan ayarlanır, varsayılan bir |
| Cannon | Dokunulan taşın satırının tamamı |
| Bomb | Merkez etrafında 3x3 |

Vuruş bir hamle harcamaz. Hakların sayısı `LevelData` içinde seviye başına
tanımlanır ve seviye bittiğinde sıfırlanır. Hakkı biten buton devre dışı kalır,
sayısı görünmeye devam eder.

Vuruşun kapsadığı alanda bomba veya roket varsa patlar ve zinciri tetikler.
Bu bedava geliyor: hücreler mevcut `ClearCell` yolundan geçiyor.

Refill ve cascade sürerken de vuruş yapılabilir. Dokunarak patlatma ve takas
zaten öyle çalışıyor.

## Sorumluluk sınırı

**`SpecialStrikes.cs`** özelliğin tamamını taşır: seçim durumu, sayaçlar, buton
görünümleri ve her vuruşun hangi hücreleri kapsadığının hesabı. Tahtanın iç
yapısına hiç dokunmaz; ürettiği tek şey bir `Vector2Int` listesi.

**`PotionBoard`** o listeyi temizler. `RunStrike` hücreleri zincir sayacıyla
`ClearCell`'den geçirir, hepsi bitince `RefillAndCascade` çağırır. `ClearCell`
ve `ChainCounter` private kalmaya devam eder.

Bu ayrım bilinçli: "hangi hücreler" oyun tasarımı sorusu ve sık değişir,
"nasıl temizlenir" tahta mekaniği ve sabit.

## Dokunulan dosyalar

- `Assets/Scripts/SpecialStrikes.cs` (yeni)
- `Assets/Scripts/Levels/LevelData.cs` — üç sayaç alanı
- `Assets/Scripts/PotionBoard.cs` — referans, `Width`, takas engeli, dokunuş
  yönlendirmesi, `RunStrike`

## Doğrulama

Elle oynayarak: seçim aç/kapa, vuruşlar arası geçiş, seçiliyken takasın
çalışmaması, üç desenin doğru hücreleri kırması, sayaçların azalması, sıfırda
butonun kapanması, hamle sayacının değişmemesi, vuruşun içindeki bombanın
zincirlemesi, refill sürerken vuruş.
