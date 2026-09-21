using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Match3.Gameplay.Potions;
using Match3.Gameplay.Session;

namespace Match3.Gameplay.Board
{
    // Bomba/roket zincirleri, süper bomba ve çift roket. Bir zinciri bitene kadar
    // çalıştırır (hücreleri temizler, puan verir, taşları serbest bırakır); tahtayı
    // doldurmak PotionBoard'un işidir.
    public sealed class SpecialChain : MonoBehaviour
    {
        // İki bomba birleşirken ikincisinin kaybolması için takas başladıktan sonra
        // beklenen süre. Takas hareketi 0.12 sn sürüyor; bunun altında tutulmalı.
        [SerializeField, Min(0f)] private float mergedBombHideDelay = 0.05f;


        // Süper bomba patlayıp taşları temizledikten sonra, üstteki taşlar düşmeye
        // başlamadan önce beklenen süre.
        [SerializeField, Min(0f)] private float explosionSettleDelay = 0.25f;


        // Süper bomba patlaması merkezden dışa doğru halka halka ilerler;
        // iki halka arasında beklenen süre.
        [SerializeField, Min(0f)] private float superBombRingDelay = 0.06f;


        // İki roket birleşince oynayan DuableRocket animasyonunun süresi;
        // patlama bu süre dolunca başlar.
        [SerializeField, Min(0f)] private float doubleRocketDelay = 1f;


        // Roket parçalarının satır boyunca ilerleme hızı (birim/sn).
        // 0 olamaz: parçalar ilerlemezse süpürme döngüsü hiç bitmez.
        [SerializeField, Min(0.1f)] private float rocketSpeed = 12f;


        // Her bomba/roket patlaması bu kadar puan verir (zincir dahil).
        [SerializeField] private int bombPoints = 10;

        private BoardGrid grid;
        private BoardGeometry geometry;
        private BoardEffects effects;
        private Action<Potion> releasePotion;

        // releasePotion: temizlenen taşı hedeflere sayıp havuza döndüren geri çağrı.
        public void Initialize(BoardGrid grid, BoardGeometry geometry, BoardEffects effects, Action<Potion> releasePotion)
        {
            this.grid = grid;
            this.geometry = geometry;
            this.effects = effects;
            this.releasePotion = releasePotion;
        }

        // Zincirdeki bir halka: hangi hücre, hangi tür ve (roketse) hangi taş.
        // Roketin uçan parçaları taşın çocuğu olduğu için taş referansı taşınır;
        // bombada null yeterli, patlama yalnızca konumu kullanır.
        private readonly struct SpecialTrigger
        {
            public readonly Vector2Int position;
            public readonly PotionType type;
            public readonly Potion potion;

            public SpecialTrigger(Vector2Int position, PotionType type, Potion potion)
            {
                this.position = position;
                this.type = type;
                this.potion = potion;
            }
        }

        // Bir patlama zincirinin (ya da eşleşme temizliğinin) işlem-yerel durumu.
        // Bomba ve roket aynı zincirden geçer, biri diğerini tetikleyebilir; zincir
        // bitince tahta YALNIZCA BİR KEZ doldurulur. Referans tipi, çünkü zincirin
        // coroutine'leri aynı nesneyi paylaşır; cascade sırasında dokunmaya izin
        // verdiğimiz için birden fazla zincir aynı anda çalışabilir, statik olamaz.
        public class ChainContext
        {
            // Tetiklenmiş özel taş hücreleri: aynı hücre ikinci kez tetiklenmez.
            public readonly HashSet<Vector2Int> triggered = new();

            // Hâlâ süren patlama/süpürme/kırılma sayısı; sıfırlanınca refill başlar.
            public int running;
        }

