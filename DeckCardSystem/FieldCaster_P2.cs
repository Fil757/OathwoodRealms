using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CustomInspector;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;
using TCG;

public class FieldCaster_P2 : MonoBehaviour
{
    public static FieldCaster_P2 instance;

    [Header("Prefabs / FX")]
    public GameObject FieldCardLifePoint_obj;

    [Header("P2 Placeholder pro Slot (0,1,2)")]
    [Tooltip("Index 0 = Slot 0 (links), Index 1 = Slot 1 (Mitte), Index 2 = Slot 2 (rechts). Reihenfolge muss zu deinem Board passen.")]
    public GameObject[] FieldCard_PlaceHolders_P2; // Länge = 3

    [Tooltip("Optional: leere Dummy-FieldCard, z.B. 'kein Effekt aktiv'. Kann leer bleiben.")]
    public FieldCard Empty_PlaceHolder_FieldEffect;

    // =====================================================
    //  CRISTAL TRACKING (robust)
    // =====================================================
    [Header("Runtime (Debug)")]
    [Tooltip("Merkt pro Slot die letzte gespawnte Cristal-Instanz, damit sie beim Replace/Destroy sicher entfernt wird.")]
    [SerializeField] private GameObject[] SpawnedCristals_P2 = new GameObject[3];

    // optional: falls Destroy-Coroutines noch laufen und später den neuen Kristall weghauen würden
    private Coroutine[] _pendingDestroy_P2 = new Coroutine[3];

    private void Awake()
    {
        instance = this;

        // Safety: Array-Länge ggf. anpassen
        if (SpawnedCristals_P2 == null || SpawnedCristals_P2.Length != 3)
            SpawnedCristals_P2 = new GameObject[3];

        if (_pendingDestroy_P2 == null || _pendingDestroy_P2.Length != 3)
            _pendingDestroy_P2 = new Coroutine[3];
    }

    // =====================================================================
    //  PUBLIC API
    // =====================================================================

    public void ReplaceP2_FieldCard(GameObject new_card_obj)
    {
        int slotIndex = FindFirstFreeSlot_P2();
        ReplaceP2_FieldCard(new_card_obj, slotIndex);
    }

    public void ReplaceP2_FieldCard(GameObject new_card_obj, int slotIndex)
    {
        if (FieldCard_PlaceHolders_P2 == null ||
            slotIndex < 0 ||
            slotIndex >= FieldCard_PlaceHolders_P2.Length)
        {
            Debug.LogWarning("[FieldCaster_P2] Ungültiger slotIndex " + slotIndex + " für P2.");
            return;
        }

        var placeholderGO = FieldCard_PlaceHolders_P2[slotIndex];
        if (!placeholderGO)
        {
            Debug.LogWarning("[FieldCaster_P2] Placeholder für Slot " + slotIndex + " ist null.");
            return;
        }

        // Wenn für diesen Slot gerade noch ein Destroy-Delay läuft: stoppen,
        // sonst killt dir die Coroutine später evtl. den neuen Stand.
        StopPendingDestroy_P2(slotIndex);

        // Placeholder sichtbar machen
        placeholderGO.SetActive(true);

        // kleiner Spawn-Effekt
        PlayParticleAt(placeholderGO, 9);

        // Logik im Controller exakt in diesen Slot reinschreiben
        SetNewFieldCard_P2_AtSlot(new_card_obj, slotIndex);

        // UI + Cristal
        ReplaceFieldCardAttributes(new_card_obj, placeholderGO, slotIndex);
    }

    private IEnumerator ReplaceP2_FieldCard_Delayed(GameObject new_card_obj, int slotIndex)
    {
        if (FieldCard_PlaceHolders_P2 == null ||
            slotIndex < 0 ||
            slotIndex >= FieldCard_PlaceHolders_P2.Length)
            yield break;

        var placeholderGO = FieldCard_PlaceHolders_P2[slotIndex];
        if (!placeholderGO)
            yield break;

        StopPendingDestroy_P2(slotIndex);

        placeholderGO.SetActive(true);
        yield return null;

        SetNewFieldCard_P2_AtSlot(new_card_obj, slotIndex);
        ReplaceFieldCardAttributes(new_card_obj, placeholderGO, slotIndex);
    }

