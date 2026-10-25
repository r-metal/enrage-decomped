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
	private string ContentDir => Path.Combine(_saveDir, "content");

	private void LoadContent()
	{
		try
		{
			Directory.CreateDirectory(ContentDir);
		}
		catch
		{
		}
		int num = LoadFactions();
		LoadOrWrite(Path.Combine(ContentDir, "places.json"), ExportPlaces, ImportPlaces);
		LoadOrWrite(Path.Combine(ContentDir, "wildcards.json"), ExportWildcards, ImportWildcards);
		GrudgeLog($"CONTENT factions.json: {num} factions - places.json: {Districts.Length} districts, {TownSpots.Count} towns - wildcards.json: {TurmoilKinds.Count} kinds, {BadBlood.Length} bad-blood / {Deals.Length} deal lines");
		int num2 = LoadOrWrite(Path.Combine(ContentDir, "lives.json"), Backstory.Export, (LivesContent c) => Backstory.Import(c));
		int num3 = LoadOrWrite(Path.Combine(ContentDir, "tales.json"), ExportTales, ImportTales);
		int num4 = LoadOrWrite(Path.Combine(ContentDir, "world_life.json"), WorldLife.Export, WorldLife.Import);
		int num5 = LoadOrWrite(Path.Combine(ContentDir, "questions.json"), Questions.Export, Questions.Import);
		GrudgeLog(string.Format("CONTENT questions.json: {0} questions{1}", num5, (Questions.AddedFromBuiltIn > 0) ? $" (+{Questions.AddedFromBuiltIn} new built-in merged by Id)" : ""));
		GrudgeLog(string.Format("CONTENT world_life.json: {0} everyday events{1}", num4, (WorldLife.AddedFromBuiltIn > 0) ? $" (+{WorldLife.AddedFromBuiltIn} new built-in rows merged by Id)" : ""));
		GrudgeLog(string.Format("CONTENT lives.json: {0} life events{1} - tales.json: {2} lore and tales", num2, (Backstory.AddedFromBuiltIn > 0) ? $" (+{Backstory.AddedFromBuiltIn} built-in rows for factions the file didn't know)" : "", num3));
		LoadCards();
		LoadCampSites();
		LoadDoors();
	}

	internal static string TextHash(string s)
	{
		using SHA1 sHA = SHA1.Create();
		return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""))).Replace("-", "");
	}

	internal void RefreshBuiltIn(string path, string builtIn)
	{
		string path2 = path + ".builtin";
		try
		{
			if (!File.Exists(path))
			{
				SafeFile.Write(path, builtIn);
				File.WriteAllText(path2, TextHash(builtIn));
				return;
			}
			string text = File.ReadAllText(path);
			string text2 = TextHash(text);
			string text3 = (File.Exists(path2) ? File.ReadAllText(path2).Trim() : null);
			if (text == builtIn)
			{
				if (text3 != text2)
				{
					File.WriteAllText(path2, text2);
				}
			}
			else if (text3 == null || !(text3 != text2))
			{
				File.Copy(path, path + ".before-update-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), overwrite: true);
				SafeFile.Write(path, builtIn);
				File.WriteAllText(path2, TextHash(builtIn));
				GrudgeLog("CONTENT " + Path.GetFileName(path) + ": the built-in content changed - file updated (old copy kept as .before-update-*)");
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("CONTENT " + Path.GetFileName(path) + ": built-in refresh failed - " + ex.Message);
		}
	}

	private int LoadOrWrite<T>(string path, Func<T> export, Func<T, int> import) where T : class
	{
		T val = null;
		string text = null;
		try
		{
			val = export();
			text = JsonConvert.SerializeObject(val, Formatting.Indented, new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				DefaultValueHandling = DefaultValueHandling.Ignore
			});
		}
		catch (Exception ex)
		{
			GrudgeLog("CONTENT " + Path.GetFileName(path) + ": built-in export failed - " + ex.Message);
		}
		try
		{
			if (text != null)
			{
				RefreshBuiltIn(path, text);
			}
			if (File.Exists(path))
			{
				T arg = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
				int num = import(arg);
				if (num > 0)
				{
					return num;
				}
				GrudgeLog("CONTENT " + Path.GetFileName(path) + " had nothing usable - built-in content used");
			}
			else if (text != null)
			{
				SafeFile.Write(path, text);
			}
		}
		catch (Exception ex2)
		{
			KeepBadFile(path, ex2);
		}
		if (val == null)
		{
			return 0;
		}
		try
		{
			return import(val);
		}
		catch (Exception ex3)
		{
			GrudgeLog("CONTENT " + Path.GetFileName(path) + ": the built-in content failed to load too - " + ex3.Message);
			return 0;
		}
	}

	private static TalesContent ExportTales()
	{
		TalesContent talesContent = new TalesContent
		{
			Lore = LoreRows.ToList()
		};
		foreach (KeyValuePair<string, (string, string, string[], string, int, string)[]> gangTale in GangTales)
		{
			talesContent.Tales[gangTale.Key] = gangTale.Value.Select(((string kind, string title, string[] places, string foe, int heard, string line) t) => new TaleRow
			{
				Kind = t.kind,
				Title = t.title,
				Places = t.places.ToList(),
				Foe = t.foe,
				HeardPercent = t.heard,
				Line = t.line
			}).ToList();
		}
		return talesContent;
	}

	private static int ImportTales(TalesContent c)
	{
		if (c == null)
		{
			return 0;
		}
		int num = 0;
		if (c.Lore != null)
		{
			c.Lore.RemoveAll((LoreRow r) => r == null);
		}
		if (c.Lore != null && c.Lore.Count > 0)
		{
			LoreRows = c.Lore;
			num += c.Lore.Count;
		}
		FixLore(LoreRows);
		if (c.Tales != null)
		{
			foreach (KeyValuePair<string, List<TaleRow>> item in c.Tales.Where((KeyValuePair<string, List<TaleRow>> kv) => kv.Key != null && kv.Value != null && kv.Value.Any((TaleRow t) => t != null)))
			{
				GangTales[item.Key] = (from t in item.Value
					where t != null && !string.IsNullOrEmpty(t.Title)
					select (t.Kind ?? "RAID", Title: t.Title, (t.Places ?? new List<string>()).DefaultIfEmpty("the backcountry").ToArray(), Foe: t.Foe, HeardPercent: t.HeardPercent, t.Line ?? "")).ToArray();
				num += item.Value.Count;
			}
		}
		return num;
	}

	private static string SaidText(Convo c)
	{
		if (c.Kind != ConvoKind.Interrogate || !((c.It?.Gang?.Id ?? c.Rec?.Gang) == Gangs.NightFolk.Id))
		{
			return "\"" + c.Line + "\"";
		}
		string text = NightFolkSilence[(uint)(c.Line ?? "").GetHashCode() % (uint)NightFolkSilence.Length];
		if (!text.StartsWith("*"))
		{
			return "\"" + text + "\"";
		}
		return text;
	}

	private static string Pick(string[] lines, Random rng)
	{
		return lines[rng.Next(lines.Length)];
	}
}
