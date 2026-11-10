using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.UI.Localization;

namespace DoIt.Actions;

/// <summary>
/// The actions one mod registers, from plain strings, ints, and delegates, so the mod depends on
/// nothing of Do It's.
/// Its id segment is <c>api:&lt;registrant name&gt;</c>, and a name holding a <c>/</c> is refused.
/// </summary>
internal sealed class RegistrationActionSource : IActionSource {
  private readonly Dictionary<string, ActionDescriptor> descriptors = [];

  /// <param name="registrantName">The registrant's assembly name.</param>
  /// <param name="registrantVersion">The registrant's assembly version, for the log.</param>
  internal RegistrationActionSource(string registrantName, string registrantVersion) {
    this.Info = new ActionSourceInfo(
      $"api:{registrantName}",
      LocalizedString.Value(registrantName),
      registrantName,
      registrantVersion
    );
  }

  public event Action? Changed;

  public ActionSourceInfo Info { get; }

  public IEnumerable<ActionDescriptor> List() => this.descriptors.Values.ToArray();

  /// <summary>
  /// Adds an action, or replaces the one registered under the same key.
  /// A key the action id refuses throws here, rather than take the whole source down on listing.
  /// </summary>
  internal void Register(ActionRegistration registration) {
    _ = new ActionId(this.Info.Segment, registration.Key);

    this.descriptors[registration.Key] = new ActionDescriptor {
      LocalId = registration.Key,
      Label = registration.LabelFallback is {} fallback
        ? LocalizedString.IdWithFallback(registration.LabelId, fallback)
        : LocalizedString.Id(registration.LabelId),
      Icon = registration.Icon,
      // Typed, as LocalizedString converts implicitly from a string, a null one included.
      Category = registration.CategoryId is {} category
        ? LocalizedString.Id(category)
        : (LocalizedString?) null,
      Modes = (GameMode) registration.Modes,
      Channel = ActionChannel.CSharpCall(registration.Invoke)
    };

    this.Changed?.Invoke();
  }

  internal void Unregister(string key) {
    if (this.descriptors.Remove(key)) {
      this.Changed?.Invoke();
    }
  }
}

/// <summary>
/// An action as a mod registers it, in plain types.
/// </summary>
internal sealed record ActionRegistration {
  /// <summary>
  /// The registrant's own stable key for the action.
  /// </summary>
  internal required string Key { get; init; }

  /// <summary>
  /// The locale id of the label, resolved through the game's dictionary.
  /// </summary>
  internal required string LabelId { get; init; }

  /// <summary>
  /// The label shown when the dictionary lacks <see cref="LabelId"/>.
  /// </summary>
  internal string? LabelFallback { get; init; }

  internal string? Icon { get; init; }

  /// <summary>
  /// The locale id of the category; the registrant's name when none.
  /// </summary>
  internal string? CategoryId { get; init; }

  /// <summary>
  /// The game's <see cref="GameMode"/> flags the action exists in, as an int.
  /// </summary>
  internal int Modes { get; init; } = (int) GameMode.Game;

  internal required Action Invoke { get; init; }
}
