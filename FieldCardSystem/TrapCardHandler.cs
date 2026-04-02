using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomInspector;

[DisallowMultipleComponent] // Verhindert, dass dieses Skript mehrfach auf demselben GameObject hinzugefügt wird (Fehlerquelle vermeiden)
public class TrapCardHandler : MonoBehaviour // Definiert die Klasse TrapCardHandler als Unity-Komponente, die an GameObjects hängt
{
    public static TrapCardHandler instance; // Statische Referenz (Singleton-ähnlich), damit andere Skripte leicht darauf zugreifen können

    void Awake() // Unity-Lebenszyklusmethode: Wird aufgerufen, wenn das Script-Objekt instanziiert/geladen wird
    {
        instance = this; // Merkt sich die aktuelle Instanz, damit TrapCardHandler.instance überall erreichbar ist
    }

    [Header("Prefabs & Parent")] // Gruppiert die folgenden Felder im Inspector unter einer Überschrift
    [Tooltip("Das zu spawnende Kristall-Prefab. Sollte ein fertig konfiguriertes GameObject sein (Mesh/Renderer/Collider etc.).")] // Hilfetext im Inspector
    public GameObject trap_cristal_prefab; // Referenz auf das Kristall-Prefab, das instanziiert werden soll

    [Tooltip("Eltern-Objekt (Container) in dem die Kristalle als Kinder einsortiert werden. Dient als logische/visuelle Sammelstelle.")] // Erläutert die Bedeutung des Parent-Feldes
    public GameObject trap_cristal_field; // Parent-Container im Hierarchiebaum, unter dem neue Kristalle liegen sollen
    public GameObject trap_cristal_field_P2; // Parent-Container im Hierarchiebaum, unter dem neue Kristalle liegen sollen
    
    [Header("Spawn-Offset (Weltkoordinaten)")] // Neue Gruppe im Inspector für Positions-Offset
    [Tooltip("X-Offset für den Partikeleffekt relativ zur Weltposition des trap_cristal_field.")] // Erklärt die Wirkung des X-Offsets
    public float vector_x_spawn_offset; // Numerischer Versatz in X-Richtung für den Partikeleffekt

    [Tooltip("Y-Offset für den Partikeleffekt relativ zur Weltposition des trap_cristal_field.")] // Erklärt die Wirkung des Y-Offsets
    public float vector_y_spawn_offset; // Numerischer Versatz in Y-Richtung für den Partikeleffekt

    [Tooltip("Z-Offset für den Partikeleffekt relativ zur Weltposition des trap_cristal_field.")] // Erklärt die Wirkung des Z-Offsets
    public float vector_z_spawn_offset; // Numerischer Versatz in Z-Richtung für den Partikeleffekt

    [Header("VFX Einstellungen")] // Abschnitt für Partikeleffekt-Konfiguration
    [Tooltip("Index des Spawn-Partikeleffekts im ParticleController (z. B. 6 = Funke/Staub bei Erzeugung).")]
    public int spawnParticleIndex = 6; // Standardindex für den Effekt bei Erzeugung (entspricht deinem bisherigen 6)

    [Tooltip("Index des Zerstörungs-Partikeleffekts im ParticleController (z. B. 8 = Zerplatzen/Glitzern beim Entfernen).")]
    public int destroyParticleIndex = 8; // Standardindex für den Effekt bei Zerstörung (entspricht deinem bisherigen 8)

    [Tooltip("Skalierung des Spawn-Partikeleffekts (z. B. 30,30,30 wie in deinem Code).")]
    public Vector3 spawnParticleScale = new Vector3(30f, 30f, 30f); // Vektor zur Kontrolle der Größe des Spawn-VFX

    [Tooltip("Skalierung des Zerstörungs-Partikeleffekts (z. B. 250,250,250 wie in deinem Code).")]
    public Vector3 destroyParticleScale = new Vector3(250f, 250f, 250f); // Vektor zur Kontrolle der Größe des Destroy-VFX

    [Tooltip("Rotation des Spawn-Partikeleffekts (z. B. -90° um X, damit viele VFX flach auf dem Board liegen).")]
    public Vector3 spawnParticleEuler = new Vector3(-90f, 0f, 0f); // Eulersche Winkel für die Ausrichtung des Spawn-VFX

    [Tooltip("Rotation des Zerstörungs-Partikeleffekts (meist identity / 0,0,0 ausreichend).")]
    public Vector3 destroyParticleEuler = Vector3.zero; // Eulersche Winkel für die Ausrichtung des Destroy-VFX

    [Header("Filter-Einstellungen")]
    [Tooltip("Nur Kinder mit diesem Tag werden als 'echte' Fallen-Kristalle betrachtet. Leer lassen, um alle Kinder zu erlauben.")]
    public string crystalTag = "TrapCrystal"; // Tag-Name, um Kandidaten im Parent-Container zu filtern (verhindert versehentliches Löschen falscher Objekte)

    [Tooltip("Falls kein Kind den Tag trägt: Soll stattdessen das erste beliebige Kind zerstört werden? (Safety-Fallback)")]
    public bool fallbackDestroyAnyChildIfNoTagMatch = false; // Optionale Sicherheits-Option, um nie in einem 'do nothing'-Zustand zu bleiben

