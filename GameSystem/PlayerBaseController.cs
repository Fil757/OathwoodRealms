using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerBaseController : MonoBehaviour
{
    public static PlayerBaseController current;

    [Header("Current Stats of Playerbases")]
    public int current_P1_Health;
    public int current_P1_Load;

    public int current_P2_Health;
    public int current_P2_Load;

    [Header("Scene Views (beide Perspektiven)")]
    public GameObject P1_Base_P1_View;
    public GameObject P1_Base_P2_View;
    public GameObject P2_Base_P2_View;
    public GameObject P2_Base_P1_View;

    [Header("Ornament und Handkarten (zur Zeit nur P1 View)")]
    public GameObject P1_Base_Ornament;
    public GameObject P1_Base_Hand;
    public GameObject P2_Base_Ornament;
    public GameObject P2_Base_Hand;

    [Header("Effect Anchors im Game-Canvas")]
    [Tooltip("z.B. Game-Canvas/P1_Player_Effect_Position")]
    public Transform p1EffectAnchor;
    [Tooltip("z.B. Game-Canvas/P2_Player_Effect_Position")]
    public Transform p2EffectAnchor;

    #region === FX Config ===
    [Header("Particles")]
    [Tooltip("Index im ParticleController für HEAL (siehe ParticleController.particleEffect).")]
    public int healParticleIndex = 2;
    [Tooltip("Index im ParticleController für LOAD.")]
    public int loadParticleIndex = 0;
    [Tooltip("Index im ParticleController für DAMAGE.")]
    public int damageParticleIndex = 8;

    [Tooltip("Einheitliche Partikel-Skalierung für Bases.")]
    public Vector3 particleScale = new Vector3(5f, 5f, 5f);
    [Tooltip("Partikel-Rotation (UI-Topdown).")]
    public Vector3 particleEuler = new Vector3(-90f, 0f, 0f);

    [Header("Popup Offsets")]
    public Vector3 popupOffsetP1 = new Vector3(0f, 150f, 0f);
    public Vector3 popupOffsetP2 = new Vector3(0f, 150f, 0f);

    [Header("Camera Shake (bei Damage)")]
    public bool shakeOnDamage = true;
    public float shakeDuration = 0.75f;
    public float shakeMagnitude = 5f;
    #endregion

    #region UNITY
    void Awake()
    {
        current = this;
    }

    //TODO: Will be transformed into no update function later
    public void Update()
    {
        current_P1_Health = Get_Player_Health(P1_Base_P1_View);
        current_P2_Health = Get_Player_Health(P2_Base_P2_View);
        current_P1_Load = Get_Player_Load(P1_Base_P1_View);
        current_P2_Load = Get_Player_Load(P2_Base_P2_View);
    }
    #endregion

    #region === API (öffentliche Kurzaufrufe) ===

    public void SpecialMove_PlayerStatChange(GameObject target, string type, int amount)
    {
        switch (target.name)
        {
            case "P1-Base":
                switch (type)
                {
                    case "Healing": Healing_P1(amount); break;
                    case "Loading": Loading_P1(amount); break;
                    case "Damage":  Damaging_P1(amount); break;
                    case "Damage_quiet":  Damaging_P1_quiet(amount); break;
                }
                break;

            case "P2-Base":
                switch (type)
                {
                    case "Healing": Healing_P2(amount); break;
                    case "Loading": Loading_P2(amount); break;
                    case "Damage":  Damaging_P2(amount); break;
                    case "Damage_quiet":  Damaging_P2_quiet(amount); break;
                }
                break;
        }
    }

    public void Healing_P1(int amount)
    {
        Healing_Player_Base(P1_Base_P1_View, amount, "P1-GUI-Layer");
        Healing_Player_Base(P1_Base_P2_View, amount, "P2-GUI-Layer");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenHealing("Self", amount);
        FieldCardController_P2.instance.Player_TakenHealing("Opponent", amount);
    }

    public void Healing_P2(int amount)
    {
        Healing_Player_Base(P2_Base_P2_View, amount, "P2-GUI-Layer");
        Healing_Player_Base(P2_Base_P1_View, amount, "P1-GUI-Layer");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenHealing("Opponent", amount);
        FieldCardController_P2.instance.Player_TakenHealing("Self", amount);
    }

    public void Loading_P1(int amount)
    {
        Loading_Player_Base(P1_Base_P1_View, amount, "P1-GUI-Layer");
        Loading_Player_Base(P1_Base_P2_View, amount, "P2-GUI-Layer");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenLoading("Self", amount);
        FieldCardController_P2.instance.Player_TakenLoading("Opponent", amount);
    }

    public void Loading_P2(int amount)
    {
        Loading_Player_Base(P2_Base_P2_View, amount, "P2-GUI-Layer");
        Loading_Player_Base(P2_Base_P1_View, amount, "P1-GUI-Layer");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenLoading("Opponent", amount);
        FieldCardController_P2.instance.Player_TakenLoading("Self", amount);
    }

    public void Damaging_P1(int amount)
    {
        Damaging_Player_Base(P1_Base_P1_View, amount, "P1-GUI-Layer");
        Damaging_Player_Base(P1_Base_P2_View, amount, "P2-GUI-Layer");

        Shake_Base("P1");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenDamage("Self", amount);
        FieldCardController_P2.instance.Player_TakenDamage("Opponent", amount);
    }
    public void Damaging_P1_quiet(int amount)
    {
        Damaging_Player_Base_quiet(P1_Base_P1_View, amount, "P1-GUI-Layer");
        Damaging_Player_Base_quiet(P1_Base_P2_View, amount, "P2-GUI-Layer");

        Shake_Base("P1");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenDamage("Self", amount);
        FieldCardController_P2.instance.Player_TakenDamage("Opponent", amount);
    }

    public void Damaging_P2(int amount)
    {
        Damaging_Player_Base(P2_Base_P2_View, amount, "P2-GUI-Layer");
        Damaging_Player_Base(P2_Base_P1_View, amount, "P1-GUI-Layer");

        Shake_Base("P2");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenDamage("Opponent", amount);
        FieldCardController_P2.instance.Player_TakenDamage("Self", amount);
    }
    public void Damaging_P2_quiet(int amount)
    {
        Damaging_Player_Base_quiet(P2_Base_P2_View, amount, "P2-GUI-Layer");
        Damaging_Player_Base_quiet(P2_Base_P1_View, amount, "P1-GUI-Layer");

        Shake_Base("P2");

        // API-Call FieldCardController
        FieldCardController.instance.Player_TakenDamage("Opponent", amount);
        FieldCardController_P2.instance.Player_TakenDamage("Self", amount);
    }
    #endregion

    #region === Core: Heal / Load / Damage ===
    public void Healing_Player_Base(GameObject playerBase, int amount, string popUpLayer)
    {
        var df = playerBase.GetComponent<Display_Figure>();

        // --- Bars & Werte ---
        var healthBar = playerBase.transform.Find("Health_Load/Health/HealthBar").GetComponent<Image>();
        FlashHeal(healthBar);

        int maxHp = df.FIGURE_MAX_HEALTH;
        int curHp = df.FIGURE_HEALTH;

        HealthBarController.current.ChangeHealthBar(-amount, healthBar, curHp, maxHp);
        df.FIGURE_HEALTH = Mathf.Min(curHp + amount, maxHp);

        AudioManager.Instance?.PlaySFX2D("Healing");


        // --- FX ---
        PlayParticleAt(playerBase, healParticleIndex);
        CreatePopUp("Healing", amount, playerBase, popUpLayer);
    }

    public void Loading_Player_Base(GameObject playerBase, int amount, string popUpLayer)
    {
        var df = playerBase.GetComponent<Display_Figure>();

        // --- Bars & Werte ---
        var loadBar = playerBase.transform.Find("Health_Load/Load/LoadBar").GetComponent<Image>();
        FlashLoad(loadBar);

        int cur = df.FIGURE_LOAD;
        int next = Mathf.Clamp(cur + amount, 0, 100);
        LoadBarController.current.GetLoad(amount, loadBar);
        df.FIGURE_LOAD = next;

        AudioManager.Instance?.PlaySFX2D("Loading");

        // --- FX ---
        PlayParticleAt(playerBase, loadParticleIndex);
        CreatePopUp("Loading", amount, playerBase, popUpLayer);
    }

    public void Damaging_Player_Base(GameObject playerBase, int amount, string popUpLayer)
    {
        var df = playerBase.GetComponent<Display_Figure>();

        // --- Defense / tatsächlicher Schaden ---
        int hpCurrent = df.FIGURE_HEALTH;
        int hpMax = df.FIGURE_MAX_HEALTH;
        int defense = df.FIGURE_DEF;
        bool isDef = df.IS_DEFENDING;

        int dealt = isDef ? Mathf.Max(0, amount - defense) : amount;

        df.FIGURE_HEALTH = Mathf.Max(0, hpCurrent - dealt);

        // Healthbar-UI
        var healthBar = playerBase.transform.Find("Health_Load/Health/HealthBar").GetComponent<Image>();
        HealthBarController.current.ChangeHealthBar(dealt, healthBar, hpCurrent, hpMax);

        AudioManager.Instance?.PlaySFX2D("Damage");
        AudioManager.Instance?.PlaySFX2D("Damage_ground");

        // --- FX ---
        PlayParticleAt(playerBase, damageParticleIndex);
        if (shakeOnDamage) CameraShake.current.Shake(shakeDuration, shakeMagnitude);

        // Popup zeigt den tatsächlich zugefügten Schaden (dealt)
        CreatePopUp("Damage", dealt, playerBase, popUpLayer);
    }
    public void Damaging_Player_Base_quiet(GameObject playerBase, int amount, string popUpLayer)
    {
        var df = playerBase.GetComponent<Display_Figure>();

        // --- Defense / tatsächlicher Schaden ---
        int hpCurrent = df.FIGURE_HEALTH;
        int hpMax = df.FIGURE_MAX_HEALTH;
        int defense = df.FIGURE_DEF;
        bool isDef = df.IS_DEFENDING;

        int dealt = isDef ? Mathf.Max(0, amount - defense) : amount;

        df.FIGURE_HEALTH = Mathf.Max(0, hpCurrent - dealt);

        // Healthbar-UI
        var healthBar = playerBase.transform.Find("Health_Load/Health/HealthBar").GetComponent<Image>();
        HealthBarController.current.ChangeHealthBar(dealt, healthBar, hpCurrent, hpMax);

        //AudioManager.Instance?.PlaySFX2D("Damage");

        // --- FX ---
        PlayParticleAt(playerBase, damageParticleIndex);
        if (shakeOnDamage) CameraShake.current.Shake(shakeDuration, shakeMagnitude);

        // Popup zeigt den tatsächlich zugefügten Schaden (dealt)
        CreatePopUp("Damage", dealt, playerBase, popUpLayer);
    }

    public int Get_Player_Health(GameObject playerBase)
    {
        var df = playerBase.GetComponent<Display_Figure>();
        int playerBase_Health = df.FIGURE_HEALTH;

        return playerBase_Health;
    }

    public int Get_Player_Load(GameObject playerBase)
    {
        var df = playerBase.GetComponent<Display_Figure>();
        int playerBase_Load = df.FIGURE_LOAD;

        return playerBase_Load;
    }
    #endregion

    private void Shake_Base(string player)
    {
        if (player == "P1")
        {
            ObjectShaker.ShakeObject(P1_Base_Ornament, 4f, 0.4f);
            ObjectShaker.ShakeObject(P1_Base_Hand, 4f, 0.4f);
        }
        if (player == "P2")
        {
            ObjectShaker.ShakeObject(P2_Base_Ornament, 4f, 0.4f);
            ObjectShaker.ShakeObject(P2_Base_Hand, 4f, 0.4f);
        }
    }

    #region === Helpers (FX & PopUps) ===
    private void PlayParticleAt(GameObject target, int particleIndex)
    {
        // Entscheide anhand der Base, welche Seite das ist
        Transform anchor = null;

        if (target == P1_Base_P1_View || target == P1_Base_P2_View)
        {
            anchor = p1EffectAnchor;
        }
        else if (target == P2_Base_P2_View || target == P2_Base_P1_View)
        {
            anchor = p2EffectAnchor;
        }

        if (anchor == null)
        {
            Debug.LogWarning("[PlayerBaseController] Kein EffectAnchor für " + target.name + " zugewiesen.");
            return;
        }

        ParticleController.Instance.PlayParticleEffect(
            anchor.position,
            particleIndex,
            particleScale,
            Quaternion.Euler(particleEuler)
        );
    }

    private void CreatePopUp(string type, int value, GameObject root, string layer)
    {
        if (PopUp.current == null)
        {
            Debug.LogWarning("[PlayerBaseController] PopUp.current ist null.");
            return;
        }

        Vector3 offset;

        if (root == P1_Base_P1_View || root == P1_Base_P2_View)
            offset = popupOffsetP1;
        else if (root == P2_Base_P1_View || root == P2_Base_P2_View)
            offset = popupOffsetP2;
        else
            offset = popupOffsetP1; // Fallback

        PopUp.current.CreatePopUp(
            type,
            value,
            root,
            offset,
            Vector3.zero,
            layer,
            false
        );
    }

    private static void FlashHeal(Image healthBar)
    {
        HealToFigure.current.FlashLightGreen(2.5f, healthBar);
    }

    private static void FlashLoad(Image loadBar)
    {
        LoadToFigure.current.FlashLightBlue(2.5f, loadBar);
    }
    #endregion
}
