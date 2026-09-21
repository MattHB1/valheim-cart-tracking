# CartTracking

See every Valheim cart on the world map from anywhere.

**Server + clients:** for multiplayer, install CartTracking on the dedicated server and on every player. Client-only will not sync pins.

## Install

1. Open [rBmodman](https://r2modman.com/) (or Thunderstore mod manager) • Valheim • your profile.
2. Online • search `DevDonley-CartTrackin``, or install from the package page once published.
3. For multiplayer: install the **same** mod on the dedicated server, then restart the server.
4. Launch through the mod manager.

Manual install: put `CartTracking.dll` in `BepInEx/plugins/` (requires [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)).

## Use

- Carts show as icons on the minimap and full map.
- No naming, no text labels - just the cart icon.

## Notes

- Server syncs cart positions so pins work even when the cart is unloaded.
- Mismatched client/server builds show an in-game update warning.
- Other mods: [DevDonkey on Thunderstore](https://thunderstore.io/c/valheim/p/DevDonkey/)

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Build

Requires a .NET SDK with the net4.8 targeting pack, plus publicized Valheim / BepInEx reference assemblies (see HintPaths in `CartTracking/CartTracking.csproj`).

```powershell
.\scripts\build-deploy.ps1
.\scripts\create-release.ps1
```

`build-deploy.ps1` defaults to the r2modman `testing` profile. Use `-Profile Default` for multiplayer testing after singleplayer signoff.
