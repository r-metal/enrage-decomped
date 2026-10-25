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
	private sealed class Fighter
	{
		public int H;

		public string Name;

		public string Temper;

		public NemesisRecord Rec;

		public float Nerve;

		public float Start;

		public float Lowest;

		public int LastHealth;

		public int WoundAt;

		public int LastShotAt;

		public int LastSeenFighting;

		public int StateUntil;

		public int DoneAt;

		public int SurrenderedAt;

		public int OrigGroup;

		public int FallBacks;

		public bool WasShooting;

		public bool HitYou;

		public bool Broke;

		public bool Done;

		public bool Again;

		public Vector3? Home;

		public bool CampBoss;

		public bool GaveGround;

		public int StartedAt;

		public int Wounds;

		public int DrainFrom;

		public int SideMax;

		public int NextSideAt;

		public int MoveWas = -1;

		public float Drained;

		public string State = "fighting";
	}

	private readonly Dictionary<int, Fighter> _fighters = new Dictionary<int, Fighter>();

	private int _nextNerve;

	private int _playerHealthWas = -1;

	private const int HoldWhileSide = 3;

	private const int HomeNerve = 15;

	private const float DrainCap = 30f;

	private const int DrainWindow = 5000;

	private static readonly string[] ArthurTauntsNear = new string[3] { "PLAYER_TAUNT_SINGLE_ENEMY_POS_NEAR", "PLAYER_TAUNT_ENEMY", "WON_FIGHT" };

	private static readonly string[] ArthurTauntsFar = new string[3] { "PLAYER_TAUNT_SINGLE_ENEMY_POS_FAR", "PLAYER_TAUNT_ENEMY", "WON_FIGHT" };

	private static readonly string[] ArthurTaunts = ArthurTauntsNear.Concat(ArthurTauntsFar).Distinct().ToArray();

	private readonly List<string> _tauntsThatPlay = new List<string>();

	private readonly HashSet<string> _tauntsTried = new HashSet<string>();

	private int _lastTauntAt = -100000;

	private readonly Dictionary<int, int> _recentFoes = new Dictionary<int, int>();

	private readonly HashSet<int> _runnerRolled = new HashSet<int>();

	private readonly HashSet<int> _letGoRunners = new HashSet<int>();

	private int _nextCombatTaunt;

	private const float GoreRifleRange = 25f;

	private static readonly string[] MonthNames = new string[12]
	{
		"January", "February", "March", "April", "May", "June", "July", "August", "September", "October",
		"November", "December"
	};

	internal bool NerveOut(int h)
	{
		if (!UnderArrest(h) && !_fightEnded.Contains(h))
		{
			if (_fighters.TryGetValue(h, out var value) && !value.Done)
			{
				return value.State != "fighting";
			}
			return false;
		}
		return true;
	}

	private static int BreakLine(string t)
	{
		return Tempers.Of(t).BreakLine;
	}

	private static int SurrenderOddsFor(string t)
	{
		return Tempers.Of(t).Surrender;
	}

	private static int TemperNerve(string t)
	{
		return Tempers.Of(t).Nerve;
	}

	private void UpdateNerve(Ped player)
	{
		UpdateRunners(player);
		try
		{
			UpdateCombatTaunts(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("SPEECH combat taunt failed: " + ex.Message);
		}
		if (Game.GameTime < _nextNerve)
		{
			return;
		}
		_nextNerve = Game.GameTime + 250;
		Vector3 me = player.Position;
		foreach (Ped item in PedsAround(player))
		{
			if (item == null || !item.Exists() || item.IsDead || item.IsPlayer || !item.IsHuman)
			{
				continue;
			}
			int handle = item.Handle;
			if (item.Position.DistanceTo(me) > 120f || (!PED.IS_PED_IN_COMBAT(handle, player.Handle) && (!_combatants.TryGetValue(handle, out var value) || Game.GameTime - value >= 2500)) || Kids.IsChild(item) || IsStoryPed(item))
			{
				continue;
			}
			_recentFoes[handle] = Game.GameTime;
			if (IsLawman(item))
			{
				continue;
			}
			if (!_fighters.TryGetValue(handle, out var value2) || (value2.Done && Game.GameTime - value2.DoneAt > 2000))
			{
				bool again = value2 != null;
				value2 = StartFighter(item, player);
				if (value2 == null)
				{
					continue;
				}
				value2.Again = again;
				_fighters[handle] = value2;
			}
			value2.LastSeenFighting = Game.GameTime;
		}
		TauntGameRunners(player);
		int health = player.Health;
		if (_playerHealthWas >= 0 && health < _playerHealthWas - 1)
		{
			Fighter fighter = (from fighter2 in _fighters.Values
				where !fighter2.Done && Game.GameTime - fighter2.LastShotAt < 1200 && ENTITY.DOES_ENTITY_EXIST(fighter2.H)
				orderby new Ped(fighter2.H).Position.DistanceTo(me)
				select fighter2).FirstOrDefault();
			if (fighter != null)
			{
				fighter.HitYou = true;
				NerveBy(fighter, 8f, "hit you");
			}
		}
		if (player.IsDead && _playerHealthWas > 0)
		{
			foreach (Fighter item2 in _fighters.Values.Where((Fighter x) => !x.Done))
			{
				NerveBy(item2, 15f, "you went down");
			}
		}
		_playerHealthWas = health;
		foreach (Fighter f in _fighters.Values.ToList())
		{
			if (f.Done)
			{
				continue;
			}
			bool flag = ENTITY.DOES_ENTITY_EXIST(f.H);
			Ped p = (flag ? new Ped(f.H) : null);
			if (!flag || p.IsDead || (Game.GameTime - f.LastSeenFighting > 8000 && f.State != "surrendered" && f.State != "hanging back" && f.State != "falling back"))
			{
				FinishFighter(f, p);
				continue;
			}
			if ((f.State == "surrendered" && IsActiveMan(f.H) && _enc != null && _enc.Surrendered) || UnderArrest(f.H))
			{
				continue;
			}
			if (f.State == "surrendered")
			{
				int handle2 = Game.Player.Handle;
				if (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(handle2, f.H) || PLAYER.IS_PLAYER_TARGETTING_ENTITY(handle2, f.H, p2: false) || Game.GameTime - GunMelee.LastShotAt < 10000 || InterroFor(f.H) != null || p.Position.DistanceTo(me) < 5f)
				{
					f.SurrenderedAt = Math.Max(f.SurrenderedAt, Game.GameTime);
				}
				bool flag2 = Game.GameTime - f.SurrenderedAt > 8000;
				if (!flag2 && Game.GameTime <= f.StateUntil && !(p.Position.DistanceTo(me) > 60f))
				{
					continue;
				}
				p.BlockPermanentEvents = StaysBlocked(p);
				NerveLog(f, flag2 ? "you've stopped threatening him - he gets up and goes" : "let go");
				bool flag3 = SparedNotSlipped(f.H, p.Position);
				if (!IsActiveMan(f.H))
				{
					NemesisRecord nemesisRecord = RecordAt(f.H);
					if (nemesisRecord != null)
					{
						if (flag3)
						{
							SparedHim(nemesisRecord, "gave up and you let him go");
						}
						else
						{
							SlippedAway(nemesisRecord, "gave up, then got up and ran");
						}
						goto IL_070e;
					}
				}
				if (!IsActiveMan(f.H) && flag3)
				{
					NoteMercy(FactionOfPed(p), NameOf(f), "h" + f.H);
				}
				else if (!IsActiveMan(f.H))
				{
					NerveLog(f, "slipped away while the fight was on - not spared");
				}
				goto IL_070e;
			}
			bool flag4 = PED.IS_PED_SHOOTING(f.H);
			if (flag4 && !f.WasShooting)
			{
				NerveBy(f, 1f, null);
				f.LastShotAt = Game.GameTime;
			}
			f.WasShooting = flag4;
			int health2 = p.Health;
			if (health2 <= 0 || PED.IS_PED_FATALLY_INJURED(f.H))
			{
				f.LastHealth = health2;
				continue;
			}
			if (f.LastHealth > 0 && health2 < f.LastHealth - 8 && Game.GameTime - f.WoundAt > 3000)
			{
				f.WoundAt = Game.GameTime;
				bool flag5 = !WEAPON.IS_PED_ARMED(player.Handle, 4) && !WEAPON.IS_PED_ARMED(player.Handle, 1);
				NerveBy(f, flag5 ? (-6) : ((f.Wounds++ == 0) ? (-20) : (-12)), flag5 ? "took a beating" : "wounded");
			}
			f.LastHealth = health2;
			if (!_fighters.Values.Any((Fighter o) => o != f && !o.Done && o.State == "fighting" && ENTITY.DOES_ENTITY_EXIST(o.H) && new Ped(o.H).Position.DistanceTo(p.Position) < 40f))
			{
				NerveBy(f, -0.25f, null);
			}
			if (f.State == "hanging back" && Game.GameTime > f.StateUntil)
			{
				int num = _fighters.Values.Count((Fighter o) => o != f && !o.Done && o.State == "fighting");
				if (num >= 2)
				{
					f.State = "fighting";
					f.LastSeenFighting = Game.GameTime;
					p.BlockPermanentEvents = StaysBlocked(p);
					TASK.TASK_COMBAT_PED(f.H, player.Handle, 0, 16);
					NerveLog(f, $"joined late ({num} of his side still fighting)");
					continue;
				}
				f.State = "slipped away";
				f.Broke = true;
				StartRunning(f.H, player);
				NerveLog(f, "slipped away - his side was losing");
				Feed("NERVE " + f.Name + " (" + f.Temper.ToLowerInvariant() + ") slips away - his side is losing", Theme.Warn);
			}
			else if (f.State == "falling back" && Game.GameTime > f.StateUntil)
			{
				f.State = "fighting";
				f.LastSeenFighting = Game.GameTime;
				p.BlockPermanentEvents = StaysBlocked(p);
				EndGiveGround(f);
				TASK.TASK_COMBAT_PED(f.H, player.Handle, 0, 16);
				NerveBy(f, 10f, "regrouped");
			}
			else
			{
				if (f.State == "fighting" && Game.GameTime >= f.NextSideAt)
				{
					f.NextSideAt = Game.GameTime + 1000;
					f.SideMax = Math.Max(f.SideMax, SideStillFighting(f, p));
				}
				if (f.State == "fighting" && !f.Broke && f.Nerve <= (float)BreakLine(f.Temper))
				{
					Break(f, p, player);
				}
			}
			continue;
			IL_070e:
			FinishFighter(f, p);
			StartRunning(f.H, player, (uint)f.OrigGroup);
		}
		if (_fighters.Count <= 60)
		{
			return;
		}
		foreach (int item3 in (from kv in _fighters
			where kv.Value.Done
			select kv.Key).ToList())
		{
			_fighters.Remove(item3);
		}
	}

	internal float OpeningNerve(Ped p, NemesisRecord rec, out string temper, out float lasting, out int allies, out float infamy, out float repPart)
	{
		int handle = p.Handle;
		temper = rec?.Personality?.ToString() ?? TemperOf(p);
		lasting = ((rec != null) ? EffectiveFear(rec) : (_acqOf.TryGetValue(handle, out var value) ? value.Fear : 0));
		allies = _fighters.Values.Count((Fighter o) => !o.Done && ENTITY.DOES_ENTITY_EXIST(o.H) && new Ped(o.H).Position.DistanceTo(p.Position) < 25f);
		infamy = RepAt(p.Position);
		repPart = ReputationNerve(temper, rec, infamy, lasting);
		float val = 50f + (float)TemperNerve(temper) + ((rec != null) ? ((float)(Mind.F(rec, "Bravery") - 50) / 2f) : 0f) - lasting / 2f + repPart + (float)Math.Min(20, allies * 4);
		return Math.Max(5f, Math.Min(100f, val));
	}

	private Fighter StartFighter(Ped p, Ped player)
	{
		if (GameOwned(p) || GameEventPed(p))
		{
			return null;
		}
		int h = p.Handle;
		NemesisRecord nemesisRecord = RecordAt(h);
		if (nemesisRecord == null && !_named.ContainsKey(h) && LookReadable(p))
		{
			_named[h] = NameByLook(p);
			_namesGiven++;
		}
		Named value;
		string name = nemesisRecord?.Name ?? (_named.TryGetValue(h, out value) ? value.Name : "a gunman");
		string temper;
		float lasting;
		int allies;
		float infamy;
		float repPart;
		float num = OpeningNerve(p, nemesisRecord, out temper, out lasting, out allies, out infamy, out repPart);
		LiveCamp liveCamp = _liveCamps.Values.FirstOrDefault((LiveCamp lc) => lc.Peds.Any((Ped x) => x != null && x.Handle == h));
		Vector3? home = null;
		if (liveCamp != null)
		{
			Vector3 vector = Kit.Centre(liveCamp.Site);
			if (p.Position.DistanceTo2D(vector) < 60f)
			{
				home = vector;
			}
		}
		if (home.HasValue)
		{
			num = Math.Min(100f, num + 15f);
		}
		bool flag = home.HasValue && liveCamp.Boss != null && nemesisRecord != null && nemesisRecord == liveCamp.Boss;
		Fighter fighter = new Fighter
		{
			H = h,
			Name = name,
			Temper = temper,
			Rec = nemesisRecord,
			Nerve = num,
			Start = num,
			Lowest = num,
			LastHealth = p.Health,
			LastSeenFighting = Game.GameTime,
			Home = home,
			CampBoss = flag,
			StartedAt = Game.GameTime
		};
		NerveLog(fighter, string.Format("in the fight - nerve {0:0} (breaks at {1}; fear {2:0}, {3} allies{4}{5})", num, BreakLine(temper), lasting, allies, (repPart > 0f) ? $", wants to prove himself +{repPart:0}" : ((repPart < (0f - infamy) / 5f - 1f) ? $", your name scares him {repPart:0}" : ""), home.HasValue ? string.Format(", defending his {0}camp +{1}", flag ? "own " : "", 15) : ""));
		if (flag && temper != "Coward" && num < 30f)
		{
			NerveLog(fighter, "his own camp - he won't back out of it");
		}
		if (num < 30f && !IsActiveMan(h) && (!flag || !(temper != "Coward")))
		{
			p.BlockPermanentEvents = true;
			if (SideStillFighting(fighter, p) <= 2 && _rng.Next(2) == 0)
			{
				fighter.State = "slipped away";
				fighter.Broke = true;
				StartRunning(h, player);
				NerveLog(fighter, "won't fight - slips away");
				Feed("NERVE " + fighter.Name + " (" + fighter.Temper.ToLowerInvariant() + ") won't face you - he runs", Theme.Warn);
			}
			else
			{
				fighter.State = "hanging back";
				fighter.StateUntil = Game.GameTime + 10000;
				Flee(h, player.Handle, 20f, 10000, 2f);
				NerveLog(fighter, "hangs back");
				Feed("NERVE " + fighter.Name + " (" + fighter.Temper.ToLowerInvariant() + ") hangs back", Theme.Warn);
			}
		}
		return fighter;
	}

	private bool IsActiveMan(int h)
	{
		if (_activeNemesisPed != null)
		{
			return _activeNemesisPed.Handle == h;
		}
		return false;
	}

	private int SideAtStart(Fighter f)
	{
		return f.SideMax;
	}

	private void GiveGround(Fighter f, Ped p, Ped player)
	{
		f.State = "falling back";
		f.StateUntil = Game.GameTime + 8000;
		f.Broke = false;
		f.FallBacks++;
		Vector3 position = p.Position;
		Vector3 vector = new Vector3(position.X - player.Position.X, position.Y - player.Position.Y, 0f);
		float num = vector.Length();
		vector = ((num < 0.1f) ? new Vector3(1f, 0f, 0f) : (vector * (1f / num)));
		Vector3 vector2 = position + vector * 12f;
		Vector3? home = f.Home;
		if (home.HasValue)
		{
			Vector3 valueOrDefault = home.GetValueOrDefault();
			if (vector2.DistanceTo2D(valueOrDefault) > 22f)
			{
				Vector3 vector3 = new Vector3(vector2.X - valueOrDefault.X, vector2.Y - valueOrDefault.Y, 0f);
				vector2 = valueOrDefault + vector3 * (22f / vector3.Length());
			}
		}
		try
		{
			PED.SET_PED_SPHERE_DEFENSIVE_AREA(f.H, vector2.X, vector2.Y, vector2.Z, 5f, p5: false, p6: false, p7: false);
			if (f.MoveWas < 0)
			{
				f.MoveWas = PED.GET_PED_COMBAT_MOVEMENT(f.H);
			}
			PED.SET_PED_COMBAT_MOVEMENT(f.H, 1);
			f.GaveGround = true;
		}
		catch (Exception ex)
		{
			GrudgeLog("NERVE give ground failed: " + ex.Message);
		}
		p.BlockPermanentEvents = StaysBlocked(p);
		TASK.TASK_COMBAT_PED(f.H, player.Handle, 0, 16);
	}

	private void EndGiveGround(Fighter f)
	{
		if (!f.GaveGround)
		{
			return;
		}
		f.GaveGround = false;
		try
		{
			if (ENTITY.DOES_ENTITY_EXIST(f.H))
			{
				PED.REMOVE_PED_DEFENSIVE_AREA(f.H, toggle: false);
				if (f.MoveWas >= 0)
				{
					PED.SET_PED_COMBAT_MOVEMENT(f.H, f.MoveWas);
				}
			}
		}
		catch
		{
		}
		f.MoveWas = -1;
	}

	private int SideStillFighting(Fighter f, Ped p)
	{
		return _fighters.Values.Count((Fighter o) => o != f && !o.Done && (o.State == "fighting" || o.State == "falling back" || o.State == "hanging back") && ENTITY.DOES_ENTITY_EXIST(o.H) && !ENTITY.IS_ENTITY_DEAD(o.H) && new Ped(o.H).Position.DistanceTo(p.Position) < 60f);
	}

	private void TauntGameRunners(Ped player)
	{
		foreach (KeyValuePair<int, int> item in _recentFoes.ToList())
		{
			int key = item.Key;
			if (Game.GameTime - item.Value > 10000 || !ENTITY.DOES_ENTITY_EXIST(key) || ENTITY.IS_ENTITY_DEAD(key))
			{
				_recentFoes.Remove(key);
			}
			else
			{
				if (_runnerRolled.Contains(key) || !PED.IS_PED_FLEEING(key))
				{
					continue;
				}
				if (!_letGoRunners.Contains(key) && _fighters.TryGetValue(key, out var value) && (value.Broke || (!value.Done && (value.State == "falling back" || value.State == "hanging back"))))
				{
					if (value.Broke)
					{
						_runnerRolled.Add(key);
					}
				}
				else
				{
					_runnerRolled.Add(key);
					Ped ped = new Ped(key);
					GrudgeLog($"SPEECH {NameOf(key)} runs from you ({ped.Position.DistanceTo(player.Position):0} m) - taunt roll");
					TauntRunner(ped, player);
				}
			}
		}
		if (_runnerRolled.Count > 300)
		{
			_runnerRolled.Clear();
			_letGoRunners.Clear();
		}
	}

	private void TauntRunner(Ped runner, Ped player)
	{
		if (Game.GameTime - _lastTauntAt < 8000 || _convo != null || player == null || player.IsDead || MISC.GET_MISSION_FLAG() || _menu.IsOpen || _tg != null || _interro != null || AUDIO.IS_ANY_SPEECH_PLAYING(player.Handle) || runner == null || !runner.Exists() || runner.Position.DistanceTo(player.Position) > 60f || _rng.Next(100) >= 70)
		{
			return;
		}
		_lastTauntAt = Game.GameTime;
		bool flag = runner.Position.DistanceTo(player.Position) < 25f;
		IEnumerable<string> enumerable2;
		if (_fighters.Values.Count((Fighter o) => (o.State == "fled" || o.State == "slipped away") && ENTITY.DOES_ENTITY_EXIST(o.H) && !ENTITY.IS_ENTITY_DEAD(o.H) && new Ped(o.H).Position.DistanceTo(player.Position) < 80f) < 2)
		{
			IEnumerable<string> enumerable = (flag ? ArthurTauntsNear : ArthurTauntsFar);
			enumerable2 = enumerable;
		}
		else
		{
			enumerable2 = new string[1] { flag ? "PLAYER_TAUNT_MULTIPLE_ENEMIES_POS_NEAR" : "PLAYER_TAUNT_MULTIPLE_ENEMIES_POS_FAR" }.Concat(flag ? ArthurTauntsNear : ArthurTauntsFar);
		}
		foreach (string item in enumerable2)
		{
			bool flag2 = _tauntsTried.Add(item);
			if (TrySay(player, item, runner))
			{
				if (!_tauntsThatPlay.Contains(item))
				{
					_tauntsThatPlay.Add(item);
				}
				GrudgeLog($"SPEECH taunt: '{item}' played for Arthur (after a runner, {runner.Position.DistanceTo(player.Position):0} m)");
				break;
			}
			if (flag2)
			{
				GrudgeLog("SPEECH taunt: '" + item + "' didn't play");
			}
		}
	}

	private void UpdateCombatTaunts(Ped player)
	{
		if (Game.GameTime < _nextCombatTaunt)
		{
			return;
		}
		_nextCombatTaunt = Game.GameTime + 6000;
		if (player == null || player.IsDead || _convo != null || _menu.IsOpen || Game.GameTime - _lastTauntAt < 8000 || MISC.GET_MISSION_FLAG() || AUDIO.IS_ANY_SPEECH_PLAYING(player.Handle))
		{
			return;
		}
		Vector3 me = player.Position;
		List<Ped> list = new List<Ped>();
		bool flag = false;
		foreach (Ped item in PedsAround(player))
		{
			if (!(item == null) && item.Exists() && !item.IsDead && item.IsHuman && !(item == player) && !(item.Position.DistanceTo(me) > 50f) && PED.IS_PED_IN_COMBAT(item.Handle, player.Handle))
			{
				list.Add(item);
				if (IsLawman(item))
				{
					flag = true;
				}
			}
		}
		if (list.Count == 0 || _rng.Next(100) >= 30)
		{
			return;
		}
		Ped ped = list.OrderBy((Ped f) => f.Position.DistanceTo(me)).First();
		bool flag2 = ped.Position.DistanceTo(me) < 25f;
		string text = ((player.Health * 2 >= player.MaxHealth) ? "POS" : "NEG");
		string text2 = (flag ? "LAW" : ((list.Count >= 3) ? "MULTIPLE_ENEMIES" : "SINGLE_ENEMY"));
		string text3 = "PLAYER_TAUNT_" + text2 + "_" + text + "_" + (flag2 ? "NEAR" : "FAR");
		if (PED.IS_PED_IN_MELEE_COMBAT(player.Handle) && ped.Position.DistanceTo(me) < 4f)
		{
			text3 = (PED.IS_PED_MALE(ped.Handle) ? "MELEE_ANTAGONIZE_MALE" : "MELEE_ANTAGONIZE_FEMALE");
		}
		_lastTauntAt = Game.GameTime;
		bool flag3 = _tauntsTried.Add(text3);
		if (TrySay(player, text3, ped) || TrySay(player, "PLAYER_TAUNT_ENEMY", ped))
		{
			if (!_tauntsThatPlay.Contains(text3))
			{
				_tauntsThatPlay.Add(text3);
			}
			GrudgeLog(string.Format("SPEECH taunt: '{0}' played for Arthur ({1} fighting you{2})", text3, list.Count, flag ? ", the law" : ""));
		}
		else if (flag3)
		{
			GrudgeLog("SPEECH taunt: '" + text3 + "' didn't play");
		}
	}

	private void ActiveSurrendered(Ped ped)
	{
		NemesisRecord activeRecord = ActiveRecord;
		if (activeRecord != null && _enc != null && !_enc.Surrendered)
		{
			_enc.Surrendered = true;
			_enc.SurrenderedAt = Game.GameTime;
			if (_activeBlip != null && _activeBlip.Exists())
			{
				_activeBlip.Delete();
			}
			_activeBlip = null;
			activeRecord.TimesSpared++;
			GrudgeNotice(activeRecord.FullName + " throws his hands up.");
			History(activeRecord, "SURRENDERED to you", Theme.Good);
			GrudgeLog("ENCOUNTER " + activeRecord.Name + " surrendered (his nerve broke)");
		}
	}

	internal string ActiveNerveLine()
	{
		if (_activeNemesisPed == null || !_fighters.TryGetValue(_activeNemesisPed.Handle, out var value))
		{
			return "nerve: not in a fight yet";
		}
		return $"nerve {value.Nerve:0}/{value.Start:0} - breaks at {BreakLine(value.Temper)}, then {SurrenderOddsFor(value.Temper)}% gives up - {value.State}";
	}

	private void NerveBy(Fighter f, float delta, string why)
	{
		if (delta < 0f)
		{
			try
			{
				if (PlayerMasked(Game.Player.Character))
				{
					delta *= 1.25f;
				}
			}
			catch
			{
			}
		}
		if (delta < 0f)
		{
			if (Game.GameTime - f.DrainFrom > 5000)
			{
				f.DrainFrom = Game.GameTime;
				f.Drained = 0f;
			}
			float num = Math.Max(0f, 30f - f.Drained);
			if (0f - delta > num)
			{
				if (why != null)
				{
					why += $" (capped {delta:0} -> {0f - num:0})";
				}
				delta = 0f - num;
			}
			f.Drained += 0f - delta;
			if (delta == 0f)
			{
				return;
			}
		}
		f.Nerve = Math.Max(0f, Math.Min(100f, f.Nerve + delta));
		f.Lowest = Math.Min(f.Lowest, f.Nerve);
		if (why != null)
		{
			f.Name = NameOf(f);
			GrudgeLog($"NERVETICK {f.Name} ({f.Temper}): {why} {delta:+0;-0} -> nerve {f.Nerve:0}");
		}
	}

	private string NameOf(Fighter f)
	{
		object obj = RecordAt(f.H)?.Name;
		if (obj == null)
		{
			if (!_named.TryGetValue(f.H, out var value))
			{
				return f.Name;
			}
			obj = value.Name;
		}
		return (string)obj;
	}

	private void NerveLog(Fighter f, string what)
	{
		f.Name = NameOf(f);
		GrudgeLog("NERVE " + f.Name + " (" + f.Temper + "): " + what);
		RepNote("NERVE " + f.Name + " (" + f.Temper.ToLowerInvariant() + "): " + what);
	}

	private void Break(Fighter f, Ped p, Ped player)
	{
		if (p.IsDead || PED.IS_PED_FATALLY_INJURED(f.H))
		{
			return;
		}
		f.Broke = true;
		int num = SurrenderOddsFor(f.Temper) + ((p.Health < p.MaxHealth / 2) ? 20 : 0);
		int num2 = _rng.Next(100);
		p.BlockPermanentEvents = true;
		int num3 = SideStillFighting(f, p);
		int num4 = SideAtStart(f);
		string text = ((f.FallBacks >= 2) ? null : ((num3 >= 3) ? $"{num3} of his side still fighting" : ((f.Home.HasValue && num3 >= 1) ? $"his camp - {num3} still with him" : ((num3 >= 1 && num3 * 2 >= num4) ? $"{num3} of {num4} still fighting" : null))));
		if (text != null)
		{
			GiveGround(f, p, player);
			NerveLog(f, $"BROKE at {f.Nerve:0} (line {BreakLine(f.Temper)}) - gives ground, still shooting ({text})");
			Feed("NERVE " + f.Name + " (" + f.Temper.ToLowerInvariant() + ") gives ground", Theme.Warn);
			return;
		}
		if (num2 < num)
		{
			f.State = "surrendered";
			f.StateUntil = Game.GameTime + 60000;
			f.SurrenderedAt = Game.GameTime;
			if (f.OrigGroup == 0)
			{
				f.OrigGroup = (int)ModGroupOf(p.Handle);
			}
			if (_surrenderGroup == 0)
			{
				_surrenderGroup = World.AddRelationshipGroup("NEMESIS_SURRENDERED");
			}
			p.RelationshipGroup = _surrenderGroup;
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _surrenderGroup, player.RelationshipGroup);
			int num5 = ((f.Temper == "Coward" && _rng.Next(100) < 55) ? 2 : _rng.Next(2));
			if (num5 == 2)
			{
				TASK.TASK_COWER(f.H, -1, player.Handle, "");
			}
			else
			{
				if (num5 == 1)
				{
					WEAPON.MAKE_PED_DROP_WEAPON(f.H, p1: true, 0, p3: true, p4: true);
				}
				TASK.TASK_HANDS_UP(f.H, -1, player.Handle, -1, p4: false);
			}
			string[] array = ((!(f.Temper == "Coward")) ? new string[4] { "Alright! Alright - I'm done.", "Enough. I'm out.", "Easy - I'm putting it down.", "You win. Don't shoot." } : new string[3] { "Don't shoot! Please!", "I give! I give!", "Mercy, mister - please!" });
			ShowSubtitle(NameOf(f) + ": " + array[_rng.Next(array.Length)]);
			NerveLog(f, num5 switch
			{
				1 => "drops his gun, hands up", 
				2 => "cowers", 
				_ => "hands up", 
			});
		}
		else if (num2 < num + 45 || f.FallBacks >= 2)
		{
			f.State = "fled";
			try
			{
				NemesisRecord nemesisRecord = RecordAt(f.H);
				if (nemesisRecord != null)
				{
					KeeperLeft(nemesisRecord, "broke and ran");
				}
			}
			catch (Exception ex)
			{
				GrudgeLog("HOLDINGS keeper left failed: " + ex.Message);
			}
			StartRunning(f.H, player);
			Say(p, "GENERIC_SHOCKED_HIGH", player);
			TauntRunner(p, player);
		}
		else
		{
			GiveGround(f, p, player);
		}
		NerveLog(f, $"BROKE at {f.Nerve:0} (line {BreakLine(f.Temper)}) - {f.State} (rolled {num2} vs surrender {num})");
		if (f.State == "surrendered" && IsActiveMan(f.H))
		{
			ActiveSurrendered(p);
		}
		Feed("NERVE " + f.Name + " (" + f.Temper.ToLowerInvariant() + ") " + ((f.State == "surrendered") ? "throws his hands up" : ((f.State == "fled") ? "breaks and runs" : "gives ground")), Theme.Warn);
	}

	private void FinishFighter(Fighter f, Ped p)
	{
		f.Done = true;
		f.DoneAt = Game.GameTime;
		EndGiveGround(f);
		if (p == null || !p.Exists() || p.IsDead)
		{
			return;
		}
		p.BlockPermanentEvents = _runners.ContainsKey(p.Handle) || StaysBlocked(p);
		f.Rec = RecordAt(f.H) ?? f.Rec;
		float num = ((f.Rec != null) ? 1f : TemperFearGain(f.Temper));
		int num2;
		int num3;
		string text;
		if (f.Broke && f.Again)
		{
			num2 = (int)Math.Round((10f + (f.Start - f.Lowest) / 2.5f) * num * 0.5f);
			num3 = 0;
			text = "broke again (" + f.State + ")";
		}
		else if (f.Broke)
		{
			num2 = (int)Math.Round((10f + (f.Start - f.Lowest) / 2.5f) * num * 1.5f);
			num3 = 0;
			text = "broke (" + f.State + ")";
		}
		else if (f.Again)
		{
			num2 = 0;
			num3 = 0;
			text = "came back into the fight and lived";
		}
		else if (PED.IS_PED_HOGTIED(p.Handle) || ENTITY.IS_ENTITY_ATTACHED(p.Handle))
		{
			num2 = 0;
			num3 = 0;
			text = "taken alive (tied up)";
		}
		else if (f.HitYou)
		{
			num2 = -10;
			num3 = 10;
			text = "fought you, hit you and lived";
		}
		else
		{
			num2 = -5;
			num3 = 5;
			text = "held his nerve and lived";
		}
		if (f.Rec != null)
		{
			int fear = f.Rec.Fear;
			int hate = f.Rec.Hate;
			if (num2 > 0)
			{
				f.Rec.AddFear(num2);
			}
			else
			{
				f.Rec.Fear = Math.Max(0, f.Rec.Fear + num2);
			}
			if (num3 > 0)
			{
				f.Rec.AddHate(num3);
			}
			if (f.Broke)
			{
				Remember(f.Rec, "Shame", 55, "broke and " + ((f.State == "surrendered") ? "gave up" : "ran") + " in a fight with you");
			}
			else if (f.HitYou)
			{
				Remember(f.Rec, "Pride", 55, "fought you and drew blood");
			}
			History(f.Rec, $"IN A FIGHT with you: {text} - fear {fear} -> {f.Rec.Fear}, hate {hate} -> {f.Rec.Hate}", f.Broke ? Theme.Good : Theme.Warn);
			SaveRoster();
		}
		else
		{
			RememberPerson(f.H, met: false);
			if (_acqOf.TryGetValue(f.H, out var value))
			{
				value.Fear = Math.Max(0, Math.Min(100, value.Fear + num2));
				if (_provoked.TryGetValue(f.H, out var value2) && value2.Record == null)
				{
					value2.Score = Math.Max(0f, Math.Min(100f, value2.Score + (float)num3));
					if (num3 != 0)
					{
						value2.Ledger.Add($"after the fight {num3:+0;-0}");
					}
				}
				value.Grudge = Math.Max(0, Math.Min(100, value.Grudge + num3));
				AddSeen(value, f.Broke ? (((f.State == "surrendered") ? "gave up" : "ran") + " in a fight with you") : "fought you and lived");
				_peopleDirty = true;
			}
		}
		NerveLog(f, $"after the fight: {text} - fear {num2:+0;-0}, hate {num3:+0;-0}");
	}

	private void NerveNearMiss(int h)
	{
		if (_fighters.TryGetValue(h, out var value) && !value.Done)
		{
			NerveBy(value, -3f, null);
		}
	}

	private void NerveAllyKilled(Ped dead, bool leader, string gore)
	{
		Ped character = Game.Player.Character;
		Fighter value;
		bool flag = _fighters.TryGetValue(dead.Handle, out value) && value.State == "surrendered";
		foreach (Fighter item in _fighters.Values.Where((Fighter x) => !x.Done && x.State == "surrendered" && x.H != dead.Handle && ENTITY.DOES_ENTITY_EXIST(x.H) && !ENTITY.IS_ENTITY_DEAD(x.H)).ToList())
		{
			Ped ped = new Ped(item.H);
			if (!(ped.Position.DistanceTo(dead.Position) > 40f) || !(ped.Position.DistanceTo(character.Position) > 40f))
			{
				if (item.Temper == "Coward")
				{
					TASK.TASK_COWER(item.H, -1, character.Handle, "");
					NerveLog(item, flag ? "you shot a man who'd given up - he freezes, cowering" : "a man went down - he freezes, cowering");
					continue;
				}
				item.State = "fled";
				item.Broke = true;
				StartRunning(item.H, character);
				Say(ped, "GENERIC_SHOCKED_HIGH", character);
				NerveLog(item, flag ? "you shot a man who'd given up - he runs for it" : "a man went down - he runs for it");
			}
		}
		foreach (Fighter item2 in _fighters.Values.Where((Fighter x) => !x.Done && x.H != dead.Handle && ENTITY.DOES_ENTITY_EXIST(x.H) && !ENTITY.IS_ENTITY_DEAD(x.H)).ToList())
		{
			if (!(new Ped(item2.H).Position.DistanceTo(dead.Position) > 30f))
			{
				float d = new Ped(item2.H).Position.DistanceTo(dead.Position);
				NerveBy(item2, (gore != null) ? (-GoreAt(d, leader ? 25 : 15)) : (leader ? (-25) : (-15)), (gore != null) ? ("saw you " + gore) : (leader ? "his leader went down" : "saw a man go down"));
			}
		}
	}

	internal IEnumerable<string> NerveDebugLines(Ped player)
	{
		List<Fighter> list = (from f in _fighters.Values
			where !f.Done && ENTITY.DOES_ENTITY_EXIST(f.H)
			orderby f.Nerve
			select f).Take(8).ToList();
		if (list.Count == 0)
		{
			yield return "nobody fighting you";
			yield break;
		}
		foreach (Fighter item in list)
		{
			yield return string.Format("{0} ({1}) nerve {2:0}/{3:0}, breaks at {4} - {5}{6}", item.Name, item.Temper.ToLowerInvariant(), item.Nerve, item.Start, BreakLine(item.Temper), item.State, item.HitYou ? ", hit you" : "");
		}
	}
}
