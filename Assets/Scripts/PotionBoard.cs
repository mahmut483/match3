using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PotionBoard : MonoBehaviour
{
    //define the size of the board
    public int width = 6;
    public int height = 8;
    //define some spacing for the board
    public float spacingX;
    public float spacingY;
    public float tileSpacing = 0.1f; // smaller tile spacing reduces potion gap
    // mobile-friendly potion scale
    public float potionParentScale = 0.5f;
    //get a reference to our potion prefabs
    public GameObject[] potionPrefabs;
    //get a reference to the collection nodes potionBoard + GO
    public Node[,] potionBoard;
    public GameObject potionBoardGO;
    private List<GameObject> potionsToDestroy = new();
    public GameObject potionParent;
    private Vector3 boardBasePosition;

    [SerializeField] private Potion selectedPotion;
    [SerializeField] private bool isProcessingMove;


    //layoutArray
    public ArrayLayout arrayLayout;
    //public static of potionboard
    public static PotionBoard Instance;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (potionParent != null)
        {
            potionParent.transform.localScale = Vector3.one * potionParentScale;
        }

        InitializeBoard();
    }

    private void Update()
    {
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.collider != null && hit.collider.gameObject.GetComponent<Potion>())
            {
                if (isProcessingMove) return;

                Potion potion = hit.collider.gameObject.GetComponent<Potion>();
                Debug.Log("I have a clicked a potion it is: " + potion.gameObject);

                SelectPotions(potion);
            }
        }
    }

    void InitializeBoard()
    {
        DestroyPotions();
        potionBoard = new Node[width, height];

        boardBasePosition = new Vector3(0f, -1.05f, 0f);
        spacingX = (float)(width - 1) / 2;
        spacingY = (float)(height - 1) / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 position = GetBoardPosition(x, y);
                if (arrayLayout.rows[y].row[x])
                {
                    potionBoard[x, y] = new Node(false, null);
                }
                else
                {
                    int randomIndex = Random.Range(0, potionPrefabs.Length);

                    GameObject potion = Instantiate(potionPrefabs[randomIndex], position, Quaternion.identity, potionParent.transform);
                    potion.transform.localScale = Vector3.one;
                    potion.GetComponent<Potion>().SetIndicies(x, y);
                    potionBoard[x, y] = new Node(true, potion);
                    potionsToDestroy.Add(potion);
                }
            }
        }

        if (CheckBoard(false))
        {
            Debug.Log("We have matches let's re-create the board");
            InitializeBoard();
        }
        else
        {
            Debug.Log("There are no matches, it's time to start the game!");
        }
    }

    private Vector3 GetBoardPosition(int x, int y)
    {
        return new Vector3((x - spacingX) * tileSpacing, (y - spacingY) * tileSpacing, 0f) + boardBasePosition;
    }

    private void DestroyPotions()
    {
        if (potionsToDestroy != null)
        {
            foreach (GameObject potion in potionsToDestroy)
                Destroy(potion);
            potionsToDestroy.Clear();
        }
    }


    public bool CheckBoard(bool _takeAction)
    {
        Debug.Log("Checking Board");
        bool hasMatched = false;

        List<Potion> potionsToRemove = new();
        
        foreach(Node nodePotion in potionBoard)
        {
            if (nodePotion.potion != null)
            {
                nodePotion.potion.GetComponent<Potion>().isMatched = false;
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                //checking if potion node is usable
                if (potionBoard[x, y].isUsable)
                {
                    //then proceed to get potion class in node.
                    Potion potion = potionBoard[x, y].potion.GetComponent<Potion>();

                    //ensure its not matched
                    if (!potion.isMatched)
                    {
                        //run some matching logic

                        MatchResult matchedPotions = IsConnected(potion);

                        if (matchedPotions.connectedPotions.Count >= 3)
                        {
                            MatchResult superMatchedPotions = SuperMatch(matchedPotions);

                            potionsToRemove.AddRange(matchedPotions.connectedPotions);

                            foreach (Potion pot in matchedPotions.connectedPotions)
                                pot.isMatched = true;

                            hasMatched = true;
                        }
                    }
                }
            }
        }

        if (_takeAction)
        {
            foreach (Potion potionToRemove in potionsToRemove)
            {
                potionToRemove.isMatched = false;
            }

            RemoveAndRefill(potionsToRemove);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ProcessTurn(potionsToRemove.Count, true);
            }

            if (CheckBoard(false))
            {
                CheckBoard(true);
            }
        }

        return hasMatched;
    }

    private void RemoveAndRefill(List<Potion> potionToRemove)
    {
        foreach (Potion potion in potionToRemove)
        {
            int _xIndex = potion.xIndex;
            int _yIndex = potion.yIndex;

            Destroy(potion.gameObject);

            potionBoard[_xIndex, _yIndex] = new Node(true, null);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (potionBoard[x, y].potion == null)
                {
                    Debug.Log("The location X: " + x + " Y: " + y + " is empty, attempting to refill it.");
                    RefillPotion(x, y);
                }
            }
        }
    }

    private void RefillPotion(int x, int y)
    {
        int yOffset = 1;

        while (y + yOffset < height && potionBoard[x, y + yOffset].potion == null)
        {
            Debug.Log("The potion above me is null, but i'm not at the top of the board yet, so add the my yOffset and try again. Current Offset is: " + yOffset + " I'm about to add 1.");
            yOffset++;
        }

        // we've either hit the top of the board or we foun a potion

        if (y + yOffset < height && potionBoard[x, y + yOffset].potion != null)
        {
            //we've found a potion

            Potion potionAbove = potionBoard[x, y + yOffset].potion.GetComponent<Potion>();

            //Move it to the correct location

            Vector3 targetPos = GetBoardPosition(x, y);
            Debug.Log("I've found a potion when refilling the board and it was in the location: [" + x + "," + (y + yOffset) + "] we have moved it to the location: [" + x + "i" + y + "]");
            // Move to location
            potionAbove.MoveToTarget(targetPos);
            // Update incidices
            potionAbove.SetIndicies(x, y);
            // Update out potionBoard
            potionBoard[x, y] = potionBoard[x, y + yOffset];
            // Set the location the potion came from to null
            potionBoard[x, y + yOffset] = new Node(true, null);

        }

        //if we've hit the top of the board without finding a potion
        if (y + yOffset == height)
        {
            Debug.Log("I've reached the top of the board without finding a potion");
            SpawnPotionAtTop(x);
        }

    }

    private void SpawnPotionAtTop(int x)
    {
        int index = FindIndexOfLowestNull(x);
        int locationToMoveTo = 8 - index;
        Debug.Log("About to spawn a potion, ideally i'd like to put it in the index of: " + index);
        // get a random potion
        int randomIndex = Random.Range(0, potionPrefabs.Length);
        GameObject newPotion = Instantiate(potionPrefabs[randomIndex], GetBoardPosition(x, height), Quaternion.identity, potionParent.transform);
        newPotion.transform.localScale = Vector3.one;
        // set indices
        newPotion.GetComponent<Potion>().SetIndicies(x, index);

        // set it on the potion board
        potionBoard[x, index] = new Node(true, newPotion);
        //move it to that location
        Vector3 targetPosition = GetBoardPosition(x, index);
        newPotion.GetComponent<Potion>().MoveToTarget(targetPosition);
    }

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

    private MatchResult SuperMatch(MatchResult _matchedResults)
    {
        if (_matchedResults.direction == MatchDirection.Horizontal || _matchedResults.direction == MatchDirection.LongHorizontal)
        {
            foreach (Potion pot in _matchedResults.connectedPotions)
            {
                List<Potion> extraConnectedPotions = new();

                CheckDirection(pot, new Vector2Int(0, 1), extraConnectedPotions);

                CheckDirection(pot, new Vector2Int(0, -1), extraConnectedPotions);

                if (extraConnectedPotions.Count >= 2)
                {
                    Debug.Log("I have a super Horizontal Match");
                    extraConnectedPotions.AddRange(_matchedResults.connectedPotions);

                    return new MatchResult
                    {
                        connectedPotions = extraConnectedPotions,
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
                List<Potion> extraConnectedPotions = new();

                CheckDirection(pot, new Vector2Int(1, 0), extraConnectedPotions);

                CheckDirection(pot, new Vector2Int(-1, 0), extraConnectedPotions);

                if (extraConnectedPotions.Count >= 2)
                {
                    Debug.Log("I have a super Vertical Match");
                    extraConnectedPotions.AddRange(_matchedResults.connectedPotions);

                    return new MatchResult
                    {
                        connectedPotions = extraConnectedPotions,
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

    MatchResult IsConnected(Potion potion)
    {
        List<Potion> connectedPotions = new();
        PotionType potionType = potion.potionType;

        connectedPotions.Add(potion);

        //check right
        CheckDirection(potion, new Vector2Int(1, 0), connectedPotions);
        //check left
        CheckDirection(potion, new Vector2Int(-1, 0), connectedPotions);
        //have we made a 3 match? (Horizontal Match)
        if (connectedPotions.Count == 3)
        {
            Debug.Log("I have a normal horizontal match, the color of my match is: " + connectedPotions[0].potionType);

            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.Horizontal
            };
        }
        //checking for more than 3 (Long horizontal Match)
        else if (connectedPotions.Count > 3)
        {
            Debug.Log("I have a Long horizontal match, the color of my match is: " + connectedPotions[0].potionType);

            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.LongHorizontal
            };
        }
        //clear out the connectedpotions
        connectedPotions.Clear();
        //readd our initial potion
        connectedPotions.Add(potion);

        //check up
        CheckDirection(potion, new Vector2Int(0, 1), connectedPotions);
        //check down
        CheckDirection(potion, new Vector2Int(0, -1), connectedPotions);

        //have we made a 3 match? (Vertical Match)
        if (connectedPotions.Count == 3)
        {
            Debug.Log("I have a normal vertical match, the color of my match is: " + connectedPotions[0].potionType);

            return new MatchResult
            {
                connectedPotions = connectedPotions,
                direction = MatchDirection.Vertical
            };
        }
        //checking for more than 3 (Long Vertical Match)
        else if (connectedPotions.Count > 3)
        {
            Debug.Log("I have a Long vertical match, the color of my match is: " + connectedPotions[0].potionType);

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

    void CheckDirection(Potion pot, Vector2Int direction, List<Potion> connectedPotions)
    {
        PotionType potionType = pot.potionType;
        int x = pot.xIndex + direction.x;
        int y = pot.yIndex + direction.y;

        //check that we're within the boundaries of the board
        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            if (potionBoard[x, y].isUsable)
            {
                Potion neighbourPotion = potionBoard[x, y].potion.GetComponent<Potion>();

                //does our potionType Match? it must also not be matched
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

    // select potion

    public void SelectPotions(Potion _potion)
    {
        if (selectedPotion == null)
        {
            Debug.Log(_potion);
            selectedPotion = _potion;
        }
        else if (selectedPotion == _potion)
        {
            selectedPotion = null;
        }
        else if (selectedPotion != _potion)
        {
            SwapPotion(selectedPotion, _potion);
            selectedPotion = null;
        }
    }
    // swap potion - logic
    private void SwapPotion(Potion _currentPotion, Potion _targetPotion)
    {
        if (!IsAdjacent(_currentPotion, _targetPotion))
        {
            return;
        }

        DoSwap(_currentPotion, _targetPotion);

        isProcessingMove = true;

        StartCoroutine(ProcessMatches(_currentPotion, _targetPotion));
    }

    // do swap
    private void DoSwap(Potion _currentPotion, Potion _targetPotion)
    {
        GameObject temp = potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion;

        potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion = potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion;
        potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion = temp;

        //Update indicies.
        int tempXIndex = _currentPotion.xIndex;
        int tempYIndex = _currentPotion.yIndex;
        _currentPotion.xIndex = _targetPotion.xIndex;
        _currentPotion.yIndex = _targetPotion.yIndex;
        _targetPotion.xIndex = tempXIndex;
        _targetPotion.yIndex = tempYIndex;

        _currentPotion.MoveToTarget(potionBoard[_targetPotion.xIndex, _targetPotion.yIndex].potion.transform.position);

        _targetPotion.MoveToTarget(potionBoard[_currentPotion.xIndex, _currentPotion.yIndex].potion.transform.position);
    }

    private IEnumerator ProcessMatches(Potion _currentPotion, Potion _targetPotion)
    {
        yield return new WaitForSeconds(0.2f);

        bool hasMatched = CheckBoard(true);

        if (!hasMatched)
        {
            DoSwap(_currentPotion, _targetPotion);
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

