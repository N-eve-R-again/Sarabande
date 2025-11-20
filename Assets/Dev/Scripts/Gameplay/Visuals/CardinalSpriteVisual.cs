// FILE: Assets/Dev/Scripts/Visuals/CardinalSpriteVisual.cs
using UnityEngine;
using Sarabande.Core; // EdgeDirection

namespace Sarabande.Visuals
{
    [System.Serializable]
    public struct CardinalSprites
    {
        public Sprite north;
        public Sprite east;
        public Sprite south;
        public Sprite west;
    }

    /// <summary>
    /// Sprite top-down à plat sur XZ :
    /// - n’hérite pas du yaw du parent (reste “pieds vers le bas”)
    /// - change de sprite selon la direction (N/E/S/W) déduite du forward du parent
    /// - s’aligne (option) sur la largeur d’une case
    /// </summary>
    public class CardinalSpriteVisual : MonoBehaviour
    {
        [Header("Sprites (drag & drop)")]
        public CardinalSprites sprites;

        [Header("Layout")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;
        [SerializeField] private float yOffset = 0.02f;
        [SerializeField] private bool fitToCell = true;
        [SerializeField, Range(0.1f, 2f)] private float scale = 1f;

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int orderInLayer = 10;

        [SerializeField] private bool flipVertical = true; // pieds en bas sans casser E/O

        private SpriteRenderer _sr;
        private CardinalDirection _facing = CardinalDirection.South;

        public void SetFacing(CardinalDirection dir)
        {
            if (_facing == dir) return;
            _facing = dir;
            RefreshSprite();
        }

        private void Awake()
        {
            EnsureRenderer();
            ApplyLayout();
            RefreshSprite();
            FixRotation(); // pour l’aperçu immediate en Play
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureRenderer();
                ApplyLayout();
                RefreshSprite();
                FixRotation();
            }
        }

        private void Update()
        {
            /*// déduire la direction depuis le forward XZ du parent
            Vector3 f = transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) return;

            CardinalDirection dir = (Mathf.Abs(f.x) > Mathf.Abs(f.z))
                ? (f.x >= 0f ? CardinalDirection.East : CardinalDirection.West)
                : (f.z >= 0f ? CardinalDirection.North : CardinalDirection.South);

            SetFacing(dir);*/
        }

        private void LateUpdate()
        {
            // annule le yaw parent + applique la pose top-down avec correction 180°
            FixRotation();
        }

        // --- internals ---

        private void EnsureRenderer()
        {
            if (_sr != null) return;

            var child = transform.Find("Sprite");
            if (child == null)
            {
                var go = new GameObject("Sprite");
                go.transform.SetParent(transform, false);
                _sr = go.AddComponent<SpriteRenderer>();
            }
            else
            {
                _sr = child.GetComponent<SpriteRenderer>() ?? child.gameObject.AddComponent<SpriteRenderer>();
            }

            _sr.sortingLayerName = sortingLayerName;
            _sr.sortingOrder = orderInLayer;
            _sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sr.receiveShadows = false;
        }

        private void ApplyLayout()
        {
            if (_sr == null) return;

            _sr.transform.localPosition = new Vector3(0f, yOffset, 0f);

            if (!fitToCell || _sr.sprite == null)
            {
                float sy = flipVertical ? -scale : scale;
                _sr.transform.localScale = new Vector3(scale, sy, 1f);
                return;
            }

            float spriteWorldWidth = _sr.sprite.bounds.size.x;
            if (spriteWorldWidth <= 0f) spriteWorldWidth = 1f;

            float targetWidth = cellSize * scale;
            float s = targetWidth / spriteWorldWidth;
            float sy2 = flipVertical ? -s : s;
            _sr.transform.localScale = new Vector3(s, sy2, 1f);
        }

        private void RefreshSprite()
        {
            if (_sr == null) return;

            Sprite s = sprites.south;
            switch (_facing)
            {
                case CardinalDirection.North: s = sprites.north; break;
                case CardinalDirection.East: s = sprites.east; break; // mapping naturel
                case CardinalDirection.South: s = sprites.south; break;
                case CardinalDirection.West: s = sprites.west; break; // mapping naturel
            }
            _sr.sprite = s;
            ApplyLayout();
        }

        private void FixRotation()
        {
            if (!_sr) return;
            float parentYaw = transform.eulerAngles.y;
            _sr.transform.localRotation = Quaternion.Euler(-90f, -parentYaw, 0f);
        }
    }
}
