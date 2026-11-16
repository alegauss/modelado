using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>MOD123: a license's conditions in the manifest, and the agreement the app records before the download.</summary>
public sealed class ModelConsentsTests
{
    private const string Manifest = """
        {
          "schema": 1,
          "version": "1",
          "models": [
            {
              "name": "gated-3d",
              "task": "image-to-mesh",
              "meta": { "url": "https://host/gated-3d-meta.json", "bytes": 10, "sha256": "aa" },
              "variants": { "fp32": { "url": "https://host/gated-3d-fp32.onnx", "bytes": 20, "sha256": "bb" } },
              "license": "Stability AI Community License",
              "license_url": "https://host/LICENSE.md",
              "conditions": { "summary": "Free under US $1M a year.", "attribution": "Powered by Stability AI", "register_url": "https://stability.ai/community-license" }
            },
            {
              "name": "plain",
              "task": "depth",
              "meta": { "url": "https://host/plain-meta.json", "bytes": 10, "sha256": "cc" },
              "variants": { "fp32": { "url": "https://host/plain-fp32.onnx", "bytes": 20, "sha256": "dd" } },
              "license": "MIT",
              "license_url": "https://opensource.org/license/mit"
            }
          ]
        }
        """;

    [Fact]
    public void A_license_with_conditions_carries_them_and_a_permissive_one_has_none()
    {
        ModelManifest manifest = ModelManifest.Parse(Manifest);

        Assert.Equal(new ModelConditions("Free under US $1M a year.", "Powered by Stability AI", "https://stability.ai/community-license"), manifest.Model("gated-3d")!.Conditions);
        Assert.Null(manifest.Model("plain")!.Conditions);
    }

    [Fact]
    public void A_model_whose_license_requires_a_notice_is_credited_from_the_manifest_and_a_permissive_one_is_not()
    {
        // MOD126: neither is installed here, so the manifest's terms are what is read.
        ModelLibrary empty = new([Path.Combine(Path.GetTempPath(), $"no-models-{Guid.NewGuid():N}")]);

        IReadOnlyList<ModelCredit> credits = ModelCredits.For(["plain", "gated-3d", "gated-3d"], empty, ModelManifest.Parse(Manifest));

        Assert.Equal([new ModelCredit("gated-3d", "Powered by Stability AI")], credits);
        Assert.Empty(ModelCredits.For(["plain"], empty, ModelManifest.Parse(Manifest)));
        Assert.Empty(ModelCredits.For(["gated-3d"], empty, null));
    }

    [Fact]
    public void An_agreement_is_recorded_per_model_and_asked_again_for_another_license_text()
    {
        string settings = Path.Combine(Path.GetTempPath(), $"consents-{Guid.NewGuid():N}", "settings.json");
        try
        {
            ManifestModel gated = ModelManifest.Parse(Manifest).Model("gated-3d")!;
            Assert.Null(ModelConsents.Agreed(settings, gated));

            DateTimeOffset now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
            ModelConsents.Record(settings, [gated], now);

            Assert.Equal(now, ModelConsents.Agreed(settings, gated));
            Assert.Null(ModelConsents.Agreed(settings, gated with { LicenseUrl = "https://host/LICENSE-v2.md" }));
            Assert.Null(ModelConsents.Agreed(settings, gated with { Name = "other" }));

            // Other settings in the file are kept.
            SettingsFile.Write(settings, "provider", System.Text.Json.Nodes.JsonValue.Create("cpu"));
            Assert.Equal(now, ModelConsents.Agreed(settings, gated));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(settings)!, recursive: true);
        }
    }
}
