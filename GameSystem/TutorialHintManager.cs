using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialHintManager : MonoBehaviour
{
    public static TutorialHintManager current;

    [Header("Hint Roots (6 Boxen in fester Reihenfolge)")]
    [Tooltip("Reihenfolge z.B.: 0 RightClick, 1 HandFull, 2 EachFigureButton, 3 DestroyArtifactX, 4 NoEnemyFiguresAttackDirect, 5 TargetingFailsSign")]
    public GameObject[] hintRoots = new GameObject[6];

    [Header("Fade Settings")]
    [Tooltip("Dauer des Fade-In in Sekunden (unscaled time).")]
    public float fadeInDuration = 0.35f;

    [Tooltip("Wenn true: beim Show werden erst alle Alphas auf 0 gesetzt (sauberer Start).")]
    public bool forceStartAlphaZero = true;

    [Tooltip("Wenn true: blendet mit Time.unscaledDeltaTime (unabhängig von Time.timeScale).")]
    public bool useUnscaledTime = true;

    [Header("Behavior")]
    [Tooltip("Wenn true: versteckt alle anderen Hints automatisch, bevor einer gezeigt wird.")]
    public bool hideOthersOnShow = true;

    [Tooltip("Globale Option: wenn false, werden Hints niemals angezeigt (API-Aufrufe werden ignoriert).")]
    public bool showTutorialHints = true;

    private bool checkNoEnemyFigures = false;

    private class HintCache
    {
        public GameObject root;
        public Graphic[] graphics;
    }

    public void HideHints()
    {
        showTutorialHints = false;
    }

    private readonly List<HintCache> _caches = new();
    private readonly Dictionary<int, Coroutine> _running = new();

    // NUR Session-Memory (Reset bei Neustart/Reload)
    private bool[] _shownThisSession;

    void Update()
    {
        if (!checkNoEnemyFigures)
            return;

        if (TurnManager.current.Figures_P2.Length == 0)
        {
            Show_NoEnemyFiguresAttackDirect(1f);
            checkNoEnemyFigures = false; // optional: nur einmal
        }
    }

    private void Awake()
    {
        current = this;

        BuildCaches();
        _shownThisSession = new bool[_caches.Count];
    }

    private void BuildCaches()
    {
        _caches.Clear();

        for (int i = 0; i < hintRoots.Length; i++)
        {
            var go = hintRoots[i];
            var cache = new HintCache { root = go, graphics = new Graphic[0] };

            if (go != null)
                cache.graphics = go.GetComponentsInChildren<Graphic>(true); // inkl. inactive children

            _caches.Add(cache);
        }
    }

    // ------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------

    /// <summary>
    /// Zeigt einen Hint (fadet Parent+alle Child-Graphics ein),
    /// ABER nur wenn showTutorialHints==true und dieser Hint in dieser Session noch nicht gezeigt wurde.
    /// </summary>
    public void ShowHint(int index, float delaySeconds = 0f)
    {
        if (!showTutorialHints) return;

        if (index < 0 || index >= _caches.Count)
        {
            Debug.LogWarning($"[TutorialHintManager] ShowHint: index out of range: {index}");
            return;
        }

        if (_caches[index].root == null)
        {
            Debug.LogWarning($"[TutorialHintManager] ShowHint: hintRoots[{index}] is null.");
            return;
        }

        // Session-Only "Show once"
        if (_shownThisSession != null && _shownThisSession[index])
            return;

        // Sofort markieren, damit doppelte Trigger im gleichen Frame nichts doppelt starten
        _shownThisSession[index] = true;

        if (hideOthersOnShow)
            HideAllImmediate();

        StartFadeIn(index, delaySeconds);

        checkNoEnemyFigures = true;
    }

    /// <summary>
    /// Global Toggle zur Laufzeit setzen (z.B. Optionsmenü).
    /// Wenn auf false gesetzt wird, werden aktive Hints sofort ausgeblendet.
    /// </summary>
    public void SetShowTutorialHints(bool enabled, bool hideActiveHints = true)
    {
        showTutorialHints = enabled;

        if (!enabled && hideActiveHints)
            HideAllImmediate();
    }

    /// <summary>
    /// Blendet sofort alles aus und deaktiviert die Roots.
    /// (Berührt NICHT den "shown" Status.)
    /// </summary>
    public void HideAllImmediate()
    {
        for (int i = 0; i < _caches.Count; i++)
        {
            StopFadeRoutine(i);

            var root = _caches[i].root;
            if (root == null) continue;

            root.SetActive(false);
            SetAlpha(_caches[i], 0f);
        }
    }

    /// <summary>
    /// Optional: Setzt den Session-Show-Once Status zurück (z.B. Debug / Testen).
    /// </summary>
    public void ResetShownState_SessionOnly(bool alsoHideAll = true)
    {
        if (_shownThisSession != null)
        {
            for (int i = 0; i < _shownThisSession.Length; i++)
                _shownThisSession[i] = false;
        }

        if (alsoHideAll)
            HideAllImmediate();
    }

    // ------------------------------------------------------------
    // Convenience: 6 einzelne Funktionen (eine pro Block)
    // ------------------------------------------------------------

    public void Show_RightClick(float delaySeconds = 0f) => ShowHint(0, delaySeconds);
    public void Show_HandFull(float delaySeconds = 0f) => ShowHint(1, delaySeconds);
    public void Show_EachFigureButton(float delaySeconds = 0f) => ShowHint(2, delaySeconds);
    public void Show_DestroyArtifactX(float delaySeconds = 0f) => ShowHint(3, delaySeconds);
    public void Show_NoEnemyFiguresAttackDirect(float delaySeconds = 0f) => ShowHint(4, delaySeconds);
    public void Show_TargetingFailsSign(float delaySeconds = 0f) => ShowHint(5, delaySeconds);

    // ------------------------------------------------------------
    // Fade Internals
    // ------------------------------------------------------------

    private void StartFadeIn(int index, float delaySeconds)
    {
        StopFadeRoutine(index);

        var co = StartCoroutine(CoFadeIn(index, delaySeconds));
        _running[index] = co;
    }

    private void StopFadeRoutine(int index)
    {
        if (_running.TryGetValue(index, out var co) && co != null)
            StopCoroutine(co);

        _running.Remove(index);
    }

    private IEnumerator CoFadeIn(int index, float delaySeconds)
    {
        var cache = _caches[index];

        // Aktivieren, bevor wir alpha setzen (Layout/Anker etc.)
        cache.root.SetActive(true);

        if (forceStartAlphaZero)
            SetAlpha(cache, 0f);

        if (delaySeconds > 0f)
            yield return Wait(delaySeconds);

        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeInDuration);

        while (t < dur)
        {
            t += Delta();
            float a = Mathf.Clamp01(t / dur);
            SetAlpha(cache, a);
            yield return null;
        }

        SetAlpha(cache, 1f);
        _running.Remove(index);
    }

    private void SetAlpha(HintCache cache, float alpha)
    {
        if (cache.graphics == null) return;

        for (int i = 0; i < cache.graphics.Length; i++)
        {
            var g = cache.graphics[i];
            if (g == null) continue;

            var c = g.color;
            c.a = alpha;
            g.color = c;
        }
    }

    private float Delta() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // WaitForSecondsRealtime ist KEIN YieldInstruction -> IEnumerator ist der gemeinsame Nenner.
    private IEnumerator Wait(float seconds)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(seconds);
        else
            yield return new WaitForSeconds(seconds);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            BuildCaches();
    }
#endif
}
