using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace NemesisSystem.Tests;

public class SettingsTests
{
	// the constructor loads the INI at the path and writes it straight back, so it gets a temp one
	private static NemesisSettings Load(string ini)
	{
		string path = Path.GetTempFileName();
		try
		{
			File.WriteAllText(path, ini);
			return new NemesisSettings(path);
		}
		finally
		{
			File.Delete(path);
			File.Delete(path + ".tmp");
		}
	}

	// an empty NemesisMod.ini: a first run
	private static NemesisSettings Fresh()
	{
		return Load("");
	}

	// Layout() as NemesisMod.ini lays it out: each [Section] with the keys written under it
	private static List<(string sec, List<string> keys)> Sections(NemesisSettings s)
	{
		List<(string sec, List<string> keys)> list = new List<(string sec, List<string> keys)>();
		IEnumerable<Setting> layout = (IEnumerable<Setting>)typeof(NemesisSettings).GetMethod("Layout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, null);
		foreach (Setting item in layout)
		{
			string text = item.Write?.Invoke();
			if (text == null)
			{
				continue;
			}
			if (item.Read == null)
			{
				if (text.StartsWith("[") && text.EndsWith("]"))
				{
					list.Add((text.Substring(1, text.Length - 2), new List<string>()));
				}
			}
			else if (list.Count > 0 && text.IndexOf('=') > 0)
			{
				list[list.Count - 1].keys.Add(text.Substring(0, text.IndexOf('=')));
			}
		}
		return list;
	}

	[Fact]
	public void CompanionsOnByDefault()
	{
		Assert.True(Fresh().CompanionsEnabled);
	}

	[Fact]
	public void RecruitsOnByDefault()
	{
		Assert.True(Fresh().RecruitsEnabled);
	}

	[Fact]
	public void RecruitsKeepSixByDefault()
	{
		Assert.Equal(6, Fresh().RecruitsMaxKept);
	}

	[Fact]
	public void RecruitChanceAsIsByDefault()
	{
		Assert.Equal(100, Fresh().RecruitsChancePercent);
	}

	[Fact]
	public void KeysSitUnderTheirSections()
	{
		List<(string sec, List<string> keys)> list = Sections(Fresh());
		int num = list.FindIndex(x => x.sec == "Companions");
		Assert.True(num >= 0, "no [Companions]");
		Assert.Equal(new string[1] { "Enabled" }, list[num].keys);
		Assert.True(num + 1 < list.Count, "nothing after [Companions]");
		Assert.Equal("Recruits", list[num + 1].sec);
		Assert.Equal(new string[3] { "Enabled", "MaxKept", "ChancePercent" }, list[num + 1].keys);
		Assert.Single(list, x => x.sec == "Recruits");
	}

	[Fact]
	public void RecruitsReadAndClamp()
	{
		NemesisSettings s = Load("[Meta]\r\nVersion=50\r\n[Recruits]\r\nEnabled=False\r\nMaxKept=40\r\nChancePercent=-5\r\n");
		Assert.False(s.RecruitsEnabled);
		Assert.Equal(12, s.RecruitsMaxKept);
		Assert.Equal(0, s.RecruitsChancePercent);
		s = Load("[Meta]\r\nVersion=50\r\n[Recruits]\r\nMaxKept=0\r\nChancePercent=300\r\n");
		Assert.True(s.RecruitsEnabled);
		Assert.Equal(0, s.RecruitsMaxKept);
		Assert.Equal(300, s.RecruitsChancePercent);
	}
}
