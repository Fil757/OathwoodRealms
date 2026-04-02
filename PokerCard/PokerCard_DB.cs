using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokerCard_DB : MonoBehaviour
{
    public static List<PokerCard> cardList = new List<PokerCard>();


    void Awake()
    {

        // Herz-Karten
        cardList.Add(new PokerCard(1, "Heart", 2, Resources.Load<Sprite>("hearts_Two")));
        cardList.Add(new PokerCard(2, "Heart", 3, Resources.Load<Sprite>("hearts_Three")));
        cardList.Add(new PokerCard(3, "Heart", 4, Resources.Load<Sprite>("hearts_Four")));
        cardList.Add(new PokerCard(4, "Heart", 5, Resources.Load<Sprite>("hearts_Five")));
        cardList.Add(new PokerCard(5, "Heart", 6, Resources.Load<Sprite>("hearts_Six")));
        cardList.Add(new PokerCard(6, "Heart", 7, Resources.Load<Sprite>("hearts_Seven")));
        cardList.Add(new PokerCard(7, "Heart", 8, Resources.Load<Sprite>("hearts_Eight")));
        cardList.Add(new PokerCard(8, "Heart", 9, Resources.Load<Sprite>("hearts_Nine")));
        cardList.Add(new PokerCard(9, "Heart", 10, Resources.Load<Sprite>("hearts_Ten")));
        cardList.Add(new PokerCard(10, "Heart", 11, Resources.Load<Sprite>("hearts_Jack")));
        cardList.Add(new PokerCard(11, "Heart", 12, Resources.Load<Sprite>("hearts_Queen")));
        cardList.Add(new PokerCard(12, "Heart", 13, Resources.Load<Sprite>("hearts_King")));
        cardList.Add(new PokerCard(13, "Heart", 14, Resources.Load<Sprite>("hearts_Ace")));

        // Kreuz-Karten
        cardList.Add(new PokerCard(14, "Club", 2, Resources.Load<Sprite>("clubs_Two")));
        cardList.Add(new PokerCard(15, "Club", 3, Resources.Load<Sprite>("clubs_Three")));
        cardList.Add(new PokerCard(16, "Club", 4, Resources.Load<Sprite>("clubs_Four")));
        cardList.Add(new PokerCard(17, "Club", 5, Resources.Load<Sprite>("clubs_Five")));
        cardList.Add(new PokerCard(18, "Club", 6, Resources.Load<Sprite>("clubs_Six")));
        cardList.Add(new PokerCard(19, "Club", 7, Resources.Load<Sprite>("clubs_Seven")));
        cardList.Add(new PokerCard(20, "Club", 8, Resources.Load<Sprite>("clubs_Eight")));
        cardList.Add(new PokerCard(21, "Club", 9, Resources.Load<Sprite>("clubs_Nine")));
        cardList.Add(new PokerCard(22, "Club", 10, Resources.Load<Sprite>("clubs_Ten")));
        cardList.Add(new PokerCard(23, "Club", 11, Resources.Load<Sprite>("clubs_Jack")));
        cardList.Add(new PokerCard(24, "Club", 12, Resources.Load<Sprite>("clubs_Queen")));
        cardList.Add(new PokerCard(25, "Club", 13, Resources.Load<Sprite>("clubs_King")));
        cardList.Add(new PokerCard(26, "Club", 14, Resources.Load<Sprite>("clubs_Ace")));

        // spades-Karten
        cardList.Add(new PokerCard(27, "Spade", 2, Resources.Load<Sprite>("spades_Two")));
        cardList.Add(new PokerCard(28, "Spade", 3, Resources.Load<Sprite>("spades_Three")));
        cardList.Add(new PokerCard(29, "Spade", 4, Resources.Load<Sprite>("spades_Four")));
        cardList.Add(new PokerCard(30, "Spade", 5, Resources.Load<Sprite>("spades_Five")));
        cardList.Add(new PokerCard(31, "Spade", 6, Resources.Load<Sprite>("spades_Six")));
        cardList.Add(new PokerCard(32, "Spade", 7, Resources.Load<Sprite>("spades_Seven")));
        cardList.Add(new PokerCard(33, "Spade", 8, Resources.Load<Sprite>("spades_Eight")));
        cardList.Add(new PokerCard(34, "Spade", 9, Resources.Load<Sprite>("spades_Nine")));
        cardList.Add(new PokerCard(35, "Spade", 10, Resources.Load<Sprite>("spades_Ten")));
        cardList.Add(new PokerCard(36, "Spade", 11, Resources.Load<Sprite>("spades_Jack")));
        cardList.Add(new PokerCard(37, "Spade", 12, Resources.Load<Sprite>("spades_Queen")));
        cardList.Add(new PokerCard(38, "Spade", 13, Resources.Load<Sprite>("spades_King")));
        cardList.Add(new PokerCard(39, "Spade", 14, Resources.Load<Sprite>("spades_Ace")));

    }
}
