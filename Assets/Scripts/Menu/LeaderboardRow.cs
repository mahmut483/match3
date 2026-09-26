using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Match3.Backend;

namespace Match3.Menu
{
    // Sıralama listesindeki tek satır. Hem listedeki satırlar hem de
    // alttaki "senin sıran" paneli bu bileşeni kullanır.
    //
    // Avatar satırın KENDİ oyuncusundan gelir ve burada yazılır. Satıra AvatarImage
    // bileşeni eklenemez: o bileşen giriş yapmış kullanıcının avatarını gösterir ve
    // UserReady olayında kendini tazeler — listedeki her satır aynı görünür, oyuncu
    // kendi profil fotoğrafını değiştirdiğinde hepsi birden değişirdi.
    public class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;

        [Tooltip("Satırdaki oyuncunun avatarı. Avatar göstermeyen satırlarda (kendi sıran paneli) boş bırakılabilir.")]
        [SerializeField] private Image avatarImage;

        [SerializeField] private AvatarCatalog avatarCatalog;

        // Satırın kime ait olduğu ve kaçıncı sırada durduğu. Panel, oyuncunun kendi
        // verisi değiştiğinde listeyi yeniden çekmeden bu satırı yerinde tazeliyor.
        public string Uid { get; private set; }
        public long Rank { get; private set; }

        public void Setup(long rank, UserData user)
        {
            if (user == null) return;

            Rank = rank;
            Uid = user.uid;

            if (rankText != null) rankText.text = rank + ".";
            if (nameText != null) nameText.text = user.displayName;
            if (levelText != null) levelText.text = user.highestCompletedLevel.ToString();

            if (avatarImage == null || avatarCatalog == null) return;

            Sprite sprite = avatarCatalog.Get(user.avatarIndex);

            if (sprite != null) avatarImage.sprite = sprite;
        }
    }
}
