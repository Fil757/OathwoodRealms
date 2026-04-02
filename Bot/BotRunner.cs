using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CustomInspector;
using TMPro;
using TCG; // <-- wichtig für DeckCardDisplay

/// <summary>
/// BotRunner (stabilisiert):
/// - Speichert bei Karten-Moves die DeckCardDisplay-Referenz (kein "c1/c2" drift mehr)
/// - Spielt Karten über display.PlaySelfFlightAndNotifyMirror() (exakt die gepickte UI-Karte fliegt)
/// - Restliche Logik wie gehabt
/// </summary>
public class BotRunner : MonoBehaviour
{
    #region Inspector

    public GameObject BOT_FIGURES;
    public GameObject OPPONENT_FIGURES;

    public GameObject BOT_PLAYER;
    public GameObject BOT_HAND;

    [Header("UI Canvases")]
    [SerializeField] private GraphicRaycaster guiCanvasP1;
    [SerializeField] private GraphicRaycaster guiCanvasP2;

    [Header("Thinking UI (TMP)")]
    [SerializeField] private TMP_Text botThinkingText;

    [Range(0f, 1f)]
    [SerializeField] private float thinkingMaxAlpha = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float thinkingMinAlpha = 0f;

    [Range(0.05f, 2f)]
    [SerializeField] private float thinkingFadeDuration = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float thinkingHoldAtMax = 0.05f;

    [Range(0f, 1f)]
    [SerializeField] private float thinkingHoldAtMin = 0.05f;

    [Range(0f, 2f)]
    [SerializeField] private float thinkingPauseAfterMoveSeconds = 0.5f;

    [Header("Target Wait")]
    [Range(0f, 10f)]
    [SerializeField] private float waitAfterTargetSeconds = 3f;

    [Header("Core")]
    [SerializeField] private TurnManager turnManager;

    [Header("Bot Turn Runtime")]
    public float WaitMinSeconds = 2f;
    public float WaitMaxSeconds = 4f;
    public bool WaitForSpecialMoveBeforeEachAction = true;

    [Header("No-Move Grace")]
    [Range(0, 10)]
    public int NoMoveRecheckTries = 3;

    [Range(0f, 1f)]
    public float NoMoveRecheckDelay = 0.15f;

    [Header("End Turn Discard")]
    [Range(0, 10)]
    public int DiscardIfHandAtLeast = 4;

    [Range(0f, 2f)]
    public float DiscardDelaySeconds = 0.25f;

    [Header("Weights (simpel)")]
    public int W_Attack = 5;
    public int W_Special = 4;
    public int W_Card = 3;
    public int W_Target = 2;
    public int W_Defend = 1;

    [Header("Debug")]
    public bool LogPickedMoves = false;

    #endregion

    #region Runtime State

    public List<string> MOVES_CONSIDER_LIST = new List<string>();

    private readonly List<MoveOption> _options = new List<MoveOption>(32);

    private Coroutine _turnRoutine;
    private bool _isRunning;

    private Coroutine _thinkingRoutine;
    private bool _thinkingShouldPulse;

    #endregion

    #region MoveOption

    private enum MoveKind
    {
        Figure,
        Card
    }

    private struct MoveOption
    {
        public MoveKind kind;
        public string id;
        public int weight;

        public GameObject figure;           // für Figure-Moves
        public DeckCardDisplay cardDisplay; // für Card-Moves

        public MoveOption(MoveKind kind, string id, int weight, GameObject figure, DeckCardDisplay cardDisplay)
        {
            this.kind = kind;
            this.id = id;
            this.weight = Mathf.Max(0, weight);
            this.figure = figure;
            this.cardDisplay = cardDisplay;
        }
    }

    private void AddFigureOption(string id, int weight)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (weight <= 0) return;

