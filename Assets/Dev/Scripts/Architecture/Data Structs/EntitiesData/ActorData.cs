using Sarabande.Core;
using UnityEngine;

namespace Sarabande.Actors
{

    [System.Serializable]
    public abstract class ActorData
    {
        public Vector2Int cell;
        public ActorData() { }
    }

    [System.Serializable]
    public class HeroData : ActorData
    {
        public CardinalDirection spawnDirection = CardinalDirection.South;
        public HeroData() : base() { }
    }

    [System.Serializable]
    public class NmeData : ActorData
    {
        public enum NmeType
        {
            Zombie,
            Charger,
        }

        public CardinalDirection facing = CardinalDirection.South;

        public NmeData() : base() { }
    }
}