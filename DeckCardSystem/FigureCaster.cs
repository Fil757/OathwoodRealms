using UnityEngine;
using UnityEngine.UI;                 // Layout-Refresh
using System.Reflection;              // Reflection (activePlayer)
using System.Collections;             // Coroutine / WaitForSeconds

namespace TCG
{
    /// <summary>
    /// Instanziiert Figuren aus der "Figure-Library" anhand DeckCard.Figure_ID.
    /// Ersetzt vorhandene Figur gleichen FIGURE_TYPE im Ziel-Root (P1 oder P2) – abhängig von TurnManager.activePlayer
    /// bzw. explizit übergebener Seite (P1/P2) bei CastFigureFromScratch.
    ///
    /// Änderung (minimal, wie gewünscht):
    /// - NUR im Replace-Fall (gleiches Zeichen/FIGURE_TYPE bereits auf dem Feld):
    ///   -> alte Figur wird per Kill-Sequenz zerstört (DamageToFigure.current.KillFigure)
    ///   -> neue Figur wird erst nach 1 Sekunde gespawnt (dann ganz normaler Cast-Flow)
    /// - In allen anderen Fällen bleibt alles wie vorher (keine Side-Template-Logik, kein Umkrempeln).
    /// </summary>
    [DisallowMultipleComponent]
    public class FigureCaster : MonoBehaviour
    {
        #region Inspector
        [Header("Scene Anchors")]
        [SerializeField] private Transform p1FiguresRoot;         // "Game-Canvas/P1_Figures"
        [SerializeField] private Transform p2FiguresRoot;         // "Game-Canvas/P2_Figures"
        [SerializeField] private Transform figureLibraryRoot;     // "Figure-Library" (Vorlagen als Kinder)
        [SerializeField] private Transform figureLibraryRoot_P2;  // "Figure-Library_P2" (Vorlagen als Kinder) (optional, bleibt ungenutzt wie zuvor)

        [Header("Integrations")]
        [SerializeField] private SpecialMoveController specialMoveController;

        [Header("Replace Timing")]
        [Tooltip("Delay (Sekunden) zwischen Kill der alten Figur und Spawn der neuen (nur Replace-Fall).")]
        [SerializeField] private float replaceSpawnDelaySeconds = 1f;

        [Header("VFX (Cast-Effekt)")]
        [Tooltip("Index im ParticleController.particleEffect[] (z.B. 6).")]
        [SerializeField] private int castEffectIndex = 6;

        [Tooltip("Lokaler Offset relativ zu EffectPosition (oder Figur, falls kein EffectPosition existiert).")]
        [SerializeField] private Vector3 castEffectOffset = Vector3.zero;

        [Tooltip("Skalierung der Partikelinstanz.")]
        [SerializeField] private Vector3 castEffectScale = new Vector3(60f, 60f, 60f);

        [Tooltip("Rotation der Partikelinstanz (Euler).")]
        [SerializeField] private Vector3 castEffectEuler = new Vector3(-90f, 0f, 0f);

        [Tooltip("Wenn vorhanden, als Parent 'EffectPosition' verwenden (folgt der Figur & korrektes Sorting).")]
        [SerializeField] private bool parentToEffectPosition = true;
        #endregion

        #region Unity
        private void Reset()
        {
            specialMoveController ??= FindObjectOfType<SpecialMoveController>(true);
            if (!p1FiguresRoot) p1FiguresRoot = GameObject.Find("Game-Canvas/P1_Figures")?.transform;
            if (!p2FiguresRoot) p2FiguresRoot = GameObject.Find("Game-Canvas/P2_Figures")?.transform;
            if (!figureLibraryRoot) figureLibraryRoot = GameObject.Find("Figure-Library")?.transform;
            if (!figureLibraryRoot_P2) figureLibraryRoot_P2 = GameObject.Find("Figure-Library_P2")?.transform; // optional
        }
        #endregion

        #region Public API
        public bool CanCast(DeckCard card)
        {
            return card != null && card.IsFigure;
        }

