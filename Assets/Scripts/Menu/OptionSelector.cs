using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ◀ değer ▶ şeklindeki seçici. Pagination objesine eklenir.
// Butonlar ve yazı alt objelerden otomatik bulunur — elle sürüklemeye gerek yok.
// Hiyerarşideki İLK buton sol ok, İKİNCİ buton sağ ok kabul edilir.
public class OptionSelector : MonoBehaviour
{
    [Header("Seçenekler")]
    [Tooltip("Ekranda görünecek yazılar.")]
    [SerializeField] private string[] labels;

    [Tooltip("Her seçeneğin kaydedilecek sayısal karşılığı. Boş bırakılırsa sıra numarası kullanılır.")]
    [SerializeField] private int[] values;

    [Header("Referanslar (boş bırakılabilir)")]
    [Tooltip("Butonların sırası tersse buradan elle atayın.")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text valueText;

    public event Action Changed;

    private int index;

    public int SelectedIndex => index;

    public int SelectedValue =>
        values != null && index < values.Length ? values[index] : index;

    private void Awake()
    {
        AutoWire();

        if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));

        Refresh();
    }

    // Atanmamış referansları alt objelerden bulur.
    private void AutoWire()
    {
        if (prevButton == null || nextButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);

            if (prevButton == null && buttons.Length > 0) prevButton = buttons[0];
            if (nextButton == null && buttons.Length > 1) nextButton = buttons[1];

            if (buttons.Length < 2)
            {
                Debug.LogWarning($"{name}: Seçici için iki buton gerekiyor, {buttons.Length} bulundu.");
            }
        }

        if (valueText == null)
        {
            // Butonların içindeki yazıları atla, gerçek değer yazısını bul.
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponentInParent<Button>() != null) continue;

                valueText = text;
                break;
            }
        }

        if (valueText == null)
        {
            Debug.LogWarning($"{name}: Değer yazısı (TMP) bulunamadı.");
        }
    }

    private void OnDestroy()
    {
        if (prevButton != null) prevButton.onClick.RemoveAllListeners();
        if (nextButton != null) nextButton.onClick.RemoveAllListeners();
    }

    // Uçlarda başa/sona sarar.
    private void Step(int direction)
    {
        if (labels == null || labels.Length == 0) return;

        index = (index + direction + labels.Length) % labels.Length;

        Refresh();
        Changed?.Invoke();
    }

    public void SetIndex(int newIndex)
    {
        if (labels == null || labels.Length == 0) return;

        index = Mathf.Clamp(newIndex, 0, labels.Length - 1);
        Refresh();
    }

    private void Refresh()
    {
        if (valueText != null && labels != null && index < labels.Length)
        {
            valueText.text = labels[index];
        }
    }
}