    public void SpawnTrapCristal(string player) // Öffentliche Methode: erzeugt einen neuen Kristall und spielt einen Spawn-Effekt ab
    {

        GameObject cristal_field = null;

        if(player == "P1"){cristal_field = trap_cristal_field;}
        if(player == "P2"){cristal_field = trap_cristal_field_P2;}

        GameObject cristalInstance = Instantiate( // Erzeugt eine neue Instanz des Prefabs in der Szene
            trap_cristal_prefab, // Das originale Prefab, das geklont wird
            cristal_field.transform // Setzt den Parent direkt beim Instanziieren, damit Hierarchie & lokale Transformationen passen
        );

        // Optional: Sicherstellen, dass die lokale Transformation initial "neutral" ist (vermeidet Überraschungen durch Prefab-Offsets)
        cristalInstance.transform.localPosition = new Vector3(0f, 0f, -33f); // Setzt die lokale Position relativ zum Parent auf 0,0,0
        cristalInstance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // Setzt die lokale Rotation relativ zum Parent auf keine Rotation
        // Hinweis: Lokale Skalierung bleibt die aus dem Prefab; bei Bedarf hier ebenfalls normalisieren

        if (ParticleController.Instance != null) // Prüft, ob ein ParticleController verfügbar ist (verhindert NullReference)
        {
            Vector3 spawnPos = cristal_field.transform.position + // Basis ist die Weltposition des Parents
                               new Vector3(vector_x_spawn_offset, vector_y_spawn_offset, vector_z_spawn_offset); // Plus einstellbarer Welt-Offset

            Quaternion spawnRot = Quaternion.Euler(spawnParticleEuler); // Wandelt die gewünschten Euler-Winkel in eine Quaternion-Rotation um

            ParticleController.Instance.PlayParticleEffect( // Fordert den Partikel-Controller auf, den Effekt zu erzeugen
                spawnPos, // Weltposition, an der der Effekt erscheinen soll
                spawnParticleIndex, // Welcher Effekt aus deiner Sammlung abgespielt werden soll
                spawnParticleScale, // Wie groß der Effekt skaliert werden soll
                spawnRot, // In welcher Orientierung der Effekt erscheinen soll
                null // Kein Parent: Effekt bleibt in der Welt verankert (verhindert ungewolltes Mitbewegen mit Hierarchie)
            );
        }
        else // Falls kein ParticleController aktiv ist
        {
            Debug.LogWarning("TrapCardHandler.SpawnTrapCristal: Kein ParticleController.Instance vorhanden – VFX wurde übersprungen."); // Hinweis zur Diagnose
        }
    }

    public void DestroyTrapCristal(string player)
    {
        Debug.Log($"[DestroyTrapCristal] Triggered by player: {player}");

        GameObject cristal_field = null;

        if (player == "P1")
        {
            cristal_field = trap_cristal_field;
            Debug.Log("[DestroyTrapCristal] cristal_field set to trap_cristal_field (P1)");
        }
        else if (player == "P2")
        {
            cristal_field = trap_cristal_field_P2;
            Debug.Log("[DestroyTrapCristal] cristal_field set to trap_cristal_field_P2 (P2)");
        }
        else
        {
            Debug.LogWarning($"[DestroyTrapCristal] Invalid player argument: {player}");
            return;
        }

        if (cristal_field == null)
        {
            Debug.LogError("[DestroyTrapCristal] cristal_field reference is NULL → cannot continue");
            return;
        }

        Transform parent = cristal_field.transform;
        Debug.Log($"[DestroyTrapCristal] Parent contains {parent.childCount} children.");

        List<Transform> candidates = new List<Transform>();

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Debug.Log($"[DestroyTrapCristal] Checking child: '{child.name}'");

            if (!string.IsNullOrEmpty(crystalTag))
            {
                if (child.CompareTag(crystalTag))
                {
                    Debug.Log($"[DestroyTrapCristal] → MATCH (tag = {crystalTag})");
                    candidates.Add(child);
                }
                else
                {
                    Debug.Log($"[DestroyTrapCristal] → SKIPPED (tag mismatch)");
                }
            }
            else
            {
                Debug.Log("[DestroyTrapCristal] No tag filter set → child automatically accepted");
                candidates.Add(child);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.Log("[DestroyTrapCristal] No candidates found with tag filter");

            if (fallbackDestroyAnyChildIfNoTagMatch && parent.childCount > 0)
            {
                Debug.Log("[DestroyTrapCristal] Fallback active → selecting first child instead");
                candidates.Add(parent.GetChild(0));
            }
            else
            {
                Debug.Log("[DestroyTrapCristal] No fallback allowed or no children available → ABORT");
                return;
            }
        }

        Transform selected = candidates[Random.Range(0, candidates.Count)];
        Debug.Log($"[DestroyTrapCristal] Selected crystal for destruction: '{selected.name}' (Index within candidates: random)");

        //Insert explosion here

        Vector3 scale = new Vector3(90f, 90f, 90f);
        Quaternion rot = Quaternion.Euler(-90f, 0f, 0f);

        ParticleController.Instance.PlayParticleEffect(
            selected.gameObject.transform.position,
            10,
            scale,
            rot
        );
        
        Destroy(selected.gameObject);

        Debug.Log("[DestroyTrapCristal] Completed");
    }

}