        public void Cast(DeckCard card)
        {
            if (!CanCast(card))
                return;

            AudioManager.Instance?.PlaySFX2D("Cast_Figure");
            AudioManager.Instance?.PlaySFX2D("Cast_Figure_Punch");

            bool castForP2 = IsActivePlayerP2Safe();
            _ = DoCast(card, castForP2, handleCosts: true);
        }

        public GameObject CastFigureFromScratch(DeckCard card, string playerSide)
        {
            if (!CanCast(card))
                return null;

            if (playerSide == "P1") // Nur für P1 da der Sound sonst gedoppelt wird
            {
                AudioManager.Instance?.PlaySFX2D("Cast_Figure");
                AudioManager.Instance?.PlaySFX2D("Cast_Figure_Punch");
            }

            bool castForP2 = ParseSideToP2(playerSide);
            return DoCast(card, castForP2, handleCosts: false);
        }
        #endregion

        #region Core Casting
        /// <summary>
        /// Kernlogik:
        /// - Kosten optional
        /// - Template bestimmen
        /// - FIGURE_TYPE bestimmen
        /// - Existing same type suchen
        /// - NUR wenn Replace: Kill + Delay + ReCast, sonst sofort spawnen wie vorher
        /// </summary>
        private GameObject DoCast(DeckCard card, bool castForP2, bool handleCosts)
        {
            // 1) Optionale Kosten-/Prüf-Logik
            if (handleCosts && specialMoveController)
            {
                specialMoveController.Play_FigureFromDeckCard(card);
            }

            // 2) Vorlage ermitteln (wie vorher: figureLibraryRoot bevorzugt, sonst Card.FigurePrefab)
            GameObject template = DetermineTemplate(card);
            if (template == null)
            {
                Debug.LogWarning("[FigureCaster] Template is null (DetermineTemplate failed).");
                return null;
            }

            // 3) FIGURE_TYPE ermitteln
            var dispTemplate = template.GetComponent<Display_Figure>();
            if (dispTemplate == null)
            {
                Debug.LogWarning($"[FigureCaster] Template '{template.name}' has no Display_Figure.");
                return null;
            }

            var newType = !string.IsNullOrEmpty(dispTemplate.FIGURE_TYPE)
                ? dispTemplate.FIGURE_TYPE
                : dispTemplate.FIGURE?.FIGURE_TYPE;

            if (string.IsNullOrEmpty(newType))
            {
                Debug.LogWarning($"[FigureCaster] Could not determine FIGURE_TYPE for template '{template.name}'.");
                return null;
            }

            // 4) Ziel-Root bestimmen
            Transform targetRoot = castForP2 ? p2FiguresRoot : p1FiguresRoot;
            if (targetRoot == null)
            {
                Debug.LogWarning($"[FigureCaster] TargetRoot is null for {(castForP2 ? "P2" : "P1")}.");
                return null;
            }

            // 5) Vorhandene Figur gleichen Typs im Ziel-Root finden
            GameObject existingSameType = FindChildWithFigureType(targetRoot, newType);

            // ===========================
            // 6) NEU: Replace-Fall -> Kill + Delay + ReCast
            // ===========================
            if (existingSameType != null)
            {
                // Stat-UI der alten Figur entfernen
                var df_existing = existingSameType.GetComponent<Display_Figure>();
                string killedSign = null;
                if (df_existing != null)
                {
                    killedSign = !string.IsNullOrEmpty(df_existing.FIGURE_TYPE)
                        ? df_existing.FIGURE_TYPE
                        : (df_existing.FIGURE != null ? df_existing.FIGURE.FIGURE_TYPE : null);
                }

                Debug.Log($"[FigureCaster] Replace detected → killing old figure '{existingSameType.name}' (sign={killedSign ?? "<null>"}) on {(castForP2 ? "P2" : "P1")}.");

                if (!string.IsNullOrEmpty(killedSign) && FigureStatsController.instance != null)
                {
                    if (castForP2)
                        FigureStatsController.instance.DestroyFigureStat_P2(killedSign);
                    else
                        FigureStatsController.instance.DestroyFigureStat_P1(killedSign);
                }

                // Kill-Sequenz (deine Vorgabe)
                if (DamageToFigure.current != null)
                    DamageToFigure.current.KillFigure(existingSameType);
                else
                    Destroy(existingSameType); // Fallback falls Controller fehlt

                // Nach 1 Sekunde casten wir nochmal ganz normal.
                // Wichtig: Wir brechen JETZT ab, damit es nicht instant ersetzt.
                StartCoroutine(SpawnAfterReplaceDelay(card, castForP2, handleCosts));
                return null;
            }

            // ===========================
            // 7) NORMALFALL: wie vorher sofort spawnen
            // ===========================

            var newFig = Instantiate(template, targetRoot);
            newFig.name = template.name;

            var disp = newFig.GetComponent<Display_Figure>();
            if (disp != null)
            {
                // Parameter aus Vorlage ziehen
                disp.Update_Parameter();

                // ► Runtime-Reset erzwingen: volle HP, 0% Load
                try
                {
                    if (disp.FIGURE_MAX_HEALTH > 0) disp.FIGURE_HEALTH = disp.FIGURE_MAX_HEALTH;
                    disp.FIGURE_LOAD = 0;
                }
                catch (System.Exception)
                {
                    // ignorieren
                }
            }

            // 7.1) Das FigurStatFeld der Figur instanziieren (verzögert)
            if (FigureStatsController.instance != null && disp != null)
            {
                ArrayRefresher.RefInstance?.RefreshFigureArrays();
                StartCoroutine(DeferredCreateStat(newFig, castForP2));
            }

            // 7.2) API - Calls an FieldCardController (beide Seiten, falls du 2 getrennte Controller nutzt)
            if (FieldCardController.instance != null)
                FieldCardController.instance.Figure_GotSummoned(newFig, true);
            if (FieldCardController_P2.instance != null)
                FieldCardController_P2.instance.Figure_GotSummoned(newFig, true);

            // Bekannter Workaround nur für P1: einmal weiterschalten, sonst „doppelt drücken“ nötig.
            if (!castForP2 && TurnManager.current != null)
            {
                TurnManager.current.NextFigureP1();
            }

            // 8) Transform sauber
            NormalizeTransform(newFig.transform);

            // 9) Optional Layout refresh pro Ziel-Root
            RefreshHorizontalLayout(targetRoot);

            // 10) Cast-VFX am neuen Objekt
            TryPlayCastVfx(newFig);

            Debug.Log($"[FigureCaster] Figur '{card.Figure_ID}' (Typ: {newType}) gesetzt → {(castForP2 ? "P2" : "P1")}");

            // 11) Arrays refreshen (z. B. TurnManager.Figures_P1/P2)
            ArrayRefresher.RefInstance?.RefreshFigureArrays();

            return newFig;
        }
        #endregion

