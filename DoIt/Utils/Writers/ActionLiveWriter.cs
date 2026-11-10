using System.Collections.Generic;
using Colossal.UI.Binding;
using DoIt.Actions;

namespace DoIt.Utils.Writers;

/// <summary>
/// Writes the live parts of the pinned actions to the UI, every optional part written, as
/// <c>null</c> when absent, so an entry keeps one shape.
/// </summary>
internal sealed class ActionLiveWriter : IWriter<IReadOnlyList<ActionLive>> {
  public void Write(IJsonWriter writer, IReadOnlyList<ActionLive> lives) {
    writer.ArrayBegin(lives.Count);

    foreach (var live in lives) {
      writer.TypeBegin(typeof(ActionLive).FullName);
      writer.PropertyName("id");
      writer.Write(live.Id);
      writer.PropertyName("isEnabled");
      writer.Write(live.IsEnabled);
      writer.PropertyName("disabledReason");
      writer.WriteNullable(live.DisabledReason);
      writer.PropertyName("state");
      ActionStateValueWriter.Write(writer, live.State);
      writer.TypeEnd();
    }

    writer.ArrayEnd();
  }
}
