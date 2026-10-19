# Magia Wheel Plus

Attack / Defense buttons and Auto Attack / Auto Defense toggles next to the Eureka magia board.

## The bar

A small native bar sits beside the magia board, follows it and matches its scale. Top to bottom:

- **Side arrow**: moves the bar to the other side of the board (left or right). The arrow points where the bar will go; the choice is saved.
- **Attack**: `/magiaattack`, spins the board to your target's elemental weakness. Greyed out without a target.
- **Defense**: `/magiadefense`, spins the board to your target's elemental affinity. Greyed out without a target.
- **Auto Attack** toggle: lit while the board's auto-spin is set to attack. Click it when dimmed to run `/magiaauto attack`, click it when lit to run `/magiaauto off`.
- **Auto Defense** toggle: same for defense (`/magiaauto defense` / `/magiaauto off`).

Only one auto toggle is lit at a time; the state is read from the board's own auto-spin indicator. A second click on a toggle is ignored until the board reacts (or about a second passes), so a fast double click cannot switch auto-spin on and straight back off.

## Auto next

When the lockbox / logogram appraisal list (the item inspection list window) is open, a small "Auto next" checkbox sits just above it. Tick it and the plugin presses **Next** on the **Results** window for you after each opening. You still pick the item and confirm Yes yourself; only Next is automated. It works for anything that goes through the item inspection windows (Eureka lockboxes, logogram appraisal, ...). The checkbox is not saved: it switches itself off whenever the list window closes.

## Install
```
https://raw.githubusercontent.com/Iluay0/magia-wheel-plus/master/repo.json
```
