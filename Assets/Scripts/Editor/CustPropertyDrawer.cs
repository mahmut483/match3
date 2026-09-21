using UnityEngine;
using UnityEditor;

using Match3.Gameplay.Board;

namespace Match3.Editor
{
    [CustomPropertyDrawer(typeof(ArrayLayout))]
    public class CustPropertyDrawer : PropertyDrawer
    {
        private const float CellHeight = 18f;
        private const float ValidationHeight = 58f;

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.PrefixLabel(position, label);

            SerializedProperty data =
                property.FindPropertyRelative("rows");

            string validationError = GetSerializedValidationError(data);

            if (validationError != null)
            {
                Rect helpPosition = position;
                helpPosition.y += CellHeight;
                helpPosition.height = 36f;
                EditorGUI.HelpBox(helpPosition, validationError, MessageType.Error);

                Rect buttonPosition = helpPosition;
                buttonPosition.y += helpPosition.height + 2f;
                buttonPosition.height = CellHeight;

                if (GUI.Button(buttonPosition, "Migrate layout to 6 x 15"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Migrate board layout",
                        "The layout will be resized to 6 columns and 15 rows. " +
                        "Cells outside that area will be removed, and hidden spawn rows will be opened.",
                        "Migrate",
                        "Cancel");

                    if (confirmed)
                    {
                        MigrateLayout(data);
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }

                return;
            }

            // Yalnızca görünür satırları, alttan yukarı (ekrandaki gibi) çiz.
            Rect newPosition = position;
            newPosition.y += CellHeight * BoardDefinition.VisibleHeight;
            newPosition.height = CellHeight;
            newPosition.width = position.width / BoardDefinition.VisibleWidth;

            for (int j = 0; j < BoardDefinition.VisibleHeight; j++)
            {
                SerializedProperty row = data
                    .GetArrayElementAtIndex(j)
                    .FindPropertyRelative("row");

                for (int i = 0; i < BoardDefinition.VisibleWidth; i++)
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
            SerializedProperty data = property.FindPropertyRelative("rows");

            if (GetSerializedValidationError(data) != null)
            {
                return CellHeight + ValidationHeight;
            }

            // Görünür grid satırları + 1 başlık satırı
            return CellHeight * (BoardDefinition.VisibleHeight + 1);
        }

        private static string GetSerializedValidationError(SerializedProperty data)
        {
            if (data == null || data.arraySize != BoardDefinition.TotalHeight)
            {
                int rowCount = data != null ? data.arraySize : 0;
                return $"Layout requires {BoardDefinition.TotalHeight} rows; found {rowCount}.";
            }

            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                SerializedProperty row = data
                    .GetArrayElementAtIndex(y)
                    .FindPropertyRelative("row");

                if (row == null || row.arraySize != BoardDefinition.VisibleWidth)
                {
                    int columnCount = row != null ? row.arraySize : 0;
                    return $"Row {y} requires {BoardDefinition.VisibleWidth} columns; found {columnCount}.";
                }

                if (y < BoardDefinition.VisibleHeight) continue;

                for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
                {
                    if (row.GetArrayElementAtIndex(x).boolValue)
                    {
                        return $"Spawn cell ({x}, {y}) must remain usable.";
                    }
                }
            }

            return null;
        }

        private static void MigrateLayout(SerializedProperty data)
        {
            data.arraySize = BoardDefinition.TotalHeight;

            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                SerializedProperty row = data
                    .GetArrayElementAtIndex(y)
                    .FindPropertyRelative("row");

                row.arraySize = BoardDefinition.VisibleWidth;

                if (y < BoardDefinition.VisibleHeight) continue;

                for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
                {
                    row.GetArrayElementAtIndex(x).boolValue = false;
                }
            }
        }
    }
}