    // =====================================================================
    //  CORE LOGIK
    // =====================================================================

    private int FindFirstFreeSlot_P2()
    {
        if (FieldCard_PlaceHolders_P2 == null || FieldCard_PlaceHolders_P2.Length == 0)
        {
            Debug.LogWarning("[FieldCaster_P2] FindFirstFreeSlot_P2: Keine Placeholders definiert. Fallback Slot 0");
            return 0;
        }

        for (int i = 0; i < FieldCard_PlaceHolders_P2.Length; i++)
        {
            var ph = FieldCard_PlaceHolders_P2[i];
            if (ph == null) continue;

            if (!ph.activeInHierarchy)
                return i;
        }

        return 0;
    }

    private void SetNewFieldCard_P2_AtSlot(GameObject new_card_obj, int slotIndex)
    {
        if (!new_card_obj)
        {
            Debug.LogWarning("[FieldCaster_P2] new_card_obj ist null (P2)");
            return;
        }

        var disp = new_card_obj.GetComponent<DeckCardDisplay>();
        if (!disp)
        {
            Debug.LogWarning("[FieldCaster_P2] DeckCardDisplay fehlt auf gespielter Karte (P2)");
            return;
        }

        var deckCardRef = disp.card;
        if (!deckCardRef)
        {
            Debug.LogWarning("[FieldCaster_P2] disp.card ist null (keine DeckCard referenziert) (P2)");
            return;
        }

        var fieldData = deckCardRef.FieldCardData;
        if (!fieldData)
        {
            Debug.LogWarning("[FieldCaster_P2] DeckCard hat keine FieldCardData (P2)");
            return;
        }

        // NOTE:
        // Dein vorhandener FieldCardController scheint (nach deinem Kontext) P1-exklusiv zu sein
        // und du hast einen separaten FieldCardController_P2.
        // -> Hier wird daher FieldCardController_P2.instance erwartet.
        if (FieldCardController_P2.instance)
        {
            FieldCardController_P2.instance.SetActiveFieldCard_AtIndex(fieldData, slotIndex);
        }
        else
        {
            Debug.LogWarning("[FieldCaster_P2] FieldCardController_P2.instance ist null (P2)");
        }
    }

    private void ReplaceFieldCardAttributes(GameObject new_card_obj, GameObject placeholderGO, int slotIndex)
    {
        if (!new_card_obj || !placeholderGO)
            return;

        var dc_dis_new = new_card_obj.GetComponent<DeckCardDisplay>();
        if (!dc_dis_new)
        {
            Debug.LogWarning("[FieldCaster_P2] ReplaceFieldCardAttributes: DeckCardDisplay fehlt an new_card_obj");
            return;
        }

        var dc_new = dc_dis_new.card;
        if (!dc_new)
        {
            Debug.LogWarning("[FieldCaster_P2] ReplaceFieldCardAttributes: disp.card ist null (kein DeckCard SO)");
            return;
        }

        var dc_dis_placeholder = placeholderGO.GetComponent<DeckCardDisplay>();
        if (!dc_dis_placeholder)
        {
            Debug.LogWarning("[FieldCaster_P2] ReplaceFieldCardAttributes: Placeholder hat keinen DeckCardDisplay");
            return;
        }

        // --- ROBUST: alten Cristal dieses Slots sicher entfernen ---
        DestroyCristal_P2(slotIndex);

        // Optionaler Fallback: räumt "Altlasten" weg, falls vorher schon mehrere Instanzen existieren
        ClearCristalChildrenFallback(placeholderGO);

        // Neu spawnen + merken
        Spawn_New_Cristal_P2(new_card_obj, placeholderGO, slotIndex);

        // UI setzen
        dc_dis_placeholder.SetCard(dc_new);
    }

    // =====================================================================
    //  EFFECTS / PARTICLES
    // =====================================================================

    #region === Helpers (FX & PopUps) ===

    public void PlayParticleAt(GameObject target, int particleIndex)
    {
        StartCoroutine(PlayParticleAt_delay(target, particleIndex));
    }

    private IEnumerator PlayParticleAt_delay(GameObject target, int particleIndex)
    {
        yield return new WaitForSeconds(0.035f);

        ParticleController.Instance.PlayParticleEffect(
            target.transform.Find("Visual/CardMask").position + new Vector3(0f, 0f, 20f),
            particleIndex,
            new Vector3(30f, 150f, 30f),
            Quaternion.Euler(-90f, 0f, 0f)
        );
    }