        // Bomba ve roket aynı zincirden geçer, biri diğerini tetikleyebilir.
        // Her halka KENDİ coroutine'inde ve TEMAS ANINDA başlar — sıralı beklense
        // yoldaki bomba, süpürme tahtayı baştan sona geçene kadar patlamazdı.
        // Hepsi bitince tahta yalnızca bir kez doldurulur.
        // İki roket takas edilince artı roket olur. Birleşme animasyonundan sonra
        // İKİ roket de merkezden süpürür: biri satırı, diğeri sütunu. Dört uçan
        // parça buradan çıkıyor, her rokette ikişer tane var. İkinci roketi yok
        // etmek yerine ayakta tutmamızın sebebi bu.
        public IEnumerator DoubleRocketExplode(Potion horizontal, Potion vertical)
        {
            Vector2Int center = new Vector2Int(horizontal.XIndex, horizontal.YIndex);

            // DuableRocket yalnızca Rocket parçasının Animator'ünde tanımlı;
            // taşın diğer Animator'lerinde bu state yok, HasState onları eler.
            int mergeState = Animator.StringToHash("DuableRocket");

            List<Animator> mergeAnimators = new();

            foreach (Animator animator in horizontal.GetComponentsInChildren<Animator>(true))
            {
                if (animator.isActiveAndEnabled && animator.HasState(0, mergeState))
                {
                    mergeAnimators.Add(animator);
                }
            }

            foreach (Animator animator in mergeAnimators) animator.Play(mergeState, 0, 0f);

            // Birleşme sesi DuableRocket animasyonuyla aynı karede bir kez başlar.
            effects.PlayDoubleRocketMerge();

            // Bombadan farklı olarak ikinci roket animasyonla AYNI karede gizlenir:
            // DuableRocket klibi ikizi kendisi çiziyor, ikisi birden dursa üç roket
            // görünürdü.
            vertical.gameObject.SetActive(false);

            // Birleşme efekti de aynı karede, roketlerin buluştuğu hücrede.
            // Referans saklanıyor: roket ateşlenince kapatılması gerekiyor,
            // prefab döngüde olduğu için kendi kendine durmuyor.
            ParticleSystem mergeEffect = null;

            if (effects.DoubleRocketParticles != null)
            {
                Vector3 mergePosition = geometry.CellToWorld(center);
                mergePosition.z = -0.1f;

                mergeEffect = effects.SpawnBoardVfx(effects.DoubleRocketParticles, mergePosition, Quaternion.identity);
            }

            yield return new WaitForSeconds(doubleRocketDelay);

            // Birleşme bitti, roket ateşleniyor: birleşme efekti kapanır.
            if (mergeEffect != null) mergeEffect.gameObject.SetActive(false);

            // Tek efekt, tek ses, tek puan: artının iki kolu da aynı merkezden çıkıyor.
            effects.PlayExplosion();
            GameManager.Instance.AddPoints(bombPoints);

            if (effects.RocketFireParticles != null)
            {
                Vector3 firePosition = geometry.CellToWorld(center);
                firePosition.z = -0.1f;

                effects.SpawnBoardVfx(effects.RocketFireParticles, firePosition, Quaternion.identity);
            }

            // İkinci roket takas sonrası komşu hücrede duruyor. Hücresi elle
            // boşaltılmazsa artı oradan geçerken onu bağımsız bir roket sanıp
            // tetikliyor ve fazladan bir süpürme başlıyor.
            grid.Clear(new Vector2Int(vertical.XIndex, vertical.YIndex));

            // Merkeze taşınır ve dikey eksene çevrilir. Hayatta kalan roket de
            // yataya sabitlenir, çünkü oyuncu iki dikey roketi de birleştirebilir.
            Vector2 centerWorld = geometry.CellToWorld(center);

            vertical.transform.position =
                new Vector3(centerWorld.x, centerWorld.y, vertical.transform.position.z);

            vertical.SetIndices(center.x, center.y);

            vertical.gameObject.SetActive(true);
            vertical.BecomeRocket(vertical: true);

            // Hayatta kalan roket takasta iki hücrenin ortasında durdu; uçan
            // parçalar hücre merkezinden çıkmalı, yoksa temizlenen hücrelerle
            // yarım hücre kayık giderler.
            horizontal.transform.position =
                new Vector3(centerWorld.x, centerWorld.y, horizontal.transform.position.z);

            // Yataya sabitlenir, çünkü oyuncu iki dikey roketi de birleştirebilir.
            horizontal.BecomeRocket(vertical: false);

            // Birleşme klibi döngüde; Animator varsayılan state'e döndürülmezse
            // taş havuzdan yeniden roket olarak çıktığında animasyon kendiliğinden
            // oynar. Split aynı karede olduğu için görsel bir sıçrama olmuyor.
            foreach (Animator animator in mergeAnimators) animator.Rebind();

            // İki süpürme aynı anda. Ortak triggered seti sayesinde kesişimdeki
            // bir bomba iki kez tetiklenmez.
            ChainContext chain = new();
            chain.triggered.Add(center);
            chain.running += 2;

            StartCoroutine(RunSweep(
                new SpecialTrigger(center, PotionType.Rocket, horizontal), chain));

            StartCoroutine(RunSweep(
                new SpecialTrigger(center, PotionType.Rocket, vertical), chain));

            // Zincire giren bomba ve roketler de bitsin.
            yield return new WaitUntil(() => chain.running == 0);
        }

