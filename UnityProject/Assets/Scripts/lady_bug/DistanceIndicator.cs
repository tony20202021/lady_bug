using UnityEngine;
using UnityEngine.UI;

// "X из Y" readout (just the numbers, no "traveled/remaining" wording) —
// distance travelled is also the win condition now (WinSequence/
// SpeedController), so this doubles as a progress bar toward the goal.
// Reads the goal distance straight from WinSequence (same pattern
// GoalDistanceLabel uses) instead of its own separate copy — a duplicated
// value drifted out of sync with WinSequence's own temporarily-lowered
// debug distance before.
public class DistanceIndicator : MonoBehaviour
{
    [SerializeField] private Text distanceText;

    private void Update()
    {
        if (distanceText == null || SpeedController.Instance == null || WinSequence.Instance == null)
            return;

        float traveled = SpeedController.Instance.DistanceKm;
        float targetKm = WinSequence.Instance.WinDistanceKm;
        // "из" rendered smaller than the two numbers around it (rich text —
        // Text.supportRichText is on by default) via an inline <size> tag,
        // per feedback — it's just a connector word, not a value.
        distanceText.text = string.Format("{0:0.0} км\n<size=26>из</size>\n{1:0} км", traveled, targetKm);
    }
}
