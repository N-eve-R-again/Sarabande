using Sarabande.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

using Sarabande.Listeners;
using Sarabande.Triggerables;
using Sarabande.Obstacles;
using Sarabande.Actors;
using Sarabande.Legacy;


namespace Sarabande.Levels
{
    /// <summary>
    /// Données blueprint d'un niveau, contient les Listeners, Triggerables, Actors, DiscoSequence. 
    /// Ainsi que les dimensions et sortie du niveau.
    /// </summary>
    [CreateAssetMenu(menuName = "Sarabande/Level Data", fileName = "LevelData")]
    public class LevelData : ScriptableObject
    {

        [Header("Level Parameters")]

        [Min(1)] public int width = 8;     // colonnes (A..H)
        [Min(1)] public int height = 8;    // rangées  (1..8)
        [SerializeField] public ExitDoorData exit;

        [Header("ObstacleData")]
        public List<ObstacleData> obstacles = new List<ObstacleData>();

        [Header("Listeners")]
        [SerializeReference] public List<ListenerData> listeners = new List<ListenerData>();

        [Header("Triggerables")]
        [SerializeReference] public List<TriggerableData> triggerables = new List<TriggerableData>();

        [Header("Actors")]
        [SerializeReference] public List<ActorData> actors = new List<ActorData>();
        public HeroData hero = new();

        [Header("Disco Sequences")]
        [SerializeField] public List<DiscoSequenceConfig> discoSequencesConfigs = new List<DiscoSequenceConfig>();

        public LevelData Clone()
        {
            string json = JsonUtility.ToJson(this); //Sérialise en JSON
            LevelData copy = CreateInstance<LevelData>(); //Crée une nouvelle instance
            JsonUtility.FromJsonOverwrite(json, copy); //Désérialise dans la copie
            return copy;
        }

    }

}


