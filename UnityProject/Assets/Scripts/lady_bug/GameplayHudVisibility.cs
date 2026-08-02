using UnityEngine;

// Corner wedge HUD (distance/time/score/tricks), tricks panel, and per-player
// gesture readouts — only visible during an actual road run.
public static class GameplayHudVisibility
{
    public static readonly string[] WedgePanelNames =
    {
        "DistancePanel", "TimerPanel", "GearSpeedPanel", "ScorePanel", "RightHubPlaceholder",
    };

    public static bool IsHardwareConnected()
    {
        bool sensorsConnected = GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected;
        bool joystickConnected = JoystickSerial.Instance != null && JoystickSerial.Instance.IsConnected;
        return sensorsConnected || joystickConnected;
    }

    public static void SetGameplayHudVisible(bool visible)
    {
        SetWedgePanelsVisible(visible);
        SetTricksHudVisible(visible);
        SetGestureHudVisible(visible);
    }

    public static void SetWedgePanelsVisible(bool visible)
    {
        Canvas scoreCanvas = FindScoreCanvas();
        if (scoreCanvas == null)
            return;

        foreach (string panelName in WedgePanelNames)
        {
            Transform panel = scoreCanvas.transform.Find(panelName);
            if (panel != null)
                panel.gameObject.SetActive(visible);
        }
    }

    public static void SetTricksHudVisible(bool visible)
    {
        GameObject tricksCanvas = FindSceneRoot("TricksCanvas");
        if (tricksCanvas != null)
            tricksCanvas.SetActive(visible);
    }

    public static void SetGestureHudVisible(bool visible)
    {
        bool show = visible && IsHardwareConnected();
        foreach (string playerName in new[] { "PlayerLeft", "PlayerRight" })
        {
            GameObject gestureCanvas = FindSceneRoot(playerName + "GestureCanvas");
            if (gestureCanvas != null)
                gestureCanvas.SetActive(show);
        }
    }

    static Canvas FindScoreCanvas()
    {
        GameObject scoreCanvasGo = FindSceneRoot("ScoreCanvas");
        return scoreCanvasGo != null ? scoreCanvasGo.GetComponent<Canvas>() : null;
    }

    // GameObject.Find skips inactive roots. The menu hides whole canvases
    // (TricksCanvas, gesture HUDs) with SetActive(false), so BeginGame's
    // re-show must look through inactive scene objects too.
    static GameObject FindSceneRoot(string name)
    {
        GameObject active = GameObject.Find(name);
        if (active != null)
            return active;

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject candidate = all[i];
            if (candidate.name != name || candidate.hideFlags != HideFlags.None)
                continue;
            if (!candidate.scene.IsValid())
                continue;
            return candidate;
        }

        return null;
    }
}
