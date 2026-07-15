using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Holding brake on every active player for a few seconds together is treated
// as a deliberate "let's stop" gesture — freezes the road and asks to confirm.
public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [SerializeField] private float holdDuration = 5f;
    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private Text yesText;
    [SerializeField] private Text noText;
    [SerializeField] private Text holdWarningText;

    private static readonly Color Highlighted = new Color(1f, 0.85f, 0.2f);

    private float _holdTimer;
    private bool _dialogOpen;
    private bool _confirmYes;

    public bool IsDialogOpen => _dialogOpen;

    private void Awake()
    {
        Instance = this;
        if (dialogRoot != null)
            dialogRoot.SetActive(false);
        SetHoldWarningVisible(false);
    }

    private void Update()
    {
        if (_dialogOpen)
        {
            HandleDialogInput();
            return;
        }

        if (SpeedController.Instance == null || !SpeedController.Instance.IsRunning)
            return; // start screen — nothing to pause yet

        if (WinSequence.Instance != null && WinSequence.Instance.Triggered)
        {
            ResetHold();
            return; // don't interrupt the victory cinematic
        }

        if (HelpController.Instance != null && HelpController.Instance.IsOpen)
        {
            ResetHold();
            return; // help overlay is up — don't count braking against it
        }

        PlayerController[] players = FindObjectsOfType<PlayerController>();

        // The countdown only starts once braking has actually brought the
        // road down to a stop — braking itself takes a moment to bite.
        bool eligible = players.Length > 0 && AllBraking(players)
                      && SpeedController.Instance.IsAtMinSpeed;
        if (!eligible)
        {
            ResetHold();
            return;
        }

        _holdTimer += Time.deltaTime;
        UpdateHoldWarning(players.Length);
        if (_holdTimer >= holdDuration)
            OpenDialog();
    }

    private void ResetHold()
    {
        if (_holdTimer > 0f)
            SetHoldWarningVisible(false);
        _holdTimer = 0f;
    }

    private void UpdateHoldWarning(int playerCount)
    {
        if (holdWarningText == null)
            return;

        int remaining = Mathf.CeilToInt(holdDuration - _holdTimer);
        string label = playerCount > 1 ? "Зажат тормоз (у обоих)" : "Зажат тормоз";
        holdWarningText.text = label + " — до выхода: " + remaining + " сек";
        SetHoldWarningVisible(true);
    }

    private void SetHoldWarningVisible(bool visible)
    {
        if (holdWarningText != null)
            holdWarningText.gameObject.SetActive(visible);
    }

    private static bool AllBraking(PlayerController[] players)
    {
        foreach (var p in players)
            if (!p.IsBraking)
                return false;
        return true;
    }

    private void OpenDialog()
    {
        _dialogOpen = true;
        _confirmYes = false;
        _holdTimer = 0f;
        SetHoldWarningVisible(false);

        if (SpeedController.Instance != null)
            SpeedController.Instance.SetPaused(true);
        if (GameTimer.Instance != null)
            GameTimer.Instance.Pause();

        foreach (var p in FindObjectsOfType<PlayerController>())
            p.enabled = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(true);
        UpdateDialogVisuals();
    }

    private void HandleDialogInput()
    {
        bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        bool accel = Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.LeftShift);

        if (left || right)
        {
            _confirmYes = !_confirmYes;
            UpdateDialogVisuals();
        }

        if (accel)
        {
            if (_confirmYes)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            else
                CloseDialog();
        }
    }

    private void CloseDialog()
    {
        _dialogOpen = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);
        if (SpeedController.Instance != null)
            SpeedController.Instance.SetPaused(false);
        if (GameTimer.Instance != null)
            GameTimer.Instance.Resume();

        foreach (var p in FindObjectsOfType<PlayerController>())
            p.enabled = true;
    }

    private void UpdateDialogVisuals()
    {
        if (yesText != null)
            yesText.color = _confirmYes ? Highlighted : Color.white;
        if (noText != null)
            noText.color = _confirmYes ? Color.white : Highlighted;
    }
}
