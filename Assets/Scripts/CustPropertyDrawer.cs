using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomPropertyDrawer(typeof(ArrayLayout))]
public class CustPropertyDrawer : PropertyDrawer
{


	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.PrefixLabel(position, label);
		Rect newposition = position;
		newposition.y += 144f;
		SerializedProperty data = property.FindPropertyRelative("rows");
		//data.rows[0][]
		if (data.arraySize != 8)
			data.arraySize = 8;
		for (int j = 0; j < 8; j++)
		{
			SerializedProperty row = data.GetArrayElementAtIndex(j).FindPropertyRelative("row");
			newposition.height = 18f;
			if (row.arraySize != 6)
				row.arraySize = 6;
			newposition.width = position.width / 6;
			for (int i = 0; i < 6; i++)
			{
				EditorGUI.PropertyField(newposition, row.GetArrayElementAtIndex(i), GUIContent.none);
				newposition.x += newposition.width;
			}

			newposition.x = position.x;
			newposition.y -= 18f;
		}
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return 18f * 10;
	}
}