    private void PlayFieldCardDestroy_Particle(GameObject target)
    {
        ParticleController.Instance.PlayParticleEffect(
            target.transform.Find("Visual/CardMask").position + new Vector3(0f, 0f, 20f),
            6,
            new Vector3(75f, 75f, 75f),
            Quaternion.Euler(-90f, 0f, 0f)
        );
    }

    private void PlayPre_FieldCardDestroy_Particle(GameObject target)
    {
        ParticleController.Instance.PlayParticleEffect(
            target.transform.Find("Visual/CardMask").position + new Vector3(0f, 0f, 20f),
            11,
            new Vector3(75f, 75f, 75f),
            Quaternion.Euler(-90f, 0f, 0f)
        );
    }

    #endregion

    // =====================================================================
    //  ORDER / INDEX LOGIK (Lesen & Zerstören)
    // =====================================================================

    private string Evaluate_P2_FieldCardOrder()
    {
        bool a0 = FieldCard_PlaceHolders_P2[0].activeInHierarchy;
        bool a1 = FieldCard_PlaceHolders_P2[1].activeInHierarchy;
        bool a2 = FieldCard_PlaceHolders_P2[2].activeInHierarchy;

        string pattern =
            (a0 ? "o" : "x") + " " +
            (a1 ? "o" : "x") + " " +
            (a2 ? "o" : "x");

        Debug.Log(pattern);
        return pattern;
    }

    public int Evaluate_P2_FieldCardTarget_newIndex(int slotIndex)
    {
        List<int> activeSlots = new List<int>(3);

        for (int realIndex = 0; realIndex < 3; realIndex++)
        {
            if (FieldCard_PlaceHolders_P2[realIndex].activeInHierarchy)
                activeSlots.Add(realIndex);
        }

        if (slotIndex >= 0 && slotIndex < activeSlots.Count)
            return activeSlots[slotIndex];

        return 0;
    }

    public void DestroyP2_FieldCard_RelativeIndex(int slotIndex)
    {
        int resolvedIndex = Evaluate_P2_FieldCardTarget_newIndex(slotIndex);
        StopPendingDestroy_P2(resolvedIndex);

        _pendingDestroy_P2[resolvedIndex] = StartCoroutine(DestroyP2_FieldCard_AbsoluteIndex_Delayed(resolvedIndex));
    }

    public void DestroyP2_FieldCard_AbsoluteIndex(int slotIndex)
    {
        StopPendingDestroy_P2(slotIndex);
        _pendingDestroy_P2[slotIndex] = StartCoroutine(DestroyP2_FieldCard_AbsoluteIndex_Delayed(slotIndex));
    }

    private IEnumerator DestroyP2_FieldCard_AbsoluteIndex_Delayed(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) yield break;

        var ph = FieldCard_PlaceHolders_P2[slotIndex];
        if (!ph) yield break;

        PlayPre_FieldCardDestroy_Particle(ph);

        yield return new WaitForSeconds(0.75f);

        PlayFieldCardDestroy_Particle(ph);

        // --- ROBUST: Cristal dieses Slots sicher entfernen ---
        DestroyCristal_P2(slotIndex);
        ClearCristalChildrenFallback(ph);

        ph.SetActive(false);

        if (FieldCardController_P2.instance)
        {
            FieldCardController_P2.instance.SetActiveFieldCard_AtIndex(
                Empty_PlaceHolder_FieldEffect,
                slotIndex
            );
        }

