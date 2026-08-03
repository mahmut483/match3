using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PotionBoard : MonoBehaviour
{
    // Değerler 11
    //define the size of the board
    public int width = 6;
    public int height = 8;
    //define some spacing for the board
    public float spacingX;
    public float spacingY;
    //get a reference to our potion prefabs
    public GameObject[] potionPrefabs;
    public Node[,] potionBoard;
    public GameObject potionParent;
    public GameObject potionParentGO;
    public List<GameObject> potionToDestroy = new();

    //get a reference to the collection nodes potionBoard + GO
    
    [SerializeField] private bool isProcessingMove;
    [SerializeField] private Potion selectedPotion;
    [SerializeField] private ParticleSystem destroyParticlesRed;
    [SerializeField] private ParticleSystem destroyParticlesBlue;
    [SerializeField] private ParticleSystem destroyParticlesGreen;
    [SerializeField] private ParticleSystem destroyParticlesPurple;
    

    //layoutArray
    public ArrayLayout arrayLayout;
    //public static of potionboard
    public static PotionBoard Instance;

    
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        InitializeBoard();
    }

    // Ray ile hangi position'a tıkladığını alırız sonra if kontrollerini yaparız sonra tıkladığımız potion'ı bir referansa kaydederiz.
    private void Update()
    {
        if (Pointer.current.press.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.collider != null && hit.collider.gameObject.GetComponent<Potion>())
            {
                if(isProcessingMove) return;

                Potion potion = hit.collider.gameObject.GetComponent<Potion>();

                SelectPotions(potion);
            }
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
    // Eşleşme kontrolü eşleşme var ise tekrar InitializeBoard methodu çağrılır
    private void InitializeBoard()
    {
        DestroyPotions();

        spacingX = (float)(width - 1) / 2;
        spacingY = (float)((height) / 2) + 2;

        potionBoard = new Node[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 position = new Vector2((x - spacingX) / 1.5f, (y - spacingY) / 1.5f );

                if (arrayLayout.rows[y].row[x])
                {
                    potionBoard[x, y] = new Node(false, null);
                }
                else
                {
                    int randomIndex = Random.Range(0, potionPrefabs.Length);

                    GameObject potion = Instantiate(potionPrefabs[randomIndex], position, Quaternion.identity);
                    potion.transform.SetParent(potionParent.transform);
                    potion.GetComponent<Potion>().SetIndicies(x, y);
                    potionToDestroy.Add(potion);
                    potionBoard[x, y] = new Node(true, potion);

                }
            }
        }

        if (CheckBoard(false))
        {
            InitializeBoard();
        }
    }
    // DestroyPotions: PotionToDestroy List dolu ise listedeki tüm elemanları gezer destroy ederiz sonra listeyi temizleriz
    private void DestroyPotions()
    {
        if (potionToDestroy.Count >= 1)
        {
            foreach (GameObject item in potionToDestroy)
            {
                Destroy(item);
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
    private bool CheckBoard(bool _takeAction)
    {

        bool hasMatched = false;

        List<Potion> potionsToRemove = new();

        foreach (Node item in potionBoard)
        {
            if (item.potion != null)
            {
                item.potion.GetComponent<Potion>().isMatched = false;
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (potionBoard[x, y].isUsable)
                {
                    Potion potion = potionBoard[x, y].potion.GetComponent<Potion>();

                    if (!potion.isMatched)
                    {

                       MatchResult matchedPotions = IsConnected(potion);

                        if (matchedPotions.connectedPotions.Count >= 3)
                        {
                            
                            MatchResult superMatch = SuperMatch(matchedPotions);

                            potionsToRemove.AddRange(superMatch.connectedPotions);

                            foreach (Potion item in superMatch.connectedPotions)
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
            foreach (Potion item in potionsToRemove)
            {
                item.isMatched = false;
            }

            StartCoroutine(RemoveAndRefill(potionsToRemove));

            
        }

        return hasMatched;
    }

    // RemoveAndRefill: İçerisinde Potion type'ında değerler tutan bir list'i parametre olarak alıyoruz.
    // Parametre olarak aldığımız list foreach ile tüm elemanlarının x ve y index'lerini bir referansa kaydediyoruz
    // Parametre olarak aldığımız listenin tüm elemanlarını destroy ediyoruz 
    // Kaydettiğimiz x ve y indexlerini potionBoard'a kaydediyoruz 
    // Tüm potionBoard'u geziyoruz ve null olan potion board'ları RefillPotion methoduna parametere olarak gönderiyoruz. 
    private IEnumerator RemoveAndRefill(List<Potion> potionsToRemove)
    {

        foreach (Potion item in potionsToRemove)
        {
            int _xIndex = item.xIndex;
            int _yIndex = item.yIndex;

            Destroy(item.gameObject);
            Debug.Log("CheckDirection çalıştı! PotionType: " + item.potionType);
            if(item.potionType == PotionType.Red)
            {
                Instantiate(destroyParticlesRed, item.transform.position, Quaternion.identity);
            }else if (item.potionType == PotionType.Blue)
            {
                Instantiate(destroyParticlesBlue, item.transform.position, Quaternion.identity);
            }else if (item.potionType == PotionType.Green)
            {
                Instantiate(destroyParticlesGreen, item.transform.position, Quaternion.identity);
            }else if (item.potionType == PotionType.Purple)
            {
                Instantiate(destroyParticlesPurple, item.transform.position, Quaternion.identity);
            }
            

            potionBoard[_xIndex, _yIndex] = new Node(true, null);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (potionBoard[x, y].potion == null)
                {
                    RefillPotion(x, y);
                }
            }
        }

        yield return new WaitForSeconds(1.2f);

        if (CheckBoard(false))
            {
                CheckBoard(true);
            }
    }
    
    
    // RefillPotion: ilk başta bir while döngüsü ile üst cell'leri tararız, board'un dışında değilse ve node null ise yOffset'i 1 arttırırız
    // İf ile board'un içinde ve potion içeriği null olmayan bir node var mı kontrol ederiz
    // if koşulu true döndüğünde ilgili poiton'un referansını alırız ve bir Vector3 targetPos belirleriz
    // Aldığımız potion'u MoveToTarget method'u ile targetPos'a akışını sağlarız
    // SetIndicies ile potion'un kendi belleğinde tutuğu konumunu güncelleriz 
    // Sonra potionBoad ile potion'un bulunduğu node'u boş node'a atarız
    // Sonra kayan potion'un eski konumunu null oolarka güncelleriz
    // Bir if kontrolü ile Board'un en üstünde isek SpawnPotionAtTop methodunu çağırırız
    private void RefillPotion(int x, int y)
    {
        int yOffset = 1;

        while (y + yOffset < height && potionBoard[x, y + yOffset].potion == null)
        {
            yOffset++;
        }

        if (y + yOffset < height && potionBoard[x, y + yOffset].potion != null)
        {
            Potion potion = potionBoard[x, y + yOffset].potion.GetComponent<Potion>();

            Vector3 targetPos = new Vector3((x - spacingX) / 1.5f, (y - spacingY) / 1.5f, potion.transform.position.z);

            potion.SetIndicies(x, y);

            potion.MoveToDown(targetPos);

            potionBoard[x, y] = potionBoard[x, y + yOffset];
            potionBoard[x, y + yOffset] = new Node(true, null);
        }

        if (y + yOffset == height)
        {
            SpawnPotionAtTop(x);
        }
    }

    // SpawnPotionAtTop: RefillPotion method'unda üstteki potion'ları alt node'a indirdik fakat üst kısımda inecek potion kalmayınca bu methodu çağırıyoruz
    // İlk önce index adında bir değer oluştururuz ve ona FindIndexOfLowestNull methodundan gelen int değeri atarız, bu method sütundaki en alttaki boş node'un değerini bize verir
    // Yeni oluşacak iksirin yukarıdan aşağıya ne kaç birim hareket edeceğini hesaplarız height - index
    // yeni bir newPotion oluştururuz
    // sonra bu yeni poiton'nu poitonBoard iki boyutlu dizisine kayıt ederiz
    // Sonra Vector3 type'ında bir targetPos oluştururuz ve MoveToTarge methoduna veririz 
    private void SpawnPotionAtTop(int x)
    {
        int index = FindIndexOfLowestNull(x);
        int locationToMoveTo = height - index;

        int randomIndex = Random.Range(0, potionPrefabs.Length);

        GameObject newPotion = Instantiate(potionPrefabs[randomIndex], new Vector2((x - spacingX) / 1.5f, (height - spacingY) / 1.5f), Quaternion.identity);
        newPotion.transform.SetParent(potionParent.transform);
        newPotion.GetComponent<Potion>().SetIndicies(x, index);
        potionBoard[x, index] = new Node(true, newPotion);
        Vector3 targetPos = new Vector3((x - spacingX) / 1.5f, (index - spacingY) / 1.5f, newPotion.transform.position.z);
        newPotion.GetComponent<Potion>().MoveToDown(targetPos);
    }

    // FindIndexOfLowestNull: Belirli bir sütundaki en aşağıda bulunan null node'un değerini döndürür
    // lowestNull adınad bir değer oluşturulur
    // aynı sütun içerisinde aşağıya doğru node'ları tarayan bir for yazılır
    // Eğer potion == null olan değer varsa y değeri lowestNull'a atanır 
    // lowestNull return edilir
    private int FindIndexOfLowestNull(int x)
    {
        int lowestNull = 99;

        for (int y = 7; y >= 0; y--)
        {
            if (potionBoard[x, y].potion == null)
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
        }else if (_matchedResults.direction == MatchDirection.Vertical || _matchedResults.direction == MatchDirection.LongVertical)
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
        
        int x = pot.xIndex + direction.x;
        int y = pot.yIndex + direction.y;

        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            if (potionBoard[x, y].isUsable)
            {
                Potion neighbourPotion = potionBoard[x, y].potion.GetComponent<Potion>();

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

    // SelectPotions: potion seçme ve selected'a kayıt etme methodu. Potion type'ında bir parametre alırız
    // ilk önce selectedPotion'ın null olup olmadığını kontrol eceriz, eğer null ise parametreyi selectedPotion'a atarız
    // else if selectedPotion'ın içinde aldığımız parametre var ise onu null yaparız 
    // else if Başka bir potion'a tıklanırsa SwapPotion methodu çağırılır ve selectedPotion'a null girilir
    private void SelectPotions(Potion _potion)
    {
        if (selectedPotion == null)
        {
            selectedPotion = _potion;
        }else if (selectedPotion == _potion)
        {
            selectedPotion = null;
        }
        else if(selectedPotion != _potion)
        {
            SwapPotion(selectedPotion, _potion);
            selectedPotion = null;
        }
    }
    // SwapPotion: _currentPotion ve _targetPotion adında Potion type'ında iki adet parametre alır
    // ilk başta bir if sorgusu ile currenPotion ve targetPotion'un isAdjacent true olduğunu kontrol ederiz(early exit) 
    // DoSwap method'du çağırılır, currentPotion ve targetPotion parametre olarak verilir
    // isProcessingMove true olarak güncellenir
    // coroutine başlatılır ve methoduna parametreler verilir
    private void SwapPotion(Potion _currenPotion, Potion _targetPotion)
    {
        if(!IsAdjacent(_currenPotion, _targetPotion)) return;

        DoSwap(_currenPotion, _targetPotion);

        isProcessingMove = true;

        StartCoroutine(ProcessMatches(_currenPotion, _targetPotion));
    }

    // do swap
    private void DoSwap(Potion _currentPotion, Potion _targetPotion)
    {
        GameObject temp = potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion;
        potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion = potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion;
        potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion = temp;

        int tempXIndex = _currentPotion.xIndex;
        int tempYIndex = _currentPotion.yIndex;
        _currentPotion.xIndex = _targetPotion.xIndex;
        _currentPotion.yIndex = _targetPotion.yIndex;
        _targetPotion.xIndex = tempXIndex;
        _targetPotion.yIndex = tempYIndex;

        _currentPotion.MoveToTarget(potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion.transform.position);
        _targetPotion.MoveToTarget(potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion.transform.position);
    }

    // IEnumerator ProcessMatches:
    private IEnumerator ProcessMatches(Potion _currentPotion, Potion _targePotion)
    {
        yield return new WaitForSeconds(1.2f);

        bool hasMatched = CheckBoard(true);

        if (!hasMatched)
        {
            DoSwap(_currentPotion, _targePotion);
        }
        else
        {
            GameManager.Instance.ProcessTurn(10, true);
        }

        isProcessingMove = false;
    }

    //IsAdjacent
    private bool IsAdjacent(Potion _currentPotion, Potion _targetPotion)
    {
        return Mathf.Abs(_currentPotion.xIndex - _targetPotion.xIndex) + Mathf.Abs(_currentPotion.yIndex - _targetPotion.yIndex) == 1;
    }

    //ProcessMatches

    #endregion

}

public class MatchResult
{
    public List<Potion> connectedPotions;
    public MatchDirection direction;
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

