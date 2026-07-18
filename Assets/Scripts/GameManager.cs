using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance; // static reference

    public GameObject backgroundPanel; // grey background
    public GameObject victoryPanel;
    public GameObject losePanel;

    public int goal; // the amount of points you need to get to to win.
    public int moves; // the number of turns you can take
    public int points; // the current points you have earned.

    public bool isGameEnded;

    public TMP_Text pointsTXT;
    public TMP_Text movesTXT;
    public TMP_Text goalTXT;

    private void  Awake()
    {
        Instance = this;
    }

    public void Initialize(int _moves, int _goal)
    {
        moves = _moves;
        goal = _goal;
    }

    // Update is called once per frame
    void Update()
    {  
        pointsTXT.text = "Points: " + points.ToString(); 
        movesTXT.text = "Moves: " + moves.ToString(); 
        goalTXT.text = "Goal: " + goal.ToString(); 
    }

    public void ProcessTurn(int _pointsToGain, bool _subtractMoves)
    {
        points += _pointsToGain;

        if (_subtractMoves)
        {
            moves--;
        }

        if (points >= goal)
        {
            //you've won the game
            isGameEnded = true;

            //Display a victory screen
            backgroundPanel.SetActive(true);
            victoryPanel.SetActive(true);
            return;
        }
        if (moves == 0)
        {
            // lose the game

            isGameEnded = true;
            backgroundPanel.SetActive(true);
            losePanel.SetActive(true);
            return;
        }
    }

    // attached to a button to change scene when winning
    public void WinGame()
    {
        SceneManager.LoadScene(0);
    }

    // attached to a button to change scene when losing
    public void LoseGame()
    {
        SceneManager.LoadScene(0);
    }
}