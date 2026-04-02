using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CustomInspector;

[DefaultExecutionOrder(-2000)]
public class FieldCardController : MonoBehaviour
{
    #region 0) Inspector + Debug

    public static FieldCardController instance;

    [Header("DEBUG")]
    public bool debug = true;
    [Tooltip("Heartbeat Log alle X Sekunden. 0 = aus.")]
    public float heartbeatSeconds = 2f;

    [Header("Timing")]
    [Tooltip("Delay bevor Checks laufen (Realtime).")]
    public float checkDelay = 0.1f;

    [Header("FieldCards")]
    [Tooltip("Alle aktuell ausliegenden FieldCards auf dem Feld. Leere Slots sind null.")]
    public FieldCard[] ActiveFieldCard;

    [Tooltip("Paralleles Array zu ActiveFieldCard[]. True = StatChange-Effekt dieser Karte ist aktiv.")]
    public bool[] FieldCard_StatChangeEffect_isActive;

    [Tooltip("Aktivierte Fallen")]
    public List<FieldCard> ActiveTrapCard = new List<FieldCard>();

    [Tooltip("Figuren Field Effekte")]
    public List<FieldCard> ActiveFigureFieldEffect = new List<FieldCard>();

    [Tooltip("Merker, ob der Figuren-Feldeffekt (Stat-Teil) bereits aktiv angewendet wurde (Index passt zu ActiveFigureFieldEffect).")]
    public List<bool> FigureFieldEffect_isActive = new List<bool>();

    [Tooltip("Merker: letzter Condition-Status je Figuren-Feldeffekt, um Trigger-Action nur einmalig bei false->true auszulösen.")]
    public List<bool> FigureFieldEffect_lastConditionTrue = new List<bool>();

    [Header("FieldCard Slots - P1")]
    public GameObject[] FieldCardSlot;

    [Header("VFX / SFX")]
    public int fieldVfxIndex_Main = 12;
    public int reverseVfxIndex = 4;

    #endregion

    #region 1) Lifetime / Diagnose

    private Coroutine _heartbeatRoutine;

    private void Awake()
    {
        // Diagnose: mehrere Controller?
        var all = FindObjectsOfType<FieldCardController>(true);
        if (all != null && all.Length > 1)
        {
            Debug.LogWarning($"[FCC] WARNING: Mehrere FieldCardController in Szene gefunden: {all.Length}. " +
                             $"instance wird ggf. überschrieben. (dieser: {name})");
        }

        instance = this;

        if (debug) Debug.Log($"[FCC] Awake() on '{name}' | enabled={enabled} activeInHierarchy={gameObject.activeInHierarchy}");
    }

    private void OnEnable()
    {
        if (debug) Debug.Log($"[FCC] OnEnable() on '{name}'");

        if (heartbeatSeconds > 0f && _heartbeatRoutine == null)
            _heartbeatRoutine = StartCoroutine(Heartbeat());
    }

    private void Start()
    {
        if (debug)
        {
            Debug.Log($"[FCC] Start() on '{name}'");
            Debug.Log($"[FCC] References: TurnManager={(TurnManager.current ? "OK" : "NULL")} | " +
                      $"SpecialMoveController.current={(SpecialMoveController.current ? "OK" : "NULL")} | " +
                      $"FieldCaster.instance={(FieldCaster.instance ? "OK" : "NULL")} | " +
                      $"ParticleController.Instance={(ParticleController.Instance ? "OK" : "NULL")} | " +
                      $"CameraShake.current={(CameraShake.current ? "OK" : "NULL")}");
            Debug.Log($"[FCC] Arrays: ActiveFieldCard={(ActiveFieldCard==null ? "NULL" : ActiveFieldCard.Length.ToString())} | " +
                      $"FieldCardSlot={(FieldCardSlot==null ? "NULL" : FieldCardSlot.Length.ToString())} | " +
                      $"Traps={ActiveTrapCard?.Count ?? -1} | FigureEffects={ActiveFigureFieldEffect?.Count ?? -1}");
        }

        RefreshFigureList();
        RefreshPlayerList();
        EnsureStatChangeArraySize();
        EnsureFigureFieldEffectListSize();

        // Optional: einmal initial checken
        RequestCheck();
    }

    private void OnDisable()
    {
        if (debug) Debug.Log($"[FCC] OnDisable() on '{name}' -> reset scheduler + stop routines");

        if (_checkRoutine != null) StopCoroutine(_checkRoutine);
        _checkRoutine = null;
        _checkScheduled = false;

        _isChecking = false;

        if (_trapRoutine != null) StopCoroutine(_trapRoutine);
        _trapRoutine = null;
        _isTriggeringTraps = false;

        if (_heartbeatRoutine != null) StopCoroutine(_heartbeatRoutine);
        _heartbeatRoutine = null;
    }

