using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wendet Schaden auf eine Figur an, spielt Trefferfeedback (3D-Shake, Popups, Partikel),
/// aktualisiert die Healthbar und triggert den Kill-Flow, sobald HP <= 0 fallen.
/// - Verhindert Doppel-Kills per HashSet
/// - Stoppt laufende Model-Shakes vor Zerstörung
/// - Warteframe vor Array-Refresh gegen "Ghost-Refs"
/// - NEU: Spill-Damage auf Player-Base, wenn die Figur NICHT verteidigt (IS_DEFENDING == false).
///        Spill-Definition: (eingehender Angriff) - (ATK der verteidigenden Figur), min 0.
///        Bei aktiver Defense KEIN Spill (egal wie hoch die Differenz).
/// - NEU: Individuelle Shake-Profile pro Defender-Name (Fallback: Standardwerte)
/// </summary>
public class DamageToFigure : MonoBehaviour
{
    public static DamageToFigure current;

    // =========================
    // Shake Profiles (NEU)
    // =========================
    [System.Serializable]
    public class ShakeProfile
    {
        [Tooltip("Exakter GameObject-Name der Figur (defender.name).")]
        public string defenderName;

        [Tooltip("Individuelle Dauer in Sekunden.")]
        public float duration = 0.25f;

        [Tooltip("Individuelle Intensität (Magnitude).")]
        public float magnitude = 0.5f;
    }

    [Header("Shake (3D Model) - Standard")]
    [Tooltip("Standard-Dauer des 3D-Shakes in Sekunden (Fallback).")]
    public float shakeDuration3D = 0.25f;

    [Tooltip("Standard-Amplitude des 3D-Shakes (Fallback).")]
    public float shakeMagnitude3D = 0.5f;

    [Header("Shake Profiles (optional)")]
    [Tooltip("Wenn defender.name hier gematcht wird, werden diese Werte statt der Standardwerte genutzt.")]
    public List<ShakeProfile> shakeProfiles = new List<ShakeProfile>();

    // Optional: Cache für schnellen Lookup (Name -> Profile)
    private Dictionary<string, ShakeProfile> shakeProfileCache;

    // Laufende Shakes je Ziel-Transform und deren ursprüngliche lokale Position
    private readonly Dictionary<Transform, Coroutine> runningModelShakes = new();
    private readonly Dictionary<Transform, Vector3> modelOriginalLocalPos = new();

    // Schutz vor Mehrfach-Kills im selben Frame / bei mehrfachen Treffern
    private readonly HashSet<GameObject> pendingKills = new();

    // --- Caches für Player-Base-Objekte ---
    private GameObject p1BaseCached, p2BaseCached;

    private void Awake()
    {
        current = this;

        // Cache bauen (Name -> Profile). Falls Duplikate: erster gewinnt.
        shakeProfileCache = new Dictionary<string, ShakeProfile>(System.StringComparer.Ordinal);
        for (int i = 0; i < shakeProfiles.Count; i++)
        {
            var p = shakeProfiles[i];
            if (p == null) continue;
            if (string.IsNullOrWhiteSpace(p.defenderName)) continue;

            if (!shakeProfileCache.ContainsKey(p.defenderName))
                shakeProfileCache.Add(p.defenderName, p);
        }
    }

    /// <summary>
    /// Haupt-API: Schaden anwenden, Trefferfeedback, Healthbar-Update, Kill bei HP <= 0.
    /// </summary>
    public void ApplyDamage(GameObject defender, int damage)
    {
        if (defender == null) return;

        // API - Call an den FieldCardController
        FieldCardController.instance.Figure_TakenDamage(defender, damage);
        FieldCardController_P2.instance.Figure_TakenDamage(defender, damage);

        var figureScript = defender.GetComponent<Display_Figure>();
        if (figureScript == null) return;

        var defendersHealthBar = figureScript.HealthBar;

        int hpCurrent = figureScript.FIGURE_HEALTH;
        int hpMax = figureScript.FIGURE_MAX_HEALTH;
        int defense = figureScript.FIGURE_DEF;
        bool isDef = figureScript.IS_DEFENDING;

        // Effektiver Schaden (bei Defense reduziert)
        int givenDamage = isDef ? Mathf.Max(0, damage - defense) : damage;

        // === Spill-Damage auf die Player-Base, falls NICHT verteidigt ===
        // Spill-Definition:
        //   overflow = max(0, (eingehender Angriff 'damage') - (ATK der verteidigenden Figur))
        // Nur wenn Ziel KEINE Defense aktiv hat und Ziel KEINE Base ist.
        if (!IsBase(defender) && !isDef)
        {
            int defenderATK = figureScript.FIGURE_ATK;
            int overflowToBase = Mathf.Max(0, damage - defenderATK);

            if (overflowToBase > 0)
            {
                var playerBase = ResolvePlayerBaseFor(defender);
                if (playerBase != null)
                {
                    PlayerBaseController.current.SpecialMove_PlayerStatChange(playerBase, "Damage_quiet", overflowToBase);
                    // Debug.Log("Overflow Damage beträgt " + overflowToBase);
                }
            }
        }

        // === Trefferfeedback (3D-Shake) ===
        GameObject model3D = defender.transform.Find("Model/3D-Model")?.gameObject;
        if (model3D != null)
        {
            GetShakeSettingsForDefender(defender, out float d, out float m);
            AnimateDamageToModel3D(model3D, d, m);
        }

        // PopUp für den Treffer (zeigt eingehenden Angriffswert an)
        PopUp.current?.FigurePopUp("Damage", givenDamage, defender);

        // === Health anwenden & clampen ===
        int newHp = Mathf.Max(0, hpCurrent - givenDamage);
        figureScript.FIGURE_HEALTH = newHp;

        // === Healthbar animieren/updaten ===
        if (defendersHealthBar != null)
        {
            HealthBarController.current?.ChangeHealthBar(
                givenDamage, defendersHealthBar, hpCurrent, hpMax
            );
        }

        // === Kill bei 0 HP ===
        if (newHp <= 0)
        {
            // Optional: Basen NICHT zerstören
            if (!IsBase(defender))
            {
                if (!pendingKills.Contains(defender))
                {
                    pendingKills.Add(defender);

                    // Laufende Model-Shakes anhalten & Positionen restoren bevor wir zerstören
                    StopShakesFor(defender.transform.Find("Model/3D-Model"));

                    KillFigure(defender);
                }
            }
        }
    }

