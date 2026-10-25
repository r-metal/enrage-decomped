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
	private bool _worldDirty;

	private int _nextWorldSave;

	private int _lastRosterFlushAt = -100000;

	private const int MixedDays = 30;

	private const int MaxWorldEvents = 5000;

	private bool _worldCatchingUp;

	private int _catchUpHours;

	private string WorldPath => Path.Combine(_saveDir, "world.json");

	private WorldState Chronicle
	{
		get
		{
			if (_chron != null)
			{
				return _chron;
			}
			_chron = SafeFile.Read<WorldState>(WorldPath, GrudgeLog) ?? new WorldState();
			if (Gangs.SetEra(_chron.Era))
			{
				GrudgeLog($"EPILOGUE this world is in {Gangs.Era}: {Gangs.All.Length} outfits ride");
			}
			return _chron;
		}
	}

	private void SaveWorld()
	{
		_worldDirty = true;
	}

	private void FlushWorld()
	{
		if (_worldDirty && Game.GameTime >= _nextWorldSave && Game.GameTime - _lastRosterFlushAt >= 4000 && !Slicing("world"))
		{
			_nextWorldSave = Game.GameTime + 60000;
			SaveWorldNow(background: true, sliced: true);
		}
	}

	internal void CleanMixedTimeline(long now)
	{
		WorldState chronicle = Chronicle;
		long lo;
		long hi;
		if (!chronicle.MixedTimelineCleaned && now > 0)
		{
			chronicle.MixedTimelineCleaned = true;
			lo = now - 43200;
			hi = now + 60;
			int num = chronicle.Events.RemoveAll((WorldEvent e) => !e.IsPast && Out(e.GameMin));
			int num2 = chronicle.Telegrams?.RemoveAll((Telegram t) => Out(t.SentAt)) ?? 0;
			int num3 = (chronicle.DeathNews?.RemoveAll((DeathNews n) => Out(n.DueAt)) ?? 0) + (chronicle.PeopleNews?.RemoveAll((DeathNews n) => Out(n.DueAt)) ?? 0);
			int num4 = chronicle.Plans?.RemoveAll((Plan p) => Out(p.DueAt)) ?? 0;
			SaveWorld();
			GrudgeLog($"TIMELINE clean-up: {num} events, {num2} telegrams, {num3} news and {num4} plans from another playthrough taken out (more than {30} days from {GameDate.FromMinutes(now):MMM d})");
		}
		bool Out(long m)
		{
			if (m > 0)
			{
				if (m >= lo)
				{
					return m > hi;
				}
				return true;
			}
			return false;
		}
	}

	private void SaveWorldNow(bool background = false, bool sliced = false)
	{
		if (sliced && Slicing("world"))
		{
			return;
		}
		if (!background && !SafeFile.WaitWrites())
		{
			background = true;
		}
		try
		{
			WorldState chronicle = Chronicle;
			if (chronicle.Events.Count > 5000)
			{
				chronicle.Events.RemoveRange(0, chronicle.Events.Count - 5000);
			}
			StampEventShifts(0L);
			foreach (WorldEvent @event in chronicle.Events)
			{
				if (@event.Title != null && @event.Title.Contains("he The "))
				{
					@event.Title = UiDraw.TheFix(@event.Title);
				}
				if (@event.Place != null && @event.Place.Contains("he The "))
				{
					@event.Place = UiDraw.TheFix(@event.Place);
				}
				if (@event.Lines == null)
				{
					continue;
				}
				for (int i = 0; i < @event.Lines.Count; i++)
				{
					if (@event.Lines[i] != null && @event.Lines[i].Contains("he The "))
					{
						@event.Lines[i] = UiDraw.TheFix(@event.Lines[i]);
					}
				}
			}
			if (chronicle.RelationWhy != null)
			{
				foreach (string item in chronicle.RelationWhy.Keys.ToList())
				{
					if (chronicle.RelationWhy[item] != null && chronicle.RelationWhy[item].Contains("he The "))
					{
						chronicle.RelationWhy[item] = UiDraw.TheFix(chronicle.RelationWhy[item]);
					}
				}
			}
			if (sliced)
			{
				_worldDirty = false;
				StartSlice("world", WorldPath, chronicle, _worldGen, delegate(Exception err)
				{
					if (err != null)
					{
						_worldDirty = true;
					}
				});
				return;
			}
			_worldGen++;
			string text = JsonConvert.SerializeObject(chronicle);
			if (background)
			{
				_worldDirty = false;
				SafeFile.WriteLater(WorldPath, text, keepBak: true, delegate(Exception err)
				{
					if (err != null)
					{
						_worldDirty = true;
					}
				});
			}
			else
			{
				SafeWrite(WorldPath, text);
				_worldDirty = false;
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("WORLD save failed: " + ex.Message);
		}
	}

	internal static void SafeWrite(string path, string text)
	{
		SafeFile.Write(path, text, keepBak: true);
	}

	internal void KeepBadFile(string path, Exception ex)
	{
		GrudgeLog("LOAD failed for " + Path.GetFileName(path) + ": " + ex.Message + " - kept as .bad");
		try
		{
			byte[] array = File.ReadAllBytes(path);
			string[] files = Directory.GetFiles(Path.GetDirectoryName(path) ?? ".", Path.GetFileName(path) + ".bad-*");
			foreach (string text in files)
			{
				if (new FileInfo(text).Length == array.Length && File.ReadAllBytes(text).SequenceEqual(array))
				{
					return;
				}
			}
			File.Copy(path, path + $".bad-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
		}
		catch
		{
		}
	}

	private void PruneSideCopies()
	{
		int gone = 0;
		try
		{
			foreach (string item in new List<string>
			{
				_saveDir,
				ContentDir,
				Path.Combine(ContentDir, "encounters")
			})
			{
				Prune(item, ".before-update-", 1);
				Prune(item, ".unreadable-", 2);
				Prune(item, ".bad-", 2);
				Prune(item, ".before-restore-", 2);
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("LOAD side-copy prune failed: " + ex.Message);
		}
		if (gone > 0)
		{
			GrudgeLog($"LOAD removed {gone} old side copies (.before-update / .unreadable / .bad / .before-restore)");
		}
		void Prune(string dir, string tag, int keep)
		{
			if (!Directory.Exists(dir))
			{
				return;
			}
			foreach (IGrouping<string, string> item2 in from f in Directory.GetFiles(dir, "*" + tag + "*")
				group f by f.Substring(0, f.IndexOf(tag, StringComparison.Ordinal)))
			{
				foreach (string item3 in item2.OrderByDescending((string f) => Path.GetFileName(f), StringComparer.Ordinal).Skip(keep))
				{
					try
					{
						File.Delete(item3);
						gone++;
					}
					catch
					{
					}
				}
			}
		}
	}
}
