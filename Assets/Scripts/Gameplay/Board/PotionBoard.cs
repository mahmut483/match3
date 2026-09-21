using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Match3.Gameplay.Potions;
using Match3.Gameplay.Session;
using Match3.Gameplay.Strikes;
using Match3.Levels;

namespace Match3.Gameplay.Board
{
    // Tahtanın orkestratörü: kurulum, takas → eşleşme → temizleme → refill → cascade
    // akışı, özel vuruşların başlatılması ve giriş kapıları. Grid, eşleşme bulma,
    // efektler, refill, zincirler ve sinematikler kendi sınıflarında.
    //
    // Takas KUYRUĞA ALINMAZ: her geçerli komut kendi çözümlemesini hemen başlatır,
    // refill veya cascade sürerken bile. Eşzamanlı çözümlemeler birbirini bozmaz,
    // çünkü tahtayı değiştiren bölümler ilk yield'den önce biter ve her zincir
    // kendi ChainContext'ini taşır.
    public class PotionBoard : MonoBehaviour
    {
        private int width => BoardDefinition.VisibleWidth;
        private int height => BoardDefinition.TotalHeight;
        private float cellSize = 0.575f;

        private BoardGeometry geometry;
        private BoardEffects effects;
        private BoardRefill refill;
        private SpecialChain specialChain;
        private StrikePresentation strikes;
        private BoardGrid grid;
        private MatchFinder matchFinder;

        // Tab bar'daki özel vuruşlar. Seçim ve desen hesabı orada; burası yalnızca
        // verilen hücreleri temizliyor.
        [SerializeField] private SpecialStrikes specialStrikes;

        // UI paneli açıkken tahta dokunuş almaz. GameBoardUI açar ve kapatır.
        public bool InputLocked { get; set; }

        [Tooltip("Normal eşleşme kırıldıktan sonra düşüş başlamadan önceki kısa bekleme.")]
        [SerializeField, Min(0f)] private float matchSettleDelay = 0.08f;

        // Taşlar korunan taşa doğru bu hızla uçar; süre alt ve üst sınıra
        // kırpılır. Takas 0.12 sn sürüyor, birleşme onun temposunda kalmalı:
        // bir hücrelik birleşme alt sınıra, uzak köşeler üst sınıra dayanır.
        [Header("Süper eşleşme birleşmesi")]
        [SerializeField, Min(0.01f)] private float superMatchMergeSpeed = 10f;
        [SerializeField, Min(0f)] private float superMatchMergeMinDuration = 0.08f;
        [SerializeField, Min(0f)] private float superMatchMergeMaxDuration = 0.2f;

        // Puanlama: her eşleşme/patlama olayı anında puan verir (cascade dahil).
        [SerializeField] private int matchPoints = 10;
        [SerializeField] private int superMatchPoints = 15;

        // Tahtanın görsel tilemap'inin yükleneceği Grid objesi.
        [SerializeField] private Transform boardGrid;

        [Header("Cannon Strike Presentation")]
        [Tooltip("Grid ve Potions'un ortak root'u. Yalnızca Cannon sinematiğinde hareket eder.")]
        [SerializeField] private Transform boardPresentation;

        // Bir vuruş (sinematik + zincir + refill) sürerken yeni vuruş ve dokunuş kabul edilmez.
        private bool isStrikeActive;

        public bool IsCannonPresentationActive => strikes.IsCannonCinematic;

        public event System.Action CannonStrikeFinished
        {
            add => strikes.CannonStrikeFinished += value;
            remove => strikes.CannonStrikeFinished -= value;
        }

        // Yardımcı bileşenler aynı objede; grid Start'ta kurulduğu için bağlama orada.
        private void Awake()
        {
            effects = GetComponent<BoardEffects>();
            effects.Initialize(boardPresentation);
            geometry = new BoardGeometry(cellSize, boardPresentation);
            refill = GetComponent<BoardRefill>();
            specialChain = GetComponent<SpecialChain>();
            strikes = GetComponent<StrikePresentation>();
        }

        // Level layout'u doğrular; geçersizse tahta kurulmaz ve bileşen kapanır.
        private void Start()
        {
            LevelData activeLevel = GameManager.Instance != null
                ? GameManager.Instance.ActiveLevel
                : null;
            string validationError = BoardDefinition.GetLayoutValidationError(
                activeLevel != null ? activeLevel.arrayLayout : null);

            if (validationError != null)
            {
                Debug.LogError($"PotionBoard cannot initialize: {validationError}", this);
                enabled = false;
                return;
            }

            InitializeBoard();
        }

