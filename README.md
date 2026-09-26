# BetterWheelWizard

<p align="center">
  <strong>A cleaner launcher for Mario Kart Wii and Retro Rewind.</strong><br />
  Play, browse RWFC, manage Miis, customize the launcher, and back up your saves.
</p>

<p align="center">
  <a href="https://github.com/imlucaa/BetterWheelWizard/releases/latest">
    <img src="https://img.shields.io/github/v/release/imlucaa/BetterWheelWizard?style=flat-square&label=release" alt="Latest release" />
  </a>
  <a href="https://github.com/imlucaa/BetterWheelWizard/releases">
    <img src="https://img.shields.io/github/downloads/imlucaa/BetterWheelWizard/total?style=flat-square" alt="Downloads" />
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/github/license/imlucaa/BetterWheelWizard?style=flat-square" alt="GPL v3 license" />
  </a>
</p>

<p align="center">
  <a href="https://github.com/imlucaa/BetterWheelWizard/releases/latest/download/BetterWheelWizard.exe"><strong>Download for Windows</strong></a>
  ·
  <a href="CHANGELOG.md">Changelog</a>
  ·
  <a href="https://github.com/imlucaa/BetterWheelWizard/issues/new">Report a bug</a>
</p>

> [!IMPORTANT]
> BetterWheelWizard is an unofficial community fork of
> [WheelWizard](https://github.com/TeamWheelWizard/WheelWizard).

## Features

- Launch and update Retro Rewind through Dolphin or WiiCompiled
- Browse live rooms and search the RWFC leaderboard
- Find, import, and manage Miis
- Create, save, and share custom launcher themes
- Back up regional `rksys.dat` saves and `RRRating.pul`
- Search and filter through compact popup actions

## Screenshots

<p align="center">
  <a href="docs/screenshots/better-rooms.png">
    <img src="docs/screenshots/better-rooms.png" alt="BetterWheelWizard room browser" width="48%" />
  </a>
  <a href="docs/screenshots/mii-downloader.png">
    <img src="docs/screenshots/mii-downloader.png" alt="BetterWheelWizard Mii downloader" width="48%" />
  </a>
</p>

## Setup

1. [Download `BetterWheelWizard.exe`](https://github.com/imlucaa/BetterWheelWizard/releases/latest/download/BetterWheelWizard.exe).
2. Select your Dolphin executable.
3. Select your legally dumped Mario Kart Wii game file.
4. Let BetterWheelWizard install or update Retro Rewind.

The Windows build is standalone; no installer or separate .NET installation is required.

### Requirements

- A legally dumped Mario Kart Wii game
- Dolphin mode: any game region
- WiiCompiled mode: PAL game only

Supported game formats: `.iso`, `.gcm`, `.gcz`, `.ciso`, `.wbfs`, `.wia`, and `.rvz`.

## Build from source

BetterWheelWizard targets **.NET 10**.

```powershell
dotnet test WheelWizard.sln --configuration Release

dotnet publish WheelWizard/WheelWizard.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

Use `linux-x64` or `linux-arm64` as the runtime for a Linux build. Prebuilt macOS packages are not currently provided.

## Credits

Based on [TeamWheelWizard/WheelWizard](https://github.com/TeamWheelWizard/WheelWizard), created by
[Patchzy](https://github.com/patchzyy) and [WantToBeeMe](https://github.com/wanttobeeme).
Retro Rewind was created by ZPL.

BetterWheelWizard is licensed under the [GNU General Public License v3.0](LICENSE).
