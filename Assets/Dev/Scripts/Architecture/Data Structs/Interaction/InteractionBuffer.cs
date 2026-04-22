using UnityEngine;
using Sarabande.Actors;
using Sarabande.Listeners;

public class InteractionBuffer
{
        public IListener listener;
        public IActor actor;
        public Vector2Int interactionPosition;

        public InteractionBuffer(IListener _listener, IActor _actor, Vector2Int _interactionPosition)
        {
            listener = _listener;
            actor = _actor;
            interactionPosition = _interactionPosition;
        }

}
