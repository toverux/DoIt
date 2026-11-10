using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using DoIt.Actions;
using DoIt.Logging;
using DoIt.Tests.Logging;
using Game;
using Game.UI.Localization;
using Unity.Entities;

namespace DoIt.Tests.Actions;

/// <summary>
/// An <see cref="ActionRegistry"/> over settable fakes of every collaborator: a recording log, a
/// text resolver answering a string's value, then its id, unless <see cref="Translations"/> holds
/// it, a fixed key binding, a settable selection and game mode, a fixed clock, and an in-memory use
/// store.
/// </summary>
internal sealed class RegistryFixture {
  internal static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

  internal RegistryFixture(IActionMatcher? matcher = null) {
    var (log, recorder) = RecordingLog.Create();

    this.Log = recorder;

    var services = new ActionRegistryServices {
      Log = new ModLog(log, static key => key),
      ResolveText = this.Resolve,
      ReadKeyBinding = (map, name) => this.KeyBinding is null
        ? null
        : this.KeyBinding with {
          Path = $"{map}/{name}"
        },
      ReadSelection = () => this.Selection,
      ReadGameMode = () => this.GameMode,
      Now = static () => RegistryFixture.Now,
      UseStore = this.UseStore,
      Matcher = matcher ?? new SubstringActionMatcher()
    };

    this.Registry = new ActionRegistry(services);
  }

  internal ActionRegistry Registry { get; }

  internal RecordingLog Log { get; }

  internal Dictionary<string, string> Translations { get; } = [];

  internal FakeKeyBinding? KeyBinding { get; set; } = new("");

  // Mutable, as a test selects an entity or changes the mode between two registry calls.
  internal Entity Selection { get; set; } = Entity.Null;

  internal GameMode GameMode { get; set; } = GameMode.Game;

  internal MemoryUseStore UseStore { get; } = new();

  private string Resolve(LocalizedString text) =>
    text.id is {} id && this.Translations.TryGetValue(id, out var translation)
      ? translation
      : text.value ?? text.id ?? "";
}

/// <summary>
/// The binding the fixture's key hint reader returns, naming the action it was read for.
/// </summary>
internal sealed record FakeKeyBinding(string Path) : IJsonWritable {
  public void Write(IJsonWriter writer) {
    writer.TypeBegin(nameof(FakeKeyBinding));
    writer.PropertyName("path");
    writer.Write(this.Path);
    writer.TypeEnd();
  }
}

internal sealed class MemoryUseStore : IActionUseStore {
  internal Dictionary<string, ActionUse> Saved { get; private set; } = [];

  public IReadOnlyDictionary<string, ActionUse> Load() => this.Saved;

  public void Save(IReadOnlyDictionary<string, ActionUse> uses) =>
    this.Saved = new Dictionary<string, ActionUse>(uses);
}

/// <summary>
/// A source listing the descriptors a test hands it, or throwing when <see cref="ListThrows"/>.
/// </summary>
internal sealed class FakeSource(string segment = "test") : IActionSource {
  public event Action? Changed;

  public ActionSourceInfo Info { get; } = new(
    segment,
    LocalizedString.Id($"Source[{segment}]"),
    "Fake Mod",
    "1.2.3.4"
  );

  internal List<ActionDescriptor> Descriptors { get; } = [];

  // Mutable, as a test breaks a source between two rebuilds.
  internal bool ListThrows { get; set; }

  internal int ListCalls { get; private set; }

  public IEnumerable<ActionDescriptor> List() {
    this.ListCalls++;

    return this.ListThrows
      ? throw new InvalidOperationException("The fake source failed listing.")
      : this.Descriptors.ToArray();
  }

  internal void RaiseChanged() => this.Changed?.Invoke();
}

internal static class Descriptors {
  /// <summary>
  /// A C# call action labelled <paramref name="label"/>, keyed by its label.
  /// </summary>
  internal static ActionDescriptor Call(string label, Action? invoke = null) =>
    new() {
      LocalId = label,
      Label = LocalizedString.Id(label),
      Channel = ActionChannel.CSharpCall(invoke ?? (static () => {}))
    };
}
