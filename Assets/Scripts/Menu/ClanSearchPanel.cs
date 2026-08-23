using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SearchPage: isme göre clan arama.
public class ClanSearchPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchButton;
    [SerializeField] private Button clearButton;

    // Sonuçların gösterileceği liste (JoinPage'deki panelin aynısı olabilir
    // ya da SearchPage'e ait ayrı bir liste).
    [SerializeField] private ClanListPanel resultList;

    [Header("Clanları göster butonu")]
    [SerializeField] private Button viewClansButton;
    [SerializeField] private ClanTabButtons tabButtons;

    [Tooltip("Arama yapılmadan önce görünen öneri bloğu. Sonuç gelince gizlenir.")]
    [SerializeField] private GameObject suggestionBody;

    [SerializeField] private int searchLimit = 30;

    private void Awake()
    {
        if (searchButton != null) searchButton.onClick.AddListener(Search);
        if (clearButton != null) clearButton.onClick.AddListener(Clear);
        if (viewClansButton != null) viewClansButton.onClick.AddListener(ShowJoinPage);

        // Klavyeden Enter'a basınca da arasın.
        if (searchInput != null) searchInput.onSubmit.AddListener(_ => Search());
    }

    private void OnDestroy()
    {
        if (searchButton != null) searchButton.onClick.RemoveListener(Search);
        if (clearButton != null) clearButton.onClick.RemoveListener(Clear);
        if (viewClansButton != null) viewClansButton.onClick.RemoveListener(ShowJoinPage);
        if (searchInput != null) searchInput.onSubmit.RemoveAllListeners();
    }

    public void Search()
    {
        if (searchInput == null || resultList == null) return;

        // Boş aramada öneri bloğuna geri dön.
        if (string.IsNullOrWhiteSpace(searchInput.text))
        {
            ShowSuggestion(true);
            resultList.ClearResults();
            return;
        }

        ShowSuggestion(false);

        ClanService.SearchClans(searchInput.text, searchLimit, resultList.Show);
    }

    public void Clear()
    {
        if (searchInput != null) searchInput.text = "";

        ShowSuggestion(true);

        if (resultList != null) resultList.ClearResults();
    }

    // Öneri bloğu ile sonuç listesi aynı anda görünmez.
    private void ShowSuggestion(bool show)
    {
        if (suggestionBody != null) suggestionBody.SetActive(show);
    }

    private void OnEnable()
    {
        ShowSuggestion(true);
    }

    private void ShowJoinPage()
    {
        if (tabButtons != null) tabButtons.JoinPage();
    }
}
