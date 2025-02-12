[← back to readme](README.md)

# Release notes
## Upcoming release
* For players:
  * Added sound effect and rare interaction for central station exit door, and removed the stanchion line blocking it (but it's still locked).
  * Raised juice prices in food court shop to avoid infinite money exploit with artisan profession.
  * Fixed map layer issue with a gift shop basket.
  * Fixed ticket machine not added if you start the day in its location.
  * Improved translations. Thanks to Hayato2236 (added Spanish) and NARCOAZAZAL (updated Portuguese)!
* For mod authors:
  * Added warning if a bookshelf entry has no messages to simplify troubleshooting.
  * Fixed custom content like tourists loaded before Content Patcher updates its tokens.

## 1.0.1
Released 08 February 2025 for SMAPI 4.1.10 or later.

* Added warning when a stop is hidden because its target location doesn't exist.
* Fixed Bus Locations mod overriding Central Station's ticket machine at the bus stop.
* Improved translations. Thanks to CapMita (added Chinese), creeperkatze (added German), Lexith (added Turkish), MakinDay (added Italian), MaxBladix (added French), and NARCOAZAZAL (added Portuguese)!

## 1.0.0
Released 07 February 2025 for SMAPI 4.1.10 or later.

- Initial release. This includes:
  - boat, bus, and train networks.
  - Central station map and custom ticket machine sprite commissioned from [Kisaa](https://next.nexusmods.com/profile/crystalinerose) (thanks!).
  - food court, gift shop, tourists, interactive bookshelves, and rare interactions in the central station.
  - integrations with the Bus Locations, CJB Cheats Menu, and Train Station mods.
  - data assets to register stops, tourists, and bookshelf messages through Content Patcher.
  - C# mod API to register stops.
