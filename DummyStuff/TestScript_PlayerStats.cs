using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript_PlayerStats : MonoBehaviour
{
    public GameObject Player1;
    public GameObject ZeroPoint;

    public void DamageToP1Test()
    {
        //DamageToFigure.current.ApplyDamage(Player1, 100);
    }

    public void HealingToP1Test()
    {
        //HealToFigure.current.HealingToFigure(Player1, 100);
    }

    public void LoadToP1Test()
    {
        //LoadToFigure.current.LoadingToFigure(Player1, 10);
    }

    public void LoadToFigureTestFigure()
    {
        LoadToFigure.current.LoadingToFigure(TurnManager.current.current_figure_P1, 10);
        //LoadToFigure.current.LoadingToFigure(TurnManager.current.current_figure_P2, 10);
    }

    public void HealToFigureTestFigure()
    {
        HealToFigure.current.HealingToFigure(TurnManager.current.current_figure_P1, 10);
        HealToFigure.current.HealingToFigure(TurnManager.current.current_figure_P2, 10);
    }

    public void DamageToFigureTestFigure()
    {
        DamageToFigure.current.ApplyDamage(TurnManager.current.current_figure_P1, 10);
        DamageToFigure.current.ApplyDamage(TurnManager.current.current_figure_P2, 10);
    }

    public void NewPopUpTest_Heal()
    {
        PopUp.current.CreatePopUp(
            "Healing",
            25,
            ZeroPoint,                      // popUpRoot
            new Vector3(0, -30f, -50f),           // popUpOffsetPos
            new Vector3(-90f, 0, 0),           // popUpOffsetRot
            "UI"                            // popUpLayer (Unity-Layer-Name)
        );
    }

    public void NewPopUpTest_Load()
    {
        PopUp.current.CreatePopUp(
            "Loading",
            25,
            ZeroPoint,                      // popUpRoot
            new Vector3(0, -30f, -50f),           // popUpOffsetPos
            new Vector3(-90f, 0, 0),           // popUpOffsetRot
            "UI"                            // popUpLayer (Unity-Layer-Name)
        );
    }

    public void NewPopUpTest_Damage()
    {
        PopUp.current.CreatePopUp(
            "Damage",
            25,
            ZeroPoint,                      // popUpRoot
            new Vector3(0, -30f, -50f),           // popUpOffsetPos
            new Vector3(-90f, 0, 0),           // popUpOffsetRot
            "UI"                            // popUpLayer (Unity-Layer-Name)
        );
    }
}
