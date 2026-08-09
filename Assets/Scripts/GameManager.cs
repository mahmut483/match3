using System;
using System.Collections;
using TMPro;
using UnityEngine;

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
    private bool isPlayedlast3MovesClip = false;

    public TMP_Text pointsTXT;
    public TMP_Text movesTXT;
    public TMP_Text goalTXT;

    [SerializeField] private GameObject outOfMovesPanel;
    [SerializeField] private Animator charAnimCtrl;
    [SerializeField] private GameObject confetti;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip last3MoveClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip lostClip;

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
        pointsTXT.text = points.ToString(); 
        movesTXT.text = moves.ToString(); 
        goalTXT.text = goal.ToString(); 
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
            charAnimCtrl.SetTrigger("Win");
            backgroundPanel.SetActive(true);
            confetti.SetActive(true);
            StartCoroutine(WaitForConfetti());
            audioSource.clip = winClip;
            audioSource.Play();
            return;
        }
        if (moves <= 3 && moves != 0 && !isPlayedlast3MovesClip)
        {
            charAnimCtrl.SetTrigger("LowMove");
            audioSource.clip = last3MoveClip;
            audioSource.Play();
            isPlayedlast3MovesClip = true;
        }
        else if (moves == 0)
        {
            // lose the game
            charAnimCtrl.SetTrigger("Lose");
            isGameEnded = true;
            backgroundPanel.SetActive(true);
            outOfMovesPanel.SetActive(true);
            audioSource.clip = lostClip;
            audioSource.Play();

            return;
        }
    }

    private IEnumerator WaitForConfetti()
    {
        yield return new WaitForSeconds(2);
        victoryPanel.SetActive(true);
    }
}
