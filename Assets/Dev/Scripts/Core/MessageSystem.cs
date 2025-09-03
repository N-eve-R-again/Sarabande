using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.UI; // pour MessagePopupUI_TMP et MessagesUIController

namespace Sarabande.Messages
{
    /// <summary>
    /// - Affiche un popup + joue la voix quand HÉRO entre sur une case "message".
    /// - Met à jour le compteur (icône + X/Total).
    /// - Les messages collectés sont DÉFINITIFS (ne reset pas).
    /// - Construit de petits marqueurs visuels au sol pour les messages non-collectés.
    /// À attacher sur LevelRoot.
    /// </summary>
    public class MessageSystem : MonoBehaviour, IResettable
    {
        [Header("Data & Refs")]
        [SerializeField] private LevelData levelData;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private HeroController hero;

        [Header("UI")]
        [SerializeField] private MessagePopupUI_TMP popupUI;         // UI_Canvas/MessagePopup
        [SerializeField] private MessagesUIController messagesUI;    // UI_Canvas/UI_MessagesRoot
        [SerializeField] private Sprite counterIcon;

        [Header("Marker visuals")]
        [SerializeField] private Material markerMaterial;
        [SerializeField, Range(0.05f, 0.6f)] private float markerRadiusScale = 0.18f;
        [SerializeField, Range(0.01f, 0.3f)] private float markerThickness = 0.03f;
        [SerializeField, Min(0f)] private float markerY = 0.01f;     // petit offset au-dessus du sol

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.9f;

        // runtime
        private readonly HashSet<int> _collected = new();                // messages déjà pris (persiste à travers Reset)
        private readonly Dictionary<Vector2Int, int> _cellToIndex = new();
        private readonly Dictionary<int, GameObject> _markers = new();
        private Vector2Int _lastHeroCell;
        private AudioSource _voice;

        private Transform _markersParent;

        private void Awake()
        {
            if (!levelData || !hero)
            {
                Debug.LogError("[MessageSystem] Références manquantes (LevelData ou Hero).");
                enabled = false;
                return;
            }

            // Audio source 2D pour la voix (non spatialisée)
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.loop = false;
            _voice.spatialBlend = 0f; // 2D
            _voice.volume = voiceVolume;

            _markersParent = new GameObject("MessageMarkers").transform;
            _markersParent.SetParent(transform, false);

            BuildMapsAndMarkers();

            _lastHeroCell = hero.GridPos;

            // UI compteur (icône + valeurs initiales)
            int total = levelData.messages?.Count ?? 0;
            messagesUI?.SetIcon(counterIcon);
            messagesUI?.SetCounter(_collected.Count, total);
        }

        private void Update()
        {
            var cell = hero.GridPos;
            if (cell == _lastHeroCell) return;
            _lastHeroCell = cell;

            if (_cellToIndex.TryGetValue(cell, out int idx))
            {
                if (!_collected.Contains(idx))
                {
                    Collect(idx);
                }
            }
        }

        // --- Build / Markers ---

        private void BuildMapsAndMarkers()
        {
            _cellToIndex.Clear();
            foreach (var kv in _markers) if (kv.Value) Destroy(kv.Value);
            _markers.Clear();

            if (levelData.messages == null) return;

            for (int i = 0; i < levelData.messages.Count; i++)
            {
                var spec = levelData.messages[i];
                var cell = new Vector2Int(spec.cell.x, spec.cell.z);

                // map cell -> index
                _cellToIndex[cell] = i;

                // si déjà collecté, pas de marker
                if (_collected.Contains(i)) continue;

                // petit disque/cylindre plat au sol
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"MsgMarker_{i}_({cell.x},{cell.y})";
                go.transform.SetParent(_markersParent, false);

                Vector3 c = GridCenter(cell);
                float r = markerRadiusScale * cellSize;
                float h = markerThickness; // hauteur monde (Y)

                go.transform.position = new Vector3(c.x, markerY + h * 0.5f, c.z);
                // Pour un cylindre Unity: scale.y = half-height
                go.transform.localScale = new Vector3(r, h * 0.5f, r);

                // désactive le collider
                var col = go.GetComponent<Collider>(); if (col) Destroy(col);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (markerMaterial) mr.sharedMaterial = markerMaterial;
                }

                _markers[i] = go;
            }
        }

        private Vector3 GridCenter(Vector2Int c)
            => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        // --- Collect ---

        private void Collect(int index)
        {
            if (index < 0 || index >= (levelData.messages?.Count ?? 0)) return;
            if (_collected.Contains(index)) return;

            _collected.Add(index);

            // retire le marker visuel s’il existe
            if (_markers.TryGetValue(index, out var marker) && marker)
            {
                Destroy(marker);
                _markers.Remove(index);
            }

            var spec = levelData.messages[index];

            // popup (tu as demandé une durée configurable par message)
            popupUI?.Show(spec.text, Mathf.Max(0.1f, spec.displaySeconds));

            // voix (si fournie)
            if (_voice && spec.voiceClip)
            {
                if (_voice.isPlaying) _voice.Stop();
                _voice.PlayOneShot(spec.voiceClip, voiceVolume);
            }

            // compteur UI
            int total = levelData.messages?.Count ?? 0;
            messagesUI?.SetCounter(_collected.Count, total);

            // (optionnel) bruitage pour NME : Sarabande.Core.NoiseSystem.Emit(cell); si jamais tu le veux
        }

        // --- Reset ---
        // IMPORTANT: on NE vide PAS _collected (les messages restent acquis).
        // On ne reconstruit que les marqueurs restants.
        public void ResetToInitial()
        {
            BuildMapsAndMarkers();

            // remet le compteur (au cas où)
            int total = levelData.messages?.Count ?? 0;
            messagesUI?.SetCounter(_collected.Count, total);
        }
    }
}
