using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DoIt.Logging;

namespace DoIt.Actions;

/// <summary>
/// <para>
/// Keeps the use record in a text file, one use per line: the count, the last use in UTC ticks,
/// then the canonical id, tab-separated, the id last so it may hold a tab.
/// </para>
/// <para>
/// A file it cannot read or write costs the ranking its history, never the registry: a line it
/// cannot read is skipped, and each failure is logged.
/// </para>
/// </summary>
internal sealed class FileActionUseStore(string path, IModLog log) : IActionUseStore {
  // Mutable, as a failing disk is logged on the first save that fails, not on each use after it.
  private bool hasSaveFailed;

  public IReadOnlyDictionary<string, ActionUse> Load() {
    var uses = new Dictionary<string, ActionUse>();

    if (!File.Exists(path)) {
      return uses;
    }

    try {
      var skipped = 0;

      foreach (var line in File.ReadAllLines(path).Where(static line => line.Length > 0)) {
        if (FileActionUseStore.TryParse(line) is var (id, use)) {
          uses[id] = use;
        }
        else {
          skipped++;
        }
      }

      if (skipped > 0) {
        log.Warn($"{nameof(FileActionUseStore)}: Skipped {skipped} unreadable lines in {path}.");
      }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
      log.Warn(ex, $"{nameof(FileActionUseStore)}: Could not read {path}, ranking starts afresh.");
    }

    return uses;
  }

  public void Save(IReadOnlyDictionary<string, ActionUse> uses) {
    var lines = uses.Select(static pair => string.Join(
        "\t",
        pair.Value.Count.ToString(CultureInfo.InvariantCulture),
        pair.Value.LastUse.Ticks.ToString(CultureInfo.InvariantCulture),
        pair.Key
      )
    );

    try {
      // Null only for a root path, which a file under the settings folder is not.
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllLines(path, lines);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
      if (!this.hasSaveFailed) {
        this.hasSaveFailed = true;

        log.Warn(ex, $"{nameof(FileActionUseStore)}: Could not write {path}, uses are not kept.");
      }
    }
  }

  private static (string Id, ActionUse Use)? TryParse(string line) {
    var parts = line.Split(['\t'], 3);

    return parts.Length is 3 &&
      int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) &&
      long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) &&
      ticks <= DateTime.MaxValue.Ticks
        ? (parts[2], new ActionUse(count, new DateTime(ticks, DateTimeKind.Utc)))
        : null;
  }
}
