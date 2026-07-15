using UnityEngine;
using UnityEngine.UI;

// Leaderboard of fastest times to reach the win condition (lower is better).
// Each entry also remembers how many freestyle tricks that session scored.
public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    [SerializeField] private Text listText;

    private const string TimeKeyPrefix = "BestTime_";
    private const string TricksKeyPrefix = "BestTricks_";
    private const int Count = 3;

    // 0 means "no record yet" — always loses to any real time.
    private readonly float[] _times = new float[Count];
    private readonly int[] _tricks = new int[Count];

    private void Awake()
    {
        Instance = this;
        Load();
        UpdateDisplay();
    }

    public void ReportWinTime(float seconds, int tricks)
    {
        for (int i = 0; i < Count; i++)
        {
            bool slotEmpty = _times[i] <= 0f;
            if (!slotEmpty && seconds >= _times[i])
                continue;

            for (int j = Count - 1; j > i; j--)
            {
                _times[j] = _times[j - 1];
                _tricks[j] = _tricks[j - 1];
            }
            _times[i] = seconds;
            _tricks[i] = tricks;

            Save();
            UpdateDisplay();
            break;
        }
    }

    private void Load()
    {
        for (int i = 0; i < Count; i++)
        {
            _times[i] = PlayerPrefs.GetFloat(TimeKeyPrefix + i, 0f);
            _tricks[i] = PlayerPrefs.GetInt(TricksKeyPrefix + i, 0);
        }
    }

    private void Save()
    {
        for (int i = 0; i < Count; i++)
        {
            PlayerPrefs.SetFloat(TimeKeyPrefix + i, _times[i]);
            PlayerPrefs.SetInt(TricksKeyPrefix + i, _tricks[i]);
        }
        PlayerPrefs.Save();
    }

    private void UpdateDisplay()
    {
        if (listText == null)
            return;

        listText.text = "ТОП-3\nвремя · очки за трюки\n"
            + FormatLine(1, _times[0], _tricks[0]) + "\n"
            + FormatLine(2, _times[1], _tricks[1]) + "\n"
            + FormatLine(3, _times[2], _tricks[2]);
    }

    private static string FormatLine(int rank, float seconds, int tricks)
    {
        if (seconds <= 0f)
            return rank + ". --:--";

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0}. {1:00}:{2:00} · {3}", rank, minutes, secs, tricks);
    }
}
