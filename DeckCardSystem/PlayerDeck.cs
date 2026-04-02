using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections; // für IEnumerator (Coroutines!)
using System.Reflection;
using TMPro;

[DisallowMultipleComponent]
public class PlayerDeck : MonoBehaviour
{
    public static PlayerDeck current;

    [Header("Deck Count UI")]
    [Tooltip("TMP Text, das die verbleibenden Karten im Deck anzeigt.")]
    public TMP_Text deckCountText;

    [Tooltip("Optionaler Prefix, z.B. 'Deck: '")]
    public string deckCountPrefix = "Deck: ";

    // =====================================================
    // 0) Deck Sources (Main + 3 BackDecks)
    // =====================================================
    public enum DeckSource
    {
        MainDeck = 0,
        BackDeck_1 = 1,
        BackDeck_2 = 2,
        BackDeck_3 = 3
    }

    [Header("Deck Auswahl (Startup)")]
    [Tooltip("Welche Liste soll beim Start als aktives Deck in 'deckCards' übernommen werden?")]
    public DeckSource startupDeckSource = DeckSource.MainDeck;

    [Header("Deck (Main) - bleibt als Hauptdeck im Inspector")]
    public List<GameObject> deckCards = new List<GameObject>();

    [Header("Back Decks (3 Presets)")]
    public List<GameObject> backDeck_1 = new List<GameObject>();
    public List<GameObject> backDeck_2 = new List<GameObject>();
    public List<GameObject> backDeck_3 = new List<GameObject>();

    // interne Templates (damit "MainDeck" im Inspector nicht kaputt-gespielt wird)
    private List<GameObject> _mainDeckTemplate;
    private List<GameObject> _back1Template;
    private List<GameObject> _back2Template;
    private List<GameObject> _back3Template;

    [Header("Hand Limit")]
    [Tooltip("Maximale Kartenanzahl in der Hand. Wenn bereits >= Limit, wird Ziehen abgebrochen.")]
    public int maxHandCards = 4;

    [Header("Zielcontainer für Handkarten (RectTransform)")]
    public RectTransform playerHand;         // normale Hand (offene Karten)
    public RectTransform playerHand_mirror;  // gespiegelte Hand (verdeckte Karten)

    [Header("Linear-Layout (zentriert, ohne LayoutGroup)")]
    public float cardSpacing = 20f;
    public float defaultCardWidth = 220f;
    public float handBaselineYOffset = 0f;

    [Header("Animation")]
    public bool animate = true;
    public float animDuration = 0.15f;
    public float flyInYOffset = -60f;
    public float startScale = 0.95f;
    public LeanTweenType easeType = LeanTweenType.easeOutQuad;
    public bool extraSmoothing = true;

    [Header("Z-Layering (Screen Space - Camera empfohlen)")]
    public bool useZLayering = true;
    public float baseZ = 0f;
    public float zOffsetStep = -0.0025f;
    public bool orderSiblingsLeftToRight = true;

    [Header("Fächer-Layout (optional)")]
    public bool useFanLayout = true;
    public float fanMaxAngle = 8f;
    public float fanMaxYOffset = 18f;
    [Range(0f, 2f)] public float fanCurvePower = 1.0f;

    [Header("Guards / Debug-Hilfen")]
    public bool hardCenterSingleCard = true;
    public bool normalizeAnchorsEachReflow = true;

    [Tooltip("Falls true: eine einheitliche Kartenbreite verwenden (robust & perfekt mittig).")]
    public bool useUniformCardWidth = true;

    [Tooltip("Sorgt optional dafür, dass die Hand selbst mittig referenziert ist (Pivot/Anchors 0.5/0.5).")]
    public bool normalizeHandAnchors = false;

