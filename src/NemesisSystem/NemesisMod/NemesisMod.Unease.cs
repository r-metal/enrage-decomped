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
	private sealed class PendingExit
	{
		public Ped P;

		public int Player;

		public string How;

		public int From;

		public int Deadline;
	}

	private readonly Dictionary<int, float> _unease = new Dictionary<int, float>();

	private readonly Dictionary<int, int> _likeGreetAt = new Dictionary<int, int>();

	private readonly HashSet<int> _walkedOff = new HashSet<int>();

	private int _nextUnease;

	internal string UneaseNote = "";

	private const ulong IsMinigameInProgressFn = 17643059143154067995uL;

	private const ulong GetEntityScriptFn = 3028850156319117423uL;

	private const ulong ScenarioTypePedIsUsingFn = 3244124155998084514uL;

	private static readonly string[] TableGameScripts = new string[4] { "poker", "blackjack", "domino", "finger" };

	private readonly Dictionary<int, (bool table, string script, int at)> _tableGame = new Dictionary<int, (bool, string, int)>();

	private const int TableStandScenario = -55367021;

	private static readonly string[] LawTokens = new string[8] { "law", "police", "sheriff", "sherriff", "deputy", "marshal", "pinkerton", "ranger" };

	private uint _copGroup;

	private readonly Dictionary<uint, bool> _lawModel = new Dictionary<uint, bool>();

	private int _lawPed;

	private int _lawSince;

	private int _lawArrivedAt;

	private int _lawSeenSince;

	private int _lawSeenPed;

	private int _lawCooldownUntil;

	private int _lawWarnings;

	private static readonly string[] LawFirst = new string[4] { "Easy there. Put that iron away.", "You planning on using that? Holster it.", "Not in my town, friend. Put it away.", "Whoa now. Away with that." };

	private int _maskCheckAt;

	private bool _maskedNow;

	private bool _gunWasOut;

	private readonly HashSet<int> _maskUnease = new HashSet<int>();

	private readonly HashSet<int> _maskOnly = new HashSet<int>();

	private readonly HashSet<int> _stoodUp = new HashSet<int>();

	private readonly Dictionary<int, int> _toldToFlee = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _uneaseFearAt = new Dictionary<int, int>();

	private readonly List<PendingExit> _pendingExits = new List<PendingExit>();

	private const int UnmaskMemoryMs = 600000;

	internal const float MaskedFearMult = 1.25f;

	private static int WalkOffAt(string temper)
	{
		return Tempers.Of(temper).WalkOffAt;
	}

	private string TemperOf(Ped p)
	{
		if (_provoked.TryGetValue(p.Handle, out var value))
		{
			return value.Temper.ToString();
		}
		if (!_acqOf.TryGetValue(p.Handle, out var value2) || value2.Temper == null)
		{
			return ShadowOf(p).Personality?.ToString() ?? "";
		}
		return value2.Temper;
	}

	private bool UneaseSkips(Ped p)
	{
		int handle = p.Handle;
		if (RecordAt(handle) == null && !_combatants.ContainsKey(handle) && !PED.IS_PED_IN_COMBAT(handle, 0) && !IsPlaced(handle) && !p.IsHogtied && !IsLawman(p) && !HardMan(p) && !GameOwned(p) && !GameEventPed(p) && !PED.IS_PED_IN_ANY_VEHICLE(handle, atGetIn: false) && !p.IsOnMount)
		{
			return AtTableGame(handle);
		}
		return true;
	}

	private static bool PlayingTableGame()
	{
		try
		{
			return Function.Call<bool>(17643059143154067995uL, Array.Empty<InputArgument>());
		}
		catch
		{
			return false;
		}
	}

	private unsafe string ScriptOf(int h)
	{
		if (!ENTITY.IS_ENTITY_A_MISSION_ENTITY(h) || BelongsToUs(h))
		{
			return null;
		}
		ulong num = 0uL;
		Crumbs.Native("table game script");
		ulong num2 = Function.Call<ulong>(3028850156319117423uL, new InputArgument[2]
		{
			h,
			new InputArgument((ulong)(&num))
		});
		if (num2 != 0L)
		{
			return Marshal.PtrToStringAnsi(new IntPtr((long)num2));
		}
		return null;
	}

	internal bool AtTableGame(int h)
	{
		if (_tableGame.TryGetValue(h, out (bool, string, int) value) && (value.Item1 || Game.GameTime - value.Item3 < 10000))
		{
			return value.Item1;
		}
		string s = null;
		try
		{
			s = ScriptOf(h);
		}
		catch
		{
		}
		bool flag = s != null && TableGameScripts.Any((string k) => s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
		int num;
		int num2;
		if (s != null)
		{
			num = ((s.Length == 0) ? 1 : 0);
			if (num != 0 && !PED.IS_PED_USING_ANY_SCENARIO(h))
			{
				num2 = ((ENTITY.GET_ENTITY_SPEED(h) < 0.3f) ? 1 : 0);
				goto IL_00a7;
			}
		}
		else
		{
			num = 0;
		}
		num2 = 0;
		goto IL_00a7;
		IL_00a7:
		bool flag2 = (byte)num2 != 0;
		bool flag3 = num != 0 && !flag2 && ScenarioTypeOf(h) == -55367021;
		bool flag4 = flag || flag2 || flag3;
		if (flag4)
		{
			GrudgeLog(string.Format("TABLEGAME {0}, ped {1}, is a card player ({2}) - the mod leaves him be", WhoIs(h), h, flag ? ("script '" + s + "'") : (flag2 ? "a game script's man seated still, in no activity" : "in the table's standing activity")));
		}
		_tableGame[h] = (flag4, s, Game.GameTime);
		return flag4;
	}

	private static int ScenarioTypeOf(int h)
	{
		try
		{
			return Function.Call<int>(3244124155998084514uL, new InputArgument[1] { h });
		}
		catch
		{
			return 0;
		}
	}

	private string BusyNote(int h)
	{
		_tableGame.TryGetValue(h, out (bool, string, int) value);
		if (value.Item1)
		{
			return "a card player (script '" + value.Item2 + "')";
		}
		string text = ((value.Item3 > 0) ? value.Item2 : null);
		if (text == null)
		{
			try
			{
				text = ScriptOf(h);
			}
			catch
			{
			}
		}
		string arg = ((text != null) ? (", script '" + text + "'") : "");
		if (!PED.IS_PED_USING_ANY_SCENARIO(h))
		{
			return "standing" + ((text != null) ? (" (script '" + text + "')") : "");
		}
		return $"busy (scenario 0x{ScenarioTypeOf(h):X8}{arg})";
	}

	private void UpdateUnease(Ped player)
	{
		if (Game.GameTime < _nextUnease)
		{
			return;
		}
		_nextUnease = Game.GameTime + 250;
		if (MISC.GET_MISSION_FLAG() || PlayingTableGame() || !Config.TownsfolkReactions)
		{
			return;
		}
		bool flag = WEAPON.IS_PED_ARMED(player.Handle, 4);
		bool flag2 = !flag && WEAPON.IS_PED_ARMED(player.Handle, 1);
		bool flag3 = flag || flag2;
		if (flag3 && !_gunWasOut)
		{
			_maskCheckAt = 0;
		}
		_gunWasOut = flag3;
		float num = RepUneaseMult(RepAt(player.Position));
		if (Game.GameTime >= _maskCheckAt)
		{
			_maskCheckAt = Game.GameTime + 1000;
			try
			{
				_maskedNow = PlayerMasked(player);
			}
			catch
			{
				_maskedNow = false;
			}
		}
		bool maskedNow = _maskedNow;
		float num2 = (maskedNow ? Balance.MaskedUneaseRange : 15f);
		float num3 = (flag ? 1f : Balance.MeleeUneaseMult);
		string text = (flag ? "gun" : "blade");
		Vector3 position = player.Position;
		HashSet<int> hashSet = new HashSet<int>();
		if (flag3 || maskedNow)
		{
			HashSet<int> hashSet2 = null;
			if (flag3)
			{
				int count = (maskedNow ? (Balance.UneaseCrowd + Balance.UneaseCrowd / 2) : Balance.UneaseCrowd);
				List<(int h, float score)> list = new List<(int h, float score)>();
				foreach (KeyValuePair<int, Named> item in _named)
				{
					int key = item.Key;
					if (!item.Value.Story && !_walkedOff.Contains(key) && ENTITY.DOES_ENTITY_EXIST(key) && !ENTITY.IS_ENTITY_DEAD(key))
					{
						float num4 = ENTITY.GET_ENTITY_COORDS(key, alive: true, realCoords: true).DistanceTo(position);
						if (!(num4 > num2) && !UneaseSkips(new Ped(key)))
						{
							list.Add((key, num4 / Math.Max(0.3f, TemperFearGain(TemperOf(new Ped(key))))));
						}
					}
				}
				hashSet2 = new HashSet<int>(from c in list.OrderBy(((int h, float score) c) => c.score).Take(count)
					select c.h);
			}
			foreach (KeyValuePair<int, Named> item2 in _named.ToList())
			{
				int key2 = item2.Key;
				if (item2.Value.Story || !ENTITY.DOES_ENTITY_EXIST(key2) || ENTITY.IS_ENTITY_DEAD(key2) || (hashSet2 != null && !hashSet2.Contains(key2) && !_walkedOff.Contains(key2)))
				{
					continue;
				}
				Ped p = new Ped(key2);
				float num5 = p.Position.DistanceTo(position);
				if (num5 > num2 || UneaseSkips(p) || (num5 > 4f && !ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT(key2, player.Handle, 17)))
				{
					continue;
				}
				string text2 = TemperOf(p);
				int num6 = WalkOffAt(text2);
				if (!flag3)
				{
					if (!_maskOnly.Contains(key2) && !_unease.ContainsKey(key2))
					{
						GrudgeLog(string.Format("UNEASE mask: {0} ({1}) at {2:0} m eyes you", item2.Value.Name, (text2 == "") ? "?" : text2, num5));
					}
					float num7 = Math.Min(num6 - 1, Balance.MaskOnlyFear * TemperFearGain(text2));
					_unease.TryGetValue(key2, out var value);
					if (value < num7)
					{
						_unease[key2] = Math.Min(num7, value + 2f);
					}
					_maskOnly.Add(key2);
					hashSet.Add(key2);
					continue;
				}
				float num8 = Math.Max(0f, 1f - num5 / num2);
				if (maskedNow && num5 <= 8f)
				{
					num8 = Math.Max(num8, Balance.MaskedFloor);
				}
				float num9 = num8 * 8f * TemperFearGain(text2) * num * num3;
				if (maskedNow)
				{
					num9 *= Balance.MaskedUneaseMult;
					num6 = Math.Min(num6, Balance.MaskedWalkOffCap);
				}
				int num10 = 0;
				try
				{
					num10 = RecentMates(key2).Count((Ped m) => m != null && m.Exists() && !m.IsDead && m.Position.DistanceTo(p.Position) < 6f);
				}
				catch
				{
				}
				if (num10 > 0)
				{
					num9 /= 1f + Balance.GroupCalm * (maskedNow ? 0.5f : 1f) * (float)Math.Min(num10, 5);
				}
				if (!_unease.ContainsKey(key2) || _maskOnly.Contains(key2))
				{
					GrudgeLog(string.Format("UNEASE starts: {0} ({1}, walks off at {2}) at {3:0} m - {4:0.0}/s, {5}{6}{7}", item2.Value.Name, (text2 == "") ? "?" : text2, num6, num5, num9, text, maskedNow ? " (MASKED)" : "", (num10 > 0) ? $", {num10} with him" : ""));
				}
				if (_maskOnly.Contains(key2) && _unease.TryGetValue(key2, out var value2))
				{
					_unease[key2] = value2 / 3f;
				}
				_maskOnly.Remove(key2);
				_unease.TryGetValue(key2, out var value3);
				value3 = Math.Min(100f, value3 + num9 * 0.25f);
				_unease[key2] = value3;
				hashSet.Add(key2);
				if (maskedNow)
				{
					_maskUnease.Add(key2);
				}
				bool flag4 = !_walkedOff.Contains(key2) && PED.IS_PED_USING_ANY_SCENARIO(key2);
				if (flag4)
				{
					num6 = Math.Max(5, (int)((float)num6 * (maskedNow ? 0.2f : 0.5f)));
				}
				if (!_walkedOff.Contains(key2) && value3 >= (float)num6 && flag4)
				{
					GrudgeLog($"UNEASE {item2.Value.Name} was sitting in a scenario - leaves early at {value3:0} (line {num6})");
				}
				if (_walkedOff.Contains(key2) || !(value3 >= (float)num6))
				{
					continue;
				}
				_walkedOff.Add(key2);
				_recognisedAt[key2] = Game.GameTime;
				string text3 = BusyNote(key2);
				string text4 = GoAway(p, player, text2, value3, maskedNow);
				string name = item2.Value.Name;
				if (maskedNow)
				{
					GrudgeLog("UNEASY " + name + " (" + text2 + ") " + text4 + " - a masked man with a " + (flag ? "gun" : "knife") + " [" + text3 + "]");
					Feed("UNEASY " + name + " (" + text2.ToLowerInvariant() + ") " + text4 + " - a masked man with a " + (flag ? "gun" : "knife"), Theme.Dim);
					continue;
				}
				RememberPerson(key2, met: false);
				if (_acqOf.TryGetValue(key2, out var value4))
				{
					int fear = value4.Fear;
					bool num11 = value4.Fear > 0 || (value4.Witnessed != null && value4.Witnessed.Any((string w) => w.StartsWith("saw you") || w.StartsWith("heard")));
					int value5;
					bool flag5 = _uneaseFearAt.TryGetValue(key2, out value5) && Game.GameTime - value5 < 300000;
					if (num11 && !flag5)
					{
						value4.Fear = Math.Min(100, value4.Fear + Math.Max(1, (int)Math.Round(5f * TemperFearGain(text2))));
						_uneaseFearAt[key2] = Game.GameTime;
					}
					if (string.IsNullOrEmpty(value4.Temper) && text2 != "")
					{
						value4.Temper = text2;
					}
					AddSeen(value4, flag ? "didn't like your gun out" : "didn't like your blade out");
					GrudgeLog($"UNEASY {name} ({text2}) walked off at {value3:0} (line {WalkOffAt(text2)}) - fear {fear} -> {value4.Fear} [{text3}]");
				}
				Feed("UNEASY " + name + " (" + text2.ToLowerInvariant() + ") " + text4 + " - your " + text, Theme.Dim);
			}
		}
		bool flag6 = PED.IS_PED_IN_COMBAT(player.Handle, 0) || _combatants.Values.Any((int t) => Game.GameTime - t < 5000) || LAW.IS_LAW_INCIDENT_ACTIVE(Game.Player.Handle);
		UpdateLawApproach(player, flag, flag6);
		foreach (int item3 in _unease.Keys.ToList())
		{
			if (!hashSet.Contains(item3))
			{
				Acquaintance value6;
				bool flag7 = _acqOf.TryGetValue(item3, out value6) && value6.Fear > 0;
				float num12 = (flag6 ? 1.5f : (flag7 ? 3f : 12f));
				if (!maskedNow && (_maskUnease.Contains(item3) || _maskOnly.Contains(item3)))
				{
					num12 = Math.Max(num12, Balance.MaskOffDrain);
				}
				float num13 = _unease[item3] - num12 * 0.25f;
				if (num13 <= 0f || !ENTITY.DOES_ENTITY_EXIST(item3))
				{
					_unease.Remove(item3);
					_walkedOff.Remove(item3);
					_maskUnease.Remove(item3);
					_maskOnly.Remove(item3);
					_stoodUp.Remove(item3);
				}
				else
				{
					_unease[item3] = num13;
				}
			}
		}
		UneaseNote = ((_unease.Count == 0) ? "unease: nobody" : ("unease: " + string.Join(", ", from kv in _unease.OrderByDescending((KeyValuePair<int, float> kv) => kv.Value).Take(5)
			select string.Format("{0} {1:0}{2}", _named.TryGetValue(kv.Key, out var value7) ? value7.Name : kv.Key.ToString(), kv.Value, _walkedOff.Contains(kv.Key) ? " (left)" : ""))));
	}

	internal bool IsLawman(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return false;
		}
		uint hash = (uint)p.Model.Hash;
		if (!_lawModel.TryGetValue(hash, out var value))
		{
			string name = ModelName(hash);
			value = LawTokens.Any((string t) => name.Contains(t));
			_lawModel[hash] = value;
		}
		if (value)
		{
			return true;
		}
		if (_copGroup == 0)
		{
			_copGroup = MISC.GET_HASH_KEY("REL_COP");
		}
		return PED.GET_PED_RELATIONSHIP_GROUP_HASH(p.Handle) == _copGroup;
	}

	private void UpdateLawApproach(Ped player, bool gun, bool threat)
	{
		Vector3 position = player.Position;
		if (MISC.GET_MISSION_FLAG())
		{
			if (_lawPed != 0)
			{
				GrudgeLog("LAW a mission started - let go");
				EndLaw(release: false);
			}
			_lawSeenPed = 0;
			return;
		}
		if (_lawPed != 0)
		{
			Ped ped = new Ped(_lawPed);
			bool flag = !ENTITY.DOES_ENTITY_EXIST(_lawPed) || ENTITY.IS_ENTITY_DEAD(_lawPed) || ped.Position.DistanceTo(position) > 60f;
			if (flag || threat || PED.IS_PED_IN_COMBAT(_lawPed, 0))
			{
				GrudgeLog("LAW approach ended (" + (flag ? "gone" : "the game took over") + ")");
				EndLaw(release: false);
				return;
			}
			float num = ped.Position.DistanceTo(position);
			if (!gun)
			{
				if (_lawArrivedAt != 0)
				{
					ShowSubtitle((_rng.Next(2) == 0) ? "Lawman: Much obliged." : "Lawman: That's better. Move along.");
				}
				GrudgeLog("LAW you put it away - he goes about his business");
				RepNote("LAW you put it away - he moved on");
				EndLaw(release: true);
			}
			else if (_lawArrivedAt == 0)
			{
				if (num < 4f)
				{
					_lawArrivedAt = Game.GameTime;
					_lawWarnings = 1;
					TASK.TASK_TURN_PED_TO_FACE_ENTITY(_lawPed, player.Handle, 4000, 0f, 0f, 0f);
					TASK.TASK_LOOK_AT_ENTITY(_lawPed, player.Handle, 12000, 0, 51, 0);
					ShowSubtitle("Lawman: " + LawFirst[_rng.Next(LawFirst.Length)]);
					GrudgeLog($"LAW lawman {_lawPed} got to you ({(Game.GameTime - _lawSince) / 1000} s) - first warning");
				}
				else if (Game.GameTime - _lawSince > 40000)
				{
					GrudgeLog("LAW he never got to you - let go");
					EndLaw(release: false);
				}
			}
			else if (Game.GameTime - _lawArrivedAt > 25000)
			{
				GrudgeLog("LAW done warning you - left to the game");
				EndLaw(release: false);
			}
			return;
		}
		if (!gun || threat || Game.GameTime < _lawCooldownUntil)
		{
			_lawSeenPed = 0;
			return;
		}
		Ped ped2 = null;
		float num2 = 30f;
		foreach (Ped item in PedsAround(player))
		{
			if (!(item == null) && item.Exists() && !item.IsDead && !item.IsPlayer && IsLawman(item) && !PED.IS_PED_IN_COMBAT(item.Handle, 0) && !GameOwned(item) && !IsStoryPed(item) && !GameEventPed(item) && !IsOurs(item))
			{
				float num3 = item.Position.DistanceTo(position);
				if (num3 < num2 && ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT(item.Handle, player.Handle, 17))
				{
					ped2 = item;
					num2 = num3;
				}
			}
		}
		if (ped2 == null)
		{
			_lawSeenPed = 0;
		}
		else if (_lawSeenPed != ped2.Handle)
		{
			_lawSeenPed = ped2.Handle;
			_lawSeenSince = Game.GameTime;
		}
		else if (Game.GameTime - _lawSeenSince >= 2000)
		{
			_lawPed = ped2.Handle;
			_lawSince = Game.GameTime;
			_lawArrivedAt = 0;
			_lawWarnings = 0;
			TASK.TASK_GO_TO_ENTITY(_lawPed, player.Handle, -1, 3f, 1.2f, 0f, 0);
			GrudgeLog($"LAW lawman {_lawPed} ({ModelName((uint)ped2.Model.Hash)}) saw your gun out at {num2:0} m - walking over");
			RepNote($"LAW a lawman saw your gun out at {num2:0} m - walking over");
		}
	}

	private void EndLaw(bool release)
	{
		if (release && _lawPed != 0 && ENTITY.DOES_ENTITY_EXIST(_lawPed) && !GameOwned(new Ped(_lawPed)))
		{
			TASK.CLEAR_PED_TASKS(_lawPed, p1: false, p2: false);
		}
		_lawPed = 0;
		_lawSeenPed = 0;
		_lawCooldownUntil = Game.GameTime + 120000;
	}

	private static int RunOdds(string temper)
	{
		return Tempers.Of(temper).RunOdds;
	}

	internal bool ToldToFlee(int h)
	{
		if (_toldToFlee.TryGetValue(h, out var value))
		{
			return Game.GameTime - value < 30000;
		}
		return false;
	}

	private void RunPendingExits()
	{
		for (int num = _pendingExits.Count - 1; num >= 0; num--)
		{
			PendingExit pendingExit = _pendingExits[num];
			if (pendingExit.P == null || !pendingExit.P.Exists() || pendingExit.P.IsDead)
			{
				_pendingExits.RemoveAt(num);
			}
			else if (pendingExit.How != "fight" && !Config.TownsfolkReactions)
			{
				_pendingExits.RemoveAt(num);
			}
			else
			{
				bool flag = !PED.IS_PED_USING_ANY_SCENARIO(pendingExit.P.Handle);
				if (Game.GameTime >= pendingExit.From && (flag || Game.GameTime >= pendingExit.Deadline))
				{
					_pendingExits.RemoveAt(num);
					if (pendingExit.How != "fight")
					{
						_toldToFlee[pendingExit.P.Handle] = Game.GameTime;
					}
					Leave(pendingExit.P, pendingExit.Player, pendingExit.How);
				}
			}
		}
	}

	internal void QueueFight(Ped p, Ped player, int ms)
	{
		_pendingExits.RemoveAll((PendingExit x) => x.P != null && x.P.Handle == p.Handle);
		_pendingExits.Add(new PendingExit
		{
			P = p,
			Player = player.Handle,
			How = "fight",
			From = Game.GameTime + ms,
			Deadline = Game.GameTime + 2500
		});
	}

	private void Leave(Ped p, int player, string how)
	{
		if (how.StartsWith("check:"))
		{
			string text = how.Substring(6);
			if (ENTITY.GET_ENTITY_SPEED(p.Handle) < 0.4f)
			{
				float distance = ((text == "runs") ? 40f : ((text == "hurries off") ? 30f : 22f));
				float speed = ((text == "runs") ? 3f : ((text == "hurries off") ? 2f : 1f));
				Flee(p.Handle, player, distance, 9000, speed);
				GrudgeLog("UNEASE " + WhoIs(p.Handle) + ": the walk off your path didn't take - away from you instead");
			}
			return;
		}
		float num;
		switch (how)
		{
		case "fight":
			TASK.TASK_COMBAT_PED(p.Handle, player, 0, 16);
			PED.SET_PED_KEEP_TASK(p.Handle, toggle: true);
			return;
		default:
			num = 22f;
			break;
		case "hurries off":
			num = 30f;
			break;
		case "runs":
			num = 40f;
			break;
		}
		float num2 = num;
		float num3 = ((how == "runs") ? 3f : ((how == "hurries off") ? 2f : 1f));
		Ped character = Game.Player.Character;
		Vector3 vector = ENTITY.GET_ENTITY_VELOCITY(character.Handle, 0);
		vector.Z = 0f;
		Vector3 vector2 = p.Position - character.Position;
		vector2.Z = 0f;
		bool flag = false;
		if (vector.Length() > 1f && vector2.Length() > 0.5f)
		{
			Vector3 vector3 = vector;
			vector3.Normalize();
			if (vector2.X * vector3.X + vector2.Y * vector3.Y > 0f)
			{
				float num4 = ((vector3.X * vector2.Y - vector3.Y * vector2.X >= 0f) ? 1f : (-1f));
				Vector3 vector4 = new Vector3((0f - vector3.Y) * num4, vector3.X * num4, 0f);
				Vector3 position = p.Position + vector4 * num2 * 0.85f - vector3 * num2 * 0.35f;
				Vector3 safeCoordForPed = World.GetSafeCoordForPed(position, onGround: true, 0);
				if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && new Vector3(safeCoordForPed.X - position.X, safeCoordForPed.Y - position.Y, 0f).Length() < 12f)
				{
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(p.Handle, safeCoordForPed.X, safeCoordForPed.Y, safeCoordForPed.Z, num3, -1, 1.5f, 0, 0f);
					flag = true;
					_pendingExits.Add(new PendingExit
					{
						P = p,
						Player = player,
						How = "check:" + how,
						From = Game.GameTime + 1500,
						Deadline = Game.GameTime + 1500
					});
				}
			}
		}
		if (!flag)
		{
			Flee(p.Handle, player, num2, 9000, num3);
		}
		GrudgeLog("UNEASE " + WhoIs(p.Handle) + " " + how + ": " + (flag ? "off to the side of your path" : "away from you"));
		if (how == "runs" && _rng.Next(100) < 60)
		{
			Say(p, "GENERIC_SHOCKED_HIGH", Game.Player.Character);
		}
		if (how == "walks off, glancing back")
		{
			TASK.TASK_LOOK_AT_ENTITY(p.Handle, player, 4000, 0, 51, 0);
		}
	}

	private string GoAway(Ped p, Ped player, string temper, float fear, bool masked = false)
	{
		if (p != null)
		{
			_toldToFlee[p.Handle] = Game.GameTime;
		}
		int num = Math.Min(95, RunOdds(temper) + (int)(fear / 3f) + (masked ? Balance.MaskedRunBonus : 0));
		int num2 = _rng.Next(100);
		string text = ((num2 < num) ? "runs" : ((num2 < num + 30) ? "hurries off" : ((_rng.Next(2) == 0) ? "walks off, glancing back" : "walks off")));
		if (p != null && PED.IS_PED_USING_ANY_SCENARIO(p.Handle) && (!(text == "runs") || !(fear >= 75f)))
		{
			TASK.CLEAR_PED_TASKS(p.Handle, p1: false, p2: false);
			_pendingExits.RemoveAll((PendingExit x) => x.P != null && x.P.Handle == p.Handle);
			_pendingExits.Add(new PendingExit
			{
				P = p,
				Player = player.Handle,
				How = text,
				From = Game.GameTime + 1200,
				Deadline = Game.GameTime + 3000
			});
			return text;
		}
		Leave(p, player.Handle, text);
		return text;
	}

	internal bool HardMan(Ped p)
	{
		if (p != null && p.Exists())
		{
			return Gangs.ForModel((uint)p.Model.Hash) != null;
		}
		return false;
	}

	internal float UneaseOf(int handle)
	{
		if (!_unease.TryGetValue(handle, out var value))
		{
			return 0f;
		}
		return value;
	}

	private void Liking(int handle, GrudgeAction a)
	{
		if (a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet)
		{
			try
			{
				NoteGreetScene(handle);
			}
			catch
			{
			}
		}
		if (a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet)
		{
			try
			{
				NoteGreetReply(handle);
			}
			catch
			{
			}
		}
		if (a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet || a == GrudgeAction.Insult || a == GrudgeAction.Taunt)
		{
			PressedAfterLeaving(handle, a);
		}
		if ((a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet || a == GrudgeAction.Defuse) && _likeGreetAt.TryGetValue(handle, out var value) && Game.GameTime - value < 300000)
		{
			return;
		}
		if (a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet || a == GrudgeAction.Defuse)
		{
			_likeGreetAt[handle] = Game.GameTime;
		}
		int num;
		switch (a)
		{
		default:
			num = ((CrimeOf(a) != null) ? (-20) : 0);
			break;
		case GrudgeAction.Insult:
		case GrudgeAction.Taunt:
			num = -5;
			break;
		case GrudgeAction.Defuse:
			num = 4;
			break;
		case GrudgeAction.EmoteGreet:
			num = 3;
			break;
		case GrudgeAction.Greet:
			num = 4;
			break;
		}
		int num2 = num;
		if (num2 == 0)
		{
			return;
		}
		if (!_acqOf.ContainsKey(handle))
		{
			RememberPerson(handle, a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet);
		}
		if (!_acqOf.TryGetValue(handle, out var value2))
		{
			return;
		}
		if (a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet || a == GrudgeAction.Defuse)
		{
			int num3 = GameDate.TodayKey();
			if (value2.LastGreetDay != num3)
			{
				value2.LastGreetDay = num3;
				num2 = Math.Max(num2, 5);
			}
		}
		num2 = LikeDelta(handle, num2);
		if (num2 > 0)
		{
			num2 = (int)Math.Round((float)num2 * GoodRepLikeMult(value2.Town));
		}
		int like = value2.Like;
		value2.Like = Math.Max(0, Math.Min(100, value2.Like + num2));
		if (value2.Like != like)
		{
			_peopleDirty = true;
			GrudgeLog($"LIKE {value2.Name}: {like} -> {value2.Like} ({a})");
			RepNote($"LIKE {value2.Name}: {like} to {value2.Like} ({a})");
		}
		SpreadLiking(value2, like);
		if (a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet)
		{
			NervousGreet(handle);
		}
	}

	internal void LikeBy(int handle, int delta, string why)
	{
		if (_acqOf.TryGetValue(handle, out var value))
		{
			delta = LikeDelta(handle, delta);
			int like = value.Like;
			value.Like = Math.Max(0, Math.Min(100, value.Like + delta));
			if (value.Like != like)
			{
				_peopleDirty = true;
				GrudgeLog($"LIKE {value.Name}: {like} -> {value.Like} ({why})");
				RepNote($"LIKE {value.Name}: {like} to {value.Like} ({why})");
			}
			SpreadLiking(value, like);
		}
	}

	private void SpreadLiking(Acquaintance acq, int before)
	{
		if (acq == null || acq.LikeSpread || before >= 40 || acq.Like < 40 || acq.Circle == null)
		{
			return;
		}
		acq.LikeSpread = true;
		_peopleDirty = true;
		List<string> list = new List<string>();
		foreach (string key in acq.Circle)
		{
			Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == key && !x.Dead);
			if (acquaintance != null && acquaintance != acq)
			{
				int like = acquaintance.Like;
				acquaintance.Like = Math.Min(100, acquaintance.Like + 5);
				if (acquaintance.Like != like)
				{
					list.Add($"{acquaintance.Name} {like} -> {acquaintance.Like}");
				}
			}
		}
		if (list.Count > 0)
		{
			GrudgeLog("LIKE " + acq.Name + " speaks well of you to his friends: " + string.Join(", ", list));
		}
	}

	private static float CardBarStep(float s)
	{
		return 0.011407407f;
	}

	private static float CardBarsHeight(float s, float fear, float like, float hate)
	{
		int num = ((fear >= 1f) ? 1 : 0) + ((like >= 1f) ? 1 : 0) + ((hate >= 1f) ? 1 : 0);
		if (num != 0)
		{
			return (float)num * CardBarStep(s) + 0.004f + ((hate >= 1f) ? (0.012f * (s / 0.25f)) : 0f);
		}
		return 0f;
	}

	private string HateBarWhy(NemesisRecord rec, Provocation p)
	{
		if (rec != null)
		{
			HistoryEntry historyEntry = rec.History?.LastOrDefault();
			if (historyEntry == null)
			{
				return null;
			}
			TimeSpan t = DateTime.Now - historyEntry.At;
			return Trim(historyEntry.Text, 34) + " - " + Ago(t) + " ago";
		}
		if (p == null || p.Ledger.Count == 0)
		{
			return null;
		}
		int num = Math.Max(0, (Game.GameTime - p.LastActionAt) / 1000);
		return Trim(p.Ledger[p.Ledger.Count - 1], 30) + " - " + ((num < 60) ? (num + " s") : (num / 60 + " min")) + " ago";
	}

	private static string Trim(string t, int n)
	{
		if (!string.IsNullOrEmpty(t))
		{
			if (t.Length > n)
			{
				return t.Substring(0, n - 1) + "...";
			}
			return t;
		}
		return "";
	}

	private void DrawCardBars(float x, float y, float s, int a, float fear, float like, float hate, bool nemesis, string why = null)
	{
		if (fear < 1f && like < 1f && hate < 1f)
		{
			return;
		}
		float w = 0.046f * (s / 0.25f);
		float h = 0.0037037036f;
		float num = 0.026f * (s / 0.25f);
		float left = x - (w + num) / 2f;
		float bx = left + num;
		float yy = y + 0.006f;
		if (fear >= 1f)
		{
			Bar("FEAR", fear, (r: 120, g: 160, b: 235));
		}
		if (like >= 1f)
		{
			Bar("LIKES", like, (r: 110, g: 200, b: 110));
		}
		if (hate >= 1f)
		{
			(int, int, int) c = (nemesis ? UiDraw.HateColor(Hate.TierOf((int)hate)) : GrudgeColor(hate));
			Bar(nemesis ? "HATE" : "DISLIKE", hate, c);
			if (!string.IsNullOrEmpty(why))
			{
				float scale = 0.14f * (s / 0.25f);
				UiDraw.Text(why, x - Theme.MixedWidth(why, scale, 0.0222f) / 2f, yy - 0.003f, scale, c.Item1, c.Item2, c.Item3, (int)((float)a * 0.85f));
			}
		}
		void Bar(string label, float v, (int r, int g, int b) tuple)
		{
			UiDraw.Text(label, left, yy - 0.0045f, 0.15f * (s / 0.25f), tuple.r, tuple.g, tuple.b, (int)((float)a * 0.9f));
			UiDraw.Rect(bx, yy, w, h, 15, 15, 18, (int)((float)a * 0.75f));
			UiDraw.Rect(bx, yy, w * Math.Min(1f, v / 100f), h, tuple.r, tuple.g, tuple.b, a);
			yy += CardBarStep(s);
		}
	}
}
