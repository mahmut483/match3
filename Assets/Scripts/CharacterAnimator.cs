using Spine.Unity;
using UnityEngine;

// HUD'daki karakterin Spine animasyonlarını yönetir.
// Idle sürekli döner; win/lose bir kez oynayıp son karesinde kalır.
public class CharacterAnimator : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation skeleton;

    [Header("Animasyon adları")]
    [SerializeField] private string idleAnimation = "IdleAnim";
    [SerializeField] private string winAnimation = "WinAnim";
    [SerializeField] private string loseAnimation = "LoseAnim";

    private void Awake()
    {
        if (skeleton == null) skeleton = GetComponent<SkeletonAnimation>();
    }

    private void Start()
    {
        PlayIdle();
    }

    public void PlayIdle()
    {
        Play(idleAnimation, true);
    }

    public void PlayWin()
    {
        PlayOnce(winAnimation);
    }

    public void PlayLose()
    {
        PlayOnce(loseAnimation);
    }

    // Bir kez oynatır ve son karesinde kalır — idle'a dönmez.
    private void PlayOnce(string animationName)
    {
        Play(animationName, false);
    }

    private bool Play(string animationName, bool loop)
    {
        if (skeleton == null || string.IsNullOrEmpty(animationName)) return false;

        skeleton.AnimationState.SetAnimation(0, animationName, loop);

        return true;
    }
}
