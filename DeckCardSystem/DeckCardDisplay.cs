using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using System;

namespace TCG
{
    /// <summary>
    /// DeckCard UI (finale Version mit Mirror-Sync und FlightLayer-Fix):
    /// - Anzeige/Preview (Figure/Spell)
    /// - Klick -> animierter Flug (eigener Canvas) zum GAME-GUI-Central-Point (Offset via CardFlightConfig)
    /// - Kurz vor Ende des Shrinks -> Cast (nur bei Self-Flight)
    /// - Danach: Karte wandert in konfigurierten Graveyard (P1 oder P2)
    /// - Hand-Re-Order am Ende
    /// - Mirror: identischer "Ghost"-Flug ohne Cast, per SiblingIndex-Matching
    ///
    /// NEU (Pragmatischer Bot-Fix):
    /// - Bot-Karten im Flug werden grundsätzlich verdeckt gezeigt (BackFrame),
    ///   damit es keinen visuellen Mismatch mehr geben kann.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class DeckCardDisplay : MonoBehaviour, IPointerClickHandler
    {
        // ---------------- Mirror Relay ----------------
        /// <summary>
        /// Wird ausgelöst, wenn eine Karte visuell gespielt wird (vor dem eigentlichen Flug).
        /// Argumente: Source-Hand (Parent der Karte), SiblingIndex in dieser Hand.
        /// </summary>
        public static event System.Action<RectTransform, int> OnCardPlayedVisual;

        #region Inspector: Data & UI
        [Header("Data")]
        [SerializeField] public DeckCard card;

        public bool containsFigure;
        public bool containsField;
        public bool containsSpell;
        public bool containsTrap;

        [Header("UI References")]
        [SerializeField] public Image artworkImage;
        [SerializeField] public TextMeshProUGUI nameText;
        [SerializeField] public TextMeshProUGUI typeText;
        [SerializeField] public TextMeshProUGUI costText;
        [SerializeField] public TextMeshProUGUI costText_upside;
        [SerializeField] public TextMeshProUGUI descriptionText;
        [SerializeField] public TextMeshProUGUI variantText;

        [SerializeField] public Image costFrame;
        [SerializeField] public Image TypeImage;

        [Header("Figure Preview (optional)")]
        [SerializeField] public GameObject figureInfoRoot;
        [SerializeField] public TextMeshProUGUI atkText;
        [SerializeField] public TextMeshProUGUI defText;

        [Header("Spell Preview (optional)")]
        [SerializeField] public GameObject spellInfoRoot;
        [SerializeField] public TextMeshProUGUI spellTitleText;

        [Header("Interactivity")]
        [SerializeField] public Button button;

        [Header("Casting (Services)")]
        [SerializeField] public SpellCaster spellCaster;
        [SerializeField] public FigureCaster figureCaster;
        #endregion

        #region Inspector: After Use
        [Header("After Use")]
        [Tooltip("Wenn true, wandert die Karte nach Benutzung in den Graveyard.")]
        [SerializeField] private bool moveToGraveyard = true;

        [Tooltip("Zusätzliche Wartezeit nach Cast (meist 0, da Cast in Shrink-Phase erfolgt).")]
        [SerializeField] private float removeDelay = 0f;

        [Header("Deck")]
        [SerializeField] private PlayerDeck deck;
        #endregion

        #region Inspector: Flight
        [Header("Flight Config (Scene)")]
        [Tooltip("Wenn leer, wird CardFlightConfig.Instance verwendet.")]
        [SerializeField] private CardFlightConfig flightConfig;

        [Header("Flight Layer (optional)")]
        [Tooltip("Ebene im selben Canvas, in die die Karte vor dem Flug umparentet wird. Verhindert Layout-Glitches.")]
        [SerializeField] private RectTransform flightLayer; // Optional; Fallbacks siehe Awake()

        [Header("Mirror Flight Rotation")]
        [SerializeField] private bool mirrorRotateDuringFlight = true;

        // Für UI-Karten ist Z=180° meist korrekt (auf dem Kopf).
        // Wenn du horizontal spiegeln willst: Y=180°.
        [SerializeField] private Vector3 mirrorRotationEuler = new Vector3(0f, 0f, 180f);

        // Optional: sanftes Drehen in die Zielrotation
        [SerializeField] private bool mirrorRotateAnimate = false;
        #endregion

        #region Runtime
        private RectTransform rt;
        private CanvasGroup cg;
        private LayoutElement layoutElement; // zum ignoreLayout setzen
        private bool consumedOrPending;
        private bool isFlying;

        // Caches für PrepareForFlight()
        private Canvas parentCanvas;
        private Camera uiCam;
        private Vector2 cachedScreenPos;

        private Quaternion _mirrorPrevRot;
        private bool _mirrorRotApplied;
        #endregion

        #region Unity
        private void Awake()
        {
            rt = GetComponent<RectTransform>();

            cg = GetComponent<CanvasGroup>();
            if (!cg) cg = gameObject.AddComponent<CanvasGroup>(); // Guard

            layoutElement = GetComponent<LayoutElement>();
            if (!spellCaster) spellCaster = FindObjectOfType<SpellCaster>(true);
            if (!figureCaster) figureCaster = FindObjectOfType<FigureCaster>(true);
            if (!deck) deck = PlayerDeck.current ?? GameObject.Find("P1-Deck")?.GetComponent<PlayerDeck>();
            if (!flightConfig) flightConfig = CardFlightConfig.Instance;

            // FlightLayer-Fallbacks:
            if (!flightLayer)
            {
                var flyGO = GameObject.Find("P1-Hand-FlyLayer");
                if (flyGO) flightLayer = flyGO.transform as RectTransform;
            }
            if (!flightLayer)
            {
                // Root des eigenen Canvas als letzte Option
                var canvas = GetComponentInParent<Canvas>();
                if (canvas) flightLayer = canvas.transform as RectTransform;
            }

            RefreshUI();
        }

        private void OnEnable() => RefreshUI();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!rt) rt = GetComponent<RectTransform>();
            if (!cg) cg = GetComponent<CanvasGroup>();
            if (!layoutElement) layoutElement = GetComponent<LayoutElement>();
            RefreshUI();
        }
