using UnityEditor;
using UnityEngine;

/// <summary>
/// Как число навыка выглядит в Инспекторе: одна строка "Подпись [значение]".
/// Подпись и ключ заполняются сами (KitParams) — менять можно только значение.
/// </summary>
[CustomPropertyDrawer(typeof(SkillParam))]
public class SkillParamDrawer : PropertyDrawer
{
    /// <summary>Высота — одна строка.</summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

    /// <summary>Нарисовать строку: подпись слева, поле значения справа.</summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty name = property.FindPropertyRelative("name");   // Подпись
        SerializedProperty value = property.FindPropertyRelative("value"); // Значение
        string text = name != null && !string.IsNullOrEmpty(name.stringValue) ? name.stringValue : label.text;

        EditorGUI.BeginProperty(position, label, property);
        float old = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = Mathf.Max(old, position.width - 70f); // длинная подпись, короткое поле числа
        EditorGUI.PropertyField(position, value, new GUIContent(text, text));
        EditorGUIUtility.labelWidth = old;
        EditorGUI.EndProperty();
    }
}
