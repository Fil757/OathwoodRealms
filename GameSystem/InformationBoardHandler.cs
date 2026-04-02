using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InformationBoardHandler : MonoBehaviour
{
    public static InformationBoardHandler current;

    void Awake()
    {
        current = this;
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }

    public GameObject Figure_InfoBoard_Prefab;
    public GameObject Non_Figure_InfoBoard_Prefab;

    public bool isCard_andContains_Figure;
    public bool isCard_andContains_Field;
    public bool isCard_andContains_Spell;

    public Vector2 offset;

    // ---------------------------
    // Robustness: Transfer-Guard
    // ---------------------------
    private int _spawnVersion = 0;                 // wird bei jedem Spawn erhöht (invalidiert alte Transfers)
    private Coroutine _pendingTransfer = null;     // aktuell laufender Transfer (falls vorhanden)

    private Transform GetParentCanvas()
    {
        GameObject parent = GameObject.Find("GUI-Canvas-P1");
        if (parent == null)
        {
            Debug.LogWarning("[InformationBoardHandler] GUI-Canvas-P1 not found.");
            return null;
        }
        return parent.transform;
    }

    private void DestroyExistingBoards(Transform parent)
    {
        if (parent == null) return;

        // Destroy ist end-of-frame -> ist ok, weil wir NICHT mehr per Find() arbeiten,
        // sondern das frisch gespawnte Board direkt referenzieren.
        foreach (Transform child in parent)
        {
            if (child.name == "FigureInformationBoard(Clone)" ||
                child.name == "FieldSpellTrapInformationBoard(Clone)")
            {
                Destroy(child.gameObject);
            }
        }
    }

    private GameObject SpawnBoard(GameObject prefab)
    {
        Transform parent = GetParentCanvas();
        if (parent == null || prefab == null) return null;

        AudioManager.Instance?.PlaySFX2D("Open_InfoBoard");

        // 1) Alle alten Transfers invalidieren / abbrechen
        _spawnVersion++;
        if (_pendingTransfer != null)
        {
            StopCoroutine(_pendingTransfer);
            _pendingTransfer = null;
        }

        // 2) Alte Boards entfernen
        DestroyExistingBoards(parent);

        // 3) Neues Board spawnen
        GameObject spawned = Instantiate(prefab, parent);

        RectTransform rt = spawned.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition += offset;
        }

        return spawned;
    }

    public void Spawn_FigureInfoBoard()
    {
        SpawnBoard(Figure_InfoBoard_Prefab);
    }

    public void Spawn_NoNFigureInfoBoard()
    {
        SpawnBoard(Non_Figure_InfoBoard_Prefab);
    }

    public void Spawn_FigureInfoComplete(GameObject Figure)
    {
        GameObject board = SpawnBoard(Figure_InfoBoard_Prefab);
        Transfer_FigureInfo(Figure, board);
        Debug.Log($"[InfoBoard] current = {(InformationBoardHandler.current ? InformationBoardHandler.current.name : "NULL")}");

    }

    public void Spawn_CardInfoComplete(GameObject Card)
    {
        var cardFlags = Card != null ? Card.GetComponent<InformationBoardHandler>() : null;
        if (cardFlags == null)
        {
            Debug.LogWarning("Spawn_CardInfoComplete: Card has no InformationBoardHandler!");
            return;
        }

        bool isFigure = cardFlags.isCard_andContains_Figure;
        bool isField  = cardFlags.isCard_andContains_Field;
        bool isSpell  = cardFlags.isCard_andContains_Spell;

        GameObject board = null;

        // Board erzeugen
        if (isFigure)
            board = SpawnBoard(Figure_InfoBoard_Prefab);
        else if (isField || isSpell)
            board = SpawnBoard(Non_Figure_InfoBoard_Prefab);

        // Infos übertragen
        Transfer_CardInfo(Card, isFigure, isField, isSpell, board);
    }

    // Overload: alte Signatur bleibt nutzbar
    public void Transfer_CardInfo(GameObject cardToDisplay, bool isFigure, bool isField, bool isSpell)
    {
        // Fallback: wenn jemand diese Methode direkt aufruft, suchen wir Board notfalls noch.
        // (Robuster ist: Spawn_CardInfoComplete nutzen.)
        GameObject board = null;
        if (isFigure)
            board = GameObject.Find("GUI-Canvas-P1/FigureInformationBoard(Clone)");
        else
            board = GameObject.Find("GUI-Canvas-P1/FieldSpellTrapInformationBoard(Clone)");

        Transfer_CardInfo(cardToDisplay, isFigure, isField, isSpell, board);
    }

    private void Transfer_CardInfo(GameObject cardToDisplay, bool isFigure, bool isField, bool isSpell, GameObject board)
    {
        if (isFigure)
            Transfer_FigureInfo_FromCard(cardToDisplay, board);
        else if (isField)
            Transfer_FieldInfo_FromCard(cardToDisplay, board);
        else if (isSpell)
            Transfer_SpellInfo_FromCard(cardToDisplay, board);
    }

    // ---------------------------
    // FIGUREBOARD BEING SPAWNED DIRECTLY BY FIGURE
    // ---------------------------
    public void Transfer_FigureInfo(GameObject FigureToDisplay)
    {
        // Fallback (alte Nutzung): Board per Find suchen
        GameObject board = GameObject.Find("GUI-Canvas-P1/FigureInformationBoard(Clone)");
        Transfer_FigureInfo(FigureToDisplay, board);
    }

    private void Transfer_FigureInfo(GameObject FigureToDisplay, GameObject board)
    {
        if (FigureToDisplay == null)
        {
            Debug.LogWarning("[InformationBoardHandler] Transfer_FigureInfo called with NULL FigureToDisplay.");
            return;
        }
        if (board == null)
        {
            Debug.LogWarning("[InformationBoardHandler] Transfer_FigureInfo called but board is NULL.");
            return;
        }

        int myVersion = _spawnVersion;
        _pendingTransfer = StartCoroutine(DelayTransfer(FigureToDisplay, board, myVersion));
    }

    private IEnumerator DelayTransfer(GameObject FigureToDisplay, GameObject board, int version)
    {
        yield return new WaitForSeconds(0.002f);

        // Abbruch, wenn inzwischen ein neuer Spawn passiert ist
        if (version != _spawnVersion) yield break;

        if (FigureToDisplay == null) yield break;
        if (board == null) yield break;

        var df = FigureToDisplay.GetComponent<Display_Figure>();
        if (df == null || df.FIGURE == null) yield break;

        var inf_board = board.GetComponent<InformationBoard>();
        if (inf_board == null) yield break;

        inf_board.nameString = df.FIGURE.FIGURE_NAME ?? string.Empty;

        if (df.FIGURE.SPECIAL_A != null)
            inf_board.spell_descriptionString = df.FIGURE.SPECIAL_A.SPECIALMOVE_DESCRIPTION;
        else
            inf_board.spell_descriptionString = string.Empty;

        var figure_spell = df.FIGURE.SPECIAL_B;
        var figure_field = df.FIGURE.Figure_FieldEffect;

        inf_board.castspell_descriptionString = (figure_spell == null)
            ? "This Figure does not possess a Cast Effect."
            : figure_spell.SPECIALMOVE_DESCRIPTION;

        inf_board.fieldeffect_descriptionString = (figure_field == null)
            ? "This Figure does not possess a Field Effect."
            : figure_field.Description;

        inf_board.health = df.FIGURE.FIGURE_HEALTH;
        inf_board.atk    = df.FIGURE.FIGURE_ATK;
        inf_board.def    = df.FIGURE.FIGURE_DEF;

        inf_board.cast_cost  = df.FIGURE.FIGURE_COST_CAST;
        inf_board.std_cost   = df.FIGURE.FIGURE_COST;
        inf_board.spell_cost = df.FIGURE.FIGURE_COST_SPC;

        inf_board.portraitSprite = df.FIGURE.FigurePortraitSprite;

        inf_board.PlayFadeIn();

        // Transfer abgeschlossen
        if (_pendingTransfer != null) _pendingTransfer = null;
    }

    // ---------------------------
    // FIGUREBOARD BEING SPAWNED BY CARD
    // ---------------------------
    public void Transfer_FigureInfo_FromCard(GameObject FigureCardToDisplay)
    {
        GameObject board = GameObject.Find("GUI-Canvas-P1/FigureInformationBoard(Clone)");
        Transfer_FigureInfo_FromCard(FigureCardToDisplay, board);
    }

    private void Transfer_FigureInfo_FromCard(GameObject FigureCardToDisplay, GameObject board)
    {
        if (FigureCardToDisplay == null || board == null) return;

        int myVersion = _spawnVersion;
        _pendingTransfer = StartCoroutine(DelayTransfer_FigureCard(FigureCardToDisplay, board, myVersion));
    }

    private IEnumerator DelayTransfer_FigureCard(GameObject FigureCardToDisplay, GameObject board, int version)
    {
        yield return new WaitForSeconds(0.002f);

        if (version != _spawnVersion) yield break;
        if (FigureCardToDisplay == null || board == null) yield break;

        var dc_display = FigureCardToDisplay.GetComponent<TCG.DeckCardDisplay>();
        if (dc_display == null || dc_display.card == null || dc_display.card.Figure_SO_Data == null) yield break;

        var inf_board = board.GetComponent<InformationBoard>();
        if (inf_board == null) yield break;

        inf_board.nameString = dc_display.card.Figure_SO_Data.FIGURE_NAME;

        var a = dc_display.card.Figure_SO_Data.SPECIAL_A;
        inf_board.spell_descriptionString = (a != null) ? a.SPECIALMOVE_DESCRIPTION : string.Empty;

        var figure_spell = dc_display.card.Figure_SO_Data.SPECIAL_B;
        var figure_field = dc_display.card.Figure_SO_Data.Figure_FieldEffect;

        inf_board.castspell_descriptionString = (figure_spell == null)
            ? "This Figure does not possess a Cast Effect."
            : figure_spell.SPECIALMOVE_DESCRIPTION;

        inf_board.fieldeffect_descriptionString = (figure_field == null)
            ? "This Figure does not possess a Field Effect."
            : figure_field.Description;

        inf_board.health = dc_display.card.Figure_SO_Data.FIGURE_HEALTH;
        inf_board.atk    = dc_display.card.Figure_SO_Data.FIGURE_ATK;
        inf_board.def    = dc_display.card.Figure_SO_Data.FIGURE_DEF;

        inf_board.cast_cost  = dc_display.card.Figure_SO_Data.FIGURE_COST_CAST;
        inf_board.std_cost   = dc_display.card.Figure_SO_Data.FIGURE_COST;
        inf_board.spell_cost = dc_display.card.Figure_SO_Data.FIGURE_COST_SPC;

        inf_board.portraitSprite = dc_display.card.Figure_SO_Data.FigurePortraitSprite;

        inf_board.PlayFadeIn();

        if (_pendingTransfer != null) _pendingTransfer = null;
    }

    // ---------------------------
    // FIELD OR TRAP SPAWNED BY CARD
    // ---------------------------
    public void Transfer_FieldInfo_FromCard(GameObject FieldCardToDisplay)
    {
        GameObject board = GameObject.Find("GUI-Canvas-P1/FieldSpellTrapInformationBoard(Clone)");
        Transfer_FieldInfo_FromCard(FieldCardToDisplay, board);
    }

    private void Transfer_FieldInfo_FromCard(GameObject FieldCardToDisplay, GameObject board)
    {
        if (FieldCardToDisplay == null || board == null) return;

        int myVersion = _spawnVersion;
        _pendingTransfer = StartCoroutine(DelayTransfer_FieldCard_fromCard(FieldCardToDisplay, board, myVersion));
    }

    private IEnumerator DelayTransfer_FieldCard_fromCard(GameObject FieldCardToDisplay, GameObject board, int version)
    {
        yield return new WaitForSeconds(0.002f);

        if (version != _spawnVersion) yield break;
        if (FieldCardToDisplay == null || board == null) yield break;

        var dc_display = FieldCardToDisplay.GetComponent<TCG.DeckCardDisplay>();
        if (dc_display == null || dc_display.card == null || dc_display.card.FieldCardData == null) yield break;

        var inf_board = board.GetComponent<InformationBoard>();
        if (inf_board == null) yield break;

        inf_board.nameString = dc_display.card.FieldCardData.Name;
        inf_board.portraitSprite = dc_display.card.FieldCardData.artwork;
        inf_board.cast_cost = dc_display.card.FieldCardData.Cost;
        inf_board.spell_descriptionString = dc_display.card.FieldCardData.Description;

        inf_board.PlayFadeIn();

        if (_pendingTransfer != null) _pendingTransfer = null;
    }

    // ---------------------------
    // SPECIAL (SPELL) SPAWNED BY CARD
    // ---------------------------
    public void Transfer_SpellInfo_FromCard(GameObject SpellCardToDisplay)
    {
        GameObject board = GameObject.Find("GUI-Canvas-P1/FieldSpellTrapInformationBoard(Clone)");
        Transfer_SpellInfo_FromCard(SpellCardToDisplay, board);
    }

    private void Transfer_SpellInfo_FromCard(GameObject SpellCardToDisplay, GameObject board)
    {
        if (SpellCardToDisplay == null || board == null) return;

        int myVersion = _spawnVersion;
        _pendingTransfer = StartCoroutine(DelayTransfer_SpellCard_fromCard(SpellCardToDisplay, board, myVersion));
    }

    private IEnumerator DelayTransfer_SpellCard_fromCard(GameObject SpellCardToDisplay, GameObject board, int version)
    {
        yield return new WaitForSeconds(0.002f);

        if (version != _spawnVersion) yield break;
        if (SpellCardToDisplay == null || board == null) yield break;

        var dc_display = SpellCardToDisplay.GetComponent<TCG.DeckCardDisplay>();
        if (dc_display == null || dc_display.card == null) yield break;

        var inf_board = board.GetComponent<InformationBoard>();
        if (inf_board == null) yield break;

        inf_board.nameString = dc_display.card.DisplayNameOverride;
        inf_board.portraitSprite = dc_display.card.ArtworkSprite;
        inf_board.cast_cost = dc_display.card.PlayerLoadCost;
        inf_board.spell_descriptionString = dc_display.card.Description;

        inf_board.PlayFadeIn();

        if (_pendingTransfer != null) _pendingTransfer = null;
    }

    // ---------------------------
    // TRAP SPAWNED BY FIELDCARDCONTROLLER
    // ---------------------------
    public void Transfer_FieldInfo_FromTrap(FieldCard FieldEffect)
    {
        GameObject board = SpawnBoard(Non_Figure_InfoBoard_Prefab);
        if (board == null) return;

        int myVersion = _spawnVersion;
        _pendingTransfer = StartCoroutine(DelayTransfer_FieldCard_fromTrap(FieldEffect, board, myVersion));
    }

    private IEnumerator DelayTransfer_FieldCard_fromTrap(FieldCard FieldEffect, GameObject board, int version)
    {
        yield return new WaitForSeconds(0.002f);

        if (version != _spawnVersion) yield break;
        if (FieldEffect == null || board == null) yield break;

        var inf_board = board.GetComponent<InformationBoard>();
        if (inf_board == null) yield break;

        inf_board.nameString = FieldEffect.Name;
        inf_board.portraitSprite = FieldEffect.artwork;
        inf_board.cast_cost = FieldEffect.Cost;
        inf_board.spell_descriptionString = FieldEffect.Description;

        inf_board.PlayFadeIn();

        if (_pendingTransfer != null) _pendingTransfer = null;
    }
}
