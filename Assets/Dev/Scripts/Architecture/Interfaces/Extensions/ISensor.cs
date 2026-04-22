using UnityEngine;

namespace Sarabande.EntityExtensions
{
    public interface ISensor
    {
        bool occupied { get; }
        bool IsActive { get; }
        InteractionLayer interactionLayer { get; } // Ajouté

        void OnEnter();
        void OnExit();
        void Activate();
        void Deactivate();
    }
}


