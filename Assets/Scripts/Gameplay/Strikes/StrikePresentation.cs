using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Match3.Gameplay.Board;

namespace Match3.Gameplay.Strikes
{
    // Hammer / Bomb / Cannon sinematikleri: prefab'ı oynatır, hücreleri zincir
    // üzerinden temizler, zincir bitince döner. Tahtayı doldurmak ve vuruş
    // kilidi PotionBoard'un işidir; Cannon'ın nişan/iptal durumu burada yaşar.
    public sealed class StrikePresentation : MonoBehaviour
    {
        [Header("Cannon")]
        [Tooltip("Cannon'ın ekrana girdiği sol nokta. Y değeri seçilen satırdan gelir.")]
        [SerializeField] private Transform cannonLeftAnchor;
        [Tooltip("Giriş/ateş animasyonunu içeren Cannon prefabı. Muzzle referansı CannonEntryView'da atanmalıdır.")]
        [SerializeField] private CannonEntryView cannonEntryPrefab;
        [Tooltip("RocketSingleRight görselinden türetilmiş, bağımsız sağa giden top prefabı.")]
        [SerializeField] private GameObject cannonballProjectilePrefab;
        [Tooltip("Cannon hedefleme süresince kapanacak iki sağ board maskesi.")]
        [SerializeField] private GameObject cannonMaskRight;
        [SerializeField] private GameObject cannonMaskRightDuplicate;

        [Header("Hammer")]
        [Tooltip("Butonda animasyonu başlar; ayrı bir parent hedef hücreye gidip geri döner.")]
        [SerializeField] private GameObject hammerStrikePrefab;
        [SerializeField, Min(0f)] private float hammerTravelDuration = 0.2f;
        [Tooltip("Prefabın oluşturulmasından itibaren darbe zamanı (saniye).")]
        [SerializeField, Min(0f)] private float hammerImpactDelay = 0.75f;
        [Tooltip("Prefabın oluşturulmasından itibaren geri dönüşün başlayacağı zaman (saniye).")]
        [SerializeField, Min(0f)] private float hammerReturnDelay = 0.95f;
        [SerializeField, Min(0f)] private float hammerReturnDuration = 0.35f;
        [SerializeField, Min(0f)] private float hammerStrikeDuration = 1.5f;

        [Header("Bomb")]
        [Tooltip("Bomb butonundan seçilen hücreye uçacak prefab. Animator'ü yolculuk boyunca oynar.")]
        [SerializeField] private GameObject bombStrikePrefab;
        [SerializeField, Min(0f)] private float bombTravelDuration = 1.1f;
        [Tooltip("Prefab oluşturulduktan sonra etki anı. Yolculuk bitmeden etki etmez.")]
        [SerializeField, Min(0f)] private float bombImpactDelay = 1f;

        [Header("Board slide")]
        [SerializeField, Min(0f)] private float boardSlideDuration = 0.22f;
        [SerializeField, Min(0f)] private float boardReturnDuration = 0.18f;
        [Tooltip("Cannon için board'un sağa kayacağı tile sayısı. 1 = tam bir hücre genişliği.")]
        [SerializeField, Min(0f)] private float cannonBoardSlideCells = 1f;
        [SerializeField, Min(0f)] private float cannonFireDelay = 0.45f;
        [SerializeField, Min(0.1f)] private float cannonballSpeed = 12f;
        [SerializeField, Min(0f)] private float cannonballExitPadding = 0.75f;

        // Bu genel bir tahta kilidi değildir; yalnızca parent transform hareket
        // ederken yeni bir input'un dünya konumlarını bozmasını önleyen dar kapsamlı
        // Cannon sinematiği durumu.
        private bool isCannonCinematic;
        private bool isCannonAwaitingTarget;
        private bool isCannonFiring;
        private Coroutine cannonAimRoutine;

        private BoardGrid grid;
        private BoardGeometry geometry;
        private SpecialChain specialChain;
        private Transform boardPresentation;

        public bool IsCannonCinematic => isCannonCinematic;
        public bool IsAwaitingTarget => isCannonAwaitingTarget;
        public bool IsCannonFiring => isCannonFiring;
        public bool CanFireCannon => isCannonCinematic && isCannonAwaitingTarget;
        public bool HasHammer => hammerStrikePrefab != null;
        public bool HasBomb => bombStrikePrefab != null;

