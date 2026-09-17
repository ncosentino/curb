using System.Diagnostics;
using System.Xml.Linq;
using AwesomeAssertions;
using Nullean.Curb.Tests.LayoutRules;

namespace Nullean.Curb.Tests.Cli;

public class CliRejectionTests
{
	[Test]
	public async Task Custom_policy_cli_explains_without_writing_then_formats(CancellationToken cancellationToken)
	{
		var root = Path.Combine(Path.GetTempPath(), "curb-layout-cli-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var file = Path.Combine(root, "Source.cs");
			var policy = Path.Combine(root, "policy.json");
			await File.WriteAllTextAsync(file, LayoutRuleSamples.Source, cancellationToken);
			await File.WriteAllTextAsync(policy, LayoutRuleSamples.Policy, cancellationToken);
			await File.WriteAllTextAsync(Path.Combine(root, ".editorconfig"), "root=true\n[*.cs]\n" + LayoutRuleSamples.Config, cancellationToken);
			var (explanationCode, explanationOutput, explanationError) = await Run([CliPath(), "explain-layout", file, "--layout-rules", policy, "--layout-rules-base", root], cancellationToken);
			explanationCode.Should().Be(0, explanationError);
			explanationOutput.Should().Contain("rule = wrappers;");
			(await File.ReadAllTextAsync(file, cancellationToken)).Should().Be(LayoutRuleSamples.Source);
			var (formatCode, _, formatError) = await Run([CliPath(), "format", "--files", file, "--layout-rules", policy], cancellationToken);
			formatCode.Should().Be(0, formatError);
			(await File.ReadAllTextAsync(file, cancellationToken)).TrimEnd().Should().Be(LayoutRuleSamples.Expected);
			var (checkCode, _, checkError) = await Run([CliPath(), "check", "--files", file, "--layout-rules", policy], cancellationToken);
			checkCode.Should().Be(0, checkError);
		}
		finally { Directory.Delete(root, recursive: true); }
	}

	[Test]
	public async Task Rule_file_changes_invalidate_msbuild_without_source_edits(CancellationToken cancellationToken)
	{
		var root = Path.Combine(Path.GetTempPath(), "curb-layout-msbuild-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var file = Path.Combine(root, "Source.cs");
			var policy = Path.Combine(root, "policy.json");
			var editorConfig = Path.Combine(root, ".editorconfig");
			var project = Path.Combine(root, "Probe.proj");
			var stamp = Path.Combine(root, "curb.stamp");
			await File.WriteAllTextAsync(file, LayoutRuleSamples.Source, cancellationToken);
			await File.WriteAllTextAsync(policy, LayoutRuleSamples.Policy, cancellationToken);
			await File.WriteAllTextAsync(editorConfig, "root=true\n[*.cs]\n" + LayoutRuleSamples.Config + "\ncurb_layout_rules=policy.json\n", cancellationToken);
			var xml = new XDocument(new XElement("Project",
				new XElement("PropertyGroup",
					new XElement("Language", "C#"), new XElement("BuildingProject", "true"),
					new XElement("IntermediateOutputPath", root + Path.DirectorySeparatorChar),
					new XElement("DOTNET_HOST_PATH", "dotnet"), new XElement("Curb_Dll", CliPath()),
					new XElement("Curb_Check", "false"), new XElement("Curb_LogLevel", "high")),
				new XElement("ItemGroup",
					new XElement("Compile", new XAttribute("Include", file)),
					new XElement("EditorConfigFiles", new XAttribute("Include", editorConfig))),
				new XElement("Import", new XAttribute("Project", Path.Combine(RepositoryRoot(), "src", "Nullean.Curb.MSBuild", "build", "curb.targets")))));
			await File.WriteAllTextAsync(project, xml.ToString(), cancellationToken);
			var arguments = new[] { "msbuild", project, "-target:Curb", "-nologo", "-verbosity:normal" };
			var (firstCode, firstOutput, firstError) = await Run(arguments, cancellationToken);
			firstCode.Should().Be(0, firstOutput + firstError);
			var custom = await File.ReadAllTextAsync(file, cancellationToken);
			custom.TrimEnd().Should().Be(LayoutRuleSamples.Expected);
			File.Exists(stamp).Should().BeTrue();
			(await File.ReadAllLinesAsync(Path.Combine(root, "curb.layout-files"), cancellationToken)).Should().Contain(policy);
			var (secondCode, secondOutput, secondError) = await Run(arguments, cancellationToken);
			secondCode.Should().Be(0, secondOutput + secondError);
			secondOutput.Should().NotContain("Formatted 1 file");
			await File.WriteAllTextAsync(policy, LayoutRuleSamples.Policy.Replace("TraceScope.RunAsync", "OtherScope.RunAsync", StringComparison.Ordinal), cancellationToken);
			File.SetLastWriteTimeUtc(policy, File.GetLastWriteTimeUtc(stamp).AddSeconds(2));
			var (changedCode, changedOutput, changedError) = await Run(arguments, cancellationToken);
			changedCode.Should().Be(0, changedOutput + changedError);
			(await File.ReadAllTextAsync(file, cancellationToken)).Should().NotBe(custom);
			File.Delete(policy);
			var (failedCode, failedOutput, failedError) = await Run(arguments, cancellationToken);
			failedCode.Should().NotBe(0);
			(failedOutput + failedError).Should().Contain("CURB0002");
			File.Exists(stamp).Should().BeFalse();
		}
		finally { Directory.Delete(root, recursive: true); }
	}

