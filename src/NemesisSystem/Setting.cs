using System;

namespace NemesisSystem;

internal sealed class Setting
{
	public Action<IniFile> Read;

	public Func<string> Write;

	public bool State;

	public string Section;

	public static Setting Key<T>(string section, string key, Func<T> get, Action<T> set, Func<string> write)
	{
		return new Setting
		{
			Read = delegate(IniFile s)
			{
				set(s.GetValue(section, key, get()));
			},
			Write = () => key + "=" + write()
		};
	}

	public static Setting ReadOnly<T>(string section, string key, Func<T> get, Action<T> set)
	{
		return new Setting
		{
			Read = delegate(IniFile s)
			{
				set(s.GetValue(section, key, get()));
			}
		};
	}

	public static Setting StateKey<T>(string section, string key, Func<T> get, Action<T> set, Func<string> write)
	{
		return new Setting
		{
			Read = delegate(IniFile s)
			{
				set(s.GetValue(section, key, get()));
			},
			Write = () => key + "=" + write(),
			State = true,
			Section = section
		};
	}

	public static Setting StateLine(string section, Func<string> text)
	{
		return new Setting
		{
			Write = text,
			State = true,
			Section = section
		};
	}

	public static Setting Line(string text)
	{
		return new Setting
		{
			Write = () => text
		};
	}

	public static Setting Line(Func<string> text)
	{
		return new Setting
		{
			Write = text
		};
	}
}
