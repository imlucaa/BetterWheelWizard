# BetterWheelWizard

A community version of [WheelWizard](https://github.com/TeamWheelWizard/WheelWizard) with a cleaner interface and additional Retro Rewind tools.

## Download

**[Download BetterWheelWizard for Windows](https://github.com/imlucaa/BetterWheelWizard/releases/latest/download/BetterWheelWizard.exe)**

The Windows build is standalone. It does not need an installer or a separate .NET installation.

## Features

- Search the RWFC leaderboard by player name or friend code
- Browse rooms with quick search, VR filters, and sorting
- Find, import, edit, and manage Miis
- Create, save, share, and import custom launcher themes
- Back up Mario Kart Wii saves and Retro Rewind ratings
- Enjoy cleaner popups, compact controls, and other QoL improvements
- Launch Retro Rewind with Dolphin or WiiCompiled

## Screenshots

| Themes | Leaderboard |
| --- | --- |
| [![Custom themes](docs/screenshots/custom-themes.png)](docs/screenshots/custom-themes.png) | [![Leaderboard](docs/screenshots/better-leaderboard.png)](docs/screenshots/better-leaderboard.png) |
| Rooms | Mii Downloader |
| [![Rooms](docs/screenshots/better-rooms.png)](docs/screenshots/better-rooms.png) | [![Mii Downloader](docs/screenshots/mii-downloader.png)](docs/screenshots/mii-downloader.png) |

## Setup

1. Download and open `BetterWheelWizard.exe`.
2. Select your Dolphin executable.
3. Select your legally dumped Mario Kart Wii game.
4. Let BetterWheelWizard install or update Retro Rewind.

Dolphin supports any game region. WiiCompiled currently requires a PAL game.

## Build from source

BetterWheelWizard requires the .NET 10 SDK.

```sh
dotnet test WheelWizard.sln --configuration Release
dotnet build WheelWizard.sln --configuration Release
```

## License

BetterWheelWizard is an unofficial fork of WheelWizard and is licensed under the [GNU GPL v3](LICENSE).
