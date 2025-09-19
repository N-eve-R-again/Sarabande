// FILE: Assets/Dev/Scripts/VFX/DiscoDancersRig.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Disco;

namespace Sarabande.VFX
{
    public class DiscoDancersRig : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private DiscoSequenceSystem disco;
        [SerializeField] private LevelContext levelContext;
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [System.Serializable]
        public class DancerSpriteSet
        {
            public Sprite north, east, south, west;
        }

        [Header("Sprites (4 orientations)")]
        [SerializeField] private DancerSpriteSet sprites;

        [Header("Look & placement")]
        [SerializeField] private float yOffset = 0.02f;
        [SerializeField, Min(0.1f)] private float spriteScale = 1.0f;
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 250;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.01f)] private float moveSeconds = 0.25f;
        [SerializeField, Min(0f)] private float pauseSeconds = 0.10f;
        [SerializeField, Min(0.05f)] private float spinSeconds = 0.30f;

        [Header("Lifecycle")]
        [SerializeField] private bool onlyWhenDisco = true;
        [SerializeField] private bool destroyOnStop = true;

        // --- runtime ---
        private Transform _root;

        // par danseur
        private readonly List<Transform> _trs = new();
        private readonly List<SpriteRenderer> _srs = new();
        private readonly List<Vector2Int> _baseCell = new();
        private readonly List<Vector2Int> _ccwDir = new();   // direction anti-horaire le long du bord
        private readonly List<bool> _atOffset = new();       // false = sur base, true = sur base+ccw

        // anneau des positions de base (espacées d’une case)
        private List<Vector2Int> _ring;

        private Coroutine _loopCo;
        private bool _active;
        private int _lastStepDir = +1; // +1 = anti-horaire, -1 = horaire (pour l’orientation des spins)

        public void OnDiscoStart() { EnsureBuilt(); StartRig(); }
        public void OnDiscoStop() { StopRig(); if (destroyOnStop) DestroyRig(); }

        private void OnEnable()
        {
            if (disco)
            {
                disco.onSequenceStart.AddListener(OnDiscoStart);
                disco.onSequenceFail.AddListener(OnDiscoStop);
                disco.onSequenceSuccess.AddListener(OnDiscoStop);
            }
            if (!onlyWhenDisco) { EnsureBuilt(); StartRig(); }
        }

        private void OnDisable()
        {
            if (disco)
            {
                disco.onSequenceStart.RemoveListener(OnDiscoStart);
                disco.onSequenceFail.RemoveListener(OnDiscoStop);
                disco.onSequenceSuccess.RemoveListener(OnDiscoStop);
            }
            StopRig();
            if (destroyOnStop) DestroyRig();
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            int w = 8, h = 8;
            if (levelContext && levelContext.LevelData)
            {
                w = Mathf.Max(1, levelContext.LevelData.width);
                h = Mathf.Max(1, levelContext.LevelData.height);
            }

            // 1) Construit l’anneau d’ancrage (positions de base)
            _ring = new List<Vector2Int>();
            for (int y = 1; y < h; y += 2) _ring.Add(new Vector2Int(-1, y));        // gauche
            for (int x = 1; x < w; x += 2) _ring.Add(new Vector2Int(x, h));         // haut
            for (int y = h - 2; y >= 0; y -= 2) _ring.Add(new Vector2Int(w, y));    // droite
            for (int x = w - 2; x >= 0; x -= 2) _ring.Add(new Vector2Int(x, -1));   // bas

            if (_ring.Count == 0)
            {
                Debug.LogWarning("[DiscoDancersRig] Anneau vide (plateau trop petit ?).");
                return;
            }

            _root = new GameObject("DiscoDancersRig_Runtime").transform;
            _root.SetParent(transform, false);

            _trs.Clear(); _srs.Clear(); _baseCell.Clear(); _ccwDir.Clear(); _atOffset.Clear();

            // 2) Spawn des danseurs : chacun garde son bord + une direction CCW propre
            foreach (var baseC in _ring)
            {
                var go = new GameObject($"Dancer_{_trs.Count}");
                go.transform.SetParent(_root, false);
                go.transform.position = CellCenterWorld(baseC);

                // COUCHÉ AU SOL + image non à l’envers
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.flipY = true; // <-- corrige les sprites "tête en bas"

                Vector2Int ccw = CCWDirForEdge(baseC, w, h);

                // orientation initiale = direction du prochain pas anti-horaire
                sr.sprite = GetSprite(FacingFromDelta(ccw));

                go.transform.localScale = Vector3.one * spriteScale;

                _trs.Add(go.transform);
                _srs.Add(sr);
                _baseCell.Add(baseC);
                _ccwDir.Add(ccw);
                _atOffset.Add(false); // démarre sur la case de base
            }
        }

        private void DestroyRig()
        {
            if (_root)
            {
                if (Application.isPlaying) Destroy(_root.gameObject);
                else DestroyImmediate(_root.gameObject);
            }
            _root = null;

            _trs.Clear(); _srs.Clear(); _baseCell.Clear(); _ccwDir.Clear(); _atOffset.Clear();
            _ring = null;
        }

        private void StartRig()
        {
            if (_active || _root == null || _ring == null || _ring.Count == 0) return;

            // Snap tout le monde sur sa case de base
            for (int i = 0; i < _trs.Count; i++)
            {
                _trs[i].position = CellCenterWorld(_baseCell[i]);
                _atOffset[i] = false;
                _srs[i].sprite = GetSprite(FacingFromDelta(_ccwDir[i]));
            }

            _active = true;
            if (_loopCo != null) StopCoroutine(_loopCo);
            _loopCo = StartCoroutine(DanceLoop());
        }

        private void StopRig()
        {
            _active = false;
            if (_loopCo != null) { StopCoroutine(_loopCo); _loopCo = null; }
        }

        private IEnumerator DanceLoop()
        {
            var waitPause = (pauseSeconds > 0f) ? new WaitForSeconds(pauseSeconds) : null;

            while (_active)
            {
                // anti-horaire : aller vers base+ccw
                yield return MoveAll(toOffset: true);
                if (waitPause != null) yield return waitPause;
                yield return SpinAllOnce(+1);

                // horaire : revenir à la base
                yield return MoveAll(toOffset: false);
                if (waitPause != null) yield return waitPause;
                yield return SpinAllOnce(-1);
            }
        }

        // Déplace d’une case : soit vers base+ccw (toOffset=true), soit vers base (toOffset=false)
        private IEnumerator MoveAll(bool toOffset)
        {
            float dur = Mathf.Max(0.01f, moveSeconds);
            float t = 0f;

            var startPos = new Vector3[_trs.Count];
            var endPos = new Vector3[_trs.Count];

            // Fixe destinations + sprites d’orientation pendant le step
            for (int i = 0; i < _trs.Count; i++)
            {
                startPos[i] = _trs[i].position;

                Vector2Int targetCell = toOffset ? (_baseCell[i] + _ccwDir[i]) : _baseCell[i];
                endPos[i] = CellCenterWorld(targetCell);

                Vector2Int delta = toOffset ? _ccwDir[i] : -_ccwDir[i];
                _srs[i].sprite = GetSprite(FacingFromDelta(delta));
            }

            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float k = Mathf.Clamp01(t);
                for (int i = 0; i < _trs.Count; i++)
                    _trs[i].position = Vector3.Lerp(startPos[i], endPos[i], k);
                yield return null;
            }

            for (int i = 0; i < _trs.Count; i++)
            {
                _trs[i].position = endPos[i];
                _atOffset[i] = toOffset;
            }

            _lastStepDir = toOffset ? +1 : -1;
        }

        private IEnumerator SpinAllOnce(int dirSign /* +1 = comme CCW, -1 = inverse */)
        {
            float total = Mathf.Max(0.05f, spinSeconds);
            float seg = total / 4f;

            for (int step = 0; step < 4; step++)
            {
                for (int i = 0; i < _trs.Count; i++)
                {
                    // base facing = sens du dernier pas
                    Vector2Int baseDelta = (dirSign >= 0) ? _ccwDir[i] : -_ccwDir[i];
                    var baseFacing = FacingFromDelta(baseDelta);
                    var spinFacing = (Facing)(((int)baseFacing + step) & 3);
                    _srs[i].sprite = GetSprite(spinFacing);
                }
                yield return new WaitForSeconds(seg);
            }
        }

        // --- util ---

        private enum Facing { North = 0, East = 1, South = 2, West = 3 }

        private Facing FacingFromDelta(Vector2Int d)
        {
            if (d.x > 0) return Facing.East;
            if (d.x < 0) return Facing.West;
            if (d.y > 0) return Facing.North;
            return Facing.South;
        }

        private Sprite GetSprite(Facing f)
        {
            if (sprites == null) return null;
            switch (f)
            {
                case Facing.North: return sprites.north ? sprites.north : (sprites.east ?? sprites.south ?? sprites.west);
                case Facing.East: return sprites.east ? sprites.east : (sprites.south ?? sprites.west ?? sprites.north);
                case Facing.South: return sprites.south ? sprites.south : (sprites.west ?? sprites.north ?? sprites.east);
                default: return sprites.west ? sprites.west : (sprites.north ?? sprites.east ?? sprites.south);
            }
        }

        private Vector2Int CCWDirForEdge(Vector2Int baseC, int w, int h)
        {
            // Détermine le bord où se trouve baseC (coordonnées "hors plateau" comme spécifié)
            if (baseC.x == -1) return new Vector2Int(0, -1); // gauche -> CCW = descendre
            if (baseC.y == h) return new Vector2Int(-1, 0); // haut   -> CCW = aller à gauche
            if (baseC.x == w) return new Vector2Int(0, +1); // droite -> CCW = monter
            /* baseC.y == -1 */
            return new Vector2Int(+1, 0); // bas    -> CCW = aller à droite
        }

        private Vector3 CellCenterWorld(Vector2Int c)
            => new Vector3((c.x + 0.5f) * cellSize, yOffset, (c.y + 0.5f) * cellSize);
    }
}
