using UnityEngine;
using UnityEngine.UI;

// Drives the ТРЕНИРОВКА carousel's "ВАШИ ДЕЙСТВИЯ" bug — a flat RawImage,
// posed exactly the way GestureDiagramAnimation's own "ОБРАЗЕЦ" bug is (duck
// = vertical squash, lean = horizontal shift + tilt, flap/jump = rise +
// alternating wing frames), but driven by the player's REAL gesture/
// joystick/keyboard input every frame instead of a scripted loop, so a
// player can immediately see their own move mirrored back at them.
public class LiveBugReactionAnimator : MonoBehaviour
{
    [SerializeField] private GestureInput gestureInput;
    [SerializeField] private JoystickInput joystickInput;
    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode downKey = KeyCode.DownArrow;

    [SerializeField] private RawImage bugImage;
    [SerializeField] private Texture2D bugNormalTexture;
    [SerializeField] private Texture2D bugAirTexture1;
    [SerializeField] private Texture2D bugAirTexture2;

    [SerializeField] private float duckSquash = 0.4f;
    // 3 explicit X coordinates (one per lane), not a rest+shift formula —
    // both bugs share the same column and are meant to freely cross into
    // each other's territory while leaning (per feedback, unlike the real
    // game's own separate lanes, so this is deliberately NOT symmetric
    // around each bug's own rest position). Wired per-instance from
    // SceneSetup.CreateLiveBugPreview.
    [SerializeField] private float laneXLeft;
    [SerializeField] private float laneXCenter;
    [SerializeField] private float laneXRight;
    [SerializeField] private float leanTiltAngle = 12f;
    // Bigger than GestureDiagramAnimation's own 50 — per feedback the jump
    // should reach almost all the way up to this column's own top edge.
    // Bumped back up from 115 — liveBugRestY (SceneSetup) moved down to sit
    // exactly halfway between top and duck, and this was raised by that
    // same amount so the top's own on-screen position doesn't move.
    [SerializeField] private float flyRise = 177.5f;
    // Duck's squash alone (see duckSquash below) only drops the bug a
    // little — per feedback ducking should also travel most of the way
    // down toward the column's own bottom edge, same as jump does toward
    // the top, not just shrink roughly in place. Added on top of the
    // squash-driven shift below. Reduced from 180 — liveBugRestY
    // (SceneSetup) moved down to sit exactly halfway between top and duck,
    // and this was lowered by that same amount so duck's own on-screen
    // position doesn't move.
    [SerializeField] private float duckDrop = 117.5f;
    [SerializeField] private float flapFrameDuration = 0.15f;
    // How fast the pose eases toward whatever's currently held — not an
    // instant snap, same eased feel the real in-game moves have.
    [SerializeField] private float poseLerpSpeed = 10f;

    private Vector2 _restPos;
    private Vector3 _restScale = Vector3.one;
    private float _flapFrameTimer;
    private bool _flapFrameToggle;
    // After a page becomes visible, hold the neutral rest pose briefly so a
    // shared GestureInput left over from menu navigation (or a sensor still
    // reading "both hands down" from the previous screen) doesn't make player
    // 1's bug pop in already squashed at the bottom and then ease up.
    private float _settleUntil;
    // -1/0/+1 — real lane changes in the actual game are sticky (you stay
    // in whatever lane you moved to, you don't spring back once you let go
    // of the key/gesture), unlike jump/duck which are momentary. Clamped to
    // one step either way so it can't go past laneXLeft/laneXRight.
    private int _laneIndex;
    private readonly GestureInput.HandFlapTracker _leftFlapTracker = new GestureInput.HandFlapTracker();
    private readonly GestureInput.HandFlapTracker _rightFlapTracker = new GestureInput.HandFlapTracker();
    private bool _sensorPrevLeanLeftHeld;
    private bool _sensorPrevLeanRightHeld;
    private bool _joystickPrevLeftHeld;
    private bool _joystickPrevRightHeld;

    private bool UsesLinkedGestureInput()
    {
        return gestureInput != null
            && gestureInput.enabled
            && gestureInput.UseRealSensors
            && gestureInput.gameObject.activeInHierarchy;
    }

    private bool UsesLinkedJoystickInput()
    {
        return joystickInput != null && joystickInput.enabled;
    }

    private bool UpHeld()
    {
        if (UsesLinkedGestureInput())
            return gestureInput.JumpHeld;
        if (TryReadSensorJumpHeld())
            return true;
        if (TryReadJoystickUp(out bool up))
            return up;
        return Input.GetKey(upKey);
    }

