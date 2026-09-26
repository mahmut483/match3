using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Menu
{
    // ◀ değer ▶ şeklindeki seçici. Pagination objesine eklenir.
    public class OptionSelector : MonoBehaviour
    {
        [Header("Seçenekler")]
        [Tooltip("Ekranda görünecek yazılar.")]
        [SerializeField] private string[] labels;

        [Tooltip("Her seçeneğin kaydedilecek sayısal karşılığı. Boş bırakılırsa sıra numarası kullanılır.")]
        [SerializeField] private int[] values;

        [Header("Referanslar")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text valueText;

        private int index;

        public int SelectedValue =>
            values != null && index < values.Length ? values[index] : index;

        private void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));

            Refresh();
        }

        // Uçlarda başa/sona sarar.
        private void Step(int direction)
        {
            if (labels == null || labels.Length == 0) return;

            index = (index + direction + labels.Length) % labels.Length;

            Refresh();
        }

        // Kayıtlı bir değere karşılık gelen seçeneği seçer (düzenleme ekranı için).
        public void SetValue(int value)
        {
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] != value) continue;

                    SetIndex(i);
                    return;
                }
            }

            SetIndex(value);
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
}
