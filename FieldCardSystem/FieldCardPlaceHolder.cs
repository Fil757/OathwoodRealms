using System.Collections;
using UnityEngine;
using CustomInspector;
using TMPro;

public class FieldCardPlaceHolder : MonoBehaviour
{
    // =========================================================
    // FieldCardPlaceHolder
    //  - Verwaltet LifePoint-Icons + optional TextMeshPro Anzeige
    //  - Robust: Erzwingt Entfernen sobald LifePoints <= 0
    //  - Fail-Safe: Wenn Coroutine abbricht (Disable/Deactivate), räumt Update trotzdem auf
    //  - Side-Detection: entscheidet P1/P2 über Parent-Name "FieldEffects_P1" / "FieldEffects_P2"
    //  - WICHTIG: Bei P2 nutzt er FieldCaster_P2.instance (identischer Caster, aber P2-Sicht)
    //  - NEW: lifepointframe kann je nach Seite (P1/P2) eine andere anchoredPosition + localZ bekommen
    // =========================================================

    [Header("LifePoint Icons")]
    public GameObject LifeTimePointField;

    [Header("LifePoint Text (optional)")]
    public TMP_Text LifePointText;

    [Header("LifePointFrame (optional)")]
    [Tooltip("UI-Frame für die Lifepoints (RectTransform). Kann je nach Side anders positioniert werden.")]
    public RectTransform lifepointframe;

    [Header("LifePointFrame Transform – Player 1")]
    public Vector2 lifepointframeAnchoredPos_P1 = Vector2.zero;
    public float lifepointframeLocalZ_P1 = 0f;

    [Header("LifePointFrame Transform – Player 2")]
    public Vector2 lifepointframeAnchoredPos_P2 = Vector2.zero;
    public float lifepointframeLocalZ_P2 = 0f;

    [Tooltip("Wenn true: Frame-Position/Z wird im Update erzwungen (robust, minimal mehr Arbeit).")]
    public bool EnforceFrameTransformInUpdate = true;

    [Header("Debug / Safety")]
    [Tooltip("Wenn true, versucht Update() bei LifePoints<=0 die Destroy-Logik robust nachzuziehen.")]
    public bool UseUpdateFailsafe = true;

    [Tooltip("Wenn true, schreibt Debug Logs, wenn Destroy angefordert/ausgeführt wird.")]
    public bool DebugLogs = false;

    public int current_lifePoints;
    public int my_true_order_index;

    private const int MAX_LIFEPOINTS = 5;

    // Merker: Destroy wurde angefordert
    private bool _destroyRequested;

    // Merker: Destroy wurde erfolgreich versucht (Call an FieldCaster abgesetzt)
    private bool _destroyCallSent;

    // Merker: Coroutine läuft (optional)
    private Coroutine _destroyRoutine;

    // Side-Cache (verhindert unnötiges Setzen, wenn Update-Transform aktiv ist)
    private enum Side { Unknown, P1, P2 }
    private Side _cachedSide = Side.Unknown;

    void OnEnable()
    {
        _destroyRequested = false;
        _destroyCallSent = false;

        Reset_LifePoints(MAX_LIFEPOINTS);
        UpdateLifePointText();

        // NEW: Frame-Transform sofort setzen
        ApplyLifePointFrameTransform(force: true);
    }

    void Update()
    {
        // Text immer aktuell halten (kostet fast nichts)
        UpdateLifePointText();

        // NEW: Frame-Transform robust halten
        if (EnforceFrameTransformInUpdate)
            ApplyLifePointFrameTransform(force: false);

        // Robust-Failsafe: wenn auf 0 und noch nicht sauber entfernt -> versuche es hier
        if (!UseUpdateFailsafe) return;

        if (current_lifePoints <= 0)
        {
            if (!_destroyRequested)
            {
                _destroyRequested = true;
                if (DebugLogs)
                    Debug.Log($"[FieldCardPlaceHolder] Update() detected LifePoints<=0 => request destroy. idx={my_true_order_index}", this);
            }

            if (!_destroyCallSent)
            {
                TryDestroyNow_Failsafe();
            }
        }
    }

