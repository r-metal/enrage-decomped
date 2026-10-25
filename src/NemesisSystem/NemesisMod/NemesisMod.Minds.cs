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
	private int _nextTieSync;

	internal int MindSnaps;

	internal int MindQuits;

	internal string LastMindNote = "";

	private readonly Dictionary<string, long> _snappedAt = new Dictionary<string, long>();

	private HashSet<string> _deadNames;

	private int _deadNamesAt = -100000;

	private void RememberMaskedKill(Ped dead, Ped player)
	{
		if (dead == null || !dead.Exists() || !dead.IsHuman || Kids.IsChild(dead) || RecordAt(dead.Handle) != null || MISC.GET_MISSION_FLAG() || IsStoryPed(dead) || GameOwned(dead) || IsCompanion(dead.Handle) || !PlayerMasked(player))
		{
			return;
		}
		uint hash = (uint)dead.Model.Hash;
		Vector3 position = dead.Position;
		string zoneName = GetZoneName(position);
		NemesisRecord nemesisRecord = NewRecord(hash, zoneName);
		if (!_named.TryGetValue(dead.Handle, out var value))
		{
			try
			{
				value = NameByLook(dead);
			}
			catch
			{
				value = null;
			}
		}
		if (value != null && !string.IsNullOrEmpty(value.Name))
		{
			nemesisRecord.Name = value.Name;
			nemesisRecord.Title = value.Title ?? "";
		}
		if (_provoked.TryGetValue(dead.Handle, out var value2) && value2.Record == null)
		{
			nemesisRecord.Personality = value2.Temper;
			if (value2.Shadow != null)
			{
				CopyTraits(value2.Shadow, nemesisRecord);
			}
		}
		nemesisRecord.Origin = ((nemesisRecord.Gang != null) ? GrudgeOrigin.GangMember : GrudgeOrigin.Gunfight);
		nemesisRecord.HasOrigin = true;
		nemesisRecord.OriginX = position.X;
		nemesisRecord.OriginY = position.Y;
		nemesisRecord.OriginZ = position.Z;
		nemesisRecord.KinId = null;
		nemesisRecord.KinRelation = null;
		nemesisRecord.Hate = 0;
		nemesisRecord.IsAlive = false;
		nemesisRecord.PresumedDead = false;
		nemesisRecord.KilledByPlayer = true;
		nemesisRecord.TimesKilledPlayer = 0;
		nemesisRecord.Touch();
		try
		{
			KeepOutfit(dead, nemesisRecord);
		}
		catch
		{
		}
		History(nemesisRecord, "KILLED BY YOU near " + zoneName + " - you wore a mask; he never saw your face", Theme.Blood);
		Roster.Add(nemesisRecord);
		_bulletImpact.TagVictim(dead.Handle, nemesisRecord.Id, nemesisRecord.FullName);
		ForgetPerson(dead.Handle, nemesisRecord.Name);
		_named.Remove(dead.Handle);
		WorldPart worldPart = new WorldPart
		{
			Id = nemesisRecord.Id,
			Name = nemesisRecord.Name,
			Gang = nemesisRecord.Gang,
			Outcome = "KILLED",
			Note = "you were masked"
		};
		ChronicleDeed("kill", "You killed " + nemesisRecord.Name, "near " + zoneName, position, nemesisRecord.Gang, worldPart);
		SaveRoster();
		GrudgeLog("MASKED KILL " + nemesisRecord.FullName + " (" + ((nemesisRecord.Gang == null) ? "unaffiliated" : Gangs.ById(nemesisRecord.Gang)?.Name) + ") remembered - near " + zoneName);
		DeathNotice(nemesisRecord.FullName + " is dead.");
	}

	private void EnsureMind(NemesisRecord r)
	{
		if (r == null)
		{
			return;
		}
		if (!r.MindRolled)
		{
			try
			{
				Mind.Roll(r, (r.Id != null) ? Seeds.For(r, "mind") : _rng, GameMinutesNow());
			}
			catch (Exception ex)
			{
				GrudgeLog("MIND roll failed for " + r.Name + ": " + ex.Message);
			}
		}
		if (!r.QuirksRolled && r.MindRolled)
		{
			Quirks.Roll(r, (r.Id != null) ? Seeds.For(r, "quirks") : _rng);
			if (r.Quirks.Count > 0)
			{
				GrudgeLog("QUIRKS " + r.Name + ": " + string.Join(", ", r.Quirks));
			}
		}
	}

	internal void CheckSkills(NemesisRecord r, string band)
	{
		if (r == null || !r.IsAlive)
		{
			return;
		}
		bool num = !r.SkillsV2;
		int num2 = r.Skills?.Count ?? 0;
		List<string> list = Quirks.EarnSkills(r, band);
		if (num)
		{
			if (num2 > 0 || (r.Skills?.Count ?? 0) > 0)
			{
				GrudgeLog(string.Format("SKILL {0}: rules v2 - now {1} (rank {2}, had {3})", r.Name, string.Join(", ", r.Skills), r.Rank, num2));
			}
			SaveRoster();
			return;
		}
		foreach (string item in list)
		{
			History(r, "LEARNED: " + Quirks.SkillText(item), Theme.Gold);
			Feed("SKILL " + r.Name + ": " + Quirks.SkillText(item), Theme.Gold);
			GrudgeLog("SKILL " + r.Name + ": " + item);
			SaveRoster();
		}
	}

	internal void Remember(NemesisRecord r, string emotion, int intensity, string text, string cause = "you")
	{
		if (r == null)
		{
			return;
		}
		EnsureMind(r);
		Dictionary<string, int> before = ((r.Facets != null) ? new Dictionary<string, int>(r.Facets) : null);
		_ = r.Goals?.Count;
		string text2 = string.Join(",", r.Goals?.Select((Goal g) => g.Type) ?? new string[0]);
		if (!Mind.Remember(r, emotion, intensity, text, cause, GameMinutesNow(), _rng))
		{
			return;
		}
		int value;
		List<string> list = ((before == null) ? new List<string>() : (from kv in r.Facets
			where before.TryGetValue(kv.Key, out value) && value != kv.Value
			select $"{kv.Key.ToLowerInvariant()} {before[kv.Key]} -> {kv.Value}").ToList());
		object obj;
		if (string.Join(",", r.Goals?.Select((Goal g) => g.Type) ?? new string[0]) != text2)
		{
			List<Goal> goals = r.Goals;
			if (goals != null && goals.Count > 0)
			{
				obj = "; now wants: " + r.Goals[0].Text;
				goto IL_0163;
			}
		}
		obj = "";
		goto IL_0163;
		IL_0163:
		string text3 = (string)obj;
		Feed(string.Format("MIND {0} will never forget: {1} ({2} {3}){4}{5}", r.Name, text, Mind.Felt(emotion), intensity, (list.Count > 0) ? (" - " + string.Join(", ", list)) : "", text3), Mind.EmotionColor(emotion), r);
		GrudgeLog(string.Format("MIND {0} long-term {1} {2}: {3} | {4}{5}", r.Name, emotion, intensity, text, string.Join(", ", list), text3));
	}

	private static string EmotionOfMood(string mood)
	{
		switch (mood)
		{
		case "Grieving":
		case "Homesick":
			return "Grief";
		case "Shaken":
		case "Sick":
		case "Hurting":
			return "Fear";
		case "Bitter":
			return "Anger";
		case "Jailed":
		case "Humiliated":
			return "Shame";
		case "Proud":
			return "Pride";
		case "Joyful":
		case "Steady":
		case "Flush":
			return "Joy";
		default:
			return null;
		}
	}

	private void UpdateMinds()
	{
		long num = GameMinutesNow();
		if (num <= 0)
		{
			return;
		}
		long num2 = num / 60;
		long num3 = num / 1440;
		if (_lastMindHour < 0)
		{
			_lastMindHour = num2;
			_lastMindDay = num3;
			return;
		}
		if (num2 <= _lastMindHour)
		{
			if (num2 < _lastMindHour - 2)
			{
				_lastMindHour = num2;
				_lastMindDay = num3;
			}
			return;
		}
		int num4 = (int)Math.Min(48L, num2 - _lastMindHour);
		_lastMindHour = num2;
		List<NemesisRecord> list = Roster.Where((NemesisRecord r) => r.IsAlive && !r.PresumedDead).ToList();
		foreach (NemesisRecord item in list)
		{
			EnsureMind(item);
			CheckSkills(item, null);
		}
		for (int num5 = 0; num5 < num4; num5++)
		{
			foreach (NemesisRecord item2 in list)
			{
				if (Roster.Contains(item2))
				{
					HourOf(item2, num - (long)(num4 - 1 - num5) * 60L);
				}
			}
		}
		if (num3 > _lastMindDay)
		{
			int num6 = (int)Math.Min(7L, num3 - _lastMindDay);
			_lastMindDay = num3;
			for (int num7 = 0; num7 < num6; num7++)
			{
				foreach (NemesisRecord item3 in list)
				{
					if (Roster.Contains(item3) && item3.IsAlive)
					{
						DayOf(item3, num);
					}
				}
			}
		}
		SaveRoster();
	}

	private void HourOf(NemesisRecord r, long now)
	{
		if (r.Needs == null)
		{
			return;
		}
		List<PastEntry> past = r.Past;
		bool flag = (past != null && past.Any((PastEntry e) => e.Text != null && e.Text.Contains("drinking"))) || (Mind.F(r, "Anger") > 65 && Mind.F(r, "Piety") < 30);
		Rise("Drink", (!flag) ? 1 : 3);
		Rise("Company", (!r.TraitsRolled || r.Sociability != Sociability.Outgoing) ? 1 : 2);
		DateTime dateTime = GameDate.FromMinutes(now);
		int hour = dateTime.Hour;
		float num = Mind.F(r, "Piety");
		float num2 = Mind.F(r, "Greed");
		if ((num >= 65f && hour % 6 == 0) || (num >= 45f && num < 65f && hour % 12 == 0))
		{
			Rise("Faith", 1);
		}
		if (num2 >= 60f || hour % 3 == 0)
		{
			Rise("Money", 1);
		}
		if (dateTime.DayOfWeek == DayOfWeek.Sunday && hour == 10 && num >= 45f)
		{
			MeetN("Faith", 60);
		}
		if (r.Gang != null && dateTime.DayOfWeek == DayOfWeek.Saturday && hour == 20)
		{
			MeetN("Money", 50);
		}
		if (r.Gang == null && hour == 18 && (uint)Seeds.Stable($"work/{r.Id}/{now / 1440}") % 3u == 0)
		{
			MeetN("Money", 35);
		}
		Rise("Violence", (Mind.F(r, "Cruelty") >= 60 || Mind.F(r, "Anger") >= 70) ? 2 : 0);
		if (r.Gang != null && (r.CampId != null || r.BossId != null))
		{
			Rise("Company", -3);
			if (_rng.Next(3) == 0)
			{
				Rise("Drink", -4);
			}
		}
		int num3 = Mind.NeedNames.Count((string n) => Mind.N(r, n) >= 70);
		int num4 = r.Memories?.Where((Memory m) => !m.LongTerm && Mind.Bad.Contains(m.Emotion)).Sum((Memory m) => m.Intensity) ?? 0;
		int num5 = num3 + ((num4 >= 150) ? 1 : 0) - 2;
		r.Stress = Math.Max(0, Math.Min(100, r.Stress + num5));
		if (r.Stress >= 85 && _rng.Next(100) < 2 && OffStage(r) && (!_snappedAt.TryGetValue(r.Id ?? "", out var value) || value > now || now - value >= 4320))
		{
			_snappedAt[r.Id ?? ""] = now;
			Snap(r, now);
		}
		void MeetN(string n, int d)
		{
			r.Needs[n] = Math.Max(0, Mind.N(r, n) - d);
		}
		void Rise(string n, int d)
		{
			r.Needs[n] = Math.Max(0, Math.Min(100, Mind.N(r, n) + d));
		}
	}

	private void DayOf(NemesisRecord r, long now)
	{
		Mind.Fade(r);
		if (r.Goals != null)
		{
			Gang gang = Gangs.Of(r);
			r.Goals.RemoveAll(delegate(Goal g)
			{
				if (g.Type == "Protect")
				{
					NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord x) => x.Id == g.Target);
					if (nemesisRecord == null)
					{
						return true;
					}
					return !nemesisRecord.IsAlive;
				}
				return false;
			});
			if (gang != null && r.Goals.Any((Goal g) => g.Type == "Rise") && r.Rank >= gang.Tiers[0].MinRank)
			{
				r.Goals.RemoveAll((Goal g) => g.Type == "Rise");
			}
		}
		if (Mind.Has(r, "Settle") && (EffectiveFear(r) >= 40 || r.Stress >= 60) && r.TimesKilledPlayer == 0 && OffStage(r) && _rng.Next(100) < 4 && !LoreProtected(r) && (r.Gang == null || SuccessorId(Gangs.ById(r.Gang)) != r.Id))
		{
			QuitTheLife(r, now);
		}
	}

	private void Snap(NemesisRecord r, long now)
	{
		MindSnaps++;
		Gang gang = Gangs.Of(r);
		NemesisRecord nemesisRecord = ((gang != null) ? (from _ in Roster
			where _ != r && _.Gang == r.Gang && _.IsAlive && !_.PresumedDead && OffStage(_)
			orderby _rng.Next()
			select _).FirstOrDefault() : null);
		string text = ((nemesisRecord != null) ? (r.Name + " snapped and went for " + nemesisRecord.Name) : (r.Name + " snapped - a bottle through a window, a fight with a stranger"));
		WorldEvent worldEvent = NewEvent("LIFE", now);
		worldEvent.Gangs.Add(r.Gang ?? "");
		worldEvent.Title = text;
		worldEvent.Place = ((gang != null) ? ("(" + gang.Name + ")") : "(rides alone)");
		worldEvent.Lines.Add($"Stress {r.Stress} - he'd had enough.");
		WorldPart worldPart = new WorldPart
		{
			Id = r.Id,
			Name = r.Name,
			Gang = r.Gang,
			Outcome = "SNAPPED",
			Note = "at the end of his rope"
		};
		worldEvent.People.Add(worldPart);
		if (nemesisRecord != null)
		{
			worldEvent.People.Add(new WorldPart
			{
				Id = nemesisRecord.Id,
				Name = nemesisRecord.Name,
				Gang = nemesisRecord.Gang,
				Outcome = "HIT",
				Note = "on the wrong end of it"
			});
			Mind.SetTie(r, nemesisRecord.Id, "enemy", Math.Min(Mind.TieTo(r, nemesisRecord.Id)?.Strength ?? 0, 0) - 40, "the night he snapped");
			Mind.SetTie(nemesisRecord, r.Id, "enemy", Math.Min(Mind.TieTo(nemesisRecord, r.Id)?.Strength ?? 0, 0) - 50, "the night he snapped");
			AddThought(nemesisRecord, "Bitter", r.Name + " went for him for no reason", -2, 72, now);
		}
		r.Stress = Math.Max(0, r.Stress - 40);
		if (r.Needs != null)
		{
			r.Needs["Violence"] = 0;
		}
		AddThought(r, "Humiliated", "lost control in front of everyone", -2, 48, now, worldPart);
		History(r, (nemesisRecord != null) ? ("SNAPPED and went for " + nemesisRecord.Name) : "SNAPPED - lost control in front of everyone", Theme.Warn);
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		SaveWorld();
		LastMindNote = text;
		GrudgeLog($"MIND {text} (stress -> {r.Stress})");
	}

	private void QuitTheLife(NemesisRecord r, long now)
	{
		MindQuits++;
		string text = (new string[6] { "a farm outside Strawberry", "his brother's place back East", "a homestead near Emerald Ranch", "the railroad in New Austin", "his wife's people in Lemoyne", "a mining claim in the Grizzlies" })[_rng.Next(6)];
		WorldEvent worldEvent = NewEvent("LIFE", now);
		worldEvent.Gangs.Add(r.Gang ?? "");
		worldEvent.Title = r.Name + " quit the life";
		worldEvent.Place = "for " + text;
		worldEvent.Lines.Add((r.Gang != null) ? ("He walked away from the " + Gangs.Of(r)?.Name + ". Nobody stopped him.") : "He sold his horse and left the territory.");
		worldEvent.People.Add(new WorldPart
		{
			Id = r.Id,
			Name = r.Name,
			Gang = r.Gang,
			Outcome = "LEFT",
			Note = $"fear {EffectiveFear(r)}, stress {r.Stress}"
		});
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		foreach (NemesisRecord item in Roster.Where((NemesisRecord x) => x != r && Mind.TieTo(x, r.Id) != null))
		{
			AddThought(item, "Homesick", r.Name + " quit the life", -1, 48, now);
		}
		RemoveRecord(r, "quit the life");
		SaveWorld();
		LastMindNote = r.Name + " quit the life";
		Feed($"MIND {r.Name} quit the life (fear {EffectiveFear(r)}, stress {r.Stress}) - gone {text}", Theme.Dim);
		GrudgeLog("MIND " + r.Name + " quit the life - " + text);
		GrudgeNotice("Word is " + r.FullName + " quit the life. Gone to " + text + ".");
	}

	private static int KinStrength(string rel)
	{
		switch (rel)
		{
		default:
			return 40;
		case "cousin":
			return 50;
		case "uncle":
		case "nephew":
			return 60;
		case "brother":
		case "son":
		case "father":
			return 80;
		}
	}

	private void SyncTies()
	{
		if (Game.GameTime < _nextTieSync)
		{
			return;
		}
		_nextTieSync = Game.GameTime + 15000;
		foreach (NemesisRecord r in Roster)
		{
			if (!r.MindRolled)
			{
				continue;
			}
			r.Ties = r.Ties ?? new List<Tie>();
			if (!r.GoalsSet)
			{
				Mind.InitGoals(r, Seeds.For(r, "goals"), GameMinutesNow());
			}
			if (r.KinId != null && Roster.Any((NemesisRecord k) => k.Id == r.KinId))
			{
				if (Mind.TieTo(r, r.KinId) == null)
				{
					Mind.SetTie(r, r.KinId, "kin", KinStrength(r.KinRelation), Traits.Inverse(r.KinRelation));
				}
				NemesisRecord nemesisRecord = Roster.First((NemesisRecord x) => x.Id == r.KinId);
				if (nemesisRecord.MindRolled && Mind.TieTo(nemesisRecord, r.Id) == null)
				{
					Mind.SetTie(nemesisRecord, r.Id, "kin", KinStrength(r.KinRelation), r.KinRelation);
				}
			}
			foreach (string item in r.FriendIds ?? new List<string>())
			{
				if (Mind.TieTo(r, item) == null)
				{
					Mind.SetTie(r, item, "friend", 50 + _rng.Next(25));
				}
			}
			foreach (Tie item2 in r.Ties.Where((Tie t) => t.Type == "friend" && !(r.FriendIds ?? new List<string>()).Contains(t.Id)).ToList())
			{
				item2.Type = "enemy";
				item2.Strength = Math.Min(item2.Strength, -20);
			}
			NemesisRecord nemesisRecord2 = ((r.BossId != null) ? Roster.FirstOrDefault((NemesisRecord b) => b.Id == r.BossId) : null);
			if (nemesisRecord2 != null && nemesisRecord2.MindRolled && Mind.TieTo(r, nemesisRecord2.Id) == null)
			{
				Mind.SetTie(r, nemesisRecord2.Id, "mentor", 20 + Mind.F(r, "Loyalty") / 3, "his boss");
				if (Mind.TieTo(nemesisRecord2, r.Id) == null)
				{
					Mind.SetTie(nemesisRecord2, r.Id, "protege", 15 + Mind.F(nemesisRecord2, "Loyalty") / 4, "one of his men");
				}
			}
			r.Ties.RemoveAll((Tie t) => !Roster.Any((NemesisRecord x) => x.Id == t.Id));
		}
	}

	private void MeetNeeds(NemesisRecord r, string text)
	{
		if (r?.Needs != null && text != null)
		{
			string text2 = text.ToLowerInvariant();
			if (text2.Contains("drink") || text2.Contains("whiskey") || text2.Contains("saloon") || text2.Contains("bottle") || text2.Contains("drunk"))
			{
				Meet("Drink", 45);
			}
			if (text2.Contains("church") || text2.Contains("pray") || text2.Contains("god") || text2.Contains("preacher") || text2.Contains("hymn"))
			{
				Meet("Faith", 50);
			}
			if (text2.Contains("pay") || text2.Contains("won") || text2.Contains("score") || text2.Contains("stole") || text2.Contains("money") || text2.Contains("cards"))
			{
				Meet("Money", 40);
			}
			if (text2.Contains("fight") || text2.Contains("brawl") || text2.Contains("duel") || text2.Contains("knife") || text2.Contains("blows") || text2.Contains("shot"))
			{
				Meet("Violence", 50);
			}
			if (text2.Contains("friend") || text2.Contains("wedding") || text2.Contains("sang") || text2.Contains("letter") || text2.Contains("visit") || text2.Contains("together"))
			{
				Meet("Company", 40);
			}
		}
		void Meet(string n, int d)
		{
			r.Needs[n] = Math.Max(0, Mind.N(r, n) - d);
		}
	}

	internal string MindDebugLine()
	{
		List<NemesisRecord> list = Roster.Where((NemesisRecord r) => r.IsAlive && r.MindRolled).ToList();
		if (list.Count == 0)
		{
			return "minds: -";
		}
		IEnumerable<string> values = from g in list.SelectMany((NemesisRecord r) => r.Goals ?? new List<Goal>())
			group g by g.Type into g
			orderby g.Count() descending
			select $"{g.Key.ToLowerInvariant()} {g.Count()}";
		return $"minds: {list.Count} - stress avg {list.Average((NemesisRecord r) => r.Stress):0}, {list.Count((NemesisRecord r) => r.Stress >= 70)} on edge, {list.Count((NemesisRecord r) => Mind.NeedNames.Any((string n) => Mind.N(r, n) >= 70))} with unmet needs" + string.Format("  -  wants: {0}  -  {1} lasting memories, {2} snapped, {3} quit", string.Join(", ", values), list.Sum((NemesisRecord r) => r.Memories?.Count((Memory m) => m.LongTerm) ?? 0), MindSnaps, MindQuits) + ((LastMindNote != "") ? ("  -  last: " + LastMindNote) : "");
	}

	internal string MindShort(NemesisRecord r)
	{
		if (r == null || !r.MindRolled)
		{
			return "";
		}
		Goal goal = r.Goals?.FirstOrDefault();
		Memory memory = Mind.Strongest(r);
		return string.Format("stress {0}{1}{2}", r.Stress, (goal != null) ? (", wants to " + goal.Type.ToLowerInvariant() + ((goal.Target == "you") ? " (you)" : "")) : "", (memory != null) ? (", " + Mind.Felt(memory.Emotion) + ": " + memory.Text) : "");
	}
}
