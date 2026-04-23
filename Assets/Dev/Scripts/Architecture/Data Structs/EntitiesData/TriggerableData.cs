using Sarabande.Core;
using Sarabande.Levels;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace Sarabande.Triggerables
{
    [System.Serializable]
    public abstract class TriggerableData
    {
        public Vector2Int cell;
        public string triggerKey = "undefined";
        public TriggerableData()
        { }
    }

    [System.Serializable]
    public class DefaultTriggerableData : TriggerableData
    {
        public DefaultTriggerableData(Vector2Int cell, string triggerKey)
        {
            this.cell = cell;
            this.triggerKey = triggerKey;
        }
        public DefaultTriggerableData() : base()
        {
        }
    }


    [System.Serializable]
    public class ArrowTrapConfig : TriggerableData
    {
        // première case dans la map que la flèche traverse
        public CardinalDirection travelDir = CardinalDirection.North;         // direction de déplacement (N/E/S/W)
        [Min(0.1f)] public float arrowSpeed = 1f;   // vitesse (unités monde / seconde)

        // --- options de réarmement ---
        public bool canRearm = false;                   // si true, le piège se réarme
        [Min(0f)] public float rearmTimeDelay = 1f;      // délai avant réarmement (secondes)


        public ArrowTrapConfig(GridCoord cell, CardinalDirection travelDir, float arrowSpeed, bool canRearm, float rearmDelay, string triggerKey)
        {
            this.cell = cell;
            this.travelDir = travelDir;
            this.arrowSpeed = arrowSpeed;
            this.canRearm = canRearm;
            this.rearmTimeDelay = rearmDelay;
            this.triggerKey = triggerKey;
        }

        public ArrowTrapConfig() : base()
        {
        }
    }

    [System.Serializable]
    public class GateConfig : TriggerableData
    {
        public enum Type
        {
            Timer,
            OneShot,
            Toggle
        }

        public Type type;
        public CardinalDirection direction;

        public float timer;
        public bool startopen = false;
        public GateConfig(Type type, CardinalDirection direction, string triggerKey, Vector2Int cell, float timer)
        {
            this.type = type;
            this.direction = direction;
            this.triggerKey = triggerKey;
            this.cell = cell;
            this.timer = timer;
        }
        public GateConfig() : base() { }

    }

    [System.Serializable]
    public class DiscoSequenceConfig : TriggerableData
    {
        public string[] successTriggerKeys = new string[0];
        public string[] failTriggerKeys = new string[0];

        [Header("Dalles")]
        public List<DiscoDalleData> discoDalleDatas = new List<DiscoDalleData>();


        public DiscoSequenceConfig(string triggerKey, string[] successTriggerKeys, string[] failTriggerKeys, List<GridCoord> cells, List<float> stepSeconds, float defaultStepSeconds)
        {
            this.triggerKey = triggerKey;
            this.successTriggerKeys = successTriggerKeys;
            this.failTriggerKeys = failTriggerKeys;
        }
        public DiscoSequenceConfig() : base()
        {
        }
    }

    [Serializable]
    public class DiscoDalleData
    {
        public Vector2Int cell;
        public float stepSecond = 1f;
    }

}