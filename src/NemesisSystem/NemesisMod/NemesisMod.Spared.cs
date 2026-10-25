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
	private sealed class Leaving
	{
		public NemesisRecord Who;

		public NemesisRecord Kin;

		public NemesisRecord Friend;

		public string Rel;

		public string How;

		public int Due;
	}

	private int _nextLeavingCheck;

	private bool _spawnMenuOpen;

	private int SpareLeaveChance(NemesisRecord r, out string why)
	{
		why = "";
		Gang gang = Gangs.Of(r);
		if (r == null || gang == null || gang.StoryOnly || !r.IsAlive || r.PresumedDead || r.Rank >= 5 || (r.Id ?? "").StartsWith("lore_") || (r.Id ?? "").StartsWith("fallen_"))
		{
			return 0;
		}
		if (r.Goals != null && r.Goals.Any((Goal x) => x.Type == "Avenge" && x.Target == "you"))
		{
			why = "he means to settle it with you";
			return 0;
		}
		int num;
		switch (r.Personality)
		{
		case Personality.Coward:
			num = 30;
			break;
		case Personality.Drifter:
			num = 28;
			break;
		case Personality.Greedy:
			num = 15;
			break;
		case Personality.Cold:
			num = 12;
			break;
		case Personality.Loyal:
			num = 4;
			break;
		case Personality.Hothead:
		case Personality.Proud:
			num = 3;
			break;
		default:
			num = 10;
			break;
		}
		int num2 = Math.Min(20, EffectiveFear(r) / 5);
		num += num2;
		bool flag = Mind.Has(r, "Settle");
		if (flag)
		{
			num += 15;
		}
		bool flag2 = r.Rank >= 4 || gang.OrdersNotFeelings || gang == Gangs.Wapiti;
		if (flag2)
		{
			num /= 2;
		}
		why = string.Format("{0} +fear {1}{2}{3}", r.Personality, num2, flag ? " +wants out" : "", flag2 ? " (half: officer / deserting)" : "");
		return Math.Max(0, Math.Min(60, num));
	}

	private int TagAlongTilt(NemesisRecord k)
	{
		if (k.Personality != Personality.Coward && k.Personality != Personality.Drifter)
		{
			if (k.Personality != Personality.Loyal)
			{
				return 0;
			}
			return -20;
		}
		return 15;
	}

	internal bool SparedNotSlipped(int handle, Vector3 at)
	{
		if (!_combatants.Any((KeyValuePair<int, int> kv) => kv.Key != handle && Game.GameTime - kv.Value < 10000) && !_liveCamps.Values.Any((LiveCamp c) => c.Engaged && !c.Cleared))
		{
			return Game.Player.Character.Position.DistanceTo(at) < 20f;
		}
		return false;
	}

	internal void SlippedAway(NemesisRecord r, string how)
	{
		if (r != null)
		{
			r.AddFear(10);
			History(r, "SLIPPED AWAY: " + how + " while you were busy", Theme.Dim);
			Feed("SLIPPED AWAY " + r.Name + ": " + how + " - not spared (the fight was on)", Theme.Dim, r);
			try
			{
				KeeperLeft(r, "slipped away");
			}
			catch
			{
			}
			SaveRoster();
		}
	}

	internal void SparedHim(NemesisRecord r, string how)
	{
		if (r == null || _leaving.Any((Leaving l) => l.Who == r))
		{
			return;
		}
		try
		{
			KeeperLeft(r, "was let go");
		}
		catch (Exception ex)
		{
			GrudgeLog("HOLDINGS keeper left failed: " + ex.Message);
		}
		NoteMercy(Gangs.Of(r), r.Name, r.Id);
		Ped ped = RecruitPedOf(r);
		if (ped != null)
		{
			CompanionsLeaveBe(ped.Handle);
		}
		OfferRecruit(r, how);
		string why;
		int num = SpareLeaveChance(r, out why);
		if (num <= 0)
		{
			if (why != "")
			{
				GrudgeLog("SPARED " + r.Name + " (" + how + "): stays - " + why);
			}
			return;
		}
		int num2 = _rng.Next(100);
		if (num2 >= num)
		{
			GrudgeLog($"SPARED {r.Name} ({how}): {num}% ({why}) - rolled {num2}, he stays with the {Gangs.Of(r)?.Name}");
			return;
		}
		Leaving lv = new Leaving
		{
			Who = r,
			How = how,
			Due = Game.GameTime + 180000
		};
		List<(NemesisRecord, string)> list = (from k in KinOf(r)
			where k.rel != "friend" && k.who.Gang == r.Gang && k.who.IsAlive && !k.who.PresumedDead && k.who.Rank < 5 && OffStage(k.who)
			select k).ToList();
		if (list.Count > 0)
		{
			(NemesisRecord, string) tuple = list[_rng.Next(list.Count)];
			if (_rng.Next(100) < 55 + TagAlongTilt(tuple.Item1))
			{
				lv.Kin = tuple.Item1;
				lv.Rel = tuple.Item2;
			}
		}
		List<NemesisRecord> list2 = (from f in FriendsOf(r)
			where f.Gang == r.Gang && f.IsAlive && !f.PresumedDead && f.Rank < 5 && f != lv.Kin && OffStage(f)
			select f).ToList();
		if (list2.Count > 0)
		{
			NemesisRecord nemesisRecord = list2[_rng.Next(list2.Count)];
			if (_rng.Next(100) < 25 + TagAlongTilt(nemesisRecord))
			{
				lv.Friend = nemesisRecord;
			}
		}
		_leaving.Add(lv);
		GrudgeLog(string.Format("SPARED {0} ({1}): {2}% ({3}) - rolled {4}: he's done with the {5}{6}{7}", r.Name, how, num, why, num2, Gangs.Of(r)?.Name, (lv.Kin != null) ? (", his " + lv.Rel + " " + lv.Kin.Name + " with him") : "", (lv.Friend != null) ? (", and his friend " + lv.Friend.Name) : ""));
	}

	private void UpdateLeaving(Ped player)
	{
		if (_leaving.Count == 0 || Game.GameTime < _nextLeavingCheck)
		{
			return;
		}
		_nextLeavingCheck = Game.GameTime + 2000;
		foreach (Leaving lv in _leaving.ToList())
		{
			if (_recruitOffers.Any((RecruitOffer o) => o.Rec == lv.Who || o.Rec == lv.Kin || o.Rec == lv.Friend))
			{
				continue;
			}
			int num = (from kv in _worldNemeses
				where kv.Value == lv.Who
				select kv.Key).FirstOrDefault();
			if (num == 0 || !ENTITY.DOES_ENTITY_EXIST(num) || !(ENTITY.GET_ENTITY_COORDS(num, alive: false, realCoords: false).DistanceTo(player.Position) < 200f) || Game.GameTime >= lv.Due)
			{
				_leaving.Remove(lv);
				if (Roster.Contains(lv.Who) && lv.Who.IsAlive && !lv.Who.PresumedDead)
				{
					WalkAway(lv);
				}
			}
		}
	}

	private void WalkAway(Leaving lv)
	{
		NemesisRecord who = lv.Who;
		Gang gang = Gangs.Of(who);
		long now = GameMinutesNow();
		List<NemesisRecord> men = new NemesisRecord[3] { who, lv.Kin, lv.Friend }.Where((NemesisRecord x) => x != null && Roster.Contains(x) && x.IsAlive && !x.PresumedDead).ToList();
		WorldEvent worldEvent = NewEvent("LIFE", now);
		worldEvent.Gangs.Add(who.Gang ?? "");
		bool flag = gang?.OrdersNotFeelings ?? false;
		worldEvent.Title = (flag ? (who.Name + " deserted") : (who.Name + " walked away from the " + gang?.Name));
		worldEvent.Lines.Add(flag ? "You let him live. He didn't go back to his post." : "You let him live. He didn't go back to them.");
		if (lv.Kin != null && men.Contains(lv.Kin))
		{
			worldEvent.Lines.Add("His " + Traits.Word(lv.Rel, Models.Female(lv.Kin.ModelHash)) + " " + lv.Kin.Name + " went with him.");
		}
		if (lv.Friend != null && men.Contains(lv.Friend))
		{
			worldEvent.Lines.Add("His friend " + lv.Friend.Name + " rode out with them.");
		}
		foreach (NemesisRecord item in men)
		{
			worldEvent.People.Add(new WorldPart
			{
				Id = item.Id,
				Name = item.Name,
				Gang = item.Gang,
				Outcome = "LEFT",
				Note = ((item == who) ? "you spared him" : ((item == lv.Kin) ? ("his " + lv.Rel) : "his friend"))
			});
		}
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		foreach (NemesisRecord m in men)
		{
			foreach (NemesisRecord item2 in Roster.Where((NemesisRecord x) => !men.Contains(x) && Mind.TieTo(x, m.Id) != null).ToList())
			{
				AddThought(item2, "Homesick", m.Name + " left the " + gang?.Name, -1, 48, now);
			}
		}
		foreach (NemesisRecord item3 in men)
		{
			RemoveRecord(item3, "left the outfit after you spared him");
		}
		SaveWorld();
		SaveRoster();
		string text = string.Join(" and ", from nemesisRecord in men.Skip(1)
			select (nemesisRecord != lv.Kin) ? ("his friend " + nemesisRecord.Name) : ("his " + Traits.Word(lv.Rel, Models.Female(nemesisRecord.ModelHash)) + " " + nemesisRecord.Name));
		Feed("SPARED " + who.Name + " left the " + gang?.Name + ((text != "") ? (" - " + text + " with him") : ""), Theme.Good);
		GrudgeLog("SPARED " + who.Name + " left the " + gang?.Name + ((text != "") ? (" with " + text) : ""));
		GrudgeNotice("Word is " + who.FullName + " " + (flag ? "deserted" : ("left the " + gang?.Name)) + ((text != "") ? (" - and took " + text + " with him") : "") + ".");
	}
}
