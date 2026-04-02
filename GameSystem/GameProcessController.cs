using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TCG;

public class GameProcessController : MonoBehaviour
{
    public static GameProcessController instance;

    // --- Public fields (wie gehabt) ---
    public int actions_perTurn;

    public GameObject P1_Deck;
    public GameObject P2_Deck;

    public GameObject UI_Cam_P1;
    public GameObject UI_Cam_P2;

    public GameObject GUI_Canvas_P1;
    public GameObject GUI_Canvas_P2;

    public GameObject TurnManager;
    public GameObject TutorialSlides;

    public GameObject Initial_P1_Figure;   // UI-Card GO mit DeckCardDisplay
    public GameObject Initial_P2_Figure;   // UI-Card GO mit DeckCardDisplay

    // Bedeutet gleichzeitig "maximale Handkartenzahl" pro Spieler
    // (z. B. 3 => 0->3, 1->2, 2->1, 3->0)
    public int onBeginTurn_DrawDeckCards = 3;
    public int onBeginTurn_DrawPokerCards;
    public int onBeginTurn_GetPlayerLoad;

    // Bewegte Intro Objekte
    public GameObject Board;      // typischerweise ein RectTransform (UI-Board)
    public GameObject P1_Base;    // typischerweise ein RectTransform (UI-Base)
    public GameObject P2_Base;    // typischerweise ein RectTransform (UI-Base)
    public GameObject MainCamera; // Hauptkamera des Spiels

    // --- Intro-Konfiguration ---
    [Header("Intro: Allgemein")]
    public bool playIntroOnStart = true;

    public enum IntroStepType
    {
        CameraMove,
        BoardMove,
        SpawnFigures,
        PlayerBases,
        P1InitialHand
    }

    [Tooltip("Reihenfolge der Intro-Schritte. Kann im Inspector umsortiert werden.")]
    public IntroStepType[] introOrder = new IntroStepType[]
    {
        IntroStepType.CameraMove,
        IntroStepType.BoardMove,
        IntroStepType.SpawnFigures,
        IntroStepType.PlayerBases,
        IntroStepType.P1InitialHand
    };

    [Header("Intro: Kamera-Move")]
    public bool cameraIntroEnabled = true;

    // Wenn true -> LeanTween.moveLocal (localPosition)
    // Wenn false -> LeanTween.move (world position)
    public bool cameraUseLocalPosition = false;

    public Vector3 cameraTargetPosition = new Vector3(0f, -632.2f, -1597.1f);
    public float cameraMoveDuration = 1.5f;
    public LeanTweenType cameraEase = LeanTweenType.easeOutQuad;

    [Header("Intro: Board-Move")]
    public bool boardIntroEnabled = true;
    public bool boardUseLocalPosition = true; // Board meist im Canvas
    public Vector3 boardTargetPosition = new Vector3(-138.1f, 1.6f, -44.08f);
    public float boardMoveDuration = 1.0f;
    public LeanTweenType boardEase = LeanTweenType.easeOutQuad;

    [Header("Intro: Playerbases-Move")]
    public bool basesIntroEnabled = true;
    public bool basesUseLocalPosition = true;
    public Vector3 p1BaseTargetPosition = new Vector3(-350.6f, -487f, 0f);
    public Vector3 p2BaseTargetPosition = new Vector3(-1.17f, 287.81f, 30.31f);
    public float basesMoveDuration = 0.8f;
    public LeanTweenType basesEase = LeanTweenType.easeOutQuad;

    [Header("Intro: Timings (Spawn & Cards)")]
    public float spawnFiguresDelay = 0.1f;    // kleiner Delay nach Board-Move
    public float basesDelayAfterSpawn = 0.1f; // kleiner Delay nach Figuren
    public float p1CardsDelayAfterBases = 0.1f;

    // --- Cached refs ---
    private PlayerDeck _p1DeckScript;
    private PlayerDeck _p2DeckScript;
    private GraphicRaycaster _rayP1;
    private GraphicRaycaster _rayP2;
    private TurnManager _turnMgr;

    // FigureCaster (neue API: CastFigureFromScratch)
    private TCG.FigureCaster _figureCaster;

