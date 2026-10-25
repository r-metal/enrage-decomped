using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace NemesisSystem;

internal sealed class IniFile
{
	private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public readonly HashSet<string> Twice = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public static IniFile Load(string path)
	{
		IniFile iniFile = new IniFile();
		if (!File.Exists(path))
		{
			return iniFile;
		}
		string text = "";
		string[] array = File.ReadAllLines(path);
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			if (text2.Length == 0 || text2.StartsWith(";") || text2.StartsWith("//"))
			{
				continue;
			}
			if (text2.StartsWith("[") && text2.Contains("]"))
			{
				text = text2.Substring(1, text2.IndexOf(']') - 1).Trim();
				continue;
			}
			int num = text2.IndexOf('=');
			if (num > 0)
			{
				string text3 = text + "/" + text2.Substring(0, num).Trim();
				if (!iniFile._values.ContainsKey(text3))
				{
					iniFile._values[text3] = text2.Substring(num + 1).Trim();
				}
				else
				{
					iniFile.Twice.Add(text3);
				}
			}
		}
		return iniFile;
	}

	public string Raw(string section, string name)
	{
		if (!_values.TryGetValue(section + "/" + name, out var value))
		{
			return null;
		}
		return value;
	}

	public static bool IsKey(string v)
	{
		if (string.IsNullOrWhiteSpace(v))
		{
			return false;
		}
		try
		{
			Enum.Parse(typeof(Keys), KeyCombo.ForParse(v), ignoreCase: true);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public T GetValue<T>(string section, string name, T defaultValue)
	{
		if (!_values.TryGetValue(section + "/" + name, out var value))
		{
			return defaultValue;
		}
		try
		{
			if (typeof(T).IsEnum)
			{
				return (T)Enum.Parse(typeof(T), (typeof(T) == typeof(Keys)) ? KeyCombo.ForParse(value) : value, ignoreCase: true);
			}
			return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
		}
		catch
		{
			return defaultValue;
		}
	}
}
