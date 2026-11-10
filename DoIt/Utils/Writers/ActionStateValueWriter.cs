using System;
using Colossal.UI.Binding;
using DoIt.Actions;

namespace DoIt.Utils.Writers;

/// <summary>
/// Writes a state as read for the UI, or <c>null</c> for an action with none, shared by the rows
/// and the pinned actions.
/// Each shape writes under its own type name, with its own fixed shape: an absent bound or step is
/// written as <c>null</c>.
/// </summary>
internal static class ActionStateValueWriter {
  internal static void Write(IJsonWriter writer, ActionStateValue? state) {
    if (state is null) {
      writer.WriteNull();

      return;
    }

    writer.TypeBegin(state.GetType().FullName);
    writer.PropertyName("kind");

    switch (state) {
      case ToggleValue toggle:
        writer.Write("toggle");
        writer.PropertyName("value");
        writer.Write(toggle.Value);

        break;
      case NumberValue number:
        writer.Write("number");
        writer.PropertyName("value");
        writer.Write(number.Value);
        writer.PropertyName("min");
        writer.WriteNullable(number.Min);
        writer.PropertyName("max");
        writer.WriteNullable(number.Max);
        writer.PropertyName("step");
        writer.WriteNullable(number.Step);

        break;
      case ChoiceValue choice:
        writer.Write("choice");
        writer.PropertyName("options");
        writer.ArrayBegin(choice.Options.Count);

        foreach (var option in choice.Options) {
          writer.TypeBegin(typeof(ChoiceOption).FullName);
          writer.PropertyName("key");
          writer.Write(option.Key);
          writer.PropertyName("label");
          writer.Write(option.Label);
          writer.TypeEnd();
        }

        writer.ArrayEnd();
        writer.PropertyName("value");
        writer.Write(choice.Key);

        break;
      case TextValue text:
        writer.Write("text");
        writer.PropertyName("value");
        writer.Write(text.Value);

        break;
      default: throw new InvalidOperationException($"No wire form for {state.GetType()}.");
    }

    writer.PropertyName("isSettable");
    writer.Write(state.IsSettable);
    writer.TypeEnd();
  }
}
