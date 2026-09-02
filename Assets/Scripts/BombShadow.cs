using UnityEngine;

// Bombanın gölgesi. Bomb objesinin child'ı olarak kalır — Potion.Bomb() yalnızca
// o objeyi açıp kapattığı için gölge de bombayla birlikte görünüp kayboluyor.
// Ama Bomb klibi position/rotation/scale'in üçünü birden oynatıyor ve gölge
// hepsini miras alıyor. Burada hangilerinin geri alınacağı tek tek seçilir.
//
// Animator transform'u Update fazında yazıyor; düzeltmenin LateUpdate'te
// olması şart, yoksa animasyon üstüne yazar.
public class BombShadow : MonoBehaviour
{
    [Tooltip("Bombanın dönüşünü alma. Gölge yerde düz durur.")]
    [SerializeField] private bool ignoreRotation = true;

    [Tooltip("Bombanın ölçek animasyonunu alma. Gölge sabit boyutta kalır.")]
    [SerializeField] private bool ignoreScale = false;

    [Tooltip("Ignore Scale açıkken korunacak dünya ölçeği.")]
    [SerializeField] private Vector3 worldScale = Vector3.one;

    [Tooltip("Gölgenin sabit yüksekliğini belirleyen obje — genelde taşın kökü. " +
             "Boş bırakılırsa gölge bombanın dikey hareketini de takip eder.")]
    [SerializeField] private Transform groundReference;

    [SerializeField] private float groundOffset;

    private void LateUpdate()
    {
        if (ignoreRotation)
        {
            transform.rotation = Quaternion.identity;
        }

        if (ignoreScale)
        {
            Vector3 parentScale = transform.parent.lossyScale;

            // Animasyon ölçeği 0'a indirirse bölme patlar.
            transform.localScale = new Vector3(
                parentScale.x != 0f ? worldScale.x / parentScale.x : 0f,
                parentScale.y != 0f ? worldScale.y / parentScale.y : 0f,
                parentScale.z != 0f ? worldScale.z / parentScale.z : 0f);
        }

        if (groundReference != null)
        {
            Vector3 position = transform.position;
            position.y = groundReference.position.y + groundOffset;
            transform.position = position;
        }
    }
}
