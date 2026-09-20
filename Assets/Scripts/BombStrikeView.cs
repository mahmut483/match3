using UnityEngine;

// BombStrike prefabının animasyonu hareket root'undan bağımsızdır: dış root
// butondan hedef hücreye giderken Animator prefabın kendi dönüş/ölçek
// eğrilerini oynatır.
public class BombStrikeView : MonoBehaviour
{
    [SerializeField] private Animator bombAnimator;
    // Controller assetindeki state'in adı clip adı değil, "Bomb".
    // "BombStrike" çağrısı Animator.GotoState uyarısı verip clip'i güvenilir
    // biçimde yeniden başlatmıyordu.
    [SerializeField] private string strikeStateName = "Bomb";

    // Gerçekte oynayan clip'in süresini döndürür. PotionBoard bu süre dolmadan
    // Bomb Strike etkisini başlatmaz; Inspector'daki gecikme daha kısa kalsa
    // bile animasyon yarıda kesilmez.
    public float PlayStrike()
    {
        if (bombAnimator == null || string.IsNullOrEmpty(strikeStateName)) return 0f;

        bombAnimator.enabled = true;
        bombAnimator.Rebind();
        bombAnimator.Play(strikeStateName, 0, 0f);
        bombAnimator.Update(0f);

        AnimatorClipInfo[] clips = bombAnimator.GetCurrentAnimatorClipInfo(0);
        return clips.Length > 0 && clips[0].clip != null ? clips[0].clip.length : 0f;
    }
}