    // Hand-Container (für ChildCount)
    private Transform _p1Hand;
    private Transform _p2Hand;

    private enum PlayerSide { P1, P2 }

    #region Unity
    void Awake()
    {
        Application.targetFrameRate = 60;
        instance = this;
        CacheReferences();
    }

    void Start()
    {
        if (playIntroOnStart)
        {
            // Während der Intro die UI-Eingabe deaktivieren
            SetEnabledIfChanged(_rayP1, false);
            SetEnabledIfChanged(_rayP2, false);
            StartCoroutine(RunIntroSequence());
        }
        else
        {
            // Fallback: Sofort alles setzen wie früher
            DoInitialSetupImmediate();
        }
    }
    #endregion

    #region Intro-Sequence
    private IEnumerator RunIntroSequence()
    {
        // Schritte nacheinander gemäß introOrder abspielen
        foreach (var step in introOrder)
        {
            switch (step)
            {
                case IntroStepType.CameraMove:
                    if (cameraIntroEnabled && MainCamera != null)
                        yield return PlayCameraMove();
                    break;

                case IntroStepType.BoardMove:
                    if (boardIntroEnabled && Board != null)
                        yield return PlayBoardMove();
                    break;

                case IntroStepType.SpawnFigures:
                    yield return SpawnFiguresStep();
                    break;

                case IntroStepType.PlayerBases:
                    if (basesIntroEnabled && (P1_Base != null || P2_Base != null))
                        yield return PlayBasesMove();
                    break;

                case IntroStepType.P1InitialHand:
                    yield return P1InitialHandStep();
                    break;
            }
        }

        // Nach der Intro: UI wieder aktivieren und sicherstellen, dass P1 aktiv ist
        SetActivePlayerUI(PlayerSide.P1);
        SetEnabledIfChanged(_rayP1, true);
        SetEnabledIfChanged(_rayP2, false);
    }

    private IEnumerator PlayCameraMove()
    {
        if (MainCamera == null)
            yield break;

        bool done = false;

        if (cameraUseLocalPosition)
        {
            // Bewegt localPosition – benutze Werte wie im RectTransform (relativ zum Parent)
            LeanTween.moveLocal(MainCamera, cameraTargetPosition, cameraMoveDuration)
                     .setEase(cameraEase)
                     .setOnComplete(() => done = true);
        }
        else
        {
            // Bewegt Weltposition – Werte müssen echte world coordinates sein
            LeanTween.move(MainCamera, cameraTargetPosition, cameraMoveDuration)
                     .setEase(cameraEase)
                     .setOnComplete(() => done = true);
        }

        while (!done) yield return null;
    }

    private IEnumerator PlayBoardMove()
    {
        if (Board == null)
            yield break;

        bool done = false;
        if (boardUseLocalPosition)
        {
            LeanTween.moveLocal(Board, boardTargetPosition, boardMoveDuration)
                     .setEase(boardEase)
                     .setOnComplete(() => done = true);
        }
        else
        {
            LeanTween.move(Board, boardTargetPosition, boardMoveDuration)
                     .setEase(boardEase)
                     .setOnComplete(() => done = true);
        }
        while (!done) yield return null;
    }

    private IEnumerator SpawnFiguresStep()
    {
        if (spawnFiguresDelay > 0f)
            yield return new WaitForSeconds(spawnFiguresDelay);

        CastInitialFigures();

        if (GameSessionSettings.tutorialEnabled)
        {
            yield return new WaitForSeconds(1.5f);
            TutorialSlides.SetActive(true);
        }
        if (!GameSessionSettings.tutorialEnabled)
        {
            TutorialHintManager.current.HideHints();
        }
        
        yield break;
    }

