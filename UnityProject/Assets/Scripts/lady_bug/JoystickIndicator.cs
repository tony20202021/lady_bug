using UnityEngine;
using UnityEngine.UI;

// Debug HUD for a joystick-controlled player — four directional arrows with a
// centre dot, lit while that direction is held (see the menu-page sketch).
public class JoystickIndicator : MonoBehaviour
{
    [SerializeField] private JoystickInput joystickInput;
    [SerializeField] private Text upArrow;
    [SerializeField] private Text downArrow;
    [SerializeField] private Text leftArrow;
    [SerializeField] private Text rightArrow;
    [SerializeField] private Text centerDot;

    private static readonly Color ActiveColor = new Color(1f, 0.84f, 0.2f);
    private static readonly Color IdleColor = new Color(0.33f, 0.33f, 0.33f);

    private void Update()
    {
        if (joystickInput == null || !joystickInput.enabled)
            return;

        SetArrow(upArrow, joystickInput.UpHeld);
        SetArrow(downArrow, joystickInput.DownHeld);
        SetArrow(leftArrow, joystickInput.LeftHeld);
        SetArrow(rightArrow, joystickInput.RightHeld);

        if (centerDot != null)
        {
            bool any = joystickInput.UpHeld || joystickInput.DownHeld
                || joystickInput.LeftHeld || joystickInput.RightHeld;
            centerDot.color = any ? ActiveColor : IdleColor;
        }
    }

    private static void SetArrow(Text arrow, bool active)
    {
        if (arrow != null)
            arrow.color = active ? ActiveColor : IdleColor;
    }
}
