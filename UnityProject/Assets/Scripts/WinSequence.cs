using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinSequence : MonoBehaviour
{
    public static WinSequence Instance { get; private set; }

    // TEMPORARY, for faster debug/test runs — revert to 100f for real play.
    [SerializeField] private float winDistanceKm = 10f;

    // Exposed so the start-screen "ЦЕЛЬ" instructions can show the real
    // distance instead of a hardcoded number that'd lie while this is
    // temporarily lowered for testing.
    public float WinDistanceKm => winDistanceKm;

    [SerializeField] private RectTransform winTextRoot;
    [SerializeField] private Text recordText;
    [SerializeField] private float recordRevealDuration = 2.5f;
    [SerializeField] private Text achievementsText;
    [SerializeField] private float achievementsPageDuration = 5f;
    [SerializeField] private int achievementsLoopCount = 2;
    // Real top-3 tables (TopResultsPage, photo slots included), one per
    // leaderboard category — same component the start-screen carousel uses,
    // shown as the finale of the recap right after the run's photo is
    // captured instead of a plain numeric rank line.
    [SerializeField] private GameObject[] leaderboardPages;

    [SerializeField] private float entityFadeDuration = 1.5f;
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
        if (recordText != null)
            recordText.gameObject.SetActive(false);
        if (achievementsText != null)
            achievementsText.gameObject.SetActive(false);
    }

    public void TryTrigger(float distanceKm)
    {
        if (_triggered || distanceKm < winDistanceKm)
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
        // BigArchSpawner runs on its own timer independent of EntitySpawner
        // and only checks SpeedController.IsRunning (which stays true
        // through the whole win sequence) — without disabling it too, it
        // could drop a fresh arch on the road well after the one-time
        // fade-out below already ran, leaving it stranded there for good.
        foreach (var spawner in FindObjectsOfType<BigArchSpawner>())
            spawner.enabled = false;
        // Clouds and the sun drift on their own timers, independent of
        // SpeedController (so they keep moving even on the start screen) —
        // nothing above stops them, so without this they'd keep sailing
        // across the sky through the whole win sequence.
        foreach (var spawner in FindObjectsOfType<CloudSpawner>())
            spawner.enabled = false;
        foreach (var cloud in FindObjectsOfType<CloudDrift>())
            cloud.enabled = false;
        foreach (var sun in FindObjectsOfType<SunArc>())
            sun.enabled = false;

        if (GameTimer.Instance != null)
            GameTimer.Instance.Pause(); // the run is over — the clock shouldn't keep ticking through the cutscene

        // Every player currently in the scene — works for 1 or 2 players
        // without the sequence needing to know which/how many up front.
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var pc in players)
        {
            pc.ForceAirborneVisual(); // wings out for the flight, not whatever ground/air pose they were mid-stride in
            pc.enabled = false; // stops input AND OnTriggerEnter — no more collisions
        }

        // Per-player "КЛАВИШИ/ЖЕСТЫ" key-instruction HUD — named after the
        // player GameObject by CreateGesturePanel (SceneSetup.cs). Hidden by
        // fixed name for BOTH players, not just the ones with an active
        // PlayerController above: in 1-player mode the unused side's whole
        // player GameObject is deactivated (StartScreenController), so it
        // never shows up in FindObjectsOfType, but its gesture canvas is a
        // separate, always-active root object that keeps rendering (dim/
        // idle) regardless — left stranded on screen through the fly-away
        // if it isn't hidden here explicitly.
        foreach (string playerName in new[] { "PlayerLeft", "PlayerRight" })
        {
            GameObject gestureCanvas = GameObject.Find(playerName + "GestureCanvas");
            if (gestureCanvas != null)
                gestureCanvas.SetActive(false);
        }

        if (SpeedController.Instance != null)
            SpeedController.Instance.BeginWinBoost();

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

        // Boost was only ever meant to last while they're still visible
        // shrinking into the distance — once they're gone, ease the road
        // back down to a stop instead of racing on forever.
        if (SpeedController.Instance != null)
            SpeedController.Instance.EndWinBoost();

        if (winTextRoot != null)
            winTextRoot.gameObject.SetActive(true);

        if (HighScoreManager.Instance != null)
        {
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
            int tricks = TricksManager.Instance != null ? TricksManager.Instance.Count : 0;
            float maxSpeed = SpeedController.Instance != null ? SpeedController.Instance.MaxSpeedReached : 0f;

            // Rank-per-category is only needed by the leaderboard tables
            // themselves, which pull fresh top-3 data straight from
            // HighScoreManager when shown, not from this snapshot.
            List<HighScoreManager.NewRecord> newRecords =
                HighScoreManager.Instance.ReportRun(winTimestamp, score, tricks, maxSpeed, out _);

            if (newRecords.Count > 0 && recordText != null)
                yield return StartCoroutine(RevealRecords(newRecords));
        }

        if (achievementsText != null)
            StartCoroutine(CycleAchievements(winTimestamp));
    }

    // Post-win summary, split across a few pages (cycled, same "read at
    // your own pace" idea as the in-game top-3 panel) since it doesn't fit
    // on screen all at once: run totals, what was collected, what was hit,
    // tricks — THEN the real per-category top-3 tables (leaderboardPages)
    // last, stats about the run itself before rankings, not interleaved.
    private IEnumerator CycleAchievements(float winTimestamp)
    {
        AchievementStats stats = AchievementStats.Instance;
        float maxSpeed = SpeedController.Instance != null ? SpeedController.Instance.MaxSpeedReached : 0f;
        int minutes = Mathf.FloorToInt(winTimestamp / 60f);
        int secs = Mathf.FloorToInt(winTimestamp % 60f);

        var pageList = new List<string>
        {
            "ИТОГИ ЗАБЕГА\n"
                + "Дистанция: " + winDistanceKm.ToString("0") + " км\n"
                + "Макс. скорость: " + maxSpeed.ToString("0.0") + " км/ч\n"
                + "Время: " + string.Format("{0:00}:{1:00}", minutes, secs),
        };

        // Zero-value rows are just noise (nobody collected/hit that thing),
        // so each line only shows up if it actually happened — and if
        // nothing at all happened in a section, that whole page is skipped
        // rather than shown empty.
        if (stats != null)
        {
            var collected = new List<string>();
            if (stats.CherriesCollected > 0) collected.Add("Вишенок: +" + stats.CherriesCollected);
            if (stats.FlowersCollected > 0) collected.Add("Цветов: +" + stats.FlowersCollected);
            if (stats.HeartsCollected > 0) collected.Add("Сердец: +" + stats.HeartsCollected);
            int otherCollected = stats.TotalCollected - stats.CherriesCollected - stats.FlowersCollected - stats.HeartsCollected;
            if (otherCollected > 0) collected.Add("Прочее: +" + otherCollected);
            if (stats.TotalCollected > 0)
            {
                collected.Add("Всего собрано: +" + stats.TotalCollected);
                pageList.Add("СОБРАНО\n" + string.Join("\n", collected));
            }

            var hit = new List<string>();
            if (stats.BicyclesHit > 0) hit.Add("Великосипедов: -" + stats.BicyclesHit);
            if (stats.CatsHit > 0) hit.Add("Кошек: -" + stats.CatsHit);
            if (stats.DogsHit > 0) hit.Add("Собак: -" + stats.DogsHit);
            int otherHit = stats.TotalHit - stats.BicyclesHit - stats.CatsHit - stats.DogsHit;
            if (otherHit > 0) hit.Add("Прочее: -" + otherHit);
            if (stats.TotalHit > 0)
            {
                hit.Add("Всего сбито: -" + stats.TotalHit);
                pageList.Add("СБИТО\n" + string.Join("\n", hit));
            }

            var tricks = new List<string>();
            if (stats.RingTricks > 0) tricks.Add("Кольцо: +" + stats.RingTricks);
            if (stats.ArchTricks > 0) tricks.Add("Арка: +" + stats.ArchTricks);
            if (tricks.Count > 0)
                pageList.Add("ТРЮКИ\n" + string.Join("\n", tricks));
        }

        string[] pages = pageList.ToArray();

        achievementsText.gameObject.SetActive(true);
        // Loops through every page a couple of times, but any button/gesture
        // press jumps straight to the reset — an arcade cabinet needs to
        // return to attract-mode (the start screen, with its own
        // top-results pages) for the next player, not make them sit through
        // a fixed-length recap they've already read.
        for (int loop = 0; loop < achievementsLoopCount; loop++)
        {
            for (int page = 0; page < pages.Length; page++)
            {
                achievementsText.text = pages[page];

                float t = 0f;
                while (t < achievementsPageDuration)
                {
                    if (AnyInputPressed())
                    {
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                        yield break;
                    }
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            if (leaderboardPages != null)
            {
                achievementsText.gameObject.SetActive(false);
                foreach (var leaderboardPage in leaderboardPages)
                {
                    if (leaderboardPage == null)
                        continue;

                    leaderboardPage.SetActive(true);

                    float t = 0f;
                    while (t < achievementsPageDuration)
                    {
                        if (AnyInputPressed())
                        {
                            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                            yield break;
                        }
                        t += Time.deltaTime;
                        yield return null;
                    }

                    leaderboardPage.SetActive(false);
                }
                achievementsText.gameObject.SetActive(true);
            }
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static bool AnyInputPressed()
    {
        if (Input.anyKeyDown)
            return true;

        foreach (var gesture in FindObjectsOfType<GestureInput>())
        {
            if (gesture.JumpDown || gesture.DuckHeld || gesture.LeanLeftDown || gesture.LeanRightDown)
                return true;
        }
        return false;
    }

    // Shows each category the run just qualified for, one at a time — a
    // run can land in several of the 4 leaderboards at once (e.g. both
    // fastest time and highest score), so they're revealed in sequence
    // instead of all at once. Each reveal is followed by a webcam capture
    // (smile + 5s countdown) attached to every qualifying leaderboard slot,
    // if a camera is available — see PlayerPhotoCapture. One photo for the
    // whole run, not one per category: sitting through a full ~10s capture
    // (5s silent + 5s countdown) once per qualifying category — up to 4 in
    // a row for a run that sweeps every leaderboard — read as the capture
    // being stuck repeating instead of finishing.
    private IEnumerator RevealRecords(List<HighScoreManager.NewRecord> records)
    {
        recordText.gameObject.SetActive(true);
        foreach (var record in records)
        {
            recordText.text = "НОВЫЙ РЕКОРД!\n" + record.CategoryName + " — место " + record.Rank + " из 10";
            yield return new WaitForSeconds(recordRevealDuration);
        }
        recordText.gameObject.SetActive(false);

        if (PlayerPhotoCapture.Instance != null && records.Count > 0)
        {
            HighScoreManager.NewRecord best = records[0];
            foreach (var r in records)
                if (r.Rank < best.Rank)
                    best = r; // headline the strongest placement (rank 1 if any)

            string message = best.Rank == 1
                ? "НОВЫЙ РЕКОРД!\n" + best.CategoryName
                : best.CategoryName + " — " + best.Rank + " МЕСТО!";

            string savedPath = null;
            yield return StartCoroutine(PlayerPhotoCapture.Instance.CaptureForRecord(message, p => savedPath = p));
            if (savedPath != null && HighScoreManager.Instance != null)
            {
                // Same photo represents the whole run — attach it to every
                // category it qualified for, not just the headline one.
                foreach (var r in records)
                    HighScoreManager.Instance.SetPhotoPath(r.CategoryIndex, r.Rank, savedPath);
            }
        }
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
