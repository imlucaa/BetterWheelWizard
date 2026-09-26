# Changelog

User-facing changes to BetterWheelWizard are recorded here. Versions follow
the BetterWheelWizard release sequence rather than the upstream Wheel Wizard
version sequence.

## [Unreleased]

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

[Unreleased]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.14...HEAD
[2.5.14]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.13...v2.5.14
[2.5.13]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.12...v2.5.13
[2.5.12]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.11...v2.5.12
[2.5.11]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.10...v2.5.11
[2.5.10]: https://github.com/imlucaa/BetterWheelWizard/compare/v2.5.9...v2.5.10
[2.5.9]: https://github.com/imlucaa/BetterWheelWizard/releases/tag/v2.5.9
