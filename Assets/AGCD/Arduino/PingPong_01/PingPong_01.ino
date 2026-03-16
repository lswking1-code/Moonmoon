/* Ping Pong
  Responds with "PONG" when it receives a "PING" via Serial port.
  Also turns the LED on when that happens

  Pressing button on pin 2 causes to send PING via serial port once.
 */


const int buttonPin = 2;
int previousButtonState = HIGH; // HIGH: released


void setup() {
  pinMode(buttonPin, INPUT_PULLUP); // HIGH: released, LOW: pressed
  pinMode(LED_BUILTIN, OUTPUT);
  Serial.begin(9600);
}


void blinkLED (int ms = 100)
{
  digitalWrite(LED_BUILTIN, HIGH);
  delay(ms);
  digitalWrite(LED_BUILTIN, LOW);
}

void loop() {
  // SENDS PING WHEN BUTTON IS PRESSED --------------------------------------------------
  // Reads the state of the button
  int buttonState = digitalRead(buttonPin);
  // Button is now pressed (LOW), but used to be released (HIGH)
  if (buttonState == LOW  && previousButtonState == HIGH) {
    Serial.println("PING");
    
    blinkLED();
  }


  previousButtonState = buttonState;



  // RESPONDS TO PING --------------------------------------------------
  // Check if data is available
  if (Serial.available()) {
    String command = Serial.readStringUntil('\n'); // Read until newline
    command.trim();  // Remove leading/trailing whitespace

    // Process command
    if (command == "PING") {
      Serial.println("PONG");

      blinkLED();
    } else if (command == "PONG") {
      //Serial.println("PING");
      blinkLED();
    }
  }


  delay(50);  // Avoid flooding the serial port
}