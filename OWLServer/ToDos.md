# KNOWN ISSUES / TODO

## Tower
- Turm Status Speichern
- Konfigurierbar ob Kontrollierende Türme gecappt bleiben wenn Abhängiger Turm eingenommen


## Spielmodie
- Mehrere Abhängigkeiten kontrollieren einen turm in Conquest. Probleme bei Einnahmekontrolle und Zurücksetzen vom Turm
- Abhängigen Turm nicht zurücksetzen wenn Kontrollierenderturm von kontrollierenden Team eingenommen wird

## Allgemein
- Start-Sound zu Counter offset
- Sieger im Admin Dashboard visualisieren
- Resetblocker nach Spielstart für Admins
- Autoreset bei spielstart
- Spielhistorie

## Fehler
- DBSet<Tower> Teams in DB Context

## Erweiterungen
- Was in der DB gespeichert wird

## Weitere
- DatabaseContext.cs line 8: TODO comment for full database schema
- No error handling for tower network failures
- Towers Reset Timer allways used

-------------------------------------------------------------

# ToTest
- Chainlink Editor
- keine -Werte im score
- Tower zurückerobern nach 50% und abbruch neutral
- Ein bereits eingenommener Turm sollte nicht vom selben Team eingenommen werden können
- Zeit bis Relock Visualisieren.


# Done
- Spielende Triggert jetzt Sound und zeigt Endscreen
- Robustere Logik für Spielende
- Überarbeitung der DebugApiSeite
- Sound können jetzt Hochgeladen und Konfiguriert werden
- Die Map kann jetzt Konfiguriert werden
- Es gibt jetzt die Mölichkeit Sounds auch unter Windows abzuspielen
- Autostart Feedback an Spieler und Admins
- Towers Chain Break? 
- Abhängigkeiten zwischen Towern Visualisieren.
- Chainlink 12345 Blau, ROt Captured 1, Alles bis auf 5 zurückgesetzt, 4 locked.
- Chainlink 1-2-3 Team Rot, 4-5 Team Blau, Blau Captured 1, 3 Locked.
- Schatten hinter Türmen auf der Karte