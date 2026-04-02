using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Figure", menuName = "Figure")]
public class Figure : ScriptableObject
{
    public string FIGURE_ID;
    public string FIGURE_NAME;
    public string FIGURE_TYPE;
    public string FIGURE_LEVEL;

    [Header("3D Darstellung")]
    public GameObject FIGURE_PREFAB;

    [Header("Basiswerte")]
    public int FIGURE_ATK;
    public int FIGURE_DEF;
    public int FIGURE_COST;
    public int FIGURE_COST_SPC;
    public int FIGURE_HEALTH;
    public int FIGURE_LOAD;
    public int FIGURE_COST_CAST;

    [Header("Special Moves")]
    public SpecialMove SPECIAL_A;
    public SpecialMove SPECIAL_B;
    public FieldCard Figure_FieldEffect;

    [Header("Portrait Stati")]
    public Sprite FigurePortraitSprite;
}