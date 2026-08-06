using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonControl : MonoBehaviour
{
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