    [Header("Mirror-Layer")]
    [Tooltip("Layer-Name für gespiegelte Karten (muss in Unity definiert sein).")]
    public string mirrorLayerName = "P2-GUI-Layer";
    [Tooltip("Wenn aktiv, werden alle gespiegelten Karten rekursiv auf diesen Layer gesetzt.")]
    public bool applyMirrorLayer = true;
    private int _mirrorLayer = -1;

    [Header("UI-Layer pro Hand")]
    [Tooltip("Layer-Name für Karten in P1-Hand.")]
    public string p1GuiLayerName = "P1-GUI-Layer";
    [Tooltip("Layer-Name für Karten in P2-Hand.")]
    public string p2GuiLayerName = "P2-GUI-Layer";

    private int _p1GuiLayer = -1;
    private int _p2GuiLayer = -1;

    // --- Caches (pro Instanz) ---
    private readonly HashSet<RectTransform> _newCards = new HashSet<RectTransform>(8);
    private readonly List<RectTransform> _children = new List<RectTransform>(16);
    private readonly List<float> _widths = new List<float>(16);
    private readonly List<Vector2> _targets = new List<Vector2>(16);
    private readonly List<float> _fanAngles = new List<float>(16);
    private readonly List<float> _fanY = new List<float>(16);

    // Nur unsere Layout-Tween-IDs pro Karte (kein globales Cancel)
    private readonly Dictionary<RectTransform, int> _layoutTweenId = new Dictionary<RectTransform, int>(16);

    private void Awake()
    {
        if (deckCards.Count == 0)
            Debug.LogWarning("[PlayerDeck] Deck ist leer – bitte im Inspector Karten hinzufügen!");
        if (!playerHand)
            Debug.LogWarning("[PlayerDeck] PlayerHand ist nicht zugewiesen – bitte im Inspector setzen!");
        if (!playerHand_mirror)
            Debug.LogWarning("[PlayerDeck] PlayerHand_Mirror ist nicht zugewiesen – Spiegel-Hand ist optional, aber empfohlen.");

        // Mirror-Layer auflösen
        if (applyMirrorLayer)
        {
            _mirrorLayer = LayerMask.NameToLayer(mirrorLayerName);
            if (_mirrorLayer == -1)
            {
                Debug.LogWarning($"[PlayerDeck] Mirror-Layer \"{mirrorLayerName}\" existiert nicht. " +
                                 $"Bitte in Project Settings > Tags and Layers anlegen oder applyMirrorLayer deaktivieren.");
            }
        }

        current = this;

        // UI-Layer IDs auflösen
        _p1GuiLayer = LayerMask.NameToLayer(p1GuiLayerName);
        _p2GuiLayer = LayerMask.NameToLayer(p2GuiLayerName);
        if (_p1GuiLayer == -1) Debug.LogWarning($"[PlayerDeck] UI-Layer '{p1GuiLayerName}' nicht gefunden.");
        if (_p2GuiLayer == -1) Debug.LogWarning($"[PlayerDeck] UI-Layer '{p2GuiLayerName}' nicht gefunden.");

        // Templates cachen (damit du jederzeit sauber "Main" oder BackDecks als Quelle verwenden kannst)
        _mainDeckTemplate = new List<GameObject>(deckCards);
        _back1Template = new List<GameObject>(backDeck_1);
        _back2Template = new List<GameObject>(backDeck_2);
        _back3Template = new List<GameObject>(backDeck_3);
    }

    private void Start()
    {
        UpdateDeckCountUI();
        // Beim Start deckCards mit der ausgewählten Quelle überschreiben
        ApplyDeckSourceToRuntimeDeck(startupDeckSource);
    }

    private void Update()
    {
        // "ganz easy" via Update: zählt immer live
        UpdateDeckCountUI();
    }

    public void UpdateDeckCountUI()
    {
        if (!deckCountText) return;

        int count = (deckCards != null) ? deckCards.Count : 0;
        deckCountText.text = deckCountPrefix + count.ToString();
    }

    #region Deck Source API

