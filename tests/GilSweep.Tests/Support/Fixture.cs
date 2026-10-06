using System.Text.Json;
using System.Text.Json.Nodes;

namespace GilSweep.Tests.Support;

/// <summary>Recorded API responses and v1 outputs under Fixtures/ (see Fixtures/README.md).</summary>
internal static class Fixture
{
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    public static string Text(params string[] parts) => File.ReadAllText(PathOf(parts));

    public static JsonNode Json(params string[] parts) => JsonNode.Parse(Text(parts)) ?? throw new InvalidOperationException("Empty fixture");

    public static JsonDocument Document(params string[] parts) => JsonDocument.Parse(Text(parts));
}
