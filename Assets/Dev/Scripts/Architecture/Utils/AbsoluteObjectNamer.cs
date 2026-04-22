using Sarabande.Listeners;
using Sarabande.Triggerables;
using Sarabande.Obstacles;
using Sarabande.Actors;

public static class AbsoluteObjectNamer
{
    public static string GetName(object data)
    {
        switch (data)
        {
            case ListenerData listenerData:
                return GetListenerName(listenerData);

            case TriggerableData triggerableData:
                return GetTriggerableName(triggerableData);

            case ObstacleData obstacleData:
                return GetObstacleName(obstacleData);

            default: return "Undefined Type";
        }

    }
    private static string GetListenerName(ListenerData data)
    {
        string name = "";
        switch (data)
        {
            case FakeWallData: name = "Fake Wall"; break;

            case MessageConfig: name = "Message"; break;

            case TriggerObjectConfig obj:
                switch (obj.type)
                {
                    case TriggerObjectConfig.Type.InvisibleTrigger: name = "InvisTrigger";break;
                    case TriggerObjectConfig.Type.TriggerPad: name = "PressurePad";break;
                    case TriggerObjectConfig.Type.Lever: name = "Lever";break;
                    default: name = "unknownType"; break;
                }
                break;

            default: name = "unknownType"; break;
        }
        return $"{name} {data.cell}";
    }

    private static string GetTriggerableName(TriggerableData data)
    {
        return "";
    }
    private static string GetObstacleName(ObstacleData data)
    {
        return "";
    }

}


