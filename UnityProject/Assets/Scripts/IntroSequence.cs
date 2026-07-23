using System.Collections;
using UnityEngine;

// Very first thing shown when the game launches: flowers rain down and pile
// up (bottom row first, see SceneSetup.CreateIntroScreen for the fill
// order) until the whole screen is covered, then this canvas hides itself —
// revealing the start menu, which has been sitting ready underneath the
// whole time.
public class IntroSequence : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    // One per grid cell, already in fill order (bottom row to top row,
    // shuffled within each row) — built at scene-setup time.
    [SerializeField] private RectTransform[] flowers;
    [SerializeField] private float totalDuration = 4.5f;
    [SerializeField] private float fallDistance = 400f;
    [SerializeField] private float fallDuration = 0.4f;

    private void Awake()
    {
        if (flowers == null)
            return;

        foreach (var flower in flowers)
            if (flower != null)
                flower.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(RunIntro());
    }

    private IEnumerator RunIntro()
    {
        if (flowers == null || flowers.Length == 0)
        {
            Finish();
            yield break;
        }

        float perFlowerDelay = totalDuration / flowers.Length;
        foreach (var flower in flowers)
        {
            if (flower != null)
                StartCoroutine(DropFlower(flower));
            yield return new WaitForSeconds(perFlowerDelay);
        }

        // Let the last handful still mid-fall actually land before revealing
        // the menu underneath, instead of cutting them off mid-air.
        yield return new WaitForSeconds(fallDuration);
        Finish();
    }

    private IEnumerator DropFlower(RectTransform flower)
    {
        Vector2 target = flower.anchoredPosition;
        Vector2 start = target + new Vector2(0f, fallDistance);
        flower.anchoredPosition = start;
        flower.gameObject.SetActive(true);

        float t = 0f;
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            flower.anchoredPosition = Vector2.Lerp(start, target, t / fallDuration);
            yield return null;
        }
        flower.anchoredPosition = target;
    }

    private void Finish()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }
}