    private IEnumerator PlayBasesMove()
    {
        if (basesDelayAfterSpawn > 0f)
            yield return new WaitForSeconds(basesDelayAfterSpawn);

        bool anyTween = false;
        bool p1Done = P1_Base == null;
        bool p2Done = P2_Base == null;

        if (P1_Base != null)
        {
            anyTween = true;
            if (basesUseLocalPosition)
            {
                LeanTween.moveLocal(P1_Base, p1BaseTargetPosition, basesMoveDuration)
                         .setEase(basesEase)
                         .setOnComplete(() => p1Done = true);
            }
            else
            {
                LeanTween.move(P1_Base, p1BaseTargetPosition, basesMoveDuration)
                         .setEase(basesEase)
                         .setOnComplete(() => p1Done = true);
            }
        }

        if (P2_Base != null)
        {
            anyTween = true;
            if (basesUseLocalPosition)
            {
                LeanTween.moveLocal(P2_Base, p2BaseTargetPosition, basesMoveDuration)
                         .setEase(basesEase)
                         .setOnComplete(() => p2Done = true);
            }
            else
            {
                LeanTween.move(P2_Base, p2BaseTargetPosition, basesMoveDuration)
                         .setEase(basesEase)
                         .setOnComplete(() => p2Done = true);
            }
        }

        if (!anyTween)
            yield break;

        while (!p1Done || !p2Done)
            yield return null;
    }

    private IEnumerator P1InitialHandStep()
    {
        if (p1CardsDelayAfterBases > 0f)
            yield return new WaitForSeconds(p1CardsDelayAfterBases);

        DoInitialP1Draw();
        yield break;
    }

    private void DoInitialSetupImmediate()
    {
        // Verhalten wie früher: P1-Hand auffüllen, dann Start-Figuren casten
        DoInitialP1Draw();
        CastInitialFigures();

        // UI sinnvoll initialisieren
        SetActivePlayerUI(PlayerSide.P1);
        SetEnabledIfChanged(_rayP1, true);
        SetEnabledIfChanged(_rayP2, false);
    }

    private void DoInitialP1Draw()
    {
        if (_p1DeckScript == null)
        {
            Debug.LogWarning("[GameProcessController] P1_DeckScript fehlt – Initial-Draw übersprungen.");
            return;
        }

        int toDraw = CalcMissingDeckCards(PlayerSide.P1);
        if (toDraw > 0)
            _p1DeckScript.GiveCard(toDraw);
    }
    #endregion

    #region Public API (gleiches Verhalten)
    public void EndTurnFor_P1()
    {
        if (!ValidateSingletons()) return;
        // Effekt für P2 (neuer aktiver Spieler), Karten ziehen usw.
        ApplyBeginTurnEffectsFor(PlayerSide.P2);
        // UI umschalten, TurnManager schalten
        SetActivePlayerUI(PlayerSide.P2);
        _turnMgr.SwitchPlayerToTwo();
#if UNITY_EDITOR
        Debug.Log("[GameProcessController] Turn -> P2");
#endif
    }

    public void EndTurnFor_P2()
    {
        if (!ValidateSingletons()) return;
        ApplyBeginTurnEffectsFor(PlayerSide.P1);
        SetActivePlayerUI(PlayerSide.P1);
        _turnMgr.SwitchPlayerToOne();
#if UNITY_EDITOR
        Debug.Log("[GameProcessController] Turn -> P1");
#endif
    }
    #endregion

    #region Core
    private void ApplyBeginTurnEffectsFor(PlayerSide sideNowActive)
    {
        // Load verteilen & Pokerkarten spawnen (wie gehabt)
        if (sideNowActive == PlayerSide.P2)
        {
            PlayerBaseController.current.Loading_P2(onBeginTurn_GetPlayerLoad);
            PokerCard_Animation.current.SpawnPokerCards_P2(onBeginTurn_DrawPokerCards);

            // Deckkarten: nur so viele ziehen, bis Handlimit erreicht ist
            int toDraw = CalcMissingDeckCards(PlayerSide.P2);
            if (toDraw > 0) _p2DeckScript?.GiveCard(toDraw);
        }
        else // P1 aktiv
        {
            PlayerBaseController.current.Loading_P1(onBeginTurn_GetPlayerLoad);
            PokerCard_Animation.current.SpawnPokerCards_P1(onBeginTurn_DrawPokerCards);

            int toDraw = CalcMissingDeckCards(PlayerSide.P1);
            if (toDraw > 0) _p1DeckScript?.GiveCard(toDraw);
        }
    }

