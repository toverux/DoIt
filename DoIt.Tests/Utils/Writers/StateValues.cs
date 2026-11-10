using System.Collections.Generic;
using DoIt.Actions;

namespace DoIt.Tests.Utils.Writers;

/// <summary>
/// A value of every state shape, with and without a setter, every optional part present then
/// absent, for the writers' shape tests.
/// </summary>
internal static class StateValues {
  internal static IReadOnlyList<ActionStateValue> All { get; } = [
    new ToggleValue(true) {
      IsSettable = true
    },
    new ToggleValue(false) {
      IsSettable = false
    },
    new NumberValue(0.5, 0, 1, 0.1) {
      IsSettable = true
    },
    new NumberValue(3, null, null, null) {
      IsSettable = false
    },
    new ChoiceValue([new ChoiceOption("1", "Slow"), new ChoiceOption("2", "Fast")], "2") {
      IsSettable = true
    },
    new ChoiceValue([], "") {
      IsSettable = false
    },
    new TextValue("Lyon") {
      IsSettable = true
    },
    new TextValue("") {
      IsSettable = false
    }
  ];

  internal static IReadOnlyList<string> TypeNames { get; } = [
    "DoIt.Actions.ToggleValue",
    "DoIt.Actions.NumberValue",
    "DoIt.Actions.ChoiceValue",
    "DoIt.Actions.ChoiceOption",
    "DoIt.Actions.TextValue"
  ];
}