    /// <summary>
    /// Wählt ein BackDeck (oder Main) und überschreibt deckCards (Runtime-Deck) sofort.
    /// </summary>
    public void SelectDeckSourceNow(DeckSource source)
    {
        startupDeckSource = source;
        ApplyDeckSourceToRuntimeDeck(source);
    }

    private void ApplyDeckSourceToRuntimeDeck(DeckSource source)
    {
        List<GameObject> src = GetDeckSourceList(source);
        deckCards.Clear();

        if (src == null || src.Count == 0)
        {
            Debug.LogWarning($"[PlayerDeck] DeckSource '{source}' ist leer/NULL. deckCards bleibt leer.");
            return;
        }

        // Kopie als Runtime-Deck
        deckCards.AddRange(src);
    }

    private List<GameObject> GetDeckSourceList(DeckSource source)
    {
        // Wichtig: wir nehmen die Templates, nicht die live-Listen, damit Ziehen/RemoveAt nicht dein Inspector-Preset zerstört.
        switch (source)
        {
            case DeckSource.MainDeck: return _mainDeckTemplate;
            case DeckSource.BackDeck_1: return _back1Template;
            case DeckSource.BackDeck_2: return _back2Template;
            case DeckSource.BackDeck_3: return _back3Template;
            default: return _mainDeckTemplate;
        }
    }

    #endregion

    #region Public API
    public void GiveCard(int amount)
    {
        if (!playerHand) return;
        StartCoroutine(GiveCardRoutine(amount));
    }

    private IEnumerator GiveCardRoutine(int amount)
    {
        _newCards.Clear();

        int actuallyDrawn = 0;

        for (int i = 0; i < amount; i++)
        {
            // --- Handlimit Guard ---
            int handCount = playerHand ? playerHand.childCount : 0;
            if (handCount >= maxHandCards)
            {
                // Abbruch: NICHT weiterziehen
                yield break;
            }

            if (deckCards.Count == 0)
            {
                Debug.Log("[PlayerDeck] Keine Karten mehr im Deck!");
                break;
            }

            AudioManager.Instance?.PlaySFX2D("Give_Card");

            int rnd = Random.Range(0, deckCards.Count);
            GameObject prefab = deckCards[rnd];
            deckCards.RemoveAt(rnd);

            // 1) Normale Karte (offen) in playerHand
            GameObject instNormal = Instantiate(prefab, playerHand);
            var rtNormal = instNormal.GetComponent<RectTransform>();
            if (!rtNormal) rtNormal = instNormal.AddComponent<RectTransform>();
            ForceVisualPixelPerfect(instNormal);
            PrepareNewCardRect(rtNormal);

            InjectDeckReference(instNormal, this);
            ApplyLayerForHand(instNormal, playerHand);

            // Visual/BackFrame umschalten für "offen"
            SetCardFace(instNormal, faceUp: true);

            // 2) Gespiegelte Karte (verdeckt) in playerHand_mirror
            RectTransform rtMirror = null;
            GameObject instMirror = null;
            if (playerHand_mirror)
            {
                instMirror = Instantiate(prefab, playerHand_mirror);
                rtMirror = instMirror.GetComponent<RectTransform>();
                if (!rtMirror) rtMirror = instMirror.AddComponent<RectTransform>();
                PrepareNewCardRect(rtMirror);

                // Richtige Referenzen im Mirror-Fall!
                InjectDeckReference(instMirror, this);
                ApplyLayerForHand(instMirror, playerHand_mirror);

                // Visual/BackFrame umschalten für "verdeckt"
                SetCardFace(instMirror, faceUp: false);

                // Mirror-Layer anwenden (rekursiv)
                if (applyMirrorLayer && _mirrorLayer != -1)
                    SetLayerRecursively(instMirror, _mirrorLayer);

                // 🔒 Interaktivität der gespiegelten Karte vollständig deaktivieren
                EnsureCanvasGroupNonInteractive(instMirror);

                // Zwillinge verknüpfen
                var twinA = instNormal.AddComponent<MirroredTwin>();
                var twinB = instMirror.AddComponent<MirroredTwin>();
                twinA.Other = rtMirror;
                twinB.Other = rtNormal;

                _newCards.Add(rtMirror);
            }

            _newCards.Add(rtNormal);

            // Reflow beider Hände
            ReflowInternalForHand(playerHand, animate, _newCards);
            if (playerHand_mirror)
                ReflowInternalForHand(playerHand_mirror, animate, _newCards);

            actuallyDrawn++;

            // kurze Verzögerung wie bisher
            yield return new WaitForSeconds(0.15f);
        }

        // finaler Reflow
        ReflowInternalForHand(playerHand, animate, _newCards);
        if (playerHand_mirror)
            ReflowInternalForHand(playerHand_mirror, animate, _newCards);

        // API-CALL zum FieldCardController: nur die wirklich gezogenen Karten melden
        if (actuallyDrawn > 0)
        {
            if (playerHand.name == "P1-Hand")
            {
                FieldCardController.instance.Player_GetsDeckCards("Self", actuallyDrawn);
                FieldCardController_P2.instance.Player_GetsDeckCards("Opponent", actuallyDrawn);
            }
            if (playerHand.name == "P2-Hand")
            {
                FieldCardController.instance.Player_GetsDeckCards("Opponent", actuallyDrawn);
                FieldCardController_P2.instance.Player_GetsDeckCards("Self", actuallyDrawn);
            }
        }
    }

