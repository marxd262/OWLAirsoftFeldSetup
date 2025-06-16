/*
    Go to thingspeak.com and create an account if you don't have one already.
    After logging in, click on the "New Channel" button to create a new channel for your data. This is where your data will be stored and displayed.
    Fill in the Name, Description, and other fields for your channel as desired, then click the "Save Channel" button.
    Take note of the "Write API Key" located in the "API keys" tab, this is the key you will use to send data to your channel.
    Replace the channelID from tab "Channel Settings" and privateKey with "Read API Keys" from "API Keys" tab.
    Replace the host variable with the thingspeak server hostname "api.thingspeak.com"
    Upload the sketch to your ESP32 board and make sure that the board is connected to the internet. The ESP32 should now send data to your Thingspeak channel at the intervals specified by the loop function.
    Go to the channel view page on thingspeak and check the "Field1" for the new incoming data.
    You can use the data visualization and analysis tools provided by Thingspeak to display and process your data in various ways.
    Please note, that Thingspeak accepts only integer values.

    You can later check the values at https://thingspeak.com/channels/2005329
    Please note that this public channel can be accessed by anyone and it is possible that more people will write their values.
 */

#include <WiFi.h>
#include <HTTPClient.h>

const char *ssid = "Marx";          // Change this to your WiFi SSID
const char *password = "WlanMarx111";  // Change this to your WiFi password

const char* host = "http://owlairsoft.local/api/KlickerClicked?color=1";        // This should not be changed
const int httpPort = 80;                        // This should not be changed


#define clickerPin 14
#define color 0

WiFiClient client;
HTTPClient http;

void setup() {
  Serial.begin(115200);
  pinMode(clickerPin, INPUT_PULLUP);

  // We start by connecting to a WiFi network

  Serial.println();
  Serial.println("******************************************************");
  Serial.print("Connecting to ");
  Serial.println(ssid);

  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());

  

}

void loop() {
  

  if(digitalRead(clickerPin) == LOW)
  {
    Serial.println("klicked");
    while(digitalRead(clickerPin) == LOW);
    http.begin(host);
    http.addHeader("Content-Type", "application/x-www-form-urlencoded");
    // WRITE --------------------------------------------------------------------------------------------
    // Connect to server and try to send the message
    String message = "color=1";
    Serial.println(message);
    int httpCode = http.POST("");
    Serial.println(httpCode);
    http.end();
    delay (200);
  }

  
  
}
