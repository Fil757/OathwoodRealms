using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake current;

    [Header("UI/Canvas, die gemeinsam 'als Kamera' wackeln")]
    [SerializeField] RectTransform[] targetCanvases; // mehrere Canvases im Inspector zuweisen
    private Vector3[] originalLocalPositions;

    [System.Serializable]
    public struct ExtraFollower
    {
        public Transform target;      // Beliebiges Objekt
        [Range(0f, 2f)] public float intensity; // 1 = identisch zu Canvas, 0.5 = halb so stark
        public bool invert;           // Richtung umkehren (selten nötig, aber praktisch)
        public Vector2 axisMask;      // (1,1)=beide Achsen, (1,0)=nur X, (0,1)=nur Y
    }

    [Header("Objekte, die synchron zum Canvas-Offset wackeln")]
    [SerializeField] ExtraFollower[] extraObjects;
    private Vector3[] extraOriginalLocalPositions;

    void Awake()
    {
        current = this;
    }

    void OnEnable()
    {
        // Canvas-Originalpositionen puffern
        if (targetCanvases != null && targetCanvases.Length > 0)
        {
            originalLocalPositions = new Vector3[targetCanvases.Length];
            for (int i = 0; i < targetCanvases.Length; i++)
                if (targetCanvases[i] != null)
                    originalLocalPositions[i] = targetCanvases[i].localPosition;
        }

        // Extra-Objekt-Originalpositionen puffern
        if (extraObjects != null && extraObjects.Length > 0)
        {
            extraOriginalLocalPositions = new Vector3[extraObjects.Length];
            for (int i = 0; i < extraObjects.Length; i++)
                if (extraObjects[i].target != null)
                    extraOriginalLocalPositions[i] = extraObjects[i].target.localPosition;
        }
    }

    public void TriggerShake() => Shake(0.75f, 5f);

    /// <summary>
    /// Canvas teilen sich einen gemeinsamen Offset.
    /// Extra-Objekte folgen exakt demselben Offset (optional skaliert / gefiltert / invertiert),
    /// jeweils relativ zur eigenen Ausgangsposition.
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        bool hasCanvasTargets = targetCanvases != null && targetCanvases.Length > 0;
        bool hasExtraObjects  = extraObjects   != null && extraObjects.Length   > 0;

        if (!hasCanvasTargets && !hasExtraObjects) return;

        StopAllCoroutines();
        StartCoroutine(DoShake(duration, magnitude));
    }

    private IEnumerator DoShake(float duration, float magnitude)
    {
        float elapsed = 0f;

        EnsureOriginalsBuffered();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float damper = 1f - t; // linearer Abklingfaktor (bei Bedarf easing einsetzen)

            // Gemeinsamer Offset für diesen Frame (Kamera-/Canvas-Feeling)
            float cx = (Random.value * 2f - 1f) * magnitude;
            float cy = (Random.value * 2f - 1f) * magnitude;
            Vector3 camOffset = new Vector3(cx * damper, cy * damper, 0f);

            // Canvas anwenden
            if (targetCanvases != null && originalLocalPositions != null)
            {
                for (int i = 0; i < targetCanvases.Length; i++)
                {
                    if (targetCanvases[i] == null) continue;
                    targetCanvases[i].localPosition = originalLocalPositions[i] + camOffset;
                }
            }

            // Extra-Objekte exakt diesem Offset folgen lassen (mit Optionen)
            if (extraObjects != null && extraOriginalLocalPositions != null)
            {
                for (int i = 0; i < extraObjects.Length; i++)
                {
                    var fol = extraObjects[i];
                    if (fol.target == null) continue;

                    // Achsen filtern + Intensität + optional invertieren
                    Vector3 filtered = new Vector3(
                        camOffset.x * fol.axisMask.x,
                        camOffset.y * fol.axisMask.y,
                        0f
                    );

                    float sign = fol.invert ? -1f : 1f;
                    Vector3 finalOffset = -filtered * (fol.intensity <= 0f ? 1f : fol.intensity) * sign;
                    fol.target.localPosition = extraOriginalLocalPositions[i] + finalOffset;
                }
            }

            yield return null;
        }

        // Positionen zurücksetzen
        ResetAllToOriginals();
    }

    private void EnsureOriginalsBuffered()
    {
        if (targetCanvases != null && (originalLocalPositions == null || originalLocalPositions.Length != targetCanvases.Length))
        {
            originalLocalPositions = new Vector3[targetCanvases.Length];
            for (int i = 0; i < targetCanvases.Length; i++)
                if (targetCanvases[i] != null) originalLocalPositions[i] = targetCanvases[i].localPosition;
        }

        if (extraObjects != null && (extraOriginalLocalPositions == null || extraOriginalLocalPositions.Length != extraObjects.Length))
        {
            extraOriginalLocalPositions = new Vector3[extraObjects.Length];
            for (int i = 0; i < extraObjects.Length; i++)
                if (extraObjects[i].target != null) extraOriginalLocalPositions[i] = extraObjects[i].target.localPosition;
        }
    }

    private void ResetAllToOriginals()
    {
        if (targetCanvases != null && originalLocalPositions != null)
        {
            for (int i = 0; i < targetCanvases.Length; i++)
                if (targetCanvases[i] != null) targetCanvases[i].localPosition = originalLocalPositions[i];
        }

        if (extraObjects != null && extraOriginalLocalPositions != null)
        {
            for (int i = 0; i < extraObjects.Length; i++)
                if (extraObjects[i].target != null) extraObjects[i].target.localPosition = extraOriginalLocalPositions[i];
        }
    }
}