    // =========================
    // Shake Settings (NEU)
    // =========================
    private void GetShakeSettingsForDefender(GameObject defender, out float duration, out float magnitude)
    {
        // Default
        duration = shakeDuration3D;
        magnitude = shakeMagnitude3D;

        if (defender == null) return;

        if (shakeProfileCache != null &&
            shakeProfileCache.TryGetValue(defender.name, out var profile) &&
            profile != null)
        {
            duration = profile.duration;
            magnitude = profile.magnitude;
        }
    }

    /// <summary>
    /// Shaket das erste sinnvolle Child unter "Model/3D-Model" (oder root, wenn kein Mesh gefunden wird).
    /// </summary>
    public void AnimateDamageToModel3D(GameObject modelGO, float duration, float magnitude)
    {
        if (modelGO == null) return;

        Transform target = GetActiveFigureChild(modelGO.transform);
        if (target == null) return;

        // Bereits laufenden Shake auf diesem Target sauber abbrechen
        if (runningModelShakes.TryGetValue(target, out var running))
        {
            StopCoroutine(running);
            RestoreModelLocalPos(target);
            runningModelShakes.Remove(target);
            modelOriginalLocalPos.Remove(target);
        }

        AudioManager.Instance?.PlaySFX2D("Damage");
        AudioManager.Instance?.PlaySFX2D("Damage_ground");

        // Treffereffekt (Partikel) am Model-Worldpos
        if (ParticleController.Instance != null)
        {
            ParticleController.Instance.PlayParticleEffect(
                modelGO.transform.position,
                8,
                new Vector3(60f, 60f, 60f),
                Quaternion.Euler(-90f, 0f, 0f)
            );
        }

        // 3D-Shake starten (mit individuellen Werten)
        var co = StartCoroutine(ShakeModelCoroutine(target, duration, magnitude));
        runningModelShakes[target] = co;
    }

