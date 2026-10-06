using System.Globalization;
using System.Text.Json.Nodes;

namespace GilSweep.Tests.Support;

/// <summary>
/// Compares JSON by meaning: numbers by value, a missing property the same as null, arrays in
/// order. Fails with the path of the first difference, so a characterization failure says
/// exactly which field of which row moved.
/// </summary>
internal static class JsonAssert
{
    public static void Equivalent(JsonNode? expected, JsonNode? actual, string path = "$", ISet<string>? ignore = null)
    {
        var difference = Difference(expected, actual, path, ignore ?? new HashSet<string>());
        Assert.True(difference is null, difference);
    }

    private static string? Difference(JsonNode? expected, JsonNode? actual, string path, ISet<string> ignore)
    {
        if (expected is null || actual is null)
        {
            return IsNullish(expected) && IsNullish(actual) ? null : $"{path}: expected {Show(expected)}, got {Show(actual)}";
        }

        switch (expected)
        {
            case JsonObject expectedObject when actual is JsonObject actualObject:
                foreach (var name in expectedObject.Select(pair => pair.Key).Union(actualObject.Select(pair => pair.Key)))
                {
                    if (ignore.Contains(name))
                    {
                        continue;
                    }

                    var found = Difference(expectedObject[name], actualObject[name], $"{path}.{name}", ignore);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;
            case JsonArray expectedArray when actual is JsonArray actualArray:
                if (expectedArray.Count != actualArray.Count)
                {
                    return $"{path}: expected {expectedArray.Count} entries, got {actualArray.Count}";
                }

                for (var i = 0; i < expectedArray.Count; i++)
                {
                    var found = Difference(expectedArray[i], actualArray[i], $"{path}[{i}]", ignore);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;
            case JsonValue expectedValue when actual is JsonValue actualValue:
                if (expectedValue.TryGetValue<double>(out var x) && actualValue.TryGetValue<double>(out var y))
                {
                    return x == y ? null : $"{path}: expected {x.ToString("R", CultureInfo.InvariantCulture)}, got {y.ToString("R", CultureInfo.InvariantCulture)}";
                }

                return expectedValue.ToJsonString() == actualValue.ToJsonString() ? null : $"{path}: expected {Show(expected)}, got {Show(actual)}";
            default:
                return $"{path}: expected {Show(expected)}, got {Show(actual)}";
        }
    }

    private static bool IsNullish(JsonNode? node) => node is null;

    private static string Show(JsonNode? node) => node?.ToJsonString() is { } text ? (text.Length > 200 ? text[..200] + "…" : text) : "null";
}
