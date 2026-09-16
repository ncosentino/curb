using System.Diagnostics;
using System.Xml.Linq;
using AwesomeAssertions;

namespace Nullean.Curb.Tests.Cli;

public class CliRejectionTests
{
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
