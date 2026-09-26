using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Match3.Backend;

namespace Match3.Menu
{
    // Profil düzenleme paneli: isim ve avatar seçimi.
    // Cihazda girişli olan kullanıcının Firestore kaydını günceller.
    public class ProfilePanel : MonoBehaviour
    {
        [Header("Alanlar")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;

        [Header("Avatar pagination")]
        [SerializeField] private Image avatarPreview;
        [SerializeField] private Button previousAvatarButton;
        [SerializeField] private Button nextAvatarButton;

        // Listedeki sıra, veritabanında saklanan avatarIndex ile eşleşir.
        [SerializeField] private AvatarCatalog catalog;

        [Header("İsim kuralları")]
        [SerializeField] private int minNameLength = 3;
        [SerializeField] private int maxNameLength = 16;

        private int selectedAvatar;

        private void Awake()
        {
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (previousAvatarButton != null) previousAvatarButton.onClick.AddListener(ShowPreviousAvatar);
            if (nextAvatarButton != null) nextAvatarButton.onClick.AddListener(ShowNextAvatar);
            if (nameInput != null) nameInput.characterLimit = maxNameLength;

            UpdatePaginationButtons();
            SelectAvatar(0);
        }

        // Panel her açıldığında mevcut kullanıcı bilgileriyle doldurulur.
        private void OnEnable()
        {
            FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

            if (bootstrap == null || !bootstrap.IsReady)
            {
                FirebaseBootstrap.UserReady += Fill;
                return;
            }

            Fill(bootstrap.User);
        }

        private void OnDisable()
        {
            FirebaseBootstrap.UserReady -= Fill;
        }

        private void Fill(UserData user)
        {
            FirebaseBootstrap.UserReady -= Fill;

            if (nameInput != null) nameInput.text = user.displayName;

            SelectAvatar(user.avatarIndex);

            if (saveButton != null) saveButton.interactable = true;
        }

        private void SelectAvatar(int index)
        {
            if (catalog == null || catalog.Count == 0)
            {
                selectedAvatar = 0;
                return;
            }

            selectedAvatar = Mathf.Clamp(index, 0, catalog.Count - 1);

            if (avatarPreview != null)
            {
                avatarPreview.sprite = catalog.Get(selectedAvatar);
            }
        }

        private void ShowPreviousAvatar()
        {
            ChangeAvatar(-1);
        }

        private void ShowNextAvatar()
        {
            ChangeAvatar(1);
        }

        private void ChangeAvatar(int direction)
        {
            if (catalog == null || catalog.Count == 0) return;

            int nextIndex = (selectedAvatar + direction + catalog.Count) % catalog.Count;
            SelectAvatar(nextIndex);
        }

        private void UpdatePaginationButtons()
        {
            bool canPaginate = catalog != null && catalog.Count > 1;

            if (previousAvatarButton != null) previousAvatarButton.interactable = canPaginate;
            if (nextAvatarButton != null) nextAvatarButton.interactable = canPaginate;
        }

        private void Save()
        {
            FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogWarning("Firebase hazır değil, profil kaydedilemedi.");
                return;
            }

            string newName = nameInput != null ? nameInput.text.Trim() : "";

            if (newName.Length < minNameLength)
            {
                Debug.LogWarning($"İsim en az {minNameLength} karakter olmalı.");
                return;
            }

            // Kaydederken çift tıklamayı engelle.
            saveButton.interactable = false;

            bootstrap.UpdateProfile(newName, selectedAvatar, success =>
            {
                saveButton.interactable = true;

                // Başarılı da olsa olmasa da panel kapanır.
                if (!success) Debug.LogWarning("Profil kaydedilemedi.");

                gameObject.SetActive(false);
            });
        }

        private void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
