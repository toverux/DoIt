using System.Collections.Generic;
using Colossal.UI.Binding;

namespace DoIt.Actions;

/// <summary>
/// The rows answering one query, tagged with its sequence number so the UI drops an answer to an
/// older query.
/// </summary>
internal sealed record ActionResults(int Sequence, IReadOnlyList<ActionRow> Rows) {
  internal static ActionResults Empty { get; } = new(0, []);
}

/// <summary>
/// One action as the UI renders it, complete: the UI holds no catalogue.
/// A hidden action never makes a row.
/// </summary>
internal sealed record ActionRow {
  internal required string Id { get; init; }

  internal required string Label { get; init; }

  internal required IReadOnlyList<MatchRange> Matches { get; init; }

  internal string? Description { get; init; }

  internal required string Category { get; init; }

  internal string? Icon { get; init; }

  /// <summary>
  /// The current binding of the action's <c>ProxyAction</c>, as the hint reader wrote it.
  /// </summary>
  internal IJsonWritable? KeyBinding { get; init; }

  /// <summary>
  /// The localized key hint of a source with no <c>ProxyAction</c>.
  /// </summary>
  internal string? KeyText { get; init; }

  internal required bool IsEnabled { get; init; }

  internal string? DisabledReason { get; init; }

  internal required ActionChannel Channel { get; init; }

  internal required ActionSubject Subject { get; init; }

  internal required bool FillsFromSelection { get; init; }
}
