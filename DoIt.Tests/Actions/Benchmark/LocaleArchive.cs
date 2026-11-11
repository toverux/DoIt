using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DoIt.Tests.Actions.Benchmark;

/// <summary>
/// Reads the game's locale archive, a ZIP of one binary <c>.loc</c> file per locale, which the
/// game's own reader cannot open off-engine.
/// </summary>
internal static class LocaleArchive {
  private const string Extension = ".loc";

  /// <summary>
  /// Every locale of the archive at <paramref name="path"/>, by locale id.
  /// </summary>
  internal static Dictionary<string, GameLocale> Read(string path) {
    using var archive = ZipFile.OpenRead(path);

    var locales = new Dictionary<string, GameLocale>();

    foreach (var entry in archive.Entries) {
      if (!entry.Name.EndsWith(LocaleArchive.Extension, StringComparison.Ordinal)) {
        continue;
      }

      using var stream = entry.Open();

      var locale = LocaleArchive.ReadLocale(stream);

      locales.Add(locale.Id, locale);
    }

    return locales;
  }

  /// <summary>
  /// A <c>.loc</c> file as the game writes it with a <see cref="BinaryWriter"/>: a format version,
  /// the system language, the locale id, and the locale's own name, then a count and that many key
  /// and value pairs.
  /// The index section after them, and the trailing bytes the game never reads, are left unread.
  /// </summary>
  private static GameLocale ReadLocale(Stream stream) {
    using var reader = new BinaryReader(stream, Encoding.UTF8);

    reader.ReadUInt16();
    reader.ReadString();

    var id = reader.ReadString();

    reader.ReadString();

    var count = reader.ReadInt32();
    var entries = new Dictionary<string, string>(count);

    for (var i = 0; i < count; i++) {
      var key = reader.ReadString();

      entries[key] = reader.ReadString();
    }

    return new GameLocale(id, count, entries);
  }
}

/// <summary>
/// One locale's dictionary, with the entry count its file declares, so a key written twice shows.
/// </summary>
internal sealed record GameLocale(
  string Id,
  int DeclaredCount,
  IReadOnlyDictionary<string, string> Entries
);
