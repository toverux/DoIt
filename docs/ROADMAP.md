# Roadmap

Planned work on the mod that no ticket holds yet.

Every entry opens with a triage line: **Priority** (high, medium, low) and **Cost** as the code reads at triage (trivial, cheap, moderate, large), either one `unsure` when nobody has judged it yet.

## The shape of an action

**Priority:** high · **Cost:** moderate

Everything else hangs on this. An action is what the palette lists and the toolbar pins: a stable ID, a localized label and description, an icon, a category, an availability predicate (main menu, in game, a tool active, an entity selected), and an invoke. It takes no argument or one, the selected entity the first case; what else an argument can be (a prefab, a position, a free-text query) and how the palette collects it before invoking are open. Settle the C# model, its wire format to the UI, and how a source reports an action gone or changed, before the first bridge.

## Game actions as the first source

**Priority:** high · **Cost:** large

Hook into the game's own actions: the input actions of the tool system and the UI (open a panel, pick a tool, a photo mode toggle), the simulation controls, selection operations. Reach each one by a direct call where the API is public, by reflection where it is not; `DoIt/Reflection` holds the accessors. Catalogue what the game exposes before picking the first batch, and keep the catalogue in a doc an agent can grow.

## Bridges to installed mods

**Priority:** high · **Cost:** large

A bridge adapts a mod that knows nothing of Do It, by reflection into its assembly, guarded so the mod's absence, another version or a thrown exception costs the player nothing but that bridge. Order the mods by install count, which the Paradox Mods API serves (HallOfFame queries it; borrow its client), and bring them in from the top. Every bridge records which mod version it was written against.

## The registration API

**Priority:** high · **Cost:** moderate

Mods that want to register actions themselves. The first shape is a reflection snippet they paste: a few lines that find Do It's registrar by type name and call it with plain types, so a mod depends on nothing and runs unchanged when Do It is absent. The snippet is published under MIT-0. A typed `DoIt.Api` assembly under MIT may follow for the mods that prefer a compile-time contract; its versioning is then a contract of its own, independent of the mod's CalVer.

## The command palette

**Priority:** high · **Cost:** large

Opened by a key binding, `Alt+Space` the first candidate, rebindable in the settings. Fuzzy search over the actions, ranked by match and recent use, keyboard driven end to end, built from the game's own chrome so it reads as part of the game. Argument collection for actions that take one. Controller support is the open question: what the palette becomes without a keyboard.

## The toolbar

**Priority:** high · **Cost:** large

Pin any palette action to a toolbar for one-click access, with the same availability rules, ordered and grouped by the player, persisted in the settings. Where it docks in the game's layout, and how it behaves when the game's own toolbars and panels move, is to be decided against the real UI.

## A skill to bring support to a mod

**Priority:** medium · **Cost:** moderate

A repo-local skill an agent runs on a mod not yet supported: locate its source or decompile, find what it exposes that could be an action, write the bridge in the shape the first bridges settled, verify it in the running game with the mod installed, and record the mod version. Written once a few bridges exist by hand, so the skill captures what they have in common rather than guesses at it.

## Robustness over foreign code

**Priority:** high · **Cost:** moderate

Every source is foreign code: the game's internals, other mods' assemblies, a mod's registration call. One misbehaving source is isolated, logged once with what failed and why, and the rest keeps working; a bridge that fails on load reports which mod and which version. A game update or a mod update breaking a bridge shows in the log before a player reports it. Decide what the palette shows for a source that is down.

## Performance budget

**Priority:** medium · **Cost:** moderate

The palette opens in a frame and filters as the player types; the toolbar costs nothing when idle. Measure in the running game rather than reason from the source, and keep the measurements with the code that meets them. Reflection lookups are resolved once, at bridge load, never per invoke.
