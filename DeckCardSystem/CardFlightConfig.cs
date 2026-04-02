using UnityEngine;

namespace TCG
{
    [DisallowMultipleComponent]
    public class CardFlightConfig : MonoBehaviour
    {
        public static CardFlightConfig Instance { get; private set; }

        #region Inspector

        [Header("Central Target (GUI)")]
        [Tooltip("Wenn gesetzt, wird dieser Punkt verwendet. Ansonsten Fallback über 'centralPointName'.")]
        public RectTransform explicitCentralPoint;
        public string centralPointName = "GAME-GUI-Central-Point";

        [Tooltip("Zusätzlicher Offset im Zielraum des Karten-Canvas (Pixel, lokale Anchored-Koordinate).")]
        public Vector2 targetOffset = Vector2.zero;

        [Header("Timings")]
        [Min(0.01f)] public float moveUpTime = 0.25f;     // Hinflug + Scale-Up
        [Min(0f)] public float holdTime = 0.08f;     // kurzes Warten
        [Min(0.01f)] public float shrinkTime = 0.22f;     // Shrink + Fade-Out

        [Header("Scale / Fade")]
        [Min(1f)] public float scaleUp = 1.18f;           // max. Größe auf dem Weg
        [Range(0.1f, 0.99f)]
        public float castTriggerAt = 0.85f;               // Anteil der ShrinkTime, wann gecastet wird
        public bool keepInvisibleInGraveyard = true;      // Nach Landung unsichtbar lassen

        [Header("Debug / Sicherheit")]
        [Tooltip("Wenn true: Logs beim Auflösen von Canvas/Kamera/Target. Gut für Fehlersuche.")]
        public bool verboseLogging = false;

        [Tooltip("Wenn true: Warnen, falls Karten-Canvas und CentralPoint auf unterschiedlichen Canvas liegen.")]
        public bool warnWhenDifferentCanvas = true;

        [Tooltip("Wenn true: Wirft eine Warnung, falls CentralPoint via Name nicht eindeutig (mehrfach) vorhanden ist.")]
        public bool warnOnMultiMatchByName = true;

        #endregion

        #region Lifecycle

        void Awake()
        {
            Instance = this;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!explicitCentralPoint && string.IsNullOrWhiteSpace(centralPointName))
            {
                Debug.LogWarning($"[{nameof(CardFlightConfig)}] Weder explicitCentralPoint gesetzt noch centralPointName definiert. Zielauflösung wird immer auf Offset ({targetOffset}) zurückfallen.");
            }
        }
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Rechnet die Weltposition des CentralPoints in eine lokale Anchored-Position
        /// im Canvas der übergebenen Karte (cardRT) um. Robuste Cross-Canvas-Konvertierung.
        /// Fallback: targetOffset.
        /// </summary>
        public Vector2 ResolveTargetAnchoredPosForCard(RectTransform cardRT)
        {
            if (!cardRT)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[{nameof(CardFlightConfig)}] cardRT == null → Fallback Offset {targetOffset}");
                return targetOffset;
            }

            // 1) Card-Canvas + Kamera ermitteln (immer frisch).
            if (!TryGetCanvasAndCamera(cardRT, out var cardCanvas, out var cardCam))
            {
                if (verboseLogging)
                    Debug.LogWarning($"[{nameof(CardFlightConfig)}] Kein Canvas im Parent von '{cardRT.name}' gefunden → Fallback Offset {targetOffset}");
                return targetOffset;
            }

