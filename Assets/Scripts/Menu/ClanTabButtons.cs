using UnityEngine;
using UnityEngine.UI;

// Clan sayfasındaki Join / Search / Create sekmeleri.
// Butona basınca ilgili sayfa açılır, diğerleri kapanır.
public class ClanTabButtons : MonoBehaviour
{
    [SerializeField] private GameObject joinPage;
    [SerializeField] private GameObject searchPage;
    [SerializeField] private GameObject createPage;

    [SerializeField] private Button joinButton;
    [SerializeField] private Button searchButton;
    [SerializeField] private Button createButton;

    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    // Butonlar koddan bağlanır — Inspector'daki OnClick listeleri BOŞ olmalı,
    // yoksa yanlış metoda bağlanma riski geri gelir.
    void Awake()
    {
        joinButton.onClick.AddListener(JoinPage);
        searchButton.onClick.AddListener(SearchPage);
        createButton.onClick.AddListener(CreatePage);
    }

    void OnDestroy()
    {
        joinButton.onClick.RemoveListener(JoinPage);
        searchButton.onClick.RemoveListener(SearchPage);
        createButton.onClick.RemoveListener(CreatePage);
    }

    // Clan sayfası her açıldığında Join sekmesine döner.
    void OnEnable()
    {
        JoinPage();
    }

    public void JoinPage()
    {
        joinPage.SetActive(true);
        searchPage.SetActive(false);
        createPage.SetActive(false);

        SetSelected(joinButton);
    }

    public void SearchPage()
    {
        joinPage.SetActive(false);
        searchPage.SetActive(true);
        createPage.SetActive(false);

        SetSelected(searchButton);
    }

    public void CreatePage()
    {
        joinPage.SetActive(false);
        searchPage.SetActive(false);
        createPage.SetActive(true);

        SetSelected(createButton);
    }

    // Seçili butona selectedSprite, diğerlerine normalSprite verir.
    private void SetSelected(Button selected)
    {
        joinButton.image.sprite = joinButton == selected ? selectedSprite : normalSprite;
        searchButton.image.sprite = searchButton == selected ? selectedSprite : normalSprite;
        createButton.image.sprite = createButton == selected ? selectedSprite : normalSprite;
    }
}