    // =========================================================
    // Public API
    // =========================================================

    public void Create_LifePoints(int amount)
    {
        for (int i = 0; i < amount; i++)
            Create_SingleLifePoint();
    }

    public void Create_SingleLifePoint()
    {
        if (LifeTimePointField == null) return;

        // Prefab weiterhin aus P1-FieldCaster ziehen (ist nur ein Prefab-Holder)
        GameObject prefab = FieldCaster.instance != null ? FieldCaster.instance.FieldCardLifePoint_obj : null;

        // Falls du das Prefab stattdessen in FieldCaster_P2 hältst, nimm diese Zeile:
        // GameObject prefab = FieldCaster_P2.instance != null ? FieldCaster_P2.instance.FieldCardLifePoint_obj : null;

        if (prefab == null) return;

        Instantiate(prefab, LifeTimePointField.transform);
        current_lifePoints++;

        if (current_lifePoints > 0)
        {
            _destroyRequested = false;
            _destroyCallSent = false;

            if (_destroyRoutine != null)
            {
                StopCoroutine(_destroyRoutine);
                _destroyRoutine = null;
            }
        }
    }

    public void Destroy_LifePoints(int destroyed_lifepoints)
    {
        TakeDamage(destroyed_lifepoints);
    }

    public void Destroy_SingleLifePoint()
    {
        TakeDamage(1);
    }

    // =========================================================
    // Core Logic
    // =========================================================

    private void Reset_LifePoints(int amount)
    {
        ClearAllIcons();
        current_lifePoints = 0;
        Create_LifePoints(amount);
    }

    private void TakeDamage(int damage)
    {
        if (damage <= 0) return;
        if (current_lifePoints <= 0) return;

        if (damage > current_lifePoints)
            damage = current_lifePoints;

        current_lifePoints -= damage;
        if (current_lifePoints < 0)
            current_lifePoints = 0;

        if (LifeTimePointField != null)
        {
            for (int k = 0; k < damage; k++)
            {
                int lastIndex = LifeTimePointField.transform.childCount - 1;
                if (lastIndex < 0) break;

                Transform lp = LifeTimePointField.transform.GetChild(lastIndex);
                lp.SetParent(null);
                Destroy(lp.gameObject);
            }
        }

        UpdateLifePointText();

        if (current_lifePoints <= 0 && !_destroyRequested)
        {
            _destroyRequested = true;
            if (DebugLogs)
                Debug.Log($"[FieldCardPlaceHolder] LifePoints reached 0 => request destroy. idx={my_true_order_index}", this);

            TryDestroyNow_Failsafe();
            StartDestroyNextFrameBackup();
        }
        else if (current_lifePoints <= 0 && _destroyRequested)
        {
            if (!_destroyCallSent)
                TryDestroyNow_Failsafe();
        }
    }

    // =========================================================
    // NEW: LifePointFrame Transform (P1/P2 via Parent "FieldEffects_P1/_P2")
    // =========================================================

    private void ApplyLifePointFrameTransform(bool force)
    {
        if (lifepointframe == null) return;

        Side side = ResolveSideFromHierarchy();
        if (!force && side == _cachedSide) return;

        _cachedSide = side;

        if (side == Side.P1)
        {
            ApplyFrameTransform(lifepointframeAnchoredPos_P1, lifepointframeLocalZ_P1);
            return;
        }

        if (side == Side.P2)
        {
            ApplyFrameTransform(lifepointframeAnchoredPos_P2, lifepointframeLocalZ_P2);
            return;
        }
    }

    private void ApplyFrameTransform(Vector2 anchoredPos, float localZ)
    {
        lifepointframe.anchoredPosition = anchoredPos;

        Vector3 lp = lifepointframe.localPosition;
        lp.z = localZ;
        lifepointframe.localPosition = lp;
    }

