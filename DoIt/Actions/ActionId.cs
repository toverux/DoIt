using System;

namespace DoIt.Actions;

/// <summary>
/// The identity of an action: the source it came from (<c>game</c>, <c>bridge:&lt;assembly&gt;</c>,
/// <c>api:&lt;assembly&gt;</c>) and a local key that source keeps stable.
/// Its canonical string, source then <c>/</c> then local, is the only form that leaves C#: on the
/// wire, in the settings, and in the log.
/// A source segment never holds a <c>/</c>, so the string splits at its first one and a local key
/// may hold more.
/// </summary>
internal readonly record struct ActionId {
  internal ActionId(string source, string local) {
    if (source.Length is 0 || source.IndexOf('/') >= 0) {
      throw new ArgumentException(
        $"A source segment is non-empty and holds no \"/\", got \"{source}\".",
        nameof(source)
      );
    }

    if (local.Length is 0) {
      throw new ArgumentException("A local segment is non-empty.", nameof(local));
    }

    this.Source = source;
    this.Local = local;
  }

  internal string Source { get; }

  internal string Local { get; }

  internal static ActionId Parse(string text) =>
    ActionId.TryParse(text, out var id)
      ? id
      : throw new FormatException($"\"{text}\" is not a canonical action id.");

  internal static bool TryParse(string text, out ActionId id) {
    var slash = text.IndexOf('/');

    if (slash <= 0 || slash == text.Length - 1) {
      id = default;

      return false;
    }

    id = new ActionId(text[..slash], text[(slash + 1)..]);

    return true;
  }

  public override string ToString() => $"{this.Source}/{this.Local}";
}
