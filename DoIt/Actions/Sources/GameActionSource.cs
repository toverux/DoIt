using System;
using System.Collections.Generic;
using Game.Tools;
using Game.UI.InGame;
using Game.UI.Localization;
using Unity.Entities;
using Version = Game.Version;

namespace DoIt.Actions.Sources;

/// <summary>
/// The game's own actions, reached through the public members of its systems, resolved on the first
/// listing: a game update breaking them then marks the source down instead of failing the mod.
/// </summary>
internal sealed class GameActionSource(World world) : IActionSource {
  // Mutable, as the actions are resolved on the first listing that succeeds, then kept.
  private ActionDescriptor[]? descriptors;

  // The game's actions do not change while it runs.
  public event Action? Changed { add {} remove {} }

  public ActionSourceInfo Info { get; } = new(
    "game",
    LocalizedString.Id("DoIt.Actions.SOURCE_GAME"),
    "Cities: Skylines II",
    Version.current.shortVersion
  );

  public IEnumerable<ActionDescriptor> List() =>
    this.descriptors ??= GameActionSource.Resolve(world);

  private static ActionDescriptor[] Resolve(World world) {
    var toolUISystem = world.GetOrCreateSystemManaged<ToolUISystem>();
    var bulldozeTool = world.GetOrCreateSystemManaged<BulldozeToolSystem>();

    return [
      new ActionDescriptor {
        // The tool's id, which the game's own tool trigger names it by.
        LocalId = bulldozeTool.toolID,
        Label = LocalizedString.Id($"Assets.NAME[{bulldozeTool.toolID}]"),
        Description = LocalizedString.Id($"Assets.DESCRIPTION[{bulldozeTool.toolID}]"),
        Icon = "Media/Game/Icons/Bulldozer.svg",
        KeyHint = new ProxyActionKeyHint("Shortcuts", "Bulldozer"),
        // The game's own tool button goes through this member, which also sets the bulldoze prefab
        // the tool needs.
        Channel = ActionChannel.CSharpCall(() => toolUISystem.SelectTool(bulldozeTool))
      }
    ];
  }
}
