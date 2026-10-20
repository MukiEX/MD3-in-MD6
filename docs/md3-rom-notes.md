# Magical Drop III (Neo Geo) — versus rules in the 68k program

Addresses are in the `magdrop3` P ROM, byte-swapped to big-endian (the `.neo` stores the P ROM
with each 16-bit word byte-swapped). They were found by static disassembly with Capstone.

## Player struct

Two structs, `$100600` (P1) and `$100640` (P2), usually held in `a6`.

| Offset | Size | Meaning |
|---|---|---|
| `$02` | byte | Character ID |
| `$03` | byte | Character of the last attacker (selects attack row patterns) |
| `$06` | byte | Lines per normal push (1 in VS) |
| `$07` | byte | Normal lines still to push |
| `$08` | byte | Level of the pending incoming attack |
| `$09` | byte | Lines of the pending incoming attack still to push |
| `$0A` | word | Push interval (frames) |
| `$0C` | word | Push countdown |
| `$16` | byte | Drops on the field (sum of the 7 column heights) |
| `$17` | byte | Set while the player is chaining |
| `$1E` | byte | Chain counter for the current chain |
| `$20` | word | Orbs cleared in the current chain |
| `$30` | byte | Level of the second (merged) incoming attack |
| `$31` | byte | Lines of the second incoming attack |

Column structs are `$40` bytes each, pointed to by `a5`. `$01` is the column height.

## Attack level — `0x159FE` (and `0x15972`, which also computes the score)

```
d0 = orbs ($20) - 3 ; if <= 0 -> no attack
d0 >>= 2
d1 = chain ($1E) - 1 ; if > 0: d0 += d1 * 4
level = min(d0, 31)
```

## Line tables — `0x175C6`

`0x17692` maps the character ID (`2 * id`) to a table selector of 0, 4 or 8. `0x175C6` is a
table of pointers indexed by that selector:

| Selector | Table | Lines for levels 0–31 (low byte of each word) |
|---|---|---|
| 0 | `0x175D2` Weak | 0 1 1 2 2 3 3 3 3 4 4 4 4 4 4 5 5 5 5 5 6 6 6 6 6 7 7 7 7 7 8 8 |
| 4 | `0x17612` Medium | 0 2 2 3 3 3 4 4 4 4 5 5 5 5 5 6 6 6 6 6 7 7 7 7 7 7 8 8 8 8 8 8 |
| 8 | `0x17652` Strong | 0 2 3 3 4 4 4 4 5 5 5 5 5 6 6 6 6 6 6 7 7 7 7 7 8 8 8 8 8 8 8 8 |

The high byte of each word is a bit mask. When line *n* is pushed (counting down), bit *n*
chooses the attacker's second row pattern for that line (`0x17334`–`0x17350`).

## Delivery — `0x15702`–`0x157EA`

This runs when a chain ends. `a1` is the opponent's struct.

```
if opponent.$08 != 0:                   ; an attack is already pending
    opponent.$30 = min(opponent.$30 + level, 31)
    opponent.$31 = table[attacker][opponent.$30]
else:
    opponent.$08 = level
    opponent.$09 = table[attacker][level]
```

When the first attack has been pushed, `$30/$31` move into `$08/$09` (`0x12ABA`–`0x12ACC`).

## Push logic — VS update `0x1292A`

- The pending attack (`$09`) is pushed as soon as the player isn't chaining (`$17 == 0`).
- **Refill** (`0x12A0A`): when the countdown hasn't run out, but no column is 11+ high (bit 0 of
  `$01`) and `$16 < 20` (`cmpi.b #$14`), push `$06` lines now.
- The countdown restarts from `$0A` after each push (`0x12A78`–`0x12AAE`). A difficulty
  offset from the table at `0x12CF0` is subtracted, with a minimum of `$20`.
- The interval starts at `$100` frames and shrinks by `$10` every `$100` frames, down to `$40`
  (`0x12932`–`0x12952`).
