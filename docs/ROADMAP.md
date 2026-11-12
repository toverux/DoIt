# Roadmap

Planned work on the mod that no ticket holds yet.

Every entry opens with a triage line: **Priority** (high, medium, low) and **Cost** as the code reads at triage (trivial, cheap, moderate, large), either one `unsure` when nobody has judged it yet.

An entry is removed once its work is done.

## Actions that take over the palette

**Priority:** high · **Cost:** large

An action can do more than call up the vanilla panel it stands for: invoked, it replaces the results with a view of its own, inside the palette. Load Game is the first case: instead of opening the game's load panel, it lists the saves where the results were, the query filters them, and Enter loads the one picked; Escape steps back to the actions. The search bar is a dynamic environment: what the box searches, what the rows are and what Enter does belong to the action in charge. The action model was built for a bare invoke and changes wherever this needs it: no choice already made closes a door here. A free-text subject, a prefix scope and a named thing picked as a subject are the same mechanism seen from other entries. The palette's scopes, an option list for a choice and a prompt for a value, are what takeover extends. Open: Tab entering a row's scope when a row holds both an act and a scope, the footer naming both keys.

## Settings as actions

**Priority:** medium · **Cost:** large

The game's and the mods' settings are searchable like actions, with a quick inline edit where the type allows (a toggle, a slider, a dropdown) and a jump to the options page at that setting otherwise; `OptionsUISystem.OpenPage` is public. The options screen already searches every page's widgets with their localized labels, through `OptionsUISystem` and its `options.pages` binding, so Do It reuses that list rather than building a second index; the `ModSetting` registry that lists every mod's key bindings is internal, so that one read is a reflection seam to isolate.

## Picking a subject for a bare invoke

**Priority:** high · **Cost:** moderate

When an action needs a subject and is invoked bare, Do It activates a pick tool, the action runs on what the player clicks, and Escape cancels and restores the previous tool. Only where possible: an entity the player can click qualifies, a prefab opens the asset search instead, a free-text subject prompts in the palette, and so does a number, on the palette's value prompt. FindIt's picker and the game's `Selection Tool` are the models.

## Game actions as the first source

**Priority:** high · **Cost:** large

Hook into the game's own actions: the input actions of the tool system and the UI (open a panel, pick a tool, a photo mode toggle), the simulation controls, selection operations. Reach each one by a direct call where the API is public, by reflection where it is not; `DoIt/Reflection` holds the accessors. The input manager enumerates every action with its current keys, and the Shortcuts map's action names are the UI's alias names, so the catalogue starts from it. Catalogue what the game exposes before picking the first batch, and keep the catalogue in a doc an agent can grow.

The main menu and the editor have their seed actions and wait on their catalogue.

## Isolating the game's systems from each other

**Priority:** high · **Cost:** moderate

`GameActionSource` will grow into one source over many unrelated game systems, and the registry isolates failures per source: a game update that retires or changes one system throws from the source's `List()` and takes every game action down with it. Contain a failure to the system it comes from, by splitting the game source into one source per system or by isolating each system's actions inside it, so the rest of the game's actions keep working and the log names the system that broke. Settle it before the game source grows past its seed actions.

## Action outcomes and feedback

**Priority:** high · **Cost:** large

An action that takes time or fails shows the player nothing: Enter closes the palette at once, and a throw only logs and greys the row. Give an invoke an outcome and a place to show it, settled before the bridges and the registration API are written against the invoke contract, so worked right after the palette. Its spec answers: a reported failure against a fault that disables the action; a task that never ends, and cancelling it; a task ending after the screen changed; the thread a foreign task completes on; and a feedback surface that serves a pinned button, a bound key and a macro step as well as the palette. Two ideas to weigh: a toast outside the palette, running then done or failed with its reason, and a spinner on the row while the palette stays open. It opens with research on what the best palettes show after an invoke.

## Bridges to installed mods

**Priority:** high · **Cost:** large

A bridge adapts a mod that knows nothing of Do It, by reflection into its assembly, guarded so the mod's absence, another version or a thrown exception costs the player nothing but that bridge. Bring the mods in by subscriber count, from the top: `mise bridges candidates` lists the most subscribed code mods not yet judged, through the search call the Paradox Mods website makes, which answers without a login. The verdict on each mod is kept in `DoIt/Actions/Sources/Bridges/mods.json`, keyed by Paradox mod id: `bridged` with the user-facing version the bridge was written against (Paradox's revision number where the mod publishes none), `set-aside` with the reason, or `gap` with the reason and the upstream proposal; `mise bridges status` marks the bridged mods whose current version moved. In the running game, the mod manager's list gives a mod's assembly version, and its Paradox version string comes from the PDX SDK's playset info, which is what a drift check against the ledger compares.

## The registration API

**Priority:** high · **Cost:** moderate