    /// <summary>
    /// Ermittelt das erste aktive Child (meist das instanzierte Figuren-Prefab).
    /// Bevorzugt ein sichtbares Mesh (MeshRenderer/SkinnedMeshRenderer) als Shake-Target.
    /// </summary>
    private Transform GetActiveFigureChild(Transform root)
    {
        if (root == null) return null;
        if (root.childCount == 0) return root;

        Transform firstActiveChild = null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.gameObject.activeInHierarchy)
            {
                firstActiveChild = c;
                break;
            }
        }

        if (firstActiveChild == null) return root;

        Transform meshTarget = FindShakeTarget(firstActiveChild);
        return meshTarget != null ? meshTarget : firstActiveChild;
    }

    /// <summary>
    /// Sucht innerhalb eines Objekts nach einem sichtbaren MeshRenderer/SkinnedMeshRenderer.
    /// </summary>
    private Transform FindShakeTarget(Transform root)
    {
        if (root == null) return null;

        if (root.GetComponent<MeshRenderer>() != null || root.GetComponent<SkinnedMeshRenderer>() != null)
            return root;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.GetComponent<MeshRenderer>() != null || child.GetComponent<SkinnedMeshRenderer>() != null)
                return child;
        }

        return root;
    }

    /// <summary>
    /// Stellt die lokale Position eines zuvor geshakten Targets wieder her.
    /// </summary>
    private void RestoreModelLocalPos(Transform t)
    {
        if (t != null && modelOriginalLocalPos.TryGetValue(t, out var orig))
            t.localPosition = orig;
    }

    /// <summary>
    /// Stoppt (falls vorhanden) den laufenden Shake auf dem "Model/3D-Model" und restoret die lokale Position.
    /// </summary>
    private void StopShakesFor(Transform modelRoot)
    {
        if (modelRoot == null) return;
        var target = GetActiveFigureChild(modelRoot);
        if (target == null) return;

        if (runningModelShakes.TryGetValue(target, out var running))
        {
            StopCoroutine(running);
            RestoreModelLocalPos(target);
            runningModelShakes.Remove(target);
            modelOriginalLocalPos.Remove(target);
        }
    }

    /// <summary>
    /// Einfache Shake-Coroutine mit linear abklingender Amplitude.
    /// </summary>
    private IEnumerator ShakeModelCoroutine(Transform t, float duration, float magnitude)
    {
        if (t == null) yield break;

        Vector3 original = t.localPosition;
        modelOriginalLocalPos[t] = original;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (t == null) yield break;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float damper = 1f - progress;

            float ox = (Random.value * 2f - 1f) * magnitude * damper;
            float oy = (Random.value * 2f - 1f) * magnitude * damper;
            float oz = (Random.value * 2f - 1f) * magnitude * 0.5f * damper;

            t.localPosition = original + new Vector3(ox, oy, oz);
            yield return null;
        }

        if (t != null)
            t.localPosition = original;

        runningModelShakes.Remove(t);
        modelOriginalLocalPos.Remove(t);
    }

    /// <summary>
    /// Public API: Figur töten (Sequenz mit FX, CamShake, Destroy, Array-Refresh).
    /// </summary>
    public void KillFigure(GameObject killed_figure)
    {
        FieldCardController.instance.Figure_GotKilled(killed_figure, true);
        FieldCardController_P2.instance.Figure_GotKilled(killed_figure, true);
        StartCoroutine(KillSequence(killed_figure));
    }

    /// <summary>
    /// Kill-Sequenz:
    /// - Partikel an EffectPosition (Fallback: Figur-Pos)
    /// - CameraShake
    /// - Destroy(GameObject)
    /// - WaitForEndOfFrame
    /// - ArrayRefresher
    /// </summary>
    private IEnumerator KillSequence(GameObject killed_figure)
    {
        if (killed_figure == null) yield break;

        Transform effectPos = killed_figure.transform.Find("EffectPosition");
        Vector3 fxPos = effectPos != null ? effectPos.position : killed_figure.transform.position;

        // Todes-Explosion
        if (ParticleController.Instance != null)
        {
            ParticleController.Instance.PlayParticleEffect(
                fxPos,
                10,
                new Vector3(60f, 60f, 60f),
                Quaternion.Euler(-90f, 0f, 0f)
            );
        }

        // Kamera-Shake
        CameraShake.current.Shake(0.75f, 5f);

        // Figur zerstören
        AudioManager.Instance?.PlaySFX2D("Destroy_Figure");
        AudioManager.Instance?.PlaySFX2D("Destroy_Figure_ground");
        Destroy(killed_figure);

        // Frame warten, damit Unity die Destroy()-Operation wirklich umsetzt
        yield return new WaitForEndOfFrame();

        // Arrays refreshen (TurnManager etc.)
        ArrayRefresher.RefInstance?.RefreshFigureArrays();

        // Clean-up im Doppelkill-Schutz
        pendingKills.Remove(killed_figure);
    }

    // ==== Helper ====

    /// <summary>
    /// Identifiziert Basis-Objekte, die in der Regel nicht zerstört werden sollen.
    /// Passe hier deine Logik an, falls Basen killbar sein sollen.
    /// </summary>
    private bool IsBase(GameObject go)
    {
        if (go == null) return false;
        return go.name == "P1-Base" || go.name == "P2-Base";
    }

    /// <summary>
    /// Leitet aus der Verteidiger-Figur die zugehörige Player-Base ab.
    /// Erwartete Struktur:
    ///   - Figuren unter "P1_Figures" → Base "GUI-Canvas-P1/P1-Base"
    ///   - Figuren unter "P2_Figures" → Base "GUI-Canvas-P2/P2-Base"
    /// Falls deine Szene andere Pfade nutzt, bitte hier anpassen.
    /// </summary>
    private GameObject ResolvePlayerBaseFor(GameObject defender)
    {
        if (!defender) return null;

        var parent = defender.transform.parent;
        string pName = parent ? parent.name : string.Empty;

        if (pName == "P1_Figures")
        {
            if (!p1BaseCached)
                p1BaseCached = GameObject.Find("GUI-Canvas-P1/P1-Base");
            return p1BaseCached;
        }
        if (pName == "P2_Figures")
        {
            if (!p2BaseCached)
                p2BaseCached = GameObject.Find("GUI-Canvas-P2/P2-Base");
            return p2BaseCached;
        }

        return null;
    }
}
