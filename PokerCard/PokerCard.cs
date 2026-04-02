using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
[System.Serializable]

public class PokerCard
{
    public int poker_id;
    public string poker_sign;
    public int value;
    public Sprite spriteImage;

    public PokerCard()
    {
        
    }


    public PokerCard(int Poker_Id, string Poker_Sign, int Value, Sprite SpriteImage)
    {
        poker_id = Poker_Id;
        poker_sign = Poker_Sign;
        value = Value;
        spriteImage = SpriteImage;
        
    }
}
