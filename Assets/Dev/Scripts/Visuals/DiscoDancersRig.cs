// FILE: Assets/Dev/Scripts/VFX/DiscoDancersRig.cs
//
// Rôle (résumé)
// - Gère le système des danseurs animés sur la scène Disco : chaque danseur suit un chemin défini sur le bord du plateau, 
//   et effectue des mouvements de type "avancer" et "pivoter" de manière coordonnée.
// - La logique est basée sur des étapes d'animation qui se déroulent suivant la séquence Disco définie par le système DiscoSequenceSystem.
// - Lors du démarrage de la Disco, les danseurs se déplacent selon un mouvement coordonné et un comportement visuel basé sur des sprites (4 orientations).
// - Possibilité de jouer le système de danse uniquement pendant la séquence Disco (option configurable).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sarabande.Core;
using Sarabande.Disco;

namespace Sarabande.VFX
{
    /// <summary>
    /// Contrôle le rig des danseurs disco, leurs positions et animations sur la grille du niveau.
    /// </summary>
    public class DiscoDancersRig : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private DiscoSequenceSystem disco;  // Référence au DiscoSequenceSystem pour les contrôles
        [SerializeField] private LevelContext levelContext; // Contexte du niveau pour obtenir les données de niveau
        [SerializeField, Min(0.001f)] private float cellSize = 1f; // Taille des cellules du niveau

        [System.Serializable]
        public class DancerSpriteSet
        {
            public Sprite north, east, south, west; // Sprites pour les 4 orientations du danseur
        }

        [Header("Sprites (4 orientations)")]
        [SerializeField] private DancerSpriteSet sprites; // Ensemble des sprites pour chaque orientation