    private bool DownHeld()
    {
        if (UsesLinkedGestureInput())
            return gestureInput.DuckHeld;
        if (TryReadSensorDuckHeld())
            return true;
        if (TryReadJoystickDown(out bool down))
            return down;
        return Input.GetKey(downKey);
    }

    private bool LeanLeftDown()
    {
        if (UsesLinkedGestureInput())
            return gestureInput.LeanLeftDown;
        if (TryReadSensorLeanLeftDown())
            return true;
        if (TryReadJoystickLeftDown(out bool left))
            return left;
        return Input.GetKeyDown(leftKey);
    }

    private bool LeanRightDown()
    {
        if (UsesLinkedGestureInput())
            return gestureInput.LeanRightDown;
        if (TryReadSensorLeanRightDown())
            return true;
        if (TryReadJoystickRightDown(out bool right))
            return right;
        return Input.GetKeyDown(rightKey);
    }

    private bool IsSensorPlayerBug()
    {
        return gestureInput != null && gestureInput.gameObject.name.Contains("Left");
    }

    private bool TryReadSensorJumpHeld()
    {
        if (!IsSensorPlayerBug())
            return false;
        if (!GestureInput.TryGetLiveHandDistances(gestureInput, out int leftMm, out int rightMm))
            return false;
        return GestureInput.BothHandsFlapping(_leftFlapTracker, _rightFlapTracker, leftMm, rightMm);
    }

    private bool TryReadSensorDuckHeld()
    {
        if (!IsSensorPlayerBug())
            return false;
        if (!GestureInput.TryGetLiveHandDistances(gestureInput, out int leftMm, out int rightMm))
            return false;
        return GestureInput.DuckHeldFromDistances(leftMm, rightMm);
    }

    private bool TryReadSensorLeanLeftDown()
    {
        if (!IsSensorPlayerBug())
            return false;
        if (!GestureInput.TryGetLiveHandDistances(gestureInput, out int leftMm, out int rightMm))
            return false;
        bool held = GestureInput.LeanLeftHeldFromDistances(leftMm, rightMm);
        bool down = held && !_sensorPrevLeanLeftHeld;
        _sensorPrevLeanLeftHeld = held;
        return down;
    }

    private bool TryReadSensorLeanRightDown()
    {
        if (!IsSensorPlayerBug())
            return false;
        if (!GestureInput.TryGetLiveHandDistances(gestureInput, out int leftMm, out int rightMm))
            return false;
        bool held = GestureInput.LeanRightHeldFromDistances(leftMm, rightMm);
        bool down = held && !_sensorPrevLeanRightHeld;
        _sensorPrevLeanRightHeld = held;
        return down;
    }

    private bool IsJoystickPlayerBug()
    {
        return gestureInput != null && gestureInput.gameObject.name.Contains("Right");
    }

    private bool TryReadJoystickUp(out bool up)
    {
        up = false;
        if (!IsJoystickPlayerBug())
            return false;
        JoystickSerial serial = JoystickSerial.Instance;
        if (serial != null && serial.IsConnected)
        {
            up = serial.Up;
            return true;
        }
        if (UsesLinkedJoystickInput())
        {
            up = joystickInput.UpHeld;
            return true;
        }
        return false;
    }

    private bool TryReadJoystickDown(out bool down)
    {
        down = false;
        if (!IsJoystickPlayerBug())
            return false;
        JoystickSerial serial = JoystickSerial.Instance;
        if (serial != null && serial.IsConnected)
        {
            down = serial.Down;
            return true;
        }
        if (UsesLinkedJoystickInput())
        {
            down = joystickInput.DownHeld;
            return true;
        }
        return false;
    }

    private bool TryReadJoystickLeftDown(out bool left)
    {
        left = false;
        if (!IsJoystickPlayerBug())
            return false;
        JoystickSerial serial = JoystickSerial.Instance;
        if (serial != null && serial.IsConnected)
        {
            bool held = serial.Left;
            left = held && !_joystickPrevLeftHeld;
            _joystickPrevLeftHeld = held;
            return true;
        }
        if (UsesLinkedJoystickInput())
        {
            left = joystickInput.LeftDown;
            return true;
        }
        return false;
    }

    private bool TryReadJoystickRightDown(out bool right)
    {
        right = false;
        if (!IsJoystickPlayerBug())
            return false;
        JoystickSerial serial = JoystickSerial.Instance;
        if (serial != null && serial.IsConnected)
        {
            bool held = serial.Right;
            right = held && !_joystickPrevRightHeld;
            _joystickPrevRightHeld = held;
            return true;
        }
        if (UsesLinkedJoystickInput())
        {
            right = joystickInput.RightDown;
            return true;
        }
        return false;
    }

