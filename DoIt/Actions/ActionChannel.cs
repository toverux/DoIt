using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

namespace DoIt.Actions;

/// <summary>
/// Which side fires an action, and how.
/// A channel carries its fixed arguments: "speed 2" is an action whose channel carries the value 2,
/// not an action taking a number.
/// </summary>
internal abstract record ActionChannel {
  /// <summary>
  /// A C# call, run by the registry when the UI sends the invoke trigger.
  /// </summary>
  internal static CSharpCallChannel CSharpCall(Action invoke) => new(_ => invoke());

  /// <inheritdoc cref="CSharpCall(System.Action)"/>
  internal static CSharpCallChannel CSharpCall(Action<Entity> invoke) => new(invoke);
}

/// <summary>
/// A C# call, run in C#.
/// It receives the action's subject, <see cref="Entity.Null"/> for an action taking none.
/// The delegate never fires a cohtml trigger with a non-primitive argument: from C#, that path
/// crashes the game.
/// </summary>
internal sealed record CSharpCallChannel(Action<Entity> Invoke) : ActionChannel;

/// <summary>
/// A cohtml trigger, always fired from the UI side with the game's <c>trigger(group, name,
/// ...args)</c>, its fixed arguments first, then the subject for an action taking one.
/// The arguments are written against a version of the trigger's owner: Do It does not check them
/// against its handler.
/// </summary>
internal sealed record TriggerChannel : ActionChannel {
  internal TriggerChannel(string group, string name, IReadOnlyList<object> args) {
    if (args.FirstOrDefault(static arg => arg is not (bool or int or float or double or string))
      is {} unwritable) {
      throw new ArgumentException(
        $"A trigger argument is a bool, a number or a string, got {unwritable.GetType()}.",
        nameof(args)
      );
    }

    this.Group = group;
    this.Name = name;
    this.Args = args;
  }

  internal string Group { get; }

  internal string Name { get; }

  internal IReadOnlyList<object> Args { get; }
}

/// <summary>
/// One of the game's named UI input actions (<c>Pause Simulation</c>), dispatched from the UI side
/// on the game's root input stack.
/// </summary>
internal sealed record InputActionChannel(string Name) : ActionChannel;