Mods that want to register actions themselves. The first shape is a reflection snippet they paste: a few lines that find Do It's registrar by type name and call it with plain types, so a mod depends on nothing and runs unchanged when Do It is absent. The snippet is published under MIT-0. A typed `DoIt.Api` assembly under MIT may follow for the mods that prefer a compile-time contract; its versioning is then a contract of its own, independent of the mod's CalVer.

## The command palette

**Priority:** high · **Cost:** large

Opened by a key binding, `Alt+Space` the first candidate, rebindable in the settings. Keyboard driven end to end, showing what the registry's search matched (the spans of the label, a matched alias or description), built from the game's own chrome so it reads as part of the game. A row shows the action's icon where it has one and nothing in its place otherwise, and the action's current key where the game or the mod binds one. Argument collection for actions that take one. The game's shortcut consumers unregister while a modal is open, so the palette either stays out of the game's modal scope or closes before it dispatches a game action. Controller support is the open question: what the palette becomes without a keyboard.

## The toolbar

**Priority:** high · **Cost:** large

Pin any palette action to a toolbar for one-click access, from its palette row with one key, with the same availability rules, ordered and grouped by the player by drag, persisted in the settings. A pinned action without an icon shows its initials, and shows its state on its button: a toggle on or off, the current value, the active mode. The toolbar takes over the button that opens the palette. Where it docks is the next entry's.

## Docking the toolbar

**Priority:** high · **Cost:** moderate

The toolbar docks like Windows's taskbar: pinned vertically or horizontally to an edge, it stays clear of the game's own components and is tested against the other overlays it meets. A quick-settings icon on it changes its appearance and behaviors in place, with one entry opening the mod's settings.

## Arranging and styling the game's top bar

**Priority:** low · **Cost:** moderate

Let the player move the game's own top bar by a drag handle, and set its buttons' color and opacity, kept in the settings.

## Managing other mods' buttons

**Priority:** low · **Cost:** large

Other mods add their own buttons to the game's top-left bar and to the universal mods menu. Wrap each of those entries so the player can manage it: hide it, move it to the other menu, or pin it to Do It's toolbar. How Do It finds and wraps an entry another mod injected into the game's UI is to be read from the game's UI bundle and checked against mods in the corpus.

## A skill to bring support to a mod

**Priority:** medium · **Cost:** moderate

A repo-local skill an agent runs on a mod not yet supported: locate its source or decompile, find what it exposes that could be an action, write the bridge in the shape the first bridges settled, verify it in the running game with the mod installed, and record the mod version. Written once a few bridges exist by hand, so the skill captures what they have in common rather than guesses at it.

## Contextual palette

**Priority:** medium · **Cost:** moderate

Opened with an entity selected, the palette lists the actions on that object first: relocate, delete, follow, toggle active, the game's own "Selected Object" family, and what bridges add for that kind of entity. The palette then doubles as the keyboard context menu the game lacks. Settle how far the selection reorders results before the order feels unpredictable, and whether the contextual actions get a visible group header.

## Any action gets a key

**Priority:** high · **Cost:** large

Bind a key to any palette action from its row, so Do It is also the shortcuts hub CS1 had in More Shortcuts. The game's own rebinding UI cannot host them: `InputManager.AddActions` is internal and the options page builds its key rows from properties declared on a settings class. So Do It adds the actions to the input manager by reflection and ships a rebinding page of its own, with the conflict check the game's page would have done.

## Prefix scopes

**Priority:** medium · **Cost:** cheap

A leading character switches what the query searches, VS Code style: actions by default, `#` settings, `@` named things in the city, `/` prefabs. One key binding, one box. A hint row lists the prefixes while the box is empty, so a player finds them without reading anything. A prefix scope extends the scope the palette's query carries, and Tab is kept free for it or for takeover.

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

The palette opens on the main menu with its seed actions. What is left: load a save by name, which the takeover entry carries, open an options page, start the editor, and continue or exit once an action can ask for a confirmation or carry the vanilla button's checks; the game's screens and `menu.*` triggers cover it.

## Macros

**Priority:** low · **Cost:** large

A pinned sequence of actions as one toolbar item. The hard parts are passing a subject between steps and what a failed step in the middle leaves behind; the first version runs only subject-free steps and stops at the first failure.

## Learning surface

**Priority:** low · **Cost:** moderate

Since every row shows the game's own key, the palette teaches the shortcuts. A "show me" on a row highlights where the action lives in the game's UI. Highlighting a vanilla element from a mod must survive the game's restyles, so anchor on the element's binding or action name rather than its class names.

## A state with several values on at once

**Priority:** medium · **Cost:** moderate

A fifth state shape beside toggle, number, choice and text: a set of options with several on at once, shown as a scope whose rows tick, as Linear's command menu does. Until then a mod registers one toggle action per option. Open: the key that ticks a row, since Space types into the box.

## The palette on a controller

**Priority:** low · **Cost:** unsure

The palette is keyboard and mouse only and closes when a gamepad takes over. What it becomes on a controller is open, with two paths: typing through the game's virtual keyboard, which its own text field supports, or a browsable list of recent and pinned actions with no typing, which waits on the toolbar's pins.
