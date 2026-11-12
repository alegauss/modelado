using Winwright.Processes;
using Winwright.Projects;
using Winwright.Scenarios;
using Winwright.Verdicts;
using Winwright.Windowing;

namespace Modelado.Cases;

/// <summary>
/// Every UI case this repository declares in cases/*.cases.json, run against the launched app
/// (MOD8, after PhotoBoost's CasesRun). The accessibility tree is read rather than the screen
/// photographed, so it needs neither foreground nor a capture.
///
/// <para><b>One test, not one per case</b>: the engine runs the selection and answers one verdict
/// over all of it, and lends a shareable fixture's window to every case that asks for it.</para>
///
/// <para><b>A desk that cannot be observed is a skip with its reason</b>, never a pass that
/// checked nothing. These run on the VMware guest, through winwright's run-tests-vm.cmd.</para>
///
/// <para><b>MODELADO_LIVE=1 runs cases-live instead</b> (MOD61): the cases on the real models,
/// under winwright.live.json's longer waits. The gate never sets it, since the guest tree carries
/// no models; run-live-vm.cmd does, after downloading them on the guest.</para>
/// </summary>
public sealed class CasesRun
{
    // Modelado.Inference's LanguageSetting.Variable; this project does not reference the app's libraries.
    private const string LanguageVariable = "MODELADO_LANGUAGE";

    private static bool Live => Environment.GetEnvironmentVariable("MODELADO_LIVE") == "1";

    // MOD78: MODELADO_PACKAGED=1 runs cases-packaged, whose fixtures start the registered Store
    // package through its execution aliases. build\check-msix.ps1 -Cases sets it, between registering
    // the layout and removing it, on a desk with Developer Mode.
    private static bool Packaged => Environment.GetEnvironmentVariable("MODELADO_PACKAGED") == "1";

    private static string CasesFolder
    {
        get
        {
            if (Packaged)
            {
                return "cases-packaged";
            }

            return Live ? "cases-live" : "cases";
        }
    }

    [Fact]
    public void EveryUiCaseInThisRepositoryRuns()
    {
        IReadOnlyList<CaseDeclaration> all = ScenarioFile.Across(ScenarioFile.LoadAll(Path.Combine(Repository.Root, CasesFolder)));
        Assert.NotEmpty(all);

        // MOD135: MODELADO_CASES=tag,tag runs only the cases carrying one of those tags, for a task
        // that touched them; the whole suite still runs every few tasks, and on any run without it.
        string[] tags = Environment.GetEnvironmentVariable("MODELADO_CASES")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        IReadOnlyList<CaseDeclaration> declared = tags.Length == 0 ? all : [.. all.Where(one => tags.Any(one.Tagged))];
        Assert.True(declared.Count > 0, $"no case carries any of the tags {string.Join(", ", tags)}");

        Desk desk = Desk.Read();
        if (!desk.CanObserve)
        {
            Assert.Skip($"no UI case ran: this desk lacks {desk.FirstAbsent!.Name}");
        }

        ProjectDeclaration project = ForThisBuild(Repository.Root, Live ? "winwright.live.json" : "winwright.json");

        // MOD86: the cases read English text, so every window they start is in English, whatever
        // the desk's display language or the language a case chooses in About. Inherited by each
        // process winwright starts.
        Environment.SetEnvironmentVariable(LanguageVariable, "en");

        // MOD11: what the download cases fetch, through --manifest, from this machine.
        using FixtureServer downloads = new(Path.Combine(Repository.Root, "cases", "fixtures", "download"));
        using ProcessRegister register = ProcessRegister.For(project);
        SuiteVerdict verdict = Suite.Launch(declared, tags.Length == 0 ? Selection.All : Selection.Of(null, tags), register, project);

        string ran = tags.Length == 0
            ? $"UI cases: {verdict.Ran.Count} of {declared.Count} declared ran"
            : $"UI cases tagged {string.Join(", ", tags)}: {verdict.Ran.Count} of {declared.Count} ran, {all.Count - declared.Count} others left for the full suite";
        TestContext.Current.TestOutputHelper?.WriteLine(ran);
        TestContext.Current.TestOutputHelper?.WriteLine(Read(verdict));

        // A case declared and not run is a claim nobody checked.
        Assert.True(verdict.Ran.Count == declared.Count, $"{ran}.{Environment.NewLine}{Read(verdict)}");
        Assert.True(verdict.Outcome == RunOutcome.Passed, Read(verdict));
        Assert.Equal(0, verdict.ExitCode);

        // And nothing this run started is still on the desk.
        IReadOnlyList<Survivor> survivors = register.StopAll();
        Assert.True(survivors.Count == 0, string.Join(Environment.NewLine, survivors));
    }

    /// <summary>The declaration with {configuration} filled with the one this assembly was built in.</summary>
    private static ProjectDeclaration ForThisBuild(string repository, string declaration)
    {
        string configuration = typeof(CasesRun).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
            .OfType<System.Reflection.AssemblyConfigurationAttribute>()
            .Select(one => one.Configuration)
            .FirstOrDefault() ?? "Debug";

        return ProjectDeclaration.Load(Path.Combine(repository, declaration), configuration);
    }

    /// <summary>The whole reading: failures, what could not be asked, what broke, and the trace.</summary>
    private static string Read(SuiteVerdict verdict)
    {
        List<string> lines = [.. verdict.Render()];
        foreach (CaseResult unhappy in verdict.Unhappy)
        {
            lines.Add(string.Empty);
            lines.Add($"{unhappy.Declared.Name}:");
            lines.AddRange(unhappy.Verdict.Failures.Select(one => $"  failed    {one}"));
            lines.AddRange(unhappy.Verdict.Unchecked.Select(one => $"  unchecked {one}"));
            lines.AddRange(unhappy.Verdict.Broke.Select(one => $"  broke     {one}"));
            lines.AddRange(unhappy.Trace.Select(one => $"  trace     {one}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