        #region Replace Delay Routine
        private IEnumerator SpawnAfterReplaceDelay(DeckCard card, bool castForP2, bool handleCosts)
        {
            // Mindest-Delay (deine 1 Sekunde)
            if (replaceSpawnDelaySeconds > 0f)
                yield return new WaitForSeconds(replaceSpawnDelaySeconds);
            else
                yield return null;

            // Zusätzlich warten, bis keine gleich-typige Figur mehr im Root ist (max Timeout als Safety)
            float timeout = 3f;
            float t = 0f;

            GameObject template = DetermineTemplate(card);
            if (template == null) yield break;

            var dispTemplate = template.GetComponent<Display_Figure>();
            string newType = !string.IsNullOrEmpty(dispTemplate?.FIGURE_TYPE)
                ? dispTemplate.FIGURE_TYPE
                : dispTemplate?.FIGURE?.FIGURE_TYPE;

            Transform targetRoot = castForP2 ? p2FiguresRoot : p1FiguresRoot;

            while (t < timeout)
            {
                if (FindChildWithFigureType(targetRoot, newType) == null)
                    break;

                t += Time.deltaTime;
                yield return null;
            }

            // Sound beim echten Spawn abspielen (damit Replace-Spawn nicht "still" ist)
            AudioManager.Instance?.PlaySFX2D("Cast_Figure");
            AudioManager.Instance?.PlaySFX2D("Cast_Figure_Punch");

            DoCast(card, castForP2, handleCosts);
        }
        #endregion