    private void SetActivePlayerUI(PlayerSide active)
    {
        // Raycaster togglen
        SetEnabledIfChanged(_rayP1, active == PlayerSide.P1);
        SetEnabledIfChanged(_rayP2, active == PlayerSide.P2);

        // UI-Kameras togglen
        //SetActiveIfChanged(UI_Cam_P1, active == PlayerSide.P1);
        //SetActiveIfChanged(UI_Cam_P2, active == PlayerSide.P2);
    }
    #endregion

    #region Initial Figures
    private void CastInitialFigures()
    {
        if (_figureCaster == null)
        {
            Debug.LogWarning("[GameProcessController] Kein FigureCaster gefunden – Initial-Figuren werden nicht gecastet.");
            return;
        }

        // P1
        if (TryExtractDeckCardFromGO(Initial_P1_Figure, out var p1Card))
        {
            var fig = _figureCaster.CastFigureFromScratch(p1Card, "P1");
#if UNITY_EDITOR
            if (fig == null) Debug.LogWarning("[GameProcessController] Initial_P1_Figure konnte nicht gecastet werden.");
#endif
        }
        else
        {
            Debug.LogWarning("[GameProcessController] Initial_P1_Figure enthält keine DeckCard (DeckCardDisplay?).");
        }

        // P2
        if (TryExtractDeckCardFromGO(Initial_P2_Figure, out var p2Card))
        {
            var fig = _figureCaster.CastFigureFromScratch(p2Card, "P2");
#if UNITY_EDITOR
            if (fig == null) Debug.LogWarning("[GameProcessController] Initial_P2_Figure konnte nicht gecastet werden.");
#endif
        }
        else
        {
            Debug.LogWarning("[GameProcessController] Initial_P2_Figure enthält keine DeckCard (DeckCardDisplay?).");
        }
    }
    #endregion

    #region Hand-/Draw-Logik
    /// <summary>
    /// Ermittelt, wie viele Deckkarten nachgezogen werden müssen, um das Handmaximum
    /// (onBeginTurn_DrawDeckCards) zu erreichen. Zählt dazu die Childs von "P1-Hand" bzw. "P2-Hand".
    /// </summary>
    private int CalcMissingDeckCards(PlayerSide side)
    {
        int maxHand = Mathf.Max(0, onBeginTurn_DrawDeckCards); // defensiv
        int current = GetCurrentHandCount(side);
        int missing = maxHand - current;
        if (missing < 0) missing = 0; // Überhang nicht negativ werden lassen
        return missing;
    }

    private int GetCurrentHandCount(PlayerSide side)
    {
        Transform hand = (side == PlayerSide.P1) ? _p1Hand : _p2Hand;
        if (hand == null) return 0;
        return hand.childCount;
    }
    #endregion

    #region Helpers
    private void CacheReferences()
    {
        if (P1_Deck != null && P1_Deck.TryGetComponent(out PlayerDeck p1Deck)) _p1DeckScript = p1Deck;
        if (P2_Deck != null && P2_Deck.TryGetComponent(out PlayerDeck p2Deck)) _p2DeckScript = p2Deck;

        if (GUI_Canvas_P1 != null && GUI_Canvas_P1.TryGetComponent(out GraphicRaycaster r1)) _rayP1 = r1;
        if (GUI_Canvas_P2 != null && GUI_Canvas_P2.TryGetComponent(out GraphicRaycaster r2)) _rayP2 = r2;

        if (TurnManager != null && TurnManager.TryGetComponent(out TurnManager tm)) _turnMgr = tm;

        // FigureCaster finden (im TCG-Namespace)
        _figureCaster = FindObjectOfType<TCG.FigureCaster>(includeInactive: true);

        // Hand-Container suchen: "GUI-Canvas-P1/P1-Hand" und analog P2
        _p1Hand = TryFindChildByPath(GUI_Canvas_P1, "P1-Hand");
        _p2Hand = TryFindChildByPath(GUI_Canvas_P2, "P2-Hand");

#if UNITY_EDITOR
        if (_p1DeckScript == null) Debug.LogWarning("[GameProcessController] Kein PlayerDeck auf P1_Deck gefunden.");
        if (_p2DeckScript == null) Debug.LogWarning("[GameProcessController] Kein PlayerDeck auf P2_Deck gefunden.");
        if (_rayP1 == null) Debug.LogWarning("[GameProcessController] Kein GraphicRaycaster auf GUI_Canvas_P1 gefunden.");
        if (_rayP2 == null) Debug.LogWarning("[GameProcessController] Kein GraphicRaycaster auf GUI_Canvas_P2 gefunden.");
        if (_turnMgr == null) Debug.LogWarning("[GameProcessController] TurnManager-Komponente fehlt.");
        if (_figureCaster == null) Debug.LogWarning("[GameProcessController] FigureCaster nicht gefunden.");
        if (_p1Hand == null) Debug.LogWarning("[GameProcessController] P1-Hand Transform nicht gefunden (erwartet unter GUI-Canvas-P1).");
        if (_p2Hand == null) Debug.LogWarning("[GameProcessController] P2-Hand Transform nicht gefunden (erwartet unter GUI-Canvas-P2).");
#endif
    }

