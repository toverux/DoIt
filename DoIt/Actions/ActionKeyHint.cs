using Game.UI.Localization;

namespace DoIt.Actions;

/// <summary>
/// The key a row shows for its action.
/// </summary>
internal abstract record ActionKeyHint;

/// <summary>
/// The current keys of a game or mod <c>ProxyAction</c>, read when the row is written, so a rebind
/// shows without a restart.
/// </summary>
internal sealed record ProxyActionKeyHint(string Map, string Name) : ActionKeyHint;

/// <summary>
/// A localized hint, for a source with no <c>ProxyAction</c> to name.
/// </summary>
internal sealed record TextKeyHint(LocalizedString Text) : ActionKeyHint;
