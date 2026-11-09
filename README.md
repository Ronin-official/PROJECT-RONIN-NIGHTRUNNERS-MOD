# Ronin NR Mods

Small client-side BepInEx 6 (IL2CPP) plugins for **NIGHT-RUNNERS PRIVATE ALPHA 3.2.8**.

Built against BepInEx `6.0.0-be.791` (Unity 6000.6, interop metadata 108).
Tested with NRMP `0.1.4b2` and the REDUX patcher `1.0.1`.

| Plugin | Folder | What it does |
|---|---|---|
| Neon Flow | `plugins/neonflow` | 5 underglow gradient modes in the neon shop |
| Music | `plugins/music` | custom per-state music + manual switching (← / → with phone raised) |
| Engine Heat Skip | `plugins/engine-heat` | F4 pins water/oil temperature |
| Real Names | `plugins/real-names` | real car names in UI, USS sheet and NRMP's F7 menu |
| Auto AWD | `plugins/auto-awd` | player car → AWD, drift-part cars stay RWD |

Download the compiled DLLs from the [Releases](../../releases) page.
Install: drop the DLLs into `BepInEx/plugins/`.

## Building

Each folder has its own `.csproj`. The projects reference BepInEx and interop
assemblies through `BepCore` / `BepInterop` properties — edit them to point at your
own install, then:

```powershell
dotnet build plugins/auto-awd/Ronin.NRAutoAWD328.csproj -c Release
```

## Notes

- All plugins are client-side. Nothing is uploaded and no server mod is required.
- Neon gradients and drivetrain changes are not synced by NRMP; other players may not
  see them (both players need Neon Flow to see each other's gradients).
- No game files, models, textures or music are included in this repository.

## Known issues

Music plugin:

- Inside the 24/7 garage the custom track keeps playing (it should be silent) and it is
  louder than expected.
- In the PA the custom music overlaps with the game music instead of replacing it.

Both are known and currently not planned to be fixed. Everything else in the pack works
as described.

## License

MIT — see [LICENSE](LICENSE).

Made by Ronin-official (in-game: 玉玉症好了).
