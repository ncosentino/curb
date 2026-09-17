using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Tests.LayoutRules;

public class LayoutConfigurationTests
{
	private const string Root = "/repo";

	private static MockFileSystem Repo()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData("root = true\n[*.cs]\n" + LayoutRuleSamples.Config + "\ncurb_layout_rules = .curb-layout.json\n"));
		fs.AddFile($"{Root}/.curb-layout.json", new MockFileData(LayoutRuleSamples.Policy));
		fs.AddFile($"{Root}/Source.cs", new MockFileData(LayoutRuleSamples.Source));
		return fs;
	}

	[Test]
	public void Policy_contents_invalidate_a_previously_proven_cache_entry()
	{
		var fs = Repo();
		var cache = $"{Root}/curb.cache";
		FormattingRun.Execute(fs, Root, write: true, cachePath: cache).ExitCode.Should().Be(0);
		FormattingRun.Execute(fs, Root, write: false, cachePath: cache).ExitCode.Should().Be(0);
		FormattingRun.Execute(fs, Root, write: false, cachePath: cache).Cached.Should().Be(1);
		var timestamp = fs.File.GetLastWriteTimeUtc($"{Root}/.curb-layout.json");
		fs.File.WriteAllText($"{Root}/.curb-layout.json", LayoutRuleSamples.Policy.Replace("TraceScope.RunAsync", "OtherScope.RunAsync", StringComparison.Ordinal));
		fs.File.SetLastWriteTimeUtc($"{Root}/.curb-layout.json", timestamp);
		var changed = FormattingRun.Execute(fs, Root, write: false, cachePath: cache);
		changed.Cached.Should().Be(0);
		changed.Changed.Should().Be(1);
		changed.ExitCode.Should().Be(1);
	}

	[Test]
	public void Missing_or_invalid_policy_never_writes_or_caches_source()
	{
		var fs = Repo();
		var source = fs.File.ReadAllText($"{Root}/Source.cs");
		fs.File.Delete($"{Root}/.curb-layout.json");
		var missing = FormattingRun.Execute(fs, Root, write: true, cachePath: $"{Root}/curb.cache");
		missing.ExitCode.Should().Be(3);
		missing.Changed.Should().Be(0);
		fs.File.ReadAllText($"{Root}/Source.cs").Should().Be(source);
		fs.File.WriteAllText($"{Root}/.curb-layout.json", "{}");
		FormattingRun.Execute(fs, Root, write: true).ExitCode.Should().Be(3);
		fs.File.ReadAllText($"{Root}/Source.cs").Should().Be(source);
		fs.File.Exists($"{Root}/curb.cache").Should().BeFalse();
	}

	[Test]
	public void Relative_policy_paths_use_the_declaring_editorconfig()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/nested/Source.cs", new MockFileData(LayoutRuleSamples.Source));
		var resolver = new CurbEditorConfig(fs);
		var loader = new LayoutRuleLoader(fs, resolver);
		var policy = loader.For($"{Root}/nested/Source.cs", resolver.For($"{Root}/nested/Source.cs"));
		policy.Path.Should().Be(fs.Path.GetFullPath($"{Root}/.curb-layout.json"));
		policy.Rules!.Rules.Should().ContainSingle();
		fs.AddFile($"{Root}/nested/.editorconfig", new MockFileData("[*.cs]\ncurb_layout_rules = none\n"));
		resolver = new CurbEditorConfig(fs);
		new LayoutRuleLoader(fs, resolver).For($"{Root}/nested/Source.cs", resolver.For($"{Root}/nested/Source.cs")).Rules.Should().BeNull();
	}

	[Test]
	public void File_filters_and_explicit_policy_snapshots_use_the_logical_base()
	{
		var fs = Repo();
		fs.AddFile("/snapshot/rules.json", new MockFileData(LayoutRuleSamples.Policy.Replace("**/*.cs", "nested/*.cs", StringComparison.Ordinal)));
		fs.AddFile($"{Root}/nested/Source.cs", new MockFileData(LayoutRuleSamples.Source));
		var resolver = new CurbEditorConfig(fs);
		var loader = new LayoutRuleLoader(fs, resolver);
		loader.For($"{Root}/Source.cs", resolver.For($"{Root}/Source.cs"), "/snapshot/rules.json", Root).Rules.Should().BeNull();
		loader.For($"{Root}/nested/Source.cs", resolver.For($"{Root}/nested/Source.cs"), "/snapshot/rules.json", Root).Rules!.Rules.Should().ContainSingle();
	}

	[Test]
	public void An_explicit_none_override_does_not_load_an_invalid_file()
	{
		var fs = Repo();
		fs.File.WriteAllText($"{Root}/.curb-layout.json", "{ broken");
		FormattingRun.Execute(fs, Root, write: true, layoutRulesPath: "none").ExitCode.Should().Be(0);
	}

	[Test]
	public void An_editorconfig_reference_cannot_escape_its_configuration_directory()
	{
		var fs = Repo();
		fs.AddFile("/outside.json", new MockFileData(LayoutRuleSamples.Policy));
		fs.File.WriteAllText($"{Root}/.editorconfig", "root=true\n[*.cs]\ncurb_layout_rules=../outside.json\n");
		FormattingRun.Execute(fs, Root, write: true).ExitCode.Should().Be(3);
		fs.File.ReadAllText($"{Root}/Source.cs").Should().Be(LayoutRuleSamples.Source);
	}

	[Test]
	public void Selected_policy_paths_are_recorded_for_msbuild()
	{
		var fs = Repo();
		var dependencies = $"{Root}/obj/layout.inputs";
		var result = FormattingRun.Execute(fs, Root, write: true, layoutDependenciesPath: dependencies);
		result.ExitCode.Should().Be(0);
		fs.File.ReadAllLines(dependencies).Should().Equal(fs.Path.GetFullPath($"{Root}/.curb-layout.json"));
	}

	[Test]
	[Arguments("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
	[Arguments("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")]
	[Arguments("\"bodyIndent\": 1", "\"bodyIndent\": 2")]
	[Arguments("\"callbackArgument\": \"last\"", "\"callbackArgument\": \"first\"")]
	[Arguments("\"TraceScope.RunAsync\"", "\"TraceScope.RunAsync()\"")]
	[Arguments("\"files\": [\"**/*.cs\"]", "\"files\": [\"../*.cs\"]")]
	public void Unsupported_policy_data_is_rejected(string before, string after)
	{
		var source = LayoutRuleSamples.Policy.Replace(before, after, StringComparison.Ordinal);
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(source));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void Valid_policy_compilation_preserves_the_rule_identity()
	{
		var definitions = LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(LayoutRuleSamples.Policy));
		definitions.Should().ContainSingle().Which.Rule.Id.Should().Be("wrappers");
	}

	[Test]
	public void Duplicate_ids_and_oversized_policy_are_rejected()
	{
		using var document = System.Text.Json.JsonDocument.Parse(LayoutRuleSamples.Policy);
		var rule = document.RootElement.GetProperty("rules")[0].GetRawText();
		var duplicate = "{\"schemaVersion\":1,\"rules\":[" + rule + "," + rule + "]}";
		Action duplicates = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(duplicate));
		duplicates.Should().Throw<LayoutRuleConfigurationException>();
		Action oversized = () => LayoutRuleCompiler.Compile(new byte[LayoutRuleCompiler.MaximumBytes + 1]);
		oversized.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void Explicit_file_exclusion_takes_precedence_over_policy_loading()
	{
		var fs = Repo();
		fs.File.WriteAllText($"{Root}/.editorconfig", "root=true\n[*.cs]\ngenerated_code=true\ncurb_layout_rules=missing.json\n");
		var result = FormattingRun.Execute(fs, Root, write: true);
		result.ExitCode.Should().Be(0);
		result.Skipped.Should().Be(1);
		fs.File.ReadAllText($"{Root}/Source.cs").Should().Be(LayoutRuleSamples.Source);
	}

	[Test]
	[Arguments("Source.cs")]
	[Arguments(".curb-layout.json")]
	[Arguments(".editorconfig")]
	public void Dependency_output_cannot_overwrite_an_input(string name)
	{
		var fs = Repo();
		var path = $"{Root}/{name}";
		var original = fs.File.ReadAllText(path);
		FormattingRun.Execute(fs, Root, write: true, layoutDependenciesPath: path).ExitCode.Should().Be(3);
		fs.File.ReadAllText(path).Should().Be(original);
	}
}
