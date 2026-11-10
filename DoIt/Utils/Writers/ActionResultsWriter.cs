using System;
using Colossal.UI.Binding;
using DoIt.Actions;

namespace DoIt.Utils.Writers;

/// <summary>
/// Writes the rows of a search to the UI.
/// Every optional part is written, as <c>null</c> when absent, so a row keeps one shape; each kind
/// of channel and of state writes under its own type name, with its own fixed shape.
/// </summary>
internal sealed class ActionResultsWriter : IWriter<ActionResults> {
  public void Write(IJsonWriter writer, ActionResults results) {
    writer.TypeBegin(typeof(ActionResults).FullName);
    writer.PropertyName("sequence");
    writer.Write(results.Sequence);
    writer.PropertyName("rows");
    writer.ArrayBegin(results.Rows.Count);

    foreach (var row in results.Rows) {
      ActionResultsWriter.WriteRow(writer, row);
    }

    writer.ArrayEnd();
    writer.TypeEnd();
  }

  private static void WriteRow(IJsonWriter writer, ActionRow row) {
    writer.TypeBegin(typeof(ActionRow).FullName);
    writer.PropertyName("id");
    writer.Write(row.Id);
    writer.PropertyName("label");
    writer.Write(row.Label);
    writer.PropertyName("matches");
    writer.ArrayBegin(row.Matches.Count);

    foreach (var match in row.Matches) {
      writer.TypeBegin(typeof(MatchRange).FullName);
      writer.PropertyName("start");
      writer.Write(match.Start);
      writer.PropertyName("length");
      writer.Write(match.Length);
      writer.TypeEnd();
    }

    writer.ArrayEnd();
    writer.PropertyName("description");
    writer.WriteNullable(row.Description);
    writer.PropertyName("category");
    writer.Write(row.Category);
    writer.PropertyName("icon");
    writer.WriteNullable(row.Icon);
    writer.PropertyName("keyBinding");

    if (row.KeyBinding is {} keyBinding) {
      keyBinding.Write(writer);
    }
    else {
      writer.WriteNull();
    }

    writer.PropertyName("keyText");
    writer.WriteNullable(row.KeyText);
    writer.PropertyName("isEnabled");
    writer.Write(row.IsEnabled);
    writer.PropertyName("disabledReason");
    writer.WriteNullable(row.DisabledReason);
    writer.PropertyName("channel");
    ActionResultsWriter.WriteChannel(writer, row.Channel);
    writer.PropertyName("state");
    ActionStateValueWriter.Write(writer, row.State);
    writer.PropertyName("subject");
    writer.Write(row.Subject is ActionSubject.Entity ? "entity" : "none");
    writer.PropertyName("fillsFromSelection");
    writer.Write(row.FillsFromSelection);
    writer.TypeEnd();
  }

  private static void WriteChannel(IJsonWriter writer, ActionChannel? channel) {
    if (channel is null) {
      writer.WriteNull();

      return;
    }

    writer.TypeBegin(channel.GetType().FullName);
    writer.PropertyName("kind");

    switch (channel) {
      case CSharpCallChannel: writer.Write("csharpCall"); break;
      case TriggerChannel trigger:
        writer.Write("trigger");
        writer.PropertyName("group");
        writer.Write(trigger.Group);
        writer.PropertyName("name");
        writer.Write(trigger.Name);
        writer.PropertyName("args");
        writer.ArrayBegin(trigger.Args.Count);

        foreach (var arg in trigger.Args) {
          ActionResultsWriter.WriteArg(writer, arg);
        }

        writer.ArrayEnd();

        break;
      case InputActionChannel inputAction:
        writer.Write("inputAction");
        writer.PropertyName("name");
        writer.Write(inputAction.Name);

        break;
      default: throw new InvalidOperationException($"No wire form for {channel.GetType()}.");
    }

    writer.TypeEnd();
  }

  /// <summary>
  /// Writes one of the primitives <see cref="TriggerChannel"/> accepts.
  /// </summary>
  private static void WriteArg(IJsonWriter writer, object arg) {
    switch (arg) {
      case bool value: writer.Write(value); break;
      case int value: writer.Write(value); break;
      case float value: writer.Write(value); break;
      case double value: writer.Write(value); break;
      case string value: writer.Write(value); break;
      default: throw new InvalidOperationException($"No wire form for a {arg.GetType()} argument.");
    }
  }
}
