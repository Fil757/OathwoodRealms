using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CustomInspector;

public enum Condition { is_greater_than, is_equal_int, is_less_than }
public enum StatHolderObject { Player, Figure, Fieldcard, Spellcard }

public enum Player_Variant { Self, Opponent, Any }
public enum Figure_Variant { Heart_Self, Spade_Self, Club_Self, Heart_Opp, Spade_Opp, Club_Opp }

public enum Player_Stat { HP, Load, nFigures_Active, nCards_In_Hand, has_Active_Fieldcard, gets_Turn }
public enum Figure_Stat { HP, Load, ATK, DEF, Target, Cost, Level, Is_Defending, Is_Active }
public enum FieldCard_Stat { Is_Active, Lifepoints }
public enum SpellCard_Stat { Cost, Level }
public enum DeckCard_Stat { Cost, Level }

public enum Player_Trigger { Takes_Damage, Takes_Healing, Takes_Loading, Takes_Unloading, GetsN_Cards, GetsPokercard, Plays_DeckSpell }
public enum Figure_Trigger { Takes_Damage, Takes_Healing, Takes_Loading, Takes_Unloading, Gets_Killed, Being_Summoned, Attacks, Defends, Makes_SpecialMove }
public enum FieldCard_Trigger { Being_Summoned, Gets_Destroyed, Makes_his_Spell }
public enum SpellCard_Trigger { Is_being_played }

public enum ByHolderObject { Any, Figure, Fieldcard, Spellcard, Deckcard, Player }

[CreateAssetMenu(fileName = "ConditionItem", menuName = "Scriptable Objects/ConditionItem")]
public class ConditionItem : ScriptableObject
{
    #region META
    [LabelSettings("ID")]
    public string ID;

    [Space(10)]
    [LabelSettings("Description"), TextArea]
    public string DESCRIPTION;

    [Space(20)]
    [LabelSettings("Main Object")]
    public StatHolderObject Main_Object;
    #endregion

    // =====================================================================
    // ================== ZENTRALE KATALOGE / HELFER =======================
    // =====================================================================
    #region Kataloge & Helper

    private static readonly HashSet<Player_Stat> s_PlayerIntStats = new()
    {
        Player_Stat.HP, Player_Stat.Load, Player_Stat.nFigures_Active, Player_Stat.nCards_In_Hand
    };
    private static readonly HashSet<Player_Stat> s_PlayerBoolStats = new()
    {
        Player_Stat.has_Active_Fieldcard, Player_Stat.gets_Turn
    };

    private static readonly HashSet<Figure_Stat> s_FigureIntStats = new()
    {
        Figure_Stat.HP, Figure_Stat.Load, Figure_Stat.ATK, Figure_Stat.DEF, Figure_Stat.Cost
    };
    private static readonly HashSet<Figure_Stat> s_FigureStringStats = new()
    {
        Figure_Stat.Target, Figure_Stat.Level
    };
    private static readonly HashSet<Figure_Stat> s_FigureBoolStats = new()
    {
        Figure_Stat.Is_Defending, Figure_Stat.Is_Active
    };

    private static readonly HashSet<FieldCard_Stat> s_FieldIntStats = new()
    {
        FieldCard_Stat.Lifepoints
    };
    private static readonly HashSet<FieldCard_Stat> s_FieldBoolStats = new()
    {
        FieldCard_Stat.Is_Active
    };

    private static readonly HashSet<SpellCard_Stat> s_SpellIntStats = new()
    {
        SpellCard_Stat.Cost
    };
    private static readonly HashSet<SpellCard_Stat> s_SpellStringStats = new()
    {
        SpellCard_Stat.Level
    };

    private static readonly HashSet<DeckCard_Stat> s_DeckIntStats = new()
    {
        DeckCard_Stat.Cost
    };
    private static readonly HashSet<DeckCard_Stat> s_DeckStringStats = new()
    {
        DeckCard_Stat.Level
    };