        // Birleşen ikinci taşı gecikmeli gizler. Ayrı coroutine, çünkü çağıran
        // rutinin akışını bekletmemeli.
        private IEnumerator HideAfter(Potion potion, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (potion != null) potion.gameObject.SetActive(false);
        }

        public IEnumerator ExplodeChain(Potion first)
        {
            ChainContext chain = new();

            TriggerSpecial(new SpecialTrigger(
                new Vector2Int(first.XIndex, first.YIndex), first.PotionType, first), chain);

            yield return new WaitUntil(() => chain.running == 0);
        }

        // Zincire yeni bir halka ekler ve hemen başlatır.
        private void TriggerSpecial(SpecialTrigger trigger, ChainContext chain)
        {
            // Aynı hücre ikinci kez tetiklenmesin.
            if (!chain.triggered.Add(trigger.position)) return;

            chain.running++;

            if (trigger.type == PotionType.Rocket)
            {
                // Ateşleme efekti, sesi ve puanı roket başına BİR kez burada; süpürmenin
                // kendisi SweepLine'da. Çift roket buradan geçmez: kendi efektini
                // bir kez oynatıp iki süpürmeyi doğrudan başlatır.
                bool vertical = trigger.potion != null && trigger.potion.IsVerticalRocket;

                effects.PlayExplosion();
                GameManager.Instance.AddPoints(bombPoints);

                if (effects.RocketFireParticles != null)
                {
                    // CellToWorld Vector2 döner, z sıfır kalır. Efekt taşların önünde
                    // dursun diye kameraya doğru 0.1 çekiliyor.
                    Vector3 firePosition = geometry.CellToWorld(trigger.position);
                    firePosition.z = -0.1f;

                    effects.SpawnBoardVfx(effects.RocketFireParticles, firePosition,
                        vertical ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.identity);
                }

                StartCoroutine(RunSweep(trigger, chain));
                return;
            }

            // Bomba anlık: beklemeye gerek yok.
            BlastAround(trigger.position, chain);
            chain.running--;
        }

        private IEnumerator RunSweep(SpecialTrigger trigger, ChainContext chain)
        {
            yield return SweepLine(trigger, chain);

            chain.running--;
        }

        // Bomba: merkez dahil 3x3 alanı temizler.
        public void BlastAround(Vector2Int center, ChainContext chain)
        {
            effects.SpawnBoardVfx(effects.ExplodingParticles, geometry.CellToWorld(center), Quaternion.identity);
            effects.PlayExplosion();

            // Zincirdeki her patlama ayrı puan verir.
            GameManager.Instance.AddPoints(bombPoints);

            for (int xIndex = center.x - 1; xIndex <= center.x + 1; xIndex++)
            {
                for (int yIndex = center.y - 1; yIndex <= center.y + 1; yIndex++)
                {
                    ClearCell(new Vector2Int(xIndex, yIndex), chain);
                }
            }
        }

