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
	private int _lastSaveSeenAt = -100000;

	private int _loadSeenAt = -100000;

	private int _undoUntil;

	private bool _savePending;

	private bool _clockChecked;

	private int _pointDueAt;

	private int _pointBurstStart;

	private int _pointBurst;

	private int _savePointsStartAt;

	private int _nextClockStore;

	private long _lastClockMin = -1L;

	private long _lastHourlyMin = -1L;

	private Vector3 _lastClockPos;

	private string _undoPoint;

	private string _undoSlot;

	private bool _undoIsFresh;

	internal string SavePointNote = "";

	private int _lastHourlyReal = -1000000;

	private int _clockRaceUntil;

	private int _loadScreenAt = -100000;

	private readonly List<Task> _pointWrites = new List<Task>();

	private const int LaterSaveDays = 3;

	private Action _afterWrites;

	private string _afterWritesWhat;

	private int _afterWritesSince;

	private int _afterWritesLogAt;

	private string _restoreSafety;

	private Dictionary<string, DateTime> _slotSnap;

	private string _slotPendingDir;

	private string _currentSlot;

	private int _slotPendingUntil;

	private volatile string _savePointError;

	private volatile string _savePointCapNote;

	private bool _pruneDue;

	private List<(string name, Action flush, Action force)> _saves;

	private const int SaveHoldInFightMs = 25000;

	private int _fightSaveHoldSince = -1;

	private bool _pauseFlushed;

	private int _nextHiddenSave;

	private readonly List<SliceJob> _slices = new List<SliceJob>();

	private readonly List<Action> _afterSlices = new List<Action>();

	private int _rosterGen;

	private int _worldGen;

	private const double SliceBudgetMs = 2.0;

	private const double SliceBudgetFightMs = 1.0;

	private readonly Stopwatch _saveClock = Stopwatch.StartNew();

	private int _xraySavesSeen;

	private string SavePointDir => Path.Combine(_saveDir, "savepoints");

	private string ClockPath => Path.Combine(_saveDir, "clock.txt");

	internal bool WorldClockSettled
	{
		get
		{
			if (Config.SavePointsEnabled)
			{
				return _clockChecked;
			}
			return true;
		}
	}

	private void UpdateSavePoints(Ped player)
	{
		if (!Config.SavePointsEnabled || Game.GameTime < _nextSavePointCheck)
		{
			return;
		}
		_nextSavePointCheck = Game.GameTime + 250;
		string savePointCapNote = _savePointCapNote;
		if (savePointCapNote != null)
		{
			_savePointCapNote = null;
			GrudgeLog("SAVEPOINT " + savePointCapNote);
		}
		if (_pruneDue)
		{
			bool flag;
			lock (_pointWrites)
			{
				flag = _pointWrites.Any((Task x) => !x.IsCompleted);
			}
			if (!flag && _afterWrites == null && _clockChecked && Game.GameTime - _loadSeenAt > 30000)
			{
				_pruneDue = false;
				SafeFile.RunLater(delegate
				{
					try
					{
						PruneSavePoints();
					}
					catch (Exception ex4)
					{
						_savePointCapNote = "prune failed: " + ex4.Message;
					}
				});
			}
		}
		if (SCRIPTS.IS_LOADING_SCREEN_VISIBLE() || DLC.GET_IS_LOADING_SCREEN_ACTIVE() || CAM.IS_SCREEN_FADED_OUT())
		{
			_loadSeenAt = Game.GameTime;
		}
		if (SCRIPTS.IS_LOADING_SCREEN_VISIBLE() || DLC.GET_IS_LOADING_SCREEN_ACTIVE())
		{
			_loadScreenAt = Game.GameTime;
		}
		if (SCRIPTS.IS_LOADING_SCREEN_VISIBLE() || DLC.GET_IS_LOADING_SCREEN_ACTIVE())
		{
			return;
		}
		if (_afterWrites != null)
		{
			if (WritesSettled(0))
			{
				Action afterWrites = _afterWrites;
				_afterWrites = null;
				GrudgeLog($"SAVEPOINT {_afterWritesWhat}: the writer's done ({(float)(Game.GameTime - _afterWritesSince) / 1000f:0.0} s) - now");
				afterWrites();
			}
			else if (Game.GameTime - _afterWritesLogAt > 10000)
			{
				_afterWritesLogAt = Game.GameTime;
				GrudgeLog($"SAVEPOINT {_afterWritesWhat}: still waiting for the writer ({(Game.GameTime - _afterWritesSince) / 1000} s)");
			}
			return;
		}
		long num = GameMinutesRaw();
		if (num <= 0)
		{
			return;
		}
		Vector3 position = player.Position;
		if (!_clockChecked)
		{
			if (_savePointsStartAt == 0)
			{
				_savePointsStartAt = Game.GameTime;
			}
			if (Game.GameTime - _savePointsStartAt < 3000 || player.IsDead)
			{
				return;
			}
			_clockChecked = true;
			long num2 = ReadClock();
			_currentSlot = ReadSlot();
			_slotSnap = SlotTimes();
			(string, string) tuple = SlotSavePoint(num, position);
			if (Config.SavePointAutoRestore && num2 > 0 && tuple.Item1 != null && tuple.Item2 != _currentSlot && Math.Abs(num - num2) <= Config.SavePointBackMinutes)
			{
				GrudgeLog("SAVEPOINT at start-up: this is " + SlotName(tuple.Item2) + ", the world was " + SlotName(_currentSlot) + " - its own world");
				RestoreWithUndo(tuple.Item1, "your save (" + SlotName(tuple.Item2) + ")");
			}
			else if (num2 > 0 && num < num2 - Config.SavePointBackMinutes)
			{
				ClockWentBack(num2, num, "at start-up");
			}
			else if (num2 > 0 && num > num2 + Config.SavePointBackMinutes)
			{
				LaterSaveLoaded(num2, num, position, "at start-up");
			}
			else
			{
				long? num3 = EarliestPointMin();
				if (num3.HasValue)
				{
					long valueOrDefault = num3.GetValueOrDefault();
					if (num < valueOrDefault - 1440)
					{
						GrudgeLog($"SAVEPOINT this save ({GameDate.FromMinutes(num):MMM d HH:mm}) is older than every save point ({GameDate.FromMinutes(valueOrDefault):MMM d HH:mm}) - a fresh world for it");
						FreshWorldForSave(GameDate.FromMinutes(num).ToString("MMM d HH:mm"));
						goto IL_03e0;
					}
				}
				HealFutureTimeline(num);
			}
			goto IL_03e0;
		}
		bool flag2 = SAVE.SAVEGAME_IS_SAVE_PENDING();
		if (!_savePending && flag2 && _pointDueAt == 0)
		{
			_slotSnap = SlotTimes();
		}
		if (_savePending && !flag2)
		{
			_lastSaveSeenAt = Game.GameTime;
			if (_pointDueAt == 0)
			{
				_pointBurstStart = Game.GameTime;
				_pointBurst = 0;
			}
			_pointBurst++;
			_pointDueAt = Math.Min(Game.GameTime + 3000, _pointBurstStart + 30000);
		}
		if (_pointDueAt != 0 && !flag2 && Game.GameTime >= _pointDueAt)
		{
			GrudgeLog("SAVEPOINT game save seen (SAVEGAME_IS_SAVE_PENDING ended" + ((_pointBurst > 1) ? $", {_pointBurst} saves in a row - one point" : "") + ")");
			_pointDueAt = 0;
			string text = TakeSavePoint("game save", sync: false, 0L);
			if (text != null)
			{
				_slotPendingDir = text;
				_slotPendingUntil = Game.GameTime + 20000;
			}
		}
		_savePending = flag2;
		CheckSlotWritten();
		bool flag3 = _lastClockPos != Vector3.Zero && position.DistanceTo(_lastClockPos) > 200f;
		if (flag3)
		{
			_loadSeenAt = Game.GameTime;
		}
		if (flag3 && Game.GameTime - _loadScreenAt < 30000 && !MISC.GET_MISSION_FLAG() && Config.SavePointAutoRestore)
		{
			(string, string) tuple2 = SlotSavePoint(num, position);
			if (tuple2.Item1 != null && tuple2.Item2 != _currentSlot)
			{
				GrudgeLog("SAVEPOINT another save loaded (" + SlotName(tuple2.Item2) + ") - its own world");
				RestoreWithUndo(tuple2.Item1, "your save (" + SlotName(tuple2.Item2) + ")");
				_lastClockMin = GameMinutesRaw();
				_lastClockPos = position;
				_lastHourlyMin = _lastClockMin;
				return;
			}
		}
		if (_lastClockMin > 0 && num - _lastClockMin > 5)
		{
			_clockRaceUntil = Game.GameTime + 10000;
		}
		if (_lastClockMin > 0 && num > _lastClockMin + Config.SavePointBackMinutes && flag3 && Game.GameTime - _loadScreenAt < 30000 && !MISC.GET_MISSION_FLAG())
		{
			LaterSaveLoaded(_lastClockMin, num, position, "a load");
			_lastHourlyMin = num;
		}
		else if (_lastClockMin > 0 && num < _lastClockMin - Config.SavePointBackMinutes)
		{
			if (Game.GameTime - _loadScreenAt < 30000 && !MISC.GET_MISSION_FLAG())
			{
				ClockWentBack(_lastClockMin, num, "a load");
			}
			else
			{
				GrudgeLog(string.Format("SAVEPOINT clock went back {0}h {1}m with no loading screen{2}{3} - left alone", (_lastClockMin - num) / 60, (_lastClockMin - num) % 60, (Game.GameTime - _loadSeenAt < 30000) ? " (a fade / jump only)" : "", MISC.GET_MISSION_FLAG() ? " (on a mission)" : ""));
				try
				{
					ClockJumpedBack(_lastClockMin, num);
				}
				catch
				{
				}
			}
			_lastHourlyMin = num;
		}
		_lastClockMin = num;
		_lastClockPos = position;
		if (Game.GameTime >= _nextClockStore)
		{
			_nextClockStore = Game.GameTime + 10000;
			WriteClock(num);
		}
		if (num - _lastHourlyMin >= Config.SavePointEveryHours * 60 && !WorldBusy(player) && Game.GameTime >= _clockRaceUntil && Game.GameTime - _lastHourlyReal >= 60000)
		{
			_lastHourlyMin = num;
			_lastHourlyReal = Game.GameTime;
			TakeSavePoint("hourly", sync: false, 0L);
		}
		return;
		IL_03e0:
		try
		{
			CleanMixedTimeline(GameMinutesRaw());
		}
		catch (Exception ex)
		{
			GrudgeLog("TIMELINE clean-up failed: " + ex.Message);
		}
		try
		{
			FixValentineSelfDefence();
		}
		catch (Exception ex2)
		{
			GrudgeLog("PEOPLE self-defence fix failed: " + ex2.Message);
		}
		try
		{
			FixPearseKeogh();
		}
		catch (Exception ex3)
		{
			GrudgeLog("PEOPLE rename fix failed: " + ex3.Message);
		}
		_lastClockMin = num;
		_lastClockPos = position;
		_lastHourlyMin = num;
	}

	private void WriteClock(long now)
	{
		try
		{
			File.WriteAllText(ClockPath, now.ToString());
		}
		catch
		{
		}
		Chronicle.LastClockMin = now;
	}

	private long ReadClock()
	{
		try
		{
			if (File.Exists(ClockPath) && long.TryParse(File.ReadAllText(ClockPath).Trim(), out var result) && result > 0)
			{
				return result;
			}
		}
		catch
		{
		}
		return Chronicle.LastClockMin;
	}

	private void ClockWentBack(long from, long to, string how)
	{
		string text = GameDate.FromMinutes(from).ToString("MMM d HH:mm");
		string text2 = GameDate.FromMinutes(to).ToString("MMM d HH:mm");
		GrudgeLog("SAVEPOINT clock went back " + text + " -> " + text2 + " (" + how + ")");
		if (!Config.SavePointAutoRestore)
		{
			ShowNotification("enRAGE: your game went back to " + text2 + ". ([SavePoints] AutoRestore is off - nothing changed.)");
			return;
		}
		string text3 = ExactSavePoint(to, Game.Player.Character.Position) ?? FindSavePoint(to);
		bool flag = false;
		if (text3 == null)
		{
			long? num = EarliestPointMin();
			if (num.HasValue)
			{
				long valueOrDefault = num.GetValueOrDefault();
				if (valueOrDefault - to <= 1440)
				{
					flag = true;
					text3 = (from x in AllSavePoints()
						where !Safety(x.m.Kind)
						orderby x.m.GameMin
						select x.dir).FirstOrDefault();
					GrudgeLog("SAVEPOINT no point at or before " + text2 + " - the earliest one is within a day: using it");
				}
			}
		}
		if (text3 == null && RestoreNearestAfter(to, text2))
		{
			return;
		}
		if (text3 == null)
		{
			GrudgeLog("SAVEPOINT no save point at or before " + text2 + " - a fresh world for this save");
			FreshWorldForSave(text2);
			return;
		}
		RestoreWithUndo(text3, "your save");
		if (!flag)
		{
			return;
		}
		try
		{
			HealFutureTimeline(GameMinutesRaw());
		}
		catch (Exception ex)
		{
			GrudgeLog("TIMESHIFT heal failed: " + ex.Message);
		}
	}

	private void LaterSaveLoaded(long from, long now, Vector3 pos, string how)
	{
		string text = GameDate.FromMinutes(now).ToString("MMM d HH:mm");
		GrudgeLog($"SAVEPOINT clock went forward {GameDate.FromMinutes(from):MMM d HH:mm} -> {text} ({how})");
		if (!Config.SavePointAutoRestore)
		{
			return;
		}
		string text2 = ExactSavePoint(now, pos);
		if (text2 != null)
		{
			RestoreWithUndo(text2, "a later save (" + text + ")");
			return;
		}
		string text3 = (from x in AllSavePoints()
			where !Safety(x.m.Kind) && x.m.GameMin <= now + 10 && now - x.m.GameMin <= 4320 && x.m.GameMin > @from + 10
			orderby x.m.GameMin descending, x.m.Real descending
			select x.dir).FirstOrDefault();
		if (text3 != null)
		{
			GrudgeLog("SAVEPOINT no exact point for " + text + " - using the nearest one before it (" + Path.GetFileName(text3) + ")");
			RestoreWithUndo(text3, "your save at " + text + " (the nearest point before it)");
		}
		else if (!RestoreNearestAfter(now, text))
		{
			if (Chronicle.Era < 1907 && IsEpiloguePlayer())
			{
				GrudgeLog("SAVEPOINT " + text + ": playing as John - this world carries on into 1907 (eight years later)");
			}
			else if (now - from > 4320)
			{
				GrudgeLog($"SAVEPOINT no save point within {3} days before {text} - a fresh world for this save");
				FreshWorldForSave(text, later: true);
			}
			else
			{
				GrudgeLog($"SAVEPOINT {(now - from) / 60} h later with no newer point - the world carries on (time skipped: jail, a wait, a later slot of this story)");
			}
		}
	}

	private bool RestoreNearestAfter(long now, string toT)
	{
		if (!Config.SavePointAutoRestore)
		{
			return false;
		}
		(string dir, SavePointMeta m) tuple = (from x in AllSavePoints(abandoned: true)
			where !Safety(x.m.Kind) && x.m.Kind != "fresh start" && x.m.GameMin > now && x.m.GameMin - now <= 1440
			orderby x.m.GameMin
			select x).FirstOrDefault();
		if (tuple.Item1 == null)
		{
			return false;
		}
		long num = tuple.Item2.GameMin - now;
		GrudgeLog($"SAVEPOINT nothing at or before {toT} - the nearest point after it ({tuple.Item2.When}, {num / 60}h {num % 60}m later) is this playthrough's world");
		RestoreWithUndo(tuple.Item1, "your save at " + toT + " (the nearest point after it)");
		try
		{
			ShiftTimeline(-num, "restored from a point later than this save");
		}
		catch (Exception ex)
		{
			GrudgeLog("TIMESHIFT failed: " + ex.Message);
		}
		return true;
	}

	private static bool Safety(string kind)
	{
		if (!(kind == "before restore") && !(kind == "before fresh"))
		{
			return kind == "before conversion";
		}
		return true;
	}

	private long? EarliestPointMin()
	{
		List<(string dir, SavePointMeta m)> list = (from x in AllSavePoints(abandoned: true)
			where x.m.Kind != "before restore" && x.m.Kind != "before fresh"
			select x).ToList();
		if (list.Count != 0)
		{
			return list.Min<(string dir, SavePointMeta m)>(((string dir, SavePointMeta m) x) => x.m.GameMin);
		}
		return null;
	}

	private void FreshWorldForSave(string when, bool later = false)
	{
		bool started = SafeFile.BeginWaitBudget(3000);
		try
		{
			FreshWorldForSaveNow(when, later);
		}
		finally
		{
			SafeFile.EndWaitBudget(started);
		}
	}

	private void FreshWorldForSaveNow(string when, bool later)
	{
		string text = TakeSavePoint("before fresh", sync: true, 0L);
		_undoSlot = _currentSlot;
		if (_menu.IsOpen)
		{
			_menu.Close();
		}
		_bulletImpact.CloseViewer(now: true);
		DebugReset("all");
		_storyApplied = false;
		_lastMindHour = -1L;
		_lastMindDay = -1L;
		WriteClock(_lastHourlyMin = (_lastClockMin = GameMinutesRaw()));
		TakeSavePoint("fresh start", sync: true, 0L);
		WriteSlot(null);
		_undoPoint = text;
		_undoUntil = Game.GameTime + 120000;
		_undoIsFresh = true;
		SavePointNote = "fresh world for the save at " + when;
		GrudgeLog("SAVEPOINT fresh world for the save at " + when + " (the other world kept as " + Path.GetFileName(text ?? "?") + ")");
		ShowNotification("enRAGE: this save (" + when + ") is " + (later ? "one" : "older than anything") + " enRAGE " + (later ? "hasn't seen" : "has") + " - it gets a world of its own. Your other world is kept with its save. " + KeyCombo.UndoText + " within 2 minutes to undo.");
	}

	private bool WritesSettled(int ms)
	{
		ms = SafeFile.BudgetMs(ms);
		Task[] array;
		lock (_pointWrites)
		{
			array = _pointWrites.Where((Task t) => !t.IsCompleted).ToArray();
		}
		if (array.Length != 0)
		{
			try
			{
				if (!Task.WaitAll(array, ms))
				{
					return false;
				}
			}
			catch
			{
			}
		}
		return SafeFile.WaitWrites(SafeFile.BudgetMs(ms));
	}

	private void RestoreAfterWrites(string what, Action then)
	{
		if (_afterWrites == null)
		{
			_afterWritesSince = (_afterWritesLogAt = Game.GameTime);
			GrudgeLog("SAVEPOINT " + what + ": the background writer is still busy - done as soon as it's finished");
		}
		_afterWrites = then;
		_afterWritesWhat = what;
	}

	private void RestoreWithUndo(string point, string why)
	{
		bool started = SafeFile.BeginWaitBudget(3000);
		string text;
		try
		{
			text = _restoreSafety ?? TakeSavePoint("before restore", sync: true, 0L);
			if (!WritesSettled(1500))
			{
				_restoreSafety = text;
				RestoreAfterWrites("a restore", delegate
				{
					RestoreWithUndo(point, why);
				});
				return;
			}
		}
		finally
		{
			SafeFile.EndWaitBudget(started);
		}
		_restoreSafety = null;
		_undoSlot = _currentSlot;
		_undoPoint = text;
		_undoUntil = Game.GameTime + 120000;
		_undoIsFresh = false;
		if (!RestoreSavePoint(point))
		{
			if (text != null)
			{
				ShowNotification("enRAGE: " + KeyCombo.UndoText + " within 2 minutes puts the world back as it was.");
			}
			return;
		}
		SavePointMeta savePointMeta = ReadMeta(point);
		ShowNotification("enRAGE: the world is back to " + (savePointMeta?.When ?? "?") + " with " + why + ". " + KeyCombo.UndoText + " within 2 minutes to undo.");
	}

	private string ExactSavePoint(long gameMin, Vector3 pos)
	{
		List<(string dir, SavePointMeta m)> list = AllSavePoints(abandoned: true);
		HashSet<string> latest = SlotLatest(list);
		return (from x in list
			where !Safety(x.m.Kind) && Math.Abs(x.m.GameMin - gameMin) <= 15 && new Vector3(x.m.X - pos.X, x.m.Y - pos.Y, 0f).Length() < 100f
			orderby latest.Contains(x.dir) descending, Math.Abs(x.m.GameMin - gameMin), x.m.Real descending
			select x.dir).FirstOrDefault();
	}

	private string SlotPath => Path.Combine(_saveDir, "slot.txt");

	private static Dictionary<string, DateTime> SlotTimes()
	{
		Dictionary<string, DateTime> dictionary = new Dictionary<string, DateTime>();
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Rockstar Games", "Red Dead Redemption 2", "Profiles");
			if (!Directory.Exists(path))
			{
				return dictionary;
			}
			string[] directories = Directory.GetDirectories(path);
			foreach (string path2 in directories)
			{
				string[] files = Directory.GetFiles(path2, "SRDR3*");
				foreach (string path3 in files)
				{
					if (Path.GetExtension(path3) == "")
					{
						dictionary[Path.GetFileName(path2) + "/" + Path.GetFileName(path3)] = File.GetLastWriteTimeUtc(path3);
					}
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	internal static string SlotName(string slot)
	{
		if (string.IsNullOrEmpty(slot))
		{
			return "an untagged save";
		}
		return "save file " + slot.Substring(slot.LastIndexOf('/') + 1);
	}

	private string ReadSlot()
	{
		try
		{
			object result;
			if (!File.Exists(SlotPath))
			{
				result = null;
			}
			else
			{
				string text = File.ReadAllText(SlotPath).Trim();
				result = ((text != null && text != "") ? text : null);
			}
			return (string)result;
		}
		catch
		{
			return null;
		}
	}

	private void WriteSlot(string slot)
	{
		_currentSlot = slot;
		try
		{
			File.WriteAllText(SlotPath, slot ?? "");
		}
		catch
		{
		}
	}

	private void CheckSlotWritten()
	{
		if (_slotPendingDir == null)
		{
			return;
		}
		Dictionary<string, DateTime> dictionary = SlotTimes();
		DateTime cut = DateTime.UtcNow.AddSeconds(-40.0);
		DateTime value;
		string text = (from kv in dictionary
			where (_slotSnap == null || !_slotSnap.TryGetValue(kv.Key, out value)) ? (kv.Value > cut) : (kv.Value > value)
			orderby kv.Value descending
			select kv.Key).FirstOrDefault();
		string path = Path.Combine(_slotPendingDir, "point.json");
		if (text != null && File.Exists(path))
		{
			string keep = _slotPendingDir;
			string slot = text;
			SafeFile.RunLater(delegate
			{
				try
				{
					SavePointMeta savePointMeta = null;
					for (int i = 0; i < 3; i++)
					{
						if (savePointMeta != null)
						{
							break;
						}
						savePointMeta = ReadMeta(keep);
						if (savePointMeta == null)
						{
							Thread.Sleep(100);
						}
					}
					if (savePointMeta == null)
					{
						_savePointCapNote = "slot tag: " + Path.GetFileName(keep) + " couldn't be read - it goes by the clock only";
						return;
					}
					savePointMeta.Slot = slot;
					File.WriteAllText(Path.Combine(keep, "point.json"), JsonConvert.SerializeObject(savePointMeta));
					DateTime real = savePointMeta.Real;
					foreach (var item in from x in AllSavePoints()
						where x.dir != keep && x.m.Kind == "game save" && x.m.Slot == slot && (real - x.m.Real).TotalMinutes < 2.0
						select x)
					{
						try
						{
							Directory.Delete(item.dir, recursive: true);
						}
						catch
						{
						}
					}
				}
				catch (Exception ex)
				{
					_savePointCapNote = "slot tag failed: " + ex.Message;
				}
			});
			WriteSlot(text);
			GrudgeLog("SAVEPOINT the save went to " + SlotName(text) + " - this world is that save's");
			_slotPendingDir = null;
			_slotSnap = dictionary;
		}
		else if (Game.GameTime > _slotPendingUntil)
		{
			GrudgeLog("SAVEPOINT couldn't tell which save file the game wrote - this point goes by the clock only");
			_slotPendingDir = null;
			_slotSnap = dictionary;
		}
	}

	private static HashSet<string> SlotLatest(List<(string dir, SavePointMeta m)> all)
	{
		return new HashSet<string>(from x in all
			where !string.IsNullOrEmpty(x.m.Slot) && !Safety(x.m.Kind)
			group x by x.m.Slot into g
			select g.OrderByDescending(((string dir, SavePointMeta m) x) => x.m.Real).First().dir);
	}

	private (string dir, string slot) SlotSavePoint(long gameMin, Vector3 pos)
	{
		List<(string dir, SavePointMeta m)> list = AllSavePoints(abandoned: true);
		HashSet<string> latest = SlotLatest(list);
		(string dir, SavePointMeta m) tuple = (from x in list
			where latest.Contains(x.dir) && Math.Abs(x.m.GameMin - gameMin) <= 15 && new Vector3(x.m.X - pos.X, x.m.Y - pos.Y, 0f).Length() < 100f
			orderby Math.Abs(x.m.GameMin - gameMin)
			select x).FirstOrDefault();
		return (dir: tuple.Item1, slot: tuple.Item2?.Slot);
	}

	internal void UndoRestore()
	{
		if (_undoPoint == null || Game.GameTime > _undoUntil)
		{
			ShowNotification("enRAGE: nothing to undo.");
			return;
		}
		if (!WritesSettled(1500))
		{
			RestoreAfterWrites("an undo", UndoRestore);
			return;
		}
		string undoPoint = _undoPoint;
		_undoPoint = null;
		bool undoIsFresh = _undoIsFresh;
		_undoIsFresh = false;
		if (!RestoreSavePoint(undoPoint))
		{
			return;
		}
		WriteSlot(_undoSlot);
		ShowNotification("enRAGE: restore undone - the world is as it was before the load.");
		if (!undoIsFresh)
		{
			return;
		}
		try
		{
			HealFutureTimeline(GameMinutesRaw());
		}
		catch (Exception ex)
		{
			GrudgeLog("TIMESHIFT heal failed: " + ex.Message);
		}
	}

	private string TakeSavePoint(string kind, bool sync = false, long stampMin = 0L)
	{
		bool started = sync && SafeFile.BeginWaitBudget(3000);
		try
		{
			return TakeSavePointNow(kind, sync, stampMin);
		}
		finally
		{
			SafeFile.EndWaitBudget(started);
		}
	}

	private string TakeSavePointNow(string kind, bool sync, long stampMin)
	{
		Dictionary<string, byte[]> files;
		bool copyNow;
		string dir;
		string metaJson;
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			if (kind != "hourly")
			{
				ForceSaves(!sync);
			}
			else
			{
				if (Slicing("roster") || Slicing("world"))
				{
					FinishSlices("a save point");
				}
				_nextRosterSave = 0;
				FlushRosterSave(force: false, sliced: true);
				if (_worldDirty)
				{
					SaveWorldNow(background: true, sliced: true);
				}
			}
			if (sync)
			{
				SafeFile.WaitWrites();
				try
				{
					_bulletImpact.SaveXRayNow();
				}
				catch
				{
				}
			}
			else
			{
				try
				{
					_bulletImpact.SaveXRayLater();
				}
				catch
				{
				}
			}
			long num = ((stampMin > 0) ? stampMin : GameMinutesRaw());
			SavePointMeta savePointMeta = new SavePointMeta
			{
				GameMin = num,
				When = GameDate.FromMinutes(num).ToString("MMM d yyyy HH:mm"),
				Real = DateTime.Now,
				Kind = kind,
				X = Game.Player.Character.Position.X,
				Y = Game.Player.Character.Position.Y,
				Notoriety = new Dictionary<string, int>(Config.Notoriety),
				SeededGangs = Config.SeededGangs,
				ArmyTopUp = Config.ArmyTopUp,
				LastSuccessionDay = Config.LastSuccessionDay,
				NextSuccessionDay = Config.NextSuccessionDay,
				LastDecayDay = Config.LastDecayDay,
				ClearedCamps = Config.ClearedCamps
			};
			files = new Dictionary<string, byte[]>();
			copyNow = sync && SafeFile.Idle;
			if (copyNow)
			{
				ReadFiles();
			}
			if (Safety(kind))
			{
				savePointMeta.Slot = null;
			}
			dir = Path.Combine(SavePointDir, $"{num:D9}-{kind.Replace(' ', '-')}-{DateTime.Now:HHmmss}");
			metaJson = JsonConvert.SerializeObject(savePointMeta);
			if (copyNow)
			{
				Write();
				_pruneDue = true;
			}
			else if (sync)
			{
				_pruneDue = true;
				lock (_pointWrites)
				{
					_pointWrites.RemoveAll((Task t) => t.IsCompleted);
					_pointWrites.Add(Task.Run(delegate
					{
						try
						{
							Write();
						}
						catch (Exception ex2)
						{
							_savePointError = ex2.Message;
						}
					}));
				}
			}
			else
			{
				AfterSlices(delegate
				{
					if (_rosterDirty)
					{
						_nextRosterSave = 0;
						FlushRosterSave();
					}
					if (_worldDirty)
					{
						SaveWorldNow(background: true);
					}
					_pruneDue = true;
					lock (_pointWrites)
					{
						_pointWrites.RemoveAll((Task t) => t.IsCompleted);
						_pointWrites.Add(Task.Run(delegate
						{
							try
							{
								Write();
							}
							catch (Exception ex2)
							{
								_savePointError = ex2.Message;
							}
						}));
					}
				});
			}
			SavePointNote = "last: " + kind + " at " + savePointMeta.When;
			GrudgeLog("SAVEPOINT taken: " + kind + " at " + savePointMeta.When + (copyNow ? $" ({files.Values.Sum((byte[] b) => b.Length) / 1024} KB)" : (sync ? " (the writer was busy - copied once it's done)" : " (writing in the background)")));
			NoteSaveTime("save point", stopwatch.Elapsed.TotalMilliseconds);
			return dir;
		}
		catch (Exception ex)
		{
			GrudgeLog("SAVEPOINT take failed: " + ex.Message);
			return null;
		}
		void ReadFiles()
		{
			string[] saveFiles = SaveFiles;
			foreach (string text in saveFiles)
			{
				string path = Path.Combine(_saveDir, text);
				for (int j = 0; j < 3; j++)
				{
					try
					{
						if (File.Exists(path))
						{
							files[text] = File.ReadAllBytes(path);
						}
					}
					catch (IOException)
					{
						Thread.Sleep(50);
						continue;
					}
					break;
				}
			}
		}
		void Write()
		{
			if (!copyNow)
			{
				SafeFile.WaitWrites(15000);
				ReadFiles();
			}
			Directory.CreateDirectory(dir);
			foreach (KeyValuePair<string, byte[]> item in files)
			{
				using FileStream stream = File.Create(Path.Combine(dir, item.Key + ".gz"));
				using GZipStream gZipStream = new GZipStream(stream, CompressionLevel.Optimal);
				gZipStream.Write(item.Value, 0, item.Value.Length);
			}
			File.WriteAllText(Path.Combine(dir, "point.json"), metaJson);
		}
	}

	private void PruneSavePoints()
	{
		List<(string dir, SavePointMeta m)> source = AllSavePoints();
		List<string> drop = new List<string>();
		List<(string dir, SavePointMeta m)> source2 = (from x in source
			where x.m.Kind == "game save"
			orderby x.m.Real descending
			select x).ToList();
		HashSet<long> hashSet = new HashSet<long>();
		foreach (var item2 in source2.Skip(40))
		{
			if (hashSet.Count >= 60 || !hashSet.Add(item2.Item2.GameMin / 1440))
			{
				drop.Add(item2.Item1);
			}
		}
		KeepNewest(source.Where<(string dir, SavePointMeta m)>(((string dir, SavePointMeta m) x) => Safety(x.m.Kind)), 5);
		List<(string dir, SavePointMeta m)> list = (from x in source
			where x.m.Kind == "hourly"
			orderby x.m.Real descending
			select x).ToList().Skip(24).ToList();
		HashSet<long> hashSet2 = new HashSet<long>();
		foreach (var item3 in list)
		{
			long item = item3.Item2.GameMin / 1440;
			if (hashSet2.Count >= 60 || !hashSet2.Add(item))
			{
				drop.Add(item3.Item1);
			}
		}
		string earliest = (from x in source
			where !Safety(x.m.Kind)
			orderby x.m.GameMin
			select x.dir).FirstOrDefault();
		drop.RemoveAll((string d) => d == earliest);
		HashSet<string> hashSet3 = new HashSet<string>(from x in source
			where x.m.Kind == "before 1907"
			select x.dir);
		drop.RemoveAll(hashSet3.Contains);
		HashSet<string> hashSet4 = SlotLatest(AllSavePoints(abandoned: true));
		drop.RemoveAll(hashSet4.Contains);
		foreach (string item4 in drop)
		{
			try
			{
				Directory.Delete(item4, recursive: true);
			}
			catch
			{
			}
		}
		try
		{
			long num = (long)Math.Max(20, Config.SavePointMaxMB) * 1024L * 1024;
			List<(string dir, SavePointMeta m)> source3 = (from x in AllSavePoints(abandoned: true)
				where Directory.Exists(x.dir)
				select x).ToList();
			Dictionary<string, long> dictionary = source3.ToDictionary<(string dir, SavePointMeta m), string, long>(((string dir, SavePointMeta m) x) => x.dir, ((string dir, SavePointMeta m) x) => Size(x.dir));
			long num2 = dictionary.Values.Sum();
			if (num2 > num)
			{
				HashSet<string> hashSet5 = new HashSet<string>(from x in source3.OrderByDescending<(string dir, SavePointMeta m), DateTime>(((string dir, SavePointMeta m) x) => x.m.Real).Take(5)
					select x.dir);
				int num3 = 0;
				foreach (var item5 in source3.OrderBy<(string dir, SavePointMeta m), DateTime>(((string dir, SavePointMeta m) x) => x.m.Real))
				{
					if (num2 <= num)
					{
						break;
					}
					if (!(item5.Item1 == earliest) && !(item5.Item2.Kind == "fresh start") && !(item5.Item2.Kind == "before 1907") && !hashSet5.Contains(item5.Item1) && !hashSet4.Contains(item5.Item1))
					{
						try
						{
							Directory.Delete(item5.Item1, recursive: true);
							num2 -= dictionary[item5.Item1];
							num3++;
						}
						catch
						{
						}
					}
				}
				if (num3 > 0)
				{
					_savePointCapNote = $"size cap: {num3} oldest points removed ({num2 / 1048576} MB left of {num / 1048576})";
				}
			}
		}
		catch
		{
		}
		try
		{
			string[] directories = Directory.GetDirectories(SavePointDir);
			foreach (string text in directories)
			{
				if (text != AbandonedDir && !File.Exists(Path.Combine(text, "point.json")) && (DateTime.Now - Directory.GetCreationTime(text)).TotalMinutes > 10.0)
				{
					try
					{
						Directory.Delete(text, recursive: true);
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		void KeepNewest(IEnumerable<(string dir, SavePointMeta m)> group, int keep)
		{
			drop.AddRange(from x in @group.OrderByDescending(((string dir, SavePointMeta m) x) => x.m.Real).Skip(keep)
				select x.dir);
		}
		static long Size(string d)
		{
			try
			{
				return new DirectoryInfo(d).GetFiles("*", SearchOption.AllDirectories).Sum((FileInfo f) => f.Length);
			}
			catch
			{
				return 0L;
			}
		}
	}

	private string AbandonedDir => Path.Combine(SavePointDir, "abandoned");

	private List<(string dir, SavePointMeta m)> AllSavePoints(bool abandoned = false)
	{
		List<(string dir, SavePointMeta m)> list = new List<(string dir, SavePointMeta m)>();
		From(SavePointDir);
		if (abandoned)
		{
			From(AbandonedDir);
		}
		return list;
		void From(string root)
		{
			if (Directory.Exists(root))
			{
				string[] directories;
				try
				{
					directories = Directory.GetDirectories(root);
				}
				catch
				{
					return;
				}
				string[] array = directories;
				foreach (string text in array)
				{
					if (!(text == AbandonedDir))
					{
						SavePointMeta savePointMeta = ReadMeta(text);
						if (savePointMeta != null)
						{
							list.Add((text, savePointMeta));
						}
					}
				}
			}
		}
	}

	private void SetAsideFuture(long fromMin)
	{
		try
		{
			Directory.CreateDirectory(AbandonedDir);
			int num = 0;
			HashSet<string> slotWorlds = SlotLatest(AllSavePoints(abandoned: true));
			foreach (var item2 in from x in AllSavePoints()
				where !Safety(x.m.Kind) && x.m.GameMin > fromMin + 10 && !slotWorlds.Contains(x.dir)
				select x)
			{
				string item = item2.dir;
				Directory.Move(item, Path.Combine(AbandonedDir, Path.GetFileName(item) + "-" + DateTime.Now.ToString("HHmmssfff")));
				num++;
			}
			foreach (string item3 in Directory.GetDirectories(AbandonedDir).OrderByDescending(Directory.GetCreationTime).Skip(40))
			{
				if (!slotWorlds.Contains(item3))
				{
					try
					{
						Directory.Delete(item3, recursive: true);
					}
					catch
					{
					}
				}
			}
			if (num > 0)
			{
				GrudgeLog($"SAVEPOINT {num} points from the timeline you left set aside");
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("SAVEPOINT set aside failed: " + ex.Message);
		}
	}

	private static SavePointMeta ReadMeta(string dir)
	{
		try
		{
			string path = Path.Combine(dir, "point.json");
			return File.Exists(path) ? JsonConvert.DeserializeObject<SavePointMeta>(File.ReadAllText(path)) : null;
		}
		catch
		{
			return null;
		}
	}

	private string FindSavePoint(long gameMin)
	{
		return (from x in AllSavePoints()
			where !Safety(x.m.Kind) && x.m.GameMin <= gameMin + 10
			orderby x.m.GameMin descending, x.m.Real descending
			select x.dir).FirstOrDefault();
	}

	private bool RestoreSavePoint(string dir)
	{
		SavePointMeta savePointMeta = ReadMeta(dir);
		if (savePointMeta == null)
		{
			GrudgeLog("SAVEPOINT restore: no point.json in " + dir);
			return false;
		}
		try
		{
			if (!WritesSettled(0))
			{
				GrudgeLog("SAVEPOINT restore: writes still going - not swapped now");
				return false;
			}
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			string[] saveFiles = SaveFiles;
			foreach (string text in saveFiles)
			{
				string path = Path.Combine(dir, text + ".gz");
				if (!File.Exists(path))
				{
					continue;
				}
				using FileStream stream = File.OpenRead(path);
				using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
				using MemoryStream memoryStream = new MemoryStream();
				gZipStream.CopyTo(memoryStream);
				dictionary[text] = Encoding.UTF8.GetString(memoryStream.ToArray());
			}
			if (!dictionary.ContainsKey("nemeses.json"))
			{
				GrudgeLog("SAVEPOINT restore: the point has no roster - nothing changed");
				return false;
			}
			try
			{
				if (JsonConvert.DeserializeObject<List<NemesisRecord>>(dictionary["nemeses.json"]) == null)
				{
					throw new Exception("empty roster");
				}
				if (dictionary.TryGetValue("world.json", out var value) && JsonConvert.DeserializeObject<WorldState>(value) == null)
				{
					throw new Exception("empty world");
				}
			}
			catch (Exception ex)
			{
				GrudgeLog("SAVEPOINT restore: the point's files don't read (" + ex.Message + ") - nothing changed");
				return false;
			}
			string text2 = DateTime.Now.ToString("yyyyMMdd-HHmmss");
			List<string> list = new List<string>();
			try
			{
				foreach (KeyValuePair<string, string> item2 in dictionary)
				{
					string text3 = Path.Combine(_saveDir, item2.Key) + ".restore-new";
					File.WriteAllText(text3, item2.Value);
					list.Add(text3);
				}
			}
			catch (Exception ex2)
			{
				foreach (string item3 in list)
				{
					try
					{
						File.Delete(item3);
					}
					catch
					{
					}
				}
				GrudgeLog("SAVEPOINT restore: couldn't write the files (" + ex2.Message + ") - nothing changed");
				ShowNotification("enRAGE: couldn't put the world back (" + ex2.Message + ") - nothing changed.");
				return false;
			}
			if (_menu.IsOpen)
			{
				_menu.Close();
			}
			_bulletImpact.CloseViewer(now: true);
			WipeWorldAfterJump();
			CleanupCamps();
			try
			{
				CleanupFuneral();
				_funeralSeenAt.Clear();
			}
			catch (Exception ex3)
			{
				GrudgeLog("SAVEPOINT funeral clean-up failed: " + ex3.Message);
			}
			DespawnAftermath();
			ClearAftermathBlips();
			ClearPlanBlips();
			List<(int, string)> list2 = (from k in _keptTied
				where ENTITY.DOES_ENTITY_EXIST(k.h)
				select (h: k.h, id: k.rec.Id)).ToList();
			_worldNemeses.Clear();
			_followerOf.Clear();
			_nemesisBodies.Clear();
			string text4 = Path.Combine(_saveDir, "restore-old-" + text2);
			List<(string, string)> list3 = new List<(string, string)>();
			try
			{
				Directory.CreateDirectory(text4);
				saveFiles = SaveFiles;
				foreach (string text5 in saveFiles)
				{
					string text6 = Path.Combine(_saveDir, text5);
					bool flag = dictionary.ContainsKey(text5);
					if (!flag && text5 == "xray_log.json" && File.Exists(text6))
					{
						string text7 = Path.Combine(_saveDir, "xray_log.other-world-" + text2 + ".json");
						File.Move(text6, text7);
						list3.Add((text6, text7));
						GrudgeLog("SAVEPOINT restore: the point has no X-rays - the current X-ray log set aside as " + Path.GetFileName(text7));
					}
					string[] array = new string[3] { "", ".bak", ".tmp" };
					foreach (string text8 in array)
					{
						string text9 = text6 + text8;
						if (File.Exists(text9))
						{
							string text10 = Path.Combine(text4, text5 + text8);
							File.Move(text9, text10);
							list3.Add((text9, text10));
						}
					}
					if (flag)
					{
						File.Move(text6 + ".restore-new", text6);
						list3.Add((text6 + ".restore-new", text6));
					}
					else
					{
						GrudgeLog("SAVEPOINT restore: " + text5 + " not in the point - the current one moved aside");
					}
				}
			}
			catch (Exception ex4)
			{
				for (int num2 = list3.Count - 1; num2 >= 0; num2--)
				{
					try
					{
						File.Move(list3[num2].Item2, list3[num2].Item1);
					}
					catch
					{
					}
				}
				foreach (string item4 in list)
				{
					try
					{
						if (File.Exists(item4))
						{
							File.Delete(item4);
						}
					}
					catch
					{
					}
				}
				GrudgeLog("SAVEPOINT restore: swapping the files failed (" + ex4.Message + ") - every file put back, the world reloaded as it was");
				ShowNotification("enRAGE: couldn't put the world back (" + ex4.Message + ") - nothing changed.");
				_rosterDirty = false;
				_worldDirty = false;
				_peopleDirty = false;
				_printsDirty = false;
				LoadRoster();
				_chron = null;
				_worldGen++;
				_chronCache = null;
				_people = null;
				_peopleListCache = null;
				_acqOf.Clear();
				_witnessBatches.Clear();
				_prints = null;
				_keptNames = null;
				_fpOf.Clear();
				Gangs.SetEra(Chronicle.Era);
				SeedGangRosters();
				AssignCamps();
				RefreshCampBlips();
				return false;
			}
			try
			{
				foreach (string item5 in (from d in Directory.GetDirectories(_saveDir, "restore-old-*")
					orderby d descending
					select d).Skip(2))
				{
					Directory.Delete(item5, recursive: true);
				}
			}
			catch
			{
			}
			_bulletImpact.ReloadXRayLog();
			foreach (string item6 in Config.Notoriety.Keys.ToList())
			{
				if (!savePointMeta.Notoriety.ContainsKey(item6))
				{
					Config.Notoriety[item6] = 0;
				}
			}
			foreach (KeyValuePair<string, int> item7 in savePointMeta.Notoriety)
			{
				Config.Notoriety[item7.Key] = item7.Value;
			}
			Config.SeededGangs = savePointMeta.SeededGangs ?? "";
			Config.ArmyTopUp = savePointMeta.ArmyTopUp;
			Config.LastSuccessionDay = savePointMeta.LastSuccessionDay;
			Config.NextSuccessionDay = savePointMeta.NextSuccessionDay;
			Config.LastDecayDay = savePointMeta.LastDecayDay;
			Config.ClearedCamps = savePointMeta.ClearedCamps ?? "";
			_cleared = null;
			Config.Save();
			SafeFile.UnblockAll();
			_rosterDirty = false;
			_worldDirty = false;
			_peopleDirty = false;
			_printsDirty = false;
			LoadRoster();
			foreach (var item8 in list2)
			{
				int item = item8.Item1;
				string id = item8.Item2;
				NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == id);
				if (nemesisRecord != null)
				{
					_worldNemeses[item] = nemesisRecord;
				}
			}
			MoveForeignXRays();
			_chron = null;
			_worldGen++;
			_chronCache = null;
			_people = null;
			_peopleListCache = null;
			_acqOf.Clear();
			_witnessBatches.Clear();
			_prints = null;
			_keptNames = null;
			_fpOf.Clear();
			_townWordCache.Clear();
			_tensionRestored = false;
			_successionCard = null;
			Gangs.SetEra(Chronicle.Era);
			SeedGangRosters();
			AssignCamps();
			RefreshCampBlips();
			_lastClockMin = GameMinutesRaw();
			_lastHourlyMin = _lastClockMin;
			WriteClock(_lastClockMin);
			if (!Safety(savePointMeta.Kind))
			{
				SetAsideFuture(savePointMeta.GameMin);
			}
			if (!Safety(savePointMeta.Kind))
			{
				WriteSlot(savePointMeta.Slot);
			}
			_lastMindHour = -1L;
			_lastMindDay = -1L;
			_storyApplied = false;
			SavePointNote = "restored " + savePointMeta.Kind + " from " + savePointMeta.When;
			GrudgeLog($"SAVEPOINT restored {savePointMeta.Kind} from {savePointMeta.When} ({Roster.Count((NemesisRecord n) => n.IsAlive)} living on the roster)");
			Feed("SAVE the world went back to " + savePointMeta.When + " with your save", Theme.Gold);
			return true;
		}
		catch (Exception ex5)
		{
			GrudgeLog("SAVEPOINT restore failed: " + ex5);
			ShowNotification("enRAGE: putting the world back failed (" + ex5.Message + ") - see grudges.log.");
			return false;
		}
	}

	internal string SavePointDebugLine()
	{
		int num = 0;
		try
		{
			num = (Directory.Exists(SavePointDir) ? Directory.GetDirectories(SavePointDir).Length : 0);
		}
		catch
		{
		}
		return string.Format("save points: {0} kept  -  {1}{2}", num, (SavePointNote == "") ? "none yet" : SavePointNote, (_savePointError != null) ? ("  -  ERROR " + _savePointError) : "") + "  -  clock " + ((_lastClockMin > 0) ? GameDate.FromMinutes(_lastClockMin).ToString("MMM d HH:mm") : "-");
	}

	private List<(string name, Action flush, Action force)> Saves
	{
		get
		{
			List<(string, Action, Action)> list = _saves;
			if (list == null)
			{
				List<(string, Action, Action)> obj = new List<(string, Action, Action)>
				{
					("roster", delegate
					{
						FlushRosterSave(force: false, sliced: true);
					}, delegate
					{
						FlushRosterSave(force: true);
					}),
					("world", FlushWorld, delegate
					{
						if (_worldDirty)
						{
							SaveWorldNow();
						}
					}),
					("people", delegate
					{
						SavePeople();
					}, delegate
					{
						SavePeople(force: true);
					}),
					("looks", FlushPrints, delegate
					{
						SavePrints();
					}),
					("log", delegate
					{
						FlushGrudgeLog(force: false, later: true);
					}, delegate
					{
						FlushGrudgeLog(force: true, later: true);
					})
				};
				List<(string, Action, Action)> list2 = obj;
				_saves = obj;
				list = list2;
			}
			return list;
		}
	}

	private void FlushSaves()
	{
		Guard("saves", delegate
		{
			bool flag = HUD.IS_PAUSE_MENU_ACTIVE();
			if (flag && !_pauseFlushed)
			{
				_pauseFlushed = true;
				if (Environment.TickCount - _perfSince > 60000)
				{
					WritePerf(force: true);
				}
				FinishSlices("the pause menu");
				try
				{
					_nextRosterSave = 0;
					FlushRosterSave();
					if (_worldDirty)
					{
						SaveWorldNow(background: true);
					}
				}
				catch (Exception ex)
				{
					GrudgeLog("SAVE on pause failed: " + ex.Message);
				}
			}
			else if (!flag)
			{
				_pauseFlushed = false;
			}
			if ((flag || _menu.IsOpen || _bulletImpact.ViewerOpen || CAM.IS_SCREEN_FADED_OUT() || CAM.IS_SCREEN_FADING_OUT()) && Game.GameTime >= _nextHiddenSave)
			{
				_nextHiddenSave = Game.GameTime + 5000;
				try
				{
					if (_rosterDirty)
					{
						_nextRosterSave = 0;
						FlushRosterSave();
					}
					else if (_worldDirty)
					{
						SaveWorldNow(background: true);
						_nextWorldSave = Game.GameTime + 60000;
					}
				}
				catch (Exception ex2)
				{
					GrudgeLog("SAVE while hidden failed: " + ex2.Message);
				}
			}
			if (_rosterWriteError != null)
			{
				GrudgeLog("SAVE roster write failed (tried again on the next pass): " + _rosterWriteError);
				_rosterWriteError = null;
			}
			bool flag2 = _combatants.Values.Any((int t) => Game.GameTime - t < 3000) || (_enc != null && _enc.Engaged);
			if (!flag2)
			{
				_fightSaveHoldSince = -1;
			}
			else if (_fightSaveHoldSince < 0)
			{
				_fightSaveHoldSince = Game.GameTime;
			}
			bool flag3 = flag2 && Game.GameTime - _fightSaveHoldSince < 25000;
			if (!flag3 && flag2)
			{
				_fightSaveHoldSince = Game.GameTime;
			}
			XRayLog.Hold = flag3;
			RunSlices(flag3 ? 1.0 : 2.0);
			foreach (var safe in Saves)
			{
				if (!flag3 || !(safe.name != "log"))
				{
					long elapsedTicks = _saveClock.ElapsedTicks;
					try
					{
						safe.flush();
					}
					catch (Exception ex3)
					{
						GrudgeLog("ERROR in save " + safe.name + ": " + ex3.Message);
					}
					double num = (double)(_saveClock.ElapsedTicks - elapsedTicks) * 1000.0 / (double)Stopwatch.Frequency;
					NoteSaveTime(safe.name, num);
					if (num >= 5.0 && safe.name != "log")
					{
						break;
					}
				}
			}
			if (XRayLog.SaveCount != _xraySavesSeen)
			{
				_xraySavesSeen = XRayLog.SaveCount;
				NoteSaveTime("xray log", XRayLog.LastSaveMs);
			}
		});
	}

	private void ForceSaves(bool later = false)
	{
		bool started = !later && SafeFile.BeginWaitBudget(2500);
		try
		{
			ForceSavesNow(later);
		}
		finally
		{
			SafeFile.EndWaitBudget(started);
		}
	}

	private void ForceSavesNow(bool later)
	{
		XRayLog.Hold = false;
		if (!later)
		{
			FinishSlices("a full save");
		}
		else if (Slicing("roster") || Slicing("world"))
		{
			FinishSlices("a save point");
		}
		foreach (var safe in Saves)
		{
			if (later && safe.name == "roster")
			{
				try
				{
					_nextRosterSave = 0;
					FlushRosterSave(force: false, sliced: true);
				}
				catch (Exception ex)
				{
					GrudgeLog("ABORT save roster failed: " + ex.Message);
				}
			}
			else if (later && safe.name == "world")
			{
				try
				{
					if (_worldDirty)
					{
						SaveWorldNow(background: true, sliced: true);
					}
				}
				catch (Exception ex2)
				{
					GrudgeLog("ABORT save world failed: " + ex2.Message);
				}
			}
			else if (later && safe.name == "people")
			{
				try
				{
					SavePeople(force: true, later: true);
				}
				catch (Exception ex3)
				{
					GrudgeLog("ABORT save people failed: " + ex3.Message);
				}
			}
			else if (later && safe.name == "looks")
			{
				try
				{
					SavePrints(later: true);
				}
				catch (Exception ex4)
				{
					GrudgeLog("ABORT save looks failed: " + ex4.Message);
				}
			}
			else
			{
				try
				{
					safe.force();
				}
				catch (Exception ex5)
				{
					GrudgeLog("ABORT save " + safe.name + " failed: " + ex5.Message);
				}
			}
		}
	}
}
