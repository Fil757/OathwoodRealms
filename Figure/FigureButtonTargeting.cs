using UnityEngine;
using UnityEngine.UI;

public class FigureButtonTargeting : MonoBehaviour
{
    public Button button;

    public bool isTargetingButton;
    public bool isAttackButton;
    public bool isDefendButton;
    public bool isSpecialButton;

    [Header("Input Lock (Fallback Cooldown)")]
    [Tooltip("Nur relevant für Attack/Defend/Targeting, falls diese Aktionen kein sauberes Finished-Signal haben.")]
    public float fallbackLockSeconds = 0.35f;

    private Transform _modelRoot;          // Parent "Model"
    private AudioSource _buttonSound;      // Model/Standard_ButtonSound
    private GameObject _figureButtonsGO;   // Model/FigureButtons

    private void Awake()
    {
        CacheRefsFromMyFigure();

        if (button != null)
        {
            if (isTargetingButton) button.onClick.AddListener(RunTargeting);
            if (isAttackButton)    button.onClick.AddListener(RunAttack);
            if (isDefendButton)    button.onClick.AddListener(RunDefend);
            if (isSpecialButton)   button.onClick.AddListener(RunSpecial);
        }
    }

    private void Update()
    {
        if (MyFigureIsLocked())
        return;
        // Globale Sperre: während Action-Lock KEINE Eingaben
        if (TurnManager.current != null && TurnManager.current.IsActionLocked())
            return;

        // Keyboard (nur einmal beim Drücken)
        if (isTargetingButton && Input.GetKeyDown(KeyCode.T)) RunTargeting();
        if (isAttackButton    && Input.GetKeyDown(KeyCode.A)) RunAttack();
        if (isDefendButton    && Input.GetKeyDown(KeyCode.D)) RunDefend();
        if (isSpecialButton   && Input.GetKeyDown(KeyCode.S)) RunSpecial();
    }

    // =========================================================
    // Cache: finde "Model" und darunter Sound + FigureButtons
    // =========================================================
    private void CacheRefsFromMyFigure()
    {
        _modelRoot = FindParentByName(transform, "Model");
        if (_modelRoot == null)
        {
            Transform root = transform.root;
            _modelRoot = FindDeepChildByName(root, "Model");
        }

        if (_modelRoot == null)
            return;

        Transform soundT = _modelRoot.Find("Standard_ButtonSound");
        if (soundT != null)
            _buttonSound = soundT.GetComponent<AudioSource>();

        Transform fbT = _modelRoot.Find("FigureButtons");
        if (fbT != null)
            _figureButtonsGO = fbT.gameObject;
    }

    private static Transform FindParentByName(Transform start, string name)
    {
        Transform t = start;
        while (t != null)
        {
            if (t.name == name)
                return t;
            t = t.parent;
        }
        return null;
    }

    private static Transform FindDeepChildByName(Transform root, string name)
    {
        if (root == null) return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
                return all[i];
        }
        return null;
    }

    // =========================================================
    // Shared action extras
    // =========================================================

    private bool Guard_LockOnly()
    {
        var tm = TurnManager.current;
        if (tm == null) return false;

        if (tm.IsActionLocked())
            return false;

        tm.LockActions();
        return true;
    }

    private void PlaySoundAndDisableButtons()
    {
        // falls Prefab im Runtime verändert wurde: einmal nach-cachen
        if (_modelRoot == null || (_buttonSound == null && _figureButtonsGO == null))
            CacheRefsFromMyFigure();

        if (_buttonSound != null)
            _buttonSound.Play();

        if (_figureButtonsGO != null && _figureButtonsGO.activeSelf)
            _figureButtonsGO.SetActive(false);
    }

    private bool Guard_ActionLockAndClearSelection()
    {
        var tm = TurnManager.current;
        if (tm == null) return false;

        // Wenn gesperrt -> Aktion blocken
        if (tm.IsActionLocked())
            return false;

        // WICHTIG: sofort sperren + Current Figure leeren (damit erneutes S/A/D/T nicht greift)
        tm.LockActions();
        tm.ClearCurrentSelectionForActiveSide();

        return true;
    }

    // =========================================================
    // Actions
    // =========================================================
    private void RunAttack()
    {
        if (!Guard_LockOnly())
            return;

        if (MyFigureIsLocked())
        return;

        PlaySoundAndDisableButtons();

        if (AttackController.current == null)
        {
            TurnManager.current.UnlockActions();
            return;
        }

        // 1) Aktion starten (braucht evtl. current_figure_P1)
        AttackController.current.FigureAttackP1();

        // 2) Danach Auswahl leeren (damit man neu klicken muss)
        TurnManager.current.ClearCurrentSelectionForActiveSide();

        // 3) Fallback-Unlock
        TurnManager.current.UnlockActionsAfterSeconds(fallbackLockSeconds);
    }

    private void RunDefend()
    {
        if (!Guard_LockOnly())
            return;

        if (MyFigureIsLocked())
        return;

        PlaySoundAndDisableButtons();

        if (AttackController.current == null)
        {
            TurnManager.current.UnlockActions();
            return;
        }

        AttackController.current.Activate_Defense_Current_Figure_P1();
        TurnManager.current.ClearCurrentSelectionForActiveSide();
        TurnManager.current.UnlockActionsAfterSeconds(fallbackLockSeconds);
    }

    private void RunTargeting()
    {

        PlaySoundAndDisableButtons();

        if (TargetingSystem.current == null)
        {
            TurnManager.current.UnlockActions();
            return;
        }

        TargetingSystem.current.RunAutoTargetingForCurrentP1();
        //TurnManager.current.ClearCurrentSelectionForActiveSide();
        //TurnManager.current.UnlockActionsAfterSeconds(fallbackLockSeconds);
    }

    private void RunSpecial()
    {
        var tm = TurnManager.current;
        if (tm != null && tm.IsActionLocked())
            return;

        if (MyFigureIsLocked())
        return;

        PlaySoundAndDisableButtons();

        if (SpecialMoveController.current == null)
            return;

        // NICHT locken – das macht der SpecialMoveController zentral
        SpecialMoveController.current.Play_SpecialMove_currentFigure_A();

        // Optional: erst NACH dem Start leeren (wie du es willst)
        tm?.ClearCurrentSelectionForActiveSide();
    }

    private bool MyFigureIsLocked()
    {
        var lockComp = GetComponentInParent<FigureActionLock>(true);
        return lockComp != null && lockComp.IsLocked;
    }
}
