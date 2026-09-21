using System.Collections;
using CartoonFX;
using UnityEngine;

namespace Match3.Gameplay.Potions
{
    public class Potion : MonoBehaviour
    {

        [SerializeField] private PotionType potionType;   // prefab'da atanır; serialize adı değişmez
        private PotionType originalPotionType;

        public PotionType PotionType { get => potionType; set => potionType = value; }
        public int XIndex { get; private set; }
        public int YIndex { get; private set; }

        // Hareket bitene kadar true; tahta bu taşı eşleşmeye ve refill'e katmaz.
        public bool IsMoving { get; private set; }

        private float swapSpeed = .12f;

        [Header("Düşüş hareketi")]
        [Tooltip("Düşüşün BAŞLANGIÇ hızı (birim/sn). Bir hücre 0.575 birim; bir hücrelik düşüş neredeyse bu hızda biter.")]
        [SerializeField, Min(0.01f)] private float fallSpeed = 13f;

        [Tooltip("Düşerken saniyede kazanılan hız. Uzun düşüşler hızlanır, bir hücrelik düşüşte fark edilmez.")]
        [SerializeField, Min(0f)] private float fallGravity = 30f;

        [Tooltip("Düşüşün aşamayacağı üst hız.")]
        [SerializeField, Min(0.01f)] private float maxFallSpeed = 20f;

        [Tooltip("Üst hızda Y çarpanı; X ters oranda incelir. Hız arttıkça esneme büyür, yere değince sıfırlanır.")]
        [SerializeField, Min(1f)] private float fallStretch = 1.08f;

        [SerializeField] private GameObject selectedVisual;
        [SerializeField] private GameObject bomb;

        // Bombanın kıvılcımlarını PotionBoard elle yeniden başlatıyor; aramanın
        // taşın tamamında değil, YALNIZCA bombanın altında yapılması gerekiyor.
        public GameObject BombObject => bomb;

        // Bombanın yere düşen gölgesi. bomb'un child'ı DEĞİL (onun animasyonlu
        // transformunu miras almasın diye), o yüzden aç/kapa işi elle yapılır.
        // Yalnızca süper bomba patlarken görünür; onu PotionBoard açar, burası
        // her durum değişiminde kapatarak temiz bir başlangıç garantiler.
        [SerializeField] private GameObject bombShadow;

        // Süper bomba patlarken PotionBoard açar. Kapalı bir Animator'de
        // HasState çalışmadığı için gölgenin adıyla açılması gerekiyor.
        public GameObject BombShadow => bombShadow;

        // Roket: gövde ile sağa/sola uçan iki parça. Parçalar taşın çocuğu olduğu
        // için taş, süpürme bitene kadar havuza yollanmaz.
        [SerializeField] private GameObject rocket;
        [SerializeField] private GameObject rocketRight;
        [SerializeField] private GameObject rocketLeft;

        // Parçaların prefabdaki yeri. Süpürme sırasında dünya konumları değiştiği
        // için havuza dönerken buraya geri konur; yoksa taş yeniden kullanıldığında
        // parçalar tahtanın ortasında bir yerde kalır.
        private Vector3 rocketRightHome;
        private Vector3 rocketLeftHome;

        // Prefabdaki rotasyonlar. Dikey roket bunların üstüne 90° ekler; Left'in
        // prefabdaki 180°'si (aynalama) korunmalı, o yüzden üstüne yazılmaz.
        private Quaternion rocketHomeRot;
        private Quaternion rocketRightHomeRot;
        private Quaternion rocketLeftHomeRot;

        // Dikey roket sütun temizler; PotionBoard ateşlerken buna bakar.
        public bool IsVerticalRocket { get; private set; }

        // Taşın kendi sprite'ı, potionVisual adlı ÇOCUKTA. Kök yalnızca konum ve
        // mantık taşır; sprite ile iniş animasyonunun Animator'ü çocukta durur.
        // Böylece kodun köke yazdığı ölçek (düşüş esnemesi, eşleşme küçülmesi)
        // ile Animator'ün çocuğa yazdığı ölçek üst üste yazılmaz, çarpılır.
        //
        // Bombaya dönüşünce gizlenir: bombanın saydam kenarlarından alttaki renk
        // sızmasın diye. Boş bırakılırsa ilk bulunan çocuk sprite'ı alınır.
        [Tooltip("Taşın sprite'ını taşıyan çocuk. Boşsa hiyerarşideki ilk SpriteRenderer.")]
        [SerializeField] private SpriteRenderer potionVisual;

