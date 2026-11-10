using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DoIt.Actions;
using DoIt.Systems;
using Game.Simulation;
using Game.UI.Localization;
using Unity.Entities;

namespace DoIt.Scenarios;

/// <summary>
/// Debug entry points timing the registry in the running game over a synthetic catalogue of the
/// size the bridges will bring, its readers reading the simulation as the seed source's do: the
/// costs that fix the tick rate and the row cap.
/// </summary>
internal static class ActionsMeasurement {
  // Added to the registry on the first seed, then emptied and filled again.
  private static SyntheticSource? source;

  /// <summary>
  /// Puts <paramref name="actionCount"/> synthetic actions in the catalogue, until
  /// <see cref="Clear"/>, for a measurement taken from the UI side.
  /// </summary>
  internal static void Seed(int actionCount = 500) {
    var world = World.DefaultGameObjectInjectionWorld;

    if (ActionsMeasurement.source is null) {
      ActionsMeasurement.source = new SyntheticSource();
      world.GetExistingSystemManaged<ActionsUISystem>()
        .Registry
        .AddSource(ActionsMeasurement.source);
    }

    ActionsMeasurement.source.Fill(actionCount, world.GetExistingSystemManaged<SimulationSystem>());
  }

  internal static void Clear() => ActionsMeasurement.source?.Clear();

  /// <summary>
  /// Times a rebuild, a search, a search the typo pass runs over in full, a re-read of the result
  /// rows, and a read of <paramref name="pinCount"/> pins, over <paramref name="actionCount"/>
  /// synthetic actions beside the real ones, and returns the mean and the worst of each in
  /// microseconds.
  /// The synthetic actions leave the catalogue when it ends.
  /// </summary>
  internal static string Run(int actionCount = 500, int pinCount = 10, int runs = 200) {
    var registry = World.DefaultGameObjectInjectionWorld
      .GetExistingSystemManaged<ActionsUISystem>()
      .Registry;

    ActionsMeasurement.Seed(actionCount);

    try {
      var results = registry.Search("", 0);
      var pins = Enumerable.Range(0, pinCount).Select(static i => $"measure/{i}").ToList();
      string[] queries = ["a", "ac", "act", "action 4", "zz"];

      // The typo pass's worst case: a long term that hits nothing, each character differing from
      // the next, so every one of its variants is tried against every field of every action.
      const string miss = "zqxjvkwzqxjvkwzqxjvk";

      return string.Join(
        "\n",
        $"{actionCount} synthetic actions, {results.Rows.Count} rows, {pinCount} pins, {runs} runs",
        ActionsMeasurement.Time("rebuild", runs, registry.Rebuild),
        ActionsMeasurement.Time(
          "search",
          runs,
          () => {
            foreach (var query in queries) {
              registry.Search(query, 0);
            }
          },
          queries.Length
        ),
        ActionsMeasurement.Time("typo pass", runs, () => registry.Search(miss, 0)),
        ActionsMeasurement.Time("re-read rows", runs, () => registry.Refresh(results)),
        ActionsMeasurement.Time("read pins", runs, () => registry.ReadLive(pins))
      );
    }
    finally {
      ActionsMeasurement.Clear();
    }
  }

  private static string Time(string name, int runs, Action run, int callsPerRun = 1) {
    var times = new List<double>(runs);

    // One run outside the count, so a first call's JIT does not weigh on the mean.
    run();

    for (var i = 0; i < runs; i++) {
      var watch = Stopwatch.StartNew();

      run();

      times.Add(watch.Elapsed.TotalMilliseconds * 1000 / callsPerRun);
    }

    return string.Format(
      CultureInfo.InvariantCulture,
      "{0}: mean {1:F1} µs, worst {2:F1} µs",
      name,
      times.Average(),
      times.Max()
    );
  }

  /// <summary>
  /// Actions labelled "Action N", every other one holding a toggle reading the simulation, every
  /// fifth one reading its availability from it.
  /// </summary>
  private sealed class SyntheticSource : IActionSource {
    private ActionDescriptor[] descriptors = [];

    public event Action? Changed;

    public ActionSourceInfo Info { get; } = new(
      "measure",
      LocalizedString.Value("Measurement"),
      "Do It",
      "measurement"
    );

    public IEnumerable<ActionDescriptor> List() => this.descriptors;

    internal void Fill(int count, SimulationSystem simulationSystem) {
      this.descriptors = Enumerable.Range(0, count)
        .Select(i => new ActionDescriptor {
            LocalId = i.ToString(CultureInfo.InvariantCulture),
            Label = LocalizedString.Value($"Action {i}"),
            Availability = i % 5 is 0
              ? () => simulationSystem.selectedSpeed > 0f
                ? ActionAvailability.Enabled
                : ActionAvailability.Disabled()
              : null,
            Channel = ActionChannel.CSharpCall(static () => {}),
            State = i % 2 is 0
              ? new ToggleState {
                Read = () => simulationSystem.selectedSpeed is 0f
              }
              : null
          }
        )
        .ToArray();

      this.Changed?.Invoke();
    }

    internal void Clear() {
      this.descriptors = [];
      this.Changed?.Invoke();
    }
  }
}
