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

    [SerializeField] private float duckSquash = 0.6f;
    // Smaller than GestureDiagramAnimation's own 220 (the ОБРАЗЕЦ side has
    // a whole page-width column to itself) — this bug sits closer to its
    // column's own edge, next to the dashed divider, so the same shift
    // used to reach across it into the ОБРАЗЕЦ half on a left lean.
    [SerializeField] private float leanShift = 100f;
    [SerializeField] private float leanTiltAngle = 12f;
    [SerializeField] private float flyRise = 50f;
    [SerializeField] private float flapFrameDuration = 0.15f;
    // How fast the pose eases toward whatever's currently held — not an
    // instant snap, same eased feel the real in-game moves have.
    [SerializeField] private float poseLerpSpeed = 10f;

    private Vector2 _restPos;
    private Vector3 _restScale = Vector3.one;
    private float _flapFrameTimer;
    private bool _flapFrameToggle;
    // -1/0/+1 — real lane changes in the actual game are sticky (you stay
    // in whatever lane you moved to, you don't spring back once you let go
    // of the key/gesture), unlike jump/duck which are momentary. Clamped to
    // one step either way so it can never wander past this column's own
    // width into the divider or off the page edge — see leanShift's comment.
    private int _laneIndex;

    private bool GestureActive => gestureInput != null && gestureInput.enabled;
    private bool JoystickActive => joystickInput != null && joystickInput.enabled;

    private bool UpHeld() => GestureActive ? gestureInput.JumpHeld : JoystickActive ? joystickInput.UpHeld : Input.GetKey(upKey);
    private bool DownHeld() => GestureActive ? gestureInput.DuckHeld : JoystickActive ? joystickInput.DownHeld : Input.GetKey(downKey);
    private bool LeanLeftDown() => GestureActive ? gestureInput.LeanLeftDown : JoystickActive ? joystickInput.LeftDown : Input.GetKeyDown(leftKey);
    private bool LeanRightDown() => GestureActive ? gestureInput.LeanRightDown : JoystickActive ? joystickInput.RightDown : Input.GetKeyDown(rightKey);

    private void Awake()
    {
        if (bugImage != null)
        {
            _restPos = bugImage.rectTransform.anchoredPosition;
            _restScale = bugImage.rectTransform.localScale;
        }
    }

    private void Update()
    {
        if (bugImage == null)
            return;

        if (LeanLeftDown())
            _laneIndex = Mathf.Max(_laneIndex - 1, -1);
        else if (LeanRightDown())
            _laneIndex = Mathf.Min(_laneIndex + 1, 1);

        bool up = UpHeld();
        bool down = !up && DownHeld();

        Vector3 targetScale = _restScale;
        Vector2 targetPos = _restPos + new Vector2(_laneIndex * leanShift, 0f);

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
                // GestureDiagramAnimation's own duck squash.
                float fullHeight = bugImage.rectTransform.sizeDelta.y * _restScale.y;
                float squashedHeight = bugImage.rectTransform.sizeDelta.y * targetScale.y;
                targetPos += new Vector2(0f, -(fullHeight - squashedHeight) / 2f);
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
