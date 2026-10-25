using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NemesisSystem.BulletImpact;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;

namespace NemesisSystem;

public partial class NemesisMod
{
	private uint _surrenderGroup;

	private const string ActiveCopyKey = "enRAGE.active";

	private string _copyId;

	private string _copiesNotice;

	private (int count, string[] files)? _copiesWarning;

	private bool _standingDown;

	private void OpenCopiesWarning()
	{
		(int, string[]) value = _copiesWarning.Value;
		_copiesWarning = null;
		_copiesNotice = null;
		_welcome.OpenWarning("Two copies of enRAGE", (value.Item1 == 1) ? "Another copy of the mod is installed alongside this one" : $"{value.Item1} other copies of the mod are installed alongside this one", new string[2]
		{
			"Both copies run at once: the menus draw over each other (overlapping text, names off their boxes), and the world is played - and saved - twice. An old copy can overwrite your world with an older format.",
			(value.Item1 == 1) ? "The extra copy is here:" : ("The extra copies are here" + ((value.Item2.Length > 3) ? " (the first 3)" : "") + ":")
		}, string.Join("\n", value.Item2.Take(3)), new string[3] { "Quit the game.", "Delete the extra file above (or the whole folder it's in, if it came from an old zip).", "Keep one copy - the newest - as scripts\\NemesisMod.dll, then start the game again." });
		GrudgeLog("COPIES warning window shown");
	}

	private void CheckOtherCopies()
	{
		Assembly assembly = typeof(NemesisMod).Assembly;
		string text = SafePath(assembly);
		_copyId = $"{Theme.ModVersion}|{text}|{Guid.NewGuid():N}";
		Version version = assembly.GetName().Version;
		bool flag = string.Equals(Path.GetFileName(text), "NemesisMod.dll", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(Path.GetDirectoryName(text) ?? ""), "scripts", StringComparison.OrdinalIgnoreCase);
		bool flag2 = false;
		List<string> list = new List<string>();
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly2 in assemblies)
		{
			if (assembly2 != assembly && assembly2.GetName().Name == assembly.GetName().Name)
			{
				list.Add($"{SafePath(assembly2)} (v{assembly2.GetName().Version}, loaded)");
				if (assembly2.GetName().Version > version || (assembly2.GetName().Version == version && !flag && IsMainFile(SafePath(assembly2))))
				{
					flag2 = true;
				}
			}
		}
		try
		{
			string directoryName = Path.GetDirectoryName(text);
			if (!string.IsNullOrEmpty(directoryName))
			{
				foreach (string f in Directory.EnumerateFiles(directoryName, "*.dll", SearchOption.AllDirectories))
				{
					if (string.Equals(Path.GetFullPath(f), Path.GetFullPath(text), StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					string text2 = null;
					try
					{
						text2 = AssemblyName.GetAssemblyName(f).Name;
					}
					catch
					{
					}
					if (!(text2 != assembly.GetName().Name))
					{
						if (!list.Any((string o) => o.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
						{
							list.Add(f);
						}
						Version version2 = null;
						try
						{
							version2 = AssemblyName.GetAssemblyName(f).Version;
						}
						catch
						{
						}
						if (version2 != null && (version2 > version || (version2 == version && !flag && IsMainFile(f))))
						{
							flag2 = true;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("COPIES couldn't look through scripts\\: " + ex.Message);
		}
		if (flag2)
		{
			_standingDown = true;
			GrudgeLog($"COPIES this copy ({text}, v{version}) stands down - a newer copy, or scripts\\NemesisMod.dll, runs instead");
			return;
		}
		if (list.Count == 0)
		{
			GrudgeLog("COPIES only this copy of the mod is here");
		}
		else
		{
			GrudgeLog(string.Format("COPIES {0} other cop{1} of the mod besides {2} - two copies draw over each other:", list.Count, (list.Count == 1) ? "y" : "ies", text));
			foreach (string item in list)
			{
				GrudgeLog("COPIES   " + item);
			}
			string p = list[0].Split(' ')[0];
			_copiesNotice = "enRAGE: another copy of the mod is installed (" + ShortScriptsPath(p) + "). Two copies draw over each other - delete the extra one and press Insert. (Details: grudges.log, COPIES.)";
			_copiesWarning = (list.Count, list.Select((string o) => o.Split(new string[1] { " (v" }, StringSplitOptions.None)[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
		}
		try
		{
			if (!(AppDomain.CurrentDomain.GetData("enRAGE.active") is string text3) || text3.IndexOf("|" + text + "|", StringComparison.OrdinalIgnoreCase) >= 0 || NewerThan(_copyId, text3))
			{
				AppDomain.CurrentDomain.SetData("enRAGE.active", _copyId);
			}
		}
		catch
		{
		}
	}

	private bool ActiveCopy()
	{
		try
		{
			string text = AppDomain.CurrentDomain.GetData("enRAGE.active") as string;
			bool flag = text == null || text == _copyId;
			if (!flag && !_standingDown)
			{
				_standingDown = true;
				GrudgeLog("COPIES a newer copy runs (" + text + ") - this one (" + _copyId + ") stands down");
			}
			return flag;
		}
		catch
		{
			return true;
		}
	}

	private static bool IsMainFile(string p)
	{
		try
		{
			return string.Equals(Path.GetFileName(p), "NemesisMod.dll", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(Path.GetDirectoryName(p) ?? ""), "scripts", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static bool NewerThan(string a, string b)
	{
		return V(a) > V(b);
		static Version V(string id)
		{
			try
			{
				return new Version(id.Split('|')[0]);
			}
			catch
			{
				return new Version(0, 0);
			}
		}
	}

	private static string SafePath(Assembly a)
	{
		try
		{
			string codeBase = a.CodeBase;
			if (!string.IsNullOrEmpty(codeBase))
			{
				return new Uri(codeBase).LocalPath;
			}
		}
		catch
		{
		}
		try
		{
			return a.Location;
		}
		catch
		{
			return "?";
		}
	}

	private static string ShortScriptsPath(string p)
	{
		int num = p.IndexOf("scripts", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return Path.GetFileName(p);
		}
		return p.Substring(num);
	}
}
