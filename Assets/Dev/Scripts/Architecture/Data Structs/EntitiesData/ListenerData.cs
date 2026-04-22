using Sarabande.Core;
using System;
using UnityEngine;

namespace Sarabande.Listeners
{
    public enum RearmType
    {
        OneShot,
        CallBack,
        Timer,
        Instant
    }

    [System.Serializable]
    public abstract class ListenerData
    {
        public Vector2Int cell = Vector2Int.zero;
        public string[] triggerKeys = new string[0];
        public ListenerData()
        {
        }

    }

    [System.Serializable]
    public class FakeWallData : ListenerData
    {
        public FakeWallData(Vector2Int cell)
        {
            this.cell = cell;
        }

        public FakeWallData() : base()
        {
        }
    }

    [System.Serializable]
    public class MessageConfig : ListenerData
    {
        // coordonnées (utilise x/z comme partout)
        [TextArea(2, 5)] public string text = "text here";
        [Min(0.1f)] public float displaySeconds = 3f;
        public AudioClip voiceClip = null;

        public MessageConfig() : base() { }
    }

    [Serializable]
    public class TriggerObjectConfig : ListenerData
    {

        public enum Type
        {
            InvisibleTrigger,
            TriggerPad,
            Lever
        }

        public Type type = Type.TriggerPad;

        [ConditionalHide("type", Type.Lever)]
        public CardinalDirection attachedTo = CardinalDirection.South;

        [Header("Rearm Behaviour")]

        public RearmType rearmType = RearmType.OneShot;
        [Min(0f)] public float timeToRearm = 1f;

        public TriggerObjectConfig(Type type, string[] triggerKeys, Vector2Int cell, bool oneShot, bool waitForCallback, float timeToRearm, CardinalDirection attachedTo = CardinalDirection.North)
        {
            if (oneShot)
            {
                rearmType = RearmType.OneShot;
            }
            else if (waitForCallback)
            {
                rearmType = RearmType.CallBack;
            }
            else
            {
                rearmType = RearmType.Timer;
                this.timeToRearm = timeToRearm;
            }
            this.timeToRearm = 0f;
            this.type = type;
            this.triggerKeys = triggerKeys;
            this.cell = cell;
            this.attachedTo = attachedTo;


        }

        public TriggerObjectConfig() : base()
        {
        }

    }
}