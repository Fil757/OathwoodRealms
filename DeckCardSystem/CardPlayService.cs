using System.Collections;
using UnityEngine;

namespace TCG
{
    public static class CardPlayService
    {
        /// <summary>
        /// Sucht in einer Hand (handRoot) das passende DeckCardDisplay mit d.card == card
        /// und spielt die Karte über den vorhandenen UI-Ablauf (Flight+Cast+Mirror).
        /// </summary>
        public static bool PlayCard(DeckCard card, Transform handRoot, bool withAnimation = true)
        {
            if (card == null || handRoot == null) return false;

            var displays = handRoot.GetComponentsInChildren<DeckCardDisplay>(true);
            for (int i = 0; i < displays.Length; i++)
            {
                var d = displays[i];
                if (d != null && d.card == card)
                {
                    if (withAnimation)
                    {
                        d.PlaySelfFlightAndNotifyMirror();
                    }
                    else
                    {
                        // Ohne Flug: Minimal-Variante (wenn du das wirklich brauchst,
                        // sollte man das sauber als public Methode im Display lösen).
                        d.StartCoroutine(ForceCastWithoutFlight(d));
                    }
                    return true;
                }
            }

            Debug.LogWarning($"[CardPlayService] Kein DeckCardDisplay für Karte '{card.name}' unter '{handRoot.name}' gefunden.");
            return false;
        }

        private static IEnumerator ForceCastWithoutFlight(DeckCardDisplay d)
        {
            if (d == null) yield break;

            // Achtung: CastNow() ist bei dir private -> das hier geht nur,
            // wenn du CastNow public/internal machst ODER eine öffentliche Wrapper-Methode anbietest.
            // Für jetzt lassen wir das leer, damit es kompiliert.
            yield return null;
        }
    }
}