            // 2) CentralPoint zuverlässig ermitteln.
            var center = ResolveCentralPoint();
            if (!center)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[{nameof(CardFlightConfig)}] Kein CentralPoint gefunden → Fallback Offset {targetOffset}");
                return targetOffset;
            }

            // 3) Canvas/Kamera des CentralPoints ermitteln.
            Camera centerCam = null;
            Canvas centerCanvas = center.GetComponentInParent<Canvas>();
            if (centerCanvas)
            {
                switch (centerCanvas.renderMode)
                {
                    case RenderMode.ScreenSpaceCamera:
                        centerCam = centerCanvas.worldCamera;
                        break;
                    case RenderMode.WorldSpace:
                        centerCam = centerCanvas.worldCamera ? centerCanvas.worldCamera : Camera.main;
                        break;
                    case RenderMode.ScreenSpaceOverlay:
                        centerCam = null;
                        break;
                }
            }
            else
            {
                centerCam = Camera.main;
            }

            if (warnWhenDifferentCanvas && centerCanvas && cardCanvas && centerCanvas != cardCanvas && verboseLogging)
            {
                Debug.Log($"[{nameof(CardFlightConfig)}] Hinweis: CentralPoint.Canvas ('{CanvasPath(centerCanvas)}') != Card.Canvas ('{CanvasPath(cardCanvas)}'). Cross-Canvas-Umrechnung wird verwendet.");
            }

            // 4) Welt → Screen (CentralPoint)
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(centerCam, center.position);

            // 5) Screen → Local im Card-Canvas
            var cardCanvasRT = cardCanvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(cardCanvasRT, screenPos, cardCam, out var local))
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        $"[{nameof(CardFlightConfig)}] OK: Center='{FullPath(center)}' | screen={screenPos} → local={local} " +
                        $"(CardCanvas='{CanvasPath(cardCanvas)}', cam='{(cardCam ? cardCam.name : "null")}') | Offset={targetOffset}");
                }
                return local + targetOffset;
            }

            if (verboseLogging)
            {
                Debug.LogWarning(
                    $"[{nameof(CardFlightConfig)}] Screen→Local failed. " +
                    $"center='{FullPath(center)}' screen={screenPos} cardCanvas='{CanvasPath(cardCanvas)}' cam='{(cardCam ? cardCam.name : "null")}'. " +
                    $"→ Fallback Offset {targetOffset}");
            }
            return targetOffset;
        }

        #endregion

        #region Helpers (Canvas/Kamera/Target)

        private bool TryGetCanvasAndCamera(RectTransform rt, out Canvas canvas, out Camera cam)
        {
            canvas = rt.GetComponentInParent<Canvas>();
            if (!canvas)
            {
                cam = null;
                return false;
            }

            switch (canvas.renderMode)
            {
                case RenderMode.ScreenSpaceCamera:
                    cam = canvas.worldCamera;
                    break;
                case RenderMode.WorldSpace:
                    cam = canvas.worldCamera ? canvas.worldCamera : Camera.main;
                    break;
                case RenderMode.ScreenSpaceOverlay:
                    cam = null;
                    break;
                default:
                    cam = null;
                    break;
            }

            return true;
        }

        private RectTransform ResolveCentralPoint()
        {
            if (explicitCentralPoint)
                return explicitCentralPoint;

            if (string.IsNullOrWhiteSpace(centralPointName))
                return null;

            var go = GameObject.Find(centralPointName);
            if (!go) return null;

            var rt = go.transform as RectTransform;
            if (!rt) return null;

            if (warnOnMultiMatchByName)
            {
                var all = FindObjectsOfType<RectTransform>(includeInactive: true);
                int count = 0;
                foreach (var r in all)
                    if (r.name == centralPointName) count++;

                if (count > 1)
                {
                    Debug.LogWarning(
                        $"[{nameof(CardFlightConfig)}] Mehrere Objekte mit dem Namen '{centralPointName}' gefunden ({count} Treffer). " +
                        $"Bitte 'explicitCentralPoint' im Inspector setzen, um Sprünge zu vermeiden.");
                }
            }

            return rt;
        }

        private static string FullPath(Transform t)
        {
            if (!t) return "null";
            string p = t.name;
            Transform cur = t.parent;
            while (cur)
            {
                p = cur.name + "/" + p;
                cur = cur.parent;
            }
            return p;
        }

        private static string CanvasPath(Canvas c)
        {
            return c ? FullPath(c.transform) + $" (mode={c.renderMode}, cam={(c.worldCamera ? c.worldCamera.name : "null")})" : "null";
        }

        #endregion
    }
}