    private static readonly HashSet<Figure_Trigger> s_FigureTriggersWithInt = new()
    {
        Figure_Trigger.Takes_Damage, Figure_Trigger.Takes_Healing,
        Figure_Trigger.Takes_Loading, Figure_Trigger.Takes_Unloading
    };
    private static readonly HashSet<Figure_Trigger> s_FigureTriggersWithBool = new()
    {
        Figure_Trigger.Gets_Killed, Figure_Trigger.Being_Summoned,
        Figure_Trigger.Attacks, Figure_Trigger.Defends, Figure_Trigger.Makes_SpecialMove
    };

    private static bool IsOneOf<T>(T v, HashSet<T> set) where T : struct => set.Contains(v);
    private static bool AnyOf<T>(T v, params T[] items) where T : struct => items.Contains(v);

    // Player
    private static bool PlayerStatIsInt(Player_Stat s) => IsOneOf(s, s_PlayerIntStats);
    private static bool PlayerStatIsBool(Player_Stat s) => IsOneOf(s, s_PlayerBoolStats);

    // Figure
    private static bool FigureStatIsInt(Figure_Stat s) => IsOneOf(s, s_FigureIntStats);
    private static bool FigureStatIsString(Figure_Stat s) => IsOneOf(s, s_FigureStringStats);
    private static bool FigureStatIsBool(Figure_Stat s) => IsOneOf(s, s_FigureBoolStats);

    // Field
    private static bool FieldStatIsInt(FieldCard_Stat s) => IsOneOf(s, s_FieldIntStats);
    private static bool FieldStatIsBool(FieldCard_Stat s) => IsOneOf(s, s_FieldBoolStats);

    // Spell
    private static bool SpellStatIsInt(SpellCard_Stat s) => IsOneOf(s, s_SpellIntStats);
    private static bool SpellStatIsString(SpellCard_Stat s) => IsOneOf(s, s_SpellStringStats);

    // Deck
    private static bool DeckStatIsInt(DeckCard_Stat s) => IsOneOf(s, s_DeckIntStats);
    private static bool DeckStatIsString(DeckCard_Stat s) => IsOneOf(s, s_DeckStringStats);

    // Figure Trigger value-kind
    private static bool FigureTriggerNeedsInt(Figure_Trigger t) => IsOneOf(t, s_FigureTriggersWithInt);
    private static bool FigureTriggerNeedsBool(Figure_Trigger t) => IsOneOf(t, s_FigureTriggersWithBool);
    private static bool FigureTriggerNeedsString(Figure_Trigger t) => false; // derzeit keiner

    #endregion

    // =====================================================================
    // ============================== PLAYER ===============================
    // =====================================================================
    #region PLAYER

    [LabelSettings("Player Variant"), ShowIf(nameof(IsPlayerSelected))]
    public Player_Variant Player_Variant;

    [LabelSettings("Player Stat"), ShowIf(nameof(IsPlayerStatSelected))]
    public Player_Stat Player_Stat;

    [LabelSettings("Condition"), ShowIf(nameof(Show_PlayerCondition))]
    public Condition Player_Condition;

    [LabelSettings("Value"), ShowIf(nameof(Show_PlayerCondition))]
    public int Player_Integer_Value;

    [LabelSettings("Bool"), ShowIf(nameof(Show_PlayerBool))]
    public bool Player_Bool_Value;

    [Space(10)]
    [LabelSettings("Is Trigger Condition"), ShowIf(nameof(IsPlayerSelected))]
    public bool Player_Is_Trigger_Condition;

    [LabelSettings("Trigger"), ShowIf(nameof(Show_PlayerTriggerBlock))]
    public Player_Trigger Player_Trigger;

    [LabelSettings("Condition"), ShowIf(nameof(Show_PlayerTriggerBlock))]
    public Condition Player_Trigger_Condition;

    [LabelSettings("Value"), ShowIf(nameof(Show_PlayerTriggerBlock))]
    public int Player_Trigger_Integer_Value;

    [Space(10)]
    [LabelSettings("Caused by"), ShowIf(nameof(Show_PlayerTriggerBlock))]
    public ByHolderObject Player_Trigger_ByHolder;

    [LabelSettings("Figure Variant"), ShowIf(nameof(IsPlayerTriggerByFigure))]
    public Figure_Variant Player_Trigger_Figure_Variant;