        _options.Add(new MoveOption(MoveKind.Figure, id, weight, null, null));
    }

    private void AddCardOption(string id, int weight, DeckCardDisplay display)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (weight <= 0) return;
        if (display == null) return;

        _options.Add(new MoveOption(MoveKind.Card, id, weight, null, display));
    }

    #endregion

    #region Public API

    public void Trigger_StartBotTurn()
    {
        if (_isRunning) return;
        _turnRoutine = StartCoroutine(BotTurnRoutine());
    }

    public void Trigger_StopBotTurn()
    {
        if (_turnRoutine != null) StopCoroutine(_turnRoutine);
        _turnRoutine = null;
        _isRunning = false;

        SetThinking(false);
    }

    private void OnDisable()
    {
        Trigger_StopBotTurn();
    }

    private void Awake()
    {
        SetThinking(false);
    }

    #endregion

    #region Thinking UI

    private void SetThinking(bool active)
    {
        _thinkingShouldPulse = active;

        if (botThinkingText == null)
            return;

        if (!active)
        {
            if (_thinkingRoutine != null) StopCoroutine(_thinkingRoutine);
            _thinkingRoutine = null;

            SetThinkingAlpha(thinkingMinAlpha);
            botThinkingText.enabled = false;
        }
        else
        {
            botThinkingText.enabled = true;

            if (_thinkingRoutine == null)
                _thinkingRoutine = StartCoroutine(ThinkingPulseRoutine());
        }
    }

    private IEnumerator ThinkingPulseRoutine()
    {
        if (botThinkingText == null) yield break;

        SetThinkingAlpha(thinkingMinAlpha);

        while (_thinkingShouldPulse && _isRunning)
        {
            yield return FadeThinking(thinkingMinAlpha, thinkingMaxAlpha, thinkingFadeDuration);
            if (!_thinkingShouldPulse || !_isRunning) break;
            if (thinkingHoldAtMax > 0f) yield return new WaitForSeconds(thinkingHoldAtMax);

            yield return FadeThinking(thinkingMaxAlpha, thinkingMinAlpha, thinkingFadeDuration);
            if (!_thinkingShouldPulse || !_isRunning) break;
            if (thinkingHoldAtMin > 0f) yield return new WaitForSeconds(thinkingHoldAtMin);
        }

        SetThinkingAlpha(thinkingMinAlpha);
        if (botThinkingText != null) botThinkingText.enabled = false;
        _thinkingRoutine = null;
    }

    private IEnumerator FadeThinking(float from, float to, float duration)
    {
        if (botThinkingText == null) yield break;

        duration = Mathf.Max(0.0001f, duration);
        float t = 0f;

        while (t < duration)
        {
            if (!_thinkingShouldPulse || !_isRunning) yield break;

            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            SetThinkingAlpha(a);
            yield return null;
        }

        SetThinkingAlpha(to);
    }

    private void SetThinkingAlpha(float a)
    {
        if (botThinkingText == null) return;

        Color c = botThinkingText.color;
        c.a = Mathf.Clamp01(a);
        botThinkingText.color = c;
    }

    private void ForceThinkingOff()
    {
        _thinkingShouldPulse = false;

        if (botThinkingText == null)
            return;

        if (_thinkingRoutine != null) StopCoroutine(_thinkingRoutine);
        _thinkingRoutine = null;

        SetThinkingAlpha(thinkingMinAlpha);
        botThinkingText.enabled = false;
    }

    #endregion

    #region Core Loop

    private IEnumerator BotTurnRoutine()
    {
        _isRunning = true;

        SetThinking(true);
        yield return new WaitForSeconds(3f);

        float minWait = Mathf.Max(0f, WaitMinSeconds);
        float maxWait = Mathf.Max(minWait, WaitMaxSeconds);

        while (true)
        {
            if (SpecialMove_Ongoing())
                ForceThinkingOff();
            else
                SetThinking(true);

            if (WaitForSpecialMoveBeforeEachAction)
                yield return WaitUntilNoSpecialMove();

            bool foundAny = false;
            for (int i = 0; i <= NoMoveRecheckTries; i++)
            {
                Consider_possible_actions();

                if (_options.Count > 0)
                {
                    foundAny = true;
                    break;
                }

                if (i < NoMoveRecheckTries)
                {
                    if (SpecialMove_Ongoing())
                        yield return WaitUntilNoSpecialMove();

                    if (NoMoveRecheckDelay > 0f)
                        yield return new WaitForSeconds(NoMoveRecheckDelay);
                }
            }

            if (!foundAny)
            {
                yield return WaitUntilNoSpecialMove();
                yield return DiscardRandom_IfHandTooLarge_AtEndTurn();
                yield return WaitUntilNoSpecialMove();

                ForceThinkingOff();
                EndTurn_Dummy();
                break;
            }

            MoveOption pick = PickWeightedMove();
            if (string.IsNullOrEmpty(pick.id))
            {
                yield return WaitUntilNoSpecialMove();
                yield return DiscardRandom_IfHandTooLarge_AtEndTurn();
                yield return WaitUntilNoSpecialMove();

                ForceThinkingOff();
                EndTurn_Dummy();
                break;
            }

            if (LogPickedMoves)
                Debug.Log($"[BotRunner] Picked move: {pick.id}");

            ForceThinkingOff();

            bool wasTargetMove = IsTargetMoveId(pick.id);

            ExecutePickedMove(pick);

            if (wasTargetMove && waitAfterTargetSeconds > 0f)
                yield return new WaitForSeconds(waitAfterTargetSeconds);

            if (thinkingPauseAfterMoveSeconds > 0f)
                yield return new WaitForSeconds(thinkingPauseAfterMoveSeconds);

            yield return WaitUntilNoSpecialMove();

            float wait = Random.Range(minWait, maxWait);
            if (wait > 0f)
            {
                float endTime = Time.time + wait;
                while (Time.time < endTime)
                {
                    if (SpecialMove_Ongoing())
                        ForceThinkingOff();
                    else
                        SetThinking(true);

                    yield return null;
                }
            }
        }

        _isRunning = false;
        _turnRoutine = null;

        ForceThinkingOff();
    }

    private IEnumerator WaitUntilNoSpecialMove()
    {
        while (SpecialMove_Ongoing())
        {
            ForceThinkingOff();
            yield return null;
        }
    }

    private void EndTurn_Dummy()
    {
        Trigger_StopBotTurn();

        if (guiCanvasP2 != null)
            guiCanvasP2.enabled = false;

        if (guiCanvasP1 != null)
            guiCanvasP1.enabled = true;

        GameProcessController.instance.EndTurnFor_P2();
        ParticleCleaner.current.DestroyFinishedParticles();

        TurnManager.current.SwitchPlayerToOne();
    }

    #endregion

    #region Consider & Pick

    public void Consider_possible_actions()
    {
        MOVES_CONSIDER_LIST.Clear();
        _options.Clear();

        CheckFigures();
        CheckCards();

        for (int i = 0; i < _options.Count; i++)
        {
            if (!MOVES_CONSIDER_LIST.Contains(_options[i].id))
                MOVES_CONSIDER_LIST.Add(_options[i].id);
        }
    }

    private MoveOption PickWeightedMove()
    {
        if (_options == null || _options.Count == 0)
            return default;

        int total = 0;
        for (int i = 0; i < _options.Count; i++)
            total += Mathf.Max(0, _options[i].weight);

        if (total <= 0)
            return default;

        int roll = Random.Range(0, total);
        int acc = 0;

        for (int i = 0; i < _options.Count; i++)
        {
            acc += Mathf.Max(0, _options[i].weight);
            if (roll < acc)
                return _options[i];
        }

        return _options[_options.Count - 1];
    }

    private bool IsTargetMoveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return id == "f1_Target" || id == "f2_Target" || id == "f3_Target";
    }

    #endregion

    #region Figures (unverändert in Ergebnis)

    private void CheckFigures()
    {
        if (BOT_FIGURES == null) return;

        int count = BOT_FIGURES.transform.childCount;
        if (count <= 0) return;

        if (count >= 1)
        {
            var figure = BOT_FIGURES.transform.GetChild(0).gameObject;
            CheckAllFigureActions(figure, "f1");
        }

        if (count >= 2)
        {
            var figure = BOT_FIGURES.transform.GetChild(1).gameObject;
            CheckAllFigureActions(figure, "f2");
        }

        if (count >= 3)
        {
            var figure = BOT_FIGURES.transform.GetChild(2).gameObject;
            CheckAllFigureActions(figure, "f3");
        }
    }

    private void CheckAllFigureActions(GameObject figure, string prefix)
    {
        if (figure == null) return;

        int before = _options.Count;

        CheckFigureAttack(figure, $"{prefix}_Atk");
        CheckFigureTarget(figure, $"{prefix}_Target");
        CheckFigureSpecialMove(figure, $"{prefix}_Special");

        int after = _options.Count;

        if (after == before)
        {
            CheckFigureDefend(figure, $"{prefix}_Def");
        }
    }

    private void CheckFigureAttack(GameObject Figure, string considerId)
    {
        if (!Figure_can_pay_Move(Figure)) return;
        if (Figure_is_attacking(Figure)) return;
        if (SpecialMove_Ongoing()) return;

        if (!Figure_has_Target(Figure) && Opponent_has_active_Figures()) return;

        AddFigureOption(considerId, W_Attack);
    }

    private void CheckFigureDefend(GameObject Figure, string considerId)
    {
        if (!Figure_can_pay_Move(Figure)) return;
        if (Figure_is_attacking(Figure)) return;
        if (SpecialMove_Ongoing()) return;
        if (Figure_is_defending(Figure)) return;

        AddFigureOption(considerId, W_Defend);
    }

    private void CheckFigureTarget(GameObject Figure, string considerId)
    {
        if (!Figure_can_pay_Move(Figure)) return;
        if (SpecialMove_Ongoing()) return;
        if (!Opponent_has_active_Figures()) return;
        if (Figure_has_Target(Figure)) return;
        if (!Figure_is_current_turn_figure(Figure)) return;

        AddFigureOption(considerId, W_Target);
    }

    private bool Figure_is_current_turn_figure(GameObject figure)
    {
        if (figure == null) return false;
        if (TurnManager.current == null) return false;

        return TurnManager.current.current_figure_P2 == figure;
    }

    private void CheckFigureSpecialMove(GameObject Figure, string considerId)
    {
        if (!Figure_can_pay_SpecialMove(Figure)) return;
        if (Figure_is_attacking(Figure)) return;
        if (SpecialMove_Ongoing()) return;
        if (!SpecialMove_MakesSense(Figure)) return;

        AddFigureOption(considerId, W_Special);
    }

    #endregion

    #region Cards (FIX)

    private void CheckCards()
    {
        if (BOT_HAND == null) return;

        Transform hand = BOT_HAND.transform;
        int count = Mathf.Min(hand.childCount, 4);
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Transform child = hand.GetChild(i);
            if (child == null) continue;

            var display = child.GetComponent<DeckCardDisplay>();
            if (display == null) continue;

            DeckCard card = display.card;
            if (card == null) continue;

            // Wir speichern die DISPLAY-Referenz, nicht nur "c1"
            if (!Bot_can_pay_Card(card)) continue;
            if (SpecialMove_Ongoing()) continue;
            if (!Playing_Card_MakesSense(card)) continue;

            AddCardOption($"c{i + 1}", W_Card, display);
        }
    }

    #endregion

    #region Execute Picked Move

    private void ExecutePickedMove(MoveOption pick)
    {
        if (string.IsNullOrEmpty(pick.id)) return;

        // Figure-Moves bleiben über switch wie gehabt
        if (pick.kind == MoveKind.Figure)
        {
            Play_RandomPicked_ConsideredMove(pick.id);
            return;
        }

        // Card-Moves: EXAKT diese Karte abspielen
        if (pick.kind == MoveKind.Card)
        {
            var d = pick.cardDisplay;

            // Guards: Karte muss noch existieren & aktiv sein
            if (d == null) return;
            if (!d.gameObject.activeInHierarchy) return;

            // Guard: muss (noch) in der BOT_HAND liegen (sonst ist sie evtl. schon geflogen/entsorgt)
            if (BOT_HAND == null) return;
            Transform hand = BOT_HAND.transform;
            if (d.transform.parent != hand)
            {
                // Wenn sie z.B. schon in FlyLayer umgehängt wurde, dann ist sie bereits im Flug/Pending.
                // In dem Fall lieber gar nichts tun, statt falsche Karte zu spielen.
                return;
            }

            // Entscheidend: diese Instanz startet ihren Flug + Mirror-Notify
            d.PlaySelfFlightAndNotifyMirror();
            return;
        }
    }

    #endregion

    #region Discard (End of Turn) - unverändert

    private IEnumerator DiscardRandom_IfHandTooLarge_AtEndTurn()
    {
        if (BOT_HAND == null) yield break;

        Transform hand = BOT_HAND.transform;
        int handCount = hand != null ? hand.childCount : 0;

        if (DiscardIfHandAtLeast <= 0) yield break;
        if (handCount < DiscardIfHandAtLeast) yield break;

        int discardCount = Random.Range(1, 3);
        discardCount = Mathf.Clamp(discardCount, 1, handCount);

        HashSet<int> picked = new HashSet<int>();
        int safety = 0;
        while (picked.Count < discardCount && safety++ < 50)
            picked.Add(Random.Range(0, handCount));

        List<RemoveCard> removeCards = new List<RemoveCard>(discardCount);

        foreach (int idx in picked)
        {
            if (hand == null) break;
            if (idx < 0 || idx >= hand.childCount) continue;

            Transform cardT = hand.GetChild(idx);
            if (cardT == null) continue;

            RemoveCard rc = cardT.GetComponent<RemoveCard>();
            if (rc == null)
            {
                rc = cardT.GetComponentInChildren<RemoveCard>();
                if (rc == null) rc = cardT.GetComponentInParent<RemoveCard>();
            }

            if (rc != null)
                removeCards.Add(rc);
        }

        for (int i = 0; i < removeCards.Count; i++)
            removeCards[i].FlyAway();

        if (DiscardDelaySeconds > 0f)
            yield return new WaitForSeconds(DiscardDelaySeconds);
    }

    #endregion

    #region Helpers - Guards (unverändert)

    private bool Figure_can_pay_Move(GameObject Figure)
    {
        if (Figure == null) return false;
        var df = Figure.GetComponent<Display_Figure>();
        if (df == null) return false;
        return df.FIGURE_LOAD >= df.FIGURE_COST;
    }

    private bool Figure_can_pay_SpecialMove(GameObject Figure)
    {
        if (Figure == null) return false;
        var df = Figure.GetComponent<Display_Figure>();
        if (df == null) return false;
        return df.FIGURE_LOAD >= df.FIGURE_COST_SPC;
    }

    private bool Figure_has_Target(GameObject Figure)
    {
        if (Figure == null) return false;
        var df = Figure.GetComponent<Display_Figure>();
        if (df == null) return false;
        return df.FIGURE_TARGET != null;
    }

    private bool Figure_is_attacking(GameObject Figure)
    {
        if (Figure == null) return false;
        var df = Figure.GetComponent<Display_Figure>();
        if (df == null) return false;
        return df.IS_ATTACKING;
    }

    private bool Figure_is_defending(GameObject Figure)
    {
        if (Figure == null) return false;
        var df = Figure.GetComponent<Display_Figure>();
        if (df == null) return false;
        return df.IS_DEFENDING;
    }

    private bool Opponent_has_active_Figures()
    {
        return OPPONENT_FIGURES != null && OPPONENT_FIGURES.transform.childCount > 0;
    }

    private bool SpecialMove_Ongoing()
    {
        var rootGO = GameObject.Find("OngoingSpecialMoves");
        if (rootGO == null)
            return false;

        return rootGO.transform.childCount > 0;
    }

    private bool SpecialMove_MakesSense(GameObject Figure)
    {
        if (Figure == null) return false;
        var df = Figure.GetComponent<Display_Figure>();
        if (df == null || df.FIGURE == null || df.FIGURE.SPECIAL_A == null) return false;

        ConditionBlock block_to_test = df.FIGURE.SPECIAL_A.MakeSenseEvaluation_Block;
        if (block_to_test == null) return false;

        return FieldCardController_P2.instance.EvaluateConditionBlock(block_to_test);
    }

    private bool Bot_can_pay_Card(DeckCard Card)
    {
        if (Card == null) return false;
        if (BOT_PLAYER == null) return false;

        var df = BOT_PLAYER.GetComponent<Display_Figure>();
        if (df == null) return false;

        return df.FIGURE_LOAD >= Card.PlayerLoadCost;
    }

    private bool Playing_Card_MakesSense(DeckCard Card)
    {
        ConditionBlock block_to_test = Card.MakeSenseEvaluation_Block_Card;
        return FieldCardController_P2.instance.EvaluateConditionBlock(block_to_test);
    }

    #endregion

    #region Execute Moves (dein Switch bleibt)

    private void Play_RandomPicked_ConsideredMove(string considerId)
    {
        if (string.IsNullOrEmpty(considerId)) return;

        switch (considerId)
        {
            case "f1_Atk": Figure_1_Attack(); break;
            case "f1_Def": Figure_1_Defend(); break;
            case "f1_Target": Figure_1_Target(); break;
            case "f1_Special": Figure_1_SpecialMove(); break;

            case "f2_Atk": Figure_2_Attack(); break;
            case "f2_Def": Figure_2_Defend(); break;
            case "f2_Target": Figure_2_Target(); break;
            case "f2_Special": Figure_2_SpecialMove(); break;

            case "f3_Atk": Figure_3_Attack(); break;
            case "f3_Def": Figure_3_Defend(); break;
            case "f3_Target": Figure_3_Target(); break;
            case "f3_Special": Figure_3_SpecialMove(); break;

            // c1..c4 werden nicht mehr über Child-Index gespielt (bewusst)
        }
    }

    // ATTACK
    private void Figure_1_Attack()
    {
        if (AttackController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 1) return;
        AttackController.current.AttackWithFigure(BOT_FIGURES.transform.GetChild(0).gameObject, AttackController.PlayerSide.P2);
    }

    private void Figure_2_Attack()
    {
        if (AttackController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 2) return;
        AttackController.current.AttackWithFigure(BOT_FIGURES.transform.GetChild(1).gameObject, AttackController.PlayerSide.P2);
    }

    private void Figure_3_Attack()
    {
        if (AttackController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 3) return;
        AttackController.current.AttackWithFigure(BOT_FIGURES.transform.GetChild(2).gameObject, AttackController.PlayerSide.P2);
    }

    // DEFEND
    private void Figure_1_Defend()
    {
        if (AttackController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 1) return;
        AttackController.current.Activate_Defense(BOT_FIGURES.transform.GetChild(0).gameObject);
    }

    private void Figure_2_Defend()
    {
        if (AttackController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 2) return;
        AttackController.current.Activate_Defense(BOT_FIGURES.transform.GetChild(1).gameObject);
    }

    private void Figure_3_Defend()
    {
        if (AttackController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 3) return;
        AttackController.current.Activate_Defense(BOT_FIGURES.transform.GetChild(2).gameObject);
    }

    // TARGET
    private void Figure_1_Target()
    {
        if (TargetingSystem.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 1) return;
        TargetingSystem.current.RunAutoTargeting(BOT_FIGURES.transform.GetChild(0).gameObject);
    }

    private void Figure_2_Target()
    {
        if (TargetingSystem.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 2) return;
        TargetingSystem.current.RunAutoTargeting(BOT_FIGURES.transform.GetChild(1).gameObject);
    }

    private void Figure_3_Target()
    {
        if (TargetingSystem.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 3) return;
        TargetingSystem.current.RunAutoTargeting(BOT_FIGURES.transform.GetChild(2).gameObject);
    }

    // SPECIAL
    private void Figure_1_SpecialMove()
    {
        if (SpecialMoveController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 1) return;

        var df = BOT_FIGURES.transform.GetChild(0).gameObject.GetComponent<Display_Figure>();
        if (df == null || df.FIGURE == null || df.FIGURE.SPECIAL_A == null) return;

        SpecialMoveController.current.PlaySpecialMoveFromFigure(BOT_FIGURES.transform.GetChild(0).gameObject);
    }

    private void Figure_2_SpecialMove()
    {
        if (SpecialMoveController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 2) return;

        var df = BOT_FIGURES.transform.GetChild(1).gameObject.GetComponent<Display_Figure>();
        if (df == null || df.FIGURE == null || df.FIGURE.SPECIAL_A == null) return;

        SpecialMoveController.current.PlaySpecialMoveFromFigure(BOT_FIGURES.transform.GetChild(1).gameObject);
    }

    private void Figure_3_SpecialMove()
    {
        if (SpecialMoveController.current == null) return;
        if (BOT_FIGURES == null || BOT_FIGURES.transform.childCount < 3) return;

        var df = BOT_FIGURES.transform.GetChild(2).gameObject.GetComponent<Display_Figure>();
        if (df == null || df.FIGURE == null || df.FIGURE.SPECIAL_A == null) return;

        SpecialMoveController.current.PlaySpecialMoveFromFigure(BOT_FIGURES.transform.GetChild(2).gameObject);
    }

    #endregion
}
