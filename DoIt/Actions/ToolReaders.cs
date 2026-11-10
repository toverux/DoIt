using System;
using Game.Tools;
using Game.UI.Localization;
using Unity.Entities;

namespace DoIt.Actions;

/// <summary>
/// Ready-made availability readers for actions that only make sense under a tool, which a bridge
/// passes as its own.
/// Each resolves the systems it reads once, when built.
/// </summary>
internal static class ToolReaders {
  internal static readonly LocalizedString NoToolReason =
    LocalizedString.Id("DoIt.Actions.REASON_NO_TOOL");

  internal static readonly LocalizedString ToolInactiveReason =
    LocalizedString.Id("DoIt.Actions.REASON_TOOL_INACTIVE");

  /// <summary>
  /// Enabled while a tool other than the default one is active.
  /// </summary>
  internal static Func<ActionAvailability> AnyToolActive(World world) {
    var toolSystem = world.GetOrCreateSystemManaged<ToolSystem>();
    var defaultTool = world.GetOrCreateSystemManaged<DefaultToolSystem>();

    return () => toolSystem.activeTool != defaultTool
      ? ActionAvailability.Enabled
      : ActionAvailability.Disabled(ToolReaders.NoToolReason);
  }

  /// <summary>
  /// Enabled while a tool of type <typeparamref name="T"/> is active.
  /// </summary>
  internal static Func<ActionAvailability> ToolActive<T>(World world) where T : ToolBaseSystem {
    var toolSystem = world.GetOrCreateSystemManaged<ToolSystem>();

    return () => toolSystem.activeTool is T
      ? ActionAvailability.Enabled
      : ActionAvailability.Disabled(ToolReaders.ToolInactiveReason);
  }
}