        // BoardInput bu kapıya bakar; true ise tahta yeni dokunuş kabul eder.
        public bool AcceptsInput =>
            !(strikes.IsCannonCinematic && !strikes.IsAwaitingTarget) && !isStrikeActive && !InputLocked;

        public bool IsAwaitingCannonTarget => strikes.IsAwaitingTarget;

        public Potion PotionAt(Vector2Int cell) => grid.PotionAt(cell);

        // Komşu, duran ve hâlâ kendi hücresinde iki taşı takas eder. Özel vuruş
        // seçiliyken takas yok. false: girdi sessizce yok sayıldı (kuyruğa alınmaz).
        public bool TrySwap(Potion current, Potion target)
        {
            if (!IsAdjacent(current, target)) return false;
            if (specialStrikes != null && specialStrikes.IsArmed) return false;
            if (!CanSwapNow(current, target)) return false;

            current.SetSelectedVisual(false);
            BeginSwap(current, target);

            return true;
        }

        // Bırakılan dokunuş. Taş hâlâ tahtada değilse (roket ateşlendiğinde hücresi
        // hemen boşalır ama collider'ı dokunuş almaya devam eder) hiçbir şey olmaz.
        // Özel vuruş seçiliyse dokunuş ona gider; yoksa özel taş patlar — refill/
        // cascade sürerken de.
        public void TryTap(Potion tapped)
        {
            if (tapped == null || grid.PotionAt(new Vector2Int(tapped.XIndex, tapped.YIndex)) != tapped) return;

            bool usedStrike = specialStrikes != null && specialStrikes.TryUseOn(tapped);

            if (!usedStrike && IsSpecial(tapped)) StartCoroutine(TapDetonate(tapped));
        }

        // Dokunarak patlatma. Takasla aynı akış: zincir, refill ve cascade tamamen
        // bitene kadar sürer, sonunda bir hamle düşer.
        private IEnumerator TapDetonate(Potion special)
        {
            yield return specialChain.ExplodeChain(special);
            yield return RefillAndCascade();

            GameManager.Instance.ProcessTurn();
        }

