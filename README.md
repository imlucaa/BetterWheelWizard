# BetterWheelWizard

A community version of [WheelWizard](https://github.com/TeamWheelWizard/WheelWizard) with a cleaner interface and additional Retro Rewind tools.

## Download

| Platform | Download |
| --- | --- |
| Windows x64 | [BetterWheelWizard.exe](https://github.com/imlucaa/BetterWheelWizard/releases/latest/download/BetterWheelWizard.exe) |
| Linux x64 | [BetterWheelWizard_Linux](https://github.com/imlucaa/BetterWheelWizard/releases/latest/download/BetterWheelWizard_Linux) |
| Linux ARM64 | [BetterWheelWizard_ARM64_Linux](https://github.com/imlucaa/BetterWheelWizard/releases/latest/download/BetterWheelWizard_ARM64_Linux) |

All builds are standalone single files. No installer or separate .NET installation is required.

## Features

- **Leaderboard:** search any player by name or friend code and view them in 3D
- **Rooms:** live room list with search, VR filter and sorting
- **Miis:** find, download, import and edit Miis
- **Themes:** custom colours and background images, with share codes
- **Backups:** save your Mario Kart Wii saves and Retro Rewind ratings
- **Launch:** play Retro Rewind with Dolphin or WiiCompiled

## Screenshots

<p align="center">
  <img src="docs/screenshots/home.png" width="230" alt="Home" />
  <img src="docs/screenshots/custom-background.png" width="230" alt="Custom background" />
  <img src="docs/screenshots/leaderboard.png" width="230" alt="Leaderboard" />
</p>
<p align="center">
  <img src="docs/screenshots/rooms.png" width="230" alt="Rooms" />
  <img src="docs/screenshots/backup.png" width="230" alt="Backup" />
</p>

## Build from source

BetterWheelWizard requires the .NET 10 SDK.

```sh
dotnet test WheelWizard.sln --configuration Release
dotnet build WheelWizard.sln --configuration Release
```

## License

BetterWheelWizard is an unofficial fork of WheelWizard and is licensed under the [GNU GPL v3](LICENSE).
