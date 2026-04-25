# KNOWN ISSUES / TODO

## Fehler
- Game ends on exact minute equality (unlikely to fire: StartTime?.AddMinutes(...) == DateTime.Now)\
- DBSet<Tower> Teams in DB Context

## Erweiterungen
- Was in der DB gespeichert wird
- Abhängigkeiten zwischen Towern Visualisieren.

## Weitere
- AudioService.cs: Countdown/Freeze sounds return empty string (TODO)
- DatabaseContext.cs line 8: TODO comment for full database schema
- No error handling for tower network failures
- Sounds für Countdown und Freeze
- Sounds Configurierbar machen
- Spielende Richtig Triggern (stribt aktuell lautlos)