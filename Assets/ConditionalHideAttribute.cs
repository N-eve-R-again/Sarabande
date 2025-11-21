using UnityEngine;
public class ConditionalHideAttribute : PropertyAttribute
{
    public string ConditionalSourceField;
    public object CompareValue;
    public string Header;

    public ConditionalHideAttribute(string conditionalSourceField, object compareValue, string header = null)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValue = compareValue;
        Header = header;
    }
}