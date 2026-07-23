using UnityEngine;
using UnityEngine.UI;

public class SpeedIndicator : MonoBehaviour
{
    public static SpeedIndicator Instance { get; private set; }

    [SerializeField] private Text speedText;
    [SerializeField] private RectTransform popupParent;
    [SerializeField] private RectTransform counterAnchor;
    [SerializeField] private GameObject panelRoot;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (speedText == null || SpeedController.Instance == null)
            return;

        SpeedController sc = SpeedController.Instance;

        // The win-boost climbs speed without limit purely for the "flying
        // off" visual — a number racing into the thousands isn't useful
        // information, so hide the whole panel for that stretch instead.
        if (panelRoot != null)
            panelRoot.SetActive(!sc.IsWinBoosting);
        if (sc.IsWinBoosting)
            return;

        speedText.text = string.Format("{0:0.0} км/ч\nпередача {1}", sc.CurrentSpeed, sc.Gear);
    }

    // Spawns a "ПЕРЕКЛЮЧЕНИЕ НА N ПЕРЕДАЧУ" popup that flies from screen
    // centre to this panel — mirrors ScoreManager/TricksManager's popups,
    // but purely cosmetic (the gear itself already changed the instant it
    // did, nothing left to apply on arrival).
    public void SpawnGearPopup(int gear)
    {
        if (popupParent == null || counterAnchor == null)
            return;

        var go = new GameObject("GearPopup");
        go.transform.SetParent(popupParent, false);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 48;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "ПЕРЕКЛЮЧЕНИЕ\nНА " + gear + " ПЕРЕДАЧУ";
        text.color = new Color(0.6f, 0.9f, 1f);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520f, 130f);
        rt.anchoredPosition = new Vector2(960f, 540f); // screen centre in the (0,0)-anchored 1920x1080 frame

        GearPopup popup = go.AddComponent<GearPopup>();
        popup.target = counterAnchor;
    }
}
