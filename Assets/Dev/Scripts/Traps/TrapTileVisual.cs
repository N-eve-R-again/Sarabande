using UnityEngine;

namespace Sarabande.Traps
{
    /// <summary>
    /// Visuel de dalle : léger relief qui peut s'enfoncer et remonter.
    /// Le GameObject porteur est le mesh de la dalle (un cube fin).
    /// </summary>
    public class TrapTileVisual : MonoBehaviour
    {
        [Header("Cell (auto)")]
        public Vector2Int Cell { get; private set; }

        [Header("Animation (centres Y en unités monde)")]
        [SerializeField] private float yUp = 0.02f;      // centre Y de la dalle “au repos”
        [SerializeField] private float yDown = 0.0f;     // centre Y de la dalle enfoncée (affleurant le sol)
        [SerializeField, Min(0.01f)] private float pressDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.15f;
        [SerializeField] private MeshRenderer mr;

        private Coroutine _anim;

        private void Awake()
        {
            if (!mr) mr = GetComponent<MeshRenderer>();
        }

        public void SetupCell(Vector2Int cell) => Cell = cell;

        public void ConfigureHeights(float upCenterY, float downCenterY)
        {
            yUp = upCenterY;
            yDown = downCenterY;
            SetY(yUp);
        }

        public void ConfigureDurations(float press, float release)
        {
            pressDuration = press;
            releaseDuration = release;
        }

        public void Press()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimateTo(yDown, pressDuration));
        }

        public void Release()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimateTo(yUp, releaseDuration));
        }
        public void ResetVisual()
        {
            if (_anim != null) { StopCoroutine(_anim); _anim = null; }
            if (mr) mr.enabled = true;
            SetY(yUp);
        }

        private System.Collections.IEnumerator AnimateTo(float targetY, float duration)
        {
            float startY = transform.position.y;
            float t = 0f;
            duration = Mathf.Max(0.0001f, duration);
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                if (t > 1f) t = 1f;
                var p = transform.position; p.y = Mathf.Lerp(startY, targetY, t); transform.position = p;
                yield return null;
            }
            _anim = null;
        }

        private void SetY(float y)
        {
            var p = transform.position; p.y = y; transform.position = p;
        }

        // nouveau: enfonce puis cache la dalle
        public void PressAndHide()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(PressThenHide());
        }
        public void SetVisible(bool v)
        {
            if (mr) mr.enabled = v;
        }
        private System.Collections.IEnumerator PressThenHide()
        {
            // anim d’enfoncement (même logique que Press())
            float startY = transform.position.y;
            float targetY = yDown;
            float t = 0f;
            float dur = Mathf.Max(0.0001f, pressDuration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                if (t > 1f) t = 1f;
                var p = transform.position; p.y = Mathf.Lerp(startY, targetY, t); transform.position = p;
                yield return null;
            }
            // puis on “disparaît”
            if (mr) mr.enabled = false;
            _anim = null;
        }
        // nouveau: réapparaît puis remonte
        public void ShowThenRelease()
        {
            if (_anim != null) StopCoroutine(_anim);
            if (mr) mr.enabled = true;     // ré-apparition visuelle
            _anim = StartCoroutine(AnimateTo(yUp, releaseDuration));
        }
    }
}
