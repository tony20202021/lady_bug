using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WinSequence : MonoBehaviour
{
    public static WinSequence Instance { get; private set; }

    [SerializeField] private int winScore = 100;

    [SerializeField] private RectTransform scorePanel;
    [SerializeField] private RectTransform winTextRoot;

    [SerializeField] private float entityFadeDuration = 1.5f;
    [SerializeField] private float scoreFlyDuration = 1.6f;
    [SerializeField] private float flyDuration = 3f;
    [SerializeField] private float flyHeightGain = 20f;
    [SerializeField] private float flyDistanceGain = 220f;

    private bool _triggered;

    public bool Triggered => _triggered;

    private void Awake()
    {
        Instance = this;
        if (winTextRoot != null)
            winTextRoot.gameObject.SetActive(false);
    }

    public void TryTrigger(int score)
    {
        if (_triggered || score < winScore)
            return;

        _triggered = true;
        float winTimestamp = GameTimer.Instance != null ? GameTimer.Instance.Elapsed : Time.time;
        StartCoroutine(RunSequence(winTimestamp));
    }

    private IEnumerator RunSequence(float winTimestamp)
    {
        foreach (var spawner in FindObjectsOfType<EntitySpawner>())
            spawner.enabled = false;
        foreach (var spawner in FindObjectsOfType<SideScenerySpawner>())
            spawner.enabled = false;

        // Every player currently in the scene — works for 1 or 2 players
        // without the sequence needing to know which/how many up front.
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var pc in players)
            pc.enabled = false; // stops input AND OnTriggerEnter — no more collisions

        if (SpeedController.Instance != null)
            SpeedController.Instance.BeginWinBoost();

        if (scorePanel != null)
            StartCoroutine(FlyScoreToCenter());

        MovingEntity[] entities = FindObjectsOfType<MovingEntity>();
        foreach (var entity in entities)
        {
            Collider col = entity.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
            entity.enabled = false; // hold position — don't let the speed boost drag them around mid-fade
        }
        yield return StartCoroutine(FadeOutEntities(entities));

        if (players.Length > 0)
            yield return StartCoroutine(FlyPlayersAway(players));

        if (winTextRoot != null)
            winTextRoot.gameObject.SetActive(true);

        if (HighScoreManager.Instance != null)
        {
            int tricks = TricksManager.Instance != null ? TricksManager.Instance.Count : 0;
            HighScoreManager.Instance.ReportWinTime(winTimestamp, tricks);
        }
    }

    private IEnumerator FlyScoreToCenter()
    {
        Vector2 startPos = scorePanel.anchoredPosition;
        Vector3 startScale = scorePanel.localScale;
        Vector2 targetPos = new Vector2(960f - scorePanel.sizeDelta.x / 2f, 540f - scorePanel.sizeDelta.y / 2f);
        Vector3 targetScale = startScale * 2.2f;

        float t = 0f;
        while (t < scoreFlyDuration)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / scoreFlyDuration), 3f); // ease-out
            scorePanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, p);
            scorePanel.localScale = Vector3.Lerp(startScale, targetScale, p);
            yield return null;
        }

        scorePanel.anchoredPosition = targetPos;
        scorePanel.localScale = targetScale;
    }

    private IEnumerator FadeOutEntities(MovingEntity[] entities)
    {
        var startScales = new Vector3[entities.Length];
        for (int i = 0; i < entities.Length; i++)
            if (entities[i] != null)
                startScales[i] = entities[i].transform.localScale;

        float t = 0f;
        while (t < entityFadeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / entityFadeDuration);
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] == null)
                    continue;
                entities[i].transform.localScale = Vector3.Lerp(startScales[i], Vector3.zero, p);
            }
            yield return null;
        }

        foreach (var entity in entities)
            if (entity != null)
                Destroy(entity.gameObject);
    }

    private IEnumerator FlyPlayersAway(PlayerController[] players)
    {
        var starts = new Vector3[players.Length];
        var startScales = new Vector3[players.Length];
        var targets = new Vector3[players.Length];
        var targetScales = new Vector3[players.Length];

        for (int i = 0; i < players.Length; i++)
        {
            Transform t = players[i].transform;
            starts[i] = t.position;
            startScales[i] = t.localScale;
            targets[i] = starts[i] + new Vector3(0f, flyHeightGain, flyDistanceGain);
            targetScales[i] = startScales[i] * 0.05f;
        }

        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / flyDuration);
            float eased = p * p; // ease-in — starts slow, rockets off toward the end

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null)
                    continue;
                Transform t = players[i].transform;
                t.position = Vector3.Lerp(starts[i], targets[i], eased);
                t.localScale = Vector3.Lerp(startScales[i], targetScales[i], eased);
            }
            yield return null;
        }
    }
}