        // İniş animasyonu potionVisual üzerindeki Animator'de. Kod yalnızca
        // state'i tetikler; ölçeği ve konumu klip yönetir, kökle çakışmaz.
        [Tooltip("potionVisual Animator'ündeki iniş state'inin adı.")]
        [SerializeField] private string landingState = "PotionLanding";

        private Animator visualAnimator;
        private int landingStateHash;

        [Header("Takas dumanı")]
        [SerializeField] private GameObject swapSmoke;

        [Tooltip("Dumanın taşı yakalama hızı. Düşük değer = taş daha çok öne geçer.")]
        [SerializeField, Min(0.1f)] private float smokeFollow = 14f;

        [Tooltip("Duman hareket YÖNÜNDE bu kadar uzar, dik eksende aynı oranda incelir.")]
        [SerializeField, Min(1f)] private float smokeGrow = 1.6f;

        [Tooltip("Dumanın taşın arkasında kalma mesafesi (taşın local birimi).")]
        [SerializeField, Min(0f)] private float smokeTrail = 1f;

        // Prefabdaki duruş. Takasta dünya konumu elle sürüldüğü için
        // bitişte buraya geri konur.
        private Vector3 swapSmokeHome;
        private Vector3 swapSmokeHomeScale;
        private Quaternion swapSmokeHomeRot;

        // Takas yönüne göre hesaplanan dönüş; duman hareketin tersinde kalır.
        private Quaternion swapSmokeRot = Quaternion.identity;

        [Tooltip("Eşleşen taşın kırılmadan önce sıfıra küçülme süresi.")]
        [SerializeField, Min(0f)] private float matchShrinkDuration = 0.09f;

        // Prefabdaki ölçek. Küçülme hep bunun üzerine uygulanır ki havuzdan
        // çıkan taş bir öncekinin ölçeğini taşımasın.
        private Vector3 baseScale;

        // Çalışan hareket coroutine'i. Yeni hedef verilmeden önce durdurulur.
        private Coroutine moveRoutine;

        private void Awake()
        {
            originalPotionType = potionType;
            baseScale = transform.localScale;

            if (rocketRight != null) rocketRightHome = rocketRight.transform.localPosition;
            if (rocketLeft != null) rocketLeftHome = rocketLeft.transform.localPosition;

            if (swapSmoke != null)
            {
                swapSmokeHome = swapSmoke.transform.localPosition;
                swapSmokeHomeScale = swapSmoke.transform.localScale;
                swapSmokeHomeRot = swapSmoke.transform.localRotation;
            }

            if (rocket != null) rocketHomeRot = rocket.transform.localRotation;
            if (rocketRight != null) rocketRightHomeRot = rocketRight.transform.localRotation;
            if (rocketLeft != null) rocketLeftHomeRot = rocketLeft.transform.localRotation;

            if (potionVisual == null) potionVisual = GetComponentInChildren<SpriteRenderer>();
            if (potionVisual != null) visualAnimator = potionVisual.GetComponent<Animator>();

            landingStateHash = Animator.StringToHash(landingState);

            // Potion kökü havuzdan tekrar kullanılıyor. Cartoon FX'in varsayılan
            // Destroy davranışı, roket izi durunca child efekt objesini kalıcı olarak
            // siliyor; sonraki kullanımda hem eksik referans hem de yarım roket kalıyor.
            foreach (CFXR_Effect effect in GetComponentsInChildren<CFXR_Effect>(true))
            {
                effect.clearBehavior = CFXR_Effect.ClearBehavior.None;
            }
        }

        public void SetSelectedVisual(bool isPressing)
        {
            // Özel taşlarda (bomba, roket) çerçeve gösterilmez: çerçeve mücevher
            // boyutunda ve özel görselin arkasında kalıyor.
            bool isSpecial = potionType == PotionType.Bomb || potionType == PotionType.Rocket;

            if (selectedVisual != null) selectedVisual.SetActive(isPressing && !isSpecial);
        }


