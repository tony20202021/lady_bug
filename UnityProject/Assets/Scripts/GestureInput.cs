using UnityEngine;

// Reads a pair of downward-facing distance sensors (one per hand) and turns
// them into the same virtual signals PlayerController normally reads from
// keys. Real sensor hardware isn't connected yet, so for now this always
// reads its six keys as a keyboard stand-in — each hand's sensor range is
// simulated as three explicit zones (its own row of keys): near/top, middle,
// far/bottom. Swapping in real hardware later only means replacing
// ReadHand()'s body with an actual distance reading.
//
// Gestures (assumes the player faces the screen, sensors mounted above each
// hand looking straight down):
//   both hands top      -> jump
//   both hands bottom    -> duck
//   one top, one bottom  -> lean toward whichever hand is at the bottom
//   both middle keys tapped together (in sync)  -> accelerate ("оба вперёд")
//   both middle keys tapped one after the other (out of sync) -> brake
//     ("оба назад") — this half of the pair wasn't specified, so it was
//     picked to mirror accelerate using the same middle-zone tap, just with
//     the two hands out of phase instead of in phase.
public class GestureInput : MonoBehaviour
{
    private enum HandState { Neutral, Up, Down }

    [SerializeField] private KeyCode leftHandUpKey;
    [SerializeField] private KeyCode leftHandMiddleKey;
    [SerializeField] private KeyCode leftHandDownKey;
    [SerializeField] private KeyCode rightHandUpKey;
    [SerializeField] private KeyCode rightHandMiddleKey;
    [SerializeField] private KeyCode rightHandDownKey;

    [SerializeField] private float syncTolerance = 0.15f; // middle taps closer together than this = in sync
    [SerializeField] private float sequenceWindow = 0.6f; // taps further apart than this aren't a paired gesture at all
    [SerializeField] private float burstDuration = 0.3f; // how long a detected beat keeps voting accel/brake

    private HandState _leftHand;
    private HandState _rightHand;

    private float _lastLeftMiddleDown = -999f;
    private float _lastRightMiddleDown = -999f;
    private float _accelBurstUntil = -999f;
    private float _brakeBurstUntil = -999f;

    public bool LeftHandUp => _leftHand == HandState.Up;
    public bool LeftHandDown => _leftHand == HandState.Down;
    public bool RightHandUp => _rightHand == HandState.Up;
    public bool RightHandDown => _rightHand == HandState.Down;

    // Both hands raised/lowered together — mirrors a held Up/Down key.
    public bool JumpHeld { get; private set; }
    public bool JumpDown { get; private set; }
    public bool DuckHeld { get; private set; }

    // One hand up, the other down — mirrors a held/just-pressed lane key.
    public bool LeanLeftHeld { get; private set; }
    public bool LeanLeftDown { get; private set; }
    public bool LeanRightHeld { get; private set; }
    public bool LeanRightDown { get; private set; }

    public bool CurrentlyAccelerating { get; private set; }
    public bool CurrentlyBraking { get; private set; }

    private void Update()
    {
        _leftHand = ReadHand(leftHandUpKey, leftHandDownKey);
        _rightHand = ReadHand(rightHandUpKey, rightHandDownKey);

        bool wasJumpHeld = JumpHeld;
        JumpHeld = _leftHand == HandState.Up && _rightHand == HandState.Up;
        JumpDown = JumpHeld && !wasJumpHeld;

        DuckHeld = _leftHand == HandState.Down && _rightHand == HandState.Down;

        bool wasLeanLeftHeld = LeanLeftHeld;
        LeanLeftHeld = _leftHand == HandState.Down && _rightHand == HandState.Up;
        LeanLeftDown = LeanLeftHeld && !wasLeanLeftHeld;

        bool wasLeanRightHeld = LeanRightHeld;
        LeanRightHeld = _leftHand == HandState.Up && _rightHand == HandState.Down;
        LeanRightDown = LeanRightHeld && !wasLeanRightHeld;

        if (Input.GetKeyDown(leftHandMiddleKey))
        {
            _lastLeftMiddleDown = Time.time;
            EvaluateMiddleBeat();
        }
        if (Input.GetKeyDown(rightHandMiddleKey))
        {
            _lastRightMiddleDown = Time.time;
            EvaluateMiddleBeat();
        }

        CurrentlyAccelerating = Time.time < _accelBurstUntil;
        CurrentlyBraking = Time.time < _brakeBurstUntil;

        if (SpeedController.Instance != null)
        {
            if (CurrentlyAccelerating)
                SpeedController.Instance.RegisterAccel();
            else if (CurrentlyBraking)
                SpeedController.Instance.RegisterBrake();
        }
    }

    // A hand just tapped its middle zone — see if the other hand tapped
    // theirs recently too, and if so, whether they landed in or out of sync.
    private void EvaluateMiddleBeat()
    {
        float gap = Mathf.Abs(_lastLeftMiddleDown - _lastRightMiddleDown);
        if (gap > sequenceWindow)
            return; // too far apart to be a deliberate paired gesture

        if (gap <= syncTolerance)
            _accelBurstUntil = Time.time + burstDuration;
        else
            _brakeBurstUntil = Time.time + burstDuration;
    }

    private static HandState ReadHand(KeyCode upKey, KeyCode downKey)
    {
        bool up = Input.GetKey(upKey);
        bool down = Input.GetKey(downKey);
        if (up && !down) return HandState.Up;
        if (down && !up) return HandState.Down;
        return HandState.Neutral;
    }
}
