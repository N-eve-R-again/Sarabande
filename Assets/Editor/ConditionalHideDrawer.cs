using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
public class ConditionalHideDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHide = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHide, property);

        if (enabled)
        {
            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHide = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHide, property);

        if (enabled)
        {
            float height = EditorGUI.GetPropertyHeight(property, label);

            if (!string.IsNullOrEmpty(condHide.Header))
            {
                height += EditorGUIUtility.singleLineHeight + 2;
            }

            return height;
        }

        return -EditorGUIUtility.standardVerticalSpacing;
    }

    private bool GetConditionalHideAttributeResult(ConditionalHideAttribute condHide, SerializedProperty property)
    {
        string propertyPath = property.propertyPath;
        string conditionPath = propertyPath.Replace(property.name, condHide.ConditionalSourceField);
        SerializedProperty sourcePropertyValue = property.serializedObject.FindProperty(conditionPath);

        if (sourcePropertyValue != null)
        {
            switch (sourcePropertyValue.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return sourcePropertyValue.boolValue.Equals(condHide.CompareValue);

                case SerializedPropertyType.Enum:
                    return sourcePropertyValue.enumValueIndex.Equals((int)condHide.CompareValue);

                case SerializedPropertyType.Integer:
                    return sourcePropertyValue.intValue.Equals(condHide.CompareValue);

                case SerializedPropertyType.Float:
                    return sourcePropertyValue.floatValue.Equals(condHide.CompareValue);

                case SerializedPropertyType.String:
                    return sourcePropertyValue.stringValue.Equals(condHide.CompareValue);

                default:
                    Debug.LogWarning($"Type {sourcePropertyValue.propertyType} non supporté pour ConditionalHide");
                    return true;
            }
        }

        return true;
    }
}