        // Tahtayı aktif level'ın layout'undan kurar ve ilk taşları üretir.
        private void InitializeBoard()
        {
            LoadBoardTilemap();

            // Tahta şekli aktif level'ın ArrayLayout'undan okunur.
            ArrayLayout levelLayout = GameManager.Instance.ActiveLevel.arrayLayout;

            grid = new BoardGrid(levelLayout);
            matchFinder = new MatchFinder(grid);

            refill.Initialize(grid, geometry);
            specialChain.Initialize(grid, geometry, effects, ReturnPotionToPool);
            strikes.Initialize(grid, geometry, specialChain, effects, boardPresentation);
            refill.CreateInitialPotions();
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

        // Her eşleşme grubunu kendi türü ve hedef konumuyla temizler.
        // Böylece aynı turdaki normal ve süper eşleşmeler birbirine karışmaz.
        private IEnumerator RemoveAndRefill(List<MatchResult> matchGroups)
        {
            List<Potion> potionsToRemove = new();

            // Bu işlemin hâlâ kırılmakta olan taş sayısı. Taşın aktif olup olmadığına
            // bakılmaz: havuza dönen taşı eşzamanlı başka bir işlemin refill'i hemen
            // yeniden aktif edebilir ve aktifliğe bağlı bekleme hiç bitmezdi.
            SpecialChain.ChainContext pending = new();

            foreach (MatchResult matchGroup in matchGroups)
            {
                if (matchGroup.IsSuperMatch)
                {
                    effects.PlaySuperMatch();
                    GameManager.Instance.AddPoints(superMatchPoints);
                }
                else
                {
                    effects.PlayMatch();
                    GameManager.Instance.AddPoints(matchPoints);
                }

                foreach (Potion item in matchGroup.ConnectedPotions)
                {
                    if (item == null || potionsToRemove.Contains(item))
                    {
                        continue;
                    }
                    if (item == matchGroup.ProtectedPotion)
                    {
                        // Uzun yatay eşleşme yatay roket (satır), uzun dikey eşleşme dikey
                        // roket (sütun) verir. L/T biçimli süper eşleşme bomba olarak kalır.
                        if (matchGroup.Direction == MatchDirection.LongHorizontal ||
                            matchGroup.Direction == MatchDirection.LongVertical)
                        {
                            item.BecomeRocket(vertical: matchGroup.Direction == MatchDirection.LongVertical);

                            effects.SpawnRocketParticle(item);
                        }
                        else
                        {
                            item.BecomeBomb();
                        }

                        continue;
                    }

                    potionsToRemove.Add(item);
                    pending.running++;

                    int xIndex = item.XIndex;
                    int yIndex = item.YIndex;

                    grid.Clear(new Vector2Int(xIndex, yIndex));

                    if (matchGroup.IsSuperMatch)
                    {
                        item.MoveToTargetAtSpeed(
                            matchGroup.ProtectedPotion.transform.position,
                            superMatchMergeSpeed,
                            superMatchMergeMinDuration,
                            superMatchMergeMaxDuration);
                        StartCoroutine(SuperMatchDestroy(item, pending));
                    }
                    else
                    {
                        StartCoroutine(ShrinkThenBreak(item, pending));
                    }
                }
            }

            yield return new WaitUntil(() => pending.running == 0);

            // Normal match'te şimdiye kadar kırılma tamamlanır tamamlanmaz refill
            // başlıyordu. Çok kısa bu boşluk kırılma efektini okunur bırakır.
            if (matchSettleDelay > 0f)
            {
                yield return new WaitForSeconds(matchSettleDelay);
            }

            refill.StartRefill();

            // Cascade yalnızca tüm board yerleşince kontrol edilir. Gizli rezerv
            // satırında bile hareket varsa, o taş bir sonraki refill'de görünür
            // alana inebileceği için CheckBoard'u erken çalıştırmayız.
            yield return new WaitUntil(() => !grid.AnyPotionMoving());
        }

        // Özel vuruş: sinematik oynar, zincir biter, sonra tahta dolar. false dönmesi
        // UI'ın hakkı düşürmemesi ve seçimin açık kalması gerektiği anlamına gelir.
        public bool TryRunStrike(
            StrikeKind kind,
            Vector2Int origin,
            IEnumerable<Vector2Int> cells,
            Transform strikeSource = null)
        {
            if (kind != StrikeKind.Cannon && (strikes.IsCannonCinematic || isStrikeActive)) return false;

            switch (kind)
            {
                case StrikeKind.Cannon:
                    if (!strikes.CanFireCannon) return false;
                    StartCoroutine(RunStrike(strikes.FireCannon(origin)));
                    return true;

                case StrikeKind.Hammer:
                    if (cells == null || !strikes.HasHammer) return false;
                    StartCoroutine(RunStrike(strikes.PlayHammer(origin, cells, strikeSource)));
                    return true;

                case StrikeKind.Bomb:
                    if (!strikes.HasBomb) return false;
                    StartCoroutine(RunStrike(strikes.PlayBomb(origin, strikeSource)));
                    return true;
            }

            return false;
        }

        // Sinematik + zincir bitince tahta dolar; kilit ne olursa olsun bırakılır.
        private IEnumerator RunStrike(IEnumerator presentation)
        {
            isStrikeActive = true;

            try
            {
                yield return presentation;
                yield return RefillAndCascade();
            }
            finally
            {
                isStrikeActive = false;
            }
        }

        // Cannon butonu: board sinematik duruşa kayar, tek dokunuşla hedef satır beklenir.
        public bool TryBeginCannonAim()
        {
            if (isStrikeActive) return false;

            return strikes.TryBeginCannonAim();
        }

        // Hedef seçilmeden Cannon'a tekrar basmak nişanı iptal eder.
        public bool TryCancelCannonAim()
        {
            return strikes.TryCancelCannonAim();
        }

        // Patlama bittikten sonraki ortak kuyruk: boşalan hücreleri doldur,
        // yeni oluşan eşleşmeleri cascade et, tahtayı Idle'a bırak.
        // Hem zincir hem süper bomba buraya iner — eskiden ikisinde kopyalanmıştı.
        private IEnumerator RefillAndCascade()
        {
            refill.StartRefill();

            yield return new WaitUntil(() => !grid.AnyPotionMoving());

            List<MatchResult> matchGroups = matchFinder.FindAll();

            while (matchGroups.Count > 0)
            {
                yield return RemoveAndRefill(matchGroups);

                matchGroups = matchFinder.FindAll();
            }
        }

        // Normal eşleşmede taş anında kaybolmaz: önce hızlıca küçülür, sonra kırılır.
        // RemoveAndRefill zaten taşların kapanmasını beklediği için ayrı zamanlama gerekmez.
        private IEnumerator ShrinkThenBreak(Potion item, SpecialChain.ChainContext pending)
        {
            yield return item.ShrinkOut();

            effects.SpawnDestroyParticle(item);
            ReturnPotionToPool(item);
            pending.running--;
        }

        // Süper eşleşme: taş hayatta kalana varınca havuza döner; pending sayacı düşer.
        private IEnumerator SuperMatchDestroy(Potion item, SpecialChain.ChainContext pending)
        {
            yield return new WaitUntil(() => !item.IsMoving);

            ReturnPotionToPool(item);
            pending.running--;
        }

        // Temizlenen taş hedeflere sayılır (özel taş ayrıca kendi hedefine) ve havuza döner.
        private void ReturnPotionToPool(Potion item)
        {
            PotionType clearedType = item.PotionType;

            refill.Release(item);   // ClearSpecial tipi orijinaline döndürür

            GameManager.Instance.RegisterClearedPotion(item.PotionType);

            if (clearedType != item.PotionType)
            {
                GameManager.Instance.RegisterClearedPotion(clearedType);
            }
        }

        // Takas yalnızca iki taş da DURUYORSA ve ikisi de hâlâ kendi hücresindeyse
        // kabul edilir. Düşen ya da temizlenmekte olan bir taşa takas yapılamaz;
        // öyle bir girdi kuyruğa alınmaz, sessizce yok sayılır.
        private bool CanSwapNow(Potion currentPotion, Potion targetPotion)
        {
            if (currentPotion == null || targetPotion == null) return false;
            if (currentPotion.IsMoving || targetPotion.IsMoving) return false;

            return grid.PotionAt(new Vector2Int(currentPotion.XIndex, currentPotion.YIndex)) == currentPotion
                && grid.PotionAt(new Vector2Int(targetPotion.XIndex, targetPotion.YIndex)) == targetPotion;
        }

        // Takası uygular ve çözümlemeyi başlatır (testler de buradan girer).
        private void BeginSwap(Potion currentPotion, Potion targetPotion)
        {
            DoSwap(currentPotion, targetPotion);

            StartCoroutine(ProcessMatches(currentPotion, targetPotion));
        }

        // Grid'de ve dünyada takas. Aynı türden iki özel taş birleşiyorsa ikisi de
        // buluşma noktasına gider: bomba iki hücrenin ortasında, roket hayatta kalanın
        // hücresinde (uçan parçalar hücre merkezinden çıkmak zorunda).
        private void DoSwap(Potion _currentPotion, Potion _targetPotion)
        {
            grid.Swap(_currentPotion, _targetPotion);

            Vector2 currentCellCenter = geometry.CellToWorld(new Vector2Int(_currentPotion.XIndex, _currentPotion.YIndex));
            Vector2 targetCellCenter = geometry.CellToWorld(new Vector2Int(_targetPotion.XIndex, _targetPotion.YIndex));

            // Aynı türden iki özel taş birleşiyorsa ikisi de AYNI noktaya gider ve
            // orada tam üst üste biner; ikincisi gizlenince tek taş kalmış gibi
            // görünür. Kendi hücrelerine gitselerdi hiç örtüşmezlerdi.
            //
            // Buluşma noktası türe göre değişiyor. Bomba iki hücrenin ORTASINDA
            // patlıyor. Roket ise hayatta kalan roketin HÜCRESİNDE birleşiyor,
            // çünkü uçan parçalar hücre merkezinden çıkmak zorunda; ortada
            // buluşsalardı animasyon biter bitmez yarım hücre zıplarlardı.
            if (_currentPotion.PotionType == _targetPotion.PotionType &&
                IsSpecial(_currentPotion))
            {
                Vector2 meetPoint = _currentPotion.PotionType == PotionType.Bomb
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
            _currentPotion.MoveToTarget(currentTarget);
            _targetPotion.MoveToTarget(targetTarget);

        }

        // Takas yerleşince: iki taştan biri özelse ilgili zincir, değilse takasın
        // eşleşmeleri; eşleşme yoksa geri takas ve hamle harcanmaz. Sonunda bir hamle düşer.
        private IEnumerator ProcessMatches(Potion _currentPotion, Potion _targetPotion)
        {
            // İki bomba birleşiyor: ikincisi süper bombayı besler ama kendisi patlamaz.
            // Takas hareketi başlar başlamaz ekrandan kalkar, süper bombanın yanında
            // durup duruyor gibi görünmesin. Board'da kaldığı için 7x7 taraması onu
            // diğer taşlarla aynı anda havuza yollar.
            // İkinci özel taşı burada GİZLEMİYORUZ. Erken gizlenirse oyuncu iki
            // taşın buluştuğunu göremiyor; gizleme birleşme animasyonunu başlatan
            // rutinlere taşındı (SuperBombExplod, DoubleRocketExplode).
            yield return new WaitUntil(() => !_currentPotion.IsMoving && !_targetPotion.IsMoving);

            // Takas edilen taşlardan biri özel mi? Roket önceliklidir: bombayla
            // takas edilirse roket süpürür, yoldaki bombayı zaten zincire alır.
            Potion specialToTrigger = null;

            if (IsSpecial(_currentPotion)) specialToTrigger = _currentPotion;
            if (IsSpecial(_targetPotion)) specialToTrigger = _targetPotion;

            if (_currentPotion.PotionType == PotionType.Rocket) specialToTrigger = _currentPotion;
            if (_targetPotion.PotionType == PotionType.Rocket) specialToTrigger = _targetPotion;

            bool bothBombs = _currentPotion.PotionType == PotionType.Bomb && _targetPotion.PotionType == PotionType.Bomb;
            bool bothRockets = _currentPotion.PotionType == PotionType.Rocket && _targetPotion.PotionType == PotionType.Rocket;

            // İki bomba süper bomba, iki roket artı roket, tek özel taş kendi zinciri.
            // Üçünde de patlama, refill ve cascade tamamen biter; normal CheckBoard
            // ve geri swap çalışmaz.
            IEnumerator specialResolution =
                bothBombs ? specialChain.SuperBombExplode(_currentPotion, _targetPotion)
                : bothRockets ? specialChain.DoubleRocketExplode(_currentPotion, _targetPotion)
                : specialToTrigger != null ? specialChain.ExplodeChain(specialToTrigger)
                : null;

            if (specialResolution != null)
            {
                yield return specialResolution;
                yield return RefillAndCascade();
            }
            else
            {
                // Takasın geçerliliğine YALNIZCA takas edilen iki taş karar verir ve
                // ilk temizleme de yalnızca onların gruplarını alır. Tahta geneli tarama
                // burada YANLIŞ: takas kendi iki taşı yerleşir yerleşmez çözülüyor, o
                // anda başka bir sütunda süren cascade'in henüz almadığı bir eşleşme
                // durabilir. Onu burada kapmak refill bitmeden birleşme başlatıyordu.
                // O eşleşme cascade'in işi, tahta durunca kendisi alacak.
                List<MatchResult> matchGroups = matchFinder.FindAround(_currentPotion, _targetPotion);

                if (matchGroups.Count == 0)
                {
                    // Geçersiz takas: geri al, hamle harcanmaz.
                    DoSwap(_currentPotion, _targetPotion);

                    yield return new WaitUntil(() => !_currentPotion.IsMoving && !_targetPotion.IsMoving);

                    yield break;
                }

                // İlk tur takasın grupları; sonraki turlar tahta durunca tüm tahta.
                while (matchGroups.Count > 0)
                {
                    yield return RemoveAndRefill(matchGroups);

                    matchGroups = matchFinder.FindAll();
                }
            }

            GameManager.Instance.ProcessTurn();
        }

        // Bomba veya roket mi?
        private static bool IsSpecial(Potion potion)
        {
            return potion.PotionType == PotionType.Bomb || potion.PotionType == PotionType.Rocket;
        }

        // Yatay ya da dikey komşu mu?
        private bool IsAdjacent(Potion _currentPotion, Potion _targetPotion)
        {
            return Mathf.Abs(_currentPotion.XIndex - _targetPotion.XIndex) + Mathf.Abs(_currentPotion.YIndex - _targetPotion.YIndex) == 1;
        }

    }
}
