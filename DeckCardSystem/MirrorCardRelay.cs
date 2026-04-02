using UnityEngine;

namespace TCG
{
    [DisallowMultipleComponent]
    public class MirrorCardRelay : MonoBehaviour
    {
        [Header("Hands")]
        [SerializeField] private RectTransform handP1;
        [SerializeField] private RectTransform handP1_mirrored;
        [SerializeField] private RectTransform handP2;
        [SerializeField] private RectTransform handP2_mirrored;

        [Header("Graveyards")]
        [SerializeField] private string graveyardP1Name = "P1-Graveyard";
        [SerializeField] private string graveyardP2Name = "P2-Graveyard";

        private void OnEnable()
        {
            DeckCardDisplay.OnCardPlayedVisual += HandleCardPlayedVisual;
        }
        private void OnDisable()
        {
            DeckCardDisplay.OnCardPlayedVisual -= HandleCardPlayedVisual;
        }

        private void HandleCardPlayedVisual(RectTransform sourceHand, int siblingIndex)
        {
            if (!sourceHand) return;

            // 1) Aktiven Spieler bestimmen
            bool p1Turn = TryIsP1Turn(out bool hasInfo) ? true : false;
            // Wenn keine Info vorhanden war (hasInfo == false), p1Turn bleibt default (false -> wir behandeln beide Fälle unten über sourceHand).

            // 2) Zielhand + Ziel-Graveyard je nach aktivem Spieler auflösen
            RectTransform targetHand = null;
            string targetGrave = graveyardP2Name;

            if (p1Turn)
            {
                // P1 ist aktiv → die gespielte Karte kommt aus P1-Hand und spiegelt in P1_mirrored → landet visuell in P2-Graveyard
                if (sourceHand == handP1)
                {
                    targetHand = handP1_mirrored;
                    targetGrave = graveyardP2Name;
                }
                else if (sourceHand == handP1_mirrored)
                {
                    // (symmetrischer Rückweg, falls du später vom Mirror aus triggerst)
                    targetHand = handP1;
                    targetGrave = graveyardP1Name;
                }
                else
                {
                    // Falls trotz P1-Zug eine andere Hand meldet, prüfen wir P2-Fall zur Sicherheit
                    if (sourceHand == handP2)
                    {
                        targetHand = handP2_mirrored;
                        targetGrave = graveyardP1Name;
                    }
                    else if (sourceHand == handP2_mirrored)
                    {
                        targetHand = handP2;
                        targetGrave = graveyardP2Name;
                    }
                    else return;
                }
            }
            else
            {
                // P2 ist aktiv → die gespielte Karte kommt aus P2-Hand und spiegelt in P2_mirrored → landet visuell in P1-Graveyard
                if (sourceHand == handP2)
                {
                    targetHand = handP2_mirrored;
                    targetGrave = graveyardP1Name;
                }
                else if (sourceHand == handP2_mirrored)
                {
                    targetHand = handP2;
                    targetGrave = graveyardP2Name;
                }
                else
                {
                    // Falls trotz P2-Zug eine andere Hand meldet, prüfen wir P1-Fall zur Sicherheit
                    if (sourceHand == handP1)
                    {
                        targetHand = handP1_mirrored;
                        targetGrave = graveyardP2Name;
                    }
                    else if (sourceHand == handP1_mirrored)
                    {
                        targetHand = handP1;
                        targetGrave = graveyardP1Name;
                    }
                    else return;
                }
            }

            if (!targetHand) return;
            if (siblingIndex < 0 || siblingIndex >= targetHand.childCount) return;

            var mirrorChild = targetHand.GetChild(siblingIndex);
            if (!mirrorChild) return;

            var mirrorDisplay = mirrorChild.GetComponent<DeckCardDisplay>();
            if (!mirrorDisplay) return;

            // Ghost-Flight (kein Cast), eigener Canvas → eigener CentralPoint wird lokal aufgelöst
            mirrorDisplay.PlayMirrorGhostFlight(targetGrave, alsoDustAndShake: true);
        }

        /// <summary>
        /// Versucht, anhand des TurnManager den aktiven Spieler zu bestimmen.
        /// Rückgabe: true = P1 ist dran; false = P2 ist dran (oder keine Info).
        /// hasInfo = false, wenn wir keine verlässliche Info finden konnten.
        /// </summary>
        private bool TryIsP1Turn(out bool hasInfo)
        {
            hasInfo = false;

            // TurnManager finden (Pattern wie im Projekt)
            var tmGO = GameObject.Find("TurnManager"); // vorhanden laut Systemarchitektur
            // (GameObject "TurnManager" mit Komponente TurnManager) 
            // -> siehe GameSystem/Architektur-Dokumentation
            // Wir nutzen gleiche Find-Strategie wie z.B. Damage/Attack-Skripte.
            if (!tmGO) return false;

            var tm = tmGO.GetComponent<TurnManager>();
            if (!tm) return false;

            // Mehrere mögliche Repräsentationen abfragen, damit es robust ist
            // 1) String-Feld "activePlayer" (z.B. "P1" / "P2")
            var strField = tm.GetType().GetField("activePlayer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (strField != null && strField.FieldType == typeof(string))
            {
                string v = (string)strField.GetValue(tm);
                if (!string.IsNullOrEmpty(v))
                {
                    hasInfo = true;
                    return v == "P1";
                }
            }

            // 2) Bool-Property "isP1Turn"
            var boolProp = tm.GetType().GetProperty("isP1Turn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (boolProp != null && boolProp.PropertyType == typeof(bool))
            {
                hasInfo = true;
                return (bool)boolProp.GetValue(tm);
            }

            // 3) Enum-Property "ActivePlayer" mit Werten P1/P2
            var enumProp = tm.GetType().GetProperty("ActivePlayer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (enumProp != null && enumProp.PropertyType.IsEnum)
            {
                var val = enumProp.GetValue(tm);
                if (val != null)
                {
                    hasInfo = true;
                    return val.ToString() == "P1";
                }
            }

            // Keine verlässliche Info gefunden
            return false;
        }
    }
}
