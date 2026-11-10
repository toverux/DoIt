using Colossal.IO.AssetDatabase;
using Colossal.UI.Binding;
using DoIt.Utils;
using Game.Modding;
using Game.Settings;
using JetBrains.Annotations;

namespace DoIt;

[FileLocation($"{Settings.Folder}/{nameof(DoIt)}")]
[SettingsUIGroupOrder(Settings.GroupAdvanced)]
[SettingsUIShowGroupName(Settings.GroupAdvanced)]
public sealed class Settings : ModSetting, IJsonWritable {
  /// <summary>
  /// The mod's folder under the game's user data, which holds the settings file and the mod's own
  /// data beside it.
  /// </summary>
  internal const string Folder = $"ModsSettings/{nameof(DoIt)}";

  private const string GroupAdvanced = "Advanced";

  public Settings(IMod mod) : base(mod) {
    this.SetDefaults();
  }

  [SettingsUIButton]
  [SettingsUIConfirmation]
  [SettingsUISection(Settings.GroupAdvanced)]
  [UsedImplicitly]
  public bool ResetSettings {
    // ReSharper disable once ValueParameterNotUsed
    set {
      this.SetDefaults();
      // SetDefaults leaves key bindings alone: only this restores them to their attribute defaults
      // (decompile, ModSetting.ResetKeyBindings).
      this.ResetKeyBindings();
      this.ApplyAndSave();
    }
  }

  /// <summary>
  /// The canonical ids of the actions pinned to the toolbar, in order, which the toolbar's live
  /// values are read for.
  /// </summary>
  [SettingsUIHidden]
  public string[] PinnedActions {
    get;
    // The settings file can hold an explicit null, which the game's loader applies verbatim.
    set => field = value ?? [];
  } = null!;

  /// <summary>
  /// Applies default values to every setting.
  /// Do NOT initialize the properties inline instead, as this method is also the reset path of
  /// <see cref="ResetSettings"/>.
  /// </summary>
  public override void SetDefaults() {
    this.PinnedActions = [];
  }

  /// <summary>
  /// Restores the default of every string setting that came back null from the settings file.
  /// Call it right after loading, passing an instance that has only been through
  /// <see cref="SetDefaults"/>; <see cref="NullStrings.Restore{T}"/> tells why.
  /// </summary>
  internal void RestoreNullStrings(Settings defaults) => NullStrings.Restore(this, defaults);

  internal Settings Clone() => (Settings) this.MemberwiseClone();

  /// <summary>
  /// The settings the UI reads, through the common UI system's settings binding: a curated subset,
  /// each setting the UI needs written as a camelCase property between the type markers.
  /// </summary>
  void IJsonWritable.Write(IJsonWriter writer) {
    writer.TypeBegin(this.GetType().FullName);
    writer.TypeEnd();
  }
}
