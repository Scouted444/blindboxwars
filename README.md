# Blind Box Wars

A first-person Labubu-dealing sim. Labubus are banned in Plushville: press them, mix them, text your buyers, meet them on street corners, and dodge the cops.

## Download
Go to **Releases** (right side of the repo page), download `BlindBoxWars-Windows.zip`, unzip, run `BlindBoxWars.exe`.
Windows SmartScreen may warn because the exe is unsigned: More info → Run anyway.

## Controls
WASD move · Shift sprint · Space jump · E use · Tab phone · Esc close / pause

## How it's built
Every push to `main` runs `.github/workflows/build.yml`, which installs Unity 2022.3 on a GitHub Windows runner (cached between runs),
activates a free Personal license from the `UNITY_USERNAME` / `UNITY_PASSWORD` repo secrets, and builds the game.
The whole town is generated from code at runtime (`Game/Assets/BlindBoxEmpire/Scripts`), so there are no scenes or prefabs to maintain.
