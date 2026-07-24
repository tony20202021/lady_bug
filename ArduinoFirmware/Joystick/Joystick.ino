// ============================================================
//  LadyBugHitTheRoad — player-2 joystick board
// ============================================================
// One arcade-style joystick (4 digital microswitches: up/down/left/right,
// common ground) driving player 2 (the left ladybug) as an alternative to
// the hand-distance-sensor gesture scheme player 1 uses (see
// ArduinoFirmware/GestureSensors). Same directions the keyboard already
// maps: up = jump, down = duck (holding down on every active player at
// once for 5s+5s already opens the quit-confirm dialog — see
// DuckToExitController.cs, generic over any input source, nothing extra
// needed here for that), left/right = lane change.
//
// Wiring: each switch's other leg to GND, internal pull-up means "not
// pressed" reads HIGH, "pressed" reads LOW.
//   D2 -> UP     D3 -> DOWN     D4 -> LEFT     D5 -> RIGHT
//
// Output: one CSV line per poll, ~33 Hz:
//   J,<up>,<down>,<left>,<right>
// Each field is 0 or 1. Identity handshake matches the gesture-sensor
// board's convention: sending '?' gets a "BOARD,JOYSTICK" reply, used by
// UnityProject/Assets/Scripts/JoystickSerial.cs to find this board among
// other serial devices — including the gesture-sensor board, if both are
// plugged in at once for a sensors+joystick co-op session.

const uint8_t UP_PIN = 2;
const uint8_t DOWN_PIN = 3;
const uint8_t LEFT_PIN = 4;
const uint8_t RIGHT_PIN = 5;

const unsigned long POLL_INTERVAL_MS = 30; // ~33 Hz, matches the gesture-sensor board's cadence

unsigned long lastPoll = 0;

void setup()
{
  Serial.begin(115200);
  Serial.println("HELLO!");
  Serial.println("BOARD,JOYSTICK");

  pinMode(UP_PIN, INPUT_PULLUP);
  pinMode(DOWN_PIN, INPUT_PULLUP);
  pinMode(LEFT_PIN, INPUT_PULLUP);
  pinMode(RIGHT_PIN, INPUT_PULLUP);
}

void read_identity_request()
{
  while (Serial.available() > 0)
  {
    if (Serial.read() == '?') Serial.println("BOARD,JOYSTICK");
  }
}

// Pressed = LOW (internal pull-up, switch ties the pin to GND).
int read_switch(uint8_t pin)
{
  return digitalRead(pin) == LOW ? 1 : 0;
}

void loop()
{
  read_identity_request();

  if (millis() - lastPoll < POLL_INTERVAL_MS) return;
  lastPoll = millis();

  Serial.print("J,");
  Serial.print(read_switch(UP_PIN));
  Serial.print(",");
  Serial.print(read_switch(DOWN_PIN));
  Serial.print(",");
  Serial.print(read_switch(LEFT_PIN));
  Serial.print(",");
  Serial.println(read_switch(RIGHT_PIN));
}
