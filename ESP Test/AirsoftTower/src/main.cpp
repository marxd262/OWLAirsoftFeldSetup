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
#include <FastLED.h>
#include <WebServer.h>

#include <uri/UriBraces.h>
#include <uri/UriRegex.h>

#define NUM_LEDS 5
#define DATA_PIN 26

CRGB leds[NUM_LEDS];
int led = 2;

bool ledOn = false;

const char *ssid = "Marx";          // Change this to your WiFi SSID
const char *password = "WlanMarx111";  // Change this to your WiFi password

const char* host = "http://owlairsoft.local/api/";        // This should not be changed
const int httpPort = 80;                        // This should not be changed

WebServer server(80); // TCP server at port 80 will respond to HTTP requests

enum SendAction{
  BlueClicked,
  BlueReleased,
  RedClicked,
  RedReleased,
  Register
};

enum LedColor{
  LedRed,
  LedBlue,
  LedOff,
  LedYellow
};

#define buttonBlue 12
#define buttonRed 14

bool buttonBluePressed = false;
bool buttonRedPressed = false;

void SetLedColor(int r, int g, int b){
  
  leds[0].setRGB(r,g,b) ;
  leds[1].setRGB(r,g,b);
  leds[2].setRGB(r,g,b);
  leds[3].setRGB(r,g,b);
  leds[4].setRGB(r,g,b);
  FastLED.show();
}

String IpAddress2String(const IPAddress& ipAddress)
{
  return String(ipAddress[0]) + String(".") +\
  String(ipAddress[1]) + String(".") +\
  String(ipAddress[2]) + String(".") +\
  String(ipAddress[3])  ; 
}
void sendMessage(SendAction action){
  String currentHost = host;
  String id = WiFi.macAddress();

  switch(action)
  {
    case Register:
      currentHost +=  "RegisterTower?id=" + id + "&ip="+ IpAddress2String(WiFi.localIP());
      break;
    case BlueClicked:
      currentHost +=  "TowerButtonPressed?id="+id+"&color=1";
      break;
    case BlueReleased:
      currentHost +=  "TowerButtonReleased?id="+id+"&color=1";
      break;
    case RedClicked:
      currentHost +=  "TowerButtonPressed?id="+id+"&color=0";
      break;
    case RedReleased:
      currentHost +=  "TowerButtonReleased?id="+id+"&color=0";
      break;

  }
  Serial.println(currentHost);
  
    HTTPClient http;

    http.begin(currentHost);
    http.addHeader("Content-Type", "application/x-www-form-urlencoded");
    // WRITE --------------------------------------------------------------------------------------------
    // Connect to server and try to send the message
    int httpCode = http.POST("");
    Serial.println(httpCode);
    http.end();
}

void HandleColorChange(){
  Serial.println("Args: ");
  Serial.println("R:" + server.pathArg(0));
  Serial.println("G:" + server.pathArg(1));
  Serial.println("B:" + server.pathArg(2));

  int r = server.pathArg(0).toInt();
  int g = server.pathArg(1).toInt();
  int b = server.pathArg(2).toInt();

  SetLedColor(r,g,b);

  server.send(200);
}

void HandlePing(){
  server.send(200);
}

void setup() {
  Serial.begin(115200);
  pinMode(buttonBlue, INPUT_PULLUP);
  pinMode(buttonRed, INPUT_PULLUP);
  pinMode(led,OUTPUT);

  
  FastLED.addLeds<WS2811, DATA_PIN, BRG>(leds, NUM_LEDS);

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
  IPAddress x = WiFi.localIP();
  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(x);

  // Start TCP (HTTP) server
  server.begin();
  Serial.println("TCP server started");

  server.on(UriBraces("/api/setcolor/{}/{}/{}"), HandleColorChange);
  server.on("/api/ping", HandlePing);

  sendMessage(Register);
}

void loop() {
  if(digitalRead(buttonRed) == LOW && !buttonRedPressed)
  {
    Serial.println("Red klicked");
    buttonRedPressed = true;
    sendMessage(RedClicked);
  }
  else if(digitalRead(buttonRed) == HIGH && buttonRedPressed){
    Serial.println("Red released");
    buttonRedPressed = false;
    sendMessage(RedReleased);
    delay(300);
  }

  if(digitalRead(buttonBlue) == LOW && !buttonBluePressed)
  {
    Serial.println("blue klicked");
    buttonBluePressed = true;
    sendMessage(BlueClicked);
  }
  else if(digitalRead(buttonBlue) == HIGH && buttonBluePressed){
    Serial.println("Blue released");
    buttonBluePressed = false;
    sendMessage(BlueReleased);
    delay(300);
  }

  server.handleClient();
}