    private void InjectDeckReference(GameObject root, PlayerDeck owner)
    {
        if (!root || !owner) return;

        // am Root
        var disp = root.GetComponent<TCG.DeckCardDisplay>();
        if (disp) disp.InjectDeck(owner);

        // sicherheitshalber auch in Kindern (falls Display tiefer sitzt)
        var disps = root.GetComponentsInChildren<TCG.DeckCardDisplay>(true);
        for (int i = 0; i < disps.Length; i++)
            if (disps[i] != disp) disps[i].InjectDeck(owner);
    }

    private void ApplyLayerForHand(GameObject cardGO, RectTransform hand)
    {
        if (!cardGO || !hand) return;

        // Hand-Name gegenprüfen (robust auf Teilstrings)
        string handName = hand.name ?? string.Empty;
        int layerToApply = -1;

        // Priorität: explizit "P1-Hand" / "P2-Hand" im Namen
        if (handName.Contains("P1-Hand"))
            layerToApply = _p1GuiLayer;
        else if (handName.Contains("P2-Hand"))
            layerToApply = _p2GuiLayer;
        else
        {
            // Fallback: Zuordnung anhand Referenzgleichheit
            if (hand == playerHand) layerToApply = _p1GuiLayer;
            else if (hand == playerHand_mirror) layerToApply = _p2GuiLayer;
        }

        if (layerToApply == -1) return; // Layer nicht gefunden → nichts tun

        SetLayerRecursively(cardGO, layerToApply);
    }

    public void RemoveCard(RectTransform card)
    {
        if (!card) return;

        // Optional: Partner in der Mirror-Hand ebenfalls entfernen
        var twin = card.GetComponent<MirroredTwin>();
        if (twin && twin.Other)
        {
            if (twin.Other) Destroy(twin.Other.gameObject);
        }

        Destroy(card.gameObject);

        if (playerHand) ReflowInternalForHand(playerHand, animate, null);
        if (playerHand_mirror) ReflowInternalForHand(playerHand_mirror, animate, null);
    }

    public void ClearHand()
    {
        if (playerHand)
        {
            for (int i = playerHand.childCount - 1; i >= 0; i--)
                Destroy(playerHand.GetChild(i).gameObject);
        }

        if (playerHand_mirror)
        {
            for (int i = playerHand_mirror.childCount - 1; i >= 0; i--)
                Destroy(playerHand_mirror.GetChild(i).gameObject);
        }
        // kein Reflow nötig
    }

