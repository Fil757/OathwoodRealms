using System.Collections.Generic;
using UnityEngine;
using CustomInspector;

[System.Serializable]
public class ConditionGroup
{
    public enum LogicOperator { AND, OR }

    [Tooltip("Wie sollen die Items IN dieser Gruppe logisch verknüpft werden? " +
             "AND = alle müssen erfüllt sein, OR = mindestens eines reicht. " +
             "Nur zur Dokumentation, keine Logik hier.")]
    public LogicOperator InGroupOperator = LogicOperator.AND;

    [Tooltip("Die einzelnen Bedingungen dieser Gruppe. (Früher: item_a1 ... item_a6 usw.)")]
    public List<ConditionItem> Items = new List<ConditionItem>();
}

[CreateAssetMenu(fileName = "ConditionBlock", menuName = "Conditions/Condition Block", order = 0)]
public class ConditionBlock : ScriptableObject
{
    public enum LogicOperator { AND, OR }
    public bool Is_pure_stat_validaton;

    [Tooltip("Alle Gruppen (entspricht deinen logischen Blöcken A, B, C, D). " +
             "Jede Gruppe enthält mehrere ConditionItems.")]
    public List<ConditionGroup> Groups = new List<ConditionGroup>();

    [Tooltip("Verknüpfungen zwischen den Gruppen. " +
             "GroupConnectors[i] beschreibt, wie Groups[i] mit Groups[i+1] verknüpft werden soll. " +
             "Nur als Datenhalter, keine Auswertung hier.")]
    public List<LogicOperator> GroupConnectors = new List<LogicOperator>();
}
