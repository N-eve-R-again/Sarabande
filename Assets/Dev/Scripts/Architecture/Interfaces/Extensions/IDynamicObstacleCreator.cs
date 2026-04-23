using Sarabande.Core;
using UnityEngine;


public interface IPortalCreator
{

}

public interface ICollider
{

}

#pragma warning disable CS0618

public static class PortalEvents
{
    public static void RegisterPortal(this IPortalCreator navigation, ExitConfig portalconfig)
        => NavigationEvents.NotifyPortalRegistry(portalconfig);
    public static void ModifyPortal(this IPortalCreator navigation, Vector2Int cell, bool newstate)
        => NavigationEvents.NotifyPortalModification(cell,newstate);


}

public static class ColliderEvents
{

}

#pragma warning restore CS0618