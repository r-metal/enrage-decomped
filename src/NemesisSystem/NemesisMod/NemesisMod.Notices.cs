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
	private sealed class PerfStat
	{
		public double Total;

		public double Worst;

		public double First;

		public double Waited;

		public int Calls;

		public int Over16;
	}

	private readonly Dictionary<string, PerfStat> _perf = new Dictionary<string, PerfStat>();

	private const int PerfEveryMs = 600000;

	private int _perfSince = Environment.TickCount;

	private int[] _gcAt = new int[3]
	{
		GC.CollectionCount(0),
		GC.CollectionCount(1),
		GC.CollectionCount(2)
	};

	internal static readonly string[] NameCardModes = new string[4] { "Off", "Known", "Everyone", "On target" };

	private static readonly int[] NameCardOrder = new int[4] { 0, 3, 1, 2 };

	private static readonly int[] MessageOrder = new int[5] { 3, 2, 1, 4, 0 };

	private bool _personalitiesAssigned;

	private const int FallenKeep = 200;

	private void WritePerf(bool force = false)
	{
		int num = Environment.TickCount - _perfSince;
		if (!force && num < 600000)
		{
			return;
		}
		_perfSince = Environment.TickCount;
		if (!_perf.TryGetValue("tick", out var value) || value.Calls == 0)
		{
			return;
		}
		int calls = value.Calls;
		int[] array = new int[3]
		{
			GC.CollectionCount(0),
			GC.CollectionCount(1),
			GC.CollectionCount(2)
		};
		PerfStat value2;
		double num2 = ((_perf.TryGetValue("(outside the systems)", out value2) && value2.Calls > 0) ? (value2.Total / (double)calls) : 0.0);
		GrudgeLog($"PERF {(double)num / 60000.0:0.0} min, {calls} frames: the mod {value.Total / (double)calls:0.00} ms a frame (worst {value.Worst:0}, over 16 ms {value.Over16}x; outside the systems {num2:0.00}) | " + $"slow frames (50 ms+) {_slowFrames}, the mod's {_slowFramesMod} | " + $"GC {array[0] - _gcAt[0]} / {array[1] - _gcAt[1]} / {array[2] - _gcAt[2]} (gen 0/1/2) | mod {(double)GC.GetTotalMemory(forceFullCollection: false) / 1048576.0:0} MB");
		_gcAt = array;
		foreach (KeyValuePair<string, PerfStat> item in (from kv in _perf
			where kv.Key != "tick" && kv.Value.Calls > 0
			orderby kv.Value.Total descending
			select kv).Take(15).ToList())
		{
			GrudgeLog(string.Format("PERF   {0,-22} {1,6:0.00} ms a frame  {2:0.0} s in all  worst {3,4:0}  over 16 ms {4}x{5}", item.Key, item.Value.Total / (double)calls, item.Value.Total / 1000.0, item.Value.Worst, item.Value.Over16, (item.Value.Waited >= 100.0) ? $"  (+{item.Value.Waited / 1000.0:0.0} s waiting for models)" : ""));
		}
		List<KeyValuePair<string, PerfStat>> list = (from kv in _perf
			where kv.Value.First >= 5.0
			orderby kv.Value.First descending
			select kv).Take(8).ToList();
		if (list.Count > 0)
		{
			GrudgeLog("PERF   first calls (compiling, once): " + string.Join(", ", list.Select((KeyValuePair<string, PerfStat> kv) => $"{kv.Key} {kv.Value.First:0}")));
		}
		foreach (PerfStat value3 in _perf.Values)
		{
			value3.Total = (value3.Worst = (value3.Waited = 0.0));
			value3.Calls = (value3.Over16 = 0);
			value3.First = 0.0;
		}
		_slowFrames = (_slowFramesMod = 0);
	}

	internal void GrudgeNotice(string text)
	{
		if (Config.GrudgeMessages)
		{
			ShowNotification(AboutHer(text));
		}
	}

	private static string ExitVerdict(string lastLine, out bool crashed)
	{
		crashed = true;
		DateTime dateTime;
		try
		{
			TimeSpan timeSpan = TimeSpan.ParseExact(lastLine.Substring(0, 12), "hh\\:mm\\:ss\\.fff", CultureInfo.InvariantCulture);
			dateTime = DateTime.Today + timeSpan;
			if (dateTime > DateTime.Now)
			{
				dateTime = dateTime.AddDays(-1.0);
			}
		}
		catch
		{
			return "the last run stopped (time unreadable)";
		}
		DateTime? dateTime2 = null;
		string arg = null;
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
			if (Directory.Exists(path))
			{
				string[] files = Directory.GetFiles(path, "RDR2.exe.*.dmp");
				foreach (string path2 in files)
				{
					DateTime lastWriteTime = File.GetLastWriteTime(path2);
					if (!(lastWriteTime >= dateTime.AddSeconds(-2.0)) || !(lastWriteTime <= DateTime.Now))
					{
						continue;
					}
					if (dateTime2.HasValue)
					{
						DateTime value = lastWriteTime;
						DateTime? dateTime3 = dateTime2;
						if (!(value < dateTime3))
						{
							continue;
						}
					}
					dateTime2 = lastWriteTime;
					arg = "crash dump " + Path.GetFileName(path2);
				}
			}
		}
		catch
		{
		}
		try
		{
			string path3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rockstar Games", "Red Dead Redemption 2", "CrashLogs", "reports");
			if (Directory.Exists(path3))
			{
				string[] files = Directory.GetFiles(path3, "*.dmp");
				foreach (string path4 in files)
				{
					DateTime lastWriteTime2 = File.GetLastWriteTime(path4);
					if (!(lastWriteTime2 >= dateTime.AddSeconds(-2.0)) || !(lastWriteTime2 <= DateTime.Now))
					{
						continue;
					}
					if (dateTime2.HasValue)
					{
						DateTime value = lastWriteTime2;
						DateTime? dateTime3 = dateTime2;
						if (!(value < dateTime3))
						{
							continue;
						}
					}
					dateTime2 = lastWriteTime2;
					arg = "the game's own crash report " + Path.GetFileName(path4);
				}
			}
		}
		catch
		{
		}
		try
		{
			using EventLog eventLog = new EventLog("Application");
			EventLogEntryCollection entries = eventLog.Entries;
			int num = entries.Count - 1;
			for (int j = 0; num >= 0 && j < 400; num--, j++)
			{
				EventLogEntry eventLogEntry = entries[num];
				if (eventLogEntry.TimeGenerated < dateTime.AddSeconds(-2.0))
				{
					break;
				}
				if (eventLogEntry.InstanceId % 65536 != 1000 || eventLogEntry.Message == null || !eventLogEntry.Message.Contains("RDR2.exe"))
				{
					continue;
				}
				int num2 = eventLogEntry.Message.IndexOf("Faulting module name:");
				string text = ((num2 >= 0) ? eventLogEntry.Message.Substring(num2 + 21).Split(',')[0].Trim() : "?");
				if (dateTime2.HasValue)
				{
					DateTime value = eventLogEntry.TimeGenerated;
					DateTime? dateTime3 = dateTime2;
					if (!(value < dateTime3))
					{
						goto IL_032d;
					}
				}
				dateTime2 = eventLogEntry.TimeGenerated;
				goto IL_032d;
				IL_032d:
				arg = "Windows: RDR2.exe crashed in " + text;
			}
		}
		catch
		{
		}
		if (!dateTime2.HasValue)
		{
			crashed = false;
			return $"the last run ended at {dateTime:HH:mm:ss} with no crash - you closed the game";
		}
		double totalSeconds = (dateTime2.Value - dateTime).TotalSeconds;
		if (totalSeconds > 600.0)
		{
			crashed = false;
			return $"the game crashed at {dateTime2:MMM d HH:mm} ({arg}) - a later start that died before the scripts loaded; the last run before it ended at {dateTime:HH:mm:ss}";
		}
		if (totalSeconds > 3.0)
		{
			return $"the game crashed {totalSeconds:0} s after the scripts' last tick ({arg}) - a freeze, or while closing or in a menu";
		}
		return $"the game CRASHED in play at {dateTime2:HH:mm:ss} ({arg})";
	}

	private bool WelcomeCalm()
	{
		Ped character = Game.Player.Character;
		if (_combatants.Values.Any((int t) => Game.GameTime - t < 15000) || (_enc != null && _enc.Engaged))
		{
			return false;
		}
		if (PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) > 0 || PED.IS_PED_IN_MELEE_COMBAT(character.Handle) || PED.IS_PED_SHOOTING(character.Handle))
		{
			return false;
		}
		if (character.IsOnMount && ENTITY.GET_ENTITY_SPEED(character.Handle) > 3f)
		{
			return false;
		}
		return !WorldBusy(character);
	}

	internal void ReplyNotice(string text)
	{
		if (Config.MessageLevel != 0)
		{
			ShowNotification(AboutHer(text));
		}
	}

	private string AboutHer(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		NemesisRecord her = Roster.FirstOrDefault((NemesisRecord r) => r.Name != null && Models.Female(r.ModelHash) && text.Contains(r.Name));
		if (her == null || Roster.Any((NemesisRecord r) => r != her && r.Name != null && !Models.Female(r.ModelHash) && text.Contains(r.Name)))
		{
			return text;
		}
		return Pron.Her(text, female: true);
	}

	internal void DeathNotice(string text)
	{
		if (Config.DeathMessages)
		{
			ShowNotification(AboutHer(text));
		}
	}

	internal void WorldNotice(string text)
	{
		if (Config.WorldNews > 0)
		{
			ShowNotification(AboutHer(text));
		}
	}

	internal void CycleWorldNews()
	{
		Config.WorldNews = (Config.WorldNews + 3) % 4;
		Config.Save();
		ShowNotification("enRAGE - World News: " + NemesisSettings.WorldNewsShort[Config.WorldNews]);
	}

	private void ShowSubtitle(string text)
	{
		if (Config.SpeechMessages)
		{
			RDR2.UI.Screen.PrintSubtitle(UiDraw.TheFix(text));
		}
	}

	private int NextNameCards(int d)
	{
		int num = Array.IndexOf(NameCardOrder, Config.NameCards);
		if (num < 0)
		{
			num = 0;
		}
		return NameCardOrder[(num + d + NameCardOrder.Length) % NameCardOrder.Length];
	}

	internal void CycleNameCards()
	{
		Config.NameCards = NextNameCards(1);
		Config.Save();
		ShowNotification("enRAGE - Name Cards: " + NameCardModes[Config.NameCards]);
	}

	private static string OnOff(bool b)
	{
		if (!b)
		{
			return "Off";
		}
		return "On";
	}

	private List<WelcomeGuide.SetupRow> SetupRows()
	{
		List<WelcomeGuide.SetupRow> list = SetupRowsAll();
		if (Gangs.Era < 1907 && !IsEpiloguePlayer())
		{
			int num = list.FindIndex((WelcomeGuide.SetupRow r) => r.Section == "Controls");
			list.Insert(num++, new WelcomeGuide.SetupRow
			{
				Section = "Your story",
				Label = "Lindsey Wofford",
				Help = "The Raiders' leader is one of the game's bounties. If you already collected it, say how - the Raiders get a new leader.",
				Value = WoffordState,
				Change = SetWofford,
				Confirm = WoffordAsk,
				Locked = WoffordLocked
			});
			if (Chronicle.StoryChapter < 5)
			{
				list.Insert(num++, new WelcomeGuide.SetupRow
				{
					Section = "Your story",
					Label = "Angelo Bronte",
					Help = "If Angelo Bronte is already dead in your story, say so - the mod can't always tell. The Bronte Family get a new leader.",
					Value = BronteState,
					Change = SetBronte,
					Confirm = BronteAsk,
					Locked = BronteLocked
				});
			}
		}
		list.Insert(list.FindIndex((WelcomeGuide.SetupRow r) => r.Section == "Controls"), new WelcomeGuide.SetupRow
		{
			Section = "Your story",
			Label = "Lock lore leaders",
			Help = "On: the outfits' leaders - Colm O'Driscoll, Angelo Bronte, Lindsey Wofford, Rains Fall and the rest - are only met in the game's own missions. Off: each is out in the world too - he holds his outfit's hideout, his men can give it up, and he can be killed. (Col. Favours is at Fort Wallace either way.)",
			Value = () => OnOff(Config.LockLeaders),
			Change = delegate
			{
				Config.LockLeaders = !Config.LockLeaders;
				Config.Save();
				GrudgeLog("SETUP lock lore leaders: " + OnOff(Config.LockLeaders));
			},
			Confirm = (int d) => (!Config.LockLeaders) ? "Turn on Lock lore leaders? The outfits' leaders leave the world and are met only in the game's own missions again. One already killed stays dead." : "Turn off Lock lore leaders? Colm O'Driscoll, Angelo Bronte, Lindsey Wofford and the rest come out into the world. Each holds his outfit's hideout, and one you kill stays dead."
		});
		return list;
	}

	private List<WelcomeGuide.SetupRow> SetupRowsAll()
	{
		return new List<WelcomeGuide.SetupRow>
		{
			new WelcomeGuide.SetupRow
			{
				Section = "On screen",
				Label = "Messages",
				Help = "What pops up: everything, speech and deaths, speech only, deaths only, or nothing. (N in the menu)",
				Value = () => NemesisSettings.MessageLevelShort[Config.MessageLevel],
				Change = delegate(int d)
				{
					int num = Array.IndexOf(MessageOrder, Config.MessageLevel);
					if (num < 0)
					{
						num = 0;
					}
					Config.MessageLevel = MessageOrder[(num + d + MessageOrder.Length) % MessageOrder.Length];
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "On screen",
				Label = "World news",
				Help = "Which world events pop up: all of them, the big news, deaths only, or none. The World tab has everything either way.",
				Value = () => NemesisSettings.WorldNewsShort[Config.WorldNews],
				Change = delegate(int d)
				{
					Config.WorldNews = (Config.WorldNews + d + 4) % 4;
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "On screen",
				Label = "Name cards",
				Help = "Names over people's heads: off, only the one you target (hold LT / right-click on them), the people you know, or everyone. (C in the menu)",
				Value = () => NameCardModes[Config.NameCards],
				Change = delegate(int d)
				{
					Config.NameCards = NextNameCards(d);
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "On screen",
				Label = "War banner",
				Help = "The war you're standing in, shown while you hold the game's HUD button (D-pad Down).",
				Value = () => OnOff(Config.WarHud),
				Change = delegate
				{
					Config.WarHud = !Config.WarHud;
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "The world",
				Label = "Encounters",
				Help = "How often someone comes for you: Fewer, Normal, More or Lots.",
				Value = () => NemesisSettings.EncounterAmountNames[Config.EncounterAmount],
				Change = delegate(int d)
				{
					Config.EncounterAmount = Math.Max(0, Math.Min(3, Config.EncounterAmount + d));
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "The world",
				Label = "Distant battles",
				Help = "Near a war, a fight now and then breaks out out of sight - you hear it and can ride over.",
				Value = () => OnOff(Config.DistantBattles),
				Change = delegate
				{
					Config.DistantBattles = !Config.DistantBattles;
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "The world",
				Label = "Pause in menus",
				Help = "Stop the clock and freeze the world while the enRAGE menu or the X-ray log is open (after a minute it runs on in slow motion). Not during a mission: pausing then can crash the game.",
				Value = () => OnOff(Config.PauseInMenus),
				Change = delegate
				{
					Config.PauseInMenus = !Config.PauseInMenus;
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "Controls",
				Label = "Menu key",
				IsKey = true,
				Help = "Opens the enRAGE menu. Enter, then the new key - or a combination like Shift+F9 if another mod uses it. (The controller's way is the next row.)",
				Value = () => KeyCombo.Name(Config.RosterMenuKey)
			},
			new WelcomeGuide.SetupRow
			{
				Section = "Controls",
				Label = "Controller: open menu",
				Help = "How a controller opens the enRAGE menu. D-pad Up stays the game's (whistle) and Interrogate. Off: the keyboard key only.",
				Value = () => NemesisSettings.PadMenuOpenNames[Config.PadMenuOpen].Replace("Double-tap ", "2x ").Replace("D-pad ", ""),
				Change = delegate(int d)
				{
					Config.PadMenuOpen = (Config.PadMenuOpen + d + 5) % 5;
					KeyCombo.PadMode = Config.PadMenuOpen;
					Config.Save();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "More",
				Label = "Debug tools",
				Help = "For testing: " + KeyCombo.Keyed("Ctrl+F11") + " shows the debug overlay, Ctrl+F10 the encounter spawner. Leave off for normal play.",
				Value = () => OnOff(Config.DebugEnabled),
				Change = delegate
				{
					Config.DebugEnabled = !Config.DebugEnabled;
					IniPanel.Dev = Config.DebugEnabled;
					Config.Save();
					if (!Config.DebugEnabled && _debugOn)
					{
						DebugMode = DebugModeNames.Length - 1;
						ToggleDebug();
					}
					GrudgeLog("SETUP debug tools " + (Config.DebugEnabled ? "on" : "off"));
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "More",
				Label = "What's new",
				Help = "What changed in version " + Theme.ModVersion + ".",
				Value = () => "Open",
				Run = delegate
				{
					_welcome.ShowWhatsNewFromSetup();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Section = "More",
				Label = "All settings",
				Help = "Every setting the mod has, in one long list (Alt+F11 opens it too).",
				Value = () => "Open",
				Run = delegate
				{
					_welcome.CloseSetup();
					_iniPanel.Toggle();
				}
			},
			new WelcomeGuide.SetupRow
			{
				Label = "Confirm",
				IsStart = true,
				Help = "Everything else is under All settings (Alt+F11)."
			}
		};
	}

	internal void CycleMessages()
	{
		Config.MessageLevel = ((Config.MessageLevel == 3) ? 2 : ((Config.MessageLevel == 2) ? 1 : ((Config.MessageLevel == 1) ? 4 : ((Config.MessageLevel != 4) ? 3 : 0))));
		Config.Save();
		ShowNotification("enRAGE - Messages: " + NemesisSettings.MessageLevelShort[Config.MessageLevel]);
	}
}
