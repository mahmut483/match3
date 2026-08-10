using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ArrayLayout))]
public class CustPropertyDrawer : PropertyDrawer
{
    private const int RowCount = 15;
    private const int ColumnCount = 6;
    private const float CellHeight = 18f;

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.PrefixLabel(position, label);

        Rect newPosition = position;

        // 20 satır × 18 piksel
        newPosition.y += CellHeight * RowCount;
        newPosition.height = CellHeight;
        newPosition.width = position.width / ColumnCount;

        SerializedProperty data =
            property.FindPropertyRelative("rows");

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
        // 20 grid satırı + 1 başlık satırı
        return CellHeight * (RowCount + 1);
    }
}