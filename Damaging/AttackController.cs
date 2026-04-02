using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class AttackController : MonoBehaviour
{

    #region 1) Public Types
    /// <summary>Von welcher Seite greift die Figur an.</summary>
    public enum PlayerSide { P1, P2 }
    #endregion

    #region 2) Inspector-Felder & Caches
    [Header("Figure References (werden beim Start auch automatisch aus der Szene gelesen)")]
    public GameObject[] Figure_P1;
    public GameObject[] Figure_P2;

    [Header("Optionales Kamera-Shake-Feedback (SOFT)")]
    public CameraShake cameraShake; // SOFT – darf fehlen

    [Header("Target Offsets (Bases)")]
    [Tooltip("Offset (WORLD) der Trefferposition, wenn das Ziel P1-Base ist.")]
    public Vector3 p1BaseTargetOffset = Vector3.zero;

    [Tooltip("Offset (WORLD) der Trefferposition, wenn das Ziel P2-Base ist.")]
    public Vector3 p2BaseTargetOffset = Vector3.zero;

    [Header("Debugging")]
    [Tooltip("Wenn aktiv, loggt der Controller ausführliche Guard-Infos und Busy/Release-Events.")]
    public bool enableDebugLogs = false;

    [Tooltip("Maximale Zeit (Sekunden), die eine Figur als 'busy' markiert bleiben darf, bevor sie zwangsfreigegeben wird.")]
    public float maxBusyTimeSeconds = 3f;

    // *** Fallback-Defaults (werden pro Instanz gecached, falls wir nichts finden) ***
    private static readonly Vector3 DEFAULT_MODEL_LOCAL_POS = new Vector3(0.5f, 33.4f, -90.8f);
    private static readonly Vector3 DEFAULT_3DMODEL_LOCAL_POS = new Vector3(0f, -2.49f, 13.8f);
    private static readonly Quaternion DEFAULT_MODEL_LOCAL_ROT = Quaternion.identity;
    private static readonly Quaternion DEFAULT_3DMODEL_LOCAL_ROT = Quaternion.identity;

    // Pro-Objekt-Cache der Standard-Lage/Rotation von Model und 3D-Model
    private readonly Dictionary<int, Vector3> _defaultModelLocalPos = new();
    private readonly Dictionary<int, Vector3> _default3DModelLocalPos = new();
    private readonly Dictionary<int, Quaternion> _defaultModelLocalRot = new();
    private readonly Dictionary<int, Quaternion> _default3DModelLocalRot = new();

    #region Inspector: Attack Animation (clean)
    [Header("Attack Animation – Distances & Timings")]
    [Tooltip("Wie weit die Figur beim Ausholen nach hinten fährt (lokal, in Einheiten deiner Canvas/Scene).")]
    public float windupDistance_p = 150f;

    [Tooltip("Wie stark die Figur beim Ausholen angehoben/abgesenkt wird (lokal). Typisch Canvas: Z positiv Richtung Kamera.")]
    public float windupUpLift_p = -200f;

    [Tooltip("Dauer des Ausholens (Sekunden).")]
    [Min(0f)] public float windupTime_p = 0.40f;

    [Tooltip("Vorstoß-Strecke in Richtung Ziel (lokal).")]
    public float punchDistance_p = 320f;

    [Tooltip("Dauer des Vorstoßes / Schlages (Sekunden).")]
    [Min(0f)] public float punchTime_p = 0.06f;

    [Tooltip("Dauer des Rückwegs zur Ausgangsposition (Sekunden).")]
    [Min(0f)] public float returnTime_p = 0.10f;

    [Header("Attack Animation – Overshoot (Return)")]
    [Tooltip("Kleines Gegen-Kippen um Z (Grad) beim Rückweg. 0 = kein Overshoot.")]
    [Range(-30f, 30f)] public float overshootTiltZ_p = 0f;

    [Tooltip("Kleines Gegen-Kippen um Y (Grad) beim Rückweg. 0 = kein Overshoot.")]
    [Range(-30f, 30f)] public float overshootTiltY_p = 0f;

    [Tooltip("Kleines Gegen-Kippen um X (Grad) beim Rückweg. 0 = kein Overshoot.")]
    [Range(-30f, 30f)] public float overshootTiltX_p = 0f;

    [Header("Return Wobble – Settings")]
    [Tooltip("Gesamtdauer des Nachwippens (Sekunden), startet beim Impact und läuft während des Rückwegs.")]
    [Min(0f)] public float wobbleTotal_p = 0.35f;

    [Tooltip("Anzahl kompletter Hin-und-Her-Bewegungen während des Wobble-Effekts.")]
    [Range(1, 8)] public int wobbleCycles_p = 2;

    [Tooltip("Maximale Rotation in Grad um die X-Achse (leichtes Nicken). 0 = kein Nick-Wobble.")]
    [Range(0f, 15f)] public float wobbleAmpX_p = 0f;

    [Tooltip("Maximale Rotation in Grad um die Z-Achse (seitliches Kippen). 0 = kein Kipp-Wobble.")]
    [Range(0f, 20f)] public float wobbleAmpZ_p = 10.03f;

    [Tooltip("Abschwächungsfaktor pro Cycle (1 = keine Dämpfung, 0.5 = halbiert pro Cycle).")]
    [Range(0f, 1f)] public float wobbleDecay_p = 0.181f;

    [Header("Attack Animation – Tilt (Pitch/Yaw/Roll)")]
    [Tooltip("Neigung um Z (Roll) beim Ausholen (Grad).")]
    [Range(-90f, 90f)] public float tiltForwardZ_p = 0f;

    [Tooltip("Ziel-Neigung um Z (Roll) beim Anflug (Grad) – meist nahe 0 für 'fast gerade'.")]
    [Range(-90f, 90f)] public float tiltNearZeroZ_p = 0f;

    [Space(4)]
    [Tooltip("Neigung um Y (Yaw) beim Ausholen (Grad).")]
    [Range(-90f, 90f)] public float tiltForwardY_p = 0f;

    [Tooltip("Ziel-Neigung um Y (Yaw) beim Anflug (Grad).")]
    [Range(-90f, 90f)] public float tiltNearZeroY_p = 0f;

    [Space(4)]
    [Tooltip("Neigung um X (Pitch) beim Ausholen (Grad).")]
    [Range(-90f, 90f)] public float tiltForwardX_p = 60f;

    [Tooltip("Ziel-Neigung um X (Pitch) beim Anflug (Grad).")]
    [Range(-90f, 90f)] public float tiltNearZeroX_p = -2f;

    // Zusatz-Parameter
    public float faceZ_OffsetDeg = 0f; // 0° wenn die Figur lokal nach +X "guckt"; 90° wenn nach +Y
    #endregion

    public static AttackController current;

    #region 2.2) Animation Concurrency (per-Object)
    [Header("Animation Concurrency (per-Object)")]
    [Tooltip("Pro-Objekt-Serialisierung: dieselbe Figur wird gequeued; andere Figuren dürfen parallel animieren.")]
    public bool serializePerAttacker = true;

    // Laufende Animationen pro Angreifer-Objekt (Model-oder-Root Transform-GameObject-ID)
    private readonly HashSet<int> _animBusy = new();

    // Pending-Angriffe pro Angreifer-ID (last-wins)
    private struct PendingAttack { public GameObject attacker; public GameObject defender; public PlayerSide side; }
    private readonly Dictionary<int, PendingAttack> _pendingByAttacker = new();

    // Map Angreifer-ID -> GameObject (für Safety-Fuse)
    private readonly Dictionary<int, GameObject> _attackerById = new();

    // Map Angreifer-ID -> Zeitpunkt, seit dem er als busy markiert ist
    private readonly Dictionary<int, float> _animBusySince = new();
    #endregion
    #endregion

    #region 3) Unity-Lifecycle
    private void Awake()
    {
        current = this;
        RefreshFiguresFromScene_ATK();
    }

    private void OnDisable()
    {
        // Bases neu suchen bei Re-Enable
        _p1Base = _p2Base = null;
    }

    private void Update()
    {
        // Safety-Fuse: falls ein Angreifer zerstört/deaktiviert wurde oder zu lange busy bleibt,
        // räumen wir ihn hier auf. So kann keine Figur für immer "busy" hängen bleiben.
        if (!serializePerAttacker || _animBusy.Count == 0) return;

        List<int> toRelease = null;

        foreach (int id in _animBusy)
        {
            _attackerById.TryGetValue(id, out var go);
            _animBusySince.TryGetValue(id, out var since);

            bool mustRelease = false;

            if (go == null || !go.activeInHierarchy)
            {
                mustRelease = true;
                if (enableDebugLogs)
                    Debug.Log($"[AttackController] Safety-Release busy-ID {id} (GO null/inactive)");
            }
            else if (maxBusyTimeSeconds > 0f && Time.time - since > maxBusyTimeSeconds)
            {
                mustRelease = true;
                if (enableDebugLogs)
                    Debug.Log($"[AttackController] Safety-Release busy-ID {id} nach Timeout {maxBusyTimeSeconds}s (GO={go.name})");
            }

            if (mustRelease)
                (toRelease ??= new List<int>()).Add(id);
        }

        if (toRelease != null)
        {
            foreach (int id in toRelease)
            {
                _animBusy.Remove(id);
                _pendingByAttacker.Remove(id);
                _attackerById.Remove(id);
                _animBusySince.Remove(id);
            }
        }
    }
    #endregion

    #region 4) Öffentliche API (Buttons/Events)
    public void FigureAttackP1() => FigureAttack(PlayerSide.P1);
    public void FigureAttackP2() => FigureAttack(PlayerSide.P2);

    /// <summary>
    /// Öffentliche API: Lässt genau diese Figur angreifen (mit denselben HARD/SOFT Guards wie FigureAttack()).
    /// Defender wird wie gewohnt über FIGURE_TARGET / TurnManager / Base-Resolve ermittelt.
    /// </summary>
    public void AttackWithFigure(GameObject attacker, PlayerSide side)
    {
        RefreshFiguresFromScene_ATK();
        if (!TryGetTurnManager(out var tm)) { GuardLog($"[{side}] HARD-GUARD: Kein TurnManager."); return; }

        if (attacker == null) { GuardLog($"[{side}] HARD-GUARD: attacker ist null."); return; }

        var display = attacker.GetComponent<Display_Figure>();
        if (display == null) { GuardLog($"[{side}] HARD-GUARD: Display_Figure fehlt auf {attacker.name}."); return; }

        // (1) PRIO: Kosten prüfen
        if (display.FIGURE_LOAD < display.FIGURE_COST)
        {
            GuardLog($"[{side}] HARD-GUARD: Zu wenig LOAD ({display.FIGURE_LOAD}/{display.FIGURE_COST}) für {attacker.name}.");
            Messagebox.current?.ShowMessageBox("Not enough Mana for this");
            return;
        }

        // (2) PRIO: Target nur verlangen, wenn Gegner Figuren hat
        bool opponentHasFigures = OpponentHasAliveFigures(side);
        bool hasTarget =
            display.FIGURE_TARGET != null ||
            (side == PlayerSide.P1 && tm.current_figures_target_P1 != null);

        if (opponentHasFigures && !hasTarget)
        {
            GuardLog($"[{side}] HARD-GUARD: Kein Target gesetzt obwohl Gegner Figuren hat.");
            Messagebox.current?.ShowMessageBox("Target a Figure first");
            return;
        }

        // Ziel ermitteln (Base-Fallback passiert in ResolveDefender nur wenn Gegner KEINE Figuren hat)
        GameObject defender = ResolveDefender(side, display);
        if (defender == null)
        {
            GuardLog($"[{side}] HARD-GUARD: Kein gültiger Defender für {attacker.name} gefunden.");
            return;
        }

        // per-Angreifer-Serialisierung
        Transform modelTr = attacker.transform.Find("Model");
        Transform targetTr = modelTr ? modelTr : attacker.transform;
        int attackerId = targetTr.gameObject.GetInstanceID();

        if (serializePerAttacker && _animBusy.Contains(attackerId))
        {
            GuardLog($"[{side}] serializePerAttacker: Figur {attacker.name} (ID {attackerId}) ist busy – Attack wird gequeued/überschrieben.");
            _pendingByAttacker[attackerId] = new PendingAttack { attacker = attacker, defender = defender, side = side };
            _attackerById[attackerId] = targetTr.gameObject;
            _animBusySince[attackerId] = Time.time;
            return;
        }

        StartAttackNow(attacker, defender, side, attackerId);
    }


    /// <summary>
    /// Startet einen Angriff: HARD GUARDS für Spiellogik; SOFT GUARDS nur loggen.
    /// Serialisierung nur pro aktuellem Angreifer (nicht global).
    /// </summary>
    public void FigureAttack(PlayerSide side)
    {
        RefreshFiguresFromScene_ATK();
        if (!TryGetTurnManager(out var tm)) { GuardLog($"[{side}] HARD-GUARD: Kein TurnManager."); return; }

        GameObject currentFig = (side == PlayerSide.P1) ? tm.current_figure_P1 : tm.current_figure_P2;
        if (currentFig == null) { GuardLog($"[{side}] HARD-GUARD: current_figure ist null."); return; }

        var display = currentFig.GetComponent<Display_Figure>();
        if (display == null) { GuardLog($"[{side}] HARD-GUARD: Display_Figure fehlt auf {currentFig.name}."); return; }

        // (1) PRIO: Kosten prüfen
        if (display.FIGURE_LOAD < display.FIGURE_COST)
        {
            GuardLog($"[{side}] HARD-GUARD: Zu wenig LOAD ({display.FIGURE_LOAD}/{display.FIGURE_COST}) für {currentFig.name}.");
            Messagebox.current?.ShowMessageBox("Not enough Mana for this");
            return;
        }

        // (2) PRIO: Target nur verlangen, wenn Gegner Figuren hat
        bool opponentHasFigures = OpponentHasAliveFigures(side);
        bool hasTarget =
            display.FIGURE_TARGET != null ||
            (side == PlayerSide.P1 && tm.current_figures_target_P1 != null);

        if (opponentHasFigures && !hasTarget)
        {
            GuardLog($"[{side}] HARD-GUARD: Kein Target gesetzt obwohl Gegner Figuren hat.");
            Messagebox.current?.ShowMessageBox("Target a Figure first");
            return;
        }

        // Ziel ermitteln (Figur oder Base)
        GameObject defender = ResolveDefender(side, display);
        if (defender == null)
        {
            GuardLog($"[{side}] HARD-GUARD: Kein gültiger Defender für {currentFig.name} gefunden.");
            return;
        }

        // --- per-Angreifer-Serialisierung ---
        Transform modelTr = currentFig.transform.Find("Model");
        Transform targetTr = modelTr ? modelTr : currentFig.transform;
        int attackerId = targetTr.gameObject.GetInstanceID();

        if (serializePerAttacker && _animBusy.Contains(attackerId))
        {
            GuardLog($"[{side}] serializePerAttacker: Figur {currentFig.name} (ID {attackerId}) ist busy – Attack wird gequeued/überschrieben.");
            _pendingByAttacker[attackerId] = new PendingAttack { attacker = currentFig, defender = defender, side = side };
            _attackerById[attackerId] = targetTr.gameObject;
            _animBusySince[attackerId] = Time.time;
            return;
        }

        StartAttackNow(currentFig, defender, side, attackerId);
    }

    #endregion

    #region 5) Zielauflösung & Gegnerstatus
    private GameObject ResolveDefender(PlayerSide side, Display_Figure attackerDisplay)
    {
        if (!TryGetTurnManager(out var tm)) return null; // HARD

        // (1) Erst Wunschziel der Figur, dann TM-Fallback (nur bei P1 gepflegt)
        GameObject defender = attackerDisplay.FIGURE_TARGET;
        if (!defender && side == PlayerSide.P1 && tm.current_figures_target_P1 != null)
            defender = tm.current_figures_target_P1;

        // (2) Hat der Gegner keine lebenden Figuren, geht es auf die Base
        if (!OpponentHasAliveFigures(side))
            defender = GetOpponentBase(side);

        return defender;
    }
    #endregion

    #region 6) Figuren-Listen aus der Szene einlesen
    /// <summary>
    /// Liest die Kinder unter Game-Canvas/P1_Figures bzw. P2_Figures ein
    /// und cached für "Model"/"3D-Model" deren Start-Position + -Rotation.
    /// </summary>
    public void RefreshFiguresFromScene_ATK()
    {
        // --- P1 Figuren laden ---
        {
            GameObject p1FiguresParent = GameObject.Find("Game-Canvas/P1_Figures");
            if (p1FiguresParent != null)
            {
                int childCount = p1FiguresParent.transform.childCount;
                Figure_P1 = new GameObject[childCount];
                for (int i = 0; i < childCount; i++)
                    Figure_P1[i] = p1FiguresParent.transform.GetChild(i).gameObject;
            }
            else { Figure_P1 = Array.Empty<GameObject>(); }
        }

        // --- P2 Figuren laden ---
        {
            GameObject p2FiguresParent = GameObject.Find("Game-Canvas/P2_Figures");
            if (p2FiguresParent != null)
            {
                int childCount = p2FiguresParent.transform.childCount;
                Figure_P2 = new GameObject[childCount];
                for (int i = 0; i < childCount; i++)
                    Figure_P2[i] = p2FiguresParent.transform.GetChild(i).gameObject;
            }
            else { Figure_P2 = Array.Empty<GameObject>(); }
        }

        CacheDefaultsForArray(Figure_P1);
        CacheDefaultsForArray(Figure_P2);
    }
    #endregion

    #region 7) Angriff ausführen (Kosten, Schaden, Treffer-Callback)
    private void StartAttackNow(GameObject attacker, GameObject defender, PlayerSide side, int attackerId)
    {
        _animBusy.Add(attackerId);
        _attackerById[attackerId] = attacker;
        _animBusySince[attackerId] = Time.time;

        var df = attacker.GetComponent<Display_Figure>();
        if (df != null) df.IS_ATTACKING = true;

        if (enableDebugLogs)
            Debug.Log($"[AttackController] StartAttackNow: {attacker.name} -> {defender.name}, side={side}, id={attackerId}");

        PerformAttack(attacker, defender, side, attackerId);
    }

    public void PerformAttack(GameObject attacker, GameObject defender, PlayerSide side, int attackerId)
    {
        if (attacker == null || defender == null)
        {
            GuardLog($"[{side}] PerformAttack: attacker/defender ist null (attacker={attacker}, defender={defender}).");
            EndAttackAndMaybeRunPending(attackerId);
            return;
        }

        var (damage, cost) = GetAttackParameters(attacker);
        if (damage <= 0)
        {
            GuardLog($"[{side}] PerformAttack: damage <= 0 für {attacker.name}.");
            EndAttackAndMaybeRunPending(attackerId);
            return;
        }

        // Laufende Defend-Effekte der angreifenden Seite deaktivieren (damit kein Dauer-Schild stehen bleibt)
        Deactivate_Defense(attacker);

        // Events an FieldCardController – SOFT
        FieldCardController.instance?.Figure_HasAttacked(attacker, true);
        FieldCardController_P2.instance?.Figure_HasAttacked(attacker, true);

        // Wir animieren möglichst das Kind "Model" (falls vorhanden), sonst das Root-Objekt
        Transform modelTr = attacker.transform.Find("Model");
        GameObject attackerModel = modelTr ? modelTr.gameObject : attacker;

        // ** Animation inkl. Neigung/Wippen **
        AttackAnimation_WithTilt(attackerModel, defender, side, () =>
        {
            try
            {
                // BASE-Treffer über PlayerBaseController – SOFT Fallback auf Figure-Damage
                if (IsPlayerBase(defender))
                {
                    if (PlayerBaseController.current != null)
                    {
                        if (defender.name == "P1-Base") PlayerBaseController.current.Damaging_P1(damage);
                        else PlayerBaseController.current.Damaging_P2(damage);
                    }
                    else { DamageToFigure.current?.ApplyDamage(defender, damage); }
                }
                else
                {
                    // Normale Figur
                    DamageToFigure.current?.ApplyDamage(defender, damage); // SOFT
                }

                // FIX: Attack bezahlt GENAU EINMAL – mit cost aus GetAttackParameters()
                LoadPay(attacker, cost);

                cameraShake?.TriggerShake(); // SOFT
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AttackController] Exception in onHit: {ex}");
            }
        }, attackerId);
    }

    private void EndAttackAndMaybeRunPending(int attackerId)
    {
        _animBusy.Remove(attackerId);
        _attackerById.Remove(attackerId);
        _animBusySince.Remove(attackerId);

        if (serializePerAttacker && _pendingByAttacker.TryGetValue(attackerId, out var p))
        {
            if (enableDebugLogs)
                Debug.Log($"[AttackController] EndAttack: pending Attack gefunden für ID {attackerId} – wird jetzt ausgeführt.");

            _pendingByAttacker.Remove(attackerId);
            if (p.attacker != null && p.defender != null)
                StartAttackNow(p.attacker, p.defender, p.side, attackerId);
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"[AttackController] EndAttack: keine pending Attack für ID {attackerId}.");
        }
    }

    /// <summary>
    /// Liest ATK und Kosten aus der Figur (READ-ONLY!).
    /// FIX: Zieht KEINE Kosten mehr ab (das macht ausschließlich LoadPay()).
    /// </summary>
    public (int damage, int cost) GetAttackParameters(GameObject attacker)
    {
        if (attacker == null) return (0, 0); // HARD
        var display = attacker.GetComponent<Display_Figure>();
        if (display == null) return (0, 0); // HARD

        // --- Damage robust via Reflection (unterstützt verschiedene Feldnamen) ---
        int damage = 0;
        var t = display.GetType();
        FieldInfo atkField = t.GetField("FIGURE_ATTACK")
                            ?? t.GetField("FIGURE_ATK")
                            ?? t.GetField("ATK")
                            ?? t.GetField("FIGURE_DMG");
        if (atkField != null)
        {
            object v = atkField.GetValue(display);
            if (v != null)
            {
                try { damage = Mathf.Max(0, Convert.ToInt32(v)); } catch { damage = 0; }
            }
        }

        int cost = Mathf.Max(0, display.FIGURE_COST);

        if (enableDebugLogs)
            Debug.Log($"[AttackController] GetAttackParameters: {attacker.name} dmg={damage}, cost={cost}, curLOAD={display.FIGURE_LOAD}");

        return (damage, cost);
    }
    #endregion

    #region 8) Angriff-Animation: Wippen/Neigung wie im Screenshot
    public void AttackAnimation_WithTilt(GameObject attacker, GameObject defender, PlayerSide side, System.Action onHit = null, int? attackerIdOpt = null)
    {
        if (attacker == null || defender == null)
        {
            GuardLog($"[{side}] AttackAnimation_WithTilt: attacker/defender null (attacker={attacker}, defender={defender}).");
            if (attackerIdOpt.HasValue) EndAttackAndMaybeRunPending(attackerIdOpt.Value);
            return; // HARD + Abschluss
        }
        if (!attacker.activeInHierarchy)
        {
            GuardLog($"[{side}] AttackAnimation_WithTilt: attacker {attacker.name} ist nicht activeInHierarchy.");
            if (attackerIdOpt.HasValue) EndAttackAndMaybeRunPending(attackerIdOpt.Value);
            return; // HARD + Abschluss
        }

        // 1) Ziel-Transform (Move & Rotate müssen auf DEMSELBEN Transform laufen)
        var modelTr = attacker.transform.Find("Model");
        var targetTr = modelTr ? modelTr : attacker.transform;
        if (targetTr == null)
        {
            GuardLog($"[{side}] AttackAnimation_WithTilt: targetTr null.");
            if (attackerIdOpt.HasValue) EndAttackAndMaybeRunPending(attackerIdOpt.Value);
            return;
        }
        var target = targetTr.gameObject;

        // 2) Lokale Startwerte cachen – wir merken uns den Zustand BEIM Start und setzen am Ende genau dorthin zurück
        Vector3 startLocalPos = targetTr.localPosition;
        Quaternion startLocalRot = targetTr.localRotation;
        Vector3 eLocalBase = startLocalRot.eulerAngles;

        // 3) Richtungs-/Winkelermittlung im Parent-Lokalsystem
        Transform parent = targetTr.parent;

        Vector3 selfLocalPos = parent ? parent.InverseTransformPoint(targetTr.position) : targetTr.position;

        // --- NEW: defender World Pos mit Base-Offsets ---
        Vector3 defenderWorldPos = defender.transform.position;

        if (IsPlayerBase(defender))
        {
            if (defender.name == "P1-Base") defenderWorldPos += p1BaseTargetOffset;
            else if (defender.name == "P2-Base") defenderWorldPos += p2BaseTargetOffset;
        }

        Vector3 goalLocalPos = parent ? parent.InverseTransformPoint(defenderWorldPos) : defenderWorldPos;

        Vector3 deltaLocal = goalLocalPos - selfLocalPos;
        deltaLocal.z = 0f;

        float faceZ = Mathf.Atan2(deltaLocal.y, deltaLocal.x) * Mathf.Rad2Deg + faceZ_OffsetDeg;

        // 4) Zeiten/Wege (HARD Defaults zur Stabilität)
        float windupDistance = Mathf.Max(0.01f, windupDistance_p);
        float windupUpLift = windupUpLift_p;
        float windupTime = Mathf.Max(0.01f, windupTime_p);
        float punchDistance = punchDistance_p;
        float punchTime = Mathf.Max(0.01f, punchTime_p);
        float returnTime = Mathf.Max(0.01f, returnTime_p);

        // 5) Return-Wobble-Parameter
        int wobbleCycles = Mathf.Max(1, wobbleCycles_p);
        float wobbleTotal = Mathf.Max(0.01f, wobbleTotal_p);
        float wobAmpX = wobbleAmpX_p;
        float wobAmpZ = wobbleAmpZ_p;
        float wobDecay = Mathf.Clamp01(wobbleDecay_p);
        float settleTime = 0.12f;

        // 6) Ziel-LOCAL-Rotationen
        Quaternion qForwardTilt = startLocalRot * Quaternion.Euler(tiltForwardX_p, tiltForwardY_p, 0f);
        Vector3 eLocalForward = qForwardTilt.eulerAngles; eLocalForward.z = faceZ;

        Quaternion qNearTilt = startLocalRot * Quaternion.Euler(tiltNearZeroX_p, tiltNearZeroY_p, 0f);
        Vector3 eLocalNear = qNearTilt.eulerAngles; eLocalNear.z = faceZ;

        // 7) Ziel-LOCAL-Positionen
        Vector3 dirLocal = deltaLocal.sqrMagnitude > 0.000001f ? deltaLocal.normalized : Vector3.right;
        Vector3 upLocal = parent ? parent.InverseTransformDirection(Vector3.forward) : Vector3.forward;

        Vector3 windupLocalPos = startLocalPos - dirLocal * windupDistance + upLocal * windupUpLift;
        Vector3 attackLocalPos = startLocalPos + dirLocal * punchDistance;

        if (enableDebugLogs)
            Debug.Log($"[AttackController] AttackAnimation start: {attacker.name} -> {defender.name}, dir={dirLocal}, faceZ={faceZ:0.0}");

        var seq = LeanTween.sequence();

        AudioManager.Instance?.PlaySFX2D("Attack_Windup");

        // 8) WINDUP – MoveLocal + RotateLocal parallel
        seq.append(() =>
        {
            if (!target) return;
            LeanTween.moveLocal(target, windupLocalPos, windupTime).setEase(LeanTweenType.easeOutQuad)
                .setIgnoreTimeScale(true);
            LeanTween.rotateLocal(target, eLocalForward, windupTime).setEase(LeanTweenType.easeOutQuad)
                .setIgnoreTimeScale(true);
        });
        seq.append(LeanTween
            .delayedCall(windupTime, () => { })
            .setIgnoreTimeScale(true));

        // 9) PUNCH – Vorstoß + onHit
        seq.append(() =>
        {
            if (!target) return;
            LeanTween.moveLocal(target, attackLocalPos, punchTime).setEase(LeanTweenType.easeOutQuad)
                .setIgnoreTimeScale(true)
                .setOnComplete(() =>
                {
                    try { onHit?.Invoke(); }
                    catch (Exception ex) { Debug.LogError(ex); }
                });
            LeanTween.rotateLocal(target, eLocalNear, punchTime).setEase(LeanTweenType.easeOutQuad)
                .setIgnoreTimeScale(true);
        });
        seq.append(LeanTween
            .delayedCall(punchTime, () => { })
            .setIgnoreTimeScale(true));

        // 10) RETURN + WOBBLE
        seq.append(() =>
        {
            if (!target) return;
            LeanTween.moveLocal(target, startLocalPos, returnTime).setEase(LeanTweenType.easeOutQuad)
                .setIgnoreTimeScale(true);

            StartReturnWobbleLocal_DriftingCenter(
                targetTr,
                baseStartLocal: Quaternion.Euler(eLocalNear),
                baseEndLocal: startLocalRot,
                totalTime: Mathf.Max(wobbleTotal, returnTime),
                cycles: wobbleCycles,
                ampXDeg: wobAmpX,
                ampZDeg: wobAmpZ,
                decay: wobDecay,
                onDone: null
            );
        });

        // 11) Abschluss
        float waitTotal = Mathf.Max(returnTime, wobbleTotal);
        seq.append(
            LeanTween.delayedCall(waitTotal, () =>
            {
                if (!targetTr || !target)
                {
                    if (attackerIdOpt.HasValue) EndAttackAndMaybeRunPending(attackerIdOpt.Value);
                    return;
                }

                LeanTween.rotateLocal(target, eLocalBase, settleTime).setEase(LeanTweenType.easeOutSine)
                    .setIgnoreTimeScale(true)
                    .setOnComplete(() =>
                    {
                        if (!targetTr)
                        {
                            if (attackerIdOpt.HasValue) EndAttackAndMaybeRunPending(attackerIdOpt.Value);
                            return;
                        }

                        targetTr.localPosition = startLocalPos;
                        targetTr.localRotation = startLocalRot;
                        SetFigureBack_ForModel(targetTr);

                        var df = attacker.GetComponentInParent<Display_Figure>();
                        if (df != null) df.IS_ATTACKING = false;

                        if (attackerIdOpt.HasValue) EndAttackAndMaybeRunPending(attackerIdOpt.Value);
                    });
            }).setIgnoreTimeScale(true)
        );
    }

    // Return-Wobble mit DRIFT des Center-Rotationspunktes
    private void StartReturnWobbleLocal_DriftingCenter(
        Transform tr,
        Quaternion baseStartLocal,
        Quaternion baseEndLocal,
        float totalTime,
        int cycles,
        float ampXDeg,
        float ampZDeg,
        float decay,
        System.Action onDone)
    {
        if (tr == null) { onDone?.Invoke(); return; } // HARD

        totalTime = Mathf.Max(0.01f, totalTime);
        cycles = Mathf.Max(1, cycles);
        decay = Mathf.Clamp01(decay);

        var wobSeq = LeanTween.sequence();

        int halfs = cycles * 2;
        float segT = Mathf.Max(0.01f, totalTime / halfs);
        float ax = ampXDeg;
        float az = ampZDeg;

        for (int i = 0; i < halfs; i++)
        {
            float tCenter = (i + 1) / (float)halfs;
            Quaternion center = Quaternion.Slerp(baseStartLocal, baseEndLocal, tCenter);

            float sign = (i % 2 == 0) ? 1f : -1f;
            Quaternion targetRot = center * Quaternion.Euler(sign * ax, 0f, sign * az);

            wobSeq.append(
                LeanTween.rotateLocal(tr.gameObject, targetRot.eulerAngles, segT)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setIgnoreTimeScale(true)
            );

            if (i % 2 == 1) { ax *= decay; az *= decay; }
        }

        wobSeq.append(
            LeanTween.rotateLocal(tr.gameObject, baseEndLocal.eulerAngles, segT * 0.8f)
                .setEase(LeanTweenType.easeOutQuad)
                .setIgnoreTimeScale(true)
                .setOnComplete(() => onDone?.Invoke())
        );
    }
    #endregion

    #region 9) Defense-API (Aktivieren/Deaktivieren)
    public void Activate_Defense_Current_Figure_P1() => Activate_Defense_Current_Figure(PlayerSide.P1);
    public void Deactivate_Defense_Current_Figure_P1() => Deactivate_Defense_Current_Figure(PlayerSide.P1);
    public void Activate_Defense_Current_Figure_P2() => Activate_Defense_Current_Figure(PlayerSide.P2);
    public void Deactivate_Defense_Current_Figure_P2() => Deactivate_Defense_Current_Figure(PlayerSide.P2);

    public void Activate_Defense_Current_Figure(PlayerSide side)
    {
        if (!TryGetTurnManager(out var tm)) return; // HARD
        GameObject figure = (side == PlayerSide.P1) ? tm.current_figure_P1 : tm.current_figure_P2;
        if (figure == null) return; // HARD

        var display = figure.GetComponent<Display_Figure>();
        if (display == null) return; // HARD

        if (display.FIGURE_LOAD < display.FIGURE_COST) { TryShowMessageBox(); return; } // HARD

        LoadPay(figure); // HARD

        display.IS_DEFENDING = true;
        Activate_Defense(figure); // SOFT-intern
    }

    public void Deactivate_Defense_Current_Figure(PlayerSide side)
    {
        if (!TryGetTurnManager(out var tm)) return; // HARD
        GameObject figure = (side == PlayerSide.P1) ? tm.current_figure_P1 : tm.current_figure_P2;
        if (figure == null) return; // HARD
        Deactivate_Defense(figure);
    }

    public void Activate_Defense(GameObject figure)
    {
        FieldCardController.instance?.Figure_HasDefended(figure, true);
        FieldCardController_P2.instance?.Figure_HasDefended(figure, true);

        if (figure == null) return; // HARD
        var display = figure.GetComponent<Display_Figure>();
        if (display == null) return; // HARD

        display.IS_DEFENDING = true;
        Animate_Defense(figure); // SOFT-intern
    }

    public void Animate_Defense(GameObject defending_figure)
    {
        if (defending_figure == null) return;

        Transform effectPos = defending_figure.transform.Find("EffectPosition");
        if (effectPos == null) { Debug.LogWarning("[Defense] Effekt-Anchor 'EffectPosition' fehlt – Partikel wird übersprungen."); return; }
        if (ParticleController.Instance == null) { Debug.LogWarning("[Defense] ParticleController.Instance fehlt – Partikel wird übersprungen."); return; }

        GameObject shield = defending_figure.transform.Find("defender_shield")?.gameObject;
        if (shield != null) shield.SetActive(true);

        AudioManager.Instance?.PlaySFX2D("DefendShield");

        if (CameraShake.current != null) CameraShake.current.Shake(0.3f, 2f);

        ParticleController.Instance.PlayParticleEffect(
            effectPos.position + new Vector3(0f, 50f, -100f),
            6,
            new Vector3(30f, 30f, 30f),
            Quaternion.Euler(86f, 0f, 0f),
            effectPos
        );
    }

    public void Deactivate_Defense(GameObject figure)
    {
        if (figure == null) return; // HARD
        GameObject shield = figure.transform.Find("defender_shield")?.gameObject;
        if (shield != null) shield.SetActive(false);

        var display = figure.GetComponent<Display_Figure>();
        if (display != null)
        {
            display.IS_DEFENDING = false;
        }
        else
        {
            var defSign = figure.transform.Find("Def_Sign");
            if (defSign != null) defSign.gameObject.SetActive(false);

            Transform effectPos = figure.transform.Find("EffectPosition");
            if (effectPos != null)
            {
                Transform defendClone = effectPos.Find("Defending(Clone)");
                if (defendClone != null) defendClone.gameObject.SetActive(false);
            }
        }
    }
    #endregion

    #region 10) Reset aller Figuren-Modelle (Position + Rotation)
    private void SetFigureBack_ForModel(Transform modelOrRoot)
    {
        if (modelOrRoot == null) return;

        var modelT = modelOrRoot.name == "Model" ? modelOrRoot : modelOrRoot.Find("Model");
        if (modelT == null) modelT = modelOrRoot;

        int modelId = modelT.GetInstanceID();

        if (_defaultModelLocalPos.TryGetValue(modelId, out var modelPos))
            modelT.localPosition = modelPos;
        else
            modelT.localPosition = DEFAULT_MODEL_LOCAL_POS;

        if (_defaultModelLocalRot.TryGetValue(modelId, out var modelRot))
            modelT.localRotation = modelRot;
        else
            modelT.localRotation = DEFAULT_MODEL_LOCAL_ROT;

        var model3DT = modelT.Find("3D-Model");
        if (model3DT != null)
        {
            int m3dId = model3DT.GetInstanceID();
            if (_default3DModelLocalPos.TryGetValue(m3dId, out var m3dPos))
                model3DT.localPosition = m3dPos;
            else
                model3DT.localPosition = DEFAULT_3DMODEL_LOCAL_POS;

            if (_default3DModelLocalRot.TryGetValue(m3dId, out var m3dRot))
                model3DT.localRotation = m3dRot;
            else
                model3DT.localRotation = DEFAULT_3DMODEL_LOCAL_ROT;
        }
    }

    public void SetFiguresBack_ForSide(PlayerSide side)
    {
        var list = side == PlayerSide.P1
            ? ((Figure_P1 != null && Figure_P1.Length > 0) ? Figure_P1 : (TurnManager.current ? TurnManager.current.Figures_P1 : null))
            : ((Figure_P2 != null && Figure_P2.Length > 0) ? Figure_P2 : (TurnManager.current ? TurnManager.current.Figures_P2 : null));

        if (list == null || list.Length == 0) return;

        foreach (var figure in list)
        {
            if (!figure) continue;

            var modelT = figure.transform.Find("Model");
            if (modelT != null)
            {
                if (_defaultModelLocalPos.TryGetValue(modelT.GetInstanceID(), out var modelPos))
                    modelT.localPosition = modelPos;
                else
                    modelT.localPosition = DEFAULT_MODEL_LOCAL_POS;

                if (_defaultModelLocalRot.TryGetValue(modelT.GetInstanceID(), out var modelRot))
                    modelT.localRotation = modelRot;
                else
                    modelT.localRotation = DEFAULT_MODEL_LOCAL_ROT;

                var model3DT = modelT.Find("3D-Model");
                if (model3DT != null)
                {
                    if (_default3DModelLocalPos.TryGetValue(model3DT.GetInstanceID(), out var m3dPos))
                        model3DT.localPosition = m3dPos;
                    else
                        model3DT.localPosition = DEFAULT_3DMODEL_LOCAL_POS;

                    if (_default3DModelLocalRot.TryGetValue(model3DT.GetInstanceID(), out var m3dRot))
                        model3DT.localRotation = m3dRot;
                    else
                        model3DT.localRotation = DEFAULT_3DMODEL_LOCAL_ROT;
                }
            }
        }
    }
    #endregion

    #region 11) Hilfsfunktionen (Caching, TurnManager, UI, Guards)
    private void EnsureDefaultLocalCached(Transform tr, Dictionary<int, Vector3> cache)
    {
        if (tr == null) return;
        int id = tr.GetInstanceID();
        if (!cache.ContainsKey(id)) cache[id] = tr.localPosition;
    }

    private void EnsureDefaultLocalRotCached(Transform tr, Dictionary<int, Quaternion> cache)
    {
        if (tr == null) return;
        int id = tr.GetInstanceID();
        if (!cache.ContainsKey(id)) cache[id] = tr.localRotation;
    }

    private void CacheDefaultsForArray(GameObject[] figures)
    {
        if (figures == null) return;

        foreach (var fig in figures)
        {
            if (!fig) continue;

            var modelT = fig.transform.Find("Model");
            if (modelT != null)
            {
                EnsureDefaultLocalCached(modelT, _defaultModelLocalPos);
                EnsureDefaultLocalRotCached(modelT, _defaultModelLocalRot);

                var model3DT = modelT.Find("3D-Model");
                if (model3DT != null)
                {
                    EnsureDefaultLocalCached(model3DT, _default3DModelLocalPos);
                    EnsureDefaultLocalRotCached(model3DT, _default3DModelLocalRot);
                }
            }
        }
    }

    private bool TryGetTurnManager(out TurnManager tm)
    {
        tm = TurnManager.current;
        if (tm != null) return true;

        var go = GameObject.Find("TurnManager");
        if (!go)
        {
            GuardLog("[AttackController] TurnManager nicht gefunden.");
            tm = null; return false;
        }

        tm = go.GetComponent<TurnManager>();
        if (tm == null)
        {
            GuardLog("[AttackController] Kein TurnManager-Component auf 'TurnManager'.");
            return false;
        }

        TurnManager.current = tm;
        return true;
    }

    private void TryShowMessageBox()
        => Messagebox.current?.ShowMessageBox();

    private void LoadPay(GameObject figure, int cost)
    {
        if (figure == null) return;
        var display = figure.GetComponent<Display_Figure>();
        if (display == null) return;

        cost = Mathf.Max(0, cost);

        int before = display.FIGURE_LOAD;
        display.FIGURE_LOAD = Mathf.Max(0, display.FIGURE_LOAD - cost);

        Image loadBar = display.LoadBar;
        if (LoadBarController.current != null && loadBar != null)
            LoadBarController.current.PayLoad(cost, loadBar);

        if (enableDebugLogs)
            Debug.Log($"[AttackController] LoadPay: {figure.name} cost={cost}, LOAD {before} -> {display.FIGURE_LOAD}");
    }

    private void LoadPay(GameObject figure)
    {
        if (figure == null) return;
        var display = figure.GetComponent<Display_Figure>();
        if (display == null) return;
        LoadPay(figure, display.FIGURE_COST);
    }

    private void GuardLog(string msg)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[AttackController][GUARD] " + msg);
    }
    #endregion

    #region 12) Gegner-Check & Base-Resolver
    private GameObject _p1Base, _p2Base;

    private static bool IsPlayerBase(GameObject go)
        => go != null && (go.name == "P1-Base" || go.name == "P2-Base");

    private GameObject GetPlayerBase(PlayerSide side)
    {
        if (side == PlayerSide.P1)
        {
            if (_p1Base == null) _p1Base = GameObject.Find("P1-Base");
            return _p1Base;
        }
        else
        {
            if (_p2Base == null) _p2Base = GameObject.Find("P2-Base");
            return _p2Base;
        }
    }

    private GameObject GetOpponentBase(PlayerSide attackerSide)
        => (attackerSide == PlayerSide.P1) ? GetPlayerBase(PlayerSide.P2) : GetPlayerBase(PlayerSide.P1);

    private static bool IsAliveFigure(GameObject go)
    {
        if (go == null) return false;
        var df = go.GetComponent<Display_Figure>();
        return df != null && df.FIGURE_HEALTH > 0 && go.activeInHierarchy;
    }

    private int CountAliveFigures(GameObject[] arr)
    {
        if (arr == null) return 0;
        int c = 0;
        for (int i = 0; i < arr.Length; i++)
            if (IsAliveFigure(arr[i])) c++;
        return c;
    }

    private bool OpponentHasAliveFigures(PlayerSide attackerSide)
    {
        return attackerSide == PlayerSide.P1
            ? CountAliveFigures(Figure_P2) > 0
            : CountAliveFigures(Figure_P1) > 0;
    }
    #endregion
}
