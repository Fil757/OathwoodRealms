using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CustomInspector;

public enum ResponseType { Action, StatChange }

[CreateAssetMenu(fileName = "FieldCard", menuName = "Scriptable Objects/FieldCard")]
public class FieldCard : ScriptableObject
{
    [LabelSettings("ID")]
    public string ID;

    [LabelSettings("Name")]
    public string Name;

    [LabelSettings("Cost")]
    public int Cost;

    [LabelSettings("Description"), TextArea]
    public string Description;

    [LabelSettings("Is used as a Trapcard")]
    public bool used_as_trap;

    [LabelSettings("Connected to Figure ID")]
    public string connection_to_figure_ID;

    [Space(25)]

    public ConditionBlock ConditionBlock;

    public ResponseType Response_Type;

    [LabelSettings("Action"), ShowIf(nameof(IsActionSelected))]
    public SpecialMove Action;

    [LabelSettings("Stat Change"), ShowIf(nameof(IsStatChangeSelected))]
    public SpecialMove StatChange;

    [LabelSettings("Reverse Change"), ShowIf(nameof(IsStatChangeSelected))]
    public SpecialMove ReverseStatChange;

    private bool IsActionSelected() => Response_Type == ResponseType.Action;
    private bool IsStatChangeSelected() => Response_Type == ResponseType.StatChange;

    [Space(25)]

    [LabelSettings("Cristal Object")]
    public GameObject cristal_prefab;
    public int cristals_load_particle_index;
    public int cristals_circle_particle_index;

    [LabelSettings("Artwork")]
    public Sprite artwork;
}
