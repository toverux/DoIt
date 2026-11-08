# Roadmap

Planned work on the mod that no ticket holds yet.

Every entry opens with a triage line: **Priority** (high, medium, low) and **Cost** as the code reads at triage (trivial, cheap, moderate, large), either one `unsure` when nobody has judged it yet.

## The shape of an action

**Priority:** high · **Cost:** moderate

Everything else hangs on this. An action is what the palette lists and the toolbar pins: a stable ID, a localized label and description, an icon (the game's or the mod's own for that action where one exists), a category, an availability predicate (main menu, in game, a tool active, an entity selected), an invoke, and, where the action is a toggle or holds a value or a mode, its state, read live so the palette and the toolbar show it. It takes no argument or one, the selected entity the first case; what else an argument can be (a prefab, a position, a free-text query) and how the palette collects it before invoking are open. Three invoke channels, measured in the running game: a public C# member; a cohtml trigger, fired from C# for `bool`, `int` and `string` arguments only (an entity argument crashes the game, so those go through the UI side or the C# member behind the trigger); and a UI input action, dispatched through the game's input stack from the UI side only. Settle the C# model, its wire format to the UI, and how a source reports an action gone or changed, before the first bridge.

## State and sub-actions in the results

**Priority:** high · **Cost:** moderate

An action is not a bare item. A result row shows, on its right, the state the action holds (a toggle on or off, the current value, the active mode) and its sub-actions where it has several, so the player reads the state and changes it without leaving the row: a toggle flips in place, a value edits inline, a sub-action is picked with the arrow keys. A pinned action shows the same state on its toolbar button.

## Search quality

**Priority:** high · **Cost:** moderate

State of the art fuzzy matching over the action labels: word-start and abbreviation bonuses (`ps` finds Pause Simulation), tolerance to a typo, diacritics folded, and a ranking that mixes match quality with recent and frequent use. Candidates to measure against the game's own action names in the 12 locales, CJK among them: fzf's algorithm, VS Code's `fuzzyScore`, command-score. The choice is made on a benchmark of real labels, not on a demo.

## Settings as actions

**Priority:** medium · **Cost:** large

The game's and the mods' settings are searchable like actions, with a quick inline edit where the type allows (a toggle, a slider, a dropdown) and a jump to the options page at that setting otherwise; `OptionsUISystem.OpenPage` is public. The options screen already searches every page's widgets with their localized labels, through `OptionsUISystem` and its `options.pages` binding, so Do It reuses that list rather than building a second index; the `ModSetting` registry that lists every mod's key bindings is internal, so that one read is a reflection seam to isolate.

## Picking a subject for a bare invoke

**Priority:** high · **Cost:** moderate

When an action needs a subject and is invoked bare, Do It activates a pick tool, the action runs on what the player clicks, and Escape cancels and restores the previous tool. Only where possible: an entity the player can click qualifies, a prefab opens the asset search instead, a free-text subject prompts in the palette. FindIt's picker and the game's `Selection Tool` are the models.

## Game actions as the first source

**Priority:** high · **Cost:** large

Hook into the game's own actions: the input actions of the tool system and the UI (open a panel, pick a tool, a photo mode toggle), the simulation controls, selection operations. Reach each one by a direct call where the API is public, by reflection where it is not; `DoIt/Reflection` holds the accessors. The input manager enumerates every action with its current keys, and the Shortcuts map's action names are the UI's alias names, so the catalogue starts from it. Catalogue what the game exposes before picking the first batch, and keep the catalogue in a doc an agent can grow.

## Bridges to installed mods

**Priority:** high · **Cost:** large

A bridge adapts a mod that knows nothing of Do It, by reflection into its assembly, guarded so the mod's absence, another version or a thrown exception costs the player nothing but that bridge. Order the mods by install count, which the Paradox Mods API serves without authentication (HallOfFameServer's `mod.service.ts` queries it; borrow its client), and bring them in from the top. Every bridge records which mod version it was written against: the mod manager's list gives the assembly version, the Paradox version string comes from the PDX SDK's playset info.

## The registration API

**Priority:** high · **Cost:** moderate

Mods that want to register actions themselves. The first shape is a reflection snippet they paste: a few lines that find Do It's registrar by type name and call it with plain types, so a mod depends on nothing and runs unchanged when Do It is absent. The snippet is published under MIT-0. A typed `DoIt.Api` assembly under MIT may follow for the mods that prefer a compile-time contract; its versioning is then a contract of its own, independent of the mod's CalVer.

## The command palette

**Priority:** high · **Cost:** large

