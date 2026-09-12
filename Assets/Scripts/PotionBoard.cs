using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PotionBoard : MonoBehaviour
{
    // Değerler 11
    //define the size of the board
    [SerializeField] private int width = 8;
    [SerializeField] private int height = 15;
    //define some spacing for the board
    [SerializeField] private float spacingX;
    [SerializeField] private float spacingY;
    private float cellSize = 0.575f;
    //get a reference to our potion prefabs
    [SerializeField] private GameObject[] potionPrefabs;
    [SerializeField] private Node[,] potionBoard;
    [SerializeField] private GameObject potionParent;
    private List<GameObject> potionToDestroy = new();
    private readonly List<MatchResult> currentMatchGroups = new();

    [SerializeField] private BoardState currentState = BoardState.Initializing;

    // Tab bar'daki özel vuruşlar. Seçim ve desen hesabı orada; burası yalnızca
    // verilen hücreleri temizliyor.
    [SerializeField] private SpecialStrikes specialStrikes;

    // SpecialStrikes satır desenini kurarken tahtanın genişliğini bilmeli.
    public int Width => width;

    private List<GameObject> deactivePotionPool = new();
    private bool waitForPointerRelease = false;

    // Takas KUYRUĞA ALINMAZ: gelen her geçerli komut kendi çözümlemesini hemen
    // başlatır, refill veya cascade sürerken bile. Dokunarak patlatma da aynı
    // şekilde çalışıyor.
    //
    // Eşzamanlı iki çözümleme birbirini bozmuyor, çünkü tahtayı değiştiren
    // bölümlerin hepsi ilk yield'den ÖNCE bitiyor: CheckBoard, eşleşme listesinin
    // kopyalanması ve hücrelerin boşaltılması tek bir kesintisiz blok. Sonradan
    // tarayan hat o hücreleri boş görüp atlıyor. Refill döngüsü de aynı şekilde
    // yield'siz.



    //get a reference to the collection nodes potionBoard + GO

    private Potion firstSelectedPotion;

    [SerializeField] private Potion secondSelectedPotion;
    [SerializeField] private ParticleSystem destroyParticlesRed;
    [SerializeField] private ParticleSystem destroyParticlesBlue;
    [SerializeField] private ParticleSystem destroyParticlesGreen;
    [SerializeField] private ParticleSystem destroyParticlesYellow;
    [SerializeField] private ParticleSystem explodingPaticles;

    // Süper bombanın kendi patlama efekti. Atanmazsa explodingPaticles kullanılır.
    [SerializeField] private ParticleSystem superExplodingParticles;

    // Roket oluşurken bir kez çalışan efekt. Taşın ÇOCUĞU değil, bağımsız
    // doğurulur: çocuk olsaydı roketin oluşma animasyonu onu da sürükler,
    // efekt roketle birlikte kayardı.
    [SerializeField] private ParticleSystem rocketSpawnParticles;

    // Roket ateşlenince bir kez çalışan efekt. Prefab yatay roket için
    // tasarlandı; dikey roket için Z ekseninde -90 döndürülür.
    [SerializeField] private ParticleSystem rocketFireParticles;

    // İki roket birleşirken, DuableRocket animasyonuyla aynı karede doğar.
    [SerializeField] private ParticleSystem doubleRocketParticles;
    // Mikser grubu AudioSource'un özelliği, klibin değil — bu yüzden her sesin
    // kendi kaynağı var. Hepsi Potion Board üzerinde durur, tek farkları
    // Inspector'daki Output alanına atanan mikser grubu.
    [SerializeField] private AudioSource matchSource;
    [SerializeField] private AudioSource superMatchSource;
    [SerializeField] private AudioSource explodingSource;

    [SerializeField] private AudioClip matchClip, superMatchClip, explodingClip;

    [Header("Ses seviyeleri")]
    [SerializeField, Range(0f, 1f)] private float matchVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float superMatchVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float explodingVolume = 1f;


    [SerializeField, Min(0f)] private float dropStaggerDelay = 0.2f;

    [Tooltip("Bir sütundaki düşüş başlangıçlarının toplamda bekleyebileceği en uzun süre.")]
    [SerializeField, Min(0f)] private float maxDropStagger = 0.1f;

    [Tooltip("Normal eşleşme kırıldıktan sonra düşüş başlamadan önceki kısa bekleme.")]
    [SerializeField, Min(0f)] private float matchSettleDelay = 0.08f;

    // Taşlar korunan taşa doğru bu hızla uçar; süre alt ve üst sınıra
    // kırpılır. Takas 0.12 sn sürüyor, birleşme onun temposunda kalmalı:
    // bir hücrelik birleşme alt sınıra, uzak köşeler üst sınıra dayanır.
    [Header("Süper eşleşme birleşmesi")]
    [SerializeField, Min(0.01f)] private float superMatchMergeSpeed = 10f;
    [SerializeField, Min(0f)] private float superMatchMergeMinDuration = 0.08f;
    [SerializeField, Min(0f)] private float superMatchMergeMaxDuration = 0.2f;

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

    // Puanlama: her eşleşme/patlama olayı anında puan verir (cascade dahil).
    [SerializeField] private int matchPoints = 10;
    [SerializeField] private int superMatchPoints = 15;
    [SerializeField] private int bombPoints = 10;


    // Tahtanın görsel tilemap'inin yükleneceği Grid objesi.
    [SerializeField] private Transform boardGrid;

    [Header("Cannon Strike Presentation")]
    [Tooltip("Grid ve Potions'un ortak root'u. Yalnızca Cannon sinematiğinde hareket eder.")]
    [SerializeField] private Transform boardPresentation;

    [Tooltip("Board'a bağlı kırılma/patlama efektlerinin root'u. Boş bırakılırsa BoardPresentation altında çalışma anında oluşturulur.")]
    [SerializeField] private Transform boardVfxRoot;

    [Tooltip("Cannon'ın ekrana girdiği sol nokta. Y değeri seçilen satırdan gelir.")]
    [SerializeField] private Transform cannonLeftAnchor;

    [Tooltip("Giriş/ateş animasyonunu içeren Cannon prefabı. Muzzle referansı CannonEntryView'da atanmalıdır.")]
    [SerializeField] private CannonEntryView cannonEntryPrefab;

    [Tooltip("RocketSingleRight görselinden türetilmiş, bağımsız sağa giden top prefabı.")]
    [SerializeField] private GameObject cannonballProjectilePrefab;
    [Tooltip("Cannon hedefleme süresince kapanacak iki sağ board maskesi.")]
    [SerializeField] private GameObject cannonMaskRight;
    [SerializeField] private GameObject cannonMaskRightDuplicate;

    [Header("Hammer Strike Presentation")]
    [Tooltip("Butonda animasyonu başlar; ayrı bir parent hedef hücreye gidip geri döner.")]
    [SerializeField] private GameObject hammerStrikePrefab;
    [SerializeField, Min(0f)] private float hammerTravelDuration = 0.2f;
    [Tooltip("Prefabın oluşturulmasından itibaren darbe zamanı (saniye).")]
    [SerializeField, Min(0f)] private float hammerImpactDelay = 0.75f;
    [Tooltip("Prefabın oluşturulmasından itibaren geri dönüşün başlayacağı zaman (saniye).")]
    [SerializeField, Min(0f)] private float hammerReturnDelay = 0.95f;
    [SerializeField, Min(0f)] private float hammerReturnDuration = 0.35f;
    [SerializeField, Min(0f)] private float hammerStrikeDuration = 1.5f;

    [SerializeField, Min(0f)] private float boardSlideDuration = 0.22f;
    [SerializeField, Min(0f)] private float boardReturnDuration = 0.18f;
    [Tooltip("Cannon için board'un sağa kayacağı tile sayısı. 1 = tam bir hücre genişliği.")]
    [SerializeField, Min(0f)] private float cannonBoardSlideCells = 1f;
    [SerializeField, Min(0f)] private float cannonFireDelay = 0.45f;
    [SerializeField, Min(0.1f)] private float cannonballSpeed = 12f;
    [SerializeField, Min(0f)] private float cannonballExitPadding = 0.75f;

    // Bu bir BoardState kapısı değildir. Yalnızca parent transform hareket
    // ederken yeni bir input'un dünya konumlarını bozmasını önleyen dar kapsamlı
    // Cannon sinematiği kilididir.
    private bool isCannonCinematic;
    private bool isCannonAwaitingTarget;
    private bool isCannonFiring;
    private bool isHammerStrikeActive;
    private Coroutine cannonAimRoutine;
    private Vector3 boardPresentationHomePosition;

    public bool IsCannonPresentationActive => isCannonCinematic;
    public event System.Action CannonStrikeFinished;

    //public static of potionboard
    public static PotionBoard Instance;


    private void Awake()
    {
        Instance = this;

        if (boardPresentation != null)
        {
            boardPresentationHomePosition = boardPresentation.position;
        }
    }

    private void Start()
    {
        InitializeBoard();
        currentState = BoardState.Idle;
    }

    // Ray ile hangi position'a tıkladığını alırız sonra if kontrollerini yaparız sonra tıkladığımız potion'ı bir referansa kaydederiz.
    private void Update()
    {
        if (GameManager.Instance.isGameEnded) return;

        if ((isCannonCinematic && !isCannonAwaitingTarget) || isHammerStrikeActive)
        {
            ClearPointerSelection();
            return;
        }

        if (waitForPointerRelease)
        {
            if (!Pointer.current.press.isPressed)
            {
                waitForPointerRelease = false;
            }
        }

        if (Pointer.current.press.isPressed && !waitForPointerRelease)
        {
            Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.collider != null && hit.collider.gameObject.GetComponent<Potion>())
            {
                Potion potion = hit.collider.gameObject.GetComponent<Potion>();

                if (firstSelectedPotion == null)
                {
                    firstSelectedPotion = potion;
                    potion.setSelectedVisual(true);
                }


                if (hit.collider.gameObject.GetComponent<Potion>() != firstSelectedPotion)
                {
                    secondSelectedPotion = potion;
                }

                if (!isCannonAwaitingTarget && firstSelectedPotion != null && secondSelectedPotion != null)
                {
                    SwapPotion(firstSelectedPotion, secondSelectedPotion);
                }

            }
        }
        else
        {
            // Parmak kalktı. İkinci bir taşa hiç değilmediyse bu bir DOKUNMA'dır;
            // takas olduysa SwapPotion referansları zaten temizlemiş olur.
            Potion tapped = secondSelectedPotion == null ? firstSelectedPotion : null;

            if (firstSelectedPotion != null)
            {
                firstSelectedPotion.setSelectedVisual(false);
            }

            firstSelectedPotion = null;
            secondSelectedPotion = null;

            // Özel vuruş seçiliyse dokunuş ona gider, taş özel olsun olmasın.
            bool usedStrike = tapped != null
                && specialStrikes != null
                && specialStrikes.TryUseOn(tapped);

            // Özel taşa dokunup bırakınca patlar. Tahta durumuna bakılmaz:
            // refill/cascade sürerken de dokunulabilir — takasta da böyle.
            if (!usedStrike && tapped != null && IsSpecial(tapped))
            {
                StartCoroutine(TapDetonate(tapped));
            }
        }
    }

    private void ClearPointerSelection()
    {
        if (firstSelectedPotion != null)
        {
            firstSelectedPotion.setSelectedVisual(false);
        }

        firstSelectedPotion = null;
        secondSelectedPotion = null;
        waitForPointerRelease = Pointer.current != null && Pointer.current.press.isPressed;
    }

    // Dokunarak patlatma. Takasla aynı akış: zincir, refill ve cascade tamamen
    // bitene kadar sürer, sonunda bir hamle düşer.
    private IEnumerator TapDetonate(Potion special)
    {
        yield return ExplodeChain(special);

        GameManager.Instance.ProcessTurn(0, true);
    }

    //InitializeBoard Board oluşturma methodu
    // İlk başta potionları yok eden methodu çağırırız sonra board'u yata ve dikey olarak merkeze yerleştiren hesaplamaları yaparız
    // Tahtanın iki boyutlu dizisi oluşturulur
    // Tüm cell'ler gezilir ve o anki cell'in position'nu belirlenir
    // arrayLayout yasaklı cell kontrolü yapılır 
    // Rastgele potionlar üretilir
    // Bu üretilen potionların parentleri potionParent olarak belirlenir
    // Sonra potion'un konumunu matrise kaydederiz(potion'Un konumunu potion'a öğretiriz)
    // Potion'Un konumunu board'a öğretiriz
    // potion'U silme listesine ekleriz
    // Potion seçilirken başlangıçta eşleşme oluşturmayacak türler arasından seçim yapılır
    private void InitializeBoard()
    {
        DestroyPotions();

        LoadBoardTilemap();

        // Tahta şekli aktif level'ın ArrayLayout'undan okunur.
        ArrayLayout levelLayout = GameManager.Instance.ActiveLevel.arrayLayout;

        spacingX = (float)(width - 1) / 2;
        spacingY = (float)((height) / 2) - 2.5f;

        potionBoard = new Node[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 position = new Vector2((x - spacingX) * cellSize, (y - spacingY) * cellSize);

                if (levelLayout.rows[y].row[x])
                {
                    potionBoard[x, y] = new Node(false, null);
                }
                else
                {
                    int randomIndex = GetValidPotionPrefabIndex(x, y);

                    GameObject potionObject = Instantiate(potionPrefabs[randomIndex], position, Quaternion.identity);
                    potionObject.transform.SetParent(potionParent.transform);
                    Potion potion = potionObject.GetComponent<Potion>();
                    potion.SetIndicies(x, y);
                    potionToDestroy.Add(potionObject);
                    potionBoard[x, y] = new Node(true, potion);

                }
            }
        }



    }

    // Aktif level'ın tilemap prefab'ını Grid altına kurar (yalnızca görsel).
    // Level'da prefab tanımlı değilse sahnedeki mevcut tilemap olduğu gibi kalır.
    private void LoadBoardTilemap()
    {
        GameObject tilemapPrefab = GameManager.Instance.ActiveLevel.boardTilemapPrefab;

        if (tilemapPrefab == null || boardGrid == null)
        {
            return;
        }

        // Sahnede duran eski tahta görselini kaldır.
        for (int i = boardGrid.childCount - 1; i >= 0; i--)
        {
            GameObject oldTilemap = boardGrid.GetChild(i).gameObject;
            oldTilemap.SetActive(false);
            Destroy(oldTilemap);
        }

        GameObject newTilemap = Instantiate(tilemapPrefab, boardGrid);
        newTilemap.transform.localPosition = tilemapPrefab.transform.localPosition;
        newTilemap.transform.localRotation = tilemapPrefab.transform.localRotation;
        newTilemap.transform.localScale = tilemapPrefab.transform.localScale;
    }

    // Mevcut hücrede yatay veya dikey üçlü eşleşme oluşturmayacak
    // potion prefablarından rastgele birini seçer.
    private int GetValidPotionPrefabIndex(int x, int y)
    {
        List<int> validIndexes = new();

        for (int i = 0; i < potionPrefabs.Length; i++)
        {
            Potion prefabPotion = potionPrefabs[i].GetComponent<Potion>();

            if (prefabPotion == null)
            {
                continue;
            }

            PotionType candidateType = prefabPotion.potionType;

            if (!WouldCreateInitialMatch(x, y, candidateType))
            {
                validIndexes.Add(i);
            }
        }

        if (validIndexes.Count == 0)
        {
            return Random.Range(0, potionPrefabs.Length);
        }

        int randomListIndex = Random.Range(0, validIndexes.Count);

        return validIndexes[randomListIndex];
    }

    private bool WouldCreateInitialMatch(int x, int y, PotionType candidateType)
    {
        bool horizontalMatch = IsSamePotionType(x - 1, y, candidateType) && IsSamePotionType(x - 2, y, candidateType);
        bool verticalMatch = IsSamePotionType(x, y - 1, candidateType) && IsSamePotionType(x, y - 2, candidateType);

        return horizontalMatch || verticalMatch;
    }

    private bool IsSamePotionType(int x, int y, PotionType candidateType)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return false;
        }

        Node node = potionBoard[x, y];

        if (node == null || node.potion == null)
        {
            return false;
        }

        return node.potion.potionType == candidateType;
    }

    // DestroyPotions: PotionToDestroy List dolu ise listedeki tüm elemanları gezer destroy ederiz sonra listeyi temizleriz
    private void DestroyPotions()
    {
        if (potionToDestroy.Count >= 1)
        {
            foreach (GameObject item in potionToDestroy)
            {
                item.SetActive(false);
                deactivePotionPool.Add(item);

            }
            potionToDestroy.Clear();
        }
    }


    // CheckBoard: İlk başta console'a "Checking Match" yazdırırız ve hasMatch değerini oluştururuz
    // potionsToRemove list'i oluşturulur
    // tüm node'lar dolaşılır(foreach) içinde potion yoksa isMatched'lar false olur
    // Tüm node'lar dolaşılır(for) anlık node'un isUsable kontrol edilir
    // Sonra her bir potion'ı potion referansında tutarız
    // Potion'Un eşleşmediğinden emin oluruz 
    // IsConnected ile potion'ların sağ sol yukarı aşağısı kontrol edilir
    // ardından connectedPotions ile eşleşen potion'ların 3'e eşit veya fazla olup olmadığını kontrol ederiz 
    private bool CheckBoard(bool _takeAction, Potion preferredPotion = null)
    {
        bool hasMatched = false;

        List<Potion> matchedPotionsThisCheck = new();

        if (_takeAction)
        {
            currentMatchGroups.Clear();
        }

        foreach (Node item in potionBoard)
        {
            if (item.potion != null)
            {
                item.potion.isMatched = false;
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (potionBoard[x, y].isUsable)
                {
                    Potion potion = potionBoard[x, y].potion;

                    // Doldurma sırasında hücre açık ama boş olabilir.
                    // Hedef hücresi mantıksal olarak atanmış olsa bile taş henüz
                    // havadaysa eşleşmeye dahil edilmez. Böylece dikey roketten
                    // sonra inen kolon yanlışlıkla ara konumdayken temizlenmez.
                    if (potion == null || potion.isMoving) continue;

                    if (!potion.isMatched)
                    {

                        MatchResult matchedPotions = IsConnected(potion);

                        if (matchedPotions.connectedPotions.Count >= 3)
                        {
                            MatchResult matchGroup = SuperMatch(matchedPotions);

                            if (_takeAction)
                            {
                                if (matchGroup.IsSuperMatch)
                                {
                                    matchGroup.protectedPotion = ChooseSuperMatchTarget(matchGroup, preferredPotion);
                                }

                                currentMatchGroups.Add(matchGroup);
                            }

                            matchedPotionsThisCheck.AddRange(matchGroup.connectedPotions);

                            foreach (Potion item in matchGroup.connectedPotions)
                            {
                                item.isMatched = true;
                            }
                            hasMatched = true;

                        }
                    }
                }
            }
        }

        if (_takeAction)
        {
            foreach (Potion item in matchedPotionsThisCheck)
            {
                item.isMatched = false;
            }
        }

        return hasMatched;
    }

    private Potion ChooseSuperMatchTarget(MatchResult matchGroup, Potion preferredPotion)
    {
        if (preferredPotion != null && matchGroup.connectedPotions.Contains(preferredPotion))
        {
            return preferredPotion;
        }

        int randomIndex = Random.Range(0, matchGroup.connectedPotions.Count);

        Potion survivePotion = matchGroup.connectedPotions[randomIndex];

        return survivePotion;
    }

    // Her eşleşme grubunu kendi türü ve hedef konumuyla temizler.
    // Böylece aynı turdaki normal ve süper eşleşmeler birbirine karışmaz.
    private IEnumerator RemoveAndRefill(List<MatchResult> matchGroups)
    {
        List<Potion> potionsToRemove = new();

        foreach (MatchResult matchGroup in matchGroups)
        {
            if (matchGroup.IsSuperMatch)
            {
                superMatchSource.PlayOneShot(superMatchClip, superMatchVolume);
                GameManager.Instance.AddPoints(superMatchPoints);
            }
            else
            {
                matchSource.PlayOneShot(matchClip, matchVolume);
                GameManager.Instance.AddPoints(matchPoints);
            }

            foreach (Potion item in matchGroup.connectedPotions)
            {
                if (item == null || potionsToRemove.Contains(item))
                {
                    continue;
                }
                if (item == matchGroup.protectedPotion)
                {
                    // Uzun yatay eşleşme yatay roket (satır), uzun dikey eşleşme dikey
                    // roket (sütun) verir. L/T biçimli süper eşleşme bomba olarak kalır.
                    if (matchGroup.direction == MatchDirection.LongHorizontal ||
                        matchGroup.direction == MatchDirection.LongVertical)
                    {
                        item.Rocket(true, vertical: matchGroup.direction == MatchDirection.LongVertical);

                        SpawnRocketParticle(item);
                    }
                    else
                    {
                        item.Bomb(true);
                    }

                    continue;
                }

                potionsToRemove.Add(item);

                int xIndex = item.xIndex;
                int yIndex = item.yIndex;

                potionBoard[xIndex, yIndex] = new Node(true, null);

                if (matchGroup.IsSuperMatch)
                {
                    item.MoveToTargetAtSpeed(
                        matchGroup.protectedPotion.transform.position,
                        superMatchMergeSpeed,
                        superMatchMergeMinDuration,
                        superMatchMergeMaxDuration);
                    StartCoroutine(SuperMatchDestroy(item));
                }
                else
                {
                    StartCoroutine(ShrinkThenBreak(item));
                }
            }
        }

        yield return new WaitUntil(() => AreAllMatchedPotionsDestroyed(potionsToRemove));

        // Normal match'te şimdiye kadar kırılma tamamlanır tamamlanmaz refill
        // başlıyordu. Çok kısa bu boşluk kırılma efektini okunur bırakır.
        if (matchSettleDelay > 0f)
        {
            yield return new WaitForSeconds(matchSettleDelay);
        }

        currentState = BoardState.Refilling;

        StartRefill();

        // Cascade yalnızca tüm board yerleşince kontrol edilir. Gizli rezerv
        // satırında bile hareket varsa, o taş bir sonraki refill'de görünür
        // alana inebileceği için CheckBoard'u erken çalıştırmayız.
        yield return new WaitUntil(() => !IsAnyPotionMoving());
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

    // Bomba ve roket aynı zincirden geçer, biri diğerini tetikleyebilir.
    // Kuyruk boşalınca tahta YALNIZCA BİR KEZ doldurulur ve cascade başlar.
    // Zincirdeki eşzamanlı patlama sayısı. Referans tipi, çünkü coroutine'ler
    // aynı sayacı paylaşmalı; cascade sırasında dokunmaya izin verdiğimiz için
    // birden fazla zincir aynı anda çalışabiliyor, statik olamaz.
    private class ChainCounter { public int running; }

    // Bomba ve roket aynı zincirden geçer, biri diğerini tetikleyebilir.
    // Her halka KENDİ coroutine'inde ve TEMAS ANINDA başlar — sıralı beklense
    // yoldaki bomba, süpürme tahtayı baştan sona geçene kadar patlamazdı.
    // Hepsi bitince tahta yalnızca bir kez doldurulur.
    // İki roket takas edilince artı roket olur. Birleşme animasyonundan sonra
    // İKİ roket de merkezden süpürür: biri satırı, diğeri sütunu. Dört uçan
    // parça buradan çıkıyor, her rokette ikişer tane var. İkinci roketi yok
    // etmek yerine ayakta tutmamızın sebebi bu.
    private IEnumerator DoubleRocketExplode(Potion horizontal, Potion vertical)
    {
        currentState = BoardState.Clearing;

        Vector2Int center = new Vector2Int(horizontal.xIndex, horizontal.yIndex);

        // DuableRocket yalnızca Rocket parçasının Animator'ünde tanımlı;
        // taşın diğer Animator'lerinde bu state yok, HasState onları eler.
        int mergeState = Animator.StringToHash("DuableRocket");

        Animator[] animators = horizontal.GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (!animator.isActiveAndEnabled) continue;

            if (!animator.HasState(0, mergeState)) continue;

            animator.Play(mergeState, 0, 0f);
        }

        // Bombadan farklı olarak ikinci roket animasyonla AYNI karede gizlenir:
        // DuableRocket klibi ikizi kendisi çiziyor, ikisi birden dursa üç roket
        // görünürdü.
        vertical.gameObject.SetActive(false);

        // Birleşme efekti de aynı karede, roketlerin buluştuğu hücrede.
        // Referans saklanıyor: roket ateşlenince kapatılması gerekiyor,
        // prefab döngüde olduğu için kendi kendine durmuyor.
        ParticleSystem mergeEffect = null;

        if (doubleRocketParticles != null)
        {
            Vector3 mergePosition = CellToWorld(center);
            mergePosition.z = -0.1f;

            mergeEffect = SpawnBoardVfx(doubleRocketParticles, mergePosition, Quaternion.identity);
        }

        yield return new WaitForSeconds(doubleRocketDelay);

        // Birleşme bitti, roket ateşleniyor: birleşme efekti kapanır.
        if (mergeEffect != null) mergeEffect.gameObject.SetActive(false);

        // Tek efekt yeter: artının iki kolu da aynı merkezden çıkıyor.
        if (rocketFireParticles != null)
        {
            Vector3 firePosition = CellToWorld(center);
            firePosition.z = -0.1f;

            SpawnBoardVfx(rocketFireParticles, firePosition, Quaternion.identity);
        }

        // İkinci roket takas sonrası komşu hücrede duruyor. Hücresi elle
        // boşaltılmazsa artı oradan geçerken onu bağımsız bir roket sanıp
        // tetikliyor ve fazladan bir süpürme başlıyor.
        potionBoard[vertical.xIndex, vertical.yIndex] = new Node(true, null);

        // Merkeze taşınır ve dikey eksene çevrilir. Hayatta kalan roket de
        // yataya sabitlenir, çünkü oyuncu iki dikey roketi de birleştirebilir.
        Vector2 centerWorld = CellToWorld(center);

        vertical.transform.position =
            new Vector3(centerWorld.x, centerWorld.y, vertical.transform.position.z);

        vertical.xIndex = center.x;
        vertical.yIndex = center.y;

        vertical.gameObject.SetActive(true);
        vertical.Rocket(true, vertical: true);

        // Hayatta kalan roket takasta iki hücrenin ortasında durdu; uçan
        // parçalar hücre merkezinden çıkmalı, yoksa temizlenen hücrelerle
        // yarım hücre kayık giderler.
        horizontal.transform.position =
            new Vector3(centerWorld.x, centerWorld.y, horizontal.transform.position.z);

        // Yataya sabitlenir, çünkü oyuncu iki dikey roketi de birleştirebilir.
        horizontal.Rocket(true, vertical: false);

        // Birleşme klibi döngüde; Animator varsayılan state'e döndürülmezse
        // taş havuzdan yeniden roket olarak çıktığında animasyon kendiliğinden
        // oynar. Split aynı karede olduğu için görsel bir sıçrama olmuyor.
        foreach (Animator animator in animators)
        {
            if (!animator.isActiveAndEnabled) continue;

            if (animator.HasState(0, mergeState)) animator.Rebind();
        }

        // İki süpürme aynı anda. Ortak triggered seti sayesinde kesişimdeki
        // bir bomba iki kez tetiklenmez.
        HashSet<Vector2Int> triggered = new() { center };
        ChainCounter counter = new();

        counter.running += 2;

        StartCoroutine(RunSweep(
            new SpecialTrigger(center, PotionType.Rocket, horizontal), triggered, counter));

        StartCoroutine(RunSweep(
            new SpecialTrigger(center, PotionType.Rocket, vertical), triggered, counter));

        // Zincire giren bomba ve roketler de bitsin.
        yield return new WaitUntil(() => counter.running == 0);

        yield return RefillAndCascade();
    }

    // Birleşen ikinci taşı gecikmeli gizler. Ayrı coroutine, çünkü çağıran
    // rutinin akışını bekletmemeli.
    private IEnumerator HideAfter(Potion potion, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (potion != null) potion.gameObject.SetActive(false);
    }

    // Özel vuruş: Hammer/Bomb hücreleri anında temizler; Cannon ise aynı satırı
    // top geçerken temizleyen ayrı bir sunum akışına girer. false dönmesi, UI'ın
    // hakkı düşürmemesi ve seçimin açık kalması gerektiği anlamına gelir.
    public bool TryRunStrike(
        StrikeKind kind,
        Vector2Int origin,
        IEnumerable<Vector2Int> cells,
        Transform strikeSource = null)
    {
        if (kind != StrikeKind.Cannon && (isCannonCinematic || isHammerStrikeActive)) return false;

        if (kind == StrikeKind.Cannon)
        {
            if (!isCannonCinematic || !isCannonAwaitingTarget) return false;

            isCannonAwaitingTarget = false;
            isCannonFiring = true;
            StartCoroutine(CannonStrikeRoutine(origin));
            return true;
        }

        if (cells == null) return false;

        if (kind == StrikeKind.Hammer)
        {
            if (hammerStrikePrefab == null) return false;

            isHammerStrikeActive = true;
            StartCoroutine(HammerStrikeRoutine(origin, cells, strikeSource));
            return true;
        }

        StartCoroutine(StrikeRoutine(cells));
        return true;
    }

    private IEnumerator HammerStrikeRoutine(
        Vector2Int origin,
        IEnumerable<Vector2Int> cells,
        Transform strikeSource)
    {
        GameObject travelRoot = null;

        try
        {
            Vector3 targetPosition = CellToWorld(origin);
            targetPosition.z = -0.2f;

            Vector3 startPosition = GetHammerStartPosition(strikeSource, targetPosition);
            // Animator yalnızca prefabın içini yönetir. Dış parent'ı hareket
            // ettirerek klip oynarken de hedef hücreye gidip geri dönebiliriz.
            travelRoot = new GameObject("HammerTravelRoot");
            travelRoot.transform.position = startPosition;
            GameObject hammerObject = Instantiate(
                hammerStrikePrefab, startPosition, Quaternion.identity, travelRoot.transform);
            HammerStrikeView hammer = hammerObject.GetComponent<HammerStrikeView>();
            if (hammer == null) hammer = hammerObject.AddComponent<HammerStrikeView>();
            hammer.PlayStrike();
            hammer.BeginTravel(travelRoot.transform);

            HashSet<Vector2Int> triggered = new();
            ChainCounter counter = new();
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
                    currentState = BoardState.Clearing;
                    foreach (Vector2Int cell in cells)
                    {
                        ClearCell(cell, triggered, counter);
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
            yield return new WaitUntil(() => counter.running == 0);

            // Tokmak ekrandan dönmüş olsa da refill sırasında yeni bir swap
            // başlatmak güvenli değildir; dar kapsamlı kilit coroutine sonuna
            // kadar burada kalır.
            yield return RefillAndCascade();
        }
        finally
        {
            if (travelRoot != null) Destroy(travelRoot);
            isHammerStrikeActive = false;
        }
    }

    private Vector3 GetHammerStartPosition(Transform strikeSource, Vector3 targetPosition)
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

    // Cannon butonuna basıldığı anda çağrılır: board hemen sinematik duruşuna
    // geçer, fakat hedef satır henüz seçilmediği için yalnızca tek dokunuş bekler.
    public bool TryBeginCannonAim()
    {
        if (isCannonCinematic || isHammerStrikeActive || !HasCannonPresentation()) return false;

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
        yield return MovePresentationTo(boardPresentationHomePosition, boardSlideDuration);

        SetCannonMasks(true);
        isCannonCinematic = false;
    }

    private IEnumerator CannonAimRoutine()
    {
        yield return new WaitUntil(() => !IsAnyPotionMoving());

        Vector3 slideTarget = boardPresentationHomePosition;
        slideTarget.x += cellSize * cannonBoardSlideCells;
        yield return MovePresentationTo(slideTarget, boardSlideDuration);

        isCannonAwaitingTarget = true;
        cannonAimRoutine = null;
    }

    private void SetCannonMasks(bool active)
    {
        if (cannonMaskRight != null) cannonMaskRight.SetActive(active);
        if (cannonMaskRightDuplicate != null) cannonMaskRightDuplicate.SetActive(active);
    }

    // Potion kırılma efektleri board'un bir parçasıdır. BoardPresentation
    // hareket ederken world-space'de asılı kalmamaları için ortak root altında
    // doğar ve bütün Particle System'leri local simulation kullanır.
    private ParticleSystem SpawnBoardVfx(ParticleSystem prefab, Vector3 worldPosition, Quaternion rotation)
    {
        if (prefab == null) return null;

        Transform parent = GetBoardVfxRoot();
        ParticleSystem effect = parent != null
            ? Instantiate(prefab, worldPosition, rotation, parent)
            : Instantiate(prefab, worldPosition, rotation);

        SetBoardParticleSimulationLocal(effect.gameObject);
        return effect;
    }

    private Transform GetBoardVfxRoot()
    {
        if (boardVfxRoot != null) return boardVfxRoot;
        if (boardPresentation == null) return null;

        Transform existingRoot = boardPresentation.Find("BoardVFX");
        if (existingRoot != null)
        {
            boardVfxRoot = existingRoot;
            return boardVfxRoot;
        }

        GameObject root = new("BoardVFX");
        boardVfxRoot = root.transform;
        boardVfxRoot.SetParent(boardPresentation, worldPositionStays: false);
        return boardVfxRoot;
    }

    private static void SetBoardParticleSimulationLocal(GameObject effectRoot)
    {
        foreach (ParticleSystem system in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }
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

    private IEnumerator CannonStrikeRoutine(Vector2Int origin)
    {
        GameObject cannonInstance = null;
        GameObject cannonballInstance = null;
        Vector3 homePosition = boardPresentationHomePosition;
        bool presentationFinished = false;

        try
        {
            Vector3 cannonPosition = cannonLeftAnchor.position;
            cannonPosition.y = CellToWorld(origin).y;

            CannonEntryView cannonView = Instantiate(cannonEntryPrefab, cannonPosition, Quaternion.identity);
            cannonInstance = cannonView.gameObject;

            yield return new WaitForSeconds(cannonFireDelay);

            Transform muzzle = cannonView != null ? cannonView.Muzzle : null;
            if (muzzle == null)
            {
                Debug.LogWarning("Cannon instance Muzzle referansını kaybetti; vuruş güvenli biçimde iptal edildi.", this);
                yield break;
            }

            cannonballInstance = Instantiate(cannonballProjectilePrefab, muzzle.position, Quaternion.identity);
            yield return CannonballSweep(origin.y, cannonballInstance.transform);

            // Mermi geçtiyse zincirlenmiş bomba/roketlerin de bitmesini bekleriz.
            yield return new WaitUntil(() => cannonCounter.running == 0);

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

            yield return RefillAndCascade();
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

    // CannonballSweep tarafından kullanılmak üzere rutin ömrünce paylaşılan
    // zincir verisi. ClearCell zincirdeki özel taşları mevcut sistemle tetikler.
    private readonly HashSet<Vector2Int> cannonTriggered = new();
    private readonly ChainCounter cannonCounter = new();

    private IEnumerator CannonballSweep(int row, Transform cannonball)
    {
        cannonTriggered.Clear();
        cannonCounter.running = 0;

        int nextColumn = 0;
        float endX = CellToWorld(new Vector2Int(width - 1, row)).x + cellSize + cannonballExitPadding;

        while (cannonball != null && cannonball.position.x < endX)
        {
            cannonball.position += Vector3.right * (cannonballSpeed * Time.deltaTime);

            while (nextColumn < width &&
                   cannonball.position.x >= CellToWorld(new Vector2Int(nextColumn, row)).x)
            {
                ClearCell(new Vector2Int(nextColumn, row), cannonTriggered, cannonCounter);
                nextColumn++;
            }

            yield return null;
        }
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

    private IEnumerator StrikeRoutine(IEnumerable<Vector2Int> cells)
    {
        currentState = BoardState.Clearing;

        HashSet<Vector2Int> triggered = new();
        ChainCounter counter = new();

        foreach (Vector2Int cell in cells)
        {
            ClearCell(cell, triggered, counter);
        }

        yield return new WaitUntil(() => counter.running == 0);

        yield return RefillAndCascade();
    }

    private IEnumerator ExplodeChain(Potion first)
    {
        currentState = BoardState.Clearing;

        HashSet<Vector2Int> triggered = new();
        ChainCounter counter = new();

        TriggerSpecial(new SpecialTrigger(
            new Vector2Int(first.xIndex, first.yIndex), first.potionType, first), triggered, counter);

        yield return new WaitUntil(() => counter.running == 0);

        yield return RefillAndCascade();
    }

    // Zincire yeni bir halka ekler ve hemen başlatır.
    private void TriggerSpecial(SpecialTrigger trigger, HashSet<Vector2Int> triggered, ChainCounter counter)
    {
        // Aynı hücre ikinci kez tetiklenmesin.
        if (!triggered.Add(trigger.position)) return;

        counter.running++;

        if (trigger.type == PotionType.Rocket)
        {
            StartCoroutine(RunSweep(trigger, triggered, counter));
            return;
        }

        // Bomba anlık: beklemeye gerek yok.
        BlastAround(trigger.position, triggered, counter);
        counter.running--;
    }

    private IEnumerator RunSweep(SpecialTrigger trigger, HashSet<Vector2Int> triggered, ChainCounter counter)
    {
        yield return SweepLine(trigger, triggered, counter);

        counter.running--;
    }

    // Bomba: merkez dahil 3x3 alanı temizler.
    private void BlastAround(Vector2Int center, HashSet<Vector2Int> triggered, ChainCounter counter)
    {
        SpawnBoardVfx(explodingPaticles, CellToWorld(center), Quaternion.identity);
        explodingSource.PlayOneShot(explodingClip, explodingVolume);

        // Zincirdeki her patlama ayrı puan verir.
        GameManager.Instance.AddPoints(bombPoints);

        for (int xIndex = center.x - 1; xIndex <= center.x + 1; xIndex++)
        {
            for (int yIndex = center.y - 1; yIndex <= center.y + 1; yIndex++)
            {
                ClearCell(new Vector2Int(xIndex, yIndex), triggered, counter);
            }
        }
    }

    // Roket: iki parça merkezden dışa sabit hızla uçar, üzerinden geçtikleri
    // hücreyi temizler. Yatay roket satır boyunca (x), dikey roket sütun boyunca
    // (y) gider; görsel aynı, yalnızca eksen değişir. Parçalar taşın çocuğu
    // olduğu için taşın hücresi hemen boşaltılır ama taş, iz sönene kadar
    // havuza yollanmaz (FlyOutAndPool).
    private IEnumerator SweepLine(SpecialTrigger trigger, HashSet<Vector2Int> triggered, ChainCounter counter)
    {
        Potion rocket = trigger.potion;
        bool vertical = rocket != null && rocket.IsVerticalRocket;

        // Eksen: yatayda sütun indeksi (x) değişir ve sınır genişlik; dikeyde
        // satır indeksi (y) değişir ve sınır görünür yükseklik (8).
        Vector2Int step = vertical ? Vector2Int.up : Vector2Int.right;
        Vector3 worldStep = vertical ? Vector3.up : Vector3.right;
        int limit = vertical ? 8 : width;
        int origin = vertical ? trigger.position.y : trigger.position.x;

        explodingSource.PlayOneShot(explodingClip, explodingVolume);
        GameManager.Instance.AddPoints(bombPoints);

        if (rocketFireParticles != null)
        {
            // CellToWorld Vector2 döner, z sıfır kalır. Efekt taşların önünde
            // dursun diye kameraya doğru 0.1 çekiliyor.
            Vector3 firePosition = CellToWorld(trigger.position);
            firePosition.z = -0.1f;

            SpawnBoardVfx(rocketFireParticles, firePosition,
                vertical ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.identity);
        }

        potionBoard[trigger.position.x, trigger.position.y] = new Node(true, null);

        // "plus" sağa ya da yukarı, "minus" sola ya da aşağı giden parça.
        Transform plus = rocket != null ? rocket.RocketRight : null;
        Transform minus = rocket != null ? rocket.RocketLeft : null;

        if (rocket != null)
        {
            // Roket gövdesinin kendi child trail'leri de Potion altında
            // kalır; Cannon dönüşünde çıkmış parçacıkların kopmaması gerekir.
            SetBoardParticleSimulationLocal(rocket.gameObject);
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
            int reached = Mathf.FloorToInt(travelled / cellSize);

            while (nextPlus < limit && nextPlus <= origin + reached)
            {
                ClearCell(trigger.position + step * (nextPlus - origin), triggered, counter);
                nextPlus++;
            }

            while (nextMinus >= 0 && nextMinus >= origin - reached)
            {
                ClearCell(trigger.position + step * (nextMinus - origin), triggered, counter);
                nextMinus--;
            }

            yield return null;
        }

        if (rocket != null)
        {
            SpawnDestroyParticle(rocket);

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

        ReturnPotionToPool(rocket);
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
    private void ClearCell(Vector2Int cell, HashSet<Vector2Int> triggered, ChainCounter counter)
    {
        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= 8) return;

        Node node = potionBoard[cell.x, cell.y];

        if (node == null || !node.isUsable || node.potion == null) return;

        Potion potion = node.potion;

        // Havuz metodu tipi orijinaline döndüreceği için önce kaydedilir.
        PotionType type = potion.potionType;

        potionBoard[cell.x, cell.y] = new Node(true, null);

        if (type == PotionType.Rocket)
        {
            TriggerSpecial(new SpecialTrigger(cell, type, potion), triggered, counter);
            return;
        }

        SpawnDestroyParticle(potion);
        ReturnPotionToPool(potion);

        if (type == PotionType.Bomb)
        {
            TriggerSpecial(new SpecialTrigger(cell, type, null), triggered, counter);
        }
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        Vector2 world = new((cell.x - spacingX) * cellSize, (cell.y - spacingY) * cellSize);

        // Grid ve potionlar BoardPresentation'ın child'ı olduğunda bu offset
        // görsel hücre merkezini doğru tutar. Normal oyunda offset sıfırdır.
        if (boardPresentation != null)
        {
            Vector3 offset = boardPresentation.position - boardPresentationHomePosition;
            world += new Vector2(offset.x, offset.y);
        }

        return world;
    }

    // Patlama bittikten sonraki ortak kuyruk: boşalan hücreleri doldur,
    // yeni oluşan eşleşmeleri cascade et, tahtayı Idle'a bırak.
    // Hem zincir hem süper bomba buraya iner — eskiden ikisinde kopyalanmıştı.
    private IEnumerator RefillAndCascade()
    {
        currentState = BoardState.Refilling;

        StartRefill();

        yield return new WaitUntil(() => !IsAnyPotionMoving());

        currentState = BoardState.Checking;

        bool hasMatched = CheckBoard(true);

        while (hasMatched)
        {
            currentState = BoardState.Clearing;

            List<MatchResult> matchGroups = new List<MatchResult>(currentMatchGroups);

            yield return RemoveAndRefill(matchGroups);

            currentState = BoardState.Checking;
            hasMatched = CheckBoard(true);
        }

        currentState = BoardState.Idle;
    }

    private IEnumerator SuperBombExplod(Potion _targetPotion, Potion _mergedPotion)
    {
        currentState = BoardState.Clearing;

        Vector2Int bombPosition = new Vector2Int(_targetPotion.xIndex, _targetPotion.yIndex);

        // İki bomba birbirine doğru geldi; birleşme iki hücrenin ORTASINDA
        // görünmeli, hayatta kalan bombanın hücresinde değil. Patlama hâlâ
        // hücreye çakılı, yalnızca görsel merkez kayıyor.
        Vector2 mergePoint = _mergedPotion != null
            ? (CellToWorld(bombPosition) +
               CellToWorld(new Vector2Int(_mergedPotion.xIndex, _mergedPotion.yIndex))) * 0.5f
            : CellToWorld(bombPosition);

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
            SetBoardParticleSimulationLocal(bombObject);

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

        ParticleSystem explosionEffect = superExplodingParticles != null
            ? superExplodingParticles
            : explodingPaticles;

        SpawnBoardVfx(explosionEffect, explosionPosition, Quaternion.identity);
        explodingSource.PlayOneShot(explodingClip, explodingVolume);

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

                    if (xIndex < 0 || xIndex >= width || yIndex < 0 || yIndex >= 8)
                    {
                        continue;
                    }
                    Node node = potionBoard[xIndex, yIndex];
                    if (node == null || !node.isUsable || node.potion == null)
                    {
                        continue;
                    }
                    Potion potion = node.potion;
                    potionBoard[xIndex, yIndex] = new Node(true, null);

                    // Taşın kendi kırılma efekti — normal eşleşmedekiyle aynı.
                    // Havuza dönmeden ÖNCE, tipi hâlâ doğruyken çağrılmalı.
                    SpawnDestroyParticle(potion);

                    ReturnPotionToPool(potion);
                }
            }
        }

        // Patlama görülsün: taşlar temizlendikten sonra tahta bir an boş kalır,
        // düşüş hemen başlamaz.
        yield return new WaitForSeconds(explosionSettleDelay);

        yield return RefillAndCascade();
    }

    // Normal eşleşmede taş anında kaybolmaz: önce hızlıca küçülür, sonra kırılır.
    // RemoveAndRefill zaten taşların kapanmasını beklediği için ayrı zamanlama gerekmez.
    private IEnumerator ShrinkThenBreak(Potion item)
    {
        yield return item.ShrinkOut();

        SpawnDestroyParticle(item);
        ReturnPotionToPool(item);
    }

    private IEnumerator SuperMatchDestroy(Potion item)
    {
        yield return new WaitUntil(() => !item.isMoving);


        ReturnPotionToPool(item);
    }

    private void ReturnPotionToPool(Potion item)
    {
        // ClearSpecial tipi orijinaline döndürmeden önce bomba olup olmadığını kaydet.
        bool wasBomb = item.potionType == PotionType.Bomb;

        item.ClearSpecial();

        // Temizlenen taş orijinal rengine sayılır; bombaysa ayrıca Bomb hedefine de sayılır.
        GameManager.Instance.RegisterClearedPotion(item.potionType);

        if (wasBomb)
        {
            GameManager.Instance.RegisterClearedPotion(PotionType.Bomb);
        }

        item.isMatched = false;
        item.isMoving = false;
        item.gameObject.SetActive(false);

        if (!deactivePotionPool.Contains(item.gameObject))
        {
            deactivePotionPool.Add(item.gameObject);
        }
    }

    // Roket oluşma efekti. Taşın KÖKÜNE bağlanır çünkü roket oluştuktan sonra
    // cascade taşı aşağı indirebiliyor (dikey eşleşmede temizlenen hücreler
    // roketin altında kalır) — efekt onunla birlikte inmeli.
    //
    // Kökte olması güvenli: oluşma animasyonu RocketArt'ta, yani efekti
    // sürüklemez. worldPositionStays sayesinde taşın 0.18'lik ölçeği efekte
    // uygulanmaz, prefabdaki boyutunda kalır.
    private void SpawnRocketParticle(Potion item)
    {
        if (rocketSpawnParticles == null) return;

        ParticleSystem effect = Instantiate(rocketSpawnParticles, item.transform.position, Quaternion.identity);

        effect.transform.SetParent(item.transform, worldPositionStays: true);
        SetBoardParticleSimulationLocal(effect.gameObject);
    }

    private void SpawnDestroyParticle(Potion item)
    {
        if (item.potionType == PotionType.Red)
        {
            SpawnBoardVfx(destroyParticlesRed, item.transform.position, Quaternion.identity);
        }
        else if (item.potionType == PotionType.Blue)
        {
            SpawnBoardVfx(destroyParticlesBlue, item.transform.position, Quaternion.identity);
        }
        else if (item.potionType == PotionType.Green)
        {
            SpawnBoardVfx(destroyParticlesGreen, item.transform.position, Quaternion.identity);
        }
        else if (item.potionType == PotionType.Yellow)
        {
            SpawnBoardVfx(destroyParticlesYellow, item.transform.position, Quaternion.identity);
        }
    }

    private bool AreAllMatchedPotionsDestroyed(List<Potion> potions)
    {
        foreach (Potion potion in potions)
        {
            if (potion != null && potion.gameObject.activeSelf)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsAnyPotionMoving()
    {
        foreach (Node node in potionBoard)
        {
            if (node == null || node.potion == null)
            {
                continue;
            }

            if (node.potion.isMoving)
            {
                return true;
            }
        }

        return false;
    }

    // RefillPotion: ilk başta bir while döngüsü ile üst cell'leri tararız, board'un dışında değilse ve node null ise yOffset'i 1 arttırırız
    // İf ile board'un içinde ve potion içeriği null olmayan bir node var mı kontrol ederiz
    // if koşulu true döndüğünde ilgili poiton'un referansını alırız ve bir Vector3 targetPos belirleriz
    // Aldığımız potion'u MoveToTarget method'u ile targetPos'a akışını sağlarız
    // SetIndicies ile potion'un kendi belleğinde tutuğu konumunu güncelleriz 
    // Sonra potionBoad ile potion'un bulunduğu node'u boş node'a atarız
    // Sonra kayan potion'un eski konumunu null oolarka güncelleriz
    // Bir if kontrolü ile Board'un en üstünde isek SpawnPotionAtTop methodunu çağırırız
    private void StartRefill()
    {
        for (int x = 0; x < width; x++)
        {
            int dropOrder = 0;
            int spawnOrder = 0;

            for (int y = 0; y < height; y++)
            {
                if (!potionBoard[x, y].isUsable || potionBoard[x, y].potion != null)
                {
                    continue;
                }

                float startDelay = Mathf.Min(dropOrder * dropStaggerDelay, maxDropStagger);

                if (RefillPotion(x, y, startDelay, spawnOrder))
                {
                    // Aynı sütundaki yeni potion'lar aynı dünya koordinatından
                    // doğmaz: her biri bir hücre daha yukarıdan gelir. Böylece
                    // uzun düşüşte birbirine yetişip üst üste binemezler.
                    spawnOrder++;
                }

                dropOrder++;
            }
        }
    }

    // true: havuzdan yeni potion doğdu; çağıran sütun giriş yüksekliğini artırır.
    private bool RefillPotion(int x, int y, float startDelay, int spawnOrder)
    {
        // Kapalı hücre asla doldurulmaz.
        if (!potionBoard[x, y].isUsable) return false;

        int yOffset = 1;

        // Yukarı ararken kapalı VE boş hücreleri atla — taşlar kapalı hücrelerin üzerinden düşer.
        while (y + yOffset < height &&
               (!potionBoard[x, y + yOffset].isUsable || potionBoard[x, y + yOffset].potion == null))
        {
            yOffset++;
        }

        if (y + yOffset < height && potionBoard[x, y + yOffset].potion != null)
        {
            Potion potion = potionBoard[x, y + yOffset].potion;

            Vector3 targetPos = new Vector3((x - spacingX) * cellSize, (y - spacingY) * cellSize, potion.transform.position.z);

            potion.SetIndicies(x, y);

            potion.MoveToDown(targetPos, startDelay);

            potionBoard[x, y] = potionBoard[x, y + yOffset];
            potionBoard[x, y + yOffset] = new Node(true, null);

            return false;
        }

        if (y + yOffset >= height)
        {
            return SpawnPotionAtTop(x, startDelay, spawnOrder);
        }

        return false;
    }

    // SpawnPotionAtTop: RefillPotion method'unda üstteki potion'ları alt node'a indirdik fakat üst kısımda inecek potion kalmayınca bu methodu çağırıyoruz
    // İlk önce index adında bir değer oluştururuz ve ona FindIndexOfLowestNull methodundan gelen int değeri atarız, bu method sütundaki en alttaki boş node'un değerini bize verir
    // Yeni oluşacak iksirin yukarıdan aşağıya ne kaç birim hareket edeceğini hesaplarız height - index
    // yeni bir newPotion oluştururuz
    // sonra bu yeni poiton'nu poitonBoard iki boyutlu dizisine kayıt ederiz
    // Sonra Vector3 type'ında bir targetPos oluştururuz ve MoveToTarge methoduna veririz 
    private bool SpawnPotionAtTop(int x, float startDelay, int spawnOrder)
    {
        int index = FindIndexOfLowestNull(x);

        if (index == 99) return false;                   // sütunda doldurulacak açık hücre yok
        if (deactivePotionPool.Count == 0) return false; // havuz boş — crash koruması

        int randomIndex = Random.Range(0, deactivePotionPool.Count);

        GameObject newPotionObject = deactivePotionPool[randomIndex];
        float spawnY = (height + spawnOrder - spacingY) * cellSize;
        newPotionObject.transform.position = new Vector2((x - spacingX) * cellSize, spawnY);

        newPotionObject.SetActive(true);
        deactivePotionPool.Remove(newPotionObject);
        Potion newPotion = newPotionObject.GetComponent<Potion>();
        newPotion.SetIndicies(x, index);
        potionBoard[x, index] = new Node(true, newPotion);
        Vector3 targetPos = new Vector3((x - spacingX) * cellSize, (index - spacingY) * cellSize, newPotionObject.transform.position.z);
        newPotion.MoveToDown(targetPos, startDelay);
        return true;
    }

    // FindIndexOfLowestNull: Belirli bir sütundaki en aşağıda bulunan null node'un değerini döndürür
    // lowestNull adınad bir değer oluşturulur
    // aynı sütun içerisinde aşağıya doğru node'ları tarayan bir for yazılır
    // Eğer potion == null olan değer varsa y değeri lowestNull'a atanır 
    // lowestNull return edilir
    private int FindIndexOfLowestNull(int x)
    {
        int lowestNull = 99;

        for (int y = height - 1; y >= 0; y--)
        {
            if (potionBoard[x, y].isUsable && potionBoard[x, y].potion == null)
            {
                lowestNull = y;
            }
        }

        return lowestNull;
    }

    #region Cascading Potions

    #endregion

    // SuperMatch: MatchResult türünde bir method, MatchResult type'ında _matchedResults adında bir parametre alıyor
    // İlk öncelikle _mathedResults.direction ile match yönünü belirleriz bunun için bir if ve if else kullanırız
    // Ardından bir foreach ile döngüdeki potionların adjacentlerinde başka matchler var mı onu taratırız
    // CheckDirection methodları ile bir yukarı ve bir aşağıdaki(Eğer horizontal ise) tarar ve oluşturduğumuz geçici listeye ekler
    // Geçici listeyi kontrol ederiz count 2'den uzunsa geçici listeye potion'Ları aktarırız

    private MatchResult SuperMatch(MatchResult _matchedResults)
    {
        if (_matchedResults.direction == MatchDirection.Horizontal || _matchedResults.direction == MatchDirection.LongHorizontal)
        {
            foreach (Potion pot in _matchedResults.connectedPotions)
            {
                List<Potion> extraConnectedPotion = new();

                CheckDirection(pot, new Vector2Int(0, 1), extraConnectedPotion);
                CheckDirection(pot, new Vector2Int(0, -1), extraConnectedPotion);

                if (extraConnectedPotion.Count >= 2)
                {
                    extraConnectedPotion.AddRange(_matchedResults.connectedPotions);

                    return new MatchResult
                    {
                        connectedPotions = extraConnectedPotion,
                        direction = MatchDirection.Super
                    };
                }
            }
            return new MatchResult
            {
                connectedPotions = _matchedResults.connectedPotions,
                direction = _matchedResults.direction
            };
        }
        else if (_matchedResults.direction == MatchDirection.Vertical || _matchedResults.direction == MatchDirection.LongVertical)
        {
            foreach (Potion pot in _matchedResults.connectedPotions)
            {
                List<Potion> extraConnectedPotion = new();

                CheckDirection(pot, new Vector2Int(1, 0), extraConnectedPotion);
                CheckDirection(pot, new Vector2Int(-1, 0), extraConnectedPotion);

                if (extraConnectedPotion.Count >= 2)
                {
                    extraConnectedPotion.AddRange(_matchedResults.connectedPotions);

                    return new MatchResult
                    {
                        connectedPotions = extraConnectedPotion,
                        direction = MatchDirection.Super
                    };
                }
            }
            return new MatchResult
            {
                connectedPotions = _matchedResults.connectedPotions,
                direction = _matchedResults.direction
            };
        }

        return null;
    }

    // IsConncected: 
    //check right, check left
    //have we made a 3 match? (Horizontal Match)
    //checking for more than 3 (Long horizontal Match)
    //clear out the connectedpotions
    //readd our initial potion
    MatchResult IsConnected(Potion potion)
    {
        List<Potion> connectedPotions = new();

        connectedPotions.Add(potion);

        CheckDirection(potion, new Vector2Int(1, 0), connectedPotions);
        CheckDirection(potion, new Vector2Int(-1, 0), connectedPotions);

        if (connectedPotions.Count == 3)
        {
            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.Horizontal
            };
        }
        if (connectedPotions.Count >= 3)
        {
            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.LongHorizontal
            };
        }

        connectedPotions.Clear();
        connectedPotions.Add(potion);

        CheckDirection(potion, new Vector2Int(0, 1), connectedPotions);
        CheckDirection(potion, new Vector2Int(0, -1), connectedPotions);

        if (connectedPotions.Count == 3)
        {
            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.Vertical
            };
        }
        else if (connectedPotions.Count >= 3)
        {
            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.LongVertical
            };
        }
        else
        {
            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.None
            };
        }
    }

    // CheckDirection: Potion, Vector2Int, List<Potion> type'ında 3 adet parametre alırız 
    // PotionType değerinde bir değer oluşturulur ve gelen parametrenin potionType'ı alınır
    // int x ve y değerlerli oluşturulur 
    // x ve y'nin board'un içinde oluduğunu kontrol eden bir while döngüsü yazarız
    // ilgili cell'in isUsable olup olmadığını kontrol ederiz 
    // o node'daki potion'un referansını oluştururuz
    // komşu potion'un isMatched olmadığını ve potionType'ının eşit olduğunu kontrol eden bir if yazılır
    // Komşu potion parametre olarak aldığımız listeye eklenir
    // x ve y değerlerine yönler eklenir
    private void CheckDirection(Potion pot, Vector2Int direction, List<Potion> connectedPotions)
    {
        PotionType potionType = pot.potionType;

        // Özel taşlar eşleşmeye katılmaz: yan yana gelen üç bomba ya da üç roket
        // patlamasın. Buradan dönünce IsConnected listesi tek elemanda kalır.
        if (potionType == PotionType.Bomb || potionType == PotionType.Rocket) return;

        int x = pot.xIndex + direction.x;
        int y = pot.yIndex + direction.y;

        while (x >= 0 && x < width && y >= 0 && y < 8)
        {
            if (potionBoard[x, y].isUsable)
            {
                Potion neighbourPotion = potionBoard[x, y].potion;

                // Refill sürerken hücre açık ama boş olabilir.
                if (neighbourPotion == null || neighbourPotion.isMoving) break;

                if (!neighbourPotion.isMatched && neighbourPotion.potionType == potionType)
                {
                    connectedPotions.Add(neighbourPotion);

                    x += direction.x;
                    y += direction.y;

                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
    }

    #region Swaping Potions

    // SwapPotion: _currentPotion ve _targetPotion adında Potion type'ında iki adet parametre alır
    // ilk başta bir if sorgusu ile currenPotion ve targetPotion'un isAdjacent true olduğunu kontrol ederiz(early exit) 
    // DoSwap method'du çağırılır, currentPotion ve targetPotion parametre olarak verilir
    // State Swapping olarak güncellenir ve ProcessMatches coroutine'i başlatılır
    private void SwapPotion(Potion _currentPotion, Potion _targetPotion)
    {
        if (!IsAdjacent(_currentPotion, _targetPotion)) return;

        // Özel vuruş seçiliyken takas yok; dokunuş vuruşa ayrılmış durumda.
        if (specialStrikes != null && specialStrikes.IsArmed) return;

        if (!CanSwapNow(_currentPotion, _targetPotion)) return;

        _currentPotion.setSelectedVisual(false);

        BeginSwap(_currentPotion, _targetPotion);
        FinishPointerSwap();
    }

    // Takas yalnızca iki taş da DURUYORSA ve ikisi de hâlâ kendi hücresindeyse
    // kabul edilir. Düşen ya da temizlenmekte olan bir taşa takas yapılamaz;
    // öyle bir girdi kuyruğa alınmaz, sessizce yok sayılır.
    private bool CanSwapNow(Potion currentPotion, Potion targetPotion)
    {
        if (currentPotion == null || targetPotion == null) return false;
        if (currentPotion.isMoving || targetPotion.isMoving) return false;

        return PotionAt(new Vector2Int(currentPotion.xIndex, currentPotion.yIndex)) == currentPotion
            && PotionAt(new Vector2Int(targetPotion.xIndex, targetPotion.yIndex)) == targetPotion;
    }

    private void BeginSwap(Potion currentPotion, Potion targetPotion)
    {
        currentState = BoardState.Swapping;
        DoSwap(currentPotion, targetPotion);

        StartCoroutine(ProcessMatches(currentPotion, targetPotion));
    }

    private Potion PotionAt(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height) return null;

        Node node = potionBoard[cell.x, cell.y];
        return node != null && node.isUsable ? node.potion : null;
    }

    private void FinishPointerSwap()
    {
        firstSelectedPotion = null;
        secondSelectedPotion = null;
        waitForPointerRelease = true;
    }

    // do swap
    private void DoSwap(Potion _currentPotion, Potion _targetPotion)
    {
        Potion temp = potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion;
        potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion = potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion;
        potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion = temp;

        int tempXIndex = _currentPotion.xIndex;
        int tempYIndex = _currentPotion.yIndex;
        _currentPotion.xIndex = _targetPotion.xIndex;
        _currentPotion.yIndex = _targetPotion.yIndex;
        _targetPotion.xIndex = tempXIndex;
        _targetPotion.yIndex = tempYIndex;

        Vector2 currentCellCenter = CellToWorld(new Vector2Int(_currentPotion.xIndex, _currentPotion.yIndex));
        Vector2 targetCellCenter = CellToWorld(new Vector2Int(_targetPotion.xIndex, _targetPotion.yIndex));

        // Aynı türden iki özel taş birleşiyorsa ikisi de AYNI noktaya gider ve
        // orada tam üst üste biner; ikincisi gizlenince tek taş kalmış gibi
        // görünür. Kendi hücrelerine gitselerdi hiç örtüşmezlerdi.
        //
        // Buluşma noktası türe göre değişiyor. Bomba iki hücrenin ORTASINDA
        // patlıyor. Roket ise hayatta kalan roketin HÜCRESİNDE birleşiyor,
        // çünkü uçan parçalar hücre merkezinden çıkmak zorunda; ortada
        // buluşsalardı animasyon biter bitmez yarım hücre zıplarlardı.
        if (_currentPotion.potionType == _targetPotion.potionType &&
            IsSpecial(_currentPotion))
        {
            Vector2 meetPoint = _currentPotion.potionType == PotionType.Bomb
                ? (currentCellCenter + targetCellCenter) * 0.5f
                : currentCellCenter;

            currentCellCenter = meetPoint;
            targetCellCenter = meetPoint;
        }

        Vector3 currentTarget = new Vector3(currentCellCenter.x, currentCellCenter.y, _currentPotion.transform.position.z);
        Vector3 targetTarget = new Vector3(targetCellCenter.x, targetCellCenter.y, _targetPotion.transform.position.z);

        // Düşüş sırasında swap başlasa bile ara transform konumlarını hedef yapma.
        // Grid, taşların tek doğruluk kaynağıdır; iki taş da yeni hücre merkezine gider.
        // Takasta iki taş da arkasında iz bırakır.
        _currentPotion.MoveToTarget(currentTarget, showSmoke: true);
        _targetPotion.MoveToTarget(targetTarget, showSmoke: true);

    }

    // IEnumerator ProcessMatches:
    private IEnumerator ProcessMatches(Potion _currentPotion, Potion _targetPotion)
    {
        // İki bomba birleşiyor: ikincisi süper bombayı besler ama kendisi patlamaz.
        // Takas hareketi başlar başlamaz ekrandan kalkar, süper bombanın yanında
        // durup duruyor gibi görünmesin. Board'da kaldığı için 7x7 taraması onu
        // diğer taşlarla aynı anda havuza yollar.
        // İkinci özel taşı burada GİZLEMİYORUZ. Erken gizlenirse oyuncu iki
        // taşın buluştuğunu göremiyor; gizleme birleşme animasyonunu başlatan
        // rutinlere taşındı (SuperBombExplod, DoubleRocketExplode).
        yield return new WaitUntil(() =>
            !_currentPotion.isMoving &&
            !_targetPotion.isMoving
        );

        // Takas edilen taşlardan biri özel mi? Roket önceliklidir: bombayla
        // takas edilirse roket süpürür, yoldaki bombayı zaten zincire alır.
        Potion specialToTrigger = null;

        if (IsSpecial(_currentPotion)) specialToTrigger = _currentPotion;
        if (IsSpecial(_targetPotion)) specialToTrigger = _targetPotion;

        if (_currentPotion.potionType == PotionType.Rocket) specialToTrigger = _currentPotion;
        if (_targetPotion.potionType == PotionType.Rocket) specialToTrigger = _targetPotion;

        if (_currentPotion.potionType == PotionType.Bomb && _targetPotion.potionType == PotionType.Bomb)
        {
            // Bomba patlaması, refill ve cascade tamamen bitsin.
            yield return SuperBombExplod(_currentPotion, _targetPotion);

            GameManager.Instance.ProcessTurn(0, true);

            // Normal CheckBoard ve geri swap çalışmasın.
            yield break;
        }
        else if (_currentPotion.potionType == PotionType.Rocket &&
                 _targetPotion.potionType == PotionType.Rocket)
        {
            // Artı roket: birleşme animasyonu, patlama, refill ve cascade bitsin.
            yield return DoubleRocketExplode(_currentPotion, _targetPotion);

            GameManager.Instance.ProcessTurn(0, true);

            // Normal CheckBoard ve geri swap çalışmasın.
            yield break;
        }
        else if (specialToTrigger != null)
        {
            // Patlama/süpürme zinciri, refill ve cascade tamamen bitsin.
            yield return ExplodeChain(specialToTrigger);

            GameManager.Instance.ProcessTurn(0, true);

            // Normal CheckBoard ve geri swap çalışmasın.
            yield break;
        }

        currentState = BoardState.Checking;

        // Takasın geçerliliğine YALNIZCA takas edilen iki taş karar verir.
        // CheckBoard board'un tamamına bakıyor; refill cascade'i sürerken
        // başka bir yerdeki eşleşme, hiçbir şey yapmayan takası geçerli gösteriyordu.
        if (!SwapCreatesMatch(_currentPotion, _targetPotion))
        {
            currentState = BoardState.Swapping;
            DoSwap(_currentPotion, _targetPotion);

            yield return new WaitUntil(() =>
                !_currentPotion.isMoving &&
                !_targetPotion.isMoving
            );

            currentState = BoardState.Idle;
            yield break;
        }

        bool hasMatched = CheckBoard(true, _currentPotion);

        while (hasMatched)
        {
            currentState = BoardState.Clearing;

            List<MatchResult> matchGroups = new List<MatchResult>(currentMatchGroups);

            yield return RemoveAndRefill(matchGroups);

            currentState = BoardState.Checking;
            hasMatched = CheckBoard(true);
        }

        GameManager.Instance.ProcessTurn(10, true);
        currentState = BoardState.Idle;
    }

    // Takas edilen taşlardan biri 3'lü bir diziye girdi mi?
    // Board'un geri kalanı bilerek yok sayılır.
    private bool SwapCreatesMatch(Potion _currentPotion, Potion _targetPotion)
    {
        // IsConnected komşuları tararken isMatched'a bakıyor;
        // önceki turdan kalmış bayraklar taramayı erken kesmesin.
        foreach (Node node in potionBoard)
        {
            if (node.potion != null) node.potion.isMatched = false;
        }

        return IsPartOfMatch(_currentPotion) || IsPartOfMatch(_targetPotion);
    }

    private bool IsPartOfMatch(Potion potion)
    {
        if (potion == null) return false;

        return IsConnected(potion).connectedPotions.Count >= 3;
    }

    private static bool IsSpecial(Potion potion)
    {
        return potion.potionType == PotionType.Bomb || potion.potionType == PotionType.Rocket;
    }

    //IsAdjacent
    private bool IsAdjacent(Potion _currentPotion, Potion _targetPotion)
    {
        return Mathf.Abs(_currentPotion.xIndex - _targetPotion.xIndex) + Mathf.Abs(_currentPotion.yIndex - _targetPotion.yIndex) == 1;
    }

    #endregion

}

public class MatchResult
{
    public List<Potion> connectedPotions;
    public MatchDirection direction;
    public Potion protectedPotion;

    public bool IsSuperMatch =>
        direction == MatchDirection.LongHorizontal ||
        direction == MatchDirection.LongVertical ||
        direction == MatchDirection.Super;
}

public enum MatchDirection
{
    Vertical,
    Horizontal,
    LongVertical,
    LongHorizontal,
    Super,
    None
}

public enum BoardState
{
    Initializing,
    Idle,
    Swapping,
    Checking,
    Clearing,
    Refilling
}
