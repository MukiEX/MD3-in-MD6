# Changelog

## 1.0.0

First release.

- MD3 attack levels, `(orbs - 3) / 4 + 4 * (chain - 1)` capped at 31, with the Weak, Medium and
  Strong line tables. Up to 8 lines per attack.
- Single clears of 7 or more orbs attack.
- An attack that arrives while an earlier one is still waiting merges into it by level, as in MD3.
- Automatic refill: below 20 drops (7 columns), a new line drops right away.
- Online play is left alone unless `ApplyOnline` is set.