    [LabelSettings("Fieldcard Variant"), ShowIf(nameof(IsPlayerTriggerByFieldcard))]
    public Player_Variant Player_Trigger_Fieldcard_Variant;

    [LabelSettings("Spellcard Variant"), ShowIf(nameof(IsPlayerTriggerBySpellcard))]
    public Player_Variant Player_Trigger_Spellcard_Variant;

    [LabelSettings("Figure Stat"), ShowIf(nameof(IsPlayerTriggerByFigure))]
    public Figure_Stat Player_Trigger_Figure_Stat;

    [LabelSettings("Spellcard Stat"), ShowIf(nameof(IsPlayerTriggerBySpellcard))]
    public SpellCard_Stat Player_Trigger_Spellcard_Stat;

    [LabelSettings("Fieldcard Stat"), ShowIf(nameof(IsPlayerTriggerByFieldcard))]
    public FieldCard_Stat Player_Trigger_Fieldcard_Stat;

    [LabelSettings("Condition"), ShowIf(nameof(IsPlayerTriggerFigureIntStat))]
    public Condition Player_Trigger_Figure_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsPlayerTriggerFigureIntStat))]
    public int Player_Trigger_Figure_Stat_Value;

    [LabelSettings("Word"), ShowIf(nameof(IsPlayerTriggerFigureStringStat))]
    public string Player_Trigger_Figure_Stat_Word;

    [LabelSettings("Bool"), ShowIf(nameof(IsPlayerTriggerFigureBoolStat))]
    public bool Player_Trigger_Figure_Stat_Bool;

    private bool IsPlayerSelected() => Main_Object == StatHolderObject.Player;
    private bool IsPlayerStatSelected() => IsPlayerSelected() && Player_Variant != Player_Variant.Any;
    private bool Show_PlayerBool() => IsPlayerSelected() && PlayerStatIsBool(Player_Stat);
    private bool Show_PlayerCondition() => IsPlayerSelected() && IsPlayerStatSelected() && PlayerStatIsInt(Player_Stat);
    private bool Show_PlayerTriggerBlock() => IsPlayerSelected() && Player_Is_Trigger_Condition;

    private bool IsPlayerTriggerByFigure() => Show_PlayerTriggerBlock() && Player_Trigger_ByHolder == ByHolderObject.Figure;
    private bool IsPlayerTriggerByFieldcard() => Show_PlayerTriggerBlock() && Player_Trigger_ByHolder == ByHolderObject.Fieldcard;
    private bool IsPlayerTriggerBySpellcard() => Show_PlayerTriggerBlock() && Player_Trigger_ByHolder == ByHolderObject.Spellcard;

    private bool IsPlayerTriggerFigureIntStat() => IsPlayerTriggerByFigure() && FigureStatIsInt(Player_Trigger_Figure_Stat);
    private bool IsPlayerTriggerFigureStringStat() => IsPlayerTriggerByFigure() && FigureStatIsString(Player_Trigger_Figure_Stat);
    private bool IsPlayerTriggerFigureBoolStat() => IsPlayerTriggerByFigure() && FigureStatIsBool(Player_Trigger_Figure_Stat);

    #endregion

    // =====================================================================
    // ============================== FIGURE ===============================
    // =====================================================================
    #region FIGURE

    [LabelSettings("Figure Variant"), ShowIf(nameof(IsFigureSelected))]
    public Figure_Variant Figure_Variant;

    [LabelSettings("Figure Stat"), ShowIf(nameof(IsFigureStatSelected))]
    public Figure_Stat Figure_Stat;

    [LabelSettings("Condition"), ShowIf(nameof(Show_Figure_Int))]
    public Condition Figure_Condition_Int;

    [LabelSettings("Value"), ShowIf(nameof(Show_Figure_Int))]
    public int Figure_Value_Int;

    [LabelSettings("Word"), ShowIf(nameof(Show_Figure_String))]
    public string Figure_Value_String;

    [LabelSettings("Bool"), ShowIf(nameof(Show_Figure_Bool))]
    public bool Figure_Value_Bool;

    [Space(10)]
    [LabelSettings("Is Trigger Condition"), ShowIf(nameof(IsFigureSelected))]
    public bool Figure_Is_Trigger_Condition;

    [LabelSettings("Trigger"), ShowIf(nameof(Show_FigureTriggerBlock))]
    public Figure_Trigger Figure_Trigger;

    [LabelSettings("Condition"), ShowIf(nameof(Show_FigureTrigger_Int))]
    public Condition Figure_Trigger_Condition_Int;

    [LabelSettings("Value"), ShowIf(nameof(Show_FigureTrigger_Int))]
    public int Figure_Trigger_Value_Int;

    [LabelSettings("Word"), ShowIf(nameof(Show_FigureTrigger_String))]
    public string Figure_Trigger_Value_String;

    [LabelSettings("Bool"), ShowIf(nameof(Show_FigureTrigger_Bool))]
    public bool Figure_Trigger_Value_Bool;

    [Space(10)]
    [LabelSettings("Caused by"), ShowIf(nameof(Show_FigureTriggerBlock))]
    public ByHolderObject Figure_Trigger_ByHolder;

    [LabelSettings("Figure Variant"), ShowIf(nameof(IsFigureTriggerByFigure))]
    public Figure_Variant Figure_Trigger_Figure_Variant;

    [LabelSettings("SpellCard Variant"), ShowIf(nameof(IsFigureTriggerBySpell))]
    public Player_Variant Figure_Trigger_Spell_Variant;

    [LabelSettings("FieldCard Variant"), ShowIf(nameof(IsFigureTriggerByField))]
    public Player_Variant Figure_Trigger_Field_Variant;

    [LabelSettings("Figure Stat"), ShowIf(nameof(IsFigureTriggerByFigure))]
    public Figure_Stat Figure_Trigger_Figure_Stat;

    [LabelSettings("SpellCard Stat"), ShowIf(nameof(IsFigureTriggerBySpell))]
    public SpellCard_Stat Figure_Trigger_Spell_Stat;

    [LabelSettings("FieldCard Stat"), ShowIf(nameof(IsFigureTriggerByField))]
    public FieldCard_Stat Figure_Trigger_Field_Stat;

    [LabelSettings("Condition"), ShowIf(nameof(IsFigTrigFigure_Int))]
    public Condition Figure_Trigger_Figure_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsFigTrigFigure_Int))]
    public int Figure_Trigger_Figure_Stat_Value_Int;

    [LabelSettings("Word"), ShowIf(nameof(IsFigTrigFigure_String))]
    public string Figure_Trigger_Figure_Stat_Value_String;

    [LabelSettings("Bool"), ShowIf(nameof(IsFigTrigFigure_Bool))]
    public bool Figure_Trigger_Figure_Stat_Value_Bool;

    [LabelSettings("Condition"), ShowIf(nameof(IsFigureTriggerBySpell))]
    public Condition Figure_Trigger_Spell_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsFigureTriggerBySpell))]
    public int Figure_Trigger_Spell_Stat_Value_Int;

    [LabelSettings("Condition"), ShowIf(nameof(IsFigureTriggerByField))]
    public Condition Figure_Trigger_Field_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsFigTrigField_Int))]
    public int Figure_Trigger_Field_Stat_Value_Int;

    [LabelSettings("Bool"), ShowIf(nameof(IsFigTrigField_Bool))]
    public bool Figure_Trigger_Field_Stat_Value_Bool;

    private bool IsFigureSelected() => Main_Object == StatHolderObject.Figure;
    private bool IsFigureStatSelected() => IsFigureSelected();

    private bool Show_Figure_Int() => IsFigureStatSelected() && FigureStatIsInt(Figure_Stat);
    private bool Show_Figure_String() => IsFigureStatSelected() && FigureStatIsString(Figure_Stat);
    private bool Show_Figure_Bool() => IsFigureStatSelected() && FigureStatIsBool(Figure_Stat);

    private bool Show_FigureTriggerBlock() => IsFigureSelected() && Figure_Is_Trigger_Condition;

    private bool Show_FigureTrigger_Int() => Show_FigureTriggerBlock() && FigureTriggerNeedsInt(Figure_Trigger);
    private bool Show_FigureTrigger_String() => Show_FigureTriggerBlock() && FigureTriggerNeedsString(Figure_Trigger);
    private bool Show_FigureTrigger_Bool() => Show_FigureTriggerBlock() && FigureTriggerNeedsBool(Figure_Trigger);

    private bool IsFigureTriggerByFigure() => Show_FigureTriggerBlock() && Figure_Trigger_ByHolder == ByHolderObject.Figure;
    private bool IsFigureTriggerBySpell() => Show_FigureTriggerBlock() && Figure_Trigger_ByHolder == ByHolderObject.Spellcard;
    private bool IsFigureTriggerByField() => Show_FigureTriggerBlock() && Figure_Trigger_ByHolder == ByHolderObject.Fieldcard;

    private bool IsFigTrigFigure_Int() => IsFigureTriggerByFigure() && FigureStatIsInt(Figure_Trigger_Figure_Stat);
    private bool IsFigTrigFigure_String() => IsFigureTriggerByFigure() && FigureStatIsString(Figure_Trigger_Figure_Stat);
    private bool IsFigTrigFigure_Bool() => IsFigureTriggerByFigure() && FigureStatIsBool(Figure_Trigger_Figure_Stat);

    private bool IsFigTrigField_Int() => IsFigureTriggerByField() && (Figure_Trigger_Field_Stat == FieldCard_Stat.Lifepoints);
    private bool IsFigTrigField_Bool() => IsFigureTriggerByField() && (Figure_Trigger_Field_Stat == FieldCard_Stat.Is_Active);

    #endregion

    // =====================================================================
    // ============================ FIELDCARD ==============================
    // =====================================================================
    #region FIELDCARD

    [LabelSettings("Fieldcard Variant"), ShowIf(nameof(IsFieldSelected))]
    public Player_Variant Field_Variant;

    [LabelSettings("Fieldcard Stat"), ShowIf(nameof(IsFieldStatSelected))]
    public FieldCard_Stat Field_Stat;

    // Top: Stat-Werte
    [LabelSettings("Condition"), ShowIf(nameof(Show_Field_Int))]
    public Condition Field_Condition_Int;

    [LabelSettings("Value"), ShowIf(nameof(Show_Field_Int))]
    public int Field_Value_Int;

    [LabelSettings("Bool"), ShowIf(nameof(Show_Field_Bool))]
    public bool Field_Value_Bool;

    // Trigger
    [Space(10)]
    [LabelSettings("Is Trigger Condition"), ShowIf(nameof(IsFieldSelected))]
    public bool Field_Is_Trigger_Condition;

    [LabelSettings("Trigger"), ShowIf(nameof(Show_FieldTriggerBlock))]
    public FieldCard_Trigger Field_Trigger;

    // Fieldcard-Trigger sind Ereignisse → Bool (true/false)
    [LabelSettings("Bool"), ShowIf(nameof(Show_FieldTriggerBlock))]
    public bool Field_Trigger_Value_Bool;

    // Caused by
    [Space(10)]
    [LabelSettings("Caused by"), ShowIf(nameof(Show_FieldTriggerBlock))]
    public ByHolderObject Field_Trigger_ByHolder;

    // Varianten (nur wo sinnvoll)
    [LabelSettings("Figure Variant"), ShowIf(nameof(IsFieldTrigByFigure))]
    public Figure_Variant Field_Trigger_Figure_Variant;

    [LabelSettings("Fieldcard Variant"), ShowIf(nameof(IsFieldTrigByField))]
    public Player_Variant Field_Trigger_Field_Variant;

    [LabelSettings("Spellcard Variant"), ShowIf(nameof(IsFieldTrigBySpell))]
    public Player_Variant Field_Trigger_Spell_Variant;

    // Stat-Auswahl je Verursacher
    [LabelSettings("Figure Stat"), ShowIf(nameof(IsFieldTrigByFigure))]
    public Figure_Stat Field_Trigger_Figure_Stat;

    [LabelSettings("Fieldcard Stat"), ShowIf(nameof(IsFieldTrigByField))]
    public FieldCard_Stat Field_Trigger_Field_Stat;

    [LabelSettings("Spellcard Stat"), ShowIf(nameof(IsFieldTrigBySpell))]
    public SpellCard_Stat Field_Trigger_Spell_Stat;

    [LabelSettings("Deckcard Stat"), ShowIf(nameof(IsFieldTrigByDeck))]
    public DeckCard_Stat Field_Trigger_Deck_Stat;

    // Werte je nach Stat-Typ (Figure-caused)
    [LabelSettings("Condition"), ShowIf(nameof(IsFieldTrigFigure_Int))]
    public Condition Field_Trigger_Figure_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsFieldTrigFigure_Int))]
    public int Field_Trigger_Figure_Stat_Value_Int;

    [LabelSettings("Word"), ShowIf(nameof(IsFieldTrigFigure_String))]
    public string Field_Trigger_Figure_Stat_Value_String;

    [LabelSettings("Bool"), ShowIf(nameof(IsFieldTrigFigure_Bool))]
    public bool Field_Trigger_Figure_Stat_Value_Bool;

    // Werte (Field-caused)
    [LabelSettings("Condition"), ShowIf(nameof(IsFieldTrigField_Int))]
    public Condition Field_Trigger_Field_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsFieldTrigField_Int))]
    public int Field_Trigger_Field_Stat_Value_Int;

    [LabelSettings("Bool"), ShowIf(nameof(IsFieldTrigField_Bool))]
    public bool Field_Trigger_Field_Stat_Value_Bool;

    // Werte (Spell-caused)
    [LabelSettings("Condition"), ShowIf(nameof(IsFieldTrigSpell_Int))]
    public Condition Field_Trigger_Spell_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsFieldTrigSpell_Int))]
    public int Field_Trigger_Spell_Stat_Value_Int;

    [LabelSettings("Word"), ShowIf(nameof(IsFieldTrigSpell_String))]
    public string Field_Trigger_Spell_Stat_Value_String;

    // Werte (Deck-caused)
    [LabelSettings("Condition"), ShowIf(nameof(IsFieldTrigDeck_Int))]
    public Condition Field_Trigger_Deck_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsFieldTrigDeck_Int))]
    public int Field_Trigger_Deck_Stat_Value_Int;

    [LabelSettings("Word"), ShowIf(nameof(IsFieldTrigDeck_String))]
    public string Field_Trigger_Deck_Stat_Value_String;

    // ---------- Predicates ----------
    private bool IsFieldSelected() => Main_Object == StatHolderObject.Fieldcard;
    private bool IsFieldStatSelected() => IsFieldSelected();

    private bool Show_Field_Int() => IsFieldStatSelected() && FieldStatIsInt(Field_Stat);
    private bool Show_Field_Bool() => IsFieldStatSelected() && FieldStatIsBool(Field_Stat);

    private bool Show_FieldTriggerBlock() => IsFieldSelected() && Field_Is_Trigger_Condition;

    private bool IsFieldTrigByFigure() => Show_FieldTriggerBlock() && Field_Trigger_ByHolder == ByHolderObject.Figure;
    private bool IsFieldTrigByField() => Show_FieldTriggerBlock() && Field_Trigger_ByHolder == ByHolderObject.Fieldcard;
    private bool IsFieldTrigBySpell() => Show_FieldTriggerBlock() && Field_Trigger_ByHolder == ByHolderObject.Spellcard;
    private bool IsFieldTrigByDeck() => Show_FieldTriggerBlock() && Field_Trigger_ByHolder == ByHolderObject.Deckcard;

    private bool IsFieldTrigFigure_Int() => IsFieldTrigByFigure() && FigureStatIsInt(Field_Trigger_Figure_Stat);
    private bool IsFieldTrigFigure_String() => IsFieldTrigByFigure() && FigureStatIsString(Field_Trigger_Figure_Stat);
    private bool IsFieldTrigFigure_Bool() => IsFieldTrigByFigure() && FigureStatIsBool(Field_Trigger_Figure_Stat);

    private bool IsFieldTrigField_Int() => IsFieldTrigByField() && FieldStatIsInt(Field_Trigger_Field_Stat);
    private bool IsFieldTrigField_Bool() => IsFieldTrigByField() && FieldStatIsBool(Field_Trigger_Field_Stat);

    private bool IsFieldTrigSpell_Int() => IsFieldTrigBySpell() && SpellStatIsInt(Field_Trigger_Spell_Stat);
    private bool IsFieldTrigSpell_String() => IsFieldTrigBySpell() && SpellStatIsString(Field_Trigger_Spell_Stat);

    private bool IsFieldTrigDeck_Int() => IsFieldTrigByDeck() && DeckStatIsInt(Field_Trigger_Deck_Stat);
    private bool IsFieldTrigDeck_String() => IsFieldTrigByDeck() && DeckStatIsString(Field_Trigger_Deck_Stat);

    #endregion

    // =====================================================================
    // ============================ SPELLCARD ==============================
    // =====================================================================
    #region SPELLCARD

    [LabelSettings("Spellcard Variant"), ShowIf(nameof(IsSpellSelected))]
    public Player_Variant Spell_Variant;

    [LabelSettings("Spellcard Stat"), ShowIf(nameof(IsSpellStatSelected))]
    public SpellCard_Stat Spell_Stat;

    // Top-Stat
    [LabelSettings("Condition"), ShowIf(nameof(Show_Spell_Int))]
    public Condition Spell_Condition_Int;

    [LabelSettings("Value"), ShowIf(nameof(Show_Spell_Int))]
    public int Spell_Value_Int;

    // Level = String (keine numerische Bedingung)
    [LabelSettings("Word"), ShowIf(nameof(Show_Spell_String))]
    public string Spell_Value_String;

    // Trigger
    [Space(10)]
    [LabelSettings("Is Trigger Condition"), ShowIf(nameof(IsSpellSelected))]
    public bool Spell_Is_Trigger_Condition;

    [LabelSettings("Trigger"), ShowIf(nameof(Show_SpellTriggerBlock))]
    public SpellCard_Trigger Spell_Trigger;

    [LabelSettings("Bool"), ShowIf(nameof(Show_SpellTriggerBlock))]
    public bool Spell_Trigger_Value_Bool;

    // Caused by: Player (entsprechend deiner Grafik)
    [Space(10)]
    [LabelSettings("Caused by"), ShowIf(nameof(Show_SpellTriggerBlock))]
    public ByHolderObject Spell_Trigger_ByHolder = ByHolderObject.Player;

    [LabelSettings("Player Variant"), ShowIf(nameof(IsSpellTrigByPlayer))]
    public Player_Variant Spell_Trigger_Player_Variant;

    [LabelSettings("Player Stat"), ShowIf(nameof(IsSpellTrigByPlayer))]
    public Player_Stat Spell_Trigger_Player_Stat;

    [LabelSettings("Condition"), ShowIf(nameof(IsSpellTrigPlayer_Int))]
    public Condition Spell_Trigger_Player_Stat_Condition;

    [LabelSettings("Value"), ShowIf(nameof(IsSpellTrigPlayer_Int))]
    public int Spell_Trigger_Player_Stat_Value_Int;

    [LabelSettings("Bool"), ShowIf(nameof(IsSpellTrigPlayer_Bool))]
    public bool Spell_Trigger_Player_Stat_Value_Bool;

    // ---------- Predicates ----------
    private bool IsSpellSelected() => Main_Object == StatHolderObject.Spellcard;
    private bool IsSpellStatSelected() => IsSpellSelected();

    private bool Show_Spell_Int() => IsSpellStatSelected() && SpellStatIsInt(Spell_Stat);
    private bool Show_Spell_String() => IsSpellStatSelected() && SpellStatIsString(Spell_Stat);

    private bool Show_SpellTriggerBlock() => IsSpellSelected() && Spell_Is_Trigger_Condition;

    private bool IsSpellTrigByPlayer() => Show_SpellTriggerBlock() && Spell_Trigger_ByHolder == ByHolderObject.Player;

    private bool IsSpellTrigPlayer_Int() => IsSpellTrigByPlayer() && PlayerStatIsInt(Spell_Trigger_Player_Stat);
    private bool IsSpellTrigPlayer_Bool() => IsSpellTrigByPlayer() && PlayerStatIsBool(Spell_Trigger_Player_Stat);

    #endregion
}
