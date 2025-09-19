using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Levels;
using Sarabande.Player;
using Sarabande.UI;

namespace Sarabande.Messages
{
    /// <summary>
    /// - Affiche un popup + joue la voix quand HÉRO entre sur une case "message".
    /// - Met à jour le compteur (icône + X/Total).
    /// - Les messages collectés sont DÉFINITIFS (ne reset pas).
    /// - Construit des marqueurs visuels au sol (sprite plat) pour les messages non-collectés.
    /// À attacher sur LevelRoot.
    /// </summary>
    public class MessageSystem : MonoBehaviour, IResettable
    {
        [Header("Data & Refs")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private HeroController hero;
        [SerializeField] private bool useLevelContext = true;
        [SerializeField] private Sarabande.Core.LevelContext levelContext;
        [SerializeField, HideInInspector] private Sarabande.Levels.LevelData levelData;


        [Header("UI")]
        [SerializeField] private MessagePopupUI_TMP popupUI;         // UI_Canvas/MessagePopup
        [SerializeField] private MessagesUIController messagesUI;    // UI_Canvas/UI_MessagesRoot
        [SerializeField] private Sprite counterIcon;

        [Header("Marker Sprite (in-level)")]
        [SerializeField] private Sprite markerSprite;                // <-- sprite à déposer ici
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int orderInLayer = 20;
        [SerializeField, Min(0f)] private float markerY = 0.02f;     // petit offset au-dessus du sol
        [SerializeField] private bool fitToCell = true;              // ajuste la largeur au cellSize
        [SerializeField, Range(0.1f, 2f)] private float spriteScale = 1f; // multiplicateur
        [SerializeField] private Color spriteTint = Color.white;     // possibilité d’alpha < 1

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.9f;

        // runtime
        private readonly HashSet<int> _collected = new();
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

            // Audio 2D pour la voix
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.loop = false;
            _voice.spatialBlend = 0f;
            _voice.volume = voiceVolume;

            _markersParent = new GameObject("MessageMarkers").transform;
            _markersParent.SetParent(transform, false);

            BuildMapsAndMarkers();

            _lastHeroCell = hero.GridPos;

            // UI compteur
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

                // sprite plat au sol (XY pivot -> couché sur XZ)
                var go = new GameObject($"MsgMarker_{i}_({cell.x},{cell.y})");
                go.transform.SetParent(_markersParent, false);

                Vector3 c = GridCenter(cell);
                go.transform.position = new Vector3(c.x, markerY, c.z);

                // enfant avec SpriteRenderer
                var srGO = new GameObject("Sprite");
                srGO.transform.SetParent(go.transform, false);
                srGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // XY -> XZ (face caméra top-down)
                var sr = srGO.AddComponent<SpriteRenderer>();
                sr.sprite = markerSprite;
                sr.color = spriteTint;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = orderInLayer;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                // scale pour s’aligner sur la largeur de case si demandé
                if (fitToCell && sr.sprite != null)
                {
                    float spriteWorldWidth = sr.sprite.bounds.size.x; // à scale=1
                    if (spriteWorldWidth <= 0f) spriteWorldWidth = 1f;
                    float targetWidth = cellSize * spriteScale;
                    float s = targetWidth / spriteWorldWidth;
                    srGO.transform.localScale = new Vector3(s, s, 1f);
                }
                else
                {
                    srGO.transform.localScale = Vector3.one * spriteScale;
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

            // popup
            popupUI?.Show(spec.text, Mathf.Max(0.1f, spec.displaySeconds));

            // voix
            if (_voice && spec.voiceClip)
            {
                if (_voice.isPlaying) _voice.Stop();
                Sarabande.Audio.AudioHub.I?.PlayVoiceAt(
    spec.voiceClip,
    transform.position, // 2D, la position n'a pas d'importance
    voiceVolume
);
            }

            // compteur UI
            int total = levelData.messages?.Count ?? 0;
            messagesUI?.SetCounter(_collected.Count, total);
        }

        // --- Reset ---
        // On NE vide PAS _collected (les messages restent acquis).
        public void ResetToInitial()
        {
            BuildMapsAndMarkers();

            int total = levelData.messages?.Count ?? 0;
            messagesUI?.SetCounter(_collected.Count, total);
        }
        private void AttachContext()
        {
            if (!useLevelContext) return;

            if (!levelContext)
                levelContext = GetComponentInParent<Sarabande.Core.LevelContext>();

            if (levelContext != null)
            {
                levelContext.LevelDataChanged += HandleContextLevelDataChanged;
                HandleContextLevelDataChanged(levelContext.LevelData); // init immédiate
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Aucun LevelContext parent trouvé.");
            }
        }

        private void DetachContext()
        {
            if (levelContext != null)
                levelContext.LevelDataChanged -= HandleContextLevelDataChanged;
        }

        private void HandleContextLevelDataChanged(Sarabande.Levels.LevelData ld)
        {
            if (levelData == ld) return;
            levelData = ld;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this); // l’inspector reflète la maj auto
#endif
            // NOTE: si ce système a besoin de se "rebuild" quand le LevelData change,
            // appelle ici ta méthode interne (ex: RebuildFromLevelData()).
        }
        private void OnEnable() { AttachContext(); }
        private void OnDisable() { DetachContext(); }
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) AttachContext(); }
#endif
    }
}