        public void SetIndices(int x, int y)
        {
            XIndex = x;
            YIndex = y;
        }

        // Takas hareketi; taş arkasında duman izi bırakır. Süper eşleşmedeki
        // birleşme MoveToTargetAtSpeed kullanır, orada iz yok.
        public void MoveToTarget(Vector2 targetPos)
        {
            StartMove(targetPos, swapSpeed, showSmoke: true);
        }

        public void MoveToTargetAtSpeed(
            Vector2 targetPos,
            float unitsPerSecond,
            float minDuration,
            float maxDuration)
        {
            float distance = Vector2.Distance(transform.position, targetPos);
            float duration = distance / Mathf.Max(0.01f, unitsPerSecond);
            StartMove(targetPos, Mathf.Clamp(duration, minDuration, maxDuration));
        }

        public void BecomeBomb()
        {
            potionType = PotionType.Bomb;
            bomb.SetActive(true);

            if (bombShadow != null) bombShadow.SetActive(false);

            // Bomba açıkken taşın görseli kapalı; ClearSpecial geri açar.
            if (potionVisual != null) potionVisual.enabled = false;

            // Durum değişirken açık kalmış seçim çerçevesi kalmasın.
            if (selectedVisual != null) selectedVisual.SetActive(false);
        }

        // BecomeBomb'un roket karşılığı. Uzun eşleşmede korunan taş buraya girer:
        // yatay eşleşme yatay roket (satır), dikey eşleşme dikey roket (sütun).
        // Dikey roket ayrı bir görsel değil, aynı üç obje 90° döndürülmüş hali —
        // üçü de taşın merkezinde ve pivot'ları ortada, dönüş yerinde kalır.
        public void BecomeRocket(bool vertical)
        {
            potionType = PotionType.Rocket;
            IsVerticalRocket = vertical;

            Quaternion turn = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;

            if (rocket != null)
            {
                rocket.SetActive(true);
                rocket.transform.localRotation = turn * rocketHomeRot;
            }

            if (rocketRight != null)
            {
                rocketRight.SetActive(false);
                rocketRight.transform.localRotation = turn * rocketRightHomeRot;
            }

            if (rocketLeft != null)
            {
                rocketLeft.SetActive(false);
                rocketLeft.transform.localRotation = turn * rocketLeftHomeRot;
            }

            if (potionVisual != null) potionVisual.enabled = false;
            if (selectedVisual != null) selectedVisual.SetActive(false);
        }

        // Roket ateşlenir: gövde kapanır, iki parça açılır.
        // Parçaları satır boyunca PotionBoard sürükler.
        public void SplitRocket()
        {
            if (rocket != null) rocket.SetActive(false);
            if (rocketRight != null) rocketRight.SetActive(true);
            if (rocketLeft != null) rocketLeft.SetActive(true);
        }

        public Transform RocketRight => rocketRight != null ? rocketRight.transform : null;
        public Transform RocketLeft => rocketLeft != null ? rocketLeft.transform : null;

        // Havuza dönerken çağrılır. Taş hangi özel tipteyse yalnızca onu kapatmak
        // yetmez — diğerinin görseli açık kalıp bir sonraki kullanımda ortaya çıkar.
        public void ClearSpecial()
        {
            potionType = originalPotionType;

            if (bomb != null) bomb.SetActive(false);
            if (bombShadow != null) bombShadow.SetActive(false);
            IsVerticalRocket = false;

            StopSwapSmoke();

            if (rocket != null)
            {
                rocket.SetActive(false);
                rocket.transform.localRotation = rocketHomeRot;
            }

            if (rocketRight != null)
            {
                rocketRight.SetActive(false);
                rocketRight.transform.localPosition = rocketRightHome;
                rocketRight.transform.localRotation = rocketRightHomeRot;
            }

            if (rocketLeft != null)
            {
                rocketLeft.SetActive(false);
                rocketLeft.transform.localPosition = rocketLeftHome;
                rocketLeft.transform.localRotation = rocketLeftHomeRot;
            }

            if (potionVisual != null) potionVisual.enabled = true;
            if (selectedVisual != null) selectedVisual.SetActive(false);
        }