        // Roket: iki parça merkezden dışa sabit hızla uçar, üzerinden geçtikleri
        // hücreyi temizler. Yatay roket satır boyunca (x), dikey roket sütun boyunca
        // (y) gider; görsel aynı, yalnızca eksen değişir. Parçalar taşın çocuğu
        // olduğu için taşın hücresi hemen boşaltılır ama taş, iz sönene kadar
        // havuza yollanmaz (FlyOutAndPool).
        private IEnumerator SweepLine(SpecialTrigger trigger, ChainContext chain)
        {
            Potion rocket = trigger.potion;
            bool vertical = rocket != null && rocket.IsVerticalRocket;

            // Eksen: yatayda sütun indeksi (x) değişir ve sınır genişlik; dikeyde
            // satır indeksi (y) değişir ve sınır görünür yükseklik (8).
            Vector2Int step = vertical ? Vector2Int.up : Vector2Int.right;
            Vector3 worldStep = vertical ? Vector3.up : Vector3.right;
            int limit = vertical ? BoardDefinition.VisibleHeight : BoardDefinition.VisibleWidth;
            int origin = vertical ? trigger.position.y : trigger.position.x;

            grid.Clear(trigger.position);

            // "plus" sağa ya da yukarı, "minus" sola ya da aşağı giden parça.
            Transform plus = rocket != null ? rocket.RocketRight : null;
            Transform minus = rocket != null ? rocket.RocketLeft : null;

            if (rocket != null)
            {
                // Roket gövdesinin kendi child trail'leri de Potion altında
                // kalır; Cannon dönüşünde çıkmış parçacıkların kopmaması gerekir.
                BoardEffects.SetParticleSimulationLocal(rocket.gameObject);
                rocket.SplitRocket();
            }

            Vector3 plusStart = plus != null ? plus.position : Vector3.zero;
            Vector3 minusStart = minus != null ? minus.position : Vector3.zero;

            int nextPlus = origin + 1;
            int nextMinus = origin - 1;
            float travelled = 0f;

            while (nextPlus < limit || nextMinus >= 0)
            {
                travelled += rocketSpeed * Time.deltaTime;

                if (plus != null) plus.position = plusStart + worldStep * travelled;
                if (minus != null) minus.position = minusStart - worldStep * travelled;

                // Parçalar kaç hücre ilerledi? Geçilen her hücre temizlenir.
                int reached = Mathf.FloorToInt(travelled / geometry.CellSize);

                while (nextPlus < limit && nextPlus <= origin + reached)
                {
                    ClearCell(trigger.position + step * (nextPlus - origin), chain);
                    nextPlus++;
                }

                while (nextMinus >= 0 && nextMinus >= origin - reached)
                {
                    ClearCell(trigger.position + step * (nextMinus - origin), chain);
                    nextMinus--;
                }

                yield return null;
            }

            if (rocket != null)
            {
                effects.SpawnDestroyParticle(rocket);

                // Beklemeden başlatılır: zincirin geri kalanı ve refill,
                // iz sönerken devam etsin, tahta duraklamasın.
                StartCoroutine(FlyOutAndPool(rocket, plus, minus, worldStep));
            }
        }

