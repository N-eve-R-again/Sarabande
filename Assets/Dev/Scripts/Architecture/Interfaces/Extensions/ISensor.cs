using UnityEngine;

namespace Sarabande.EntityExtensions
{
    public interface ISensor
    {
        Vector2Int cell {  get; }
        bool occupied { get; }
        bool IsActive { get; }
        InteractionLayer interactionLayer { get; } // Ajouté

        void OnEnter();
        void OnExit();
        void Activate();
        void Deactivate();

    }

    public static class SensorEvents
    {
        public static void RegisterSensor(this ISensor sensor) => RegistryEvents.Raise_SensorRegistry(sensor);
    }
}


