using UnityEngine;
using UnityEngine.UI;

// Pre-game menu: pick 1 or 2 players and confirm with Space/Enter — a
// neutral key, not tied to either player's own scheme, since nothing is
// bound to a specific player yet at this point.
public class StartScreenController : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private GameObject[] carouselPages;
    [SerializeField] private GameObject carouselBackground;

    [SerializeField] private Image option1Bg;
    [SerializeField] private Image option2Bg;
    [SerializeField] private Text option1Text;
    [SerializeField] private Text option2Text;
    [SerializeField] private Outline optionsRowOutline;

    [SerializeField] private Image controller1Bg;
    [SerializeField] private Image controller2Bg;
    [SerializeField] private Image controller3Bg;
    [SerializeField] private Text controller1Text;
    [SerializeField] private Text controller2Text;
    [SerializeField] private Text controller3Text;
    [SerializeField] private Outline controllerRowOutline;

    [SerializeField] private Image startBg;
    [SerializeField] private Outline startOutline;

    [SerializeField] private Text notImplementedText;

    [SerializeField] private GameObject playerRight;
    [SerializeField] private GameObject playerLeft;
    [SerializeField] private GestureInput gestureRight;
    [SerializeField] private GestureInput gestureLeft;

    // Per-player КЛАВИШИ/ЖЕСТЫ gameplay HUD (CreateGesturePanel) — hidden
    // while this menu is up (they're feedback for actual play, not menu
    // chrome) and revealed once BeginGame fires.
    [SerializeField] private GameObject gestureCanvasRight;
    [SerializeField] private GameObject gestureCanvasLeft;

    [SerializeField] private AudioSource musicSource;

    private static readonly Color SelectedColor = new Color(0.2f, 0.75f, 0.25f, 0.9f);
    private static readonly Color UnselectedColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    private static readonly Color StartFocusColor = new Color(0.4f, 0.35f, 0.1f, 0.9f);
    private static readonly Color FocusOutline = new Color(1f, 0.85f, 0.15f);
    private static readonly Color IdleOutline = Color.gray;

    private const int RowCount = 3; // 0 = player count, 1 = controller type, 2 = start button
    private const int ControllerCount = 3; // 0 = keyboard, 1 = distance sensors, 2 = sensor simulator

    private int _selectedPlayers = 2;
    private int _selectedController; // 0 = keyboard, 1 = distance sensors, 2 = simulator
    private int _row;

    // TEMPORARY, for faster debug/test cycling — revert to 10f for real play.
    private const float CarouselInterval = 2f;
    // Pure pause before the carousel shows anything at all — first
    // impression is the game running behind the menu buttons, not a table,
    // same as before the carousel/winner-tables existed.
    private const float CarouselStartDelay = 4f;
    private const int NoCarouselPage = -2; // sentinel distinct from _lastCarouselPage's initial -1
    private int _lastCarouselPage = -1;

    private PlayerController _rightController;
    private int _rightHomeLane;

    // Edge-detect state for the two menu-only gestures that GestureInput
    // doesn't already expose as a "just happened" signal (LeanLeft/RightDown
    // and JumpDown already are — DuckHeld and "both hands up" are level
    // signals there since ducking/jumping mid-run don't need edges).
    private bool _prevBothUpRight, _prevBothUpLeft;
    private bool _prevDuckRight, _prevDuckLeft;

    private void Awake()
    {
        // Gameplay stays inert (players unresponsive, road stopped, nothing
        // spawns — SpeedController holds at 0) until a mode is confirmed.
        SetPlayerControlEnabled(playerRight, false);
        SetPlayerControlEnabled(playerLeft, false);

        if (playerRight != null)
        {
            _rightController = playerRight.GetComponent<PlayerController>();
            if (_rightController != null)
                _rightHomeLane = _rightController.HomeLane;
        }

        // The per-player gesture HUD is feedback for actual play, not menu
        // chrome — hidden while this menu is up (MenuHelpText covers the
        // freed-up space instead), shown again once BeginGame fires.
        if (gestureCanvasRight != null)
            gestureCanvasRight.SetActive(false);
        if (gestureCanvasLeft != null)
            gestureCanvasLeft.SetActive(false);

        // Nothing's chosen a controller yet at this point (that's what row 1
        // picks), so the menu itself listens for whichever gesture source is
        // actually available — real sensors if connected, the keyboard
        // simulator otherwise — on both players at once, in addition to the
        // plain arrow/WASD keys already handled below.
        bool useRealSensors = GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected;
        EnableGestureForMenu(gestureRight, useRealSensors);
        EnableGestureForMenu(gestureLeft, useRealSensors);

        if (musicSource != null)
            musicSource.Play();

        UpdateVisuals();
        UpdateCarousel();
    }

    private void Update()
    {
        UpdateCarousel();

        bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);

        // Gesture nav (works from either player, whichever the keyboard
        // simulator or real sensors are hooked up to): leaning one hand
        // moves left/right, both hands up moves the row cursor up, both
        // hands down (duck) moves it down, and flapping (the same signal
        // that means "jump" in actual play) confirms — "start and wave"
        // rather than a dedicated new gesture.
        left |= IsLeanLeftDown(gestureRight) || IsLeanLeftDown(gestureLeft);
        right |= IsLeanRightDown(gestureRight) || IsLeanRightDown(gestureLeft);
        up |= EdgeBothHandsUp(gestureRight, ref _prevBothUpRight) || EdgeBothHandsUp(gestureLeft, ref _prevBothUpLeft);
        down |= EdgeDuck(gestureRight, ref _prevDuckRight) || EdgeDuck(gestureLeft, ref _prevDuckLeft);
        confirm |= IsJumpDown(gestureRight) || IsJumpDown(gestureLeft);

        if (left || right)
        {
            if (_row == 0)
            {
                _selectedPlayers = _selectedPlayers == 1 ? 2 : 1;
            }
            else if (_row == 1)
            {
                int delta = right ? 1 : -1;
                _selectedController = (_selectedController + delta + ControllerCount) % ControllerCount;
            }
            UpdateVisuals();
        }

        if (up)
        {
            _row = (_row - 1 + RowCount) % RowCount;
            UpdateVisuals();
        }
        else if (down)
        {
            _row = (_row + 1) % RowCount;
            UpdateVisuals();
        }

        if (confirm && _row == 2)
            BeginGame();
    }

    private void UpdateVisuals()
    {
        bool oneSelected = _selectedPlayers == 1;

        // Live preview: the second ladybug appears/disappears on the road
        // the instant the selection changes, before Start is even pressed.
        if (playerLeft != null)
            playerLeft.SetActive(!oneSelected);

        // Solo mode stands the lone player in the middle lane instead of
        // its usual (right-edge) co-op starting lane.
        if (_rightController != null)
            _rightController.SetPreviewLane(oneSelected ? _rightController.LaneCount / 2 : _rightHomeLane);

        if (option1Bg != null)
            option1Bg.color = oneSelected ? SelectedColor : UnselectedColor;
        if (option2Bg != null)
            option2Bg.color = oneSelected ? UnselectedColor : SelectedColor;
        if (option1Text != null)
            option1Text.text = (oneSelected ? "[X] " : "[ ] ") + "1 ИГРОК";
        if (option2Text != null)
            option2Text.text = (oneSelected ? "[ ] " : "[X] ") + "2 ИГРОКА";

        bool keyboardSelected = _selectedController == 0;
        bool sensorsSelected = _selectedController == 1;
        bool simulatorSelected = _selectedController == 2;
        if (controller1Bg != null)
            controller1Bg.color = keyboardSelected ? SelectedColor : UnselectedColor;
        if (controller2Bg != null)
            controller2Bg.color = sensorsSelected ? SelectedColor : UnselectedColor;
        if (controller3Bg != null)
            controller3Bg.color = simulatorSelected ? SelectedColor : UnselectedColor;
        if (controller1Text != null)
            controller1Text.text = (keyboardSelected ? "[X] " : "[ ] ") + "КЛАВИАТУРА";
        if (controller2Text != null)
            controller2Text.text = (sensorsSelected ? "[X] " : "[ ] ") + "ДАТЧИКИ";
        if (controller3Text != null)
            controller3Text.text = (simulatorSelected ? "[X] " : "[ ] ") + "ИМИТАТОР";

        if (optionsRowOutline != null)
            optionsRowOutline.effectColor = _row == 0 ? FocusOutline : IdleOutline;
        if (controllerRowOutline != null)
            controllerRowOutline.effectColor = _row == 1 ? FocusOutline : IdleOutline;
        if (startOutline != null)
            startOutline.effectColor = _row == 2 ? FocusOutline : IdleOutline;
        if (startBg != null)
            startBg.color = _row == 2 ? StartFocusColor : UnselectedColor;

        // Any navigation clears the "not implemented" message from a
        // previous attempt to start with sensors selected.
        if (notImplementedText != null)
            notImplementedText.gameObject.SetActive(false);
    }

    private void BeginGame()
    {
        if (_selectedController == 1)
        {
            bool connected = GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected;
            if (!connected)
            {
                if (notImplementedText != null)
                {
                    notImplementedText.gameObject.SetActive(true);
                    notImplementedText.text = "Датчики не найдены — проверь подключение платы";
                }
                return;
            }
        }

        bool gestureActive = _selectedController == 1 || _selectedController == 2;
        bool useRealSensors = _selectedController == 1;

        // playerLeft's active state already reflects the selection (toggled
        // live in UpdateVisuals) — only need to arm its controls if present.
        if (_selectedPlayers == 2)
        {
            SetPlayerControlEnabled(playerLeft, true);
            SetGestureEnabled(gestureLeft, gestureActive, useRealSensors);
        }

        SetPlayerControlEnabled(playerRight, true);
        SetGestureEnabled(gestureRight, gestureActive, useRealSensors);

        // The gesture HUD was hidden for the menu (see Awake) — back on now
        // that the run itself is starting.
        if (gestureCanvasRight != null)
            gestureCanvasRight.SetActive(true);
        if (gestureCanvasLeft != null)
            gestureCanvasLeft.SetActive(true);

        if (SpeedController.Instance != null)
            SpeedController.Instance.BeginGame();

        if (GameTimer.Instance != null)
            GameTimer.Instance.BeginTiming();

        if (musicSource != null)
            musicSource.Stop();

        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        enabled = false;
    }

    // Cycles the middle info panel between pages (rules/controls/trick
    // diagrams/gesture diagrams) every CarouselInterval seconds, looping —
    // driven off Time.time so no timer field is needed. Pages are whole
    // pre-built GameObjects (not just swapped text) so some can be visual
    // diagrams instead of plain text.
    private void UpdateCarousel()
    {
        if (carouselPages == null || carouselPages.Length == 0)
            return;

        if (Time.time < CarouselStartDelay)
        {
            if (_lastCarouselPage != NoCarouselPage)
            {
                for (int i = 0; i < carouselPages.Length; i++)
                    if (carouselPages[i] != null)
                        carouselPages[i].SetActive(false);
                if (carouselBackground != null)
                    carouselBackground.SetActive(false);
                _lastCarouselPage = NoCarouselPage;
            }
            return;
        }

        int rawPage = Mathf.FloorToInt((Time.time - CarouselStartDelay) / CarouselInterval) % carouselPages.Length;
        int page = ResolveDisplayPage(rawPage);
        if (page == _lastCarouselPage)
            return;

        if (carouselBackground != null)
            carouselBackground.SetActive(true);
        for (int i = 0; i < carouselPages.Length; i++)
            if (carouselPages[i] != null)
                carouselPages[i].SetActive(i == page);
        _lastCarouselPage = page;
    }

    // Skips past any TopResultsPage that has nothing to show yet (no one's
    // played that category, so it'd just be 3 rows of "--") — falls through
    // to the next page in cycle order instead. Non-TopResultsPage pages
    // (rules/diagrams) always count as showable.
    private int ResolveDisplayPage(int page)
    {
        for (int attempt = 0; attempt < carouselPages.Length; attempt++)
        {
            int idx = (page + attempt) % carouselPages.Length;
            GameObject candidate = carouselPages[idx];
            if (candidate == null)
                continue;

            TopResultsPage results = candidate.GetComponent<TopResultsPage>();
            if (results == null || results.HasAnyEntry())
                return idx;
        }
        return page; // every page empty — shouldn't happen, fall back rather than show nothing
    }

    private static void SetPlayerControlEnabled(GameObject player, bool value)
    {
        if (player == null)
            return;

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = value;
    }

    private static void SetGestureEnabled(GestureInput gesture, bool enabled, bool useRealSensors)
    {
        if (gesture == null)
            return;

        gesture.enabled = enabled;
        gesture.UseRealSensors = useRealSensors;
    }

    // Menu-only: turns a GestureInput on regardless of the (not yet made)
    // controller choice, purely so this screen can read hand state from it.
    // Harmless to actual gameplay — PlayerController is disabled at this
    // point, so nothing consumes GestureInput's output but this menu.
    // BeginGame overwrites .enabled/.UseRealSensors with the real choice
    // right after, once a mode is confirmed.
    private static void EnableGestureForMenu(GestureInput gesture, bool useRealSensors)
    {
        if (gesture == null)
            return;

        gesture.enabled = true;
        gesture.UseRealSensors = useRealSensors;
    }

    private static bool IsLeanLeftDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanLeftDown;
    private static bool IsLeanRightDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.LeanRightDown;
    private static bool IsJumpDown(GestureInput gesture) => gesture != null && gesture.enabled && gesture.JumpDown;

    // "Both hands up, held" doesn't mean anything during actual play (only
    // the flapping motion does, to avoid an accidental jump from just
    // resting hands up) — but it's a natural, otherwise-unused signal for
    // "move the row cursor up" here, so this menu reads it directly off
    // GestureInput's raw per-hand state instead of its interpreted Jump.
    private static bool EdgeBothHandsUp(GestureInput gesture, ref bool prev)
    {
        bool now = gesture != null && gesture.enabled && gesture.LeftHandUp && gesture.RightHandUp;
        bool edgeUp = now && !prev;
        prev = now;
        return edgeUp;
    }

    private static bool EdgeDuck(GestureInput gesture, ref bool prev)
    {
        bool now = gesture != null && gesture.enabled && gesture.DuckHeld;
        bool edgeUp = now && !prev;
        prev = now;
        return edgeUp;
    }
}
