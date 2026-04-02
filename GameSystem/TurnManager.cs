using System;
using System.Collections;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager current;

    [Header("Figures")]
    public GameObject[] Figures_P1;
    public GameObject[] Figures_P2;

    [Header("Active Player")]
    public string activePlayer = "P1";

    [Header("TurnSigns")]
    public GameObject YourTurnText;
    public GameObject P1_TurnSmaragd;
    public GameObject P2_TurnSmaragd;

    [Header("Decks")]
    public GameObject P1_Deck;
    public GameObject P2_Deck;

    [Header("Traps")]
    public GameObject P1_Traps;
    public GameObject P2_Traps;

    [Header("Current Selection")]
    public GameObject current_figure_P1;
    public GameObject current_figure_P2;

    // Aktuelles Target der Figur
    public GameObject current_figures_target_P1;
    public GameObject current_figures_target_P2;

    // Index-Tracker (nur 0..2 relevant)
    private int currentIndexP1 = -1;
    private int currentIndexP2 = -1;

    [Tooltip("Wenn true, wird beim Aktivieren einer Figur diese automatisch selektiert (P1 & P2).")]
    public bool autoSelectOnActivate = true;

    // Aktives Point Light von P1/P2 und deren Coroutines
    private Light activePointLightP1;
    private Coroutine pulseCoroutineP1;
    private Light activePointLightP2;
    private Coroutine pulseCoroutineP2;

    // Puls-Parameter
    private readonly float baseIntensity = 0.75f;
    private readonly float pulseAmplitude = 0.2f;
    private readonly float pulseSpeed = 5f;

    // Merker: Zuletzt aktivierter Target-Frame
    private GameObject lastTargetP1;
    private GameObject lastTargetP2;

    private bool _refreshScheduled;

    public GameObject fullRayCastBlocker;

    // --- ADD: Action Lock ---
    [Header("Action Lock")]
    [Tooltip("Wenn true, werden Eingaben blockiert (Buttons + Keyboard).")]
    [SerializeField] private bool actionLocked;
    
    private int _actionLockCounter;
    private Coroutine _unlockAfterCoroutine;

    #region Unity Lifecycle
    private void Awake()
    {
        current = this;
        StartCoroutine(DelayedArrayRefresh());
    }

    private IEnumerator DelayedArrayRefresh()
    {
        // Warten bis Ende dieses Frames → alle Objekte im Scene-Hierarchy sind garantiert initialisiert
        yield return new WaitForEndOfFrame();

        if (ArrayRefresher.RefInstance != null)
        {
            ArrayRefresher.RefInstance.RefreshFigureArrays();
            Debug.Log("[Init] Figure Arrays wurden nach Scene-Setup aktualisiert.");
        }
        else
        {
            Debug.LogWarning("[Init] Kein ArrayRefresher gefunden!");
        }
    }

    private void OnDisable()
    {
        // Sauber aufräumen, falls Objekt deaktiviert wird
        ClearActivePointLightP1();
        ClearActivePointLightP2();
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
        ClearActivePointLightP1();
        ClearActivePointLightP2();
    }

    private void Start()
    {
        // 1) Arrays einmal initial aus der Szene laden
        RefreshFiguresFromScene();

        // 2) Initiale Auswahl/Frames/Licht setzen, wenn möglich
        InitializeSelectionP1();
        InitializeSelectionP2();

        // 3) Verzögertes Validieren (falls Spawn/Setup Zeit braucht)
        StartCoroutine(DelayedEnsureSelection(0.25f));
    }
    #endregion

    #region Init & Refresh
    /// <summary>
    /// Lädt Figures_P1 / Figures_P2 aus der Szene-Hierarchie:
    /// "Game-Canvas/P1_Figures" und "Game-Canvas/P2_Figures".
    /// </summary>
    public void RefreshFiguresFromScene()
    {
        Figures_P1 = GetChildrenAtPath("Game-Canvas/P1_Figures");
        Figures_P2 = GetChildrenAtPath("Game-Canvas/P2_Figures");

        // Nach einem Refresh sicherstellen, dass die aktuelle Auswahl gültig ist
        EnsureValidCurrentSelectionP1();
        EnsureValidCurrentSelectionP2();
    }

    private void InitializeSelectionP1()
    {
        if (Figures_P1 != null && Figures_P1.Length > 0)
        {
            currentIndexP1 = FirstActiveIndexP1();
            if (currentIndexP1 < 0) currentIndexP1 = 0;

            current_figure_P1 = SafeIndex(Figures_P1, currentIndexP1);
            if (current_figure_P1 != null && current_figure_P1.activeInHierarchy)
            {
                UpdatePointLightP1(current_figure_P1);
                ActivateCurrentFrame(current_figure_P1);
            }
        }

        EnsureValidCurrentSelectionP1();
        RefreshTargetFrameP1();
    }

    private void InitializeSelectionP2()
    {
        if (Figures_P2 != null && Figures_P2.Length > 0)
        {
            currentIndexP2 = FirstActiveIndexP2();
            if (currentIndexP2 < 0) currentIndexP2 = 0;

            current_figure_P2 = SafeIndex(Figures_P2, currentIndexP2);
            if (current_figure_P2 != null && current_figure_P2.activeInHierarchy)
            {
                UpdatePointLightP2(current_figure_P2);
                ActivateCurrentFrame(current_figure_P2);
            }
        }

        EnsureValidCurrentSelectionP2();
        RefreshTargetFrameP2();
    }

    private GameObject[] GetChildrenAtPath(string path)
    {
        var parent = GameObject.Find(path);
        if (parent == null)
        {
            Debug.LogWarning($"[TurnManager] Pfad nicht gefunden: '{path}'. Array wird leer gesetzt.");
            return Array.Empty<GameObject>();
        }

        int n = parent.transform.childCount;
        var arr = new GameObject[n];
        for (int i = 0; i < n; i++)
            arr[i] = parent.transform.GetChild(i)?.gameObject;

        return arr;
    }

    private static GameObject SafeIndex(GameObject[] arr, int index)
    {
        if (arr == null || index < 0 || index >= arr.Length) return null;
        return arr[index];
    }
    #endregion

    #region Selection Helpers (P1)
    private bool IsSelectableP1(GameObject go) => go != null && go.activeInHierarchy;

    private int FirstActiveIndexP1()
    {
        if (Figures_P1 == null) return -1;
        int count = Mathf.Min(3, Figures_P1.Length);
        for (int i = 0; i < count; i++)
            if (IsSelectableP1(Figures_P1[i])) return i;
        return -1;
    }

    /// <summary>
    /// Sucht vom Startindex (exklusiv) vor/zurück den nächsten aktiven Index in 0..(count-1) mit Wrap-Around.
    /// direction: +1 (vorwärts) oder -1 (rückwärts)
    /// </summary>
    private int NextActiveIndexP1(int fromExclusive, int direction)
    {
        if (Figures_P1 == null) return -1;

        int count = Mathf.Min(3, Figures_P1.Length);
        if (count == 0) return -1;

        int start = Mathf.Clamp(fromExclusive, 0, count - 1);

        for (int step = 1; step <= count; step++)
        {
            int idx = (start + direction * step) % count;
            if (idx < 0) idx += count;

            if (IsSelectableP1(Figures_P1[idx])) return idx;
        }

        return -1;
    }

    private void EnsureValidCurrentSelectionP1()
    {
        if (!IsSelectableP1(current_figure_P1))
        {
            int idx = FirstActiveIndexP1();
            if (idx >= 0)
            {
                SetCurrentFigureP1(idx);
            }
            else
            {
                // Keine aktive Figur vorhanden → UI zurücksetzen
                ClearActivePointLightP1();

                if (current_figure_P1 != null)
                {
                    var oldFrame = current_figure_P1.transform.Find("BaseFrameCurrentFigure");
                    if (oldFrame) oldFrame.gameObject.SetActive(false);
                }

                current_figure_P1 = null;
                currentIndexP1 = -1;

                // Target-Frame ebenfalls bereinigen
                SetTargetFrameActive(lastTargetP1, false);
                lastTargetP1 = null;
                current_figures_target_P1 = null;
            }
        }
    }
    #endregion

    #region Selection Helpers (P2) – symmetrisch zu P1
    private bool IsSelectableP2(GameObject go) => go != null && go.activeInHierarchy;

    private int FirstActiveIndexP2()
    {
        if (Figures_P2 == null) return -1;
        int count = Mathf.Min(3, Figures_P2.Length);
        for (int i = 0; i < count; i++)
            if (IsSelectableP2(Figures_P2[i])) return i;
        return -1;
    }

    private int NextActiveIndexP2(int fromExclusive, int direction)
    {
        if (Figures_P2 == null) return -1;

        int count = Mathf.Min(3, Figures_P2.Length);
        if (count == 0) return -1;

        int start = Mathf.Clamp(fromExclusive, 0, count - 1);

        for (int step = 1; step <= count; step++)
        {
            int idx = (start + direction * step) % count;
            if (idx < 0) idx += count;

            if (IsSelectableP2(Figures_P2[idx])) return idx;
        }

        return -1;
    }

    private void EnsureValidCurrentSelectionP2()
    {
        if (!IsSelectableP2(current_figure_P2))
        {
            int idx = FirstActiveIndexP2();
            if (idx >= 0)
            {
                SetCurrentFigureP2(idx);
            }
            else
            {
                ClearActivePointLightP2();

                if (current_figure_P2 != null)
                {
                    var oldFrame = current_figure_P2.transform.Find("BaseFrameCurrentFigure");
                    if (oldFrame) oldFrame.gameObject.SetActive(false);
                }

                current_figure_P2 = null;
                currentIndexP2 = -1;

                SetTargetFrameActive(lastTargetP2, false);
                lastTargetP2 = null;
                current_figures_target_P2 = null;
            }
        }
    }
    #endregion

    #region Public API (P1 Navigation)
    public void NextFigureP1()
    {
        RefreshFiguresFromScene();
        EnsureValidCurrentSelectionP1();
        if (currentIndexP1 < 0) return;

        int idx = NextActiveIndexP1(currentIndexP1, +1);
        if (idx >= 0) SetCurrentFigureP1(idx);
    }

    public void PreviousFigureP1()
    {
        RefreshFiguresFromScene();
        EnsureValidCurrentSelectionP1();
        if (currentIndexP1 < 0) return;

        int idx = NextActiveIndexP1(currentIndexP1, -1);
        if (idx >= 0) SetCurrentFigureP1(idx);
    }

    /// <summary>
    /// Setzt die aktuelle P1-Figur (nur Index 0..2 gültig).
    /// Schaltet alten Current-Frame aus, neuen an; aktualisiert PointLight und Target-Frame.
    /// </summary>
    public void SetCurrentFigureP1(int newIndex)
    {
        if (Figures_P1 == null || Figures_P1.Length == 0)
        {
            RefreshFiguresFromScene();
            if (Figures_P1 == null || Figures_P1.Length == 0) return;
        }

        if (newIndex < 0 || newIndex >= Figures_P1.Length) return;
        if (newIndex > 2) return; // nur 0..2 erlaubt

        var target = Figures_P1[newIndex];

        if (!IsSelectableP1(target))
        {
            int first = FirstActiveIndexP1();
            if (first < 0)
            {
                EnsureValidCurrentSelectionP1();
                return;
            }
            newIndex = first;
            target = Figures_P1[newIndex];
        }

        if (current_figure_P1 != null)
        {
            var oldFrame = current_figure_P1.transform.Find("BaseFrameCurrentFigure");
            if (oldFrame != null) oldFrame.gameObject.SetActive(false);
        }

        currentIndexP1 = newIndex;
        current_figure_P1 = target;

        UpdatePointLightP1(current_figure_P1);
        ActivateCurrentFrame(current_figure_P1);

        // Target nachziehen
        var df = current_figure_P1.GetComponent<Display_Figure>();
        if (df != null && df.FIGURE_TARGET != null)
            SetCurrentTargetForP1(df.FIGURE_TARGET);
        else
            RefreshTargetFrameP1();
    }

    public void OnP1FigureActiveStateChanged()
    {
        RefreshFiguresFromScene();
        EnsureValidCurrentSelectionP1();
    }
    #endregion

    #region Public API (P2 Navigation) – symmetrisch zu P1
    public void NextFigureP2()
    {
        RefreshFiguresFromScene();
        EnsureValidCurrentSelectionP2();
        if (currentIndexP2 < 0) return;

        int idx = NextActiveIndexP2(currentIndexP2, +1);
        if (idx >= 0) SetCurrentFigureP2(idx);
    }

    public void PreviousFigureP2()
    {
        RefreshFiguresFromScene();
        EnsureValidCurrentSelectionP2();
        if (currentIndexP2 < 0) return;

        int idx = NextActiveIndexP2(currentIndexP2, -1);
        if (idx >= 0) SetCurrentFigureP2(idx);
    }

    /// <summary>
    /// Setzt die aktuelle P2-Figur (nur Index 0..2 gültig).
    /// Schaltet alten Current-Frame aus, neuen an; aktualisiert PointLight und Target-Frame.
    /// </summary>
    public void SetCurrentFigureP2(int newIndex)
    {
        if (Figures_P2 == null || Figures_P2.Length == 0)
        {
            RefreshFiguresFromScene();
            if (Figures_P2 == null || Figures_P2.Length == 0) return;
        }

        if (newIndex < 0 || newIndex >= Figures_P2.Length) return;
        if (newIndex > 2) return; // nur 0..2 erlaubt

        var target = Figures_P2[newIndex];

        if (!IsSelectableP2(target))
        {
            int first = FirstActiveIndexP2();
            if (first < 0)
            {
                EnsureValidCurrentSelectionP2();
                return;
            }
            newIndex = first;
            target = Figures_P2[newIndex];
        }

        if (current_figure_P2 != null)
        {
            var oldFrame = current_figure_P2.transform.Find("BaseFrameCurrentFigure");
            if (oldFrame != null) oldFrame.gameObject.SetActive(false);
        }

        currentIndexP2 = newIndex;
        current_figure_P2 = target;

        UpdatePointLightP2(current_figure_P2);
        ActivateCurrentFrame(current_figure_P2);

        // Target nachziehen
        var df = current_figure_P2.GetComponent<Display_Figure>();
        if (df != null && df.FIGURE_TARGET != null)
            SetCurrentTargetForP2(df.FIGURE_TARGET);
        else
            RefreshTargetFrameP2();
    }

    public void OnP2FigureActiveStateChanged()
    {
        RefreshFiguresFromScene();
        EnsureValidCurrentSelectionP2();
    }
    #endregion

    #region Target-Handling (P1)
    public void SetCurrentTargetForP1(GameObject newTarget)
    {
        if (lastTargetP1 != null && lastTargetP1 != newTarget)
            SetTargetFrameActive(lastTargetP1, false);

        current_figures_target_P1 = newTarget;

        if (current_figures_target_P1 != null && current_figures_target_P1.activeInHierarchy)
        {
            SetTargetFrameActive(current_figures_target_P1, true);
            lastTargetP1 = current_figures_target_P1;
        }
        else
        {
            lastTargetP1 = null;
        }

        if (current_figure_P1 != null)
        {
            var df = current_figure_P1.GetComponent<Display_Figure>();
            if (df != null) df.FIGURE_TARGET = current_figures_target_P1;
        }
    }

    public void RefreshTargetFrameP1()
    {
        GameObject desired = null;

        if (current_figure_P1 != null)
        {
            var df = current_figure_P1.GetComponent<Display_Figure>();
            if (df != null) desired = df.FIGURE_TARGET;
        }

        if (desired == lastTargetP1)
        {
            if (desired != null)
                SetTargetFrameActive(desired, true);
            return;
        }

        SetCurrentTargetForP1(desired);
    }
    #endregion

    #region Target-Handling (P2) – symmetrisch
    public void SetCurrentTargetForP2(GameObject newTarget)
    {
        if (lastTargetP2 != null && lastTargetP2 != newTarget)
            SetTargetFrameActive(lastTargetP2, false);

        current_figures_target_P2 = newTarget;

        if (current_figures_target_P2 != null && current_figures_target_P2.activeInHierarchy)
        {
            SetTargetFrameActive(current_figures_target_P2, true);
            lastTargetP2 = current_figures_target_P2;
        }
        else
        {
            lastTargetP2 = null;
        }

        if (current_figure_P2 != null)
        {
            var df = current_figure_P2.GetComponent<Display_Figure>();
            if (df != null) df.FIGURE_TARGET = current_figures_target_P2;
        }
    }

    public void RefreshTargetFrameP2()
    {
        GameObject desired = null;

        if (current_figure_P2 != null)
        {
            var df = current_figure_P2.GetComponent<Display_Figure>();
            if (df != null) desired = df.FIGURE_TARGET;
        }

        if (desired == lastTargetP2)
        {
            if (desired != null)
                SetTargetFrameActive(desired, true);
            return;
        }

        SetCurrentTargetForP2(desired);
    }
    #endregion

    #region Delayed selection validation
    private IEnumerator DelayedEnsureSelection(float delay)
    {
        yield return new WaitForSeconds(delay);

        RefreshFiguresFromScene();

        EnsureValidCurrentSelectionP1();
        RefreshTargetFrameP1();

        EnsureValidCurrentSelectionP2();
        RefreshTargetFrameP2();
    }
    #endregion

    #region Activation Hooks (P1/P2)
    public void OnP1FigureActivated(GameObject go)
    {
        if (!autoSelectOnActivate || go == null) return;

        RefreshFiguresFromScene();

        int idx = Array.IndexOf(Figures_P1, go);
        if (idx >= 0 && idx <= 2 && go.activeInHierarchy)
        {
            SetCurrentFigureP1(idx);
        }
        else
        {
            EnsureValidCurrentSelectionP1();
        }
    }

    public void OnP1FigureDeactivated(GameObject go)
    {
        RefreshFiguresFromScene();

        if (go != null && go == current_figure_P1)
            EnsureValidCurrentSelectionP1();

        if (go != null && go == current_figures_target_P1)
            SetCurrentTargetForP1(null);
    }

    public void OnP2FigureActivated(GameObject go)
    {
        if (!autoSelectOnActivate || go == null) return;

        RefreshFiguresFromScene();

        int idx = Array.IndexOf(Figures_P2, go);
        if (idx >= 0 && idx <= 2 && go.activeInHierarchy)
        {
            SetCurrentFigureP2(idx);
        }
        else
        {
            EnsureValidCurrentSelectionP2();
        }
    }

    public void OnP2FigureDeactivated(GameObject go)
    {
        RefreshFiguresFromScene();

        if (go != null && go == current_figure_P2)
            EnsureValidCurrentSelectionP2();

        if (go != null && go == current_figures_target_P2)
            SetCurrentTargetForP2(null);
    }
    #endregion

    #region Visuals (Frame + Light)
    private void ActivateCurrentFrame(GameObject figure)
    {
        if (figure == null || !figure.activeInHierarchy) return;
        var tr = figure.transform.Find("BaseFrameCurrentFigure");
        if (tr != null) tr.gameObject.SetActive(true);
    }

    private void UpdatePointLightP1(GameObject figure)
    {
        ClearActivePointLightP1();

        if (figure == null || !figure.activeInHierarchy) return;

        Transform lightTransform = figure.transform.Find("Point Light");
        if (lightTransform != null)
        {
            Light pointLight = lightTransform.GetComponent<Light>();
            if (pointLight != null)
            {
                pointLight.color = Color.white;
                pointLight.intensity = baseIntensity;
                pointLight.enabled = true;

                activePointLightP1 = pointLight;
                pulseCoroutineP1 = StartCoroutine(PulseLight(pointLight));
            }
        }
    }

    private void UpdatePointLightP2(GameObject figure)
    {
        ClearActivePointLightP2();

        if (figure == null || !figure.activeInHierarchy) return;

        Transform lightTransform = figure.transform.Find("Point Light");
        if (lightTransform != null)
        {
            Light pointLight = lightTransform.GetComponent<Light>();
            if (pointLight != null)
            {
                pointLight.color = Color.white;
                pointLight.intensity = baseIntensity;
                pointLight.enabled = true;

                activePointLightP2 = pointLight;
                pulseCoroutineP2 = StartCoroutine(PulseLight(pointLight));
            }
        }
    }

    private IEnumerator PulseLight(Light light)
    {
        if (light == null) yield break;

        float t = 0f;
        while (true)
        {
            if (light == null) yield break;
            t += Time.deltaTime;
            float fluctuation = Mathf.Sin(t * pulseSpeed) * pulseAmplitude;
            light.intensity = Mathf.Max(0f, baseIntensity + fluctuation);
            yield return null;
        }
    }

    private void ClearActivePointLightP1()
    {
        if (pulseCoroutineP1 != null)
        {
            StopCoroutine(pulseCoroutineP1);
            pulseCoroutineP1 = null;
        }

        if (activePointLightP1 != null)
        {
            activePointLightP1.color = Color.white;
            activePointLightP1.intensity = 0.35f;
            activePointLightP1 = null;
        }
    }

    private void ClearActivePointLightP2()
    {
        if (pulseCoroutineP2 != null)
        {
            StopCoroutine(pulseCoroutineP2);
            pulseCoroutineP2 = null;
        }

        if (activePointLightP2 != null)
        {
            activePointLightP2.color = Color.white;
            activePointLightP2.intensity = 0.35f;
            activePointLightP2 = null;
        }
    }
    #endregion

    public void SetTargetFrameActive(GameObject target, bool active)
    {
        if (target == null) return;
        var tr = target.transform.Find("BaseFrameTargetFigure");
        if (tr != null) tr.gameObject.SetActive(active);
    }

    #region Switch Player

    public void SwitchPlayerToOne()
    {
        // Guard: wenn wir schon P1 sind -> nix machen (verhindert Double-Tick)
        if (activePlayer == "P1")
            return;
        
        if (P2_hasZeroCards()){GameOverController.current.TriggerGameOver("P2"); return;}

        SetHandRaycast("P1-Hand", true, "P1");

        // P2 -> P1 : P1 Traps ticken runter (genau 1x)
        TickDownTraps(P1_Traps);

        SpecialMoveController.current.activeSide = SpecialMoveController.ActiveSide.P1;
        SetCardFlyCentralPoint("GUI-P1-Central-Point");

        fullRayCastBlocker.SetActive(false);

        TutorialHintManager.current.Show_HandFull(1f);

        YourTurnTextAnimator.current_yt.Play();

        P2_TurnSmaragd.SetActive(false);
        P1_TurnSmaragd.SetActive(true);
        YourTurnText.SetActive(true);
    }

    public void SwitchPlayerToTwo()
    {
        // Guard: wenn wir schon P2 sind -> nix machen (verhindert Double-Tick)
        if (activePlayer == "P2")
            return;

        if (P1_hasZeroCards()){GameOverController.current.TriggerGameOver("P1"); return;}

        SetHandRaycast("P1-Hand", false, "P2");

        // P1 -> P2 : P2 Traps ticken runter (genau 1x)
        TickDownTraps(P2_Traps);

        SpecialMoveController.current.activeSide = SpecialMoveController.ActiveSide.P2;
        SetCardFlyCentralPoint("GUI-P2-Central-Point");

        fullRayCastBlocker.SetActive(true);
    }

    private bool P1_hasZeroCards()
    {
        if(CountDeckCards(P1_Deck) == 0){return true;}
        return false;
    }

    private bool P2_hasZeroCards()
    {
        if(CountDeckCards(P2_Deck) == 0){return true;}
        return false;
    }

    private int CountDeckCards(GameObject P_Deck)
    {
        int deck_count = P_Deck.GetComponent<PlayerDeck>().deckCards.Count;

        return deck_count;
    }

    /// <summary>
    /// Aktiviert oder deaktiviert blocksRaycasts für alle CanvasGroups unterhalb eines Hand-Objekts.
    /// </summary>
    private void SetHandRaycast(string handName, bool enable, string newActivePlayer)
    {
        activePlayer = newActivePlayer;

        var hand = GameObject.Find(handName);

        // alle CanvasGroups effizient holen (inkl. verschachtelter Kinder, falls gewünscht)
        foreach (var cg in hand.GetComponentsInChildren<CanvasGroup>(true))
            cg.blocksRaycasts = enable;
    }

    private void SetCardFlyCentralPoint(string centralPointNameNew)
    {
        TCG.CardFlightConfig.Instance.centralPointName = centralPointNameNew;
    }

    #endregion

    /// <summary>
    /// Fordert einen einmaligen Refresh im nächsten Frame an,
    /// um Hierarchie-Änderungen (Enable/Disable/Destroy) sicher zu verarbeiten.
    /// </summary>
    public void RequestRefreshFiguresDeferred()
    {
        if (_refreshScheduled) return;
        _refreshScheduled = true;
        StartCoroutine(Co_DeferredRefresh());
    }

    private IEnumerator Co_DeferredRefresh()
    {
        // 1 Frame warten, damit OnDisable/Destroy von Unity sauber durchlaufen ist
        yield return null;

        // Arrays neu aus der Szene laden
        RefreshFiguresFromScene();

        // Wenn ein aktuelles Target inzwischen inaktiv/weg ist → leeren
        if (current_figures_target_P1 != null && !current_figures_target_P1.activeInHierarchy)
            SetCurrentTargetForP1(null);
        if (current_figures_target_P2 != null && !current_figures_target_P2.activeInHierarchy)
            SetCurrentTargetForP2(null);

        // Selektion sicherstellen + Target-Frame nachziehen
        EnsureValidCurrentSelectionP1();
        RefreshTargetFrameP1();

        EnsureValidCurrentSelectionP2();
        RefreshTargetFrameP2();

        _refreshScheduled = false;
    }

    #region Trap Tick (NEW)

    private void TickDownTraps(GameObject trapsRoot)
    {
        if (trapsRoot == null) return;

        Transform root = trapsRoot.transform;
        int childCount = root.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;

            // TrapCardDisplay sitzt auf dem Child-GameObject
            TrapCardDisplay display = child.GetComponent<TrapCardDisplay>();
            if (display == null) continue;

            // 1 Life verlieren
            display.ModifyLifePoints(-1);

            // Wenn tot -> self destroy (nutzt deine public Funktion)
            if (display.lifepoints <= 0)
            {
                display.DestroySelf();
            }
        }
    }

    #endregion

    #region Locking

    // =========================
    // Action Lock
    // =========================

    public bool IsActionLocked()
    {
        return actionLocked || _actionLockCounter > 0;
    }

    public void LockActions()
    {
        _actionLockCounter++;
        actionLocked = true;

        if (_unlockAfterCoroutine != null)
        {
            StopCoroutine(_unlockAfterCoroutine);
            _unlockAfterCoroutine = null;
        }
    }

    public void UnlockActions()
    {
        _actionLockCounter = Mathf.Max(0, _actionLockCounter - 1);
        if (_actionLockCounter == 0)
            actionLocked = false;
    }

    public void UnlockActionsAfterSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            UnlockActions();
            return;
        }

        if (_unlockAfterCoroutine != null)
            StopCoroutine(_unlockAfterCoroutine);

        _unlockAfterCoroutine = StartCoroutine(Co_UnlockAfter(seconds));
    }

    private IEnumerator Co_UnlockAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _unlockAfterCoroutine = null;
        UnlockActions();
    }

    // --- ADD: Clear selection for active side ---
    public void ClearCurrentSelectionForActiveSide()
    {
        if (activePlayer == "P1")
            ClearCurrentSelectionP1();
        else
            ClearCurrentSelectionP2();
    }

    private void ClearCurrentSelectionP1()
    {
        // Current-Frame aus
        if (current_figure_P1 != null)
        {
            var oldFrame = current_figure_P1.transform.Find("BaseFrameCurrentFigure");
            if (oldFrame) oldFrame.gameObject.SetActive(false);
        }

        // PointLight aus
        ClearActivePointLightP1();

        // Target-Frame aus
        if (lastTargetP1 != null)
            SetTargetFrameActive(lastTargetP1, false);

        lastTargetP1 = null;
        current_figures_target_P1 = null;

        current_figure_P1 = null;
        currentIndexP1 = -1;
    }

    private void ClearCurrentSelectionP2()
    {
        if (current_figure_P2 != null)
        {
            var oldFrame = current_figure_P2.transform.Find("BaseFrameCurrentFigure");
            if (oldFrame) oldFrame.gameObject.SetActive(false);
        }

        ClearActivePointLightP2();

        if (lastTargetP2 != null)
            SetTargetFrameActive(lastTargetP2, false);

        lastTargetP2 = null;
        current_figures_target_P2 = null;

        current_figure_P2 = null;
        currentIndexP2 = -1;
    }

    #endregion
}
