// FILE: Assets/DEV/Scripts/Disco/DiscoTileVisual.cs
using UnityEngine;

namespace Sarabande.Disco
{
    /// <summary>
    /// Visuel d’une dalle disco : "base" (tuile entière) + "core" (cœur central).
    /// OFF  : base grise, core caché.
    /// NEXT : base grise, core visible (petit carré lumineux).
    /// ON   : base entièrement colorée, core caché.
    /// </summary>
    public class DiscoTileVisual : MonoBehaviour
    {
        // Géométrie & couleurs (fournies par Setup)
        private float _cellSize;
        private float _tileSizeScale, _coreSizeScale, _tileThickness, _tileLift;
        private Material _baseMat, _coreMat;
        private Color _baseOffColor;
        private float _nextIntensity, _onIntensity;

        // Rendus
        private Renderer _baseR;
        private Renderer _coreR;

        public Vector2Int Cell { get; private set; }

        public void Setup(
            Vector2Int cell, float cellSize,
            float tileSizeScale, float coreSizeScale,
            float tileThickness, float tileLift,
            Material baseMaterial, Material coreMaterial,
            Color baseOffColor,
            float nextIntensity, float onIntensity)
        {
            Cell = cell;
            _cellSize = cellSize;
            _tileSizeScale = tileSizeScale;
            _coreSizeScale = coreSizeScale;
            _tileThickness = tileThickness;
            _tileLift = tileLift;
            _baseMat = baseMaterial;
            _coreMat = coreMaterial;
            _baseOffColor = baseOffColor;
            _nextIntensity = nextIntensity;
            _onIntensity = onIntensity;

            BuildGeometry();
            SetOff(); // état par défaut
        }

        private void BuildGeometry()
        {
            // --- Base (tuile pleine) ---
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = "Base";
            baseGo.transform.SetParent(transform, false);

            float sx = _tileSizeScale * _cellSize;
            float sy = _tileThickness;
            float sz = _tileSizeScale * _cellSize;

            baseGo.transform.localPosition = new Vector3(0f, _tileLift + sy * 0.5f, 0f);
            baseGo.transform.localScale = new Vector3(sx, sy, sz);

            var colB = baseGo.GetComponent<Collider>(); if (colB) Destroy(colB);
            _baseR = baseGo.GetComponent<Renderer>();
            if (_baseR)
            {
                _baseR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _baseR.receiveShadows = false;
                if (_baseMat) _baseR.sharedMaterial = _baseMat;
            }

            // --- Core (petit carré central, très fin, posé au-dessus de la base) ---
            var coreGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coreGo.name = "Core";
            coreGo.transform.SetParent(transform, false);

            float cx = _coreSizeScale * _cellSize;
            float cy = Mathf.Max(0.001f, _tileThickness * 0.5f); // très fin
            float cz = _coreSizeScale * _cellSize;

            // On le pose juste au-dessus pour éviter le z-fighting
            coreGo.transform.localPosition = new Vector3(0f, _tileLift + (sy + cy) * 0.5f + 0.0005f, 0f);
            coreGo.transform.localScale = new Vector3(cx, cy, cz);

            var colC = coreGo.GetComponent<Collider>(); if (colC) Destroy(colC);
            _coreR = coreGo.GetComponent<Renderer>();
            if (_coreR)
            {
                _coreR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _coreR.receiveShadows = false;
                if (_coreMat) _coreR.sharedMaterial = _coreMat;
            }
        }

        // --- États publics -------------------------------------------------------

        public void SetOff()
        {
            // Base grisée, core caché
            SetRendererColor(_baseR, _baseOffColor, emission: false);
            if (_coreR) _coreR.enabled = false;
        }

        public void SetNext(Color c)
        {
            // Base grisée, core visible (lumineux)
            SetRendererColor(_baseR, _baseOffColor, emission: false);
            if (_coreR)
            {
                _coreR.enabled = true;
                SetRendererColor(_coreR, c * _nextIntensity, emission: true);
            }
        }

        public void SetOn(Color c)
        {
            // Base pleine lumineuse, core caché
            SetRendererColor(_baseR, c * _onIntensity, emission: true);
            if (_coreR) _coreR.enabled = false;
        }

        // --- Utilitaires couleur/emission ----------------------------------------

        private static void SetRendererColor(Renderer r, Color c, bool emission)
        {
            if (!r) return;

            // On clone le material instance pour éviter de toucher le sharedMaterial
            var mat = r.material;
            if (mat.HasProperty("_Color")) mat.color = c;

            if (emission && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c);
            }
            else if (mat.HasProperty("_EmissionColor"))
            {
                // coupe l'émission
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
        }
    }
}