#endif
        #endregion

        #region UI & Data
        public void SetCard(DeckCard newCard)
        {
            card = newCard;
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (card == null)
            {
                SetVisible(figureInfoRoot, false);
                SetVisible(spellInfoRoot, false);
                SafeSet(nameText, "<no card>");
                SafeSet(typeText, "-");
                SafeSet(costText, "-");
                SafeSet(costText_upside, "-");
                SafeSet(descriptionText, "");
                if (artworkImage) artworkImage.sprite = null;
                SetInteractable(false);
                return;
            }

            // Basisdaten
            SafeSet(nameText, string.IsNullOrEmpty(card.DisplayNameOverride) ? card.name : card.DisplayNameOverride);
            SafeSet(typeText, card.CardType.ToString());
            SafeSet(costText, card.PlayerLoadCost.ToString());
            SafeSet(costText_upside, card.PlayerLoadCost.ToString());
            SafeSet(descriptionText, card.Description);
            SafeSet(variantText, card.CardVariant);
            if (artworkImage) artworkImage.sprite = card.ArtworkSprite;

            // Info-Blöcke
            SetVisible(figureInfoRoot, card.IsFigure);
            SetVisible(spellInfoRoot, card.IsSpell);

            if (card.IsFigure)
            {
                var disp = ResolveDisplayFigureForPreview(card);
                if (disp != null)
                {
                    int atk = (disp.FIGURE != null) ? disp.FIGURE.FIGURE_ATK : disp.FIGURE_ATK;
                    int def = (disp.FIGURE != null) ? disp.FIGURE.FIGURE_DEF : disp.FIGURE_DEF;
                    SafeSet(atkText, atk.ToString());
                    SafeSet(defText, def.ToString());
                }
            }

            if (card.IsSpell && card.SpecialMoveData != null)
            {
                SafeSet(spellTitleText, card.SpecialMoveData.name);
            }

            SetInteractable(true);
        }

        private Display_Figure ResolveDisplayFigureForPreview(DeckCard c)
        {
            if (c == null || !c.IsFigure) return null;

            var lib = GameObject.Find("Figure-Library");
            if (lib)
            {
                var child = lib.transform.Find(c.Figure_ID);
                if (child) return child.GetComponent<Display_Figure>();
            }
            if (c.FigurePrefab) return c.FigurePrefab.GetComponent<Display_Figure>();
            return null;
        }
        #endregion

        #region Input über OnPointer (ausgesetzt, jetzt über Button)
        public void OnPointerClick(PointerEventData eventData)
        {
            //PlaySelfFlightAndNotifyMirror();
            //Über Button
        }
        #endregion

        #region Public API (Self & Mirror)
        /// <summary>
        /// Startet den eigenen Flug inkl. Cast und benachrichtigt Mirror per Event (SiblingIndex).
        /// </summary>
        public void PlaySelfFlightAndNotifyMirror()
        {
            if (consumedOrPending || isFlying || !button || !button.interactable) return;
            StartCoroutine(PlaySelfAndNotifyCoroutine());
        }

        /// <summary>
        /// Startet auf der Mirror-Karte nur den visuellen Flug (kein Cast).
        /// NEU: Bot/Mirror-Flug wird grundsätzlich verdeckt gezeigt (BackFrame).
        /// </summary>
        public void PlayMirrorGhostFlight(string graveyardName = "P2-Graveyard", bool alsoDustAndShake = true)
        {
            if (consumedOrPending || isFlying) return;

            // --- Pragmatischer Bot-Fix: IMMER verdeckt fliegen ---
            EnsureBackVisible();

            PrepareForFlight();
            StartCoroutine(FlyRoutine(
                doCast: false,
                graveyardNameOverride: graveyardName,
                alsoDustAndShake: alsoDustAndShake
            ));
        }

        public void InjectDeck(PlayerDeck owner)
        {
            if (owner == null) return;
            this.deck = owner;
        }

        private void ApplyMirrorFlightRotation(bool enable)
        {
            var visual = transform.Find("Visual");
            if (!visual) return;

            if (enable)
            {
                if (_mirrorRotApplied) return;
                _mirrorPrevRot = visual.localRotation;

                if (mirrorRotateAnimate)
                {
                    LeanTween.rotateLocal(visual.gameObject, mirrorRotationEuler,
                            Mathf.Max(0.05f, (flightConfig ? flightConfig.moveUpTime : 0.15f)))
                        .setEase(LeanTweenType.easeOutQuad);
                }
                else
                {
                    visual.localRotation = Quaternion.Euler(mirrorRotationEuler);
                }

                _mirrorRotApplied = true;
            }
            else
            {
                if (!_mirrorRotApplied) return;

                if (mirrorRotateAnimate)
                {
                    LeanTween.rotateLocal(visual.gameObject, _mirrorPrevRot.eulerAngles, 0.12f)
                        .setEase(LeanTweenType.easeInQuad);
                }
                else
                {
                    visual.localRotation = _mirrorPrevRot;
                }

                _mirrorRotApplied = false;
            }
        }

        private void EnsureFrontVisible()
        {
            // 1) Front aktiv & Back aus
            var visual = transform.Find("Visual");
            var back = transform.Find("BackFrame");
            if (visual) visual.gameObject.SetActive(true);
            if (back) back.gameObject.SetActive(false);

            // 2) CanvasGroup(s) unter Visual hart auf 1
            if (visual)
            {
                var groups = visual.GetComponentsInChildren<CanvasGroup>(true);
                for (int i = 0; i < groups.Length; i++)
                    groups[i].alpha = 1f;
            }

            // 3) Artwork-Image + alle Graphics unter Visual auf Alpha=1
            if (visual)
            {
                var graphics = visual.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    var g = graphics[i];
                    var col = g.color; col.a = 1f; g.color = col;
                    g.canvasRenderer.SetAlpha(1f);
                    g.CrossFadeAlpha(1f, 0f, true);
                }

                // Gezielt das Artwork
                var artwork = visual.Find("Image")?.GetComponent<Image>();
                if (artwork)
                {
                    var mat = artwork.material;
                    if (mat && mat.HasProperty("_Color"))
                    {
                        var mcol = mat.GetColor("_Color");
                        if (mcol.a < 1f) { mcol.a = 1f; mat.SetColor("_Color", mcol); }
                    }
                }
            }

            // 4) Root-CanvasGroup sicherheitshalber voll deckend
            if (cg) cg.alpha = 1f;
        }

        /// <summary>
        /// NEU: Erzwingt BackFrame sichtbar (verdeckt), Front aus.
        /// Verhindert jeden visuellen Mismatch beim Bot-Flug.
        /// </summary>
        private void EnsureBackVisible()
        {
            // 1) Back aktiv & Front aus
            var visual = transform.Find("Visual");
            var back = transform.Find("BackFrame");
            if (visual) visual.gameObject.SetActive(false);
            if (back) back.gameObject.SetActive(true);

            // 2) CanvasGroup(s) unter Back hart auf 1
            if (back)
            {
                var groups = back.GetComponentsInChildren<CanvasGroup>(true);
                for (int i = 0; i < groups.Length; i++)
                    groups[i].alpha = 1f;

                // 3) Alle Graphics unter Back auf Alpha=1
                var graphics = back.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    var g = graphics[i];
                    var col = g.color; col.a = 1f; g.color = col;
                    g.canvasRenderer.SetAlpha(1f);
                    g.CrossFadeAlpha(1f, 0f, true);
                }
            }

            // 4) Root CanvasGroup sicherheitshalber voll deckend
            if (cg) cg.alpha = 1f;

            // Mirror-Rotation betrifft Visual -> deaktivieren
            ApplyMirrorFlightRotation(false);
        }

        #endregion

        #region Flow
        private IEnumerator PlaySelfAndNotifyCoroutine()
        {
            int cost = card.PlayerLoadCost;
            int load = GetCardHoldersLoad(); // z. B. Player-Base.Display_Figure.FIGURE_LOAD

            if (load < cost)
            {
                Debug.Log($"Not enough Load. Cost={cost}, PlayerLoad={load}");
                Messagebox.current.ShowMessageBox("Not enough Mana for this.");
                yield break; // Karte NICHT spielen
            }

            LoadPaySystem.current.PayLoad(GetCardHolder(), cost);
            LoadPaySystem.current.PayLoad(GetCardHolder_mirror(), cost);

            Debug.Log($"Enough Load. Cost={cost}, PlayerLoad={load} – play card.");

            // Interaktion sperren
            consumedOrPending = true;
            isFlying = true;
            SetInteractable(false);
            if (cg) cg.blocksRaycasts = false;

            // NEU (optional, aber empfohlen): Wenn diese Karte aus Bot-Hand kommt, Flug verdecken.
            // Dein Setup: Bot = "P2-Hand"
            if (rt != null && rt.parent != null && rt.parent.name == "P2-Hand")
                EnsureBackVisible();
            else
                EnsureFrontVisible();

            // Mirror informieren: von welcher Hand + Index?
            var hand = rt.parent as RectTransform;
            int idx = rt.GetSiblingIndex();
            OnCardPlayedVisual?.Invoke(hand, idx);

            PrepareForFlight();

            // Flug inkl. Cast & P1-Graveyard
            yield return StartCoroutine(FlyRoutine(
                doCast: true,
                graveyardNameOverride: "P1-Graveyard",
                alsoDustAndShake: true
            ));
        }

        /// <summary>
        /// Zieht alle Vorbereitungen für einen stabilen Flug:
        /// - Layout-Einfluss abschalten
        /// - FlightLayer-Reparent (im selben Canvas)
        /// - Start-AnchoredPosition im neuen Parent korrekt setzen
        /// </summary>
        private void PrepareForFlight()
        {
            if (!layoutElement) layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            parentCanvas = GetComponentInParent<Canvas>();
            uiCam = parentCanvas ? parentCanvas.worldCamera : null;

            cachedScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, rt.position);

            if (flightLayer != null && rt.parent != flightLayer)
            {
                rt.SetParent(flightLayer, worldPositionStays: true);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    flightLayer, cachedScreenPos, uiCam, out var local))
                {
                    rt.anchoredPosition = local;
                }
            }

            SetInteractable(false);
            if (cg) cg.blocksRaycasts = false;
        }

        /// <summary>
        /// Kern-Flugroutine für Self- oder Ghost-Flight.
        /// Self: doCast = true, Graveyard P1, inkl. FX
        /// Ghost: doCast = false, Graveyard P2, inkl. FX (optional)
        /// </summary>
        private IEnumerator FlyRoutine(bool doCast, string graveyardNameOverride, bool alsoDustAndShake)
        {
            AudioManager.Instance?.PlaySFX2D("Play_Card");

            if (flightConfig == null)
            {
                Debug.LogWarning("[DeckCardDisplay] Kein CardFlightConfig gefunden.");
                if (doCast)
                {
                    yield return StartCoroutine(CastNow());
                    if (removeDelay > 0f) yield return new WaitForSeconds(removeDelay);
                }
                if (moveToGraveyard) MoveToGraveyard(graveyardNameOverride);
                ReOrderCards();
                isFlying = false;
                yield break;
            }

            Vector2 targetAP = flightConfig.ResolveTargetAnchoredPosForCard(rt);

            Vector2 startAP = rt.anchoredPosition;
            Vector3 startScale = rt.localScale;
            float startAlpha = cg ? cg.alpha : 1f;

            // -------- Phase 1: Move + ScaleUp --------
            float t1 = Mathf.Max(0.01f, flightConfig.moveUpTime);
            float t = 0f;
            while (t < t1)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / t1);
                float e = 1f - (1f - p) * (1f - p); // easeOutQuad

                rt.anchoredPosition = Vector2.LerpUnclamped(startAP, targetAP, e);
                rt.localScale = Vector3.LerpUnclamped(startScale, startScale * flightConfig.scaleUp, e);
                yield return null;
            }
            rt.anchoredPosition = targetAP;
            rt.localScale = startScale * flightConfig.scaleUp;

            if (flightConfig.holdTime > 0f)
                yield return new WaitForSecondsRealtime(flightConfig.holdTime);

            // -------- Phase 2: Shrink + FadeOut --------
            bool castFired = false;
            float t2 = Mathf.Max(0.01f, flightConfig.shrinkTime);
            t = 0f;

            Vector3 shrinkTargetScale = startScale * 0.6f;
            float castAt = Mathf.Clamp01(flightConfig.castTriggerAt);

            while (t < t2)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / t2);

                if (doCast && !castFired && p >= castAt)
                {
                    castFired = true;
                    yield return StartCoroutine(CastNow());
                    if (removeDelay > 0f)
                        yield return new WaitForSeconds(removeDelay);
                }

                float e = p * p; // easeInQuad
                rt.localScale = Vector3.LerpUnclamped(startScale * flightConfig.scaleUp, shrinkTargetScale, e);
                if (cg) cg.alpha = Mathf.Lerp(startAlpha, 0f, e);
                rt.anchoredPosition = targetAP;
                yield return null;
            }
            if (cg) cg.alpha = 0f;

            // -------- Nach dem Verschwinden: in Graveyard setzen --------
            if (moveToGraveyard)
            {
                MoveToGraveyard(graveyardNameOverride);
                if (alsoDustAndShake)
                    PlayDustAnimationAndShake();
            }

            if (!flightConfig.keepInvisibleInGraveyard && cg)
            {
                cg.alpha = 1f;
                rt.localScale = startScale;
            }

            ReOrderCards();
            isFlying = false;
        }

        public int SelectedSlotIndex = 0; // <- dieser Wert muss vor dem Cast gesetzt werden (0,1,2)

        private int GetAutoSlotIndexForP1()
        {
            var root = GameObject.Find("Game-Canvas/FieldEffects_P1");
            if (!root)
            {
                Debug.LogWarning("[DeckCardDisplay] GetAutoSlotIndexForP1: 'Game-Canvas/FieldEffects_P1' nicht gefunden. Fallback Slot 0.");
                return 0;
            }

            int activeCount = 0;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.gameObject.activeInHierarchy)
                    activeCount++;
            }

            if (activeCount <= 0) return 0;
            if (activeCount == 1) return 1;
            if (activeCount == 2) return 2;
            return 0;
        }

        private int GetAutoSlotIndexForP2()
        {
            var root = GameObject.Find("Game-Canvas/FieldEffects_P2");
            if (!root)
            {
                Debug.LogWarning("[DeckCardDisplay] GetAutoSlotIndexForP2: 'Game-Canvas/FieldEffects_P2' nicht gefunden. Fallback Slot 0.");
                return 0;
            }

            int activeCount = 0;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.gameObject.activeInHierarchy)
                    activeCount++;
            }

            if (activeCount <= 0) return 0;
            if (activeCount == 1) return 1;
            if (activeCount == 2) return 2;
            return 0;
        }

        private IEnumerator CastNow()
        {
            AudioManager.Instance?.PlaySFX2D("Landing_Card");

            bool isFieldCard = (card != null && card.FieldCardData != null && !card.FieldCardData.used_as_trap);
            bool isTrapCard = (card != null && card.FieldCardData != null && card.FieldCardData.used_as_trap);

            if (isFieldCard)
            {
                string parentName = gameObject.transform.parent != null
                    ? gameObject.transform.parent.name
                    : string.Empty;

                if (parentName == "GUI-Canvas-P1")
                {
                    if (FieldCaster.instance != null)
                    {
                        int autoSlot = GetAutoSlotIndexForP1();
                        FieldCaster.instance.ReplaceP1_FieldCard(this.gameObject);
                    }
                    else
                    {
                        Debug.LogWarning("[DeckCardDisplay] FieldCaster.instance ist null, FieldCard konnte nicht gesetzt werden (P1).");
                    }
                }
                else if (parentName == "GUI-Canvas-P2")
                {
                    if (FieldCaster_P2.instance != null)
                    {
                        int autoSlot = GetAutoSlotIndexForP2();
                        FieldCaster_P2.instance.ReplaceP2_FieldCard(this.gameObject);
                    }
                    else
                    {
                        Debug.LogWarning("[DeckCardDisplay] FieldCaster_P2.instance ist null, FieldCard konnte nicht gesetzt werden (P2).");
                    }
                }

                yield return null;
                yield break;
            }

            if (isTrapCard)
            {
                string parentName = gameObject.transform.parent != null
                    ? gameObject.transform.parent.name
                    : string.Empty;

                if (parentName == "GUI-Canvas-P1")
                {
                    TrapCardHandler.instance.SpawnTrapCristal("P1");
                    FieldCardController.instance.AddNewTrapCard(card.FieldCardData);
                }
                if (parentName == "GUI-Canvas-P2")
                {
                    TrapCardHandler.instance.SpawnTrapCristal("P2");
                    FieldCardController_P2.instance.AddNewTrapCard(card.FieldCardData);
                }
            }

            if (card != null)
            {
                if (card.IsSpell && spellCaster)
                {
                    spellCaster.Cast(card);
                }
                else if (card.IsFigure && figureCaster)
                {
                    figureCaster.Cast(card);

                    SpecialMove figure_CastSpell = card.Figure_SO_Data.SPECIAL_B;
                    if (figure_CastSpell != null)
                    {
                        SpecialMoveController.current.PlaySpecialMove(figure_CastSpell);
                    }

                    FieldCard figure_fieldEffect = card.Figure_SO_Data.Figure_FieldEffect;
                    if (figure_fieldEffect != null)
                    {
                        string parentName = gameObject.transform.parent != null
                            ? gameObject.transform.parent.name
                            : string.Empty;

                        if (parentName == "GUI-Canvas-P1")
                            FieldCardController.instance.AddNewFigureFieldCard(figure_fieldEffect);

                        if (parentName == "GUI-Canvas-P2")
                            FieldCardController_P2.instance.AddNewFigureFieldCard(figure_fieldEffect);
                    }
                }
            }

            yield return null;
        }
        #endregion

        #region Move & ReOrder & Particle & Shake
        private void MoveToGraveyard(string overrideName = null)
        {
            string targetName = string.IsNullOrEmpty(overrideName) ? "P1-Graveyard" : overrideName;
            var graveyardObj = GameObject.Find(targetName);
            if (!graveyardObj)
            {
                Debug.LogWarning($"[DeckCardDisplay] Kein '{targetName}' gefunden – Karte bleibt erhalten.");
                return;
            }
            transform.SetParent(graveyardObj.transform, false);
            if (cg) cg.blocksRaycasts = false;
            SetInteractable(false);
        }

        public void ReOrderCards()
        {
            var d = deck ?? PlayerDeck.current ?? GameObject.Find("P1-Deck")?.GetComponent<PlayerDeck>();
            if (d != null)
            {
                d.ReflowNow(true);
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogWarning("[DeckCardDisplay] Kein PlayerDeck gefunden für ReOrderCards().");
            }
#endif
        }

        public void PlayDustAnimationAndShake()
        {
            GameObject pointZero = GameObject.Find("PointZero");

            if (pointZero == null)
            {
                Debug.LogWarning("Kein GameObject mit dem Namen 'PointZero' gefunden.");
                return;
            }

            Vector3 dustplace = pointZero.transform.position;

            ParticleController.Instance.PlayParticleEffect(
                dustplace,
                6,
                new Vector3(20f, 20f, 20f),
                Quaternion.Euler(-90f, 0f, 0f)
            );

            CameraShake.current.Shake(0.5f, 3f);
        }
        #endregion

        #region Helpers
        private void SetVisible(GameObject go, bool v) { if (go) go.SetActive(v); }
        private void SafeSet(TextMeshProUGUI t, string v) { if (t) t.text = v; }
        private void SetInteractable(bool v) { if (button) button.interactable = v; }

        private GameObject GetCardHolder()
        {
            GameObject cardholder = null;

            if (gameObject.transform.parent.name == "P1-Hand")
                cardholder = PlayerBaseController.current.P1_Base_P1_View;

            if (gameObject.transform.parent.name == "P2-Hand")
                cardholder = PlayerBaseController.current.P2_Base_P2_View;

            return cardholder;
        }

        private GameObject GetCardHolder_mirror()
        {
            GameObject cardholder = null;

            if (gameObject.transform.parent.name == "P1-Hand")
                cardholder = PlayerBaseController.current.P1_Base_P2_View;

            if (gameObject.transform.parent.name == "P2-Hand")
                cardholder = PlayerBaseController.current.P2_Base_P1_View;

            return cardholder;
        }

        private int GetCardHoldersLoad()
        {
            int holders_load = 0;

            if (gameObject.transform.parent.name == "P1-Hand")
                holders_load = PlayerBaseController.current.current_P1_Load;

            if (gameObject.transform.parent.name == "P2-Hand")
                holders_load = PlayerBaseController.current.current_P2_Load;

            return holders_load;
        }
        #endregion
    }
}
