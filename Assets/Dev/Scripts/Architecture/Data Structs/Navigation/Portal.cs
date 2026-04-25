using Sarabande.Core;
using UnityEngine;

[System.Serializable]
public class Portal
{
    [SerializeField] private Vector2Int cell;
    [SerializeField] private CardinalDirection direction;
    [SerializeField] private bool activated;

    public string log => $"at {this.cell} with direction {this.direction} and state {this.activated}";
    public Vector2Int Cell => cell;

    public bool ValidExit(CardinalDirection _actorDir)
    {
        return activated && _actorDir == this.direction;
    }

    public Portal(Vector2Int cell, CardinalDirection direction, bool activated)
    {
        this.cell = cell;
        this.direction = direction;
        this.activated = activated;
    }

    public void SetActivated(bool activated)
    {
        this.activated = activated;
        this.UpdatePortal();
    }

    public bool Activated => activated;
}

#pragma warning disable CS0618

public static class PortalEvents
{
    public static void RegisterPortal(this Portal portal)
        => NavigationEvents.Raise_PortalRegistry(portal);
    public static void UpdatePortal(this Portal portal)
        => NavigationEvents.Raise_PortalUpdate(portal);


}
#pragma warning restore CS0618