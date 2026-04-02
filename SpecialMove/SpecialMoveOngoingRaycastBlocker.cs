using UnityEngine;

/// <summary>
/// SpecialMoveOngoingRaycastBlocker
/// - Blockt Raycasts sofort, sobald ein "Figuren-SpecialMove" (Name beginnt mit Prefix) läuft
/// - Gibt Raycasts erst X Sekunden NACH Ende aller passenden SpecialMoves wieder frei
/// - Reagiert sauber auf neue passende SpecialMoves während der Delay-Zeit
///
/// Erwartet in der Szene ein GameObject namens "OngoingSpecialMoves".
/// </summary>
[DisallowMultipleComponent]
public class SpecialMoveOngoingRaycastBlocker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject blockerObject;
    [SerializeField] private CanvasGroup blockerCanvasGroup;

    [Header("SpecialMove Root")]
    [SerializeField] private string ongoingRootName = "OngoingSpecialMoves";

    [Header("Filter")]
    [Tooltip("Es wird nur geblockt, wenn mindestens ein Child unter OngoingSpecialMoves mit diesem Prefix beginnt.")]
    [SerializeField] private string requiredNamePrefix = "SpecialMove_P1_SM";

    [Header("Timing")]
    [Tooltip("Sekunden, die nach Ende aller passenden SpecialMoves gewartet wird, bevor freigegeben wird.")]
    [SerializeField] private float releaseDelaySeconds = 1f;

    [Tooltip("Prüffrequenz. 0 = jeden Frame.")]
    [Range(0f, 60f)]
    [SerializeField] private float checkRateHz = 20f;

    private Transform _ongoingRoot;
    private bool _isBlocking;
    private float _releaseAtTime = -1f;
    private float _nextCheckTime;

    private void Reset()
    {
        blockerObject = gameObject;
        blockerCanvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        CacheRoot();

        if (blockerCanvasGroup != null)
        {
            blockerCanvasGroup.blocksRaycasts = true;
            blockerCanvasGroup.interactable = false;
        }

        ApplyBlockingState(IsMatchingSpecialMoveOngoing(), force: true);
    }

    private void Update()
    {
        if (checkRateHz > 0f)
        {
            if (Time.unscaledTime < _nextCheckTime) return;
            _nextCheckTime = Time.unscaledTime + (1f / Mathf.Max(0.0001f, checkRateHz));
        }

        bool ongoing = IsMatchingSpecialMoveOngoing();

        // 1) Passender SpecialMove läuft → sofort blocken & Release abbrechen
        if (ongoing)
        {
            _releaseAtTime = -1f;

            if (!_isBlocking)
                ApplyBlockingState(true);

            return;
        }

        // 2) Kein passender SpecialMove mehr → Release-Timer starten (falls nicht schon aktiv)
        if (_isBlocking)
        {
            if (_releaseAtTime < 0f)
            {
                _releaseAtTime = Time.unscaledTime + Mathf.Max(0f, releaseDelaySeconds);
            }
            else if (Time.unscaledTime >= _releaseAtTime)
            {
                ApplyBlockingState(false);
                _releaseAtTime = -1f;
            }
        }
    }

    private void CacheRoot()
    {
        if (_ongoingRoot != null) return;

        GameObject rootGO = GameObject.Find(ongoingRootName);
        _ongoingRoot = rootGO != null ? rootGO.transform : null;
    }

    private bool IsMatchingSpecialMoveOngoing()
    {
        CacheRoot();
        if (_ongoingRoot == null) return false;

        // Wenn Prefix leer ist, wäre das "alles matcht" -> das wollen wir nicht versehentlich.
        if (string.IsNullOrEmpty(requiredNamePrefix)) return false;

        int c = _ongoingRoot.childCount;
        for (int i = 0; i < c; i++)
        {
            Transform t = _ongoingRoot.GetChild(i);
            if (t == null) continue;

            // Beispielname: "SpecialMove_P1_SM0005 - 416254"
            // StartsWith reicht, weil der Präfix vorne steht
            if (t.name.StartsWith(requiredNamePrefix))
                return true;
        }

        return false;
    }

    private void ApplyBlockingState(bool shouldBlock, bool force = false)
    {
        if (!force && _isBlocking == shouldBlock) return;

        _isBlocking = shouldBlock;

        if (blockerObject != null)
            blockerObject.SetActive(shouldBlock);

        if (blockerCanvasGroup != null)
        {
            blockerCanvasGroup.alpha = shouldBlock ? 1f : 0f;
            blockerCanvasGroup.blocksRaycasts = shouldBlock;
            blockerCanvasGroup.interactable = false;
        }
    }
}