        [Header("Look & placement")]
        [SerializeField] private float yOffset = 0.02f; // Décalage vertical du danseur
        [SerializeField, Min(0.1f)] private float spriteScale = 1.0f; // Échelle des sprites
        [SerializeField] private string sortingLayer = "Default"; // Layer de tri des sprites
        [SerializeField] private int sortingOrder = 250; // Ordre de tri des sprites

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.01f)] private float moveSeconds = 0.25f; // Durée du déplacement
        [SerializeField, Min(0f)] private float pauseSeconds = 0.10f; // Durée de la pause entre les étapes
        [SerializeField, Min(0.05f)] private float spinSeconds = 0.30f; // Durée du spin des danseurs

        [Header("Lifecycle")]
        [SerializeField] private bool onlyWhenDisco = true; // Si true, les danseurs n'apparaissent que pendant la séquence disco
        [SerializeField] private bool destroyOnStop = true; // Si true, les danseurs sont détruits lorsque la disco est terminée

        // Variables runtime
        private Transform _root; // Parent des danseurs
        private readonly List<Transform> _trs = new(); // Transform des danseurs
        private readonly List<SpriteRenderer> _srs = new(); // SpriteRenderer des danseurs
        private readonly List<Vector2Int> _baseCell = new(); // Cellules de départ des danseurs
        private readonly List<Vector2Int> _ccwDir = new(); // Direction anti-horaire des danseurs
        private readonly List<bool> _atOffset = new(); // Si le danseur est déplacé à l'offset ou non
        private List<Vector2Int> _ring; // Anneau des positions des danseurs sur la grille

        private Coroutine _loopCo; // Coroutine du cycle de danse
        private bool _active; // Indicateur de l'état des danseurs
        private int _lastStepDir = +1; // Dernière direction de déplacement des danseurs (horaire ou anti-horaire)

        /// <summary>
        /// Active le système de danseurs quand la Disco commence.
        /// </summary>
        public void OnDiscoStart() { EnsureBuilt(); StartRig(); }

        /// <summary>
        /// Désactive le système de danseurs quand la Disco s'arrête.
        /// </summary>
        public void OnDiscoStop() { StopRig(); if (destroyOnStop) DestroyRig(); }

        /// <summary>
        /// S'abonne aux événements du système Disco.
        /// </summary>
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

        /// <summary>
        /// Se désabonne des événements du système Disco et nettoie le rig des danseurs.
        /// </summary>
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

        /// <summary>
        /// Assure que le rig des danseurs est bien construit avant de commencer.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_root != null) return;

            // Récupère les dimensions du niveau
            int w = 8, h = 8;
            if (levelContext && levelContext.LevelData)
            {
                w = Mathf.Max(1, levelContext.LevelData.width);
                h = Mathf.Max(1, levelContext.LevelData.height);
            }

            // Crée l'anneau des positions de départ des danseurs
            _ring = new List<Vector2Int>();
            for (int y = 1; y < h; y += 2) _ring.Add(new Vector2Int(-1, y));        // côté gauche
            for (int x = 1; x < w; x += 2) _ring.Add(new Vector2Int(x, h));         // côté haut
            for (int y = h - 2; y >= 0; y -= 2) _ring.Add(new Vector2Int(w, y));    // côté droit
            for (int x = w - 2; x >= 0; x -= 2) _ring.Add(new Vector2Int(x, -1));   // côté bas

            if (_ring.Count == 0)
            {
                Debug.LogWarning("[DiscoDancersRig] Anneau vide (plateau trop petit ?).");
                return;
            }

            // Crée le parent des danseurs
            _root = new GameObject("DiscoDancersRig_Runtime").transform;
            _root.SetParent(transform, false);

            _trs.Clear(); _srs.Clear(); _baseCell.Clear(); _ccwDir.Clear(); _atOffset.Clear();

            // Spawn des danseurs sur l'anneau
            foreach (var baseC in _ring)
            {
                var go = new GameObject($"Dancer_{_trs.Count}");
                go.transform.SetParent(_root, false);
                go.transform.position = CellCenterWorld(baseC);

                // Assure que le danseur est orienté correctement (horizontal et pas à l'envers)
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.flipY = true; // Corrige l'orientation du sprite

                Vector2Int ccw = CCWDirForEdge(baseC, w, h);

                // Orientation initiale
                sr.sprite = GetSprite(FacingFromDelta(ccw));

                go.transform.localScale = Vector3.one * spriteScale;

                _trs.Add(go.transform);
                _srs.Add(sr);
                _baseCell.Add(baseC);
                _ccwDir.Add(ccw);
                _atOffset.Add(false); // Le danseur commence sur la base
            }
        }

        /// <summary>
        /// Détruit le rig des danseurs.
        /// </summary>
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

        /// <summary>
        /// Lance le mouvement des danseurs.
        /// </summary>
        private void StartRig()
        {
            if (_active || _root == null || _ring == null || _ring.Count == 0) return;

            // Réinitialise la position de tous les danseurs
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

        /// <summary>
        /// Arrête le mouvement des danseurs.
        /// </summary>
        private void StopRig()
        {
            _active = false;
            if (_loopCo != null) { StopCoroutine(_loopCo); _loopCo = null; }
        }

        /// <summary>
        /// Coroutine qui anime les danseurs à travers un cycle de danse.
        /// </summary>
        private IEnumerator DanceLoop()
        {
            var waitPause = (pauseSeconds > 0f) ? new WaitForSeconds(pauseSeconds) : null;

            while (_active)
            {
                // Mouvement anti-horaire
                yield return MoveAll(toOffset: true);
                if (waitPause != null) yield return waitPause;

                // Rotation (spin)
                yield return SpinAllOnce(+1);

                // Mouvement horaire
                yield return MoveAll(toOffset: false);
                if (waitPause != null) yield return waitPause;

                // Rotation (spin inverse)
                yield return SpinAllOnce(-1);
            }
        }

        /// <summary>
        /// Déplace tous les danseurs d'une case, soit vers l'offset (base+ccw), soit vers la base.
        /// </summary>
        private IEnumerator MoveAll(bool toOffset)
        {
            float dur = Mathf.Max(0.01f, moveSeconds);
            float t = 0f;

            var startPos = new Vector3[_trs.Count];
            var endPos = new Vector3[_trs.Count];

            // Fixe les destinations et les sprites d'orientation pendant le mouvement
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

        /// <summary>
        /// Fait tourner tous les danseurs d'un tour complet.
        /// </summary>
        private IEnumerator SpinAllOnce(int dirSign /* +1 = anti-horaire, -1 = horaire */)
        {
            float total = Mathf.Max(0.05f, spinSeconds);
            float seg = total / 4f;

            for (int step = 0; step < 4; step++)
            {
                for (int i = 0; i < _trs.Count; i++)
                {
                    // Orientation de base = direction du dernier pas
                    Vector2Int baseDelta = (dirSign >= 0) ? _ccwDir[i] : -_ccwDir[i];
                    var baseFacing = FacingFromDelta(baseDelta);
                    var spinFacing = (Facing)(((int)baseFacing + step) & 3);
                    _srs[i].sprite = GetSprite(spinFacing);
                }
                yield return new WaitForSeconds(seg);
            }
        }

        // --- Utilitaires ---

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
