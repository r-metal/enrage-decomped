using System;
using System.Reflection;
using Xunit;

namespace NemesisSystem.Tests;

public class VersionTests
{
	private static readonly Assembly Mod = typeof(NemesisRecord).Assembly;

	[Fact]
	public void InformationalVersionIsTheFork()
	{
		// Theme.ModVersion shows everything before '+', so there must be no '+hash' suffix
		Assert.Equal("0.9.5.3-decomped.1", Mod.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
	}

	[Fact]
	public void AssemblyVersionStaysUpstream()
	{
		// CheckOtherCopies compares this: a newer official enRAGE must still win
		Assert.Equal(new Version(0, 9, 5, 3), Mod.GetName().Version);
		Assert.Equal("0.9.5.3", Mod.GetName().Version.ToString());
	}

	[Fact]
	public void FileVersionStaysUpstream()
	{
		Assert.Equal("0.9.5.3", Mod.GetCustomAttribute<AssemblyFileVersionAttribute>().Version);
	}
}
