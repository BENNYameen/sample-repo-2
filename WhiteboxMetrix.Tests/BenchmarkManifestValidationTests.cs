using System.Reflection;
using System.Text.Json;
using NUnit.Framework;

namespace WhiteboxMetrix.Tests;

/// <summary>Contract tests so benchmark manifests used for all-uses / churn tooling stay internally consistent.</summary>
[TestFixture]
public sealed class BenchmarkManifestValidationTests
{
    private static string ManifestPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, fileName);

    [Test]
    public void AllUses_manifest_shape_and_exercisedBy_targets_resolve()
    {
        var path = ManifestPath("AllUsesManifest.json");
        Assert.That(File.Exists(path), Is.True, $"Missing copied manifest — check WhiteboxMetrix.Tests.csproj: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        Assert.That(root.GetProperty("version").GetInt32(), Is.GreaterThan(0));

        foreach (var u in root.GetProperty("uses").EnumerateArray())
        {
            Assert.That(u.GetProperty("id").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(u.GetProperty("path").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(u.GetProperty("useDescription").GetString(), Is.Not.Null.And.Not.Empty);
            var exercised = false;
            foreach (var token in u.GetProperty("exercisedBy").EnumerateArray())
            {
                exercised = true;
                var full = token.GetString();
                Assert.That(full, Is.Not.Null.And.Not.Empty);
                Assert.That(MethodExists(full!), Is.True, $"No test method mapped for '{full}'.");
            }

            Assert.That(exercised, Is.True);
        }
    }

    [Test]
    public void CodeChurn_manifest_hotspots_anchor_existing_definition_seed_ids()
    {
        var usesPath = ManifestPath("AllUsesManifest.json");
        var churnPath = ManifestPath("CodeChurnManifest.json");
        var defsPath = ManifestPath("AllDefinitionsManifest.json");

        foreach (var p in new[] { usesPath, churnPath, defsPath })
            Assert.That(File.Exists(p), Is.True, $"Missing manifest at {p}");

        using var defsDoc = JsonDocument.Parse(File.ReadAllText(defsPath));
        var defIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in defsDoc.RootElement.GetProperty("definitions").EnumerateArray())
            defIds.Add(d.GetProperty("id").GetString()!);

        using var churnDoc = JsonDocument.Parse(File.ReadAllText(churnPath));
        foreach (var h in churnDoc.RootElement.GetProperty("hotspots").EnumerateArray())
        {
            Assert.That(h.GetProperty("path").GetString(), Is.Not.Null.And.Not.Empty);

            if (!h.TryGetProperty("allUsesAnchorIds", out var anchors))
                continue;
            foreach (var id in anchors.EnumerateArray())
            {
                var s = id.GetString();
                Assert.That(s, Is.Not.Null.And.Not.Empty);
                Assert.That(defIds.Contains(s!), Is.True, $"Churn hotspot references unknown seed id '{s}'.");
            }
        }
    }

    private static bool MethodExists(string fullName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var last = fullName.LastIndexOf('.', StringComparison.Ordinal);
        if (last < 0)
            return false;

        var typeName = fullName[..last];
        var methodName = fullName[(last + 1)..];
        var t = asm.GetType(typeName, throwOnError: false);
        var m = t?.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.FlattenHierarchy);
        return m is not null;
    }
}