    private Side ResolveSideFromHierarchy()
    {
        // Side-Detection explizit über FieldEffects_P1 / FieldEffects_P2
        Transform t = transform;
        while (t != null)
        {
            if (t.name == "FieldEffects_P1")
                return Side.P1;

            if (t.name == "FieldEffects_P2")
                return Side.P2;

            t = t.parent;
        }

        return Side.Unknown;
    }

    // =========================================================
    // Destroy Handling (robust)
    // =========================================================

    private void TryDestroyNow_Failsafe()
    {
        if (_destroyCallSent) return;

        // Side Detection über Parent
        Transform t = transform;
        bool isP1 = false;
        bool isP2 = false;

        while (t != null)
        {
            if (t.name == "FieldEffects_P1")
            {
                isP1 = true;
                break;
            }

            if (t.name == "FieldEffects_P2")
            {
                isP2 = true;
                break;
            }

            t = t.parent;
        }

        // P1
        if (isP1)
        {
            if (FieldCaster.instance == null)
            {
                if (DebugLogs)
                    Debug.LogWarning($"[FieldCardPlaceHolder] FieldCaster.instance is NULL. Cannot destroy P1 idx={my_true_order_index} obj={name}", this);
                return;
            }

            _destroyCallSent = true;

            if (DebugLogs)
                Debug.Log($"[FieldCardPlaceHolder] Destroy => P1 idx={my_true_order_index} obj={name}", this);

            FieldCaster.instance.DestroyP1_FieldCard_AbsoluteIndex(my_true_order_index);
            return;
        }

        // P2 (über FieldCaster_P2)
        if (isP2)
        {
            if (FieldCaster_P2.instance == null)
            {
                if (DebugLogs)
                    Debug.LogWarning($"[FieldCardPlaceHolder] FieldCaster_P2.instance is NULL. Cannot destroy P2 idx={my_true_order_index} obj={name}", this);
                return;
            }

            _destroyCallSent = true;

            if (DebugLogs)
                Debug.Log($"[FieldCardPlaceHolder] Destroy => P2 (FieldCaster_P2) idx={my_true_order_index} obj={name}", this);

            FieldCaster_P2.instance.DestroyP2_FieldCard_AbsoluteIndex(my_true_order_index);
            return;
        }

        Debug.LogError(
            $"[FieldCardPlaceHolder] Could not determine side (no parent named FieldEffects_P1 / FieldEffects_P2 found). idx={my_true_order_index} obj={name}",
            this
        );
    }

    private void StartDestroyNextFrameBackup()
    {
        if (_destroyRoutine != null) return;
        _destroyRoutine = StartCoroutine(DestroyAfterFrameBackup());
    }

    private IEnumerator DestroyAfterFrameBackup()
    {
        yield return null;

        if (current_lifePoints > 0)
        {
            if (DebugLogs)
                Debug.Log($"[FieldCardPlaceHolder] DestroyAfterFrameBackup aborted (LifePoints > 0). idx={my_true_order_index} obj={name}", this);

            _destroyRoutine = null;
            yield break;
        }

        if (!_destroyCallSent)
        {
            if (DebugLogs)
                Debug.Log($"[FieldCardPlaceHolder] DestroyAfterFrameBackup sending destroy call. idx={my_true_order_index} obj={name}", this);

            TryDestroyNow_Failsafe();
        }

        _destroyRoutine = null;
    }

    // =========================================================
    // Helpers
    // =========================================================

    private void ClearAllIcons()
    {
        if (LifeTimePointField == null) return;

        for (int i = LifeTimePointField.transform.childCount - 1; i >= 0; i--)
        {
            Transform c = LifeTimePointField.transform.GetChild(i);
            c.SetParent(null);
            Destroy(c.gameObject);
        }
    }

    private void UpdateLifePointText()
    {
        if (LifePointText == null) return;
        LifePointText.text = current_lifePoints.ToString();
    }
}
