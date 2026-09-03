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
    [SerializeField] private GameObject potionParentGO;
    private List<GameObject> potionToDestroy = new();
    private readonly List<MatchResult> currentMatchGroups = new();

    [SerializeField] private BoardState currentState = BoardState.Initializing;

    private List<GameObject> deactivePotionPool = new();
    private bool waitForPointerRelease = false;



    //get a reference to the collection nodes potionBoard + GO

    private Potion firstSelectedPotion;

    [SerializeField] private Potion secondSelectedPotion;
    [SerializeField] private ParticleSystem destroyParticlesRed;
    [SerializeField] private ParticleSystem destroyParticlesBlue;
    [SerializeField] private ParticleSystem destroyParticlesGreen;
    [SerializeField] private ParticleSystem destroyParticlesPurple;
    [SerializeField] private ParticleSystem explodingPaticles;

    // Süper bombanın kendi patlama efekti. Atanmazsa explodingPaticles kullanılır.
    [SerializeField] private ParticleSystem superExplodingParticles;
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

    // İki bomba birleşirken ikincisinin kaybolması için takas başladıktan sonra
    // beklenen süre. Takas hareketi 0.12 sn sürüyor; bunun altında tutulmalı.
    [SerializeField, Min(0f)] private float mergedBombHideDelay = 0.05f;

    // Süper bomba patlayıp taşları temizledikten sonra, üstteki taşlar düşmeye
    // başlamadan önce beklenen süre.
    [SerializeField, Min(0f)] private float explosionSettleDelay = 0.25f;

    // Süper bomba patlaması merkezden dışa doğru halka halka ilerler;
    // iki halka arasında beklenen süre.
    [SerializeField, Min(0f)] private float superBombRingDelay = 0.06f;

    // Roket parçalarının satır boyunca ilerleme hızı (birim/sn).
    // 0 olamaz: parçalar ilerlemezse süpürme döngüsü hiç bitmez.
    [SerializeField, Min(0.1f)] private float rocketSpeed = 12f;

    // Puanlama: her eşleşme/patlama olayı anında puan verir (cascade dahil).
    [SerializeField] private int matchPoints = 10;
    [SerializeField] private int superMatchPoints = 15;
    [SerializeField] private int bombPoints = 10;


    // Tahtanın görsel tilemap'inin yükleneceği Grid objesi.
    [SerializeField] private Transform boardGrid;

    //public static of potionboard
    public static PotionBoard Instance;


    private void Awake()
    {
        Instance = this;
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

                // if (potion.potionType == PotionType.Bomb && currentState == BoardState.Idle)
                // {
                //     waitForPointerRelease = true;

                //     StartCoroutine(BombExploding(potion));

                //     return;
                // }

                if (firstSelectedPotion == null)
                {
                    firstSelectedPotion = potion;
                    potion.setSelectedVisual(true);
                }


                if (hit.collider.gameObject.GetComponent<Potion>() != firstSelectedPotion)
                {
                    secondSelectedPotion = potion;
                }

                if (firstSelectedPotion != null && secondSelectedPotion != null)
                {
                    SwapPotion(firstSelectedPotion, secondSelectedPotion);
                }

            }
        }
        else
        {
            if (firstSelectedPotion != null)
            {
                firstSelectedPotion.setSelectedVisual(false);
            }

            firstSelectedPotion = null;
            secondSelectedPotion = null;
        }
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
                    if (potion == null) continue;

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
                    // Uzun YATAY eşleşme roket verir; diğer süper eşleşmeler bomba.
                    if (matchGroup.direction == MatchDirection.LongHorizontal)
                    {
                        item.Rocket(true);
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
                    item.MoveToTarget(matchGroup.protectedPotion.transform.position);
                    StartCoroutine(SuperMatchDestroy(item));
                }
                else
                {
                    StartCoroutine(ShrinkThenBreak(item));
                }
            }
        }

        yield return new WaitUntil(() => AreAllMatchedPotionsDestroyed(potionsToRemove));

        currentState = BoardState.Refilling;

        for (int x = 0; x < width; x++)
        {
            int dropOrder = 0;

            for (int y = 0; y < height; y++)
            {
                if (potionBoard[x, y].isUsable && potionBoard[x, y].potion == null)
                {
                    float startDelay = dropOrder * dropStaggerDelay;
                    RefillPotion(x, y, startDelay);
                    dropOrder++;
                }
            }
        }

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
    private IEnumerator ExplodeChain(Potion first)
    {
        currentState = BoardState.Clearing;

        Queue<SpecialTrigger> pending = new();
        HashSet<Vector2Int> triggered = new();

        pending.Enqueue(new SpecialTrigger(
            new Vector2Int(first.xIndex, first.yIndex), first.potionType, first));

        while (pending.Count > 0)
        {
            SpecialTrigger trigger = pending.Dequeue();

            // Aynı hücre ikinci kez tetiklenmesin.
            if (!triggered.Add(trigger.position)) continue;

            if (trigger.type == PotionType.Rocket)
            {
                yield return SweepRow(trigger, pending, triggered);
            }
            else
            {
                BlastAround(trigger.position, pending, triggered);
            }
        }

        yield return RefillAndCascade();
    }

    // Bomba: merkez dahil 3x3 alanı temizler.
    private void BlastAround(Vector2Int center, Queue<SpecialTrigger> pending, HashSet<Vector2Int> triggered)
    {
        Instantiate(explodingPaticles, CellToWorld(center), Quaternion.identity);
        explodingSource.PlayOneShot(explodingClip, explodingVolume);

        // Zincirdeki her patlama ayrı puan verir.
        GameManager.Instance.AddPoints(bombPoints);

        for (int xIndex = center.x - 1; xIndex <= center.x + 1; xIndex++)
        {
            for (int yIndex = center.y - 1; yIndex <= center.y + 1; yIndex++)
            {
                ClearCell(new Vector2Int(xIndex, yIndex), pending, triggered);
            }
        }
    }

    // Roket: iki parça merkezden dışa sabit hızla uçar, üzerinden geçtikleri
    // hücreyi temizler. Parçalar taşın çocuğu olduğu için taşın hücresi hemen
    // boşaltılır ama taş, süpürme bitene kadar havuza yollanmaz.
    private IEnumerator SweepRow(SpecialTrigger trigger, Queue<SpecialTrigger> pending, HashSet<Vector2Int> triggered)
    {
        Potion rocket = trigger.potion;
        int row = trigger.position.y;

        explodingSource.PlayOneShot(explodingClip, explodingVolume);
        GameManager.Instance.AddPoints(bombPoints);

        potionBoard[trigger.position.x, row] = new Node(true, null);

        Transform right = rocket != null ? rocket.RocketRight : null;
        Transform left = rocket != null ? rocket.RocketLeft : null;

        if (rocket != null) rocket.SplitRocket();

        Vector3 rightStart = right != null ? right.position : Vector3.zero;
        Vector3 leftStart = left != null ? left.position : Vector3.zero;

        int nextRight = trigger.position.x + 1;
        int nextLeft = trigger.position.x - 1;
        float travelled = 0f;

        while (nextRight < width || nextLeft >= 0)
        {
            travelled += rocketSpeed * Time.deltaTime;

            if (right != null) right.position = rightStart + Vector3.right * travelled;
            if (left != null) left.position = leftStart + Vector3.left * travelled;

            // Parçalar kaç hücre ilerledi? Geçilen her hücre temizlenir.
            int reached = Mathf.FloorToInt(travelled / cellSize);

            while (nextRight < width && nextRight <= trigger.position.x + reached)
            {
                ClearCell(new Vector2Int(nextRight, row), pending, triggered);
                nextRight++;
            }

            while (nextLeft >= 0 && nextLeft >= trigger.position.x - reached)
            {
                ClearCell(new Vector2Int(nextLeft, row), pending, triggered);
                nextLeft--;
            }

            yield return null;
        }

        if (rocket != null)
        {
            SpawnDestroyParticle(rocket);
            ReturnPotionToPool(rocket);
        }
    }

    // Tek hücre temizler; özel taş bulursa zincire ekler.
    // Roket havuza YOLLANMAZ — uçan parçaları için taşın yaşaması gerekiyor,
    // onu kuyruktan çıkınca SweepRow havuza döndürür.
    private void ClearCell(Vector2Int cell, Queue<SpecialTrigger> pending, HashSet<Vector2Int> triggered)
    {
        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= 8) return;

        Node node = potionBoard[cell.x, cell.y];

        if (node == null || !node.isUsable || node.potion == null) return;

        Potion potion = node.potion;

        // Havuz metodu tipi orijinaline döndüreceği için önce kaydedilir.
        PotionType type = potion.potionType;

        potionBoard[cell.x, cell.y] = new Node(true, null);

        if (type == PotionType.Rocket && !triggered.Contains(cell))
        {
            pending.Enqueue(new SpecialTrigger(cell, type, potion));
            return;
        }

        SpawnDestroyParticle(potion);
        ReturnPotionToPool(potion);

        if (type == PotionType.Bomb && !triggered.Contains(cell))
        {
            pending.Enqueue(new SpecialTrigger(cell, type, null));
        }
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2((cell.x - spacingX) * cellSize, (cell.y - spacingY) * cellSize);
    }

    // Patlama bittikten sonraki ortak kuyruk: boşalan hücreleri doldur,
    // yeni oluşan eşleşmeleri cascade et, tahtayı Idle'a bırak.
    // Hem zincir hem süper bomba buraya iner — eskiden ikisinde kopyalanmıştı.
    private IEnumerator RefillAndCascade()
    {
        currentState = BoardState.Refilling;

        for (int x = 0; x < width; x++)
        {
            int dropOrder = 0;

            for (int y = 0; y < height; y++)
            {
                if (potionBoard[x, y].isUsable && potionBoard[x, y].potion == null)
                {
                    RefillPotion(x, y, dropOrder * dropStaggerDelay);
                    dropOrder++;
                }
            }
        }

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

    private IEnumerator SuperBombExplod(Potion _targetPotion)
    {
        currentState = BoardState.Clearing;

        Vector2Int bombPosition = new Vector2Int(_targetPotion.xIndex, _targetPotion.yIndex);

        // Bomba ve gölgesi ayrı Animator'lerde duruyor; ikisi de aynı karede
        // başlamalı. Taşın altındaki her Animator'de aynı state adı aranır,
        // böylece ileride animasyonlu bir parça daha eklenirse kendiliğinden dahil olur.
        foreach (Animator animator in _targetPotion.GetComponentsInChildren<Animator>(true))
        {
            // Gölge yalnızca burada görünür; kapalı duran parça açılmadan Play işlemez.
            animator.gameObject.SetActive(true);

            animator.Play("SuperBomb", 0, 0f);
        }

        // Patlayan bomba diğer bombaların ve taşların önünde çizilsin.
        BombMaskBinder maskBinder = _targetPotion.GetComponentInChildren<BombMaskBinder>(true);

        if (maskBinder != null) maskBinder.LockToFront();

        // Partikülü animasyona bırakmıyoruz: obje zaten aktifse OnEnable tetiklenmez
        // ve Play On Awake çalışmaz. Baştan başlatmak için elle tetikliyoruz.
        ParticleSystem sparks = _targetPotion.GetComponentInChildren<ParticleSystem>(true);

        if (sparks != null)
        {
            sparks.gameObject.SetActive(true);
            sparks.Clear(true);
            sparks.Play(true);
        }

        yield return new WaitForSeconds(1.82f);


        Vector2 explosionPosition = new Vector2((bombPosition.x - spacingX) * cellSize, (bombPosition.y - spacingY) * cellSize);

        ParticleSystem explosionEffect = superExplodingParticles != null
            ? superExplodingParticles
            : explodingPaticles;

        Instantiate(explosionEffect, explosionPosition, Quaternion.identity);
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

    private void SpawnDestroyParticle(Potion item)
    {
        if (item.potionType == PotionType.Red)
        {
            Instantiate(destroyParticlesRed, item.transform.position, Quaternion.identity);
        }
        else if (item.potionType == PotionType.Blue)
        {
            Instantiate(destroyParticlesBlue, item.transform.position, Quaternion.identity);
        }
        else if (item.potionType == PotionType.Green)
        {
            Instantiate(destroyParticlesGreen, item.transform.position, Quaternion.identity);
        }
        else if (item.potionType == PotionType.Yellow)
        {
            Instantiate(destroyParticlesPurple, item.transform.position, Quaternion.identity);
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
            if (node.potion == null)
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
    private void RefillPotion(int x, int y, float startDelay)
    {
        // Kapalı hücre asla doldurulmaz.
        if (!potionBoard[x, y].isUsable) return;

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
        }

        if (y + yOffset == height)
        {
            SpawnPotionAtTop(x, startDelay);
        }
    }

    // SpawnPotionAtTop: RefillPotion method'unda üstteki potion'ları alt node'a indirdik fakat üst kısımda inecek potion kalmayınca bu methodu çağırıyoruz
    // İlk önce index adında bir değer oluştururuz ve ona FindIndexOfLowestNull methodundan gelen int değeri atarız, bu method sütundaki en alttaki boş node'un değerini bize verir
    // Yeni oluşacak iksirin yukarıdan aşağıya ne kaç birim hareket edeceğini hesaplarız height - index
    // yeni bir newPotion oluştururuz
    // sonra bu yeni poiton'nu poitonBoard iki boyutlu dizisine kayıt ederiz
    // Sonra Vector3 type'ında bir targetPos oluştururuz ve MoveToTarge methoduna veririz 
    private void SpawnPotionAtTop(int x, float startDelay)
    {
        int index = FindIndexOfLowestNull(x);

        if (index == 99) return;                   // sütunda doldurulacak açık hücre yok
        if (deactivePotionPool.Count == 0) return; // havuz boş — crash koruması

        int randomIndex = Random.Range(0, deactivePotionPool.Count);

        GameObject newPotionObject = deactivePotionPool[randomIndex];
        newPotionObject.transform.position = new Vector2((x - spacingX) * cellSize, (height - spacingY) * cellSize);

        newPotionObject.SetActive(true);
        deactivePotionPool.Remove(newPotionObject);
        Potion newPotion = newPotionObject.GetComponent<Potion>();
        newPotion.SetIndicies(x, index);
        potionBoard[x, index] = new Node(true, newPotion);
        Vector3 targetPos = new Vector3((x - spacingX) * cellSize, (index - spacingY) * cellSize, newPotionObject.transform.position.z);
        newPotion.MoveToDown(targetPos, startDelay);
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
                if (neighbourPotion == null) break;

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
        if (_targetPotion.transform.position.y >= 2) return;

        _currentPotion.setSelectedVisual(false);
        currentState = BoardState.Swapping;
        DoSwap(_currentPotion, _targetPotion);

        StartCoroutine(ProcessMatches(_currentPotion, _targetPotion));
        firstSelectedPotion = null;
        secondSelectedPotion = null;
        waitForPointerRelease = true;
    }

    // do swap
    private void DoSwap(Potion _currentPotion, Potion _targetPotion)
    {
        Vector3 currentPos = _currentPotion.transform.position;
        Vector3 targetPos = _targetPotion.transform.position;
        Potion temp = potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion;
        potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion = potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion;
        potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion = temp;

        int tempXIndex = _currentPotion.xIndex;
        int tempYIndex = _currentPotion.yIndex;
        _currentPotion.xIndex = _targetPotion.xIndex;
        _currentPotion.yIndex = _targetPotion.yIndex;
        _targetPotion.xIndex = tempXIndex;
        _targetPotion.yIndex = tempYIndex;

        _currentPotion.MoveToTarget(targetPos);
        _targetPotion.MoveToTarget(currentPos);

    }

    // IEnumerator ProcessMatches:
    private IEnumerator ProcessMatches(Potion _currentPotion, Potion _targetPotion)
    {
        // İki bomba birleşiyor: ikincisi süper bombayı besler ama kendisi patlamaz.
        // Takas hareketi başlar başlamaz ekrandan kalkar, süper bombanın yanında
        // durup duruyor gibi görünmesin. Board'da kaldığı için 7x7 taraması onu
        // diğer taşlarla aynı anda havuza yollar.
        if (_currentPotion.potionType == PotionType.Bomb &&
            _targetPotion.potionType == PotionType.Bomb)
        {
            yield return new WaitForSeconds(mergedBombHideDelay);

            _targetPotion.gameObject.SetActive(false);
        }

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
            yield return SuperBombExplod(_currentPotion);

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
