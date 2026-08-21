using UnityEngine;
using UnityEngine.UI;

// Bir Image'ı girişli kullanıcının avatarıyla doldurur.
// Profil butonu (PP) gibi yerlerde kullanılır; kullanıcı avatarını değiştirince kendini günceller.
[RequireComponent(typeof(Image))]
public class AvatarImage : MonoBehaviour
{
    [SerializeField] private AvatarCatalog catalog;

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        FirebaseBootstrap.UserReady += Apply;

        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        // Veri zaten gelmişse olayı bekleme.
        if (bootstrap != null && bootstrap.IsReady) Apply(bootstrap.User);
    }

    private void OnDisable()
    {
        FirebaseBootstrap.UserReady -= Apply;
    }

    private void Apply(UserData user)
    {
        if (catalog == null || user == null) return;

        Sprite sprite = catalog.Get(user.avatarIndex);

        if (sprite != null) image.sprite = sprite;
    }
}
