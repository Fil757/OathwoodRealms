using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FigureStat : MonoBehaviour
{
    public string typeOfFigureHolder;

    [Header("Bars")]
    public Image HealthBar;
    public Image LoadBar;

    [Header("Portrait")]
    public Image FigurePortraitImage;
    public Image SignImage;

    [Header("UI-Text")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI loadText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI specialcostText;
    public TextMeshProUGUI tierText;

}
