# Changelog

User-facing changes to BetterWheelWizard are recorded here. Versions follow
the BetterWheelWizard release sequence rather than the upstream Wheel Wizard
version sequence.

## [Unreleased]

## [2.6.5] - 2026-10-18

### Added

- Discord RPC tab: show Retro Rewind on your Discord profile with your own image, the current screen, track, players, VR and BR. A short setup guide walks you through it.
- "View Player" and "View Room" buttons on your Discord profile, each with its own switch. View Room opens the room in Wheel Wizard for people who have it open.
- Color picker now works, and any hex color is used exactly as typed.

### Changed

- The "Mods found" question no longer appears when you start the game.
- Panels at partial opacity no longer show a black line.

## [2.6.4] - 2026-10-17

### Changed

- Backing up rksys.dat now asks which region to save, or uses the region you played last.
- Simplified the Backup tab and the Theme library popup.
- Centered the Rooms and Leaderboard titles.

### Fixed

- Rooms labels and many settings, leaderboard and friend messages are now translated into Japanese.

## [2.6.3] - 2026-10-17

### Changed

- Restyled the launcher with flat, theme-aware controls: no outlines, dark panel buttons, and accent buttons that stay readable on every theme.
- Redesigned the My Miis and Mii Downloader pages, with an interactive 3D Mii preview in the downloader and in Leaderboard search results.
- Refreshed Rooms, Leaderboard, Patches, Themes and Settings layouts, and restyled all popups.
- Custom background images no longer show a seam at the window edge.

### Fixed

- Accent-coloured button labels are now always crisp black or white for contrast.
- Disabled accent buttons are visibly dimmed instead of turning grey.

## [2.6.0] - 2026-09-28

### Changed

- Rebuilt BetterWheelWizard on the rewritten Wheel Wizard application engine for improved reliability, maintainability, startup handling, and path management.
- Preserved the existing `CT-MKWII` application-data location so current settings and files continue to be found after upgrading.
- Updated the app identity, update checker, and standalone release downloads for BetterWheelWizard.

### Added back

- Custom launcher themes, built-in presets, share codes, and theme importing.
- Direct backups for regional Mario Kart Wii saves and Retro Rewind ratings.
- RWFC leaderboard search, pagination, player profiles, and Mii copying.
- Room search, average-VR filters, sorting, track details, and race timers.
- Online Mii search, suggestions, downloading/importing, and saved-Mii filtering.

### Downloads

- Standalone Windows x64, Linux x64, and Linux ARM64 single-file builds remain available without a separate .NET installation.

## [2.5.14] - 2026-09-27

### Added

- Added a popup Theme Library for choosing built-in and saved themes.
- Added compact palette cards with larger color swatches and aligned HEX inputs.
- Added animated gold highlighting for searched leaderboard players.

### Changed

- Simplified theme choosing, saving, sharing, and deletion into compact actions.
- Improved leaderboard page spacing, ranked-player rows, pagination, and later-page placement labels.
- Made popup confirmation buttons use the same neutral style as popup cancel buttons.
- Simplified the README and release automation.

### Fixed

- Fixed RWFC search ranks above 50,000 being replaced with the search-result position.
- Fixed imported theme codes retaining the previously selected theme name and delete state.
- Fixed later leaderboard pages showing page-slot labels instead of actual placements.
- Fixed the searched-player profile button not matching the adjacent Back button.

## [2.5.13] - 2026-09-25

### Added

- Added RWFC leaderboard search by friend code or player/Mii name.
- Added duplicate-result browsing for leaderboard and Mii downloader searches.
- Added popup-based search entry for the leaderboard, My Miis, and Mii Downloader.
- Added a dedicated Rooms sort popup for cleaner room-browser controls.

### Changed

- Refined the leaderboard header and player result card to better match the app's native styling.
- Reworked Rooms controls into centered popup actions for search, VR filtering, and sorting.
- Cleaned up page chrome, spacing, separators, and window-control padding across the app.
- Updated the README with current screenshots and clearer setup and feature documentation.

### Fixed

- Fixed RWFC Mii downloader lookups for both friend code and player-name searches.
- Fixed leaderboard search reset behavior so clearing a search returns to the default ranked view.
- Fixed font/resource URI usage for BetterWheelWizard branding assets.
- Removed the unused Rooms page warning and deleted an unused converter.

## [2.5.12] - 2026-09-24

### Added

- Added a visual color picker with copyable and editable HEX values for all four theme colors.
- Added more built-in presets covering basic rainbow colors and popular colors such as coral, mint, lavender, and rose.

### Changed

- Renamed the accent control to Main color and added plain-language descriptions of what each color changes.
- Applied selected main, wheel/title, and background colors exactly across the full brightness range, including pure black and white.
- Adapted surfaces and text for bright backgrounds while retaining automatic readable-text contrast.
- Made the Home wheel and game title follow the wheel/title color, while the BetterWheelWizard name follows the text color.
- Simplified and compacted the Themes page and color-picker layout.

### Fixed

- Fixed built-in presets loading intermediate values into the wrong color fields.
- Fixed positive VR History changes using accent/green text instead of the normal theme text color.

## [2.5.11] - 2026-09-24

### Changed

- Renamed future release downloads to use the BetterWheelWizard name.
- Improved room-browser responsiveness at narrow widths and high display scaling.
- Updated release automation to publish structured notes from this changelog.

## [2.5.10] - 2026-09-23

### Fixed

- Fixed clipped room game-mode labels such as `RR 150CC`.
- Tightened spacing between game modes and room IDs.
- Expanded player rows to use the full width in Rooms and Room Details.

## [2.5.9] - 2026-09-23

### Added

- Added built-in and saved launcher themes with replace, delete, and restore controls.
- Added compact `BWW1` theme codes for copying and importing shared colors.
- Added direct backups for regional `rksys.dat` saves and `RRRating.pul`.

### Changed

- Applied theme colors across launcher navigation, actions, profiles, rooms, and status indicators.
- Refined page headers, cards, tabs, buttons, spacing, and narrow layouts.

[Unreleased]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.6.0...HEAD
[2.6.0]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.14...v2.6.0
[2.5.14]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.13...v2.5.14
[2.5.13]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.12...v2.5.13
[2.5.12]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.11...v2.5.12
[2.5.11]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.10...v2.5.11
[2.5.10]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.9...v2.5.10
[2.5.9]: https://github.com/imlucaa/BetterWheelWizard/releases/tag/v2.5.9
