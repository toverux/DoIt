using Colossal.UI.Binding;

namespace DoIt.Utils.Writers;

internal static class JsonWriterExtensions {
  extension(IJsonWriter writer) {
    /// <summary>
    /// Writes a value or <c>null</c>: <c>Write((string) null)</c> drops the property from the JS
    /// object (read in the running game), which would change the shape cached under its type name.
    /// </summary>
    internal void WriteNullable(string? value) {
      if (value is null) {
        writer.WriteNull();
      }
      else {
        writer.Write(value);
      }
    }

    /// <inheritdoc cref="WriteNullable(IJsonWriter, string?)"/>
    internal void WriteNullable(double? value) {
      if (value is {} number) {
        writer.Write(number);
      }
      else {
        writer.WriteNull();
      }
    }
  }
}