        // Parçalar tahtayı geçti ama izleri (World uzayında) hattın üstünde duruyor.
        // Taş hemen kapansa parçacıklar tek karede silinir; bunun yerine emisyon
        // kesilir, parçalar aynı eksende ekran dışına uçmaya devam eder ve son
        // parçacık sönünce taş havuza döner. Süre parçacıkların gerçek ömrüne bağlı.
        private IEnumerator FlyOutAndPool(Potion rocket, Transform plus, Transform minus, Vector3 worldStep)
        {
            // Yalnızca AKTİF sistemler: kapalı duran bomba kıvılcımı dahil olmaz.
            ParticleSystem[] trails = rocket.GetComponentsInChildren<ParticleSystem>();

            foreach (ParticleSystem trail in trails)
            {
                if (trail != null)
                {
                    trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            // Emniyet: ölmeyen bir parçacık ayarı taşı sonsuza dek havuzdan uzak tutmasın.
            float safety = 3f;

            while (AnyAlive(trails) && (safety -= Time.deltaTime) > 0f)
            {
                Vector3 delta = worldStep * (rocketSpeed * Time.deltaTime);

                if (plus != null) plus.position += delta;
                if (minus != null) minus.position -= delta;

                yield return null;
            }

            releasePotion(rocket);
        }

        private static bool AnyAlive(ParticleSystem[] systems)
        {
            foreach (ParticleSystem system in systems)
            {
                // CFXR bazı efekt objelerini son parçacık söndüğünde Destroy ediyor.
                // Unity'nin "fake null" kontrolü olmadan IsAlive çağrısı coroutine'i
                // yarıda keser ve roket havuza hiç dönmez.
                if (system != null && system.IsAlive(true)) return true;
            }

            return false;
        }

        // Tek hücre temizler; özel taş bulursa zincire ekler.
        // Roket havuza YOLLANMAZ — uçan parçaları için taşın yaşaması gerekiyor,
        // onu kuyruktan çıkınca SweepLine havuza döndürür.
        public void ClearCell(Vector2Int cell, ChainContext chain)
        {
            if (!BoardDefinition.IsPlayable(cell)) return;

            Potion potion = grid.PotionAt(cell);

            if (potion == null) return;

            // Havuz metodu tipi orijinaline döndüreceği için önce kaydedilir.
            PotionType type = potion.PotionType;

            // Roketin hücresini SweepLine boşaltır.
            if (type == PotionType.Rocket)
            {
                TriggerSpecial(new SpecialTrigger(cell, type, potion), chain);
                return;
            }

            grid.Clear(cell);

            effects.SpawnDestroyParticle(potion);
            releasePotion(potion);

            if (type == PotionType.Bomb)
            {
                TriggerSpecial(new SpecialTrigger(cell, type, null), chain);
            }
        }

        public IEnumerator SuperBombExplode(Potion _targetPotion, Potion _mergedPotion)
        {
            Vector2Int bombPosition = new Vector2Int(_targetPotion.XIndex, _targetPotion.YIndex);

            // İki bomba birbirine doğru geldi; birleşme iki hücrenin ORTASINDA
            // görünmeli, hayatta kalan bombanın hücresinde değil. Patlama hâlâ
            // hücreye çakılı, yalnızca görsel merkez kayıyor.
            Vector2 mergePoint = _mergedPotion != null
                ? (geometry.CellToWorld(bombPosition) +
                   geometry.CellToWorld(new Vector2Int(_mergedPotion.XIndex, _mergedPotion.YIndex))) * 0.5f
                : geometry.CellToWorld(bombPosition);

            _targetPotion.transform.position =
                new Vector3(mergePoint.x, mergePoint.y, _targetPotion.transform.position.z);

            // Gölge yalnızca burada görünür ve prefabda KAPALI durur. Kapalı bir
            // GameObject'te Animator başlatılmadığı için HasState hep false döner;
            // bu yüzden gölgeyi döngüden önce, adıyla açıyoruz.
            if (_targetPotion.BombShadow != null) _targetPotion.BombShadow.SetActive(true);

            // Bomba ve gölgesi ayrı Animator'lerde duruyor, ikisi de aynı karede
            // başlamalı. SuperBomb state'i yalnızca bomba ve gölge controller'larında
            // var; HasState olmadan bu döngü roketin Animator'lerini de açıyordu ve
            // bombanın altında roket beliriyordu.
            int superState = Animator.StringToHash("SuperBomb");

            foreach (Animator animator in _targetPotion.GetComponentsInChildren<Animator>(true))
            {
                // HasState kapalı bir Animator'de "not playing an AnimatorController"
                // uyarısı basıyor ve hep false dönüyor. Gölgeyi zaten yukarıda açtık,
                // geriye kalan kapalılar (roket parçaları) sessizce eleniyor.
                if (!animator.isActiveAndEnabled) continue;

                if (!animator.HasState(0, superState)) continue;

                animator.Play(superState, 0, 0f);
            }

            // Fünyenin sesi SuperBomb animasyonuyla aynı karede ve yalnızca bir kez başlar.
            // Patlama anındaki explodingClip ayrıca aşağıda çalmaya devam eder.
            effects.PlayBombFuse();

            // İkinci bomba animasyon BAŞLADIKTAN kısa süre sonra kaybolur; iki
            // bombanın üst üste gelip birleştiği an görünsün diye.
            if (_mergedPotion != null)
            {
                StartCoroutine(HideAfter(_mergedPotion, mergedBombHideDelay));
            }

            // Patlayan bomba diğer bombaların ve taşların önünde çizilsin.
            BombMaskBinder maskBinder = _targetPotion.GetComponentInChildren<BombMaskBinder>(true);

            if (maskBinder != null) maskBinder.LockToFront();

            // Partikülü animasyona bırakmıyoruz: obje zaten aktifse OnEnable tetiklenmez
            // ve Play On Awake çalışmaz. Baştan başlatmak için elle tetikliyoruz.
            //
            // Arama BOMBANIN altında: taşın tamamında arasak hiyerarşide daha önce
            // gelen roket alevini bulup onu açardı.
            GameObject bombObject = _targetPotion.BombObject;

            if (bombObject != null)
            {
                BoardEffects.SetParticleSimulationLocal(bombObject);

                // Kıvılcımın kendi çocukları da olabiliyor; hepsi baştan başlasın.
                foreach (ParticleSystem sparks in bombObject.GetComponentsInChildren<ParticleSystem>(true))
                {
                    sparks.gameObject.SetActive(true);
                    sparks.Clear(true);
                    sparks.Play(true);
                }
            }

            yield return new WaitForSeconds(1.82f);


            Vector2 explosionPosition = mergePoint;

            effects.SpawnBoardVfx(effects.SuperExplodingParticles, explosionPosition, Quaternion.identity);
            effects.PlayExplosion();

            GameManager.Instance.AddPoints(bombPoints);

            // Patlama merkezden dışa doğru halka halka ilerler: önce merkez hücre,
            // sonra onu saran kare çerçeve, sonra bir sonraki... Halkalar arasındaki
            // kısa bekleme şok dalgası hissini verir.
            // Yarıçap tek yerde: ring'in üst sınırı. 3 = 7x7.
            for (int ring = 0; ring <= 3; ring++)
            {
                // Bekleme halkalar ARASINDA; son halkadan sonra fazladan duraklama olmasın.
                if (ring > 0) yield return new WaitForSeconds(superBombRingDelay);

                for (int xIndex = bombPosition.x - ring; xIndex <= bombPosition.x + ring; xIndex++)
                {
                    for (int yIndex = bombPosition.y - ring; yIndex <= bombPosition.y + ring; yIndex++)
                    {
                        // Yalnızca bu halkanın çerçevesi; içi önceki turlarda temizlendi.
                        int distance = Mathf.Max(Mathf.Abs(xIndex - bombPosition.x),
                                                 Mathf.Abs(yIndex - bombPosition.y));

                        if (distance != ring)
                        {
                            continue;
                        }

                        if (!BoardDefinition.IsPlayable(new Vector2Int(xIndex, yIndex)))
                        {
                            continue;
                        }
                        Potion potion = grid.PotionAt(new Vector2Int(xIndex, yIndex));
                        if (potion == null)
                        {
                            continue;
                        }
                        grid.Clear(new Vector2Int(xIndex, yIndex));

                        // Taşın kendi kırılma efekti — normal eşleşmedekiyle aynı.
                        // Havuza dönmeden ÖNCE, tipi hâlâ doğruyken çağrılmalı.
                        effects.SpawnDestroyParticle(potion);

                        releasePotion(potion);
                    }
                }
            }

            // Patlama görülsün: taşlar temizlendikten sonra tahta bir an boş kalır,
            // düşüş hemen başlamaz.
            yield return new WaitForSeconds(explosionSettleDelay);
        }
    }
}
