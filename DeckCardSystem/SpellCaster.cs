using UnityEngine;

namespace TCG
{
    [DisallowMultipleComponent]
    public class SpellCaster : MonoBehaviour
    {
        [SerializeField] private SpecialMoveController specialMoveController;

        private void Reset()
        {
            if (!specialMoveController)
                specialMoveController = FindObjectOfType<SpecialMoveController>(includeInactive: true);
        }

        public bool CanCast(DeckCard card)
        {
            return card && card.IsSpell && card.SpecialMoveData != null;
        }

        public void Cast(DeckCard card)
        {
            if (!CanCast(card)) { Debug.LogWarning("[SpellCaster] Invalid card/spell"); return; }

            // ZENTRAL: nutzt ausschließlich die bestehende Logik im SpecialMoveController
            // (dupliziert NICHT Stats-/Animations-Logik)
            specialMoveController.Play_SpellFromDeckCard(card.SpecialMoveData);
        }
    }
}

