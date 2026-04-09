using UnityEngine;


public static class LogGen
{


    public static void LogAs(object who, string log, string logColor = "#CECECE")
    {
        Debug.Log($"{GetHeader(who)} <color={logColor}>{log}</color>");

    }
    public static void ErrorAs(object who, string log, string logColor = "red")
    {
        Debug.LogError($"{GetHeader(who)} <color={logColor}>{log}</color>");
    }

    public static void WarningAs(object who, string log, string logColor = "yellow")
    {
        Debug.LogWarning($"{GetHeader(who)} <color={logColor}>{log}</color>");
    }

    public static string GetHeader(object obj) {

        string _temp = obj.GetType().Name.ToString();
        string _color = "white";

        switch (obj) {
            case BootStrapper bt: _color = "cyan"; break;
            case NavigationManager nm: _color = "#81CCB0"; break;
            case EntityRegister er: _color = "#FFBDFD"; break;

        }

        string result = $"<color={_color}>[{_temp}]</color>";
        return result;
    }


}
