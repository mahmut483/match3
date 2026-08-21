using UnityEngine;

// Oyundaki tüm avatarlar. Firestore'da yalnızca index saklanır,
// görselin kendisi burada durur (Storage'a gerek yok).
// Listedeki SIRA = kullanıcının avatarIndex değeri — sonradan araya ekleme yapmayın,
// yeni avatarları hep sona ekleyin.
[CreateAssetMenu(fileName = "AvatarCatalog", menuName = "Scriptable Objects/AvatarCatalog")]
public class AvatarCatalog : ScriptableObject
{
    [SerializeField] private Sprite[] avatars;

    public int Count => avatars != null ? avatars.Length : 0;

    public Sprite Get(int index)
    {
        if (avatars == null || avatars.Length == 0) return null;

        index = Mathf.Clamp(index, 0, avatars.Length - 1);

        return avatars[index];
    }
}
