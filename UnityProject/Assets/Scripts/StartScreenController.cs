using UnityEngine;
using UnityEngine.UI;

// Pre-game menu: pick 1 or 2 players and confirm, using either control
// scheme (arrows/IJ or WASD/Shift+Ctrl) — nothing is bound yet, so any
// player's movement/accel keys work interchangeably here.
public class StartScreenController : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private Text carouselText;

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

    private const float CarouselInterval = 10f;
    private static readonly string[] CarouselPages =
    {
        "СУТЬ ИГРЫ\n"
        + "Ехать вперёд\n"
        + "Собирать хорошее\n"
        + "Избегать плохое\n"
        + "Вдвоём — делать трюки",

        "ЦЕЛЬ\n"
        + "Набрать 100 очков\n"
        + "За самое короткое время\n"
        + "И дополнительно выполняя трюки",

        "УПРАВЛЕНИЕ\n"
        + "Правый: ← → полоса, ↑ прыжок, ↓ пригнуться, I газ, K тормоз\n"
        + "Левый: A D полоса, W прыжок, S пригнуться, Shift газ, Ctrl тормоз\n"
        + "Газ/тормоз суммируются: оба жмут газ — разгон вдвое",

        "ТРЮКИ\n"
        + "АРКА: один приседает под аркой, другой в этот момент перепрыгивает её вместе с ним\n"
        + "КОЛЬЦО: игроки одновременно меняются полосами — один в прыжке, другой понизу",

        "ДАТЧИКИ РАССТОЯНИЯ (ИМИТАТОР)\n"
        + "По датчику на каждую руку, смотрят вниз: обе руки вверх — прыжок, обе вниз — пригнуться\n"
        + "Одна вверх, другая вниз — полоса в сторону опущенной руки\n"
        + "Тронуть среднее положение обеих рук разом — газ, по очереди — тормоз\n"
        + "Имитатор (верх, середина, низ): левый Q,A,Z и W,S,X — правый O,L,. и P,;,/",
    };

    private PlayerController _rightController;
    private int _rightHomeLane;

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
        bool accel = Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.LeftShift);

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

        if (accel && _row == 2)
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
            // Real hardware isn't connected yet — the simulator (option 2)
            // is the only working gesture path for now.
            if (notImplementedText != null)
            {
                notImplementedText.gameObject.SetActive(true);
                notImplementedText.text = "Датчики расстояния — пока не реализовано";
            }
            return;
        }

        // playerLeft's active state already reflects the selection (toggled
        // live in UpdateVisuals) — only need to arm its controls if present.
        if (_selectedPlayers == 2)
        {
            SetPlayerControlEnabled(playerLeft, true);
            SetGestureEnabled(gestureLeft, _selectedController == 2);
        }

        SetPlayerControlEnabled(playerRight, true);
        SetGestureEnabled(gestureRight, _selectedController == 2);

        if (SpeedController.Instance != null)
            SpeedController.Instance.BeginGame();

        if (GameTimer.Instance != null)
            GameTimer.Instance.BeginTiming();

        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        enabled = false;
    }

    // Cycles the middle info panel between controls and the trick list every
    // CarouselInterval seconds, looping — driven off Time.time so no timer
    // field is needed.
    private void UpdateCarousel()
    {
        if (carouselText == null || CarouselPages.Length == 0)
            return;

        int page = Mathf.FloorToInt(Time.time / CarouselInterval) % CarouselPages.Length;
        if (carouselText.text != CarouselPages[page])
            carouselText.text = CarouselPages[page];
    }

    private static void SetPlayerControlEnabled(GameObject player, bool value)
    {
        if (player == null)
            return;

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = value;
    }

    private static void SetGestureEnabled(GestureInput gesture, bool value)
    {
        if (gesture != null)
            gesture.enabled = value;
    }
}