        // Cannon paneli (top + zincir + board dönüşü) bitince; refill'den önce.
        public event Action CannonStrikeFinished;

        public void Initialize(BoardGrid grid, BoardGeometry geometry, SpecialChain specialChain, Transform boardPresentation)
        {
            this.grid = grid;
            this.geometry = geometry;
            this.specialChain = specialChain;
            this.boardPresentation = boardPresentation;
        }

        // Cannon butonuna basıldığı anda çağrılır: board hemen sinematik duruşuna
        // geçer, fakat hedef satır henüz seçilmediği için yalnızca tek dokunuş bekler.
        public bool TryBeginCannonAim()
        {
            if (isCannonCinematic || !HasCannonPresentation()) return false;

            isCannonCinematic = true;
            isCannonFiring = false;
            SetCannonMasks(false);
            cannonAimRoutine = StartCoroutine(CannonAimRoutine());
            return true;
        }

        // Hedef henüz seçilmemişse Cannon butonuna ikinci kez basmak bu özel
        // vuruşu iptal eder. Atış başladıktan sonra iptal edilmez; o aşamada
        // görsel, zincir ve board mantığı tek bir atomik akıştır.
        public bool TryCancelCannonAim()
        {
            if (!isCannonCinematic || isCannonFiring) return false;

            if (cannonAimRoutine != null)
            {
                StopCoroutine(cannonAimRoutine);
                cannonAimRoutine = null;
            }

            isCannonAwaitingTarget = false;
            StartCoroutine(CancelCannonAimRoutine());
            return true;
        }

        private IEnumerator CancelCannonAimRoutine()
        {
            // Başlangıç konumu negatif olsa dahi transform'u tek karede atamak
            // yerine, Cannon seçilirken kullanılan geçişin tersiyle geri dön.
            yield return MovePresentationTo(geometry.PresentationHome, boardSlideDuration);

            SetCannonMasks(true);
            isCannonCinematic = false;
        }

        private IEnumerator CannonAimRoutine()
        {
            yield return new WaitUntil(() => !grid.AnyPotionMoving());

            Vector3 slideTarget = geometry.PresentationHome;
            slideTarget.x += geometry.CellSize * cannonBoardSlideCells;
            yield return MovePresentationTo(slideTarget, boardSlideDuration);

            isCannonAwaitingTarget = true;
            cannonAimRoutine = null;
        }

        // Tokmak butondan hedefe uçar, vurur, döner; darbe anında hücreler temizlenir.
        public IEnumerator PlayHammer(
            Vector2Int origin,
            IEnumerable<Vector2Int> cells,
            Transform strikeSource)
        {
            GameObject travelRoot = null;

            try
            {
                Vector3 targetPosition = geometry.CellToWorld(origin);
                targetPosition.z = -0.2f;

                Vector3 startPosition = GetStrikeStartPosition(strikeSource, targetPosition);
                // Animator yalnızca prefabın içini yönetir. Dış parent'ı hareket
                // ettirerek klip oynarken de hedef hücreye gidip geri dönebiliriz.
                travelRoot = new GameObject("HammerTravelRoot");
                travelRoot.transform.position = startPosition;
                GameObject hammerObject = Instantiate(
                    hammerStrikePrefab, startPosition, Quaternion.identity, travelRoot.transform);
                HammerStrikeView hammer = hammerObject.GetComponent<HammerStrikeView>();
                hammer.PlayStrike();
                hammer.BeginTravel(travelRoot.transform);

                SpecialChain.ChainContext chain = new();
                float travelDuration = Mathf.Max(0f, hammerTravelDuration);
                float impactTime = Mathf.Max(travelDuration, hammerImpactDelay);
                float returnTime = Mathf.Max(impactTime, hammerReturnDelay);
                float returnDuration = Mathf.Max(0f, hammerReturnDuration);
                float endTime = Mathf.Max(hammerStrikeDuration, returnTime + returnDuration);
                float elapsed = 0f;
                bool impacted = false;

                // Hareket ve darbe aynı başlangıç saatini kullanır; Animator varışta
                // tekrar başlatılmaz ve dönüşte başlangıç pozuna sıfırlanmaz.
                while (travelRoot != null)
                {
                    if (elapsed < travelDuration)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, elapsed / travelDuration);
                        hammer.SetTravelPosition(Vector3.Lerp(startPosition, targetPosition, t), 0f);
                    }
                    else if (elapsed < returnTime)
                    {
                        hammer.SetTravelPosition(targetPosition, 0f);
                    }
                    else
                    {
                        float t = returnDuration > 0f
                            ? Mathf.SmoothStep(0f, 1f, (elapsed - returnTime) / returnDuration)
                            : 1f;
                        hammer.SetTravelPosition(Vector3.Lerp(targetPosition, startPosition, t), t);
                    }

                    if (!impacted && elapsed >= impactTime)
                    {
                        impacted = true;
                        hammer.PlayImpactEffect();
                        foreach (Vector2Int cell in cells)
                        {
                            specialChain.ClearCell(cell, chain);
                        }
                    }

                    if (elapsed >= endTime) break;
                    yield return null;
                    elapsed += Time.deltaTime;
                }