        public void MoveToDown(Vector2 targetPos, float startDelay = 0f)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);

            transform.localScale = baseScale;
            moveRoutine = StartCoroutine(FallCoroutine(targetPos, startDelay));
        }

        // Taş hareket hâlindeyken yeni bir hedef alabiliyor (cascade sürerken yapılan
        // takas gibi). İki MoveCoroutine aynı anda transform'a yazar ve hangisi önce
        // biterse IsMoving'i temizler — bekleyen kod yanlış anda devam eder.
        // Bu yüzden yeni hareket başlamadan önce eskisi kesilir.
        private void StartMove(Vector2 targetPos, float duration, bool showSmoke = false)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);

            transform.localScale = baseScale;
            moveRoutine = StartCoroutine(MoveCoroutine(targetPos, duration, showSmoke));
        }

        // Havuza dönerken obje kapanır ve coroutine'ler durur; elde kalan referans
        // temizlenmezse taş yeniden kullanıldığında geçersiz bir handle taşır.
        private void OnDisable()
        {
            moveRoutine = null;
            IsMoving = false;
            // Küçülme yarıda kalmış olabilir; havuzdan çıkarken temiz başlasın.
            transform.localScale = baseScale;
        }

        private IEnumerator MoveCoroutine(Vector2 targetPos, float duration, bool showSmoke = false)
        {
            IsMoving = true;

            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 target = new(targetPos.x, targetPos.y, startPos.z);

            if (showSmoke) StartSwapSmoke(target - startPos);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);

                transform.position = Vector3.Lerp(startPos, target, t);

                if (showSmoke) DriveSwapSmoke(t);

                yield return null;
            }
            transform.position = target;

            if (showSmoke) StopSwapSmoke();

            // Taş hedefine vardı: tahta mantığı buradan itibaren serbest.
            IsMoving = false;
            moveRoutine = null;
        }

        // Sabit süreli Lerp yerine fiziksel hız eğrisi kullanılır: uzun düşüş
        // hızlanır ama bir üst hızı geçmez. Bu, dikey roketten sonra taşların
        // sekiz hücreyi tek hücreyle aynı sürede geçmesini engeller.
        private IEnumerator FallCoroutine(Vector2 targetPos, float startDelay)
        {
            IsMoving = true;

            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            Vector3 start = transform.position;
            Vector3 target = new(targetPos.x, targetPos.y, start.z);
            float distance = Vector2.Distance(start, target);

            if (distance <= Mathf.Epsilon)
            {
                transform.position = target;
                IsMoving = false;
                moveRoutine = null;
                yield break;
            }

            // Taş fallSpeed ile başlar ve yerçekimiyle maxFallSpeed'e kadar
            // hızlanır. Bir hücrelik düşüş hızlanmaya fırsat bulamadan biter, üç
            // ve daha uzunu belirgin şekilde hızlanır. Sabit hızda uzun düşüş
            // "havada asılı" görünüyordu: göz düşen nesnenin hızlanmasını bekliyor.
            float velocity = fallSpeed;
            float travelled = 0f;

            while (travelled < distance)
            {
                velocity = Mathf.Min(maxFallSpeed, velocity + fallGravity * Time.deltaTime);
                travelled = Mathf.Min(distance, travelled + velocity * Time.deltaTime);

                transform.position = Vector3.LerpUnclamped(start, target, travelled / distance);

                // Esneme hızla büyür: bir hücrelik düşüş neredeyse hiç esnemez,
                // hızlanan uzun düşüş belirgin uzar. Sabit esneme kısa düşüşte
                // ilk karede "pat" diye açılıp kapanıyordu.
                float stretch = Mathf.Lerp(1f, fallStretch, velocity / maxFallSpeed);
                transform.localScale = new Vector3(baseScale.x / stretch, baseScale.y * stretch, baseScale.z);

                yield return null;
            }

            transform.position = target;
            transform.localScale = baseScale;

            // Yere değdi: iniş animasyonu. Animator çocuğu ezer, kök buna karışmaz.
            // Animasyon bitene kadar taş hâlâ "hareket ediyor" sayılır; böylece
            // CheckBoard tahtadaki son taşın inişi tamamlanmadan çalışmaz ve
            // eşleşen taş esnerken küçülmeye başlamaz. Sıra: düş, in, sonra kırıl.
            if (visualAnimator != null && visualAnimator.isActiveAndEnabled)
            {
                visualAnimator.Play(landingStateHash, 0, 0f);

                // Play bir sonraki karede işlenir; state bilgisi ondan sonra doğru.
                yield return null;

                while (IsPlayingLanding())
                {
                    yield return null;
                }
            }

            // Taş hücresine vardı ve inişi bitti: tahta mantığı buradan itibaren serbest.
            IsMoving = false;
            moveRoutine = null;
        }

        // Klip biter bitmez false döner: Exit üzerinden Idle'a geçtiyse state
        // değişmiştir, geçiş yoksa normalizedTime 1'i aşmıştır.
        private bool IsPlayingLanding()
        {
            AnimatorStateInfo state = visualAnimator.GetCurrentAnimatorStateInfo(0);

            return state.shortNameHash == landingStateHash && state.normalizedTime < 1f;
        }

        // Eşleşen taş kırılmadan önce hızlıca sıfıra küçülür.
        // Havuza dönerken OnDisable ölçeği zaten baseScale'e sıfırlıyor.
        public IEnumerator ShrinkOut()
        {
            // Düşüş/squash coroutine'i de ölçeğe yazıyor; ikisi çakışmasın.
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
                IsMoving = false;
            }

            float elapsed = 0f;
            Vector3 from = transform.localScale;

            while (elapsed < matchShrinkDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / matchShrinkDuration);

                transform.localScale = Vector3.Lerp(from, Vector3.zero, t);

                yield return null;
            }

            transform.localScale = Vector3.zero;
        }

        // Duman taşın ÇOCUĞU olduğu için normalde ona yapışık gider. Gecikme
        // hissi vermek adına dünya konumu her karede elle sürülüyor: hedefe
        // Lerp ile yaklaşır, yani taş hızlandıkça geride kalır.
        private void StartSwapSmoke(Vector2 direction)
        {
            if (swapSmoke == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            swapSmokeRot = Quaternion.Euler(0f, 0f, angle);

            Transform smoke = swapSmoke.transform;

            smoke.localRotation = swapSmokeRot;
            smoke.localScale = swapSmokeHomeScale;
            smoke.position = SwapSmokeAnchor();

            swapSmoke.SetActive(true);
        }

        // Dumanın peşinden koştuğu nokta: taşın GİDİŞ YÖNÜNÜN TERSİNDE,
        // smokeTrail kadar geride. swapSmokeRot +x'i hareket yönüne çevirdiği
        // için "geri" yönü basitçe Vector3.left oluyor.
        private Vector3 SwapSmokeAnchor()
        {
            Vector3 behind = swapSmokeRot * (Vector3.left * smokeTrail);

            return transform.TransformPoint(swapSmokeHome + behind);
        }

        private void DriveSwapSmoke(float t)
        {
            if (swapSmoke == null) return;

            Transform smoke = swapSmoke.transform;

            smoke.position = Vector3.Lerp(smoke.position, SwapSmokeAnchor(), smokeFollow * Time.deltaTime);

            // Duman hareket yönüne döndürülmüş durumda, yani LOCAL x = gidiş ekseni.
            // x uzar, y aynı oranda incelir: yatay takasta dünyada x büyür/y küçülür,
            // dikey takasta obje 90° dönük olduğu için tam tersi olur.
            Vector3 stretched = new Vector3(swapSmokeHomeScale.x * smokeGrow,
                                            swapSmokeHomeScale.y / smokeGrow,
                                            swapSmokeHomeScale.z);

            smoke.localScale = Vector3.Lerp(swapSmokeHomeScale, stretched, t);
        }

        private void StopSwapSmoke()
        {
            if (swapSmoke == null) return;

            swapSmoke.SetActive(false);

            Transform smoke = swapSmoke.transform;

            smoke.localPosition = swapSmokeHome;
            smoke.localRotation = swapSmokeHomeRot;
            smoke.localScale = swapSmokeHomeScale;
        }

    }

    // PotionType enum
    public enum PotionType
    {
        Red,
        Blue,
        Yellow,
        Green,
        Bomb,
        Rocket
    }
}
