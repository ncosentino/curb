using System.IO.Abstractions;
using EditorConfig.Core;

namespace Nullean.Curb.EditorConfig;

/// <summary>
/// Resolves <c>.editorconfig</c> settings for source files.
/// </summary>
/// <remarks>
/// Wraps <see cref="EditorConfigParser"/> and holds it for the lifetime of a run so its
/// per-directory chain cache, compiled-glob cache and file cache all stay warm. Formatting a
/// repository then resolves configuration once per directory rather than once per file.
/// </remarks>
/// <param name="fileSystem">
/// Injected so tests can run against <c>MockFileSystem</c>. Every filesystem touch in Curb goes
/// through this — there are no direct <c>System.IO.File</c> calls.
/// </param>
public sealed class CurbEditorConfig(IFileSystem fileSystem)
{
	// EditorConfigParser's file cache is private per instance by default as of editorconfig 0.18.0
	// (editorconfig/editorconfig-core-net#64, filed from this repo — nullean/curb#65). Before that,
	// the default constructor routed every parse through a static, process-wide cache keyed on
	// path+mtime+length with no regard for which IFileSystem produced it, so two MockFileSystem-backed
	// tests reusing the same conventional path could collide and one would silently read the other's
	// settings. Fixed upstream; no workaround needed here any more.
	private readonly EditorConfigParser _parser = new(fileSystem);
	private readonly Dictionary<string, EditorConfigResolvedChain> _chains = new(StringComparer.Ordinal);
	private readonly Dictionary<string, GlobMatcher> _matchers = new(StringComparer.Ordinal);

	/// <summary>Resolves the settings that apply to <paramref name="filePath"/>.</summary>
	public FileConfiguration For(string filePath)
	{
		return _parser.Parse(filePath, Chain(filePath));
	}

	/// <summary>Finds the directory of the last matching section that declares a property.</summary>
	/// <param name="filePath">The source file whose configuration is being resolved.</param>
	/// <param name="property">A normalized EditorConfig property name.</param>
	/// <returns>The declaring configuration directory, or null when no matching section declares the property.</returns>
	public string? OriginDirectoryFor(string filePath, string property)
	{
		var fullPath = fileSystem.Path.GetFullPath(filePath);
		var sections = Chain(fullPath).Sections;
		for (var i = sections.Length - 1; i >= 0; i--)
		{
			var section = sections[i];
			if (!section.ContainsKey(property))
				continue;
			if (!_matchers.TryGetValue(section.Glob, out var matcher))
			{
				matcher = GlobMatcher.Create(section.Glob, new GlobMatcherOptions { MatchBase = true, Dot = true, AllowWindowsPaths = true });
				_matchers.Add(section.Glob, matcher);
			}
			if (matcher.IsMatch(fullPath))
				return section.EditorConfigFile.Directory;
		}
		return null;
	}

	private EditorConfigResolvedChain Chain(string filePath)
	{
		var directory = Path.GetDirectoryName(filePath) ?? ".";
		if (!_chains.TryGetValue(directory, out var chain))
		{
			chain = _parser.GetResolvedChainForDirectory(directory);
			_chains[directory] = chain;
		}
		return chain;
	}
}
