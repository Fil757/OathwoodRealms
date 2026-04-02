using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PokerCard_Display : MonoBehaviour
{
    public List<PokerCard> displayCard = new List<PokerCard>();
    public int displayId;

    public int poker_id;
    [SerializeField] public string poker_sign;
    [SerializeField] public int value;

    public Sprite spriteImage;
    public Image artImage;

    public bool is_deckcard;

    public void InitCard()
    {
        ShufflePokerCard();
        UpdateDisplay(); // <-- ersetzt das Update() komplett
    }

    public void ShufflePokerCard()
    {
        if (is_deckcard == false)
        {
            displayId = Random.Range(1, 39);
            displayCard[0] = PokerCard_DB.cardList[displayId];
            UpdateDisplay();
        }
    }

    public void UpdateDisplay()
    {
        poker_id = displayCard[0].poker_id;
        poker_sign = displayCard[0].poker_sign;
        value = displayCard[0].value;
        spriteImage = displayCard[0].spriteImage;
        artImage.sprite = spriteImage;
    }
}

