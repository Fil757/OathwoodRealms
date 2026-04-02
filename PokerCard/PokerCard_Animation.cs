using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokerCard_Animation : MonoBehaviour
{
    public static PokerCard_Animation current;

    public enum PlayerSide { P1, P2 }

    [System.Serializable]
    public class SideConfig
    {
        [Header("Scene Roots & Hubs")]
        [Tooltip("Parent mit den Figuren der Seite (wird sonst per Pfad gesucht).")]
        public Transform figuresParentOverride;
        [Tooltip("Pfad-Backup, falls Override leer ist.")]
        public string figuresParentPath = "Game-Canvas/P1_Figures";
        [Tooltip("UI-Container, in den die Pokerkarten instanziert werden.")]
        public RectTransform pokerCardHub;
        [Tooltip("Fallback-Ziel wenn kein Match auf ein Figure Sign gefunden wird.")]
        public GameObject dummyField;

        [Header("Spawn / Stack (lokal im Hub)")]
        public Vector3 spawnLocalPos = new Vector3(-800f, -400f, 0f);
        [Tooltip("Negativ nach unten stapeln, Positiv nach oben.")]
        public float stackOffsetY = 50f;
        [Tooltip("±X-Zufall beim Stacken (lokal).")]
        public float randomXRange = 25f;
        [Tooltip("±Z-Rotation (Grad) für leichte Kartenrotation im Stack.")]
        public float randomRotZ = 6f;

        [Header("Flight (zum Stack-Ziel)")]
        [Min(0.01f)] public float moveToStackDuration = 0.6f;
        [Min(0f)] public float delayBetweenCards = 0.2f;

        [Header("Flight (Stack -> Figur)")]
        [Min(0.01f)] public float flyToFigureDuration = 0.2f;
        [Min(0f)] public float afterStackDelayBeforeFly = 0.62f;

        [Header("Visuals (Spiegel-Seite)")]
        [Tooltip("Drehe die Karten im Hub (z.B. 180° um Z) für die Gegenseite.")]
        public Vector3 hubCardEulerOffset = Vector3.zero;
    }

    [Header("Prefabs & Shared")]
    [Tooltip("Pokerkarten-Prefab mit PokerCard_Display.")]
    public GameObject pokerCardPrefab;

    [Header("P1 Config")]
    public SideConfig P1 = new SideConfig
    {
        figuresParentPath = "Game-Canvas/P1_Figures",
        spawnLocalPos = new Vector3(-800f, -400f, 0f),
        stackOffsetY = 50f,
        randomXRange = 25f,
        randomRotZ = 6f,
        moveToStackDuration = 0.6f,
        delayBetweenCards = 0.2f,
        flyToFigureDuration = 0.2f,
        afterStackDelayBeforeFly = 0.62f,
        hubCardEulerOffset = Vector3.zero
    };

    [Header("P2 Config")]
    public SideConfig P2 = new SideConfig
    {
        figuresParentPath = "Game-Canvas/P2_Figures",
        spawnLocalPos = new Vector3(800f, 400f, 0f), // Beispiel: spiegelverkehrt
        stackOffsetY = 50f,
        randomXRange = 25f,
        randomRotZ = 6f,
        moveToStackDuration = 0.6f,
        delayBetweenCards = 0.2f,
        flyToFigureDuration = 0.2f,
        afterStackDelayBeforeFly = 0.62f,
        // Karten „kopfüber“ darstellen; passe nach Bedarf an (Z=180 dreht die Karte).
        hubCardEulerOffset = new Vector3(0f, 0f, 180f)
    };

    [Header("Debug")]
    public PlayerSide defaultSideForLegacyAPI = PlayerSide.P1;
    [SerializeField] private GameObject[] Figures_P1;
    [SerializeField] private GameObject[] Figures_P2;

    private void Awake()
    {
        current = this;
        RefreshFiguresFromScene(PlayerSide.P1);
        RefreshFiguresFromScene(PlayerSide.P2);
    }

    // --- Neues API mit Seite ---

    public void SpecialMove_DrawPokerCards(int count, GameObject targetedPlayer)
    {
        if (targetedPlayer.name == "P1-Base") { SpawnPokerCards_P1(count); }
        if (targetedPlayer.name == "P2-Base") { SpawnPokerCards_P2(count); }
    }

    public void SpawnPokerCards_P1(int count)
    {
        SpawnPokerCards(count, PlayerSide.P1);

        // API-CALL zum FieldCardController
        FieldCardController.instance.Player_GetsPokerCards("Self", true);
        FieldCardController_P2.instance.Player_GetsPokerCards("Opponent", true);
    }

    public void SpawnPokerCards_P2(int count)
    {
        SpawnPokerCards(count, PlayerSide.P2);

        // API-CALL zum FieldCardController
        FieldCardController_P2.instance.Player_GetsPokerCards("Self", true);
        FieldCardController.instance.Player_GetsPokerCards("Opponent", true);
    }

    public void SpawnPokerCards(int count, PlayerSide side)
    {
        var cfg = Get(side);
        if (!pokerCardPrefab || !cfg.pokerCardHub)
        {
            Debug.LogWarning("[PokerCard_Animation] Prefab oder Hub fehlen.");
            return;
        }

        RefreshFiguresFromScene(side);

        // Stack-Animation im Hub (lokale Koordinaten)
        for (int i = 0; i < count; i++)
        {
            var card = Instantiate(pokerCardPrefab, cfg.pokerCardHub);
            card.transform.localPosition = cfg.spawnLocalPos;

            // per-Card zufällige Startrotation (Stackoptik) + Seitenrotation addieren
            float rotZ = Random.Range(-cfg.randomRotZ, cfg.randomRotZ) + cfg.hubCardEulerOffset.z;
            Vector3 eul = cfg.hubCardEulerOffset; 
            eul.z = rotZ;
            card.transform.localRotation = Quaternion.Euler(eul);

            // Init & Value/Sign
            var display = card.GetComponent<PokerCard_Display>();
            if (display != null) display.InitCard();

            // Ziel im Stack (lokal)
            float targetX = Random.Range(-cfg.randomXRange, cfg.randomXRange);
            float targetY = -i * (cfg.stackOffsetY + Random.Range(-20f, 20f));
            Vector3 targetLocal = new Vector3(targetX, targetY, 0f);

            // Verzögerung pro Karte
            float delay = i * cfg.delayBetweenCards;

            // Ziel-Figur nach Sign bestimmen
            GameObject targetFigure = ResolveTargetBySign(side, display ? display.poker_sign : null);
            if (!targetFigure) targetFigure = cfg.dummyField;

            // 1) in den Stapel gleiten
            LeanTween.moveLocal(card, targetLocal, cfg.moveToStackDuration)
                    .setDelay(delay)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnStart(() =>
                    {
                        // Soundeffekt des Aushändigens einer Karte (respektiert Delay)
                        AudioManager.Instance?.PlaySFX2D("Play_PokerCard");
                    })
                    .setOnComplete(() =>
                    {
                        // 2) kurze Pause -> zum Ziel fliegen
                        LeanTween.delayedCall(cfg.afterStackDelayBeforeFly, () =>
                        {
                            FlyCardToTarget(card, targetFigure, cfg.flyToFigureDuration, side);
                        });
                    });
        }
    }


    private SideConfig Get(PlayerSide side) => side == PlayerSide.P1 ? P1 : P2;

    private void RefreshFiguresFromScene(PlayerSide side)
    {
        SideConfig cfg = Get(side);

        Transform parent = cfg.figuresParentOverride;
        if (!parent && !string.IsNullOrEmpty(cfg.figuresParentPath))
        {
            var go = GameObject.Find(cfg.figuresParentPath);
            if (go) parent = go.transform;
        }

        if (!parent)
        {
            Debug.LogWarning($"[PokerCard_Animation] Figures parent für {side} nicht gefunden (Path='{cfg.figuresParentPath}').");
            if (side == PlayerSide.P1) Figures_P1 = null; else Figures_P2 = null;
            return;
        }

        int n = parent.childCount;
        var arr = new GameObject[n];
        for (int i = 0; i < n; i++)
            arr[i] = parent.GetChild(i).gameObject;

        if (side == PlayerSide.P1) Figures_P1 = arr; else Figures_P2 = arr;

    }

    private GameObject ResolveTargetBySign(PlayerSide side, string sign)
    {
        if (string.IsNullOrEmpty(sign)) return null;

        var figures = (side == PlayerSide.P1) ? Figures_P1 : Figures_P2;
        if (figures == null || figures.Length == 0) return null;

        for (int i = 0; i < figures.Length; i++)
        {
            var go = figures[i];
            if (!go) continue;

            var df = go.GetComponent<Display_Figure>();
            if (!df) continue;

            if (df.FIGURE_TYPE == sign) // „Heart“, „Spade“, „Club“, „Diamond“
                return go;
        }
        return null;
    }

    private void FlyCardToTarget(GameObject pokerCard, GameObject target, float duration, PlayerSide side)
    {
        if (!pokerCard || !target) { if (pokerCard) Destroy(pokerCard); return; }

        // Weltziel: leicht „unter“ das Ziel, damit Scale→0 nicht verdeckt
        Vector3 worldTarget = target.transform.position + new Vector3(0f, 0f, -100f);

        int value = 0;
        var disp = pokerCard.GetComponent<PokerCard_Display>();
        if (disp != null) value = disp.value;

        // Move (WELT)
        LeanTween.move(pokerCard, worldTarget, duration).setEase(LeanTweenType.easeInOutQuad);

        // Shrink parallel
        LeanTween.scale(pokerCard, Vector3.zero, duration)
                 .setEase(LeanTweenType.easeInOutQuad)
                 .setOnComplete(() =>
                 {
                     // Nur echte Figur (nicht Dummy) lädt
                     var cfg = Get(side);
                     if (target != cfg.dummyField && LoadToFigure.current != null)
                     {
                         LoadToFigure.current.LoadingToFigure(target, value);
                     }
                     Destroy(pokerCard);
                 });
    }

    // --- Legacy-Wrapper für alte Aufrufe (z.B. ArrayRefresher) ---
    [System.Obsolete("Use RefreshFiguresFromScene(PlayerSide side) instead.")]
    public void RefreshFiguresFromScene_PC()
    {
        // beide Seiten aktualisieren, damit alter Call weiter funktioniert
        RefreshFiguresFromScene(PlayerSide.P1);
        RefreshFiguresFromScene(PlayerSide.P2);
    }
}
