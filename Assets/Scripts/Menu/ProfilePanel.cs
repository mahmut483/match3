using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Profil düzenleme paneli: isim ve avatar seçimi.
// Cihazda girişli olan kullanıcının Firestore kaydını günceller.
public class ProfilePanel : MonoBehaviour
{
    [Header("Alanlar")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button saveButton;

    // Sıra önemli: dizideki index = kullanıcının avatarIndex değeri.
    [SerializeField] private Button[] avatarButtons;

    [Header("Seçim görünümü")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1.2f;

    [Header("İsim kuralları")]
    [SerializeField] private int minNameLength = 3;
    [SerializeField] private int maxNameLength = 16;

    private int selectedAvatar;

    private void Awake()
    {
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            int index = i;

            if (avatarButtons[i] != null)
            {
                avatarButtons[i].onClick.AddListener(() => SelectAvatar(index));
            }
        }

        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (nameInput != null) nameInput.characterLimit = maxNameLength;
    }

    private void OnDestroy()
    {
        foreach (Button button in avatarButtons)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        if (saveButton != null) saveButton.onClick.RemoveListener(Save);
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
        selectedAvatar = Mathf.Clamp(index, 0, avatarButtons.Length - 1);

        for (int i = 0; i < avatarButtons.Length; i++)
        {
            if (avatarButtons[i] == null) continue;

            float scale = i == selectedAvatar ? selectedScale : normalScale;

            avatarButtons[i].transform.localScale = Vector3.one * scale;
        }
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
}
