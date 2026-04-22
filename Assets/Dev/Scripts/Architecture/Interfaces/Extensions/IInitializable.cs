using UnityEngine;

namespace Sarabande.EntityExtensions { 
    public enum InitPhase
    {
        Environment,  // decors, obstacles
        Entities,     // portes, switches
        Actors,       // ennemis
        Player        // toujours dernier
    }
    public interface IInitializable
    {
        InitPhase phase => InitPhase.Entities;
        public void Init();
    }

}