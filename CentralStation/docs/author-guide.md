← [README](README.md)

This page helps mod authors use Central Station with their mods. **See the [main README](README.md) for other
info.**

> ![TIP]  
> This page covers everything you can do with Central Station. However, you probably only need the 'Basic usage'
section below.

## Contents
* [Basic usage](#basic-usage)
  * [Overview](#overview)
  * [Add a stop](#add-a-stop)
  * [Add a ticket machine](#add-a-ticket-machine)
* [Advanced destinations](#advanced-destinations)
  * [Edit the railroad map](#edit-the-railroad-map)
  * [Conditional stops](#conditional-stops)
* [See also](#see-also)

## Basic usage
### Overview
This mod adds three transport networks (boat, bus, and train) which connect to the Central Station. Mods can add their
own stops to each network.

For example, let's say you add a custom island only reachable by boat. There are two ways to reach it:
* Get a ticket from [Willy's boat](https://stardewvalleywiki.com/Fish_Shop#Willy.27s_Boat) (or any ticket machine on
  the boat network) to go the island.
* _Or_ get a ticket to the Central Station from any network, then get a boat ticket from there to your island.

Your destinations are highly customizable with optional features like ticket pricing and conditions.

### Add a stop
To add a boat, bus, or train stop:

1. Create a [Content Patcher content pack](https://stardewvalleywiki.com/Modding:Content_Patcher) if you don't already
   have one.
2. In your `content.json`, add entries to the `Mods/Pathoschild.CentralStation/Stops` asset:
   ```js
   {
       "Action": "EditData",
       "Target": "Mods/Cherry.TrainStation/Destinations",
       "Entries": {
           "{{ModId}}_ClintShop": {
               "DisplayName": "Clint's Shop",
               "ToLocation": "Town",
               "Networks": [ "Train" ],
               "ToTile": { "X": 105, "Y": 80 }
           }
       }
   }
   ```
3. Edit the data accordingly (see the fields below). You can list any number of boat or train stops in the same
   `EditData` patch.

The available fields for a boat or train stop are:

field name          | usage
------------------- | -----
_key_               | The entry key (not a field) is a [unique string ID](https://stardewvalleywiki.com/Modding:Common_data_field_types#Unique_string_ID) for your destination. This must be prefixed with your unique mod ID like `{{ModId}}_`.
`DisplayName`       | The display name to show in the menu. This should usually be translated into the player's current language using Content Patcher's `i18n` token.
`ToLocation`        | The internal name of the location to which the player should be warped to. You can see internal location names in-game using [Debug Mode](https://www.nexusmods.com/stardewvalley/mods/679).
`ToTile`            | <p>_(Optional)_ The tile position to which the player should be warped to. You can see tile coordinates in-game using [Debug Mode](https://www.nexusmods.com/stardewvalley/mods/679).</p><p>If omitted, Central Station will place the player just south of the ticket machine (if present), else it'll use the [default arrival tile](https://stardewvalleywiki.com/Modding:Maps#Warps_.26_map_positions).</li></ul>
`ToFacingDirection` | _(Optional)_ The direction the player should face after warping. The possible values are `up`, `down`, `left`, and `right`. Default `down`.
`Network`           | _(Optional)_ How the player can reach the stop. This can be `Boat`, `Bus`, or `Train`. Defaults to `Train`.
`Cost`              | _(Optional)_ The gold price to purchase a ticket. Default free.
`Conditions`        | _(Optional)_ If set, the [game state query](https://stardewvalleywiki.com/Modding:Game_state_queries) which must be met for the destination to appear in the menu.
`DisplayNameInCombinedLists` | _(Optional)_ If set, overrides `DisplayName` when shown in a menu containing multiple transport networks. This is only needed if a destination name is reused for different transport networks (e.g. "Stardew Valley" for boat, bus, and train stops).

### Add a ticket machine
You can add an [`Action` map property](https://stardewvalleywiki.com/Modding:Maps#Action) wherever you want the player
to get tickets. This can be a ticket machine, counter, or anything else thematically appropriate for your location.

The format is:
```
Action CentralStation Network
```

The `Network` must be `Boat`, `Bus`, or `Train`. If omitted, it defaults to `Train`.

The network affects:
* which destinations are shown in the menu;
* and which sound effects play when transiting to a destination which is on multiple networks.

## Advanced destinations
### Edit the railroad map
Central Station automatically adds a train ticket machine to `Maps/Railroad` on tile (32, 40).

If you edit the layout of that map, you can optionally add the ticket machine yourself. Central Station won't re-add
the machine if the `Action: CentralStation Train` tile property is already present, and it'll automatically adjust its
train stop to match the position of the ticket machine.

### Conditional stops
The `Conditions` field in your stop data lets you decide when your destination should appear. The conditions are
checked each time the menu is opened, so this can be used for a wide variety of customizations.

For example, let's say you want to create your own hub station:
```
                                              ┌─────────────────┐
                                          ┌──>│ Your location A │
                                          │   └─────────────────┘
┌─────────────────┐   ┌────────────────┐  │   ┌─────────────────┐
│ Central Station ├──>│ Your rail stop ├──┼──>│ Your location B │
└─────────────────┘   └────────────────┘  │   └─────────────────┘
                                          │   ┌─────────────────┐
                                          └──>│ Your location C │
                                              └─────────────────┘
```

You can add a condition like this to the [stop data](#add-a-stop) for your location A–C, so they're only available from
your hub station:
```js
"Conditions": "LOCATION_NAME Here {{ModId}}_YourRailStop"
```

See [Modding:Game state queries](https://stardewvalleywiki.com/Modding:Game_state_queries) on the wiki for the built-in
conditions, and you can also use any game state queries added by other mods.

## See also
* [README](README.md) for other info
* [Ask for help](https://stardewvalleywiki.com/Modding:Help)
