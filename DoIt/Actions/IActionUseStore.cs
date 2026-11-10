using System;
using System.Collections.Generic;

namespace DoIt.Actions;

/// <summary>
/// Persists how often and when each action was used, keyed by its canonical id.
/// </summary>
internal interface IActionUseStore {
  IReadOnlyDictionary<string, ActionUse> Load();

  void Save(IReadOnlyDictionary<string, ActionUse> uses);
}

internal readonly record struct ActionUse(int Count, DateTime LastUse);
