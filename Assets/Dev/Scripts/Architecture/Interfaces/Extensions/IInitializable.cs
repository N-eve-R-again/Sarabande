using UnityEngine;

namespace Sarabande.EntityExtensions { 
    public enum InitPhase
    {
        Base,         // Init basique qui n'appelle aucune fonction
        Simple,       // Init simple qui fait des appels locaux ou de la logique avancée
        Complex,      // Init complexe qui fait des appels globaux, appels d'event, ou création d'objet.
        Actors,       // Init NMes
        Player        // Init Joueur 
    }
    public interface IInitializable
    {
        InitPhase phase => InitPhase.Simple;
        public void Init();
    }

}