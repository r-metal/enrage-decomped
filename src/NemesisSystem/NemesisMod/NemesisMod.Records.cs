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
	private readonly Queue<(bool down, KeyEventArgs e)> _keyQueue = new Queue<(bool, KeyEventArgs)>();

	private static readonly Dictionary<string, (string sound, string set)> UiSounds = new Dictionary<string, (string, string)>
	{
		["NAV_UP"] = ("NAV_UP", "Ledger_Sounds"),
		["NAV_DOWN"] = ("NAV_DOWN", "Ledger_Sounds"),
		["NAV_LEFT"] = ("NAV_LEFT", "Ledger_Sounds"),
		["NAV_RIGHT"] = ("NAV_RIGHT", "Ledger_Sounds"),
		["SELECT"] = ("SELECT", "HUD_SHOP_SOUNDSET"),
		["BACK"] = ("BACK", "HUD_SHOP_SOUNDSET"),
		["BUMPER_LEFT"] = ("NAV_LEFT", "PAUSE_MENU_SOUNDSET"),
		["BUMPER_RIGHT"] = ("NAV_RIGHT", "PAUSE_MENU_SOUNDSET"),
		["MENU_ENTER"] = ("INFO_SHOW", "Ledger_Sounds"),
		["MENU_CLOSE"] = ("INFO_HIDE", "Ledger_Sounds")
	};

	private int _idCounter;

	internal List<NemesisRecord> StoryRoster = SafeStoryGang();

	private static string StoryBuildError;

	private (string outcome, string how)? _encEnd;

	private readonly Dictionary<string, int> _errorLoggedAt = new Dictionary<string, int>();

	internal int ErrorCount;

	internal string LastError = "";

	private readonly Dictionary<string, int> _slowLoggedAt = new Dictionary<string, int>();

	private const double SlowFrameMs = 50.0;

	private double _lastTickAt;

	private double _lastTickHeld;

	private bool _lastTickWaited;

	private int _slowFrames;

	private int _slowFramesMod;

	private string _tickTop;

	private string _lastTickTop;

	private double _tickTopMs;

	private double _lastTickTopMs;

	private int _slowFrameLoggedAt;

	private const string OutsideSystems = "(outside the systems)";

	private int _guardDepth;

	private double _systemsMs;

	private NemesisRecord NewRecord(uint modelHash, string zone, Gang forGang = null, int rank = 1)
	{
		Gang gang = forGang ?? Gangs.ForModel(modelHash) ?? (RallyModel(modelHash) ? Gangs.Klan : null);
		string[] array = gang?.FirstNames ?? NameBank.AmericanFirst;
		string[] array2 = gang?.LastNames ?? NameBank.AmericanLast;
		string[] array3 = gang?.Titles ?? NameBank.AmericanTitles;
		if (Models.Female(modelHash) && array != NameBank.WapitiFirst)
		{
			array = FemaleFirstFor(array);
			array3 = NameBank.FemaleTitles;
		}
		string name = null;
		string text = null;
		for (int i = 0; i < 30; i++)
		{
			name = array[_rng.Next(array.Length)] + " " + array2[_rng.Next(array2.Length)];
			text = array3[_rng.Next(array3.Length)];
			string sur = name.Substring(name.LastIndexOf(' ') + 1);
			bool num = gang != null && gang.RankInName && Roster.Any((NemesisRecord n) => n.Gang == gang.Id && n.Name != null && n.Name.EndsWith(" " + sur));
			bool flag = gang?.ExcludedNames != null && gang.ExcludedNames.Contains(name);
			if (!num && !flag && !Roster.Any((NemesisRecord n) => n.Name == name) && !Gangs.All.Any((Gang g) => g.LeaderName.EndsWith(name)))
			{
				break;
			}
		}
		NemesisRecord nemesisRecord = new NemesisRecord
		{
			Id = NewId(12),
			Rank = rank,
			Gang = gang?.Id,
			Name = name,
			Title = (HasNickname(name, gang != null) ? text : ""),
			ModelHash = modelHash,
			KillLocation = zone,
			KilledPlayerAt = DateTime.Now
		};
		RollNewLife(nemesisRecord, Roster);
		return nemesisRecord;
	}

	private void OnNemesisCrippled(string id, CrippleLevel level)
	{
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == id);
		if (nemesisRecord == null)
		{
			return;
		}
		nemesisRecord.AddFear(Balance.FearPerCripple);
		nemesisRecord.AddYourNotoriety(10);
		if (level >= CrippleLevel.SpineBroken)
		{
			if (!nemesisRecord.Paralyzed)
			{
				GrudgeNotice(nemesisRecord.FullName + " will never walk again.");
			}
			nemesisRecord.Paralyzed = true;
		}
		else
		{
			nemesisRecord.Limp = true;
		}
		SaveRoster();
	}

	private NemesisTag NemesisTagFor(int handle)
	{
		NemesisRecord nemesisRecord = null;
		bool flag = false;
		NemesisRecord value;
		NemesisRecord value2;
		if (_activeNemesisPed != null && ActiveRecord != null && handle == _activeNemesisPed.Handle)
		{
			nemesisRecord = ActiveRecord;
		}
		else if (_worldNemeses.TryGetValue(handle, out value))
		{
			nemesisRecord = value;
		}
		else if (_followerOf.TryGetValue(handle, out value2))
		{
			nemesisRecord = value2;
			flag = true;
		}
		else if (_sightingPed != null && handle == _sightingPed.Handle)
		{
			nemesisRecord = _sightingRec;
		}
		Named value3;
		string text = (_named.TryGetValue(handle, out value3) ? value3.Name : null);
		if (nemesisRecord == null)
		{
			if (text != null)
			{
				return new NemesisTag
				{
					PersonName = text
				};
			}
			return null;
		}
		return new NemesisTag
		{
			Id = nemesisRecord.Id,
			Name = (nemesisRecord.FullName ?? ""),
			IsFollower = flag,
			PersonName = (flag ? text : nemesisRecord.Name)
		};
	}

	private NemesisFate FateOf(string id)
	{
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == id);
		if (nemesisRecord == null)
		{
			return NemesisFate.Unknown;
		}
		if (!nemesisRecord.IsAlive)
		{
			return NemesisFate.Dead;
		}
		if (nemesisRecord.PresumedDead)
		{
			return NemesisFate.PresumedDead;
		}
		if (nemesisRecord.TimesCheatedDeath <= 0)
		{
			return NemesisFate.Hunting;
		}
		return NemesisFate.CheatedDeath;
	}

	internal void Forget(NemesisRecord rec)
	{
		if (rec == ActiveRecord)
		{
			ReleaseActive();
		}
		RemoveRecord(rec, "forgotten (menu)");
		SaveRoster();
	}

	private static List<NemesisRecord> SafeStoryGang()
	{
		try
		{
			return StoryGang.Build();
		}
		catch (Exception ex)
		{
			StoryBuildError = ex.ToString();
			return new List<NemesisRecord>();
		}
	}

	internal NemesisRecord FindRecord(string id)
	{
		object obj;
		if (id != null)
		{
			obj = Roster.FirstOrDefault((NemesisRecord r) => r.Id == id);
			if (obj == null)
			{
				return StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == id);
			}
		}
		else
		{
			obj = null;
		}
		return (NemesisRecord)obj;
	}

	private void SummonWithDot(NemesisRecord rec, EncounterKind kind, Ped player)
	{
		_encForced = true;
		try
		{
			SpawnEncounter(rec, kind, player);
		}
		finally
		{
			_encForced = false;
		}
	}

	internal string NewId(int len)
	{
		byte[] array = new byte[len / 2 + 1];
		_rng.NextBytes(array);
		array[0] ^= (byte)(++_idCounter * 37 + Environment.TickCount);
		array[1] ^= (byte)(Environment.TickCount >> 8);
		string id = BitConverter.ToString(array).Replace("-", "").ToLowerInvariant()
			.Substring(0, len);
		if (!Roster.Any((NemesisRecord n) => n.Id == id))
		{
			return id;
		}
		return Guid.NewGuid().ToString("N").Substring(0, len);
	}

	internal unsafe void RemoveRecord(NemesisRecord rec, string why)
	{
		if (rec == null || !Roster.Remove(rec))
		{
			return;
		}
		foreach (NemesisRecord item in Roster)
		{
			if (item.BossId == rec.Id)
			{
				item.BossId = null;
			}
			if (item.KinId == rec.Id)
			{
				item.KinId = null;
			}
			item.FriendIds?.Remove(rec.Id);
			item.Ties?.RemoveAll((Tie t) => t.Id == rec.Id);
		}
		foreach (int item2 in (from kv in _worldNemeses
			where kv.Value == rec
			select kv.Key).ToList())
		{
			_worldNemeses.Remove(item2);
		}
		if (_roadAmbush?.Rec == rec)
		{
			_roadAmbush = null;
		}
		foreach (LeaderSeat value in Seats.Values)
		{
			if (value.Successor == rec.Id)
			{
				value.Successor = null;
			}
		}
		foreach (TownMan item3 in _townMen.Where((TownMan t) => t.Rec == rec).ToList())
		{
			if (item3.Blip != null && item3.Blip.Exists())
			{
				item3.Blip.Delete();
			}
			if (item3.Ped != null && item3.Ped.Exists())
			{
				int handle = item3.Ped.Handle;
				item3.Ped.BlockPermanentEvents = false;
				ReleaseIfOurs(&handle);
			}
			_townMen.Remove(item3);
		}
		GrudgeLog("ROSTER removed " + rec.Name + " (" + rec.Id + "): " + why);
	}

	internal void Promote(NemesisRecord rec)
	{
		if (rec.Rank < 5)
		{
			rec.Rank++;
			History(rec, $"PROMOTED to rank {rec.Rank}", Theme.Gold);
			if (rec == ActiveRecord && _activeNemesisPed != null && _activeNemesisPed.Exists())
			{
				ApplyRankStats(_activeNemesisPed, rec, fullHeal: false);
			}
			SaveRoster();
		}
	}

	internal void SetRandomAmbushes(bool enabled)
	{
		Config.RandomAmbushes = enabled;
		Config.Save();
		GrudgeLog("SETTINGS random encounters " + (enabled ? "ON" : "OFF") + " (the menu's R, debug)");
		ShowNotification("enRAGE: random encounters " + (enabled ? "on" : "OFF - nobody comes for you until you turn them back on (R in this menu)") + ".");
	}

	internal void NoteOutcome(NemesisRecord rec, string outcome)
	{
		if (rec != null && outcome != null)
		{
			string text = outcome.ToLowerInvariant();
			rec.ArcStep = ((!(rec.LastOutcome == text)) ? 1 : (rec.ArcStep + 1));
			rec.LastOutcome = text;
			GrudgeLog($"ARC {rec.Name}: last outcome '{text}' ({rec.ArcStep}x in a row)");
		}
	}

	private unsafe void ReleaseActive()
	{
		if (_encEnd.HasValue && ActiveRecord != null && ActiveRecord.IsAlive && !ActiveRecord.PresumedDead && !_aborting)
		{
			NemesisRecord activeRecord = ActiveRecord;
			Ped character = Game.Player.Character;
			WorldPart worldPart = PartFor(activeRecord, _encEnd.Value.outcome, _encEnd.Value.how);
			worldPart.Effects.Add($"hate {activeRecord.Hate}");
			if (activeRecord.Fear > 0)
			{
				worldPart.Effects.Add($"fear {activeRecord.Fear}");
			}
			try
			{
				ChronicleDeed("encounter", activeRecord.Name + " came for you", "near " + GetZoneName(character.Position), character.Position, activeRecord.Gang, worldPart);
			}
			catch
			{
			}
			NoteOutcome(activeRecord, _encEnd.Value.outcome);
			bool flag = _activeNemesisPed != null && _bulletImpact.LatestWound(_activeNemesisPed.Handle) != null;
			string text = _encEnd.Value.outcome.ToUpperInvariant();
			if (text.Contains("GOT AWAY") || text.Contains("BACKED OFF"))
			{
				GainRankXp(activeRecord, (!flag) ? 1 : 2, "lived through it (" + _encEnd.Value.outcome.ToLowerInvariant() + (flag ? ", wounded by you" : "") + ")");
			}
		}
		_encEnd = null;
		if (_activeBlip != null && _activeBlip.Exists())
		{
			_activeBlip.Delete();
		}
		if (_activeNemesisPed != null && _activeNemesisPed.Exists())
		{
			int handle = _activeNemesisPed.Handle;
			if (!_activeNemesisPed.IsDead || LooksKnockedOut(_activeNemesisPed))
			{
				_activeNemesisPed.RelationshipGroup = PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(handle);
				ForgetSwitch(handle);
				PED.SET_PED_KEEP_TASK(handle, toggle: false);
				if (ActiveRecord != null)
				{
					_worldNemeses[handle] = ActiveRecord;
				}
			}
			ReleaseIfOurs(&handle);
		}
		if (!_aborting && _lastEncFought)
		{
			EnlistHiredGuns();
		}
		_lastEncFought = true;
		ReleaseFollowers();
		_activeBlip = null;
		_activeNemesisPed = null;
		ActiveRecord = null;
		_activeLastPunchTime = -100000;
		_activeDownSince = -1;
	}

	private string GetZoneName(Vector3 pos)
	{
		uint key = ZONE._GET_MAP_ZONE_AT_COORDS(pos.X, pos.Y, pos.Z, 1);
		if (ZoneNames.TryGetValue(key, out var value))
		{
			return value;
		}
		uint key2 = ZONE._GET_MAP_ZONE_AT_COORDS(pos.X, pos.Y, pos.Z, 10);
		if (ZoneNames.TryGetValue(key2, out value))
		{
			return value;
		}
		return "the wilds";
	}

	internal void ShowNotification(string text)
	{
		RDR2.UI.Screen.PrintSubtitle(UiDraw.TheFix(text).Replace("  ", " ").Replace(" .", ".")
			.Replace(" ,", ","));
	}

	private void Guard(string what, Action a)
	{
		Crumbs.Mark(what);
		if (StartupTrace.TicksLeft > 0)
		{
			StartupTrace.Write("  " + what);
		}
		long elapsedTicks = _saveClock.ElapsedTicks;
		double modelWaitMs = ModelWaitMs;
		int num = _guardDepth++;
		try
		{
			a();
		}
		catch (ThreadAbortException)
		{
			_guardDepth = num;
			throw;
		}
		catch (Exception ex2)
		{
			ErrorCount++;
			string key = what + ":" + ex2.GetType().Name + ":" + ex2.Message;
			LastError = what + ": " + ex2.GetType().Name + " " + ex2.Message;
			if (!_errorLoggedAt.TryGetValue(key, out var value) || Game.GameTime - value > 60000)
			{
				_errorLoggedAt[key] = Game.GameTime;
				GrudgeLog($"ERROR in {what}: {ex2}");
			}
		}
		_guardDepth = num;
		double num2 = (double)(_saveClock.ElapsedTicks - elapsedTicks) * 1000.0 / (double)Stopwatch.Frequency;
		double num3 = Math.Max(0.0, ModelWaitMs - modelWaitMs);
		double num4 = Math.Max(0.0, num2 - num3);
		NotePerf(what, num4, num3);
		switch (num)
		{
		case 1:
			_systemsMs += num4;
			if (num4 > _tickTopMs)
			{
				_tickTopMs = num4;
				_tickTop = what;
			}
			break;
		case 0:
			if (what == "tick")
			{
				NotePerf("(outside the systems)", Math.Max(0.0, num4 - _systemsMs), 0.0);
				_systemsMs = 0.0;
				_lastTickHeld = num4;
				_lastTickWaited = num3 > 0.0;
				_lastTickTop = _tickTop;
				_lastTickTopMs = _tickTopMs;
				_tickTop = null;
				_tickTopMs = 0.0;
			}
			break;
		}
		if (num4 >= 8.0 && what != "saves" && (!_slowLoggedAt.TryGetValue(what, out var value2) || Game.GameTime - value2 > 60000))
		{
			_slowLoggedAt[what] = Game.GameTime;
			GrudgeLog((num3 >= 1.0) ? $"SLOW {what} held the frame {num4:0} ms (and waited {num3:0} ms for models to load - the game ran on meanwhile)" : $"SLOW {what} took {num4:0} ms");
		}
	}

	private void NoteFrame()
	{
		double num = (double)_saveClock.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
		if (_lastTickAt > 0.0)
		{
			double num2 = num - _lastTickAt;
			if (!_lastTickWaited && !_busy && num2 >= 50.0 && num2 < 1500.0)
			{
				_slowFrames++;
				if (_lastTickHeld >= num2 * 0.5)
				{
					_slowFramesMod++;
					if (Environment.TickCount - _slowFrameLoggedAt > 1000)
					{
						_slowFrameLoggedAt = Environment.TickCount;
						GrudgeLog(string.Format("SLOWFRAME {0:0} ms - the mod held {1:0} ms of it, most in {2} ({3:0} ms)", num2, _lastTickHeld, _lastTickTop ?? "(between its systems)", _lastTickTopMs));
					}
				}
			}
		}
		_lastTickAt = num;
	}

	private void NotePerf(string what, double ms, double waited)
	{
		if (!_perf.TryGetValue(what, out var value))
		{
			_perf[what] = new PerfStat
			{
				First = ms,
				Waited = waited
			};
			return;
		}
		value.Total += ms;
		value.Calls++;
		value.Waited += waited;
		if (ms > value.Worst)
		{
			value.Worst = ms;
		}
		if (ms >= 16.0)
		{
			value.Over16++;
		}
	}
}