Opened by a key binding, `Alt+Space` the first candidate, rebindable in the settings. Search per the Search quality entry, keyboard driven end to end, built from the game's own chrome so it reads as part of the game. A row shows the action's icon where it has one and nothing in its place otherwise, and the action's current key where the game or the mod binds one. Argument collection for actions that take one. The game's shortcut consumers unregister while a modal is open, so the palette either stays out of the game's modal scope or closes before it dispatches a game action. Controller support is the open question: what the palette becomes without a keyboard.

## The toolbar

**Priority:** high · **Cost:** large

Pin any palette action to a toolbar for one-click access, from its palette row with one key, with the same availability rules, ordered and grouped by the player by drag, persisted in the settings. A pinned action without an icon shows its initials. Where it docks in the game's layout, and how it behaves when the game's own toolbars and panels move, is to be decided against the real UI.

## A skill to bring support to a mod

**Priority:** medium · **Cost:** moderate

A repo-local skill an agent runs on a mod not yet supported: locate its source or decompile, find what it exposes that could be an action, write the bridge in the shape the first bridges settled, verify it in the running game with the mod installed, and record the mod version. Written once a few bridges exist by hand, so the skill captures what they have in common rather than guesses at it.

## Robustness over foreign code

**Priority:** high · **Cost:** moderate

Every source is foreign code: the game's internals, other mods' assemblies, a mod's registration call. One misbehaving source is isolated, logged once with what failed and why, and the rest keeps working; a bridge that fails on load reports which mod and which version. A game update or a mod update breaking a bridge shows in the log before a player reports it. A trigger fired from C# with a mistyped argument crashes the game instead of throwing, so the trigger channel is typed at the bridge, never at invoke. Decide what the palette shows for a source that is down.

## Performance budget

**Priority:** medium · **Cost:** moderate

The palette opens in a frame and filters as the player types; the toolbar costs nothing when idle. Measure in the running game rather than reason from the source, and keep the measurements with the code that meets them. Reflection lookups are resolved once, at bridge load, never per invoke.

## Contextual palette

**Priority:** medium · **Cost:** moderate

Opened with an entity selected, the palette lists the actions on that object first: relocate, delete, follow, toggle active, the game's own "Selected Object" family, and what bridges add for that kind of entity. The palette then doubles as the keyboard context menu the game lacks. Settle how far the selection reorders results before the order feels unpredictable, and whether the contextual actions get a visible group header.

## Any action gets a key

**Priority:** high · **Cost:** large

Bind a key to any palette action from its row, so Do It is also the shortcuts hub CS1 had in More Shortcuts. The game's own rebinding UI cannot host them: `InputManager.AddActions` is internal and the options page builds its key rows from properties declared on a settings class. So Do It adds the actions to the input manager by reflection and ships a rebinding page of its own, with the conflict check the game's page would have done.

## Prefix scopes

**Priority:** medium · **Cost:** cheap

A leading character switches what the query searches, VS Code style: actions by default, `#` settings, `@` named things in the city, `/` prefabs. One key binding, one box. A hint row lists the prefixes while the box is empty, so a player finds them without reading anything.

## Named things as subjects

**Priority:** medium · **Cost:** large

Search named buildings, cims, lines and districts through the game's `NameSystem`; "go to" focuses the camera and selects the result, and the result can be the subject of any entity action. Indexing names live in a large city must fit the performance budget: measure an incremental index against a full scan on open before choosing.

## Prefab placement

**Priority:** medium · **Cost:** moderate

"Place <asset>" finds a prefab and activates its tool, which `ToolSystem.ActivatePrefabTool` does in one call; the label and icon come from the game's `Assets.NAME` keys and `ImageSystem`. Decide the overlap with Find It first: its `GetFindItSearchMethod` convention could feed Do It's prefab search when Find It is installed, with Do It's own index as the fallback.

## Contextual toolbars

**Priority:** low · **Cost:** moderate

A toolbar that appears with a tool or a selection, like the game's own tool options row, and toolbar presets a player switches between. Decide against the real UI how many surfaces the screen takes before it reads as cluttered.

## Main-menu palette

**Priority:** low · **Cost:** cheap

Load a save by name, open an options page, start the editor: the game's screens and `menu.*` triggers cover it, and the availability predicate already knows the main menu. Check with players whether the main menu is where they want a palette before polishing it.

## Macros

**Priority:** low · **Cost:** large

A pinned sequence of actions as one toolbar item. The hard parts are passing a subject between steps and what a failed step in the middle leaves behind; the first version runs only subject-free steps and stops at the first failure.

## Learning surface

**Priority:** low · **Cost:** moderate

Since every row shows the game's own key, the palette teaches the shortcuts. A "show me" on a row highlights where the action lives in the game's UI. Highlighting a vanilla element from a mod must survive the game's restyles, so anchor on the element's binding or action name rather than its class names.
