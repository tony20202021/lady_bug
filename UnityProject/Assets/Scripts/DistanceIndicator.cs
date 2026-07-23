using UnityEngine;
using UnityEngine.UI;

// "X из Y" readout (just the numbers, no "traveled/remaining" wording) —
// distance travelled is also the win condition now (WinSequence/
// SpeedController), so this doubles as a progress bar toward the goal.
public class DistanceIndicator : MonoBehaviour
{
    [SerializeField] private Text distanceText;
    [SerializeField] private float targetKm = 100f;

    private void Update()
    {
        if (distanceText == null || SpeedController.Instance == null)
            return;

        float traveled = SpeedController.Instance.DistanceKm;
        distanceText.text = string.Format("{0:0.0} из {1:0}", traveled, targetKm);
    }
}
