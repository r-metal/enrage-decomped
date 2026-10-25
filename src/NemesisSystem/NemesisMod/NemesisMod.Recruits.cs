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
	private sealed class RecruitOffer
	{
		public NemesisRecord Rec;

		public string How;

		public int Until;

		public int Ped;

		public uint Model;

		public uint Group;

		public bool Held;

		public bool Spoke;

		public bool Shown;

		public bool Tied;

		public int Started;

		public int NextOrder;
	}

	private sealed class RecruitCall
	{
		public NemesisRecord Rec;

		public int Tries;
	}

	private static readonly string[] OfferLines = new string[4] { "Wait... I'm done with them. Let me ride with you.", "Hold on, Morgan. I ain't going back - you could use a gun like mine.", "You let me live. I'd as soon ride with you as with them.", "I'd ride with you, if you'll have me." };

	private static readonly string[] RecruitHomeLines = new string[3] { "I'll be around when you need me.", "Send word when you need a gun.", "I'll keep my head down till you call." };

	private readonly List<RecruitOffer> _recruitOffers = new List<RecruitOffer>();

	private readonly List<RecruitCall> _recruitCalls = new List<RecruitCall>();

	private int _pRecruit;

	private int _pRecruitFor;

	private int _pRecruitGroup;

	private bool _recruitArmed;

	private RecruitOffer _recruitAt;

	private int _nextRecruitScan;

	private int _nextRecruitCall;

	private static bool IsRecruit(Companion c)
	{
		if (c != null && c.Key != null)
		{
			return c.Key.StartsWith("rec:");
		}
		return false;
	}

	internal IReadOnlyList<NemesisRecord> RecruitsKept()
	{
		return Chronicle.Recruits?.ToList() ?? new List<NemesisRecord>();
	}

	internal string RecruitStatus(NemesisRecord rec)
	{
		if (rec == null)
		{
			return "Waiting";
		}
		string key = "rec:" + rec.Id;
		if (_comps.Any((Companion c) => c.Key == key && c.State != 2))
		{
			return "Riding";
		}
		long num = GameMinutesNow();
		if (CompHurt.TryGetValue(key, out var value) && value > num)
		{
			return $"Laid up ({LaidUpHours(value, num)}h)";
		}
		return "Waiting";
	}

	private static long LaidUpHours(long until, long now)
	{
		return Math.Max(1L, (until - now + 59) / 60);
	}

	internal void OfferRecruit(NemesisRecord r, string how)
	{
		try
		{
			if (r == null || !Roster.Contains(r) || !r.IsAlive || r.PresumedDead || _recruitOffers.Any((RecruitOffer o) => o.Rec == r))
			{
				return;
			}
			Gang gang = Gangs.Of(r);
			string text = r.Id ?? "";
			bool flag = text.StartsWith("lore_") || text.StartsWith("fallen_") || text.StartsWith("vdl_") || (gang?.StoryOnly ?? false);
			bool flag2 = r.Goals != null && r.Goals.Any((Goal g) => g.Type == "Avenge" && g.Target == "you");
			bool flag3 = gang?.OrdersNotFeelings ?? false;
			int num = EffectiveFear(r);
			int num2 = Chronicle.Recruits?.Count ?? 0;
			string text2 = ((!Config.RecruitsEnabled) ? "recruits are off ([Recruits] Enabled)" : ((num2 >= Config.RecruitsMaxKept) ? $"you keep {num2} men already ([Recruits] MaxKept {Config.RecruitsMaxKept})" : null));
			int num3 = ((text2 == null) ? RecruitRules.OfferChance(r.Personality, r.Hate, num, r.Rank, flag3, flag2, flag, Config.RecruitsChancePercent) : 0);
			if (num3 <= 0)
			{
				GrudgeLog("RECRUIT " + r.Name + " (" + how + "): no offer - " + (text2 ?? (flag ? "a story man" : (flag2 ? "he means to settle it with you" : ((r.Rank >= 5) ? "he leads them" : ((Config.RecruitsChancePercent <= 0) ? "[Recruits] ChancePercent is 0" : "the odds come to nothing"))))));
				return;
			}
			string text3 = string.Format("{0} +fear {1} -hate {2}{3}{4}", r.Personality?.ToString() ?? "no temper", Math.Min(20, Math.Max(0, Math.Min(100, num)) / 5), Math.Max(0, Math.Min(100, r.Hate)) / 4, (r.Rank >= 4 || flag3) ? " (half: officer / law)" : "", (Config.RecruitsChancePercent != 100) ? $" x{Config.RecruitsChancePercent}%" : "");
			int num4 = _rng.Next(100);
			if (num4 >= num3)
			{
				GrudgeLog($"RECRUIT {r.Name} ({how}): {num3}% ({text3}) - rolled {num4}, no offer");
				return;
			}
			Ped ped = RecruitPedOf(r);
			if (ped == null)
			{
				GrudgeLog($"RECRUIT {r.Name} ({how}): {num3}% ({text3}) - rolled {num4}: he'd ride with you, but his ped is gone");
				return;
			}
			_recruitOffers.Add(new RecruitOffer
			{
				Rec = r,
				How = how,
				Until = Game.GameTime + 60000,
				Started = Game.GameTime,
				Ped = ped.Handle,
				Model = ENTITY.GET_ENTITY_MODEL(ped.Handle)
			});
			GrudgeLog($"RECRUIT {r.Name} ({how}): {num3}% ({text3}) - rolled {num4}: offers to ride with you (60 s, ped {ped.Handle}, {ped.Position.DistanceTo(Game.Player.Character.Position):0} m)");
		}
		catch (Exception ex)
		{
			GrudgeLog("RECRUIT offer failed: " + ex.Message);
		}
	}

	private Ped RecruitPedOf(NemesisRecord rec)
	{
		int num = (from kv in _worldNemeses
			where kv.Value == rec && ENTITY.DOES_ENTITY_EXIST(kv.Key)
			select kv.Key).FirstOrDefault();
		if (num != 0)
		{
			return new Ped(num);
		}
		if (rec == ActiveRecord && _activeNemesisPed != null && _activeNemesisPed.Exists())
		{
			return _activeNemesisPed;
		}
		TownMan townMan = _townMen.FirstOrDefault((TownMan t) => t.Rec == rec && t.Ped != null && t.Ped.Exists());
		if (townMan != null)
		{
			return townMan.Ped;
		}
		if (_interro?.Rec == rec && _interro.Ped != null && _interro.Ped.Exists())
		{
			return _interro.Ped;
		}
		return null;
	}

	private Ped OfferPed(RecruitOffer o)
	{
		Ped ped = RecruitPedOf(o.Rec);
		if (ped == null && o.Ped != 0 && ENTITY.DOES_ENTITY_EXIST(o.Ped) && ENTITY.GET_ENTITY_MODEL(o.Ped) == o.Model)
		{
			ped = new Ped(o.Ped);
		}
		if (ped != null && ped.Handle != o.Ped)
		{
			o.Ped = ped.Handle;
			o.Model = ENTITY.GET_ENTITY_MODEL(ped.Handle);
		}
		return ped;
	}

	private bool OfferFree(Ped ped)
	{
		int handle = ped.Handle;
		if (!ped.IsHogtied && !ENTITY.IS_ENTITY_ATTACHED(handle) && !ped.IsRagdoll && !UnderArrest(handle) && !_sparedBound.ContainsKey(handle))
		{
			if (_interro?.Ped != null)
			{
				return _interro.Ped.Handle != handle;
			}
			return true;
		}
		return false;
	}

	private void UpdateRecruits(Ped player)
	{
		UpdateRecruitCalls(player);
		if (_recruitOffers.Count == 0)
		{
			HideRecruitPrompt();
			return;
		}
		if (!Config.RecruitsEnabled)
		{
			foreach (RecruitOffer item in _recruitOffers.ToList())
			{
				EndOffer(item, player, "recruits were turned off", OfferPed(item));
			}
			HideRecruitPrompt();
			return;
		}
		if (Game.GameTime >= _nextRecruitScan)
		{
			_nextRecruitScan = Game.GameTime + 400;
			_recruitAt = null;
			float num = 3f;
			foreach (RecruitOffer item2 in _recruitOffers.ToList())
			{
				Ped ped = OfferPed(item2);
				bool flag = ped != null && ped.Exists() && !ped.IsDead && (ped.IsHogtied || _sparedBound.ContainsKey(ped.Handle));
				if (flag && Game.GameTime - item2.Started < 600000)
				{
					item2.Until = Math.Max(item2.Until, Game.GameTime + 60000);
				}
				string text = ((!Roster.Contains(item2.Rec) || !item2.Rec.IsAlive || item2.Rec.PresumedDead) ? "he's no longer on the roster" : ((ped == null || !ped.Exists() || ped.IsDead) ? "he's gone or down" : ((Game.GameTime >= item2.Until) ? "expired - you never took him up on it" : ((ped.Position.DistanceTo(player.Position) > 150f) ? "you rode on" : ((item2.Held && PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle)) ? "he went for you" : null)))));
				if (text != null)
				{
					EndOffer(item2, player, text, ped);
					continue;
				}
				if (flag)
				{
					if (!item2.Tied)
					{
						item2.Tied = true;
						GrudgeLog("RECRUIT " + item2.Rec.Name + ": still tied - the offer waits till you cut him free");
					}
					continue;
				}
				HoldForOffer(item2, ped, player);
				float num2 = ped.Position.DistanceTo(player.Position);
				if (num2 < num && OfferFree(ped))
				{
					num = num2;
					_recruitAt = item2;
				}
			}
		}
		RecruitOffer recruitAt = _recruitAt;
		Ped ped2 = ((recruitAt != null && _recruitOffers.Contains(recruitAt)) ? OfferPed(recruitAt) : null);
		if (ped2 == null || !ped2.Exists() || ped2.Position.DistanceTo(player.Position) > 3.5f || MISC.GET_MISSION_FLAG() || _menu.IsOpen || _convo != null || _tg != null || player.IsInCombat || _pAsk != 0)
		{
			HideRecruitPrompt();
			return;
		}
		int num3 = HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(ped2.Handle);
		if (_pRecruit == 0 || !PromptValid(_pRecruit) || _pRecruitFor != ped2.Handle || _pRecruitGroup != num3)
		{
			Crumbs.Native("Recruit prompt delete");
			if (PromptValid(_pRecruit))
			{
				HUD._UI_PROMPT_DELETE(_pRecruit);
			}
			_pRecruit = MakePrompt(eInputType.InteractOption1, "Ride With Me", hold: true, num3);
			_pRecruitFor = ped2.Handle;
			_pRecruitGroup = num3;
			if (!recruitAt.Shown)
			{
				recruitAt.Shown = true;
				GrudgeLog($"RECRUIT {recruitAt.Rec.Name}: 'Ride With Me' shown ({(recruitAt.Until - Game.GameTime) / 1000} s left)");
			}
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pRecruit, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pRecruit, toggle: true);
		_dpadOursAt = Game.GameTime;
		if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(_pRecruit))
		{
			_recruitArmed = true;
		}
		else if (_recruitArmed)
		{
			_recruitArmed = false;
			HideRecruitPrompt();
			Recruit(recruitAt, ped2, player);
		}
	}

	private void HoldForOffer(RecruitOffer o, Ped ped, Ped player)
	{
		float num = ped.Position.DistanceTo(player.Position);
		if (!OfferFree(ped) || num > 80f)
		{
			return;
		}
		if (!o.Held)
		{
			o.Held = true;
			o.Group = (uint)PED.GET_PED_RELATIONSHIP_GROUP_HASH(ped.Handle);
			StandAndWait(o, ped, player);
			GrudgeLog($"RECRUIT {o.Rec.Name}: stops and waits on you ({num:0} m) - hold 'Ride With Me' by him to take him on");
		}
		else if (Game.GameTime >= o.NextOrder && (PED.IS_PED_FLEEING(ped.Handle) || TASK.IS_PED_RUNNING(ped.Handle) || _walkOffs.ContainsKey(ped.Handle) || _runners.ContainsKey(ped.Handle)))
		{
			StandAndWait(o, ped, player);
		}
		if (!o.Spoke && num < 40f)
		{
			o.Spoke = true;
			ShowSubtitle(o.Rec.Name.Split(' ')[0] + ": \"" + OfferLines[_rng.Next(OfferLines.Length)] + "\"");
			SayAny(ped, "agree2", AgreeVoice, player);
		}
	}

	private void StandAndWait(RecruitOffer o, Ped ped, Ped player)
	{
		int handle = ped.Handle;
		o.NextOrder = Game.GameTime + 2500;
		_runners.Remove(handle);
		_walkOffs.Remove(handle);
		_letGoRunners.Remove(handle);
		CompanionsLeaveBe(handle);
		if (_surrenderGroup == 0)
		{
			_surrenderGroup = World.AddRelationshipGroup("NEMESIS_SURRENDERED");
		}
		ped.RelationshipGroup = _surrenderGroup;
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _surrenderGroup, player.RelationshipGroup);
		ped.BlockPermanentEvents = true;
		TASK.CLEAR_PED_TASKS(handle, p1: true, p2: false);
		TASK.TASK_TURN_PED_TO_FACE_ENTITY(handle, player.Handle, -1, 0f, 0f, 0f);
	}

	private void EndOffer(RecruitOffer o, Ped player, string why, Ped ped)
	{
		_recruitOffers.Remove(o);
		if (_recruitAt == o)
		{
			_recruitAt = null;
			HideRecruitPrompt();
		}
		GrudgeLog("RECRUIT " + o.Rec.Name + ": offer over - " + why);
		if (o.Held && ped != null && ped.Exists() && !ped.IsDead && !IsCompanion(ped.Handle) && OfferFree(ped) && !PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle))
		{
			StartRunning(ped.Handle, player, o.Group);
		}
	}

	private unsafe void Recruit(RecruitOffer o, Ped ped, Ped player)
	{
		NemesisRecord rec = o.Rec;
		int kept = Chronicle.Recruits?.Count ?? 0;
		if (!Config.RecruitsEnabled || kept >= Config.RecruitsMaxKept)
		{
			EndOffer(o, player, (!Config.RecruitsEnabled) ? "recruits are off ([Recruits] Enabled)" : $"you keep {kept} men already ([Recruits] MaxKept {Config.RecruitsMaxKept})", ped);
			return;
		}
		_recruitOffers.Remove(o);
		_recruitAt = null;
		if (!Roster.Contains(rec) || ped == null || !ped.Exists() || ped.IsDead)
		{
			GrudgeLog("RECRUIT " + rec.Name + ": refused - he's gone or off the roster");
			return;
		}
		int handle = ped.Handle;
		if (rec == ActiveRecord)
		{
			if (!_encEnd.HasValue)
			{
				_encEnd = ("SURRENDERED", "gave up - rides with you now");
			}
			EndEncounter();
			ReleaseActive();
			GrudgeLog("RECRUIT " + rec.Name + ": he was the man you were up against - the encounter is over");
		}
		LeaveOldScenes(handle);
		TakeOn(rec, o.How);
		int num = _comps.Count((Companion c) => c.State != 2);
		if (!Config.CompanionsEnabled || num >= 3)
		{
			ShowSubtitle(rec.Name.Split(' ')[0] + ": \"I'll be around when you need me.\"");
			TASK.CLEAR_PED_TASKS(handle, p1: true, p2: false);
			if (_surrenderGroup == 0)
			{
				_surrenderGroup = World.AddRelationshipGroup("NEMESIS_SURRENDERED");
			}
			ped.RelationshipGroup = _surrenderGroup;
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _surrenderGroup, player.RelationshipGroup);
			ped.BlockPermanentEvents = false;
			TASK.TASK_WANDER_STANDARD(handle, 10f, 10);
			int handle2 = handle;
			ReleaseIfOurs(&handle2);
			GrudgeLog("RECRUIT " + rec.Name + ": " + ((!Config.CompanionsEnabled) ? "riding together is off" : $"{num} already ride with you") + " - he goes his way till you send for him");
			return;
		}
		MakeOurs(handle);
		TASK.CLEAR_PED_TASKS(handle, p1: true, p2: false);
		ped.BlockPermanentEvents = false;
		Crumbs.Native("Companion prompt name");
		Function.Call(5352729231319872684uL, ped.Handle, UiDraw.ForeverString(rec.Name));
		Companion companion = new Companion
		{
			Key = "rec:" + rec.Id,
			Name = rec.Name,
			Rec = rec,
			NoCamp = true,
			Ped = ped,
			IsReal = false,
			Slot = _comps.Count
		};
		_comps.Add(companion);
		Setup(companion, ped, player);
		GrudgeLog($"RECRUIT {rec.Name}: rides with you ({num + 1} riding)");
	}

	private void TakeOn(NemesisRecord rec, string how)
	{
		Gang gang = Gangs.Of(rec);
		_leaving.RemoveAll((Leaving l) => l.Who == rec);
		long now = GameMinutesNow();
		List<NemesisRecord> list = Roster.Where((NemesisRecord x) => x != rec && Mind.TieTo(x, rec.Id) != null).ToList();
		bool female = Models.Female(rec.ModelHash);
		WorldEvent worldEvent = NewEvent("LIFE", now);
		worldEvent.Gangs.Add(rec.Gang ?? "");
		worldEvent.Title = rec.Name + " turned - rides with Arthur Morgan now";
		worldEvent.Lines.Add(Pron.Her((gang != null) ? ("You let him live. He left the " + gang.Name + " to ride with you.") : "You let him live. He rides with you now.", female));
		worldEvent.People.Add(new WorldPart
		{
			Id = rec.Id,
			Name = rec.Name,
			Gang = rec.Gang,
			Outcome = "LEFT",
			Note = "rides with you now"
		});
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		foreach (NemesisRecord item2 in list)
		{
			AddThought(item2, "Bitter", rec.Name + " went over to Arthur Morgan", -2, 48, now);
		}
		RemoveRecord(rec, "turned - rides with you");
		(Chronicle.Recruits ?? (Chronicle.Recruits = new List<NemesisRecord>())).Add(rec);
		SaveWorld();
		SaveRoster();
		Feed("SPARED " + rec.Name + ((gang != null) ? (" turned on the " + gang.Name) : " turned") + " - he rides with you now", Theme.Good, rec);
		GrudgeLog(string.Format("RECRUIT {0} ({1}): you took him on - off the roster, {2} kept now; {3} of his people think less of him", rec.Name, how, Chronicle.Recruits.Count, list.Count));
		GrudgeNotice(Pron.Her("Word is " + rec.FullName + ((gang != null) ? (" turned on the " + gang.Name) : " turned") + " - he rides with you now.", female));
	}

	internal bool CanRecruitNow(NemesisRecord r)
	{
		if (r == null || !Roster.Contains(r) || r.Rank >= 5 || !OffStage(r) || _interro?.Rec == r)
		{
			return false;
		}
		string text = r.Id ?? "";
		if (!text.StartsWith("lore_") && !text.StartsWith("fallen_") && !text.StartsWith("vdl_"))
		{
			return !(Gangs.Of(r)?.StoryOnly ?? false);
		}
		return false;
	}

	internal void RecruitNow(NemesisRecord r)
	{
		if (!CanRecruitNow(r))
		{
			GrudgeLog("RECRUIT debug Recruit Now refused for " + (r?.Name ?? "nobody") + " - off the roster, out in the world, or a story man");
			return;
		}
		int num = Chronicle.Recruits?.Count ?? 0;
		if (num >= Config.RecruitsMaxKept)
		{
			ShowNotification($"You keep {num} men already ([Recruits] MaxKept {Config.RecruitsMaxKept}).");
			GrudgeLog($"RECRUIT debug Recruit Now refused for {r.Name} - {num} kept already");
			return;
		}
		TakeOn(r, "debug: Recruit Now");
		ShowNotification(r.Name + " rides for you now. Send for him from Your Men.");
	}

	private void LeaveOldScenes(int h)
	{
		Predicate<Ped> match = (Ped p) => p != null && p.Handle == h;
		_followers.RemoveAll(match);
		_survivors.RemoveAll(match);
		_aftermathPeds.RemoveAll(match);
		_hunters.RemoveAll(match);
		foreach (LiveCamp value in _liveCamps.Values)
		{
			value.Peds.RemoveAll(match);
		}
		foreach (Raid raid in _raids)
		{
			raid.Men.RemoveAll(match);
		}
		foreach (Band band in _bands)
		{
			band.Men.RemoveAll(match);
		}
		_rival?.Men.RemoveAll(match);
		_runners.Remove(h);
		_provoked.Remove(h);
		_followerOf.Remove(h);
		foreach ((string, ICollection<int>, Action<int>) item in BookkeepingMaps().ToList())
		{
			if (item.Item2.Contains(h))
			{
				item.Item3(h);
			}
		}
	}

	private bool SpawnRecruit(Companion c, Ped player)
	{
		if (c.Rec == null)
		{
			return false;
		}
		Vector3 vector = OutOfSightSpot(player, 35f, 60f, ahead: false, allowSeen: true);
		if (vector == Vector3.Zero)
		{
			return false;
		}
		Ped ped = SpawnNemesisPed(c.Rec, vector, player.Heading, 0u, out var _, 424175505u, releaseModel: true, blockEvents: false, factionStyle: false);
		if (ped == null)
		{
			return false;
		}
		Crumbs.Native("Companion prompt name");
		Function.Call(5352729231319872684uL, ped.Handle, UiDraw.ForeverString(c.Name));
		c.Ped = ped;
		c.IsReal = false;
		if (c.Horse != null && c.Horse.Exists() && !c.Horse.IsDead)
		{
			c.Horse.Position = vector + new Vector3(2.5f, 0f, 0f);
		}
		Setup(c, ped, player);
		Crumbs.Native("Companion onto mount");
		if (player.IsOnMount && c.Horse != null && c.Horse.Exists())
		{
			PED.SET_PED_ONTO_MOUNT(ped.Handle, c.Horse.Handle, -1, p3: true);
		}
		GrudgeLog(string.Format("RECRUIT {0} joined you at {1:0},{2:0} ({3}, {4:0} m back)", c.Name, vector.X, vector.Y, player.IsOnMount ? "mounted" : "on foot", vector.DistanceTo(player.Position)));
		return true;
	}

	private string RecruitRefusal(NemesisRecord rec, bool sending)
	{
		string key = "rec:" + rec.Id;
		long num = GameMinutesNow();
		if (Chronicle.Recruits == null || !Chronicle.Recruits.Contains(rec))
		{
			return rec.Name + " doesn't ride for you any more.";
		}
		if (!Config.CompanionsEnabled)
		{
			return "Riding together is off (NemesisMod.ini [Companions] Enabled).";
		}
		if (_comps.Any((Companion c) => c.Key == key && c.State != 2))
		{
			return rec.Name + " is already riding with you.";
		}
		if (CompHurt.TryGetValue(key, out var value) && value > num)
		{
			return rec.Name.Split(' ')[0] + " sends word: \"Still healing up, Arthur. Give me " + LaidUpHours(value, num) + " more hours.\"";
		}
		if (_comps.Any((Companion c) => c.Key == key))
		{
			return rec.Name + " is still riding off - give him a moment.";
		}
		if (sending && _recruitCalls.Any((RecruitCall x) => x.Rec == rec))
		{
			return rec.Name + " is already on his way.";
		}
		if (_comps.Count((Companion c) => c.State != 2) + (sending ? _recruitCalls.Count : 0) >= 3)
		{
			return "Three already ride with you. Send one home first.";
		}
		return null;
	}

	internal void SendForRecruit(NemesisRecord rec)
	{
		if (rec == null)
		{
			return;
		}
		string text = RecruitRefusal(rec, sending: true);
		if (text != null)
		{
			ShowNotification(Pron.Her(text, Models.Female(rec.ModelHash)));
			GrudgeLog("RECRUIT sent for " + rec.Name + ": refused - " + text);
			return;
		}
		_recruitCalls.Add(new RecruitCall
		{
			Rec = rec
		});
		ShowNotification(Pron.Her("Word's out to " + rec.Name + " - he'll catch up with you.", Models.Female(rec.ModelHash)));
		GrudgeLog("RECRUIT sent for " + rec.Name);
	}

	private void UpdateRecruitCalls(Ped player)
	{
		if (_recruitCalls.Count == 0 || _menu.IsOpen || Game.GameTime < _nextRecruitCall || WorldBusy(player))
		{
			return;
		}
		_nextRecruitCall = Game.GameTime + 1000;
		bool flag = false;
		try
		{
			flag = MISC.GET_MISSION_FLAG();
		}
		catch
		{
		}
		if (flag)
		{
			return;
		}
		RecruitCall recruitCall = _recruitCalls[0];
		NemesisRecord rec = recruitCall.Rec;
		string text = RecruitRefusal(rec, sending: false);
		if (text != null)
		{
			_recruitCalls.Remove(recruitCall);
			ShowNotification(Pron.Her(text, Models.Female(rec.ModelHash)));
			GrudgeLog("RECRUIT sent for " + rec.Name + ": refused on arrival - " + text);
			return;
		}
		Companion companion = new Companion
		{
			Key = "rec:" + rec.Id,
			Name = rec.Name,
			Rec = rec,
			NoCamp = true,
			Slot = _comps.Count
		};
		if (SpawnRecruit(companion, player))
		{
			_recruitCalls.Remove(recruitCall);
			_comps.Add(companion);
		}
		else if (++recruitCall.Tries >= 20)
		{
			_recruitCalls.Remove(recruitCall);
			ShowNotification(Pron.Her(rec.Name + " couldn't find his way to you. Try again in the open.", Models.Female(rec.ModelHash)));
			GrudgeLog("RECRUIT sent for " + rec.Name + ": refused - no spot out of sight to bring him in (20 tries)");
		}
	}

	internal void SendRecruitHome(NemesisRecord rec)
	{
		if (rec == null)
		{
			return;
		}
		string key = "rec:" + rec.Id;
		if (_recruitCalls.RemoveAll((RecruitCall x) => x.Rec == rec) > 0)
		{
			GrudgeLog("RECRUIT " + rec.Name + ": sent home before he reached you");
			return;
		}
		Companion companion = _comps.FirstOrDefault((Companion c) => c.Key == key && c.State != 2);
		if (companion == null)
		{
			ShowNotification(Pron.Her(rec.Name + " isn't riding with you.", Models.Female(rec.ModelHash)));
			GrudgeLog("RECRUIT " + rec.Name + ": sent home - refused, he isn't riding with you");
			return;
		}
		GrudgeLog("RECRUIT " + rec.Name + ": sent home");
		SendHome(companion, "you sent him home");
	}

	internal void LetRecruitGo(NemesisRecord rec)
	{
		if (rec == null || Chronicle.Recruits == null || !Chronicle.Recruits.Contains(rec))
		{
			return;
		}
		string key = "rec:" + rec.Id;
		_recruitCalls.RemoveAll((RecruitCall x) => x.Rec == rec);
		foreach (Companion item in _comps.Where((Companion c) => c.Key == key && c.State != 2).ToList())
		{
			SendHome(item, "you let him go");
		}
		Chronicle.Recruits.Remove(rec);
		CompHurt.Remove(key);
		bool female = Models.Female(rec.ModelHash);
		WorldEvent worldEvent = NewEvent("LIFE", GameMinutesNow());
		worldEvent.Gangs.Add("");
		worldEvent.Title = Pron.Her(rec.Name + " went his own way", female);
		worldEvent.Lines.Add(Pron.Her("You let him go. He rides alone now.", female));
		worldEvent.People.Add(new WorldPart
		{
			Id = rec.Id,
			Name = rec.Name,
			Gang = rec.Gang,
			Outcome = "LEFT",
			Note = "you let him go"
		});
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		SaveWorld();
		Feed("COMPANION " + rec.Name + " went his own way - you let him go", Theme.Dim, rec);
		GrudgeLog($"RECRUIT {rec.Name}: let go for good ({Chronicle.Recruits.Count} kept now)");
	}

	private void HideRecruitPrompt()
	{
		if (_pRecruit != 0)
		{
			Crumbs.Native("Recruit prompt delete");
			if (PromptValid(_pRecruit))
			{
				HUD._UI_PROMPT_DELETE(_pRecruit);
			}
			_pRecruit = 0;
			_pRecruitFor = 0;
		}
	}

	private void EndRecruitOffers(string why)
	{
		Ped character = Game.Player.Character;
		foreach (RecruitOffer item in _recruitOffers.ToList())
		{
			EndOffer(item, character, why, OfferPed(item));
		}
		HideRecruitPrompt();
	}

	private void WipeRecruitOffers()
	{
		_recruitOffers.Clear();
		_recruitCalls.Clear();
		_recruitAt = null;
		_recruitArmed = false;
		HideRecruitPrompt();
	}
}
