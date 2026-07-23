using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Captures a webcam snapshot when a run lands in a leaderboard's top 10 —
// shown full-screen with a "smile, the camera is taking your picture"
// countdown, then saved to disk so HighScoreManager can attach it to that
// leaderboard slot and the start screen can show it later.
//
// NOTE (untested on real hardware / a signed build — no camera available in
// this dev environment): on a macOS standalone build, the OS only grants
// camera access if the app's Info.plist has an NSCameraUsageDescription key
// — set via Player Settings > macOS > "Camera Usage Description" before
// building. Without it, WebCamTexture.Play() doesn't throw, it just never
// produces real frames (width stays at the 16x16 placeholder), which is why
// CaptureForRecord below times out that wait and falls back to "no photo"
// instead of hanging forever.
public class PlayerPhotoCapture : MonoBehaviour
{
    public static PlayerPhotoCapture Instance { get; private set; }

    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private Text messageText;
    [SerializeField] private Text smileText;
    [SerializeField] private RawImage cameraPreview;
    [SerializeField] private Text countdownText;
    [SerializeField] private float countdownSeconds = 5f;
    [SerializeField] private float deviceStartTimeout = 3f;

    private WebCamTexture _webcam;

    private void Awake()
    {
        Instance = this;
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    // message: shown big at the top, e.g. "НОВЫЙ РЕКОРД!\nВРЕМЯ" or
    // "ВРЕМЯ — 7 МЕСТО!". Calls onSaved with the PNG's full path once done,
    // or null if no camera was available/usable.
    public IEnumerator CaptureForRecord(string message, Action<string> onSaved)
    {
        if (WebCamTexture.devices.Length == 0)
        {
            onSaved?.Invoke(null);
            yield break;
        }

        if (overlayRoot != null)
            overlayRoot.SetActive(true);
        if (messageText != null)
            messageText.text = message;
        if (smileText != null)
            smileText.gameObject.SetActive(true);
        if (countdownText != null)
            countdownText.text = "";

        _webcam = new WebCamTexture();
        _webcam.Play();
        if (cameraPreview != null)
            cameraPreview.texture = _webcam;

        // Width starts at a 16x16 placeholder until the device actually
        // begins streaming — bail out to the no-photo path instead of
        // hanging forever if that never happens (denied permission, no
        // Info.plist entry on a built app, etc — see the class comment).
        float waitTimer = 0f;
        while (_webcam.width <= 16 && waitTimer < deviceStartTimeout)
        {
            waitTimer += Time.deltaTime;
            yield return null;
        }

        bool deviceReady = _webcam.width > 16;

        if (deviceReady)
        {
            for (int i = Mathf.CeilToInt(countdownSeconds); i > 0; i--)
            {
                if (countdownText != null)
                    countdownText.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }
            if (countdownText != null)
                countdownText.text = "0";
        }

        string path = null;
        if (deviceReady)
        {
            var snapshot = new Texture2D(_webcam.width, _webcam.height, TextureFormat.RGB24, false);
            snapshot.SetPixels(_webcam.GetPixels());
            snapshot.Apply();

            byte[] png = snapshot.EncodeToPNG();
            Destroy(snapshot);

            string dir = Path.Combine(Application.persistentDataPath, "PlayerPhotos");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "photo_" + DateTime.Now.Ticks + ".png");
            File.WriteAllBytes(path, png);
        }

        _webcam.Stop();
        _webcam = null;
        if (cameraPreview != null)
            cameraPreview.texture = null;
        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        onSaved?.Invoke(path);
    }
}