    private static Transform TryFindChildByPath(GameObject rootGO, string childName)
    {
        if (rootGO == null || string.IsNullOrEmpty(childName)) return null;

        // 1) Direkter Child unterhalb des Canvas
        Transform t = rootGO.transform.Find(childName);
        if (t != null) return t;

        // 2) Tiefensuche als Fallback
        return FindDeep(rootGO.transform, childName);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform hit = FindDeep(child, name);
            if (hit != null) return hit;
        }
        return null;
    }

    private bool ValidateSingletons()
    {
        if (PlayerBaseController.current == null)
        {
            Debug.LogError("[GameProcessController] PlayerBaseController.current ist null.");
            return false;
        }
        if (PokerCard_Animation.current == null)
        {
            Debug.LogError("[GameProcessController] PokerCard_Animation.current ist null.");
            return false;
        }
        if (_turnMgr == null)
        {
            Debug.LogError("[GameProcessController] TurnManager fehlt.");
            return false;
        }
        return true;
    }

    private static void SetActiveIfChanged(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf != active) go.SetActive(active);
    }

    private static void SetEnabledIfChanged(Behaviour comp, bool enabled)
    {
        if (comp == null) return;
        if (comp.enabled != enabled) comp.enabled = enabled;
    }

    /// <summary>
    /// Extrahiert eine DeckCard aus einem GameObject. Erwartet typischerweise eine Komponente
    /// "TCG.DeckCardDisplay" mit einer öffentlichen Property/Feld (z. B. "DeckCard", "Card" o.ä.).
    /// Fällt zurück auf direkte DeckCard-Komponente.
    /// </summary>
    private static bool TryExtractDeckCardFromGO(GameObject go, out DeckCard card)
    {
        card = null;
        if (go == null) return false;

        // DeckCard ist ein ScriptableObject → niemals via GetComponent abfragen!
        // Stattdessen über eine Anzeige-/Bridge-Komponente (DeckCardDisplay) extrahieren.

        // 1) Eigene DeckCardDisplay-Komponente
        if (TryExtractFromDeckCardDisplay(go.GetComponent<DeckCardDisplay>(), out card))
            return true;

        // 2) In Kindern nach einem DeckCardDisplay suchen
        var dcdChild = go.GetComponentInChildren<DeckCardDisplay>(includeInactive: true);
        if (TryExtractFromDeckCardDisplay(dcdChild, out card))
            return true;

        return false;
    }

    private static bool TryExtractFromDeckCardDisplay(DeckCardDisplay dcd, out DeckCard card)
    {
        card = null;
        if (dcd == null) return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Properties zuerst (häufigster Fall)
        string[] propNames = { "DeckCard", "Card", "deckCard", "card" };
        foreach (var pn in propNames)
        {
            var p = dcd.GetType().GetProperty(pn, flags);
            if (p != null && p.CanRead)
            {
                var val = p.GetValue(dcd, null);
                if (val is DeckCard dc) { card = dc; return true; }
            }
        }

        // Felder
        string[] fieldNames = { "DeckCard", "Card", "deckCard", "card" };
        foreach (var fn in fieldNames)
        {
            var f = dcd.GetType().GetField(fn, flags);
            if (f != null)
            {
                var val = f.GetValue(dcd);
                if (val is DeckCard dc) { card = dc; return true; }
            }
        }

        return false;
    }
    #endregion
}
