# BetterWheelWizard

<p align="center">
  <strong>An enhanced Wheel Wizard launcher for Mario Kart Wii and Retro Rewind.</strong>
</p>

<p align="center">
  <a href="https://github.com/imlucaa/BetterWheelWizard/releases/latest">
    <img src="https://img.shields.io/github/v/release/imlucaa/BetterWheelWizard?style=for-the-badge&label=Latest%20release" alt="Latest release" />
  </a>
  <a href="https://github.com/imlucaa/BetterWheelWizard/releases">
    <img src="https://img.shields.io/github/downloads/imlucaa/BetterWheelWizard/total?style=for-the-badge" alt="Total downloads" />
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/github/license/imlucaa/BetterWheelWizard?style=for-the-badge" alt="GNU GPL v3 license" />
  </a>
</p>

> [!IMPORTANT]
> BetterWheelWizard is an unofficial community fork of
> [TeamWheelWizard/WheelWizard](https://github.com/TeamWheelWizard/WheelWizard).
> It is not an official Team Wheel Wizard release.

## Highlights

- Enhanced room browser with search, VR filters, sorting, room details, and race status
- Mii Downloader with RWFC friend-code lookup and downloadable public Miis
- Saved-Mii search, importing, copying, and player Mii actions
- Custom launcher colors for controls, branding, text, and backgrounds
- Dolphin and WiiCompiled support

## Feature showcase

### Custom launcher themes

Choose separate colors for controls, BetterWheelWizard branding, interface
text, and the launcher background. Colors update across the launcher with
automatic readability adjustments.

<p align="center">
  <img src="docs/screenshots/custom-themes.png" alt="BetterWheelWizard custom theme editor" width="680" />
</p>

<table>
  <tr>
    <td width="50%" valign="top">
      <h3>Mii Downloader</h3>
      <p>Find an RWFC player by friend code or browse random public Miis, then import a fresh copy into My Miis.</p>
      <img src="docs/screenshots/mii-downloader.png" alt="BetterWheelWizard Mii Downloader" width="100%" />
    </td>
    <td width="50%" valign="top">
      <h3>Better room browsing</h3>
      <p>Search rooms and players, filter by average VR, and sort the room list from one compact control panel.</p>
      <img src="docs/screenshots/better-rooms.png" alt="BetterWheelWizard room search and VR filters" width="100%" />
    </td>
  </tr>
</table>

## Download

Download **BetterWheelWizard.exe** from the
[latest release](https://github.com/imlucaa/BetterWheelWizard/releases/latest).



## Requirements

You must provide your own legally dumped Mario Kart Wii game backup.
Supported formats include `.iso`, `.gcm`, `.gcz`, `.ciso`, `.wbfs`, `.wia`,
and `.rvz`.

| Mode | Supported game region |
| --- | --- |
| Retro Rewind through Dolphin | Any region |
| WiiCompiled, with or without Retro Rewind | PAL |

## Build from source

The project currently targets .NET 10.

```powershell
dotnet test WheelWizard.sln --configuration Release

dotnet publish WheelWizard/WheelWizard.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## Report a bug

[Open an issue](https://github.com/imlucaa/BetterWheelWizard/issues/new) and
include:

- What happened
- Steps to reproduce it
- Your Windows version and launch mode
- A screenshot or log when available

## Credits

BetterWheelWizard is based on
[TeamWheelWizard/WheelWizard](https://github.com/TeamWheelWizard/WheelWizard),
created by [Patchzy](https://github.com/patchzyy) and
[WantToBeeMe](https://github.com/wanttobeeme).

Additional upstream acknowledgements:

- Retro Rewind was created by ZPL. See the
  [Tockdom Wiki](https://wiki.tockdom.com/wiki/Retro_Rewind).
- Parts of the Mii renderer were inspired by
  [ariankordi/FFL-Testing](https://github.com/ariankordi/FFL-Testing), based on
  [aboood40091/FFL-Testing](https://github.com/aboood40091/FFL-Testing).
- The wheel and flat-tire icons are by Delapouite from
  [Game Icons](https://game-icons.net/about.html).
- Special icons use Chadderz' Terrible Mario Kart Font.

## License

BetterWheelWizard is distributed under the
[GNU General Public License v3.0](LICENSE), matching the upstream project.
You may use, modify, and redistribute it under the terms of that license, and
derivative distributions must remain GPL v3.0 compatible and provide their
corresponding source code.
