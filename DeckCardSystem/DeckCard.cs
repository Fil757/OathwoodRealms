using UnityEngine;

namespace TCG
{
    public enum DeckCardType { Figure, Spell, Field }

    [CreateAssetMenu(fileName = "DeckCard", menuName = "TCG/DeckCard")]
    public class DeckCard : ScriptableObject
    {
        #region 1. Type Definition
        [Header("Type")]
        public DeckCardType CardType;
        #endregion

        #region 2. Figure Card Data
        [Header("Figure Data")]
        [Tooltip("ID der Figur in der Figure-Library.")]
        public string Figure_ID;
        public Figure Figure_SO_Data;

        [Tooltip("Optionales Prefab (nur wenn keine Figur mit dieser ID gefunden wird).")]
        public GameObject FigurePrefab;
        #endregion

        #region 3. Spell Card Data
        [Header("Spell Data")]
        [Tooltip("Referenz auf das SpecialMove-Objekt für diese Zauberkarte.")]
        public SpecialMove SpecialMoveData;
        #endregion

        #region 4. Field Card Data
        [Header("Field Data")]
        [Tooltip("Referenz auf das FieldCard-Objekt, das beim Ausspielen aktiviert wird.")]
        public FieldCard FieldCardData;
        #endregion

        [Header("Variant")]
        public string CardVariant;

        #region 5. Meta Info
        [Header("Meta")]
        public string DisplayNameOverride;
        public Sprite ArtworkSprite;
        [TextArea] public string Description;
        #endregion

        #region 6. Cost Info
        [Header("Costs")]
        [Tooltip("Kosten (z. B. Player Load Cost) beim Ausspielen der Karte.")]
        public int PlayerLoadCost = 0;
        #endregion

        #region 7. Helper Properties
        public bool IsFigure => CardType == DeckCardType.Figure && !string.IsNullOrEmpty(Figure_ID);
        public bool IsSpell => CardType == DeckCardType.Spell && SpecialMoveData != null;
        public bool IsField => CardType == DeckCardType.Field && FieldCardData != null;
        #endregion

        [Header("Evaluation for the Bot")]
        public ConditionBlock MakeSenseEvaluation_Block_Card;
    }
}




