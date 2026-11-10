using System.Collections.Generic;
using System.Linq;

namespace DoIt.Utils;

/// <summary>
/// Compares two lists item by item, so a binding over a list rebuilt with the same items is left
/// unwritten.
/// </summary>
internal sealed class SequenceEqualityComparer<T> : EqualityComparer<IReadOnlyList<T>> {
  public override bool Equals(IReadOnlyList<T>? x, IReadOnlyList<T>? y) =>
    ReferenceEquals(x, y) || (x is not null && y is not null && x.SequenceEqual(y));

  public override int GetHashCode(IReadOnlyList<T> list) => list.Count;
}
