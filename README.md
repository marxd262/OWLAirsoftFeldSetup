# Ziel

Wir brauchen ein System um auf dem Airsoftfeld die Flaggenpunkte, Klicker, Signalanlage miteinander zu verbinden und an einem zentralen Punkt zusammenzuführen. Das System soll dabei verschiedene Spielmodi unterstützen und dynamisch die eingaben verschiedener Teilsysteme kombinieren. Basierend auf den Spielmodi sollen Punkte getrackt werden.

## Unterstützte Spielmodi
### - Timer
Ein Timer der einfach nur abläuft

### - Team Deathmatch
Die Klicker in den Spawns der Teams zählen die Abschüsse (nach jedem Tod im Spawn betätigen). Das Team das die meisten Abschüsse erreicht hat (am wenigsten Tode hatte) gewinnt. Spielende kann entweder nach Abschüssen oder nach Zeit erfolgen.

### - Conquest
Auf dem Spielfeld verteilt befinden sich verschiedene Flaggenpunkte. Die Teams können die Flaggenpunkte durch Drücken eines Knopfes an der Flagge einnehmen (gedrückt halten). Eingenommene Flaggenpunkte generieren in regelmäßigen Abständen Punkte.
Das Team mit den meisten Punkten gewinnt.
SPielende kann entweder nach Punkten oder nach Zeit eintreten.

### - Capture the Flag
Flaggen müssen von bestimmten Punkten in den Spawn des Jeweiligen Teams gebracht werden.
Das Team mit den Meisten Flaggen gewinnt.
Spielende ist nach Zeit oder wenn alle Flaggen gerettet wurden.

### - Missionday
Auf dem Spielfeld verteilt befinden sich verschiedene Aufgaben welche erreicht werden müssen. Die Reihenfolge der Aufgaben ist entscheidend. Die Aufgaben und die Reihenfolge können vor Spielbeginn von der Orga festgelegt werden. 
Sieger ist das Team das alle Missionen geschafft hat.

### - Bombe
Ein Team soll an einem Punkt eine Bombe plazieren und aktivieren. Nach aktivierung läuft ein Timer runter in dem die Verteidiger die Bombe entschärfen können. Vergleichbar mit CS.



# Geplante Soft-/ Hardware

## Netzwerk
Für das Netzwerk wird ein WLAN fähiger Router verwendet welcher lediglich ein WLAN-Netz zur Kommunikation stellt.

## Server
Server auf dem die Website läuft befindet sich auf einem Raspberry Pi. Die Website wird in Form einer Blazor Anwendung implementiert und stellt gleichzeitig eine Api über welche verschiedene Externe Systeme ihren Status teilen.

## Türme
Als Microcontroller verwenden die Türme einen ESP32 NodeMCU für die Wlan connectivität.
Angeschlossen werden zwei Knöpfe, eins pro Team, um die Türme einzunehmen.
Über LED Streifen wird nach außen hin sichtbar gemacht welches Team den Turm hällt. Für die LedStreifen wird ein Pixeladdressierbarer Streifen mit dem Chip WS2812B WS2811 Verwendet.
Betrieben wird das ganze mit einem 11.1V Airsoftakku.
Über einen Stepdown Spannungswandler vom Typ MP2307DN wird die Spannung von 11.1V auf 3.5V für den ESP32 gewandelt.

## Klicker
Für die Klicker in den Spawns wird ein ESP32 NodeMCU als Microcontroller verwendet. Angeschlossen ist ein Knopf der Betätigt werden kann um einen Tod zu tracken.
Betrieben wird das ganze mit einem 11.1V Airsoftakku.
Über einen Stepdown Spannungswandler vom Typ MP2307DN wird die Spannung von 11.1V auf 3.5V für den ESP32 gewandelt.

## Signalanlage
tba

## Bombe
tba

## Adminpannel
Für die Orga wird ein Tablet genutzt um die Website des Servers aufzurufen.

## Anzeige in den Spawns
Die Teams bekommen ein Tablet in den Spawn um den Aktuellen Status des Spiels zu sehen.

# Server
Der Server dient als Zentrale Sammelstelle für alle Informationen. Die Informationen der einzenlen Sensoren (Türme, Klicker usw.) werden hier ausgewertet und basierend darauf Punkte vergeben und der Status der aktuellen Runde angepasst.

## Website
Über die Website können alle Beteiligten den Aktuellen Status sehen und eventuell eingaben Tätigen
### Orga Seite
Über die Orga Seite kann:
- Spielmodus gewechselt werden
- Spielmodus Konfiguriert werden
- Match gestartet werden (eventuell mit Timer)
- Reset durchführen

### Teams Seite
Die Teamseite zeigt den Aktuellen Status des Matches an. Dazu gehört:
- Punkte der Teamseiten
- Erreichte Ziele
- Status Flaggenpunkte
- Timer bis Match Anfang/Ende

## API
Die API stellt verschiedene Endpunkte bereit über welche die Sensoren Stati mitteilen können.

### Endpunkte

#### Ping(ID)
#### FlagIsPressed(ID, TeamEnum)
#### FlagIsReleased(ID, TeamEnum)
#### ClickerPressed(ID)

## Services
### GameStateService
Hällt den aktuellen Stand des Matches und eine Historie des Tages.
Das beinhaltet:
Teams (Punkte, Name, Tode)
Flaggen (Gehalten von Team)
### GameRunnerService

## Model
### Team
### GameMode
### Enums