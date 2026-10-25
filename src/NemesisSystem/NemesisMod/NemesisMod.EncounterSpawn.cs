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
	private sealed class Encounter
	{
		public EncounterKind Kind;

		public int Start;

		public int NextRetask;

		public bool Engaged;

		public bool Spoke;

		public bool Spotted;

		public bool Surrendered;

		public int SurrenderedAt;

		public string SpotNote = "";

		public int StandoffUntil = -1;

		public int EngageAt;

		public int NextSpotCheck;

		public bool GreetRolled;

		public string Activity = "";

		public string Taunt;

		public EncounterCard Card;

		public bool Calmed;

		public int TalkedAt;
	}

	private const ulong ApplyShopItem = 15251352194024488921uL;

	private const ulong UpdatePedVariation = 14739335899491263887uL;

	private static readonly uint[] ArmySaddles = new uint[6] { 4182595107u, 1067410126u, 353177002u, 3943774635u, 3226461225u, 3648353217u };

	private static readonly uint[] PlainSaddles = new uint[10] { 3863513944u, 3539625148u, 413102085u, 4150799767u, 487323890u, 171561806u, 86166765u, 1531312434u, 2415718507u, 3218117906u };

	private static readonly uint[] Blankets = new uint[8] { 1351319737u, 1741336375u, 2548819561u, 647332810u, 3153089429u, 1797293285u, 2905092357u, 2035405959u };

	private static readonly uint[] Stirrups = new uint[4] { 1484641439u, 1739551490u, 1964477906u, 2185635887u };

	private static readonly uint[] Bedrolls = new uint[4] { 414935856u, 317775775u, 2552318748u, 1266550546u };

	private static readonly uint[] Saddlebags = new uint[4] { 4006051442u, 720369354u, 491707272u, 4039311985u };

	private bool _tackLogged;

	private readonly List<(Ped horse, bool army, int until)> _tackPending = new List<(Ped, bool, int)>();

	private readonly Dictionary<int, uint> _tackModel = new Dictionary<int, uint>();

	private int _tackWay = -1;

	private readonly List<(Ped horse, List<uint> items, int way, int at)> _tackChecks = new List<(Ped, List<uint>, int, int)>();

	private readonly Dictionary<int, int> _dismountOrderedAt = new Dictionary<int, int>();

	private const int TalkGraceMs = 10000;

	private void SpawnEncounter(NemesisRecord rec, EncounterKind kind, Ped player, EncounterCard card = null)
	{
		card = card ?? Cards.FirstOrDefault((EncounterCard c) => c.Enabled && c.Stage == kind.ToString().ToLowerInvariant()) ?? new EncounterCard
		{
			Id = "plain",
			Stage = kind.ToString().ToLowerInvariant()
		};
		SetWorldPaused(paused: false);
		if (_enc != null)
		{
			EndEncounter();
		}
		if (_worldNemeses.ContainsValue(rec))
		{
			GrudgeLog("ENCOUNTER skipped: " + rec.Name + " is already out in the world");
			return;
		}
		Vector3 fwd = Forward(player);
		Vector3 side = new Vector3(fwd.Y, 0f - fwd.X, 0f);
		bool flag = card.Stage == "wagon";
		float? num = null;
		if (flag && !_spawnAt.HasValue)
		{
			(Vector3, float, string)? tuple = RoadAhead(player);
			if (!tuple.HasValue)
			{
				flag = false;
				GrudgeLog("WAGON no road near you - " + rec.Name + " comes on foot instead");
			}
			else
			{
				_wagonRoad = tuple.Value.Item1;
				num = tuple.Value.Item2;
				GrudgeLog("WAGON " + tuple.Value.Item3);
			}
		}
		Vector3 vector;
		switch (kind)
		{
		case EncounterKind.Riders:
			vector = GoodSpot(player, () => Ground(player.Position - fwd * 130f + side * ((float)_rng.NextDouble() * 40f - 20f)));
			if (vector == Vector3.Zero)
			{
				GrudgeLog("ENCOUNTER riders for " + rec.Name + ": no dry ground behind you - not sent");
				LastEncounterNote = "riders: no dry ground";
				return;
			}
			break;
		case EncounterKind.Ahead:
		{
			(Vector3, float, string)? tuple3 = RoadAhead(player);
			if (!tuple3.HasValue)
			{
				GrudgeLog("ENCOUNTER ahead for " + rec.Name + ": no road near you - not sent");
				LastEncounterNote = "ahead: no road near you";
				return;
			}
			vector = tuple3.Value.Item1;
			num = tuple3.Value.Item2;
			GrudgeLog("ENCOUNTER ahead for " + rec.Name + ": " + tuple3.Value.Item3);
			break;
		}
		case EncounterKind.Sniper:
			vector = HighGround(player.Position, player);
			if (vector == Vector3.Zero)
			{
				GrudgeLog("ENCOUNTER sniper for " + rec.Name + ": no high ground out of your view - not sent");
				LastEncounterNote = "sniper: no hidden high ground";
				return;
			}
			break;
		case EncounterKind.Idle:
		{
			(Vector3, float, int)? tuple2 = ((_spawnAt.HasValue || flag || TownAt(player.Position) != null) ? (((Vector3, float, int)?)null) : FindSeat(player, 55f, 120f));
			if (tuple2.HasValue && Travelling(player, out var dir))
			{
				Vector3 vector2 = tuple2.Value.Item1 - player.Position;
				vector2.Z = 0f;
				float num2 = vector2.Length();
				if (num2 > 1f && (vector2.X * dir.X + vector2.Y * dir.Y) / num2 < 0.17f)
				{
					GrudgeLog($"ENCOUNTER idle: the seat found was behind you ({num2:0} m) - a spot ahead instead");
					tuple2 = null;
				}
			}
			vector = (_spawnAt.HasValue ? _spawnAt.Value : (flag ? _wagonRoad : (tuple2.HasValue ? StandIn(tuple2.Value.Item1, tuple2.Value.Item2) : OutOfSightSpot(player, 70f, 120f))));
			if (vector == Vector3.Zero && !_spawnAt.HasValue && !flag)
			{
				vector = OutOfSightSpot(player, 120f, 180f);
				if (vector != Vector3.Zero)
				{
					GrudgeLog($"ENCOUNTER idle for {rec.Name}: nothing hidden at 70-120 m - placed {vector.DistanceTo(player.Position):0} m out");
				}
			}
			if (vector == Vector3.Zero)
			{
				GrudgeLog("ENCOUNTER idle for " + rec.Name + ": no spot out of your view - not sent");
				LastEncounterNote = "idle: no spot out of view";
				return;
			}
			GrudgeLog(string.Format("ENCOUNTER idle (wild) spot for {0}: {1:0},{2:0} {3:0} m from you, zone {4} (you: {5}, current town {6})", rec.Name, vector.X, vector.Y, vector.DistanceTo(player.Position), TownAt(vector) ?? "wild", TownAt(player.Position) ?? "wild", _currentTown ?? "none"));
			break;
		}
		default:
		{
			string text = _currentTown ?? TownAt(player.Position);
			_calloutSeat = FindTownSeat(player, text, 60f);
			vector = (_calloutSeat.HasValue ? StandIn(_calloutSeat.Value.pos, _calloutSeat.Value.heading) : NaturalTownSpot(player, text).pos);
			if (vector == Vector3.Zero)
			{
				GrudgeLog("ENCOUNTER callout for " + rec.Name + ": no spot in " + text + " - not placed");
				LastEncounterNote = "callout: no spot in town";
				return;
			}
			GrudgeLog("ENCOUNTER callout spot for " + rec.Name + ": " + SpotNote(vector, text, player));
			break;
		}
		}
		float heading = HeadingTo(vector, player.Position);
		bool flag2 = kind == EncounterKind.Idle || kind == EncounterKind.Callout;
		Model model;
		Ped ped = SpawnNemesisPed(rec, vector, heading, NemesisGroup(), out model, 424175505u, releaseModel: false, !flag2);
		if (flag2 && ped != null)
		{
			MakeSociable(ped);
		}
		if (ped == null)
		{
			return;
		}
		NoteCame(rec);
		World.SetRelationshipBetweenGroups((kind == EncounterKind.Ahead || kind == EncounterKind.Callout || kind == EncounterKind.Idle) ? eRelationshipType.Ignore : eRelationshipType.Hate, _nemesisGroup, player.RelationshipGroup);
		if (IsArmy(rec) && kind != EncounterKind.Sniper && kind != EncounterKind.Callout && card?.Id != "plan-habit-drink")
		{
			SpawnArmyCrew(rec, vector, heading);
		}
		else if (kind == EncounterKind.Riders || kind == EncounterKind.Ahead || kind == EncounterKind.Idle)
		{
			List<int> list = card.Cast?.Crew;
			_crewOverride = ((list != null && list.Count == 2) ? new int?(list[0] + _rng.Next(Math.Max(1, list[1] - list[0] + 1))) : ((int?)null));
			SpawnFollowers(rec, model, vector, heading);
			_crewOverride = null;
		}
		STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)model.Hash);
		switch (kind)
		{
		case EncounterKind.Riders:
			MountOnHorse(ped);
			foreach (Ped item in _followers.Where((Ped f) => f.Exists()))
			{
				MountOnHorse(item);
			}
			break;
		case EncounterKind.Sniper:
			WEAPON.REMOVE_ALL_PED_WEAPONS(ped.Handle, p1: true, p2: true);
			ped.GiveWeapon((_rng.Next(2) == 0) ? eWeapon.RifleBoltAction : eWeapon.SniperRifleRollingblock, 60, eWeaponAttachPoint.Rifle, bForceInHand: true);
			ped.Accuracy = Math.Min(90, 40 + rec.Rank * 10);
			PED.SET_PED_COMBAT_MOVEMENT(ped.Handle, 0);
			PED.SET_PED_COMBAT_RANGE(ped.Handle, 2);
			break;
		case EncounterKind.Ahead:
			foreach (Ped item2 in from p in new Ped[1] { ped }.Concat(_followers)
				where p.Exists()
				select p)
			{
				TASK.TASK_START_SCENARIO_IN_PLACE_HASH(item2.Handle, MISC.GET_HASH_KEY((_rng.Next(2) == 0) ? "WORLD_HUMAN_GUARD_SCOUT" : "WORLD_HUMAN_GUARD_LAZY"), -1, playEnterAnim: true, 0u, -1f, p6: false);
			}
			break;
		}
		if (flag2)
		{
			foreach (Ped item3 in _followers.Where((Ped f) => f.Exists()))
			{
				item3.BlockPermanentEvents = false;
			}
		}
		if (flag2)
		{
			foreach (Ped item4 in _followers.Where((Ped f) => f.Exists()))
			{
				MakeSociable(item4);
			}
			HandToGame(ped);
			foreach (Ped item5 in _followers.Where((Ped f) => f.Exists()))
			{
				HandToGame(item5);
			}
		}
		if (flag2 && rec.Gang == null)
		{
			foreach (Ped item6 in from p in new Ped[1] { ped }.Concat(_followers)
				where p.Exists()
				select p)
			{
				item6.RelationshipGroup = PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(item6.Handle);
				ForgetSwitch(item6.Handle);
			}
		}
		_encVehicle = null;
		if (flag)
		{
			PutOnWagon(ped, rec, vector, num ?? HeadingTo(vector, player.Position), player);
		}
		string text2 = ((flag && _encVehicle != null) ? "driving a wagon up the road" : ((kind == EncounterKind.Idle) ? SetAtEase(ped, _followers.Where((Ped f) => f.Exists()).ToList(), vector, card.Activity == "patrol" || (card.Activity == null && IsArmy(rec)), (card.Activity != "patrol") ? card.Activity : null) : ""));
		bool presumedDead = rec.PresumedDead;
		string text3 = CardTaunt(card, rec, presumedDead);
		rec.PresumedDead = false;
		if (presumedDead && rec.Gang == null)
		{
			TryRankUp(rec, "came back from the dead");
		}
		rec.TimesAmbushed++;
		rec.Touch();
		string zoneName = GetZoneName(Game.Player.Character.Position);
		History(rec, (presumedDead && string.IsNullOrEmpty(card.History)) ? "CAME BACK from the dead to hunt you" : ((!string.IsNullOrEmpty(card.History)) ? card.History.Replace("{kind}", card.Title ?? card.Stage).Replace("{place}", zoneName) : ("CAME FOR YOU (" + (card.Title ?? kind.ToString()).ToLowerInvariant() + ") near " + zoneName)), presumedDead ? Theme.Blood : Theme.Warn);
		UseCard(card);
		SaveRoster();
		_activeNemesisPed = ped;
		ActiveRecord = rec;
		_enc = new Encounter
		{
			Kind = kind,
			Start = Game.GameTime,
			Taunt = text3,
			Activity = text2,
			Card = card
		};
		if (kind == EncounterKind.Idle)
		{
			GrudgeLog($"ENCOUNTER idle: {rec.Name} + {_followers.Count} men {text2}, {vector.DistanceTo(player.Position):0} m off");
		}
		Feed(string.Format("ENCOUNTER {0} [{1}]: {2}{3}{4}, {5:0} m off", kind.ToString().ToUpperInvariant(), card.Id, rec.FullName, (_followers.Count > 0) ? $" + {_followers.Count} men" : "", (text2 != "") ? (", " + text2) : "", vector.DistanceTo(player.Position)), Theme.Warn, rec);
		string text4 = rec.FullName ?? "";
		string text5 = ((_followers.Count > 0) ? $" and {_followers.Count} men" : "");
		if (_encForced && _activeBlip == null && ped.Exists())
		{
			Crumbs.Native("Encounters blip add (V2)");
			_activeBlip = ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
			if (_activeBlip != null)
			{
				Function.Call(11290983183885202434uL, _activeBlip.Handle, new InputArgument(UiDraw.ForeverVarString(rec.FullName ?? "")));
			}
		}
		switch (kind)
		{
		case EncounterKind.Riders:
			EngageEncounter(player, blip: true);
			ShowSubtitle(rec.Name + ": \"" + text3 + "\"");
			GrudgeNotice("Riders coming up behind you: " + text4 + text5 + ".");
			break;
		case EncounterKind.Sniper:
			EngageEncounter(player, blip: false);
			break;
		case EncounterKind.Callout:
			if (_calloutSeat.HasValue)
			{
				SitAt(ped, _calloutSeat.Value.point, _calloutSeat.Value.pos, _calloutSeat.Value.heading);
			}
			else
			{
				PlaceNaturally(ped, vector);
			}
			Crumbs.Native("Encounters blip add (V2)");
			if (_activeBlip == null)
			{
				_activeBlip = ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
			}
			if (_activeBlip != null)
			{
				Function.Call(11290983183885202434uL, _activeBlip.Handle, new InputArgument(UiDraw.ForeverVarString(rec.FullName + " (looking for you)")));
			}
			break;
		case EncounterKind.Ahead:
		case EncounterKind.Holdup:
		case EncounterKind.Idle:
			break;
		}
	}

	private string SetAtEase(Ped lead, List<Ped> crew, Vector3 at, bool patrol = false, string scenario = null)
	{
		if (!string.IsNullOrEmpty(scenario) && !patrol)
		{
			SettleInWorld(lead, 30f, scenario, -1f);
			foreach (Ped item in crew)
			{
				SettleInWorld(item, 10f, scenario, HeadingTo(item.Position, lead.Position));
			}
			return scenario.Replace("WORLD_HUMAN_", "").Replace('_', ' ').ToLowerInvariant();
		}
		if (patrol || _rng.Next(100) < 50)
		{
			TASK.TASK_WANDER_STANDARD(lead.Handle, 10f, 10);
			foreach (Ped item2 in crew)
			{
				TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(item2.Handle, lead.Handle, (float)(_rng.NextDouble() * 4.0 - 2.0), -2f - (float)_rng.NextDouble() * 3f, 0f, 1f, -1, 2f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
			}
			if (!patrol)
			{
				return "on the move";
			}
			return "on patrol";
		}
		if (CampAroundFire(lead, crew))
		{
			return "round a fire";
		}
		(string, string, string[]) tuple = Activities[_rng.Next(Activities.Length)];
		SettleInWorld(lead, 30f, tuple.Item2, -1f);
		foreach (Ped item3 in crew)
		{
			SettleInWorld(item3, 10f, tuple.Item3[_rng.Next(tuple.Item3.Length)], HeadingTo(item3.Position, lead.Position));
		}
		return tuple.Item1;
	}

	private void TackUp(Ped horse, bool army = false)
	{
		if (!(horse == null) && horse.Exists())
		{
			_tackModel[horse.Handle] = (uint)horse.Model.Hash;
			if (!Function.Call<bool>(11582290322814659388uL, new InputArgument[1] { horse.Handle }))
			{
				_tackPending.Add((horse, army, Game.GameTime + 10000));
			}
			else
			{
				TackNow(horse, army);
			}
		}
	}

	private bool TackStillHim(Ped horse)
	{
		if (horse != null && horse.Exists() && !horse.IsDead && _tackModel.TryGetValue(horse.Handle, out var value) && value == (uint)horse.Model.Hash)
		{
			return BelongsToUs(horse.Handle);
		}
		return false;
	}

	private void TackNow(Ped horse, bool army)
	{
		try
		{
			List<uint> list = new List<uint>
			{
				Pick(army ? ArmySaddles : PlainSaddles),
				Pick(Blankets),
				Pick(Stirrups)
			};
			if (_rng.Next(100) < 60)
			{
				list.Add(Pick(Bedrolls));
				list.Add(Pick(Saddlebags));
			}
			ApplyTack(horse, list, (_tackWay >= 0) ? _tackWay : 0);
			_tackChecks.Add((horse, list, (_tackWay >= 0) ? _tackWay : 0, Game.GameTime + 700));
		}
		catch (Exception ex)
		{
			if (!_tackLogged)
			{
				_tackLogged = true;
				GrudgeLog("HORSE tack failed: " + ex.Message);
			}
		}
		uint Pick(uint[] a)
		{
			return a[_rng.Next(a.Length)];
		}
	}

	private static void ApplyTack(Ped horse, List<uint> items, int way)
	{
		int handle = horse.Handle;
		if (way == 4)
		{
			int num = PED.GET_NUM_META_PED_OUTFITS(handle);
			Crumbs.Native("Encounters outfit");
			if (num > 1)
			{
				PED._EQUIP_META_PED_OUTFIT_PRESET(handle, 1 + new Random(handle).Next(num - 1), p2: false);
			}
		}
		else
		{
			foreach (uint item in items)
			{
				switch (way)
				{
				case 1:
					PED._APPLY_SHOP_ITEM_TO_PED(handle, item, immediately: true, isMp: true, p4: true);
					break;
				case 2:
					PED._APPLY_SHOP_ITEM_TO_PED(handle, item, immediately: false, isMp: false, p4: true);
					break;
				default:
					PED._APPLY_SHOP_ITEM_TO_PED(handle, item, immediately: true, isMp: false, p4: false);
					break;
				}
			}
		}
		if (way == 3)
		{
			PED._REFRESH_META_PED_SHOP_ITEMS(handle, 0);
		}
		Crumbs.Native("Encounters ped variation");
		PED._UPDATE_PED_VARIATION(handle, p1: false, p2: true, p3: true, p4: true, p5: false);
	}

	private static bool HasSaddle(Ped horse)
	{
		if (!ArmySaddles.Concat(PlainSaddles).Any((uint s) => WearsComponent(horse.Handle, s)))
		{
			return WearsComponent(horse.Handle, 3131565592u);
		}
		return true;
	}

	internal static bool WearsComponent(int ped, uint component)
	{
		return Function.Call<bool>(18106882544748776897uL, new InputArgument[2] { ped, component });
	}

	private void UpdateTack()
	{
		for (int num = _tackPending.Count - 1; num >= 0; num--)
		{
			var (ped, army, num2) = _tackPending[num];
			if (!TackStillHim(ped))
			{
				_tackPending.RemoveAt(num);
				if (ped != null)
				{
					_tackModel.Remove(ped.Handle);
				}
			}
			else if (Function.Call<bool>(11582290322814659388uL, new InputArgument[1] { ped.Handle }))
			{
				_tackPending.RemoveAt(num);
				TackNow(ped, army);
			}
			else if (Game.GameTime > num2)
			{
				_tackPending.RemoveAt(num);
				GrudgeLog($"HORSE {ped.Handle} never got ready to render - no tack");
			}
		}
		for (int num3 = _tackChecks.Count - 1; num3 >= 0; num3--)
		{
			var (ped2, list, num4, num5) = _tackChecks[num3];
			if (Game.GameTime >= num5)
			{
				_tackChecks.RemoveAt(num3);
				if (TackStillHim(ped2))
				{
					if (HasSaddle(ped2))
					{
						if (_tackWay != num4)
						{
							GrudgeLog($"HORSE saddle took with way {num4} - used for every horse from now on");
						}
						_tackWay = num4;
					}
					else if (num4 >= 4)
					{
						if (!_tackLogged)
						{
							_tackLogged = true;
							GrudgeLog("HORSE no way put a saddle on (ways 0-4 all failed the check)");
						}
					}
					else
					{
						GrudgeLog($"HORSE saddle check: way {num4} didn't take - trying way {num4 + 1}");
						ApplyTack(ped2, list, num4 + 1);
						_tackChecks.Add((ped2, list, num4 + 1, Game.GameTime + 700));
					}
				}
			}
		}
	}

	private Ped MountOnHorse(Ped rider)
	{
		Vector3 pos = rider.Position + new Vector3(1.5f, 0f, 0f);
		Ped ped = SpawnPed(RiderHorses[_rng.Next(RiderHorses.Length)], pos, rider.Heading);
		if (ped == null)
		{
			return null;
		}
		MakeOurs(ped.Handle);
		TackUp(ped, Gangs.ForModel((uint)rider.Model.Hash) == Gangs.Army);
		Crumbs.Native("Encounters onto mount");
		PED.SET_PED_ONTO_MOUNT(rider.Handle, ped.Handle, -1, p3: true);
		_encHorses.Add(ped);
		return ped;
	}

	private bool EncounterAttacked(Ped ped, Ped player, float d)
	{
		if (!(from p in new Ped[1] { ped }.Concat(_followers)
			where p != null && p.Exists() && !p.IsDead
			select p).Any((Ped p) => PED.IS_PED_IN_COMBAT(p.Handle, player.Handle) || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true)))
		{
			if (FiredInAnger && d < 90f)
			{
				return WEAPON.IS_PED_ARMED(player.Handle, 4);
			}
			return false;
		}
		return true;
	}

	private void EngageEncounter(Ped player, bool blip)
	{
		if (_activeNemesisPed == null || !_activeNemesisPed.Exists())
		{
			GrudgeLog("ENCOUNTER engage skipped - he's gone");
			return;
		}
		_enc.Engaged = true;
		if (ActiveRecord?.Needs != null)
		{
			ActiveRecord.Needs["Violence"] = 0;
		}
		if (_currentTown != null && InTownForArrivals(player, _currentTown) && ActiveRecord != null)
		{
			try
			{
				TownFightReaction(_activeNemesisPed, player, _currentTown, Loadout.BandOf(ActiveRecord) == Loadout.Band.Gun);
			}
			catch (Exception ex)
			{
				GrudgeLog("FIGHT town reaction failed: " + ex.Message);
			}
		}
		ClaimBack(_activeNemesisPed);
		foreach (Ped follower in _followers)
		{
			ClaimBack(follower);
		}
		if (_activeNemesisPed != null && _activeNemesisPed.Exists())
		{
			_activeNemesisPed.BlockPermanentEvents = true;
			_activeNemesisPed.RelationshipGroup = NemesisGroup();
		}
		foreach (Ped item in _followers.Where((Ped f) => f.Exists()))
		{
			item.BlockPermanentEvents = true;
			item.RelationshipGroup = NemesisGroup();
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _nemesisGroup, player.RelationshipGroup);
		Ped activeNemesisPed = _activeNemesisPed;
		if (blip && _activeBlip != null && _activeBlip.Exists())
		{
			_activeBlip.Delete();
			_activeBlip = null;
		}
		Crumbs.Native("Encounters blip add (V2)");
		if (blip && _activeBlip == null)
		{
			_activeBlip = activeNemesisPed.AddBlip(BlipType.BLIP_STYLE_ENEMY);
		}
		TASK.TASK_COMBAT_PED(activeNemesisPed.Handle, player.Handle, 0, 16);
		PED.SET_PED_KEEP_TASK(activeNemesisPed.Handle, toggle: true);
		foreach (Ped item2 in _followers.Where((Ped f) => f.Exists() && !f.IsDead))
		{
			Crumbs.Native("Encounters blip add (V2)");
			if (blip)
			{
				_followerBlips.Add(item2.AddBlip(BlipType.BLIP_STYLE_ENEMY));
			}
			TASK.TASK_COMBAT_PED(item2.Handle, player.Handle, 0, 16);
			PED.SET_PED_KEEP_TASK(item2.Handle, toggle: true);
		}
	}

	private bool FightsHandToHand(Ped p)
	{
		if (p == _activeNemesisPed && ActiveRecord != null)
		{
			return Loadout.BandOf(ActiveRecord) != Loadout.Band.Gun;
		}
		if (_worldNemeses.TryGetValue(p.Handle, out var value))
		{
			return Loadout.BandOf(value) != Loadout.Band.Gun;
		}
		uint num = WEAPON.GET_WEAPONTYPE_GROUP(WEAPON._GET_PED_CURRENT_HELD_WEAPON(p.Handle));
		if (num != 3566412244u)
		{
			return num == 2685387236u;
		}
		return true;
	}

	private void GetOffForAFight(Ped player)
	{
		if (_enc == null || !_enc.Engaged)
		{
			return;
		}
		List<Ped> list = new List<Ped>();
		if (_activeNemesisPed != null)
		{
			list.Add(_activeNemesisPed);
		}
		list.AddRange(_followers);
		foreach (Ped item in list)
		{
			if (item == null || !item.Exists() || item.IsDead || NerveOut(item.Handle))
			{
				continue;
			}
			bool num = PED.IS_PED_ON_MOUNT(item.Handle);
			_dismountOrderedAt.TryGetValue(item.Handle, out var value);
			if (num)
			{
				if (!(item.Position.DistanceTo(player.Position) > 30f) && FightsHandToHand(item) && (value == 0 || Game.GameTime - value >= 4000))
				{
					TASK.TASK_DISMOUNT_ANIMAL(item.Handle, 0, 0uL, 0uL, 0uL, 0);
					_dismountOrderedAt[item.Handle] = Game.GameTime;
					GrudgeLog($"ENCOUNTER {item.Handle} gets off his horse to fight on foot");
				}
			}
			else if (value != 0)
			{
				_dismountOrderedAt.Remove(item.Handle);
				TASK.TASK_COMBAT_PED(item.Handle, player.Handle, 0, 16);
				PED.SET_PED_KEEP_TASK(item.Handle, toggle: true);
			}
		}
	}
}