    private void Awake()
    {
        CaptureRestPose();
    }

    private void Start()
    {
        // Layout is final by Start — re-read in case Awake ran too early.
        CaptureRestPose();
        ApplyRestPose();
    }

    private void OnEnable()
    {
        CaptureRestPose();
        ApplyRestPose();
        _settleUntil = Time.time + 0.2f;
        _sensorPrevLeanLeftHeld = false;
        _sensorPrevLeanRightHeld = false;
        _joystickPrevLeftHeld = false;
        _joystickPrevRightHeld = false;
    }

    private void CaptureRestPose()
    {
        if (bugImage == null)
            return;

        _restPos = bugImage.rectTransform.anchoredPosition;
        _restScale = bugImage.rectTransform.localScale;
    }

    public void ApplyBugLook(Texture2D normal, Texture2D air1, Texture2D air2, Color tint)
    {
        bugNormalTexture = normal;
        bugAirTexture1 = air1;
        bugAirTexture2 = air2;
        if (bugImage == null)
            return;

        bugImage.color = tint;
        if (normal != null)
            bugImage.texture = normal;
    }

    private void ApplyRestPose()
    {
        if (bugImage == null)
            return;

        RectTransform rt = bugImage.rectTransform;
        rt.anchoredPosition = _restPos;
        rt.localScale = _restScale;
        rt.localRotation = Quaternion.identity;
        _laneIndex = 0;
        _flapFrameTimer = 0f;
        if (bugNormalTexture != null)
            bugImage.texture = bugNormalTexture;
    }

    private void Update()
    {
        if (bugImage == null)
            return;

        if (Time.time < _settleUntil)
            return;

        if (LeanLeftDown())
            _laneIndex = Mathf.Max(_laneIndex - 1, -1);
        else if (LeanRightDown())
            _laneIndex = Mathf.Min(_laneIndex + 1, 1);

        bool up = UpHeld();
        bool down = !up && DownHeld();

        Vector3 targetScale = _restScale;
        float laneX = _laneIndex < 0 ? laneXLeft : _laneIndex > 0 ? laneXRight : laneXCenter;
        Vector2 targetPos = new Vector2(laneX, _restPos.y);

        if (up)
        {
            targetPos += new Vector2(0f, flyRise);
            _flapFrameTimer += Time.deltaTime;
            if (_flapFrameTimer >= flapFrameDuration)
            {
                _flapFrameTimer = 0f;
                _flapFrameToggle = !_flapFrameToggle;
                Texture2D frame = _flapFrameToggle ? bugAirTexture2 : bugAirTexture1;
                if (frame != null)
                    bugImage.texture = frame;
            }
        }
        else
        {
            _flapFrameTimer = 0f;
            if (bugNormalTexture != null)
                bugImage.texture = bugNormalTexture;

            if (down)
            {
                targetScale = new Vector3(_restScale.x, _restScale.y * duckSquash, _restScale.z);
                // Shrink from the bottom, not the center — same reasoning as
                // GestureDiagramAnimation's own duck squash — then drop the
                // whole squashed shape down further still (duckDrop).
                float fullHeight = bugImage.rectTransform.sizeDelta.y * _restScale.y;
                float squashedHeight = bugImage.rectTransform.sizeDelta.y * targetScale.y;
                targetPos += new Vector2(0f, -(fullHeight - squashedHeight) / 2f - duckDrop);
            }
        }

        RectTransform rt = bugImage.rectTransform;
        float ease = Time.deltaTime * poseLerpSpeed;
        Vector2 previousPos = rt.anchoredPosition;
        rt.anchoredPosition = Vector2.Lerp(previousPos, targetPos, ease);
        rt.localScale = Vector3.Lerp(rt.localScale, targetScale, ease);

        // Leans INTO an active lane change, same as the real in-game lean
        // (PlayerController.laneTiltAngle) — driven by the actual per-frame
        // movement itself rather than a held key, so it eases back to
        // upright on its own once the lerp above catches up to the new
        // lane, instead of staying tilted for as long as some input state
        // is (or isn't) held.
        float dx = rt.anchoredPosition.x - previousPos.x;
        float targetTilt = Mathf.Abs(dx) > 0.0001f ? -Mathf.Sign(dx) * leanTiltAngle : 0f;
        float currentTilt = rt.localEulerAngles.z;
        if (currentTilt > 180f)
            currentTilt -= 360f;
        float newTilt = Mathf.MoveTowards(currentTilt, targetTilt, leanTiltAngle * 6f * Time.deltaTime);
        rt.localRotation = Quaternion.Euler(0f, 0f, newTilt);
    }
}
