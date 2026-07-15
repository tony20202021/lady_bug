using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private int laneCount = 3;
    [SerializeField] private float laneWidth = 3f;
    [SerializeField] private float laneChangeSpeed = 11f;
    [SerializeField] private float laneRepeatDelay = 0.35f;
    [SerializeField] private float laneRepeatInterval = 0.16f;
    [SerializeField] private int startLane = -1; // -1 = default to the middle lane

    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode downKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode accelKey = KeyCode.I;
    [SerializeField] private KeyCode brakeKey = KeyCode.J;

    [SerializeField] private float jumpHeightDelta = 1.4f;
    [SerializeField] private float jumpDuration = 0.9f;

    [SerializeField] private float duckHeightDelta = 0.4f;

    [SerializeField] private float heightChangeSpeed = 8f;

    [SerializeField] private float tumbleDuration = 0.8f;
    [SerializeField] private float tumbleHopHeight = 1f;
    [SerializeField] private float blinkDuration = 1.5f;
    [SerializeField] private float blinkInterval = 0.12f;

    private int _currentLane;
    private float _baseScaleY;
    private float _baseGroundY;
    private float _actionTimer;
    private float _laneRepeatTimer;
    private bool _bounceRising; // Bouncing sub-phase: rising back up vs resting on the occupant
    private bool _bounceCharging; // holding Up mid-bounce for a higher hop — timed, like a ground jump
    private bool _bounceChargeConsumed; // charge timed out while still held — needs a release before charging again

    // Recent-move bookkeeping used to detect the "ring" trick — a full lane
    // swap where one player crosses over airborne while the other passes
    // under them at the same time.
    private int _lastMoveFromLane = -1;
    private int _lastMoveToLane = -1;
    private float _lastMoveTime = -999f;
    private bool _lastMoveWasAirborne;
    private static float _lastRingTrickTime = -999f;
    private const float RingTrickWindow = 0.5f;
    private const float RingTrickCooldown = 0.3f;

    // Bouncing = landed mid-air on top of another (grounded) player sharing
    // the lane — self-contained oscillation, never touches Jumping, so it
    // can't be cancelled by jump-key release like a normal jump can.
    private enum VerticalState { Normal, Jumping, Ducking, Bouncing }
    private VerticalState _verticalState = VerticalState.Normal;

    private enum CrashState { None, Tumbling, Blinking }
    private CrashState _crashState = CrashState.None;
    private float _crashTimer;
    private Renderer _spriteRenderer;

    // When present and enabled (controller mode = gesture simulator/sensors),
    // these stand in for the usual key reads — see the wrapper methods below.
    private GestureInput _gestureInput;
    private bool GestureActive => _gestureInput != null && _gestureInput.enabled;

    public int CurrentLane => _currentLane;
    public int LaneCount => laneCount;
    public int HomeLane => startLane;
    public bool IsAirborne => _verticalState == VerticalState.Jumping || _verticalState == VerticalState.Bouncing;
    public bool IsDucking => _verticalState == VerticalState.Ducking;
    public bool IsBraking => GestureActive ? _gestureInput.CurrentlyBraking : Input.GetKey(brakeKey);
    public float TopY => transform.position.y + transform.localScale.y / 2f;

    // Used by the start screen to preview where a player will stand before
    // Start() has run (e.g. centering the sole player in 1-player mode).
    public void SetPreviewLane(int lane)
    {
        startLane = lane;
        Vector3 pos = transform.position;
        pos.x = (lane - (laneCount - 1) / 2f) * laneWidth;
        transform.position = pos;
    }

    private void Start()
    {
        _currentLane = startLane >= 0 ? startLane : laneCount / 2;
        _baseScaleY = transform.localScale.y;
        _baseGroundY = transform.position.y - _baseScaleY / 2f;

        Transform sprite = transform.Find("Sprite");
        if (sprite != null)
            _spriteRenderer = sprite.GetComponent<Renderer>();

        _gestureInput = GetComponent<GestureInput>();
    }

    // Gesture-mode stand-ins for the usual key reads — fall straight through
    // to plain keyboard reads whenever gesture mode isn't active for this
    // player (i.e. keyboard controller selected — behaves exactly as before).
    private bool UpKeyDown() => GestureActive ? _gestureInput.JumpDown : Input.GetKeyDown(upKey);
    private bool UpKeyHeld() => GestureActive ? _gestureInput.JumpHeld : Input.GetKey(upKey);
    private bool DownKeyHeld() => GestureActive ? _gestureInput.DuckHeld : Input.GetKey(downKey);
    private bool LeftKeyDown() => GestureActive ? _gestureInput.LeanLeftDown : Input.GetKeyDown(leftKey);
    private bool LeftKeyHeld() => GestureActive ? _gestureInput.LeanLeftHeld : Input.GetKey(leftKey);
    private bool RightKeyDown() => GestureActive ? _gestureInput.LeanRightDown : Input.GetKeyDown(rightKey);
    private bool RightKeyHeld() => GestureActive ? _gestureInput.LeanRightHeld : Input.GetKey(rightKey);

    private void Update()
    {
        switch (_crashState)
        {
            case CrashState.Tumbling:
                UpdateTumble();
                return; // no lane/jump/duck input while spinning
            case CrashState.Blinking:
                UpdateBlink();
                break; // falls through — player can still move while invulnerable
        }

        HandleInput();
        UpdateLanePosition();
        UpdateVerticalState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_crashState != CrashState.None)
            return; // mid-hit-reaction — ignore further collisions

        MovingEntity entity = other.GetComponentInParent<MovingEntity>();
        if (entity == null)
            return;

        DuckUnderObstacle arch = entity.GetComponent<DuckUnderObstacle>();
        if (arch != null)
        {
            if (_verticalState == VerticalState.Ducking)
            {
                // Checked from here, not the rider's own collision, because
                // this collider's height is stable and always overlaps the
                // arch — the rider's swings between resting-on-back and full
                // jump height and can pass above the arch's collider
                // entirely, so their own trigger doesn't always fire.
                PlayerController partner = FindPartnerInLane(_currentLane);
                if (partner != null && partner.IsAirborne)
                    AwardArchTrickOnce(arch, Midpoint(partner));
                return; // ducked under it successfully
            }

            if (IsAirborne)
            {
                PlayerController partner = FindPartnerInLane(_currentLane);
                if (partner != null && partner.IsDucking)
                {
                    AwardArchTrickOnce(arch, Midpoint(partner)); // in case this fires instead of/as well as the ducker's
                    return; // bounced clean over a ducking partner
                }
            }
        }

        ScoreValue scoreValue = entity.GetComponent<ScoreValue>();
        if (scoreValue != null)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.SpawnPopup(scoreValue.value, transform.position);

            if (scoreValue.value < 0)
                StartCrash();

            Destroy(entity.gameObject);
            return;
        }

        StartCrash();
    }

    private PlayerController FindPartnerInLane(int lane)
    {
        foreach (var other in FindObjectsOfType<PlayerController>())
        {
            if (other != this && other.CurrentLane == lane)
                return other;
        }
        return null;
    }

    private Vector3 Midpoint(PlayerController other) => (transform.position + other.transform.position) / 2f;

    private static void AwardArchTrickOnce(DuckUnderObstacle arch, Vector3 worldPos)
    {
        if (arch.TrickAwarded)
            return;

        arch.TrickAwarded = true;
        if (TricksManager.Instance != null)
            TricksManager.Instance.SpawnPopup("АРКА", worldPos);
    }

    private void StartCrash()
    {
        _crashState = CrashState.Tumbling;
        _crashTimer = 0f;

        // Reset any jump/duck in progress so the tumble starts from a clean pose.
        _verticalState = VerticalState.Normal;
        Vector3 scale = transform.localScale;
        scale.y = _baseScaleY;
        transform.localScale = scale;
        transform.rotation = Quaternion.identity;

        if (SpeedController.Instance != null)
            SpeedController.Instance.HalveSpeed();
    }

    private void UpdateTumble()
    {
        _crashTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_crashTimer / tumbleDuration);

        Vector3 pos = transform.position;
        pos.y = _baseGroundY + _baseScaleY / 2f + Mathf.Sin(t * Mathf.PI) * tumbleHopHeight;
        transform.position = pos;

        // Two full turns over the course of the hop.
        transform.rotation = Quaternion.Euler(0f, 0f, t * 720f);

        if (_crashTimer >= tumbleDuration)
        {
            transform.rotation = Quaternion.identity;
            Vector3 landed = transform.position;
            landed.y = _baseGroundY + _baseScaleY / 2f;
            transform.position = landed;

            _crashState = CrashState.Blinking;
            _crashTimer = 0f;
        }
    }

    private void UpdateBlink()
    {
        _crashTimer += Time.deltaTime;

        if (_spriteRenderer != null)
        {
            bool visible = Mathf.FloorToInt(_crashTimer / blinkInterval) % 2 == 0;
            _spriteRenderer.enabled = visible;
        }

        if (_crashTimer >= blinkDuration)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;
            _crashState = CrashState.None;
        }
    }

    private void HandleInput()
    {
        HandleLaneInput();
        HandleSpeedInput();

        if (_verticalState == VerticalState.Normal)
        {
            if (UpKeyDown())
            {
                _verticalState = VerticalState.Jumping;
                _actionTimer = jumpDuration;
            }
            else if (DownKeyHeld())
            {
                _verticalState = VerticalState.Ducking;
            }
        }
        else if (_verticalState == VerticalState.Ducking)
        {
            // Held, not timed — stays down for as long as the key is down.
            if (!DownKeyHeld())
                _verticalState = VerticalState.Normal;
        }
        else if (_verticalState == VerticalState.Jumping)
        {
            // Held up to the jumpDuration cap (enforced in UpdateVerticalState);
            // release early and it comes down immediately — unless someone's
            // still grounded in this lane, then it bounces off them instead.
            // Jumps are normally a tap, so this fires almost every time —
            // it must go through the same occupant check as the timer expiry.
            if (!UpKeyHeld())
                EndJump();
        }
    }

    // Resolves a Jump that's ending (early release or timer expiry): lands
    // normally, or — if another player is still grounded in this lane —
    // bounces off them instead, never touching the road.
    private void EndJump()
    {
        bool blocked = FindLandingBlocker(_currentLane) != null;
        if (blocked)
        {
            _verticalState = VerticalState.Bouncing;
            _bounceRising = false; // dip down onto their back first
            _bounceCharging = false;
            _bounceChargeConsumed = false;
            _actionTimer = jumpDuration * 0.4f;
        }
        else
        {
            _verticalState = VerticalState.Normal;
        }
    }

    private void HandleLaneInput()
    {
        // First tap moves one lane immediately; holding the key keeps moving
        // further after a short delay, then repeats at a steady rate.
        if (LeftKeyDown())
        {
            MoveLane(-1);
            _laneRepeatTimer = laneRepeatDelay;
        }
        else if (RightKeyDown())
        {
            MoveLane(1);
            _laneRepeatTimer = laneRepeatDelay;
        }
        else if (LeftKeyHeld() || RightKeyHeld())
        {
            _laneRepeatTimer -= Time.deltaTime;
            if (_laneRepeatTimer <= 0f)
            {
                MoveLane(LeftKeyHeld() ? -1 : 1);
                _laneRepeatTimer = laneRepeatInterval;
            }
        }
    }

    private void HandleSpeedInput()
    {
        if (SpeedController.Instance == null)
            return;

        // In gesture mode, GestureInput votes accel/brake directly from wave
        // detection in its own Update() — nothing to do here.
        if (GestureActive)
            return;

        // Each held key casts one vote per frame — SpeedController sums
        // votes from both players (LateUpdate, after everyone's Update runs).
        if (Input.GetKey(accelKey))
            SpeedController.Instance.RegisterAccel();
        if (Input.GetKey(brakeKey))
            SpeedController.Instance.RegisterBrake();
    }

    private void MoveLane(int direction)
    {
        int target = Mathf.Clamp(_currentLane + direction, 0, laneCount - 1);
        if (target == _currentLane || IsLaneOccupiedByOther(target))
            return;

        int previousLane = _currentLane;
        _currentLane = target;

        _lastMoveFromLane = previousLane;
        _lastMoveToLane = target;
        _lastMoveTime = Time.time;
        _lastMoveWasAirborne = IsAirborne;

        TryDetectRingTrick();
    }

    // "Ring" trick: two players cross through each other's lanes at once —
    // one airborne, one grounded — a full swap of places, like passing
    // through a ring. Works in either direction; whoever completes the
    // swap second is the one that notices and scores it.
    private void TryDetectRingTrick()
    {
        if (Time.time - _lastRingTrickTime < RingTrickCooldown)
            return; // already scored this exact swap from the other player's side

        foreach (var other in FindObjectsOfType<PlayerController>())
        {
            if (other == this)
                continue;

            bool recentEnough = Time.time - other._lastMoveTime <= RingTrickWindow;
            bool isSwap = other._lastMoveFromLane == _lastMoveToLane && other._lastMoveToLane == _lastMoveFromLane;
            bool oneAirborneOneGrounded = _lastMoveWasAirborne != other._lastMoveWasAirborne;

            if (recentEnough && isSwap && oneAirborneOneGrounded)
            {
                _lastRingTrickTime = Time.time;
                if (TricksManager.Instance != null)
                    TricksManager.Instance.SpawnPopup("КОЛЬЦО", Midpoint(other));
                return;
            }
        }
    }

    // Blocked only if BOTH players would be grounded in that lane at once —
    // either one being airborne (jumping/bouncing) is enough to pass through,
    // whichever side is in the air.
    private bool IsLaneOccupiedByOther(int lane)
    {
        return !IsAirborne && FindGroundedOccupant(lane) != null;
    }

    private PlayerController FindGroundedOccupant(int lane)
    {
        foreach (var other in FindObjectsOfType<PlayerController>())
        {
            if (other == this)
                continue;
            if (other.CurrentLane == lane && !other.IsAirborne)
                return other;
        }
        return null;
    }

    // Used when a jump/bounce is resolving whether it can touch down. Blocks
    // if another player is grounded here — or, if both players are mid-air
    // in the same lane at once (simultaneous jumps merging into it), the tie
    // is broken by a stable priority (lower instance ID lands first) so
    // exactly one of them lands while the other keeps bouncing on top,
    // instead of both dropping onto the road together.
    private PlayerController FindLandingBlocker(int lane)
    {
        PlayerController grounded = null;
        PlayerController airbornePeer = null;

        foreach (var other in FindObjectsOfType<PlayerController>())
        {
            if (other == this || other.CurrentLane != lane)
                continue;

            if (!other.IsAirborne)
                grounded = other;
            else if (GetInstanceID() > other.GetInstanceID())
                airbornePeer = other; // the lower ID wins the tie and lands
        }

        return grounded != null ? grounded : airbornePeer;
    }

    private void UpdateLanePosition()
    {
        float targetX = (_currentLane - (laneCount - 1) / 2f) * laneWidth;
        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX, laneChangeSpeed * Time.deltaTime);
        transform.position = pos;
    }

    private void UpdateVerticalState()
    {
        // Jump lifts the whole cube off the road (position moves, scale stays put).
        // Duck shrinks the cube while keeping its base anchored to the road.
        float targetScaleY = _baseScaleY;
        float targetPosY = _baseGroundY + _baseScaleY / 2f;

        if (_verticalState == VerticalState.Jumping)
        {
            targetPosY = _baseGroundY + _baseScaleY / 2f + jumpHeightDelta;
        }
        else if (_verticalState == VerticalState.Ducking)
        {
            targetScaleY = Mathf.Max(0.2f, _baseScaleY - duckHeightDelta);
            targetPosY = _baseGroundY + targetScaleY / 2f;
        }
        else if (_verticalState == VerticalState.Bouncing)
        {
            // Self-contained oscillation between "resting on the other
            // player's back" and "up at full jump height" — never dips to
            // road level while they're still grounded in this lane. Holding
            // Up charges a higher hop — the same height gain as a solo jump,
            // but measured from the partner's back instead of the road (so
            // a ducked partner naturally starts the hop a bit lower). Follows
            // _bounceCharging (not the raw key) so it drops back out of the
            // charged height once the timeout consumes the charge, even if
            // the key is still held down.
            PlayerController occupant = FindLandingBlocker(_currentLane);
            if (occupant != null && _bounceCharging)
                targetPosY = occupant.TopY + _baseScaleY / 2f + jumpHeightDelta;
            else if (_bounceRising || occupant == null)
                targetPosY = _baseGroundY + _baseScaleY / 2f + jumpHeightDelta;
            else
                targetPosY = occupant.TopY + _baseScaleY / 2f;
        }

        Vector3 scale = transform.localScale;
        scale.y = Mathf.MoveTowards(scale.y, targetScaleY, heightChangeSpeed * Time.deltaTime);
        transform.localScale = scale;

        Vector3 pos = transform.position;
        pos.y = Mathf.MoveTowards(pos.y, targetPosY, heightChangeSpeed * Time.deltaTime);
        transform.position = pos;

        // Only Jump/Bounce are timed — Ducking reverts as soon as the key is
        // released (handled in HandleInput), not on a countdown.
        if (_verticalState == VerticalState.Jumping)
        {
            _actionTimer -= Time.deltaTime;
            if (_actionTimer <= 0f)
                EndJump();
        }
        else if (_verticalState == VerticalState.Bouncing)
        {
            PlayerController occupant = FindLandingBlocker(_currentLane);
            bool holdingUp = occupant != null && UpKeyHeld();

            if (!holdingUp)
                _bounceChargeConsumed = false; // released — armed for another charge

            if (holdingUp && !_bounceCharging && !_bounceChargeConsumed)
            {
                // Start a charge — capped at jumpDuration even if held
                // longer, same timeout a ground jump has.
                _bounceCharging = true;
                _actionTimer = jumpDuration;
            }
            else if (!holdingUp && _bounceCharging)
            {
                // Released early — falls back into the ordinary auto-cycle,
                // same as releasing a ground jump early brings it down.
                _bounceCharging = false;
                _bounceRising = false;
                _actionTimer = jumpDuration * 0.4f;
            }

            _actionTimer -= Time.deltaTime;
            if (_actionTimer <= 0f)
            {
                if (occupant == null)
                {
                    _verticalState = VerticalState.Normal; // lane's clear — fall the rest of the way down
                }
                else
                {
                    // Timed out while still held — needs a fresh release+press
                    // to charge again, so holding Up doesn't hang up forever.
                    if (_bounceCharging)
                        _bounceChargeConsumed = true;
                    _bounceCharging = false;
                    _bounceRising = !_bounceRising; // flip between resting on them and hopping back up
                    _actionTimer = jumpDuration * 0.4f;
                }
            }
        }
    }
}
