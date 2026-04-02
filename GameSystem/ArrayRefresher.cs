using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrayRefresher : MonoBehaviour
{
    public static ArrayRefresher RefInstance;

    void Awake()
    {
        RefInstance = this;
    }

    public void RefreshFigureArrays()
    {
        SpecialMoveController.current.RefreshFiguresFromScene_SM();
        AttackController.current.RefreshFiguresFromScene_ATK();
        TurnManager.current.RefreshFiguresFromScene();
        PokerCard_Animation.current.RefreshFiguresFromScene_PC();
    }
}