                if (travelRoot != null) Destroy(travelRoot);
                travelRoot = null;

                // Roket/bomba zinciri tokmak animasyonundan bağımsız sürer. Refill
                // başlamadan önce yine de tamamını beklemek zorundayız.
                yield return new WaitUntil(() => chain.running == 0);
            }
            finally
            {
                if (travelRoot != null) Destroy(travelRoot);
            }
        }

        // Bomb ve hammer aynı uzay dönüşümünü kullanır: UI butonundaki piksel,
        // tahtadaki hedef hücrenin derinliğinde bir dünya konumuna çevrilir.
        public IEnumerator PlayBomb(
            Vector2Int origin,
            Transform strikeSource)
        {
            GameObject travelRoot = null;

            try
            {
                Vector3 targetPosition = geometry.CellToWorld(origin);
                targetPosition.z = -0.2f;

                Vector3 startPosition = GetStrikeStartPosition(strikeSource, targetPosition);
                travelRoot = new GameObject("BombTravelRoot");
                travelRoot.transform.position = startPosition;

                GameObject bombObject = Instantiate(
                    bombStrikePrefab, startPosition, Quaternion.identity, travelRoot.transform);
                BombStrikeView bomb = bombObject.GetComponent<BombStrikeView>();
                float animationDuration = bomb.PlayStrike();

                float travelDuration = Mathf.Max(0f, bombTravelDuration);
                float impactTime = Mathf.Max(travelDuration, bombImpactDelay, animationDuration);
                float elapsed = 0f;

                while (elapsed < travelDuration)
                {
                    float t = travelDuration > 0f
                        ? Mathf.SmoothStep(0f, 1f, elapsed / travelDuration)
                        : 1f;
                    travelRoot.transform.position = Vector3.Lerp(startPosition, targetPosition, t);

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                travelRoot.transform.position = targetPosition;

                // Inspector'daki etki zamanı, yolculuktan kısa tutulsa bile bomba
                // seçilen taşa varmadan hücreler temizlenmez.
                while (elapsed < impactTime)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }

                SpecialChain.ChainContext chain = new();

                // Özel Bomb'un merkezi patlaması: efekt, ses, puan ve 3x3 temizleme
                // aynı ortak akıştan gelir. ClearCell'leri burada tek tek çağırmak
                // yalnızca taşları siliyor, patlamanın kendisini hiç üretmiyordu.
                specialChain.BlastAround(origin, chain);

                if (travelRoot != null) Destroy(travelRoot);
                travelRoot = null;

                yield return new WaitUntil(() => chain.running == 0);
            }
            finally
            {
                if (travelRoot != null) Destroy(travelRoot);
            }
        }

        // Hedef seçildi: top girer, satırı süpürür, board eve döner. Refill çağıranın işi.
        public IEnumerator FireCannon(Vector2Int origin)
        {
            isCannonAwaitingTarget = false;
            isCannonFiring = true;

            GameObject cannonInstance = null;
            GameObject cannonballInstance = null;
            Vector3 homePosition = geometry.PresentationHome;
            bool presentationFinished = false;

            try
            {
                Vector3 cannonPosition = cannonLeftAnchor.position;
                cannonPosition.y = geometry.CellToWorld(origin).y;

                CannonEntryView cannonView = Instantiate(cannonEntryPrefab, cannonPosition, Quaternion.identity);
                cannonInstance = cannonView.gameObject;

                yield return new WaitForSeconds(cannonFireDelay);

                Transform muzzle = cannonView != null ? cannonView.Muzzle : null;
                if (muzzle == null)
                {
                    Debug.LogWarning("Cannon instance Muzzle referansını kaybetti; vuruş güvenli biçimde iptal edildi.", this);
                    yield break;
                }

                SpecialChain.ChainContext chain = new();

                cannonballInstance = Instantiate(cannonballProjectilePrefab, muzzle.position, Quaternion.identity);
                yield return CannonballSweep(origin.y, cannonballInstance.transform, chain);

                // Mermi geçtiyse zincirlenmiş bomba/roketlerin de bitmesini bekleriz.
                yield return new WaitUntil(() => chain.running == 0);

                yield return MovePresentationTo(homePosition, boardReturnDuration);

                // Panel sadece top, zincir ve board dönüşünü kapsar. Refill/cascade
                // normal board akışı olarak arka planda sürer; Cannon kilidi ise
                // overlap oluşmaması için coroutine sonuna kadar korunur.
                if (cannonballInstance != null)
                {
                    Destroy(cannonballInstance);
                    cannonballInstance = null;
                }

                if (cannonInstance != null)
                {
                    Destroy(cannonInstance);
                    cannonInstance = null;
                }

                SetCannonMasks(true);
                presentationFinished = true;
                CannonStrikeFinished?.Invoke();
            }
            finally
            {
                if (cannonballInstance != null) Destroy(cannonballInstance);
                if (cannonInstance != null) Destroy(cannonInstance);

                if (boardPresentation != null)
                {
                    boardPresentation.position = homePosition;
                }

                if (!presentationFinished)
                {
                    SetCannonMasks(true);
                    CannonStrikeFinished?.Invoke();
                }

                isCannonAwaitingTarget = false;
                isCannonFiring = false;
                isCannonCinematic = false;
            }
        }

        // ClearCell zincirdeki özel taşları mevcut sistemle tetikler.
        private IEnumerator CannonballSweep(int row, Transform cannonball, SpecialChain.ChainContext chain)
        {
            int nextColumn = 0;
            float endX = geometry.CellToWorld(new Vector2Int(BoardDefinition.VisibleWidth - 1, row)).x + geometry.CellSize + cannonballExitPadding;

            while (cannonball != null && cannonball.position.x < endX)
            {
                cannonball.position += Vector3.right * (cannonballSpeed * Time.deltaTime);

                while (nextColumn < BoardDefinition.VisibleWidth &&
                       cannonball.position.x >= geometry.CellToWorld(new Vector2Int(nextColumn, row)).x)
                {
                    specialChain.ClearCell(new Vector2Int(nextColumn, row), chain);
                    nextColumn++;
                }

                yield return null;
            }
        }

        private Vector3 GetStrikeStartPosition(Transform strikeSource, Vector3 targetPosition)
        {
            Camera sceneCamera = Camera.main;
            if (strikeSource == null || sceneCamera == null) return targetPosition;

            Vector2 sourceScreenPosition = RectTransformUtility.WorldToScreenPoint(
                sceneCamera,
                strikeSource.position);

            return HammerStrikeView.WorldPointFromScreenPoint(
                sceneCamera,
                sourceScreenPosition,
                targetPosition);
        }

        private bool HasCannonPresentation()
        {
            if (boardPresentation == null || cannonLeftAnchor == null ||
                cannonEntryPrefab == null || cannonEntryPrefab.Muzzle == null ||
                cannonballProjectilePrefab == null)
            {
                Debug.LogWarning("Cannon strike başlatılamadı: BoardPresentation, CannonLeftAnchor, CannonEntry/Muzzle veya Cannonball Projectile referansı eksik.", this);
                return false;
            }

            return true;
        }

        private void SetCannonMasks(bool active)
        {
            if (cannonMaskRight != null) cannonMaskRight.SetActive(active);
            if (cannonMaskRightDuplicate != null) cannonMaskRightDuplicate.SetActive(active);
        }

        private IEnumerator MovePresentationTo(Vector3 target, float duration)
        {
            if (boardPresentation == null) yield break;

            Vector3 start = boardPresentation.position;

            if (duration <= 0f)
            {
                boardPresentation.position = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                boardPresentation.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            boardPresentation.position = target;
        }
    }
}
