using UnityEngine;
using UnityEngine.UI;

// Bottom action readout for a joystick-controlled player — mirrors
// GestureActionIndicator but reads JoystickInput instead.
public class JoystickActionIndicator : MonoBehaviour
{
    [SerializeField] private JoystickInput joystickInput;
    [SerializeField] private Text actionText;

    private void Update()
    {
        if (actionText == null || joystickInput == null || !joystickInput.enabled)
            return;

        if (joystickInput.UpHeld)
            actionText.text = "ПРЫЖОК";
        else if (joystickInput.DownHeld)
            actionText.text = "ПРИСЕСТЬ";
        else if (joystickInput.LeftHeld)
            actionText.text = "ВЛЕВО";
        else if (joystickInput.RightHeld)
            actionText.text = "ВПРАВО";
        else
            actionText.text = "–";
    }
}
