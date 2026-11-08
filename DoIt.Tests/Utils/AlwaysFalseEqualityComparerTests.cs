using DoIt.Utils;
using Xunit;

namespace DoIt.Tests.Utils;

public sealed class AlwaysFalseEqualityComparerTests {
  [Fact]
  public void Equals_IsFalseForAnObjectAndItself() {
    var value = new object();

    Assert.False(new AlwaysFalseEqualityComparer<object>().Equals(value, value));
  }

  [Fact]
  public void Equals_IsFalseForEqualValues() =>
    Assert.False(new AlwaysFalseEqualityComparer<int>().Equals(1, 1));
}
