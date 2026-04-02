using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using System;

public class RemoveCard : MonoBehaviour
{
    public Vector3 flyDirection = new Vector3(1, 0, 0);
    public float flyDistance = 300f;
    public float flyTime = 0.5f;
    public LeanTweenType easeType = LeanTweenType.easeInQuad;
    public float destroyDelay = 1.5f;

    // ---------------- SFX ----------------
    [Header("SFX")]
    [Tooltip("Optional: existing AudioSource to use (recommended: on an always-alive UI object).")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Sound to play when FlyAway starts.")]
    [SerializeField] private AudioClip flyAwayClip;

    [Range(0f, 1f)]
    [SerializeField] private float flyAwayVolume = 0.9f;

    // --- Names used in hierarchy ---
    const string CANVAS_P1 = "GUI-Canvas-P1";
    const string CANVAS_P2 = "GUI-Canvas-P2";
    const string HAND_P1   = "P1-Hand";
    const string HAND_P2   = "P2-Hand";
    const string GY_P1     = "P1-Graveyard";
    const string GY_P2     = "P2-Graveyard";
    const string MIRROR_P1_ON_P2 = "P1-Hand-Mirror-BackView";
    const string MIRROR_P2_ON_P1 = "P2-Hand-Mask/P2-Hand-Mirror-BackView";
    const string DECK_P1   = "P1-Deck";
    const string DECK_P2   = "P2-Deck";
    const string CARD_LIBRARY = "CardLibrary";

    public void FlyAway()
    {
        // 1) SFX sofort am Anfang
        PlayFlyAwaySfx();

        // Ursprungshand merken, bevor wir umhängen
        string originHand = transform.parent.name; // "P1-Hand" oder "P2-Hand"
        bool fromP1 = originHand == HAND_P1;
        bool fromP2 = originHand == HAND_P2;

        // Ziel-Graveyard je nach Ursprung
        Transform gyParent = fromP1
            ? FindInCanvas(CANVAS_P1, GY_P1)
            : FindInCanvas(CANVAS_P2, GY_P2);

        // Reparenten (Weltposition beibehalten, damit der Flug von der aktuellen Stelle startet)
        transform.SetParent(gyParent, worldPositionStays: true);

        // Hand neu sortieren (DeckCardDisplay sitzt üblicherweise auf den Hand-Cards)
        ReOrderAfter();

        // Karte logisch ins Deck zurücklegen
        BringBackToDeck(CardsGoSource(), fromP1 ? "P1" : "P2");

        // Spiegelkarte auf der Gegenseite entsorgen/verschieben
        DestroyMirrorCard(fromP1 ? "P1" : "P2");

        // Flug + Aufräumen
        var targetPos = transform.position + flyDirection.normalized * flyDistance;

        LeanTween.move(gameObject, targetPos, flyTime)
            .setEase(easeType)
            .setOnComplete(() =>
            {
                Destroy(gameObject, destroyDelay);
                CleanGraveyards(); // leert beide Graveyards visuell
            });
    }

    private void PlayFlyAwaySfx()
    {
        if (flyAwayClip == null) return;

        // Wenn du einen Source im Inspector zuweist -> den nutzen
        if (sfxSource != null)
        {
            // UI-SFX sollten 2D sein
            sfxSource.spatialBlend = 0f;
            sfxSource.PlayOneShot(flyAwayClip, flyAwayVolume);
            return;
        }

        // Fallback: temporärer 2D-OneShot (lebt unabhängig vom Card-Object)
        GameObject go = new GameObject("SFX_FlyAway_OneShot");
        var src = go.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.volume = 1f;
        src.playOnAwake = false;
        src.loop = false;

        src.PlayOneShot(flyAwayClip, flyAwayVolume);
        Destroy(go, Mathf.Max(0.1f, flyAwayClip.length + 0.1f));
    }

    private void ReOrderAfter()
    {
        var dc_display = GetComponentInParent<TCG.DeckCardDisplay>();
        if (dc_display != null) dc_display.ReOrderCards();
    }

    private void BringBackToDeck(GameObject removedCard, string player_holder)
    {
        if (removedCard == null) return;

        GameObject player_deck_p1 = GameObject.Find(DECK_P1);
        GameObject player_deck_p2 = GameObject.Find(DECK_P2);
        if (player_deck_p1 == null || player_deck_p2 == null) return;

        var deck_script_p1 = player_deck_p1.GetComponent<PlayerDeck>();
        var deck_script_p2 = player_deck_p2.GetComponent<PlayerDeck>();
        if (deck_script_p1 == null || deck_script_p2 == null) return;

        if (player_holder == "P1") deck_script_p1.deckCards.Add(removedCard);
        if (player_holder == "P2") deck_script_p2.deckCards.Add(removedCard);
    }

    private GameObject CardsGoSource()
    {
        GameObject cardLibrary = GameObject.Find(CARD_LIBRARY);
        if (cardLibrary == null) return null;

        foreach (Transform child in cardLibrary.transform)
        {
            if (child.name + "(Clone)" == gameObject.name)
                return child.gameObject;
        }
        return null;
    }

    private void DestroyMirrorCard(string playerOriginal)
    {
        if (playerOriginal == "P1")
        {
            Transform canvasP2Obj = GameObject.Find(CANVAS_P2)?.transform;
            if (canvasP2Obj == null) return;

            Transform mirrorHand = canvasP2Obj.Find(MIRROR_P1_ON_P2);
            if (mirrorHand == null || mirrorHand.childCount == 0) return;

            Transform firstChild = mirrorHand.GetChild(0);
            Transform gyP2 = canvasP2Obj.Find(GY_P2);
            if (gyP2 == null) return;

            firstChild.SetParent(gyP2, worldPositionStays: true);

            var dc = firstChild.GetComponent<TCG.DeckCardDisplay>();
            if (dc != null) dc.ReOrderCards();
        }
        else
        {
            Transform canvasP1Obj = GameObject.Find(CANVAS_P1)?.transform;
            if (canvasP1Obj == null) return;

            Transform mirrorHand = canvasP1Obj.Find(MIRROR_P2_ON_P1);
            if (mirrorHand == null || mirrorHand.childCount == 0) return;

            Transform firstChild = mirrorHand.GetChild(0);
            Transform gyP1 = canvasP1Obj.Find(GY_P1);
            if (gyP1 == null) return;

            firstChild.SetParent(gyP1, worldPositionStays: true);

            var dc = firstChild.GetComponent<TCG.DeckCardDisplay>();
            if (dc != null) dc.ReOrderCards();
        }
    }

    private void CleanGraveyards()
    {
        Transform canvasP1Obj = GameObject.Find(CANVAS_P1)?.transform;
        Transform canvasP2Obj = GameObject.Find(CANVAS_P2)?.transform;
        if (canvasP1Obj == null || canvasP2Obj == null) return;

        Transform gyP1 = canvasP1Obj.Find(GY_P1);
        Transform gyP2 = canvasP2Obj.Find(GY_P2);
        if (gyP1 == null || gyP2 == null) return;

        for (int i = gyP1.childCount - 1; i >= 0; i--)
            Destroy(gyP1.GetChild(i).gameObject);

        for (int i = gyP2.childCount - 1; i >= 0; i--)
            Destroy(gyP2.GetChild(i).gameObject);
    }

    private Transform FindInCanvas(string canvasName, string childName)
    {
        var c = GameObject.Find(canvasName);
        if (c == null) return null;
        return c.transform.Find(childName);
    }
}
