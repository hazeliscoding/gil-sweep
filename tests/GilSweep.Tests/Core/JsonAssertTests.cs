using System.Text.Json.Nodes;
using GilSweep.Tests.Support;
using Xunit.Sdk;

namespace GilSweep.Tests.Core;

/// <summary>The characterization comparer must actually catch differences, or the v1 tests prove nothing.</summary>
public sealed class JsonAssertTests
{
    [Fact]
    public void A_changed_number_fails_with_its_path() =>
        Assert.Contains("$.rows[1].avg", Fails("""{"rows":[{"avg":1},{"avg":2}]}""", """{"rows":[{"avg":1},{"avg":3}]}"""));

    [Fact]
    public void A_reordered_list_fails() =>
        Fails("""[1,2]""", """[2,1]""");

    [Fact]
    public void A_missing_entry_fails() =>
        Assert.Contains("entries", Fails("""[1,2]""", """[1]"""));

    [Fact]
    public void A_missing_property_equals_null() =>
        JsonAssert.Equivalent(JsonNode.Parse("""{"a":null}"""), JsonNode.Parse("""{}"""));

    [Fact]
    public void Numbers_compare_by_value() =>
        JsonAssert.Equivalent(JsonNode.Parse("""{"a":2023}"""), JsonNode.Parse("""{"a":2023.0}"""));

    [Fact]
    public void A_changed_string_fails() =>
        Fails("""{"why":"leves ×2"}""", """{"why":"leves ×3"}""");

    private static string Fails(string expected, string actual) =>
        Assert.Throws<TrueException>(() => JsonAssert.Equivalent(JsonNode.Parse(expected), JsonNode.Parse(actual))).Message;
}
