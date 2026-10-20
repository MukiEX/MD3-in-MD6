# MD3-in-MD6

A [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin that makes Magical Drop VI's versus
modes play by Magical Drop III's rules:

- **Chains and combos put real pressure on the opponent.** Attacks use MD3's attack levels and
  line tables, up to 8 lines per attack. Big single clears attack too.
- **The well refills itself.** When your field runs low, new lines drop right away, as in MD3,
  so you don't have to fill it by hand.

The values come from the Neo Geo Magical Drop III program (`magdrop3`). The routines and tables
are documented in [docs/md3-rom-notes.md](docs/md3-rom-notes.md).

## Install

1. Download `MD3-in-MD6-<version>-with-BepInEx-x64.zip` from
   [Releases](../../releases) and extract it into the Magical Drop VI folder, next to the game's
   `.exe`.
   If BepInEx 5 is already installed, use `MD3-in-MD6-<version>.zip` instead. It contains only
   `BepInEx/plugins/MD3-in-MD6`.
2. **Steam Deck / Linux (Proton):** set the game's launch options to
   `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
3. Start the game once. `BepInEx/LogOutput.log` should contain `MD3-in-MD6 1.0.0 loaded.` The
   config file `BepInEx/config/muki.md6.md3inmd6.cfg` is created on that first start.

## What changes

### Attacks

```
level = (orbs - 3) / 4            (rounded down; 0 if 3 orbs or fewer)
      + 4 * (chain - 1)           (when chain >= 2)
level = min(level, 31)
lines = table[level]
```

`orbs` is the total cleared over the whole chain. The table comes from the character's MDVI
attack group: Low = Weak, Middle = Medium, High = Strong.

| level  | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |10 |11 |12 |13 |14 |15 |16 |17 |18 |19 |20 |21 |22 |23 |24 |25 |26 |27 |28 |29 |30 |31 |
|--------|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Weak   | 0 | 1 | 1 | 2 | 2 | 3 | 3 | 3 | 3 | 4 | 4 | 4 | 4 | 4 | 4 | 5 | 5 | 5 | 5 | 5 | 6 | 6 | 6 | 6 | 6 | 7 | 7 | 7 | 7 | 7 | 8 | 8 |
| Medium | 0 | 2 | 2 | 3 | 3 | 3 | 4 | 4 | 4 | 4 | 5 | 5 | 5 | 5 | 5 | 6 | 6 | 6 | 6 | 6 | 7 | 7 | 7 | 7 | 7 | 7 | 8 | 8 | 8 | 8 | 8 | 8 |
| Strong | 0 | 2 | 3 | 3 | 4 | 4 | 4 | 4 | 5 | 5 | 5 | 5 | 5 | 6 | 6 | 6 | 6 | 6 | 6 | 7 | 7 | 7 | 7 | 7 | 8 | 8 | 8 | 8 | 8 | 8 | 8 | 8 |

Some examples, compared with unmodded MDVI (casual rules):

| Clear              | Level | MD3 Weak / Medium / Strong | MDVI Low / Middle / High |
|--------------------|------:|:--------------------------:|:------------------------:|
| single, 7 orbs     |  1    | 1 / 2 / 2                  | 0 / 0 / 0                |
| 2-chain, 6 orbs    |  4    | 2 / 3 / 4                  | 1 / 1 / 3                |
| 3-chain, 9 orbs    |  9    | 4 / 4 / 5                  | 2 / 2 / 6                |
| 5-chain, 15 orbs   | 19    | 5 / 6 / 7                  | 2 / 4 / 7                |
| 7-chain, 21 orbs   | 28    | 7 / 8 / 8                  | 3 / 5 / 7                |

**Waiting attacks merge.** An attack waits while its target is still chaining. If more attacks
arrive before it has finished dropping, MD3 adds their *levels* together (max 31) into a single
second attack instead of stacking more lines.

### Refill

While the field holds fewer than 20 drops (scaled for widths other than 7 columns), a new line
drops right away and the regular push timer restarts. Nothing is pushed while any column is one
row from the bottom. Held drops don't count toward the 20.

The refill is on in VS CPU and local versus. Survival and Caravan can be switched on in the
config. MD3's push timer, which speeds up over a round, is left at MDVI's timing.

### Online

Online matches are left alone unless `ApplyOnline = true`. Both players then need the same
version and settings.

## Configuration

`BepInEx/config/muki.md6.md3inmd6.cfg`

| Section | Key | Default | |
|---|---|---|---|
| General | `Enabled` | true | Master switch |
| General | `ApplyOnline` | false | Use the rules in online matches |
| General | `DebugLog` | false | Log every attack (chain, orbs, level, lines) and refill |
| Attacks | `Md3Attacks` | true | MD3 attack levels and tables |
| Attacks | `MergePendingAttacks` | true | Merge waiting attacks by level |
| Attacks | `ForceTable` | Character | `Character`, `Weak`, `Medium` or `Strong` |
| Refill | `AutoRefill` | true | Refill when the field runs low |
| Refill | `RefillThreshold` | 20 | Drops on a 7-column field |
| Refill | `RefillInSurvival` | false | |
| Refill | `RefillInCaravan` | false | |

## Building

You need the .NET SDK (6 or newer) and a Magical Drop VI install. The project references the
game's own `Assembly-CSharp.dll` and Unity DLLs from the install, so no game files are kept in
this repo.

```
copy Directory.Build.props.user.example Directory.Build.props.user   # set GameDir
dotnet build src/MD3inMD6 -c Release
```

If the game folder already has `BepInEx/plugins`, the build copies `MD3inMD6.dll` into
`BepInEx/plugins/MD3-in-MD6`.

Without the .NET SDK, [build/build-roslyn.ps1](build/build-roslyn.ps1) builds the DLL with the
compiler that ships inside PowerShell 7. [build/package.ps1](build/package.ps1) makes the release
zips.

## Planned

- Paddle / spinner / mouse control of the clown, as in the MD3 Paddle Edition patches.

## License

MIT, see [LICENSE](LICENSE). This covers the plugin's code only. Magical Drop VI and
Magical Drop III belong to their owners, and no game files are included. The
`-with-BepInEx` release zip bundles BepInEx, which is LGPL-2.1.
