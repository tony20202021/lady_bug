using UnityEngine;
using UnityEngine.UI;

public class SpeedIndicator : MonoBehaviour
{
    [SerializeField] private Text speedText;

    private void Update()
    {
        if (speedText == null || SpeedController.Instance == null)
            return;

        speedText.text = Mathf.RoundToInt(SpeedController.Instance.CurrentSpeed) + " км/ч";
    }
}
