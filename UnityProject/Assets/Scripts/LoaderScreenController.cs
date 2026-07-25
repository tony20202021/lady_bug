using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// First screen shown when the game boots — a plain "attract mode" idle
// screen mimicking a real arcade cabinet: a schematic of the physical
// control panel at the bottom (see SceneSetup.CreateLoaderScreen) with a
// prompt cycling through what to try, sliding in from the right edge,
// pausing centered, then sliding out the left — a different randomly
// chosen control on the panel is framed to match whichever prompt is
// showing. On a real cabinet a real controller drives the handoff; for now
// (debug) holding down any key stands in for it: pressing one hands off to
// IntroSequence's flower/countdown screen, and releasing before that
// countdown finishes aborts back here instead of letting it complete.
public class LoaderScreenController : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private IntroSequence introSequence;
    [SerializeField] private Text messageText;

    private static readonly string[] Messages =
    {
        "НАЖМИТЕ ЛЮБУЮ КНОПКУ",
        "НАКРОЙТЕ ЛЮБОЙ ДАТЧИК",
        "ПОШЕВЕЛИТЕ ЛЮБОЙ ДЖОЙСТИК",
        "ПОКРУТИТЕ ЛЮБУЮ РУКОЯТКУ",
    };

    // Needs to clear the reference-resolution half-width (960) plus half
    // the message box's own width (see SceneSetup.CreateLoaderScreen,
    // messageRt.sizeDelta.x = 1700 -> 850) for the text to actually finish
    // off-screen rather than just past center — 2200 leaves a safe margin.
    [SerializeField] private float slideDistance = 2200f;
    [SerializeField] private float slideInDuration = 0.5f;
    [SerializeField] private float holdDuration = 1.6f;
    [SerializeField] private float slideOutDuration = 0.5f;

    // Index-matched to Messages above — message i highlights a random
    // element from pool i. Some categories have more than one real element
    // on the panel (several buttons/knobs), some just one (joystick/sensor).
    [SerializeField] private GameObject[] buttonHighlights;
    [SerializeField] private GameObject[] sensorHighlights;
    [SerializeField] private GameObject[] joystickHighlights;
    [SerializeField] private GameObject[] knobHighlights;

    private Vector2 _messageRestPos;
    private GameObject _activeHighlight;
    private bool _holding;

    private void Awake()
    {
        if (messageText != null)
            _messageRestPos = messageText.rectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        StartCoroutine(CycleMessages());
    }

    private void Update()
    {
        bool anyKey = Input.anyKey; // stand-in for a real cabinet controller — see class comment

        if (!_holding && anyKey)
        {
            _holding = true;
            if (canvasRoot != null)
                canvasRoot.SetActive(false);
            if (introSequence != null)
                introSequence.BeginConfirmHold();
        }
        else if (_holding && !anyKey)
        {
            _holding = false;
            // Only abort if the sequence hasn't already committed to
            // finishing (IsRunning goes false right as "СТАРТ" completes) —
            // a release after that point is just the player letting go of a
            // button they don't need anymore, not a cancel.
            if (introSequence != null && introSequence.IsRunning)
            {
                introSequence.AbortIfIncomplete();
                if (canvasRoot != null)
                    canvasRoot.SetActive(true);
            }
        }
    }

    private IEnumerator CycleMessages()
    {
        int i = 0;
        while (true)
        {
            ShowHighlightForCategory(i);
            yield return StartCoroutine(SlideMessage(Messages[i]));
            HideActiveHighlight();
            i = (i + 1) % Messages.Length;
        }
    }

    private IEnumerator SlideMessage(string text)
    {
        if (messageText == null)
            yield break;

        messageText.text = text;
        RectTransform rt = messageText.rectTransform;

        yield return StartCoroutine(SlideX(rt, slideDistance, 0f, slideInDuration));
        yield return new WaitForSeconds(holdDuration);
        yield return StartCoroutine(SlideX(rt, 0f, -slideDistance, slideOutDuration));
    }

    private IEnumerator SlideX(RectTransform rt, float fromX, float toX, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = _messageRestPos + new Vector2(Mathf.Lerp(fromX, toX, p), 0f);
            yield return null;
        }
        rt.anchoredPosition = _messageRestPos + new Vector2(toX, 0f);
    }

    private void ShowHighlightForCategory(int messageIndex)
    {
        GameObject[] pool = null;
        if (messageIndex == 0) pool = buttonHighlights;
        else if (messageIndex == 1) pool = sensorHighlights;
        else if (messageIndex == 2) pool = joystickHighlights;
        else if (messageIndex == 3) pool = knobHighlights;

        if (pool == null || pool.Length == 0)
            return;

        _activeHighlight = pool[Random.Range(0, pool.Length)];
        if (_activeHighlight != null)
            _activeHighlight.SetActive(true);
    }

    private void HideActiveHighlight()
    {
        if (_activeHighlight != null)
            _activeHighlight.SetActive(false);
        _activeHighlight = null;
    }
}