        _pendingDestroy_P2[slotIndex] = null;
    }

    // =====================================================================
    //  CRISTAL SPAWN / CLEANUP
    // =====================================================================

    private void Spawn_New_Cristal_P2(GameObject cristal_holder, GameObject cristal_new_parent_object, int slotIndex)
    {
        var disp = cristal_holder.GetComponent<DeckCardDisplay>();
        if (!disp || disp.card == null || disp.card.FieldCardData == null || disp.card.FieldCardData.cristal_prefab == null)
            return;

        GameObject cristal_object = Instantiate(
            disp.card.FieldCardData.cristal_prefab,
            cristal_new_parent_object.transform
        );

        cristal_object.transform.localPosition = new Vector3(0f, 5.62f, -50.11f);
        cristal_object.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        cristal_object.transform.localScale = Vector3.one * 150f;

        if (slotIndex >= 0 && slotIndex < 3)
            SpawnedCristals_P2[slotIndex] = cristal_object;
    }

    private void DestroyCristal_P2(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;

        var go = SpawnedCristals_P2[slotIndex];
        if (go)
        {
            Destroy(go);
            SpawnedCristals_P2[slotIndex] = null;
        }
    }

    private void StopPendingDestroy_P2(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;

        if (_pendingDestroy_P2[slotIndex] != null)
        {
            StopCoroutine(_pendingDestroy_P2[slotIndex]);
            _pendingDestroy_P2[slotIndex] = null;
        }
    }

    // Fallback: falls Altlasten existieren (z.B. mehrere Cristals schon im Placeholder),
    // versuchen wir sie zu entfernen. Das ist bewusst defensiv.
    private void ClearCristalChildrenFallback(GameObject placeholderGO)
    {
        if (!placeholderGO) return;

        var kill = new List<GameObject>();

        foreach (Transform t in placeholderGO.transform)
        {
            if (!t) continue;

            string n = t.name.ToLowerInvariant();
            if (n.Contains("cristal") || n.Contains("crystal") || n.Contains("gem"))
            {
                kill.Add(t.gameObject);
                continue;
            }

            // optional: sehr defensiv – wenn Prefab-Namen unbekannt sind, kannst du Clone generell killen:
            // if (t.name.Contains("(Clone)")) kill.Add(t.gameObject);
        }

        for (int i = 0; i < kill.Count; i++)
            Destroy(kill[i]);
    }

    // =====================================================================
    //  (Dein alter Name bleibt erhalten, wird aber nicht mehr direkt genutzt)
    // =====================================================================
    private void Spawn_New_Cristal(GameObject cristal_holder, GameObject cristal_new_parent_object)
    {
        // Legacy Wrapper (falls irgendwo noch aufgerufen):
        // Wir können hier ohne SlotIndex NICHT sauber tracken.
        // Daher: erst Fallback cleanup, dann spawn (ohne Tracking).
        ClearCristalChildrenFallback(cristal_new_parent_object);

        var disp = cristal_holder.GetComponent<DeckCardDisplay>();
        if (!disp || disp.card == null || disp.card.FieldCardData == null || disp.card.FieldCardData.cristal_prefab == null)
            return;

        GameObject cristal_object = Instantiate(
            disp.card.FieldCardData.cristal_prefab,
            cristal_new_parent_object.transform
        );

        cristal_object.transform.localPosition = new Vector3(0f, 5.62f, -50.11f);
        cristal_object.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        cristal_object.transform.localScale = Vector3.one * 150f;
    }

    // ================================
    // UI BUTTON API (P2)
    // ================================

    // sofort (ohne Partikel/Delay) Platz freimachen
    public void DeactivateFieldCardSlotP2_Instant(int slotIndex)
    {
        if (!IsValidSlotP2(slotIndex)) return;

        StopPendingDestroy_P2(slotIndex);

        var ph = FieldCard_PlaceHolders_P2[slotIndex];
        if (!ph) return;

        // Cristal weg + (optional) Altlasten weg
        DestroyCristal_P2(slotIndex);
        ClearCristalChildrenFallback(ph);

        ph.SetActive(false);

        if (FieldCardController_P2.instance)
            FieldCardController_P2.instance.SetActiveFieldCard_AtIndex(Empty_PlaceHolder_FieldEffect, slotIndex);
    }

    // mit deiner vorhandenen Animation/Delay
    public void DeactivateFieldCardSlotP2_WithFX(int slotIndex)
    {
        if (!IsValidSlotP2(slotIndex)) return;

        // nutzt deine bereits saubere Destroy-Logik (inkl. Cristal-Cleanup)
        DestroyP2_FieldCard_AbsoluteIndex(slotIndex);
    }

    // kleine Helper
    private bool IsValidSlotP2(int slotIndex)
    {
        if (FieldCard_PlaceHolders_P2 == null) return false;
        if (slotIndex < 0 || slotIndex >= FieldCard_PlaceHolders_P2.Length) return false;
        return true;
    }
}
