using System.IO;
using UnityEngine;
using UnityEngine.UI;

// One start-screen carousel page: the top 3 for one leaderboard category,
// each row showing its own photo (see PlayerPhotoCapture/HighScoreManager)
// if one was ever attached to that specific slot — not just a single photo
// for #1 shown off to the side. Refreshed every time the page becomes
// visible rather than baked at scene-build time, since the underlying
// leaderboard is only known at runtime (PlayerPrefs) and can change
// between carousel cycles.
public class TopResultsPage : MonoBehaviour
{
    private const int RowCount = 3;

    [SerializeField] private int category;
    [SerializeField] private Text titleText;
    [SerializeField] private Text[] rowRankTexts;
    [SerializeField] private Text[] rowValueTexts;
    [SerializeField] private RawImage[] rowPhotos;

    private readonly Texture2D[] _loadedPhotos = new Texture2D[RowCount];

    private void OnEnable()
    {
        Refresh();
    }

    // True if this category has at least one real entry — lets the carousel
    // (StartScreenController) skip a table that would otherwise show 3 rows
    // of "--" (nobody's played this category yet).
    public bool HasAnyEntry()
    {
        if (HighScoreManager.Instance == null)
            return false;

        HighScoreManager.Entry[] top = HighScoreManager.Instance.GetTopEntries(category, RowCount);
        for (int i = 0; i < top.Length; i++)
            if (top[i].Value > 0f)
                return true;
        return false;
    }

    private void OnDisable()
    {
        for (int i = 0; i < RowCount; i++)
        {
            if (_loadedPhotos[i] != null)
            {
                Destroy(_loadedPhotos[i]);
                _loadedPhotos[i] = null;
            }
        }
    }

    private void Refresh()
    {
        if (HighScoreManager.Instance == null)
            return;

        if (titleText != null)
            titleText.text = "ТОП-3: " + HighScoreManager.Instance.GetCategoryName(category);

        HighScoreManager.Entry[] top = HighScoreManager.Instance.GetTopEntries(category, RowCount);

        for (int i = 0; i < RowCount; i++)
        {
            bool hasEntry = i < top.Length && top[i].Value > 0f;
            string valueText = hasEntry ? HighScoreManager.Instance.FormatEntryValue(category, top[i].Value) : "--";

            if (rowRankTexts != null && i < rowRankTexts.Length && rowRankTexts[i] != null)
                rowRankTexts[i].text = (i + 1) + ".";
            if (rowValueTexts != null && i < rowValueTexts.Length && rowValueTexts[i] != null)
                rowValueTexts[i].text = valueText;

            if (_loadedPhotos[i] != null)
            {
                Destroy(_loadedPhotos[i]);
                _loadedPhotos[i] = null;
            }

            if (rowPhotos == null || i >= rowPhotos.Length || rowPhotos[i] == null)
                continue;

            string photoPath = hasEntry ? top[i].PhotoPath : null;
            if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
            {
                byte[] bytes = File.ReadAllBytes(photoPath);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                _loadedPhotos[i] = tex;
                rowPhotos[i].texture = tex;
                rowPhotos[i].gameObject.SetActive(true);
            }
            else
            {
                rowPhotos[i].texture = null;
                rowPhotos[i].gameObject.SetActive(false);
            }
        }
    }
}
