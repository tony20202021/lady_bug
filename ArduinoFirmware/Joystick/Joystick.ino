// ============================================================
//  LadyBugHitTheRoad — player-2 joystick board
// ============================================================
// One 2-axis analog joystick module (KY-023 style: 2 potentiometers + a
// push-button on the stick itself, 5 pins GND/+5V/VRx/VRy/SW) driving
// player 2 (the right ladybug) as an alternative to the hand-distance-
// sensor gesture scheme player 1 uses (see ArduinoFirmware/GestureSensors).
// Same directions the keyboard already maps: up = jump, down = duck
// (holding down on every active player at once for 5s+5s already opens the
// quit-confirm dialog — see DuckToExitController.cs, generic over any
// input source, nothing extra needed here for that), left/right = lane
// change. SW (the stick's own click-button) isn't read/sent — nothing in
// the current move set needs a 5th input, wired for later use only.
//
// Wiring:
//   GND -> GND     +5V -> 5V     VRx -> A0     VRy -> A1     SW -> (unused)
//
// VRx/VRy are analog voltage dividers, not switches — idle center reads
// ~half of ANALOG_MAX (board-to-board tolerance means it's rarely exactly
// half), full deflection reads near 0 or ANALOG_MAX. Thresholded into the
// same 4 discrete up/down/left/right booleans the old digital-button
// version sent, with a wide deadzone around center so idle drift/noise
// never reads as a direction. If a direction comes out backwards on your
// actual board (axes aren't standardized between modules/orientations),
// swap that axis's LOW_THRESHOLD/HIGH_THRESHOLD comparison below rather
// than re-wiring anything.
//
// Output: one CSV line per poll, ~33 Hz:
//   J,<up>,<down>,<left>,<right>
// Each field is 0 or 1 — same protocol the old digital-button version
// used, so JoystickSerial.cs/JoystickInput.cs on the Unity side need no
// changes at all. Identity handshake matches the gesture-sensor board's
// convention: sending '?' gets a "BOARD,JOYSTICK" reply, used by
// UnityProject/Assets/Scripts/JoystickSerial.cs to find this board among
// other serial devices — including the gesture-sensor board, if both are
// plugged in at once for a sensors+joystick co-op session.

const uint8_t VRX_PIN = A0;
const uint8_t VRY_PIN = A1;

const int ANALOG_MAX = 1023;
// Deadzone around center (~511) — anything between these two counts as
// "not pushed" on that axis. Wide on purpose: cheap potentiometers drift
// a fair bit at idle, and a false direction reading is far more annoying
// than a slightly-late real one.
const int LOW_THRESHOLD = ANALOG_MAX * 3 / 10;  // ~307
const int HIGH_THRESHOLD = ANALOG_MAX * 7 / 10; // ~716

const unsigned long POLL_INTERVAL_MS = 30; // ~33 Hz, matches the gesture-sensor board's cadence

unsigned long lastPoll = 0;

void setup()
{
  Serial.begin(115200);
  Serial.println("HELLO!");
  Serial.println("BOARD,JOYSTICK");
}

void read_identity_request()
{
  while (Serial.available() > 0)
  {
    if (Serial.read() == '?') Serial.println("BOARD,JOYSTICK");
  }
}

void loop()
{
  read_identity_request();

  if (millis() - lastPoll < POLL_INTERVAL_MS) return;
  lastPoll = millis();

  int x = analogRead(VRX_PIN);
  int y = analogRead(VRY_PIN);

  int up = y >= HIGH_THRESHOLD ? 1 : 0;
  int down = y <= LOW_THRESHOLD ? 1 : 0;
  int left = x <= LOW_THRESHOLD ? 1 : 0;
  int right = x >= HIGH_THRESHOLD ? 1 : 0;

  Serial.print("J,");
  Serial.print(up);
  Serial.print(",");
  Serial.print(down);
  Serial.print(",");
  Serial.print(left);
  Serial.print(",");
  Serial.println(right);
}
