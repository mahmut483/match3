using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ArrayLayout))]
public class CustPropertyDrawer : PropertyDrawer
{
    // Veri dizisi 15 satır (PotionBoard.height) — spawn alanı dahil.
    private const int RowCount = 15;
    // Inspector'da yalnızca görünür (oynanabilir) satırlar çizilir.
    // Üstteki spawn satırları hep açık kalır, göstermeye gerek yok.
    private const int VisibleRowCount = 8;
    private const int ColumnCount = 8;
    private const float CellHeight = 18f;

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.PrefixLabel(position, label);

        SerializedProperty data =
            property.FindPropertyRelative("rows");

        // Tüm satırların (gizli spawn satırları dahil) boyutunu garanti et,
        // yoksa PotionBoard okurken IndexOutOfRange fırlar.
        if (data.arraySize != RowCount)
        {
            data.arraySize = RowCount;
        }

        for (int j = 0; j < RowCount; j++)
        {
            SerializedProperty row = data
                .GetArrayElementAtIndex(j)
                .FindPropertyRelative("row");

            if (row.arraySize != ColumnCount)
            {
                row.arraySize = ColumnCount;
            }

            // Gizli spawn satırlarında kalmış eski işaretleri temizle —
            // görünmez blokaj spawn'ı sessizce bozar.
            if (j >= VisibleRowCount)
            {
                for (int i = 0; i < ColumnCount; i++)
                {
                    SerializedProperty cell = row.GetArrayElementAtIndex(i);

                    if (cell.boolValue)
                    {
                        cell.boolValue = false;
                    }
                }
            }
        }

        // Yalnızca görünür satırları, alttan yukarı (ekrandaki gibi) çiz.
        Rect newPosition = position;
        newPosition.y += CellHeight * VisibleRowCount;
        newPosition.height = CellHeight;
        newPosition.width = position.width / ColumnCount;

        for (int j = 0; j < VisibleRowCount; j++)
        {
            SerializedProperty row = data
                .GetArrayElementAtIndex(j)
                .FindPropertyRelative("row");

            for (int i = 0; i < ColumnCount; i++)
            {
                EditorGUI.PropertyField(
                    newPosition,
                    row.GetArrayElementAtIndex(i),
                    GUIContent.none
                );

                newPosition.x += newPosition.width;
            }

            newPosition.x = position.x;
            newPosition.y -= CellHeight;
        }
    }

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        // Görünür grid satırları + 1 başlık satırı
        return CellHeight * (VisibleRowCount + 1);
    }
}
