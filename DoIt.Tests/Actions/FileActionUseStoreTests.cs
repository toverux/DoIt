using System;
using System.Collections.Generic;
using System.IO;
using DoIt.Actions;
using DoIt.Logging;
using DoIt.Tests.Logging;
using Xunit;

namespace DoIt.Tests.Actions;

public sealed class FileActionUseStoreTests : IDisposable {
  private readonly string folder =
    Path.Combine(Path.GetTempPath(), $"{nameof(FileActionUseStoreTests)}-{Guid.NewGuid():N}");

  private readonly RecordingLog log;

  private readonly FileActionUseStore store;

  public FileActionUseStoreTests() {
    var (sink, recorder) = RecordingLog.Create();

    this.log = recorder;
    this.store = new FileActionUseStore(this.FilePath, new ModLog(sink, static key => key));
  }

  private string FilePath => Path.Combine(this.folder, "uses.txt");

  public void Dispose() {
    if (Directory.Exists(this.folder)) {
      Directory.Delete(this.folder, true);
    }
  }

  [Fact]
  public void Load_ReadsBackEverySavedUse_InAFolderItCreated() {
    var uses = new Dictionary<string, ActionUse> {
      ["game/Pause Simulation"] = new(3, new DateTime(2026, 11, 10, 9, 30, 15, DateTimeKind.Utc)),
      ["bridge:Some.Mod/a/local\tid"] = new(1, new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc))
    };

    this.store.Save(uses);

    Assert.Equal(uses, this.store.Load());
    Assert.Empty(this.log.Calls);
  }

  [Fact]
  public void Load_IsEmptyWithNoFile() {
    Assert.Empty(this.store.Load());
    Assert.Empty(this.log.Calls);
  }

  [Fact]
  public void Load_SkipsTheLinesItCannotRead_AndWarnsOnce() {
    Directory.CreateDirectory(this.folder);

    File.WriteAllText(
      this.FilePath,
      "2\t638981234567890000\tgame/Bulldoze Tool\nnot a use\n1\tlater\tgame/Economy\n"
    );

    var use = Assert.Single(this.store.Load());

    Assert.Equal("game/Bulldoze Tool", use.Key);
    Assert.Equal(2, use.Value.Count);
    Assert.Single(this.log.Calls, static call => call.Method is "Warn");
  }
}
