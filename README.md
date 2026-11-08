# Do It: Command Palette & Toolbar

A command palette over many game and mod actions, and a toolbar to pin the ones you use most.

## Development

This mod started from [Greenfield Mod Template](https://github.com/CitiesSkylinesModding/greenfield-mod-template), whose README explains its tooling and the reasons behind it.

### Setup

- [mise](https://mise.jdx.dev), then `mise install` for the pinned bun and .NET SDK.
- The game's modding toolchain, installed from the game's Modding options. On Linux, where that installer cannot run, `mise setup:linux` installs it.
- `bun install`, then `bunx lefthook install` for the git hooks.

### Commands

- `mise build`: Builds the mod and deploys it to the game, which must be closed.
- `mise build:ui`: Builds and deploys the UI alone, with the game open.
- `mise dev`: Rebuilds the UI on every change.
- `mise check`: Type-checks the UI, lints it, and checks its formatting.
- `mise fix`: Applies the lint fixes, then formats the TypeScript and the C#.
- `mise test`: Runs the off-engine C# tests, the UI tests, and the scripts' tests.
- `mise l10n:pull`, `mise l10n:push`: Pull translations from Crowdin and push the repo's edits to it.
- `mise publish`: Publishes a new version on Paradox Mods.

Run `mise tasks` for the full list.

An agent restarts the game and runs the in-game scenarios with the `cs2-modding` plugin's `cs2-dev` tools.