	[Test]
	[Arguments("check")]
	[Arguments("format")]
	public async Task The_process_returns_failure_for_rejected_source(string command, CancellationToken cancellationToken)
	{
		var root = Path.Combine(Path.GetTempPath(), "curb-rejection-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			const string source = "class Broken { void M( }";
			var file = Path.Combine(root, "Broken.cs");
			await File.WriteAllTextAsync(file, source, cancellationToken);
			var (code, output, error) = await Run([CliPath(), command, file], cancellationToken);

			code.Should().Be(3);
			output.Should().Contain("1 unparsable");
			error.Should().Contain("does not parse");
			(await File.ReadAllTextAsync(file, cancellationToken)).Should().Be(source);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task MSBuild_reports_rejection_without_a_success_stamp(CancellationToken cancellationToken)
	{
		var root = Path.Combine(Path.GetTempPath(), "curb-msbuild-rejection-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var file = Path.Combine(root, "Broken.cs");
			await File.WriteAllTextAsync(file, "class Broken { void M( }", cancellationToken);
			var project = Path.Combine(root, "Probe.proj");
			var stamp = Path.Combine(root, "curb.stamp");
			var xml = new XDocument(new XElement("Project",
				new XElement("PropertyGroup",
					new XElement("Language", "C#"),
					new XElement("BuildingProject", "true"),
					new XElement("IntermediateOutputPath", root + Path.DirectorySeparatorChar),
					new XElement("DOTNET_HOST_PATH", "dotnet"),
					new XElement("Curb_Dll", CliPath()),
					new XElement("Curb_Check", "true")),
				new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", file))),
				new XElement("Import", new XAttribute("Project", Path.Combine(RepositoryRoot(), "src", "Nullean.Curb.MSBuild", "build", "curb.targets")))));
			await File.WriteAllTextAsync(project, xml.ToString(), cancellationToken);

			var (code, output, error) = await Run(["msbuild", project, "-target:Curb", "-nologo", "-verbosity:quiet"], cancellationToken);

			code.Should().NotBe(0);
			(output + error).Should().Contain("CURB0002");
			File.Exists(stamp).Should().BeFalse("rejected input must not stamp a successful format");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string RepositoryRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "curb.slnx")))
				return directory.FullName;
		}
		throw new InvalidOperationException("The repository root could not be located.");
	}

	private static string CliPath() =>
		Path.Combine(RepositoryRoot(), ".artifacts", "bin", "Nullean.Curb.Cli", new DirectoryInfo(AppContext.BaseDirectory).Name, "curb.dll");

	private static async Task<(int Code, string Output, string Error)> Run(string[] arguments, CancellationToken cancellationToken)
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo("dotnet")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			}
		};
		foreach (var argument in arguments)
			process.StartInfo.ArgumentList.Add(argument);
		if (!process.Start())
			throw new InvalidOperationException("The CLI process could not start.");
		var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var error = process.StandardError.ReadToEndAsync(cancellationToken);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(30));
		try
		{
			await process.WaitForExitAsync(timeout.Token);
			return (process.ExitCode, await output, await error);
		}
		finally
		{
			if (!process.HasExited)
				process.Kill(entireProcessTree: true);
		}
	}
}
