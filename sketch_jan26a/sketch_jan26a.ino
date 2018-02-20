
 bool toggle = true;
  
void setup() {
  // put your setup code here, to run once:
  pinMode(2, OUTPUT);
  Serial.begin(9600); // bits per second.

}

void loop() {

    
  while( Serial.available() == 0);

    // read
    char val = Serial.read();

    if(toggle) {
      digitalWrite(2, HIGH);
      toggle = false;
    }
    else {
      toggle = true;
      digitalWrite(2, LOW); 
    }
    
    // echo
    Serial.print(val);

  
  //digitalWrite(2, HIGH);   // turn the LED on (HIGH is the voltage level)
  //delay(1000);                       // wait for a second
  //digitalWrite(2, LOW);    // turn the LED off by making the voltage LOW
  //delay(1000);                       // wait for a second

}
