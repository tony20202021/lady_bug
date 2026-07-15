using UnityEngine;

public class SpeedController : MonoBehaviour
{
    public static SpeedController Instance { get; private set; }

    [SerializeField] private float minSpeed = 3f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float accelerationRate = 8f;
    [SerializeField] private float brakeRate = 15f;
    [SerializeField] private float dragRate = 1.5f;

    public float CurrentSpeed { get; private set; }

    private bool _winBoost;
    private float _winAccel;
    private bool _gameStarted;
    private bool _paused;
    private float _speedBeforePause;

    private int _accelVotes;
    private int _brakeVotes;

    public bool IsRunning => _gameStarted;
    public bool IsAtMinSpeed => CurrentSpeed <= minSpeed + 0.01f;

    private void Awake()
    {
        Instance = this;
        // Ambient idle scroll on the start screen — road, dashed lines and
        // side scenery drift by in the background instead of sitting frozen.
        CurrentSpeed = minSpeed;
    }

    /// <summary>Called by the start screen once a mode is confirmed — releases the road.</summary>
    public void BeginGame()
    {
        _gameStarted = true;
        CurrentSpeed = minSpeed;
    }

    /// <summary>Called by the pause dialog — freezes the road (and everything driven by
    /// CurrentSpeed: entities, dashed lines, spawners) without losing the pre-pause speed.</summary>
    public void SetPaused(bool paused)
    {
        if (paused == _paused)
            return;

        _paused = paused;
        if (paused)
        {
            _speedBeforePause = CurrentSpeed;
            CurrentSpeed = 0f;
        }
        else
        {
            CurrentSpeed = _speedBeforePause;
        }
    }

    /// <summary>Called on collision — the road slows but keeps moving, never below the floor.</summary>
    public void HalveSpeed()
    {
        CurrentSpeed = Mathf.Max(CurrentSpeed * 0.5f, minSpeed);
    }

    /// <summary>Victory sequence — speed climbs forever, faster and faster, ignoring input.</summary>
    public void BeginWinBoost()
    {
        _winBoost = true;
        _winAccel = accelerationRate;
    }

    // Each player casts one "vote" per frame from their own accel/brake keys.
    // Same direction adds up (both accelerating = double rate); opposite
    // votes cancel out (net zero — same as nobody pressing anything).
    public void RegisterAccel() => _accelVotes++;
    public void RegisterBrake() => _brakeVotes++;

    private void LateUpdate()
    {
        if (!_gameStarted || _paused)
        {
            _accelVotes = 0;
            _brakeVotes = 0;
            return; // still on the start screen, or frozen behind the pause dialog
        }

        // LateUpdate runs after every player's Update has cast its vote for
        // this frame, regardless of script execution order between them.
        if (_winBoost)
        {
            _winAccel += 5f * Time.deltaTime; // the acceleration itself keeps ramping up
            CurrentSpeed += _winAccel * Time.deltaTime;
            return;
        }

        int net = _accelVotes - _brakeVotes;
        _accelVotes = 0;
        _brakeVotes = 0;

        if (net > 0)
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, maxSpeed, accelerationRate * net * Time.deltaTime);
        else if (net < 0)
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, minSpeed, brakeRate * -net * Time.deltaTime);
        else
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, minSpeed, dragRate * Time.deltaTime);
    }
}