    private IEnumerator Heartbeat()
    {
        while (true)
        {
            if (debug)
            {
                Debug.Log($"[FCC] Heartbeat | instance={(instance ? instance.name : "NULL")} | " +
                          $"this='{name}' | active={isActiveAndEnabled} | scheduled={_checkScheduled} | " +
                          $"SM.current={(SpecialMoveController.current ? "OK" : "NULL")} | timeScale={Time.timeScale}");
            }
            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, heartbeatSeconds));
        }
    }

    private void Update()
    {
        // Quick Test: F9 -> RequestCheck
        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (debug) Debug.Log("[FCC] F9 pressed -> RequestCheck()");
            RequestCheck();
        }
    }

    [ContextMenu("FCC/Ping")]
    public void Ping()
    {
        Debug.Log($"[FCC] PING '{name}' | active={isActiveAndEnabled} | instance={(instance ? instance.name : "NULL")}");
    }

    [ContextMenu("FCC/Force Final_Check")]
    public void ForceFinalCheck()
    {
        Debug.Log($"[FCC] FORCE Final_Check() '{name}'");
        Final_Check();
    }

    #endregion

    #region 2) Robust Scheduler

    private bool _checkScheduled;
    private Coroutine _checkRoutine;

    private bool _isChecking;

    private bool _isTriggeringTraps;
    private Coroutine _trapRoutine;

    public void RequestCheck()
    {
        if (!isActiveAndEnabled)
        {
            if (debug) Debug.Log("[FCC] RequestCheck() ignored: not active/enabled");
            return;
        }

        // Recover falls Coroutines extern gekillt wurden (StopAllCoroutines etc.)
        if (_checkScheduled && _checkRoutine == null)
        {
            if (debug) Debug.Log("[FCC] RequestCheck() RECOVER: scheduled=true but routine=null -> rearm");
            _checkScheduled = false;
        }

        if (_checkScheduled)
        {
            if (debug) Debug.Log("[FCC] RequestCheck() skipped: already scheduled");
            return;
        }

        _checkScheduled = true;

        if (_checkRoutine != null) StopCoroutine(_checkRoutine);
        _checkRoutine = StartCoroutine(CheckAfterDelay());

        if (debug) Debug.Log($"[FCC] RequestCheck() -> scheduled | delay={checkDelay}s");
    }

    private IEnumerator CheckAfterDelay()
    {
        if (debug) Debug.Log($"[FCC] CheckAfterDelay() wait {checkDelay}s realtime");
        yield return new WaitForSecondsRealtime(checkDelay);

        try
        {
            if (debug) Debug.Log("[FCC] CheckAfterDelay() -> Final_Check()");
            Final_Check();
        }
        catch (Exception e)
        {
            Debug.LogError("[FCC] CheckAfterDelay EXCEPTION:\n" + e);
        }
        finally
        {
            _checkScheduled = false;
            _checkRoutine = null;
            if (debug) Debug.Log("[FCC] CheckAfterDelay() cleanup -> scheduled=false");
        }
    }

    #endregion

    #region 3) Public API: Karten setzen / Listen füllen

    public void SetActiveFieldCard(FieldCard newCard)
    {
        if (debug) Debug.Log($"[FCC] SetActiveFieldCard(card={(newCard ? newCard.name : "NULL")})");

        EnsureStatChangeArraySize();
        int slotIndex = FindFirstFreeSlot();
        SetActiveFieldCard_AtIndex(newCard, slotIndex);
    }

    public void SetActiveFieldCard_AtIndex(FieldCard newCard, int slotIndex)
    {
        if (ActiveFieldCard == null || ActiveFieldCard.Length == 0)
        {
            Debug.LogWarning("[FCC] SetActiveFieldCard_AtIndex ABORT: ActiveFieldCard null/leer");
            return;
        }

        EnsureStatChangeArraySize();

        if (slotIndex < 0 || slotIndex >= ActiveFieldCard.Length)
        {
            Debug.LogWarning("[FCC] SetActiveFieldCard_AtIndex ABORT: slotIndex out of range");
            return;
        }

        var previous = ActiveFieldCard[slotIndex];
        if (previous != null &&
            previous.ConditionBlock != null &&
            previous.ConditionBlock.Is_pure_stat_validaton &&
            FieldCard_StatChangeEffect_isActive[slotIndex])
        {
            if (debug) Debug.Log($"[FCC] Reverse previous pureStat (slot {slotIndex}) card={previous.name}");
            PlayFieldEffectSfx();
            PlayReverseForFieldCard(previous, slotIndex);
            PlayMove(previous.ReverseStatChange, DetermineFieldcardSideForMove(previous.ReverseStatChange));
            FieldCard_StatChangeEffect_isActive[slotIndex] = false;
        }

        ActiveFieldCard[slotIndex] = newCard;
        FieldCard_StatChangeEffect_isActive[slotIndex] = false;

        AudioManager.Instance?.PlaySFX2D("Cast_Figure");
        AudioManager.Instance?.PlaySFX2D("Cast_Figure_Punch");

        RefreshFigureList();
        RefreshPlayerList();

        RequestCheck();

        TutorialHintManager.current.Show_DestroyArtifactX(1f);
    }

    public void AddNewTrapCard(FieldCard newCard)
    {
        if (debug) Debug.Log($"[FCC] AddNewTrapCard(card={(newCard ? newCard.name : "NULL")})");
        if (newCard == null) return;
        ActiveTrapCard.Add(newCard);
        RequestCheck();
    }

    public void AddNewFigureFieldCard(FieldCard newCard)
    {
        if (debug) Debug.Log($"[FCC] AddNewFigureFieldCard(card={(newCard ? newCard.name : "NULL")})");
        if (newCard == null) return;
        //ActiveFigureFieldEffect.Add(newCard);
        EnsureFigureFieldEffectListSize();
        RequestCheck();
    }

    private int FindFirstFreeSlot()
    {
        if (ActiveFieldCard == null || ActiveFieldCard.Length == 0) return 0;
        for (int i = 0; i < ActiveFieldCard.Length; i++)
            if (ActiveFieldCard[i] == null) return i;
        return 0;
    }

    #endregion

    #region 4) Figure System (inkl. Trigger API)

    [System.Serializable]
    public sealed class FigureData
    {
        public string Variant;
        public int HP, Load, ATK, DEF, Cost;
        public string Target, Level;
        public bool IsDefending, IsActive;

        public int taken_damage, taken_healing, taken_loading, taken_unloading;
        public string by_figure_variant = "-";

        public bool gets_killed,
                    being_summoned,
                    attacks,
                    defends,
                    makes_specialMove;
    }

    public List<FigureData> Figures = new List<FigureData>()
    {
        new FigureData { Variant = "Heart.Self" },
        new FigureData { Variant = "Spade.Self" },
        new FigureData { Variant = "Club.Self"  },
        new FigureData { Variant = "Heart.Opp"  },
        new FigureData { Variant = "Spade.Opp"  },
        new FigureData { Variant = "Club.Opp"   },
    };

    public void RefreshFigureList()
    {
        var tm = TurnManager.current;
        if (tm == null || Figures == null) return;

        foreach (var f in Figures)
        {
            if (f == null || string.IsNullOrEmpty(f.Variant)) continue;
            var split = f.Variant.Split('.');
            if (split.Length != 2) continue;

            string sign = split[0];
            string player = split[1] == "Self" ? "P1" : "P2";

            f.HP = ReturnFigureIntValue("HP", sign, player);
            f.Load = ReturnFigureIntValue("Load", sign, player);
            f.Cost = ReturnFigureIntValue("Cost", sign, player);
            f.ATK = ReturnFigureIntValue("ATK", sign, player);
            f.DEF = ReturnFigureIntValue("DEF", sign, player);

            f.Target = ReturnFigureStringValue("Target", sign, player);
            f.Level = ReturnFigureStringValue("Level", sign, player);
            f.IsDefending = ReturnFigureBoolValue("IsDefending", sign, player);
            f.IsActive = ReturnFigureBoolValue("IsActive", sign, player);
        }
    }

    private static GameObject[] GetFiguresForPlayer(TurnManager tm, string player) =>
        player == "P1" ? tm.Figures_P1 : tm.Figures_P2;

    public int ReturnFigureIntValue(string type, string figureType, string player)
    {
        var tm = TurnManager.current;
        if (tm == null) return 0;

        var figs = GetFiguresForPlayer(tm, player);
        if (figs == null) return 0;

        foreach (var go in figs)
        {
            if (!TryGetDisplay(go, out var df)) continue;
            if (df.FIGURE_TYPE != figureType) continue;

            return type switch
            {
                "HP" => df.FIGURE_HEALTH,
                "Load" => df.FIGURE_LOAD,
                "ATK" => df.FIGURE_ATK,
                "DEF" => df.FIGURE_DEF,
                "Cost" => df.FIGURE_COST,
                _ => 0
            };
        }
        return 0;
    }

    public string ReturnFigureStringValue(string type, string figureType, string player)
    {
        var tm = TurnManager.current;
        if (tm == null) return "-";

        var figs = GetFiguresForPlayer(tm, player);
        if (figs == null) return "-";

        foreach (var go in figs)
        {
            if (!TryGetDisplay(go, out var df)) continue;
            if (df.FIGURE_TYPE != figureType) continue;

            if (type == "Target")
            {
                var t = df.FIGURE_TARGET ? df.FIGURE_TARGET.GetComponent<Display_Figure>() : null;
                return t && !string.IsNullOrEmpty(t.FIGURE_TYPE) ? t.FIGURE_TYPE : "-";
            }

            if (type == "Level")
                return string.IsNullOrEmpty(df.FIGURE_LEVEL) ? "-" : df.FIGURE_LEVEL;
        }
        return "-";
    }

    public bool ReturnFigureBoolValue(string type, string figureType, string player)
    {
        var tm = TurnManager.current;
        if (tm == null) return false;

        var figs = GetFiguresForPlayer(tm, player);
        if (figs == null) return false;

        foreach (var go in figs)
        {
            if (!TryGetDisplay(go, out var df)) continue;
            if (df.FIGURE_TYPE != figureType) continue;

            return type switch
            {
                "IsDefending" => df.IS_DEFENDING,
                "IsActive" => df.FIGURE_HEALTH > 0,
                _ => false
            };
        }
        return false;
    }

    private enum FigureTakenType { Healing, Damage, Load, Killed, Summoned, Attacks, Defends, Makes_SpecialMove }

    public void Figure_TakenHealing(GameObject figure, int amount) => TriggerFigure(figure, amount, true, FigureTakenType.Healing);
    public void Figure_TakenDamage(GameObject figure, int amount) => TriggerFigure(figure, amount, true, FigureTakenType.Damage);
    public void Figure_TakenLoad(GameObject figure, int amount) => TriggerFigure(figure, amount, true, FigureTakenType.Load);
    public void Figure_GotKilled(GameObject figure, bool result) => TriggerFigure(figure, 1, result, FigureTakenType.Killed);
    public void Figure_GotSummoned(GameObject figure, bool result) => TriggerFigure(figure, 1, result, FigureTakenType.Summoned);
    public void Figure_HasAttacked(GameObject figure, bool result) => TriggerFigure(figure, 1, result, FigureTakenType.Attacks);
    public void Figure_HasDefended(GameObject figure, bool result) => TriggerFigure(figure, 1, result, FigureTakenType.Defends);

    private void TriggerFigure(GameObject figureGO, int amount, bool result, FigureTakenType type)
    {
        if (debug) Debug.Log($"[FCC] TriggerFigure(type={type}, amount={amount}, result={result}) go={(figureGO ? figureGO.name : "NULL")}");

        if (!figureGO) return;
        if (amount == 0 && (type == FigureTakenType.Healing || type == FigureTakenType.Damage || type == FigureTakenType.Load))
            return;

        RefreshFigureList();

        if (!TryGetDisplay(figureGO, out var df)) return;

        var parent = figureGO.transform?.parent;
        if (!parent) return;

        string parentName = parent.name;
        if (parentName != "P1_Figures" && parentName != "P2_Figures") return;

        string sign = df.FIGURE_TYPE;
        if (string.IsNullOrEmpty(sign) || !(sign is "Heart" or "Spade" or "Club")) return;

        string variant = $"{sign}.{(parentName == "P1_Figures" ? "Self" : "Opp")}";
        var fig = Figures?.FirstOrDefault(f => f != null && f.Variant == variant);
        if (fig == null) return;

        switch (type)
        {
            case FigureTakenType.Healing: fig.taken_healing = amount; break;
            case FigureTakenType.Damage: fig.taken_damage = amount; break;
            case FigureTakenType.Load: fig.taken_loading = amount; break;
            case FigureTakenType.Killed: fig.gets_killed = result; break;
            case FigureTakenType.Summoned: fig.being_summoned = result; break;
            case FigureTakenType.Attacks: fig.attacks = result; break;
            case FigureTakenType.Defends: fig.defends = result; break;
            case FigureTakenType.Makes_SpecialMove: fig.makes_specialMove = result; break;
        }

        RequestCheck();
        StartCoroutine(ClearFigureTriggersNextFrame(fig));
    }

    private IEnumerator ClearFigureTriggersNextFrame(FigureData fig)
    {
        yield return null;
        if (fig == null) yield break;

        fig.taken_healing = 0;
        fig.taken_damage = 0;
        fig.taken_loading = 0;

        fig.attacks = false;
        fig.defends = false;
        fig.makes_specialMove = false;

        fig.by_figure_variant = "-";
        fig.gets_killed = false;
        fig.being_summoned = false;
    }

    #endregion

    #region 5) Player System

    [System.Serializable]
    public sealed class PlayerData
    {
        public string player_Variant; // "Self" | "Opponent"
        public int player_HP;
        public int player_Load;
        public int player_Figures_active;
        public int player_Cards_inHand;
        public bool player_hasActive_Fieldcard;

        public int player_taken_damage;
        public int player_taken_healing;
        public int player_taken_loading;
        public int player_taken_unloading;
        public int player_gets_deckcards;
        public bool player_gets_pokercards;
        public bool player_plays_deckcardspell;
        public bool player_gets_turn;

        public string player_by_figure_variant = "-";
    }

    public List<PlayerData> Players = new List<PlayerData>
    {
        new PlayerData { player_Variant = "Self" },
        new PlayerData { player_Variant = "Opponent" },
    };

    private enum PlayerTriggerType { Healing, Damage, Loading, Unloading, Gets_DeckCards, Gets_PokerCards, Plays_DeckSpell, Gets_Turn }

    public void Player_TakenHealing(string playerVariant, int amount) => TriggerPlayer(playerVariant, amount, true, PlayerTriggerType.Healing);
    public void Player_TakenDamage(string playerVariant, int amount) => TriggerPlayer(playerVariant, amount, true, PlayerTriggerType.Damage);
    public void Player_TakenLoading(string playerVariant, int amount) => TriggerPlayer(playerVariant, amount, true, PlayerTriggerType.Loading);
    public void Player_GetsDeckCards(string playerVariant, int n) => TriggerPlayer(playerVariant, n, true, PlayerTriggerType.Gets_DeckCards);
    public void Player_GetsPokerCards(string playerVariant, bool yes) => TriggerPlayer(playerVariant, 1, yes, PlayerTriggerType.Gets_PokerCards);
    public void Player_GetsTurn(string playerVariant, bool yes) => TriggerPlayer(playerVariant, 1, yes, PlayerTriggerType.Gets_Turn);
    public void Player_PlaysDeckSpell(string playerVariant, bool yes) => TriggerPlayer(playerVariant, 1, yes, PlayerTriggerType.Plays_DeckSpell);

    private void TriggerPlayer(string playerVariant, int amount, bool result, PlayerTriggerType type)
    {
        if (debug) Debug.Log($"[FCC] TriggerPlayer(p={playerVariant}, type={type}, amount={amount}, result={result})");

        var p = FindPlayer(playerVariant);
        if (p == null) return;

        RefreshPlayerList();

        switch (type)
        {
            case PlayerTriggerType.Healing: p.player_taken_healing = amount; break;
            case PlayerTriggerType.Damage: p.player_taken_damage = amount; break;
            case PlayerTriggerType.Loading: p.player_taken_loading = amount; break;
            case PlayerTriggerType.Unloading: p.player_taken_unloading = amount; break;
            case PlayerTriggerType.Gets_DeckCards: p.player_gets_deckcards = amount; break;
            case PlayerTriggerType.Gets_PokerCards: p.player_gets_pokercards = result; break;
            case PlayerTriggerType.Plays_DeckSpell: p.player_plays_deckcardspell = result; break;
            case PlayerTriggerType.Gets_Turn: p.player_gets_turn = result; break;
        }

        RequestCheck();
        StartCoroutine(ClearPlayerTriggersNextFrame(p));
    }

    private IEnumerator ClearPlayerTriggersNextFrame(PlayerData p)
    {
        yield return null;
        if (p == null) yield break;

        p.player_taken_healing = 0;
        p.player_taken_damage = 0;
        p.player_taken_loading = 0;
        p.player_taken_unloading = 0;
        p.player_gets_deckcards = 0;
        p.player_gets_pokercards = false;
        p.player_plays_deckcardspell = false;
        p.player_gets_turn = false;
        p.player_by_figure_variant = "-";
    }

    public void RefreshPlayerList()
    {
        if (Players == null) return;

        foreach (var p in Players)
        {
            if (p == null || string.IsNullOrEmpty(p.player_Variant)) continue;
            string side = p.player_Variant == "Self" ? "P1" : "P2";

            var dfPlayer = GetPlayerDF(side);
            p.player_HP = dfPlayer ? dfPlayer.FIGURE_HEALTH : 0;
            p.player_Load = dfPlayer ? dfPlayer.FIGURE_LOAD : 0;

            p.player_Figures_active = ReturnPlayerIntValue("nFigures_Active", side);
            p.player_Cards_inHand = ReturnPlayerIntValue("nCards_In_Hand", side);
            p.player_hasActive_Fieldcard = ReturnPlayerBoolValue("has_Active_Fieldcard", side);
        }
    }

    private Display_Figure GetPlayerDF(string side)
    {
        var p1Base = GameObject.Find("GUI-Canvas-P1/P1-Base");
        var p2Base = GameObject.Find("GUI-Canvas-P2/P2-Base");

        GameObject root = side == "P1" ? p1Base : p2Base;
        if (!root || !root.TryGetComponent(out Display_Figure df)) return null;
        return df;
    }

    public int ReturnPlayerIntValue(string valueType, string side)
    {
        var tm = TurnManager.current;
        if (tm == null) return 0;

        GameObject[] figs = side == "P1" ? tm.Figures_P1 : tm.Figures_P2;

        switch (valueType)
        {
            case "nFigures_Active":
                if (figs == null) return 0;
                return figs.Count(go => go && TryGetDisplay(go, out var df) && df.FIGURE_HEALTH > 0);

            case "nCards_In_Hand":
                var p1Hand = GameObject.Find("GUI-Canvas-P1/P1-Hand");
                var p2Hand = GameObject.Find("GUI-Canvas-P2/P2-Hand");
                int countP1 = p1Hand ? p1Hand.transform.childCount : 0;
                int countP2 = p2Hand ? p2Hand.transform.childCount : 0;
                return side == "P1" ? countP1 : countP2;
        }

        return 0;
    }

    public bool ReturnPlayerBoolValue(string valueType, string side)
    {
        if (valueType == "has_Active_Fieldcard")
        {
            if (ActiveFieldCard == null) return false;
            return ActiveFieldCard.Any(c => c != null);
        }
        return false;
    }

    #endregion

    #region 6) Condition Evaluation

    private bool CompareIntStat(int actual, int target, Condition cond)
    {
        return cond switch
        {
            Condition.is_equal_int     => actual == target,
            Condition.is_greater_than  => actual > target,
            Condition.is_less_than     => actual < target,
            _                          => false
        };
    }

    private string TranslatePlayerVariant(Player_Variant v)
    {
        return v switch
        {
            Player_Variant.Self => "Self",
            Player_Variant.Opponent => "Opponent",
            Player_Variant.Any => "Self",
            _ => "Self"
        };
    }

    public string TranslateFigureVariant(Figure_Variant variant)
    {
        return variant switch
        {
            Figure_Variant.Heart_Self => "Heart.Self",
            Figure_Variant.Spade_Self => "Spade.Self",
            Figure_Variant.Club_Self => "Club.Self",
            Figure_Variant.Heart_Opp => "Heart.Opp",
            Figure_Variant.Spade_Opp => "Spade.Opp",
            Figure_Variant.Club_Opp => "Club.Opp",
            _ => "default"
        };
    }

    private bool CheckFigureItem(ConditionItem item)
    {
        var translated = TranslateFigureVariant(item.Figure_Variant);
        var f = Figures?.FirstOrDefault(x => x != null && x.Variant == translated);
        if (f == null) return false;

        switch (item.Figure_Stat)
        {
            case Figure_Stat.HP: if (!CompareIntStat(f.HP, item.Figure_Value_Int, item.Figure_Condition_Int)) return false; break;
            case Figure_Stat.Load: if (!CompareIntStat(f.Load, item.Figure_Value_Int, item.Figure_Condition_Int)) return false; break;
            case Figure_Stat.ATK: if (!CompareIntStat(f.ATK, item.Figure_Value_Int, item.Figure_Condition_Int)) return false; break;
            case Figure_Stat.DEF: if (!CompareIntStat(f.DEF, item.Figure_Value_Int, item.Figure_Condition_Int)) return false; break;
            case Figure_Stat.Cost: if (!CompareIntStat(f.Cost, item.Figure_Value_Int, item.Figure_Condition_Int)) return false; break;
            case Figure_Stat.Is_Defending: if (f.IsDefending != item.Figure_Value_Bool) return false; break;
            case Figure_Stat.Is_Active: if (f.IsActive != item.Figure_Value_Bool) return false; break;
            case Figure_Stat.Target: if (f.Target != item.Figure_Value_String) return false; break;
            case Figure_Stat.Level: if (f.Level != item.Figure_Value_String) return false; break;
        }

        if (item.Figure_Is_Trigger_Condition)
        {
            switch (item.Figure_Trigger)
            {
                case Figure_Trigger.Takes_Damage: if (!CompareIntStat(f.taken_damage, item.Figure_Trigger_Value_Int, item.Figure_Trigger_Condition_Int)) return false; break;
                case Figure_Trigger.Takes_Healing: if (!CompareIntStat(f.taken_healing, item.Figure_Trigger_Value_Int, item.Figure_Trigger_Condition_Int)) return false; break;
                case Figure_Trigger.Takes_Loading: if (!CompareIntStat(f.taken_loading, item.Figure_Trigger_Value_Int, item.Figure_Trigger_Condition_Int)) return false; break;
                case Figure_Trigger.Gets_Killed: if (f.gets_killed != item.Figure_Value_Bool) return false; break;
                case Figure_Trigger.Being_Summoned: if (f.being_summoned != item.Figure_Value_Bool) return false; break;
                case Figure_Trigger.Attacks: if (f.attacks != item.Figure_Value_Bool) return false; break;
                case Figure_Trigger.Defends: if (f.defends != item.Figure_Value_Bool) return false; break;
                case Figure_Trigger.Makes_SpecialMove: if (f.makes_specialMove != item.Figure_Value_Bool) return false; break;
            }
        }

        return true;
    }

    private bool CheckPlayerItem(ConditionItem item)
    {
        string pv = TranslatePlayerVariant(item.Player_Variant);
        var p = Players?.FirstOrDefault(x => x != null && x.player_Variant == pv);
        if (p == null) return false;

        switch (item.Player_Stat)
        {
            case Player_Stat.HP: if (!CompareIntStat(p.player_HP, item.Player_Integer_Value, item.Player_Condition)) return false; break;
            case Player_Stat.Load: if (!CompareIntStat(p.player_Load, item.Player_Integer_Value, item.Player_Condition)) return false; break;
            case Player_Stat.nFigures_Active: if (!CompareIntStat(p.player_Figures_active, item.Player_Integer_Value, item.Player_Condition)) return false; break;
            case Player_Stat.nCards_In_Hand: if (!CompareIntStat(p.player_Cards_inHand, item.Player_Integer_Value, item.Player_Condition)) return false; break;
            case Player_Stat.has_Active_Fieldcard: if (p.player_hasActive_Fieldcard != item.Player_Bool_Value) return false; break;
            case Player_Stat.gets_Turn: if (p.player_gets_turn != item.Player_Bool_Value) return false; break;
        }

        if (item.Player_Is_Trigger_Condition)
        {
            switch (item.Player_Trigger)
            {
                case Player_Trigger.Takes_Damage: if (!CompareIntStat(p.player_taken_damage, item.Player_Trigger_Integer_Value, item.Player_Trigger_Condition)) return false; break;
                case Player_Trigger.Takes_Healing: if (!CompareIntStat(p.player_taken_healing, item.Player_Trigger_Integer_Value, item.Player_Trigger_Condition)) return false; break;
                case Player_Trigger.Takes_Loading: if (!CompareIntStat(p.player_taken_loading, item.Player_Trigger_Integer_Value, item.Player_Trigger_Condition)) return false; break;
                case Player_Trigger.Takes_Unloading: if (!CompareIntStat(p.player_taken_unloading, item.Player_Trigger_Integer_Value, item.Player_Trigger_Condition)) return false; break;
                case Player_Trigger.GetsN_Cards: if (!CompareIntStat(p.player_gets_deckcards, item.Player_Trigger_Integer_Value, item.Player_Trigger_Condition)) return false; break;
                case Player_Trigger.GetsPokercard: if (p.player_gets_pokercards != item.Player_Bool_Value) return false; break;
                case Player_Trigger.Plays_DeckSpell: if (p.player_plays_deckcardspell != item.Player_Bool_Value) return false; break;
            }
        }

        return true;
    }

    private bool EvaluateGroup(ConditionGroup group)
    {
        if (group == null || group.Items == null || group.Items.Count == 0) return false;

        bool result = (group.InGroupOperator == ConditionGroup.LogicOperator.AND);

        foreach (var item in group.Items)
        {
            bool itemResult =
                item.Main_Object == StatHolderObject.Player ? CheckPlayerItem(item) :
                item.Main_Object == StatHolderObject.Figure ? CheckFigureItem(item) :
                false;

            if (group.InGroupOperator == ConditionGroup.LogicOperator.AND)
            {
                result = result && itemResult;
                if (!result) break;
            }
            else
            {
                result = result || itemResult;
                if (result) break;
            }
        }

        return result;
    }

    public bool EvaluateConditionBlock(ConditionBlock block)
    {
        if (block == null || block.Groups == null || block.Groups.Count == 0) return false;

        var groupResults = block.Groups.Select(EvaluateGroup).ToList();
        if (groupResults.Count == 1) return groupResults[0];

        bool final = groupResults[0];

        var connectors = block.GroupConnectors;
        if (connectors == null || connectors.Count == 0)
        {
            for (int i = 1; i < groupResults.Count; i++) final = final && groupResults[i];
            return final;
        }

        for (int i = 0; i < connectors.Count && (i + 1) < groupResults.Count; i++)
        {
            if (connectors[i] == ConditionBlock.LogicOperator.AND) final = final && groupResults[i + 1];
            else if (connectors[i] == ConditionBlock.LogicOperator.OR) final = final || groupResults[i + 1];
        }

        return final;
    }

    #endregion

    #region 7) Final_Check (mit klaren Logs)

    public void Final_Check()
    {
        if (_isChecking)
        {
            if (debug) Debug.Log("[FCC] Final_Check SKIP: already checking");
            return;
        }

        _isChecking = true;

        try
        {
            if (debug) Debug.Log("[FCC] Final_Check ENTER");

            RefreshFigureList();
            RefreshPlayerList();
            EnsureStatChangeArraySize();
            EnsureFigureFieldEffectListSize();

            int triggered = 0;

            // FIELD SLOTS
            if (ActiveFieldCard != null && FieldCardSlot != null)
            {
                int n = Mathf.Min(ActiveFieldCard.Length, FieldCardSlot.Length);

                for (int i = 0; i < n; i++)
                {
                    var card = ActiveFieldCard[i];
                    if (!card) continue;

                    var holder = FieldCardSlot[i] ? FieldCardSlot[i].GetComponent<FieldCardPlaceHolder>() : null;
                    int life = holder ? holder.current_lifePoints : 999;

                    bool cond = EvaluateConditionBlock(card.ConditionBlock);
                    bool pure = (card.ConditionBlock != null && card.ConditionBlock.Is_pure_stat_validaton);
                    bool statActive = (FieldCard_StatChangeEffect_isActive != null && i < FieldCard_StatChangeEffect_isActive.Length) && FieldCard_StatChangeEffect_isActive[i];

                    if (debug) Debug.Log($"[FCC] Slot[{i}] card={card.name} cond={cond} pure={pure} life={life} statActive={statActive}");

                    if (cond)
                    {
                        if (holder != null && life <= 0) continue;

                        if (pure)
                        {
                            if (!FieldCard_StatChangeEffect_isActive[i])
                            {
                                PlayFieldEffectSfx();
                                PlayFieldCardVfx(card, i);
                                holder?.Destroy_SingleLifePoint();

                                PlayMove(card.StatChange, DetermineFieldcardSideForMove(card.StatChange));
                                FieldCard_StatChangeEffect_isActive[i] = true;
                                triggered++;
                            }
                        }
                        else
                        {
                            PlayFieldEffectSfx();
                            PlayFieldCardVfx(card, i);
                            holder?.Destroy_SingleLifePoint();

                            PlayMove(card.Action, DetermineFieldcardSideForMove(card.Action));
                            triggered++;
                        }
                    }
                    else
                    {
                        if (pure && FieldCard_StatChangeEffect_isActive[i])
                        {
                            PlayFieldEffectSfx();
                            PlayReverseForFieldCard(card, i);
                            PlayMove(card.ReverseStatChange, DetermineFieldcardSideForMove(card.ReverseStatChange));
                            FieldCard_StatChangeEffect_isActive[i] = false;
                            triggered++;
                        }
                    }
                }
            }

            // FIGURE FIELD EFFECTS
            if (ActiveFigureFieldEffect != null)
            {
                for (int i = 0; i < ActiveFigureFieldEffect.Count; i++)
                {
                    var card = ActiveFigureFieldEffect[i];
                    if (!card) continue;

                    bool cond = EvaluateConditionBlock(card.ConditionBlock);
                    bool hasStat = (card.StatChange != null && card.ReverseStatChange != null);
                    bool hasAction = (card.Action != null);
                    bool wasTrue = FigureFieldEffect_lastConditionTrue[i];

                    if (debug) Debug.Log($"[FCC] FigureEffect[{i}] card={card.name} cond={cond} hasStat={hasStat} hasAction={hasAction}");

                    if (cond)
                    {
                        if (hasStat && !FigureFieldEffect_isActive[i])
                        {
                            PlayFieldEffectSfx();
                            PlayMove(card.StatChange, DetermineFieldcardSideForMove(card.StatChange));
                            FigureFieldEffect_isActive[i] = true;
                            triggered++;
                        }

                        if (hasAction && !wasTrue)
                        {
                            PlayFieldEffectSfx();
                            PlayMove(card.Action, DetermineFieldcardSideForMove(card.Action));
                            triggered++;
                        }
                    }
                    else
                    {
                        if (hasStat && FigureFieldEffect_isActive[i])
                        {
                            PlayFieldEffectSfx();
                            PlayMove(card.ReverseStatChange, DetermineFieldcardSideForMove(card.ReverseStatChange));
                            FigureFieldEffect_isActive[i] = false;
                            triggered++;
                        }
                    }

                    FigureFieldEffect_lastConditionTrue[i] = cond;
                }
            }

            // TRAPS
            if (!_isTriggeringTraps)
            {
                if (_trapRoutine != null) StopCoroutine(_trapRoutine);
                _trapRoutine = StartCoroutine(TriggerTrapsSimple());
            }

            if (debug) Debug.Log($"[FCC] Final_Check EXIT triggered={triggered}");
        }
        catch (Exception e)
        {
            Debug.LogError("[FCC] Final_Check EXCEPTION:\n" + e);
        }
        finally
        {
            _isChecking = false;
        }
    }

    private IEnumerator TriggerTrapsSimple()
    {
        _isTriggeringTraps = true;

        try
        {
            if (ActiveTrapCard == null || ActiveTrapCard.Count == 0)
            {
                if (debug) Debug.Log("[FCC] Traps: none");
                yield break;
            }

            // 1) Snapshot der aktuell vorhandenen Traps
            var snapshot = ActiveTrapCard.Where(c => c != null).ToList();
            if (debug) Debug.Log($"[FCC] Traps snapshot={snapshot.Count}");

            // 2) WICHTIG: Alle zu triggernden Traps SOFORT bestimmen (ohne yield),
            // damit Trigger-Werte (taken_healing etc.) nicht im nächsten Frame gelöscht sind.
            var toFire = new List<FieldCard>(snapshot.Count);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var card = snapshot[i];
                if (card == null) continue;

                // Trap wurde evtl. schon entfernt
                if (!ActiveTrapCard.Contains(card)) continue;

                bool cond = EvaluateConditionBlock(card.ConditionBlock);
                if (debug) Debug.Log($"[FCC] Trap precheck card={card.name} cond={cond}");

                if (cond) toFire.Add(card);
            }

            if (toFire.Count == 0)
            {
                if (debug) Debug.Log("[FCC] Traps: no matches this check");
                yield break;
            }

            // 3) Jetzt feuern wir alle, die in diesem Check matchen.
            // Optional: kleine Delay pro Trap für VFX – aber Cond ist schon "eingefroren".
            for (int i = 0; i < toFire.Count; i++)
            {
                var card = toFire[i];
                if (card == null) continue;

                // Falls zwischenzeitlich entfernt:
                if (!ActiveTrapCard.Contains(card)) continue;

                if (debug) Debug.Log($"[FCC] Trap FIRE card={card.name}");

                PlayTrapSfxAndVfx(card);
                PlayMove(card.Action, SpecialMoveController.ActiveSide.P1);
                TrapCardHandler.instance?.DestroyTrapCristal("P1");

                ActiveTrapCard.Remove(card);

                // rein kosmetisch, beeinträchtigt das Triggern nicht mehr:
                if (i < toFire.Count - 1)
                    yield return new WaitForSecondsRealtime(0.05f);
            }
        }
        finally
        {
            _isTriggeringTraps = false;
            _trapRoutine = null;
        }
    }


    #endregion

    #region 8) Move / VFX / SFX

    private void PlayMove(SpecialMove move, SpecialMoveController.ActiveSide side)
    {
        if (move == null)
        {
            if (debug) Debug.Log("[FCC] PlayMove ABORT: move NULL");
            return;
        }

        if (SpecialMoveController.current == null)
        {
            Debug.LogError($"[FCC] PlayMove ABORT: SpecialMoveController.current NULL (move={move.name})");
            return;
        }

        if (debug) Debug.Log($"[FCC] PlayMove OK: move={move.name} side={side}");

        SpecialMoveController.current.activeSide = side;
        SpecialMoveController.current.PlaySpecialMove(move);
    }

    private void PlayFieldEffectSfx()
    {
        AudioManager.Instance?.PlaySFX2D("FieldEffect");
        AudioManager.Instance?.PlaySFX2D("FieldEffect_Punch");
    }

    private void PlayTrapSfxAndVfx(FieldCard card)
    {
        AudioManager.Instance?.PlaySFX2D("FieldEffect_Punch");

        ParticleController.Instance?.PlayParticleEffect(
            Vector3.zero,
            card.cristals_circle_particle_index,
            new Vector3(150f, 150f, 150f),
            Quaternion.Euler(-90f, 0f, 0f),
            GameObject.Find("Game-Canvas/CentralEffectPosition")?.transform
        );

        CameraShake.current?.Shake(0.4f, 3f);
    }

    private void PlayFieldCardVfx(FieldCard card, int index)
    {
        if (FieldCardSlot == null || index < 0 || index >= FieldCardSlot.Length) return;
        if (FieldCardSlot[index] == null) return;

        FieldCaster.instance?.PlayParticleAt(FieldCardSlot[index], fieldVfxIndex_Main);
        FieldCaster.instance?.PlayParticleAt(FieldCardSlot[index], card.cristals_load_particle_index);

        ParticleController.Instance?.PlayParticleEffect(
            Vector3.zero,
            card.cristals_circle_particle_index,
            new Vector3(150f, 150f, 150f),
            Quaternion.Euler(-90f, 0f, 0f),
            GameObject.Find("Game-Canvas/CentralEffectPosition")?.transform
        );

        CameraShake.current?.Shake(0.2f, 2f);
    }

    private void PlayReverseForFieldCard(FieldCard card, int index)
    {
        if (FieldCardSlot == null || index < 0 || index >= FieldCardSlot.Length) return;
        if (FieldCardSlot[index] == null) return;

        FieldCaster.instance?.PlayParticleAt(FieldCardSlot[index], reverseVfxIndex);
    }

    private SpecialMoveController.ActiveSide DetermineFieldcardSideForMove(SpecialMove move)
    {
        if (move == null) return SpecialMoveController.ActiveSide.P1;

        if (move.Stats != null && move.Stats.Length > 0)
        {
            var first = move.Stats[0];
            return first.SPECIALMOVE_TARGET_PLAYER switch
            {
                TargetedPlayer.Self => SpecialMoveController.ActiveSide.P1,
                TargetedPlayer.Opponent => SpecialMoveController.ActiveSide.P2,
                _ => SpecialMoveController.ActiveSide.P1
            };
        }

        return SpecialMoveController.ActiveSide.P1;
    }

    #endregion

    #region 9) Helpers

    private static bool TryGetDisplay(GameObject go, out Display_Figure df)
    {
        df = null;
        if (!go) return false;
        return go.TryGetComponent(out df);
    }

    private PlayerData FindPlayer(string playerVariant)
    {
        if (Players == null) return null;
        return Players.FirstOrDefault(x => x != null && x.player_Variant == playerVariant);
    }

    private void EnsureStatChangeArraySize()
    {
        if (ActiveFieldCard == null) return;
        if (FieldCard_StatChangeEffect_isActive == null || FieldCard_StatChangeEffect_isActive.Length != ActiveFieldCard.Length)
            FieldCard_StatChangeEffect_isActive = new bool[ActiveFieldCard.Length];
    }

    private void EnsureFigureFieldEffectListSize()
    {
        if (ActiveFigureFieldEffect == null)
        {
            FigureFieldEffect_isActive.Clear();
            FigureFieldEffect_lastConditionTrue.Clear();
            return;
        }

        while (FigureFieldEffect_isActive.Count < ActiveFigureFieldEffect.Count)
            FigureFieldEffect_isActive.Add(false);

        while (FigureFieldEffect_lastConditionTrue.Count < ActiveFigureFieldEffect.Count)
            FigureFieldEffect_lastConditionTrue.Add(false);

        while (FigureFieldEffect_isActive.Count > ActiveFigureFieldEffect.Count)
            FigureFieldEffect_isActive.RemoveAt(FigureFieldEffect_isActive.Count - 1);

        while (FigureFieldEffect_lastConditionTrue.Count > ActiveFigureFieldEffect.Count)
            FigureFieldEffect_lastConditionTrue.RemoveAt(FigureFieldEffect_lastConditionTrue.Count - 1);
    }

    #endregion
}
