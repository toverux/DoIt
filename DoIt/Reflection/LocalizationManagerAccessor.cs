using System.Reflection;
using Colossal.Localization;

namespace DoIt.Reflection;

/// <summary>
/// Reaches the localization manager's fallback dictionary, the source locale's, which the game
/// keeps loaded whatever locale is active but does not expose.
/// The manager creates it once and only refills it, so a reference read once stays current.
/// </summary>
internal static class LocalizationManagerAccessor {
  private static readonly FieldInfo? FallbackDictionaryField = typeof(LocalizationManager).GetField(
    "m_FallbackDictionary",
    BindingFlags.NonPublic | BindingFlags.Instance
  );

  /// <summary>
  /// The manager's fallback dictionary, or <c>null</c> when a game update moved it.
  /// </summary>
  internal static LocalizationDictionary? FallbackDictionary(LocalizationManager manager) =>
    LocalizationManagerAccessor.FallbackDictionaryField?.GetValue(manager) as
      LocalizationDictionary;
}
