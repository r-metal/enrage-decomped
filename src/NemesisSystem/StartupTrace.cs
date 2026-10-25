using System;
using System.IO;

namespace NemesisSystem;

internal static class StartupTrace
{
	private static string _path;

	private static bool _failed;

	internal static int TicksLeft = 2;

	private static string SafeDir(string file)
	{
		try
		{
			return string.IsNullOrEmpty(file) ? null : Path.GetDirectoryName(file);
		}
		catch
		{
			return null;
		}
	}

	internal static void Retarget(string dir)
	{
		try
		{
			if (_path != null)
			{
				_path = Path.Combine(dir, Path.GetFileName(_path));
			}
		}
		catch
		{
		}
	}

	private static string DocsDir()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Rockstar Games", "Red Dead Redemption 2", "enRAGE");
		Directory.CreateDirectory(text);
		return text;
	}

	internal static void Write(string step)
	{
		if (_failed)
		{
			return;
		}
		try
		{
			if (_path == null)
			{
				string text;
				try
				{
					text = DocsDir();
				}
				catch
				{
					string text2 = Path.Combine(Directory.GetCurrentDirectory(), "scripts");
					if (!Directory.Exists(text2))
					{
						_failed = true;
						return;
					}
					text = Path.Combine(text2, "enRAGE");
					Directory.CreateDirectory(text);
				}
				_path = Path.Combine(text, "startup.log");
				try
				{
					File.WriteAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} startup trace begins{Environment.NewLine}");
				}
				catch
				{
					_failed = true;
					return;
				}
			}
			File.AppendAllText(_path, $"{DateTime.Now:HH:mm:ss.fff} {step}{Environment.NewLine}");
		}
		catch
		{
			_failed = true;
		}
	}
}