        /// <summary>
        /// Erzeugt das Figuren-Stat-UI für genau diese neue Figur leicht verzögert.
        /// </summary>
        private IEnumerator DeferredCreateStat(GameObject newFig, bool castForP2)
        {
            yield return null;

            if (FigureStatsController.instance == null || newFig == null)
                yield break;

            FigureStatsController.instance.CreateFigureStatForFigure(newFig, castForP2);
        }

        #region Helpers
        private GameObject DetermineTemplate(DeckCard card)
        {
            if (figureLibraryRoot)
            {
                var child = figureLibraryRoot.Find(card.Figure_ID);
                if (child) return child.gameObject;
            }
            return card.FigurePrefab ? card.FigurePrefab : null;
        }

        // bleibt drin (unverändert), wird aber absichtlich NICHT verwendet – damit nichts am bisherigen P2-Verhalten bricht.
        private GameObject DetermineTemplate_P2(DeckCard card)
        {
            if (figureLibraryRoot_P2)
            {
                var child = figureLibraryRoot_P2.Find(card.Figure_ID);
                if (child) return child.gameObject;
            }
            return card.FigurePrefab ? card.FigurePrefab : null;
        }

        private void NormalizeTransform(Transform t)
        {
            if (t is RectTransform rt)
            {
                rt.localScale = Vector3.one;
                rt.anchoredPosition3D = Vector3.zero;
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                t.localScale = Vector3.one;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
            }
        }

        private void RefreshHorizontalLayout(Transform targetRoot)
        {
            var layout = targetRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout)
            {
                Canvas.ForceUpdateCanvases();
                if (targetRoot is RectTransform rtRoot)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rtRoot);
                Canvas.ForceUpdateCanvases();
            }
        }

        private GameObject FindChildWithFigureType(Transform parent, string gesuchterTyp)
        {
            if (!parent || string.IsNullOrEmpty(gesuchterTyp)) return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var df = child.GetComponent<Display_Figure>();
                if (!df) continue;

                var t = !string.IsNullOrEmpty(df.FIGURE_TYPE) ? df.FIGURE_TYPE : df.FIGURE?.FIGURE_TYPE;
                if (t == gesuchterTyp) return child.gameObject;
            }
            return null;
        }

        private void TryPlayCastVfx(GameObject newFig)
        {
            if (ParticleController.Instance == null || newFig == null) return;

            Transform effectPos = newFig.transform.Find("EffectPosition");
            Vector3 worldPos = (effectPos ? effectPos.position : newFig.transform.position) + castEffectOffset;
            Quaternion rot = Quaternion.Euler(castEffectEuler);
            Transform parent = (parentToEffectPosition && effectPos) ? effectPos : null;

            CameraShake.current.TriggerShake();

            ParticleController.Instance.PlayParticleEffect(
                worldPos,
                castEffectIndex,
                castEffectScale,
                rot,
                parent
            );
        }

        private bool IsActivePlayerP2Safe()
        {
            var tm = TurnManager.current;
            if (tm == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object value = null;

            var f = tm.GetType().GetField("activePlayer", flags);
            if (f != null) value = f.GetValue(tm);

            if (value == null)
            {
                var p = tm.GetType().GetProperty("activePlayer", flags);
                if (p != null && p.CanRead) value = p.GetValue(tm, null);
            }

            string s = value as string;
            if (string.IsNullOrEmpty(s)) return false;

            return string.Equals(s, "P2", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool ParseSideToP2(string side)
        {
            if (string.IsNullOrEmpty(side)) return false;
            return string.Equals(side, "P2", System.StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
