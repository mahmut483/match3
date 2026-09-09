using UnityEngine;

// Cannon giriş prefabının kod ile yaptığı tek sözleşme namlu ucudur. Animator,
// particle ve giriş hareketi prefabın kendi sorumluluğunda kalır.
public sealed class CannonEntryView : MonoBehaviour
{
    [Tooltip("Cannonball'ın üretileceği namlu ucu.")]
    [SerializeField] private Transform muzzle;

    public Transform Muzzle => muzzle;
}
