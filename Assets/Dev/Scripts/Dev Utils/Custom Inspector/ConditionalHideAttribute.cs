using UnityEngine;
public class ConditionalHideAttribute : PropertyAttribute
{
    public string ConditionalSourceField;
    public object CompareValue;
    public string Header;

    public ConditionalHideAttribute(string conditionalSourceField, object compareValue)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValue = compareValue;
    }
}