    public void ReflowNow(bool animateReflow = true)
    {
        if (playerHand) ReflowInternalForHand(playerHand, animateReflow, null);
        if (playerHand_mirror) ReflowInternalForHand(playerHand_mirror, animateReflow, null);
    }

    public void ReOrderCards() => ReflowNow(true);

    public void ShuffleDeck()
    {
        for (int i = deckCards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (deckCards[i], deckCards[j]) = (deckCards[j], deckCards[i]);
        }
    }
    #endregion

    #region Core Layout (pro Hand)
    public void ReflowInternalForHand(RectTransform hand, bool animateReflow, HashSet<RectTransform> newCards)
    {
        if (!hand) return;

        // Safety: falls es die Mirror-Hand ist, alle Kinder erneut sicher nicht interaktiv schalten
        if (playerHand_mirror && hand == playerHand_mirror)
        {
            for (int i = 0; i < hand.childCount; i++)
            {
                var go = hand.GetChild(i).gameObject;
                EnsureCanvasGroupNonInteractive(go);
            }
        }

        if (normalizeHandAnchors)
            EnsureCenteredAnchors(hand);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(hand);

        _children.Clear();
        _widths.Clear();

        int nChildren = hand.childCount;
        for (int i = 0; i < nChildren; i++)
        {
            var rt = hand.GetChild(i) as RectTransform;
            if (!rt) continue;

            if (normalizeAnchorsEachReflow) EnsureCenteredAnchors(rt);

            _children.Add(rt);
        }

        int n = _children.Count;
        if (n == 0) return;

        // --- Breiten robust bestimmen ---
        float uniformW = 0f;
        if (useUniformCardWidth)
        {
            uniformW = GetCardWidthRobust(_children[0], defaultCardWidth);
            if (uniformW <= 0.01f)
            {
                Debug.LogWarning($"[PlayerDeck] Ermittelte Uniform-Breite ≈ 0. Fallback auf defaultCardWidth={defaultCardWidth}.");
                uniformW = defaultCardWidth;
            }
            _widths.Clear();
            for (int i = 0; i < n; i++) _widths.Add(uniformW);
        }
        else
        {
            _widths.Clear();
            for (int i = 0; i < n; i++)
            {
                float w = GetCardWidthRobust(_children[i], defaultCardWidth);
                if (w <= 0.01f) w = defaultCardWidth;
                _widths.Add(w);
            }
        }

        // --- Gesamtbreite ---
        float totalWidth = 0f;
        for (int i = 0; i < n; i++) totalWidth += _widths[i];
        if (n > 1) totalWidth += cardSpacing * (n - 1);

        // Linke Kante relativ zur Mitte der Hand
        float xLeft = -totalWidth * 0.5f;
        float baseY = handBaselineYOffset;

        // --- Zielpositionen (pivot-robust) ---
        _targets.Clear();
        for (int i = 0; i < n; i++)
        {
            RectTransform rt = _children[i];
            float w = _widths[i];
            float targetAPx = xLeft + rt.pivot.x * w;
            _targets.Add(new Vector2(targetAPx, baseY));
            xLeft += w + cardSpacing;
        }

        if (hardCenterSingleCard && n == 1)
            _targets[0] = new Vector2(0f, baseY);

        // --- Fan-Berechnung ---
        _fanAngles.Clear();
        _fanY.Clear();
        if (useFanLayout && n > 1)
        {
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Lerp(-1f, 1f, (n == 1) ? 0f : i / (float)(n - 1));
                float tPow = Mathf.Sign(t) * Mathf.Pow(Mathf.Abs(t), Mathf.Clamp(fanCurvePower, 0.0001f, 4f));
                float angle = fanMaxAngle * tPow;
                float yOff = fanMaxYOffset * (1f - Mathf.Abs(tPow));
                _fanAngles.Add(angle);
                _fanY.Add(yOff);
            }
        }
        else
        {
            for (int i = 0; i < n; i++) { _fanAngles.Add(0f); _fanY.Add(0f); }
        }

        // --- Sibling-Order (links -> rechts) ---
        if (orderSiblingsLeftToRight)
        {
            var orderIdx = new List<int>(n);
            for (int i = 0; i < n; i++) orderIdx.Add(i);
            orderIdx.Sort((a, b) => _targets[a].x.CompareTo(_targets[b].x));
            for (int rank = 0; rank < orderIdx.Count; rank++)
                _children[orderIdx[rank]].SetSiblingIndex(rank);
        }

        // --- Anwenden / Animieren ---
        for (int i = 0; i < n; i++)
        {
            var rt = _children[i];

            var lockComp = rt.GetComponent<ReflowLock>();
            if (lockComp != null && lockComp.busy)
                continue;

            Vector2 targetPos = _targets[i];
            targetPos.y += _fanY[i];

            Quaternion targetRot = Quaternion.Euler(0f, 0f, _fanAngles[i]);
            float targetZ = useZLayering ? baseZ + i * zOffsetStep : rt.localPosition.z;

            CancelLayoutTween(rt);

            if (animateReflow)
            {
                Vector2 startPos = rt.anchoredPosition;
                Quaternion startRot = rt.localRotation;
                Vector3 startScaleV = rt.localScale;
                Vector3 endScaleV = Vector3.one;

                var descr = LeanTween.value(rt.gameObject, 0f, 1f, animDuration)
                    .setEase(easeType)
                    .setIgnoreTimeScale(true)
                    .setOnUpdate((float t) =>
                    {
                        float e = extraSmoothing ? (1f - (1f - t) * (1f - t)) : t;

                        rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, e);
                        rt.localRotation = Quaternion.SlerpUnclamped(startRot, targetRot, e);

                        if (useZLayering)
                        {
                            var lp = rt.localPosition;
                            lp.z = targetZ;
                            rt.localPosition = lp;
                        }

                        if (newCards != null && newCards.Contains(rt))
                            rt.localScale = Vector3.LerpUnclamped(startScaleV, endScaleV, e);
                    })
                    .setOnComplete(() =>
                    {
                        rt.anchoredPosition = targetPos;
                        rt.localRotation = targetRot;
                        var lp = rt.localPosition; lp.z = targetZ; rt.localPosition = lp;

                        if (newCards != null && newCards.Contains(rt))
                            rt.localScale = Vector3.one;

                        _layoutTweenId.Remove(rt);
                    });

                _layoutTweenId[rt] = descr.id;
            }
            else
            {
                rt.anchoredPosition = targetPos;
                rt.localRotation = targetRot;
                var lp = rt.localPosition; lp.z = targetZ; rt.localPosition = lp;

                if (newCards != null && newCards.Contains(rt))
                    rt.localScale = Vector3.one;

                _layoutTweenId.Remove(rt);
            }
        }
    }
    #endregion

    #region Helpers
    private static void EnsureCenteredAnchors(RectTransform rt)
    {
        if (!rt) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    private void PrepareNewCardRect(RectTransform rt)
    {
        EnsureCenteredAnchors(rt);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = new Vector2(0f, handBaselineYOffset + (animate ? flyInYOffset : 0f));
        if (animate) rt.localScale = Vector3.one * startScale;

        if (useZLayering)
        {
            var lp = rt.localPosition;
            lp.z = baseZ;
            rt.localPosition = lp;
        }
    }

    /// <summary>
    /// Schaltet die Kinder "Visual" und "BackFrame" je nach faceUp um.
    /// </summary>
    private void SetCardFace(GameObject cardRoot, bool faceUp)
    {
        if (!cardRoot) return;
        Transform visual = cardRoot.transform.Find("Visual");
        Transform back = cardRoot.transform.Find("BackFrame");

        if (visual) visual.gameObject.SetActive(faceUp);
        if (back) back.gameObject.SetActive(!faceUp);
    }

    /// <summary>
    /// Setzt den Layer rekursiv auf dem Objekt und all seinen Kindern.
    /// </summary>
    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (!go || layer < 0) return;
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            if (child) SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Sehr robuste Breiten-Ermittlung für UI-Karten:
    /// 1) PreferredSize, 2) Rect.width, 3) größtes Kinder-Graphic, 4) RelativeBounds, 5) Fallback.
    /// </summary>
    private float GetCardWidthRobust(RectTransform rt, float fallback)
    {
        if (!rt) return fallback;

        float w = LayoutUtility.GetPreferredSize(rt, 0);
        if (w > 0.01f) return w;

        w = rt.rect.width;
        if (w > 0.01f) return w;

        float maxChildGraphic = 0f;
        var graphics = rt.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var gr = graphics[i];
            if (!gr || !gr.rectTransform) continue;
            float gw = gr.rectTransform.rect.width;
            if (gw > maxChildGraphic) maxChildGraphic = gw;
        }
        if (maxChildGraphic > 0.01f) return maxChildGraphic;

        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rt);
        if (bounds.size.x > 0.01f) return bounds.size.x;

        return fallback;
    }

    private void CancelLayoutTween(RectTransform rt)
    {
        if (rt == null) return;
        if (_layoutTweenId.TryGetValue(rt, out int id))
        {
            if (LeanTween.isTweening(id))
                LeanTween.cancel(id);
            _layoutTweenId.Remove(rt);
        }
    }

    /// <summary>
    /// Stellt sicher, dass es eine CanvasGroup gibt und deaktiviert Interaktivität.
    /// </summary>
    private void EnsureCanvasGroupNonInteractive(GameObject cardGO)
    {
        if (!cardGO) return;
        var cg = cardGO.GetComponent<CanvasGroup>();
        if (!cg) cg = cardGO.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private IEnumerator ForceVisualPixelPerfect_IEnumerator(GameObject cardRoot, float onSeconds = 0.5f, bool useRootCanvas = true)
    {
        if (!cardRoot) yield break;

        // "Visual" finden
        var visual = cardRoot.transform.Find("Visual");
        if (!visual) yield break;

        // Canvas holen/erstellen
        var cv = visual.GetComponent<Canvas>();
        if (!cv) cv = visual.gameObject.AddComponent<Canvas>();

        // Ziel-Canvas (optional RootCanvas)
        var target = useRootCanvas && cv.rootCanvas ? cv.rootCanvas : cv;
        if (!target) yield break;

        // Reflection: overridePixelPerfect optional setzen
        var prop = typeof(Canvas).GetProperty("overridePixelPerfect", BindingFlags.Instance | BindingFlags.Public);

        // Einschalten
        if (prop != null && prop.CanWrite)
            prop.SetValue(target, true, null);

        target.pixelPerfect = true;

        // Warten
        yield return new WaitForSeconds(onSeconds);

        // Falls Objekt in der Zwischenzeit zerstört wurde
        if (!target) yield break;

        // Ausschalten
        if (prop != null && prop.CanWrite)
            prop.SetValue(target, false, null);

        target.pixelPerfect = false;
    }

    public void ForceVisualPixelPerfect(GameObject cardRoot)
    {
        StartCoroutine(ForceVisualPixelPerfect_IEnumerator(cardRoot, 0.5f, true));
    }

    #endregion
}

/// <summary>
/// Verknüpft eine Karte mit ihrer gespiegelten Gegenkarte,
/// damit man später beide gemeinsam löschen/verschieben kann.
/// </summary>
public class MirroredTwin : MonoBehaviour
{
    public RectTransform Other;
}

/// <summary>
/// Optionales Lock-Flag für externe Ziel-Animationen.
/// </summary>
public class ReflowLock : MonoBehaviour
{
    public bool busy;
}
