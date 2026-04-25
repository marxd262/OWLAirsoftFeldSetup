# KNOWN ISSUES / TODO

## Errors=
- Game ends on exact minute equality (unlikely to fire: StartTime?.AddMinutes(...) == DateTime.Now)\
- DBSet<Tower> Teams in DB Context
- Spielende Richtig Triggern (stribt aktuell lautlos)

## Erweiterungen
Was in der DB gespeichert wird


## Weitere
- AudioService.cs: Countdown/Freeze sounds return empty string (TODO)
- DatabaseContext.cs line 8: TODO comment for full database schema
- No authentication/authorization on API endpoints
- Tower color communication uses plaintext RGB HTTP (security concern)
- No error handling for tower network failures
-  Sioybds für Counddown und Freeze