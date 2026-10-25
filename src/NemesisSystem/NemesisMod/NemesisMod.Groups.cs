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
	private readonly Dictionary<int, (int at, List<int> mates)> _groupCache = new Dictionary<int, (int, List<int>)>();

	private readonly HashSet<int> _groupLinked = new HashSet<int>();

	internal int GroupsLinked;

	internal int GroupIntros;

	private readonly Dictionary<int, Dictionary<int, int>> _togetherAt = new Dictionary<int, Dictionary<int, int>>();

	private int _nextGroupWatch;

	private int _groupWatchNext;

	private int _nextTogetherPrune;

	internal const int FuryToPromote = 3;

	private const int PromptTypeGreet = 3;

	internal List<Ped> GroupOf(Ped target, bool allowDead = false)
	{
		List<Ped> list = new List<Ped>();
		if (target == null || !target.Exists() || (target.IsDead && !allowDead) || !target.IsHuman)
		{
			return list;
		}
		bool flag = !target.IsDead && PED.IS_PED_USING_ANY_SCENARIO(target.Handle);
		Ped character = Game.Player.Character;
		Vector3 tp = target.Position;
		Vector3 vector = ENTITY.GET_ENTITY_VELOCITY(target.Handle, 0);
		bool flag2 = IsLawman(target);
		int num = (PED.IS_PED_IN_GROUP(target.Handle) ? PED.GET_PED_GROUP_INDEX(target.Handle) : (-1));
		_ = target.Heading;
		List<(Ped p, Vector3 pos)> list2 = PedSnapshot(character);
		bool flag3 = list2.Count(((Ped p, Vector3 pos) s) => s.p != null && s.p.Handle != target.Handle && (s.pos - tp).Length() < 6f) >= 5;
		_togetherAt.TryGetValue(target.Handle, out var value);
		foreach (var (ped, vector2) in list2)
		{
			if (ped == null || ped.Handle == target.Handle || (vector2 - tp).Length() > 6f || !ped.Exists() || ped.IsPlayer || !ped.IsHuman || ped.IsDead)
			{
				continue;
			}
			float num2 = ped.Position.DistanceTo(tp);
			if (num2 > 5f || Kids.IsChild(ped) || IsStoryPed(ped) || IsPlaced(ped.Handle) || GameOwned(ped) || IsLawman(ped) != flag2 || OppositeSides(target, ped))
			{
				continue;
			}
			bool flag4 = num != -1 && PED.IS_PED_IN_GROUP(ped.Handle) && PED.GET_PED_GROUP_INDEX(ped.Handle) == num;
			if (!flag4 && num2 < 3f && PED.IS_PED_USING_ANY_SCENARIO(ped.Handle) && (flag || target.IsDead))
			{
				flag4 = true;
			}
			if (!flag4 && target.IsDead)
			{
				flag4 = num2 < 2.2f && ENTITY.GET_ENTITY_VELOCITY(ped.Handle, 0).Length() < 1.5f;
			}
			if (!flag4)
			{
				Vector3 vector3 = ENTITY.GET_ENTITY_VELOCITY(ped.Handle, 0);
				float num3 = vector.Length();
				float num4 = vector3.Length();
				bool flag5 = value?.ContainsKey(ped.Handle) ?? false;
				if (num3 < 0.5f && num4 < 0.5f && num2 < 3.2f)
				{
					flag4 = ((!flag3) ? (num2 < 1.8f || Facing(ped, tp) || Facing(target, ped.Position)) : ((Facing(ped, tp) && Facing(target, ped.Position)) || (flag5 && num2 < 2.5f)));
				}
				else if (num3 > 0.5f && num4 > 0.5f && num2 < 4f)
				{
					flag4 = (vector - vector3).Length() < (flag3 ? 0.4f : 0.8f) && (!flag3 || flag5 || num2 < 2f);
				}
			}
			if (flag4)
			{
				list.Add(ped);
				if (list.Count >= 4)
				{
					break;
				}
			}
		}
		_groupCache[target.Handle] = (Game.GameTime, list.Select((Ped x) => x.Handle).ToList());
		return list;
	}

	internal static bool OppositeSides(Ped a, Ped b)
	{
		try
		{
			Gang gang = Gangs.ForModel((uint)a.Model.Hash);
			Gang gang2 = Gangs.ForModel((uint)b.Model.Hash);
			if (gang != gang2)
			{
				return true;
			}
			if (PED.GET_RELATIONSHIP_BETWEEN_PEDS(a.Handle, b.Handle) >= 4 || PED.GET_RELATIONSHIP_BETWEEN_PEDS(b.Handle, a.Handle) >= 4)
			{
				return true;
			}
			if (!a.IsDead && !b.IsDead && (PED.IS_PED_IN_COMBAT(a.Handle, b.Handle) || PED.IS_PED_IN_COMBAT(b.Handle, a.Handle)))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool Facing(Ped who, Vector3 at)
	{
		Vector3 forwardVector = who.ForwardVector;
		Vector3 vector = at - who.Position;
		vector.Z = 0f;
		float num = vector.Length();
		if (num > 0.01f)
		{
			return (forwardVector.X * vector.X + forwardVector.Y * vector.Y) / num > 0.5f;
		}
		return false;
	}

	private string CircleKey(int handle)
	{
		NemesisRecord nemesisRecord = RecordAt(handle);
		if (nemesisRecord != null)
		{
			if (nemesisRecord.Id == null)
			{
				return null;
			}
			return "n:" + nemesisRecord.Id;
		}
		if (!_acqOf.TryGetValue(handle, out var value))
		{
			return null;
		}
		return value.Id;
	}

	private List<string> CircleOf(string key)
	{
		if (key == null)
		{
			return null;
		}
		if (key.StartsWith("n:"))
		{
			NemesisRecord nemesisRecord = FindRecord(key.Substring(2));
			object obj;
			if (nemesisRecord != null)
			{
				obj = nemesisRecord.CircleAcq;
				if (obj == null)
				{
					return nemesisRecord.CircleAcq = new List<string>();
				}
			}
			else
			{
				obj = null;
			}
			return (List<string>)obj;
		}
		Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == key);
		if (acquaintance != null)
		{
			return acquaintance.Circle ?? (acquaintance.Circle = new List<string>());
		}
		NemesisRecord nemesisRecord2 = Roster.FirstOrDefault((NemesisRecord x) => x.FormerAcqId == key);
		object obj2;
		if (nemesisRecord2 != null)
		{
			obj2 = nemesisRecord2.CircleAcq;
			if (obj2 == null)
			{
				return nemesisRecord2.CircleAcq = new List<string>();
			}
		}
		else
		{
			obj2 = null;
		}
		return (List<string>)obj2;
	}

	private (Acquaintance a, NemesisRecord r) ResolveCircle(string key)
	{
		if (key == null)
		{
			return (a: null, r: null);
		}
		if (key.StartsWith("n:"))
		{
			return (a: null, r: FindRecord(key.Substring(2)));
		}
		Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == key);
		if (acquaintance != null)
		{
			return (a: acquaintance, r: null);
		}
		return (a: null, r: Roster.FirstOrDefault((NemesisRecord x) => x.FormerAcqId == key));
	}

	private void LinkGroup(Ped target, List<Ped> mates, bool met)
	{
		List<Ped> list = new List<Ped> { target };
		list.AddRange(mates);
		foreach (Ped item in list)
		{
			if (RecordAt(item.Handle) != null)
			{
				continue;
			}
			if (!_named.ContainsKey(item.Handle))
			{
				if (!LookReadable(item))
				{
					continue;
				}
				_named[item.Handle] = NameByLook(item);
				_namesGiven++;
			}
			RememberPerson(item.Handle, met && item == target);
		}
		List<string> list2 = (from p in list
			select CircleKey(p.Handle) into k
			where k != null
			select k).Distinct().ToList();
		bool flag = false;
		foreach (string item2 in list2)
		{
			List<string> list3 = CircleOf(item2);
			if (list3 == null)
			{
				continue;
			}
			foreach (string item3 in list2)
			{
				if (item3 != item2 && !list3.Contains(item3))
				{
					list3.Add(item3);
					flag = true;
					if (list3.Count > 6)
					{
						list3.RemoveAt(0);
					}
				}
			}
		}
		List<NemesisRecord> list4 = (from p in list
			select RecordAt(p.Handle) into r
			where r?.IsAlive ?? false
			select r).Distinct().ToList();
		for (int num = 0; num < list4.Count; num++)
		{
			for (int num2 = num + 1; num2 < list4.Count; num2++)
			{
				MakeFriends(list4[num], list4[num2], "found together");
			}
		}
		if (flag)
		{
			GroupsLinked++;
			_peopleDirty = true;
			SaveRoster();
			GrudgeLog("GROUP " + string.Join(", ", list.Select(delegate(Ped p)
			{
				object obj;
				if (!_named.TryGetValue(p.Handle, out var value))
				{
					obj = RecordAt(p.Handle)?.Name;
					if (obj == null)
					{
						return "?";
					}
				}
				else
				{
					obj = value.Name;
				}
				return (string)obj;
			})) + " are together");
		}
		foreach (Ped item4 in list)
		{
			_groupLinked.Add(item4.Handle);
		}
	}

	private void MakeFriends(NemesisRecord a, NemesisRecord b, string why)
	{
		if (a != b && a.Id != null && b.Id != null && !(a.KinId == b.Id) && !(b.KinId == a.Id))
		{
			a.FriendIds = a.FriendIds ?? new List<string>();
			b.FriendIds = b.FriendIds ?? new List<string>();
			if (!a.FriendIds.Contains(b.Id) && a.FriendIds.Count < 3 && b.FriendIds.Count < 3)
			{
				a.FriendIds.Add(b.Id);
				b.FriendIds.Add(a.Id);
				GrudgeLog("FRIENDS " + a.Name + " <-> " + b.Name + " (" + why + ")");
				SaveRoster();
			}
		}
	}

	private void LinkGroupOnce(int handle)
	{
		if (_groupLinked.Contains(handle))
		{
			return;
		}
		_groupLinked.Add(handle);
		Ped ped = Entity.FromHandle(handle) as Ped;
		if (!(ped == null) && ped.Exists() && !ped.IsDead)
		{
			List<Ped> list = GroupOf(ped);
			if (list.Count > 0)
			{
				LinkGroup(ped, list, met: false);
			}
		}
	}

	private void GroupGreet(int target)
	{
		Ped ped = Entity.FromHandle(target) as Ped;
		if (ped == null || !ped.Exists() || ped.IsDead)
		{
			return;
		}
		List<Ped> list = GroupOf(ped);
		_groupLinked.Add(target);
		if (list.Count == 0)
		{
			return;
		}
		Named value3;
		List<Ped> list2 = list.Where((Ped m) => RecordAt(m.Handle) == null && (!_named.TryGetValue(m.Handle, out value3) || !value3.Met)).ToList();
		LinkGroup(ped, list, met: true);
		int num = 0;
		foreach (Ped item in list)
		{
			if (RecordAt(item.Handle) != null)
			{
				continue;
			}
			Introduce(item.Handle);
			try
			{
				Liking(item.Handle, GrudgeAction.EmoteGreet);
			}
			catch
			{
			}
			if (Config.GsBody)
			{
				NemesisRecord nemesisRecord = ShadowOf(item);
				Personality personality = nemesisRecord?.Personality ?? Personality.Cold;
				int num2;
				if (!((_acqOf.TryGetValue(item.Handle, out var value) ? ((float)value.Fear) : 0f) >= 30f))
				{
					if (nemesisRecord == null || !nemesisRecord.TraitsRolled || nemesisRecord.Disposition != Disposition.Surly)
					{
						switch (personality)
						{
						default:
							num2 = ((_rng.Next(2) == 0) ? (-1457020913) : 901097731);
							break;
						case Personality.Cold:
							num2 = -822629770;
							break;
						case Personality.Hothead:
						case Personality.Proud:
							num2 = 1879954891;
							break;
						}
					}
					else
					{
						num2 = 0;
					}
				}
				else
				{
					num2 = -1312563584;
				}
				uint num3 = (uint)num2;
				if (num3 != 0)
				{
					_reactions.Add((Game.GameTime + 1300 + num * 650, item, "greet:" + num3));
				}
			}
			num++;
		}
		if (list2.Count == 0 || !_named.TryGetValue(target, out var value2))
		{
			return;
		}
		GroupIntros++;
		string tLast = LastName(value2.Name);
		List<string> list3 = list2.Select(delegate(Ped m)
		{
			string text3 = (_named.TryGetValue(m.Handle, out value3) ? value3.Name : "a friend");
			string text4 = text3.Split(' ')[0];
			bool num4 = tLast != null && LastName(text3) == tLast;
			bool flag = !PED.IS_PED_MALE(m.Handle);
			if (!num4)
			{
				return text3;
			}
			return (!flag) ? ("my brother " + text4) : ("my sister " + text4);
		}).ToList();
		string text = ((list3.Count == 1) ? list3[0] : (string.Join(", ", list3.Take(list3.Count - 1)) + " and " + list3.Last()));
		string text2 = ((list3.Count != 1) ? "them" : (PED.IS_PED_MALE(list2[0].Handle) ? "him" : "her"));
		string[] array = new string[7]
		{
			"This here's " + text + ".",
			"Meet " + text + ".",
			Cap(text) + " - we ride together.",
			"You'll know " + text + ", then.",
			"Don't mind " + text2 + ". " + Cap(text) + ".",
			Cap(text) + ". Friends of mine.",
			"And this is " + text + "."
		};
		ShowSubtitle(value2.Name + ": \"" + array[_rng.Next(array.Length)] + "\"");
		Feed("GROUP met " + string.Join(", ", list2.Select((Ped m) => (!_named.TryGetValue(m.Handle, out value3)) ? "?" : value3.Name)) + " (with " + value2.Name + ")", Theme.Good);
	}

	private static string Cap(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1);
		}
		return s;
	}

	private static string LastName(string full)
	{
		string[] array = (full ?? "").Split(' ');
		if (array.Length <= 1)
		{
			return null;
		}
		return array[array.Length - 1];
	}

	private void GroupInsulted(int target)
	{
		Ped ped = Entity.FromHandle(target) as Ped;
		if (ped == null || !ped.Exists())
		{
			return;
		}
		List<Ped> list = GroupOf(ped);
		if (list.Count == 0)
		{
			return;
		}
		LinkGroup(ped, list, met: false);
		Ped character = Game.Player.Character;
		foreach (Ped item in list)
		{
			if (RecordAt(item.Handle) != null)
			{
				continue;
			}
			Provocation provocation = RememberStranger(item.Handle);
			if (provocation != null && provocation.Record == null)
			{
				Grudge(item.Handle, provocation, GrudgeAction.FriendInsulted);
				if (Config.TownsfolkReactions)
				{
					TASK.TASK_TURN_PED_TO_FACE_ENTITY(item.Handle, character.Handle, 2500, 0f, 0f, 0f);
					TASK.TASK_LOOK_AT_ENTITY(item.Handle, character.Handle, 4000, 0, 51, 0);
				}
			}
		}
	}

	private void UpdateGroupWatch(Ped player)
	{
		if (Game.GameTime < _nextGroupWatch)
		{
			return;
		}
		_nextGroupWatch = Game.GameTime + 500;
		Vector3 me = player.Position;
		List<(Ped, Vector3)> list = (from x in PedSnapshot(player)
			where x.p != null && (x.pos - me).Length() <= 30f
			select x).ToList();
		if (list.Count == 0)
		{
			return;
		}
		int num = 0;
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			if (num >= 6)
			{
				break;
			}
			Ped item = list[(_groupWatchNext + num2) % list.Count].Item1;
			if (!item.Exists() || item.IsDead || item.IsPlayer || !item.IsHuman || IsPlaced(item.Handle) || Kids.IsChild(item) || IsStoryPed(item))
			{
				continue;
			}
			num++;
			List<Ped> list2 = GroupOf(item);
			if (list2.Count == 0)
			{
				continue;
			}
			if (!_togetherAt.TryGetValue(item.Handle, out var value))
			{
				value = (_togetherAt[item.Handle] = new Dictionary<int, int>());
			}
			foreach (Ped item2 in list2)
			{
				value[item2.Handle] = Game.GameTime;
			}
		}
		_groupWatchNext = (_groupWatchNext + 6) % Math.Max(1, list.Count);
		if (Game.GameTime < _nextTogetherPrune)
		{
			return;
		}
		_nextTogetherPrune = Game.GameTime + 2000;
		foreach (int item3 in _togetherAt.Keys.ToList())
		{
			Dictionary<int, int> dictionary2 = _togetherAt[item3];
			foreach (int item4 in (from kv in dictionary2
				where Game.GameTime - kv.Value > 90000
				select kv.Key).ToList())
			{
				dictionary2.Remove(item4);
			}
			if (dictionary2.Count == 0 || !ENTITY.DOES_ENTITY_EXIST(item3))
			{
				_togetherAt.Remove(item3);
			}
		}
	}

	private List<Ped> RecentMates(int handle)
	{
		List<Ped> list = new List<Ped>();
		if (_togetherAt.TryGetValue(handle, out var value))
		{
			foreach (KeyValuePair<int, int> item in value)
			{
				Ped ped = Entity.FromHandle(item.Key) as Ped;
				if (ped != null && ped.Exists())
				{
					list.Add(ped);
				}
			}
		}
		foreach (KeyValuePair<int, Dictionary<int, int>> item2 in _togetherAt)
		{
			if (item2.Value.ContainsKey(handle))
			{
				Ped ped2 = Entity.FromHandle(item2.Key) as Ped;
				if (ped2 != null && ped2.Exists() && !list.Contains(ped2))
				{
					list.Add(ped2);
				}
			}
		}
		return list;
	}

	internal bool IsCircleMate(int mate, int victim)
	{
		if (_togetherAt.TryGetValue(victim, out var value) && value.ContainsKey(mate))
		{
			return true;
		}
		if (_togetherAt.TryGetValue(mate, out var value2) && value2.ContainsKey(victim))
		{
			return true;
		}
		if (_groupCache.TryGetValue(victim, out (int, List<int>) value3) && Game.GameTime - value3.Item1 < 120000 && value3.Item2.Contains(mate))
		{
			return true;
		}
		string text = CircleKey(victim);
		string text2 = CircleKey(mate);
		if (text != null && text2 != null)
		{
			List<string> list = CircleOf(text);
			if (list != null && list.Contains(text2))
			{
				return true;
			}
		}
		NemesisRecord nemesisRecord = RecordAt(victim);
		NemesisRecord nemesisRecord2 = RecordAt(mate);
		if (nemesisRecord != null && nemesisRecord2 != null)
		{
			return nemesisRecord.FriendIds?.Contains(nemesisRecord2.Id) ?? false;
		}
		return false;
	}

	private void QueuePeopleNews(string deadName, string deadTown, List<string> circle, Vector3 body, NemesisRecord deadRec = null)
	{
		if (circle == null || circle.Count == 0)
		{
			return;
		}
		long num = GameMinutesNow();
		List<DeathNews> list = Chronicle.PeopleNews ?? (Chronicle.PeopleNews = new List<DeathNews>());
		HashSet<object> hashSet = new HashSet<object>();
		foreach (string item2 in circle.ToList())
		{
			var (a, r) = ResolveCircle(item2);
			if ((a == null && r == null) || !hashSet.Add(((object)r) ?? ((object)a)) || (a != null && a.Dead) || (r != null && !r.IsAlive))
			{
				continue;
			}
			if (r != null && deadRec != null)
			{
				List<string> friendIds = deadRec.FriendIds;
				if ((friendIds != null && friendIds.Contains(r.Id)) || r.KinId == deadRec.Id || deadRec.KinId == r.Id)
				{
					continue;
				}
			}
			bool num2 = _acqOf.Any((KeyValuePair<int, Acquaintance> kv) => kv.Value == a && a != null && ENTITY.DOES_ENTITY_EXIST(kv.Key) && new Ped(kv.Key).Position.DistanceTo(body) < 60f) || _worldNemeses.Any((KeyValuePair<int, NemesisRecord> kv) => kv.Value == r && r != null && ENTITY.DOES_ENTITY_EXIST(kv.Key) && new Ped(kv.Key).Position.DistanceTo(body) < 60f) || (r != null && r == ActiveRecord && _activeNemesisPed != null && _activeNemesisPed.Exists() && _activeNemesisPed.Position.DistanceTo(body) < 60f);
			string text = a?.Town ?? r?.KillLocation;
			int num3 = ((!num2) ? ((text != null && text == deadTown) ? (3 + _rng.Next(10)) : (12 + _rng.Next(37))) : 0);
			DeathNews item = new DeathNews
			{
				DeadName = deadName,
				KinId = item2,
				ByYou = true,
				At = num,
				DueAt = num + num3 * 60 + _rng.Next(60)
			};
			if (!num2)
			{
				list.Add(item);
				GrudgeLog(Pron.Her($"NEWS {a?.Name ?? r?.Name} will hear you killed his friend {deadName} in {num3} h", Models.Female(a?.Model ?? r?.ModelHash ?? 0)));
			}
		}
		SaveWorld();
	}

	private void DeliverPeopleNews(DeathNews n)
	{
		(Acquaintance, NemesisRecord) tuple = ResolveCircle(n.KinId);
		Acquaintance a = tuple.Item1;
		NemesisRecord item = tuple.Item2;
		string text = Pron.Her("heard you killed his friend " + n.DeadName, Models.Female(a?.Model ?? item?.ModelHash ?? 0));
		if (item != null)
		{
			if (item.IsAlive)
			{
				int hate = item.Hate;
				item.AddHate(12);
				Remember(item, "Grief", 60, "you killed his friend, " + n.DeadName);
				History(item, "HEARD you killed his friend " + n.DeadName, Theme.Blood);
				item.Touch();
				Feed("FRIEND " + item.Name + " " + text + ": hate " + Delta(hate, item.Hate), Theme.Blood, item);
			}
		}
		else
		{
			if (a == null || a.Dead)
			{
				return;
			}
			Personality result;
			Personality personality = (Enum.TryParse<Personality>(a.Temper ?? "", out result) ? result : Personality.Cold);
			float num;
			switch (personality)
			{
			default:
				num = 1f;
				break;
			case Personality.Coward:
				num = 0.5f;
				break;
			case Personality.Drifter:
				num = 0.6f;
				break;
			case Personality.Cold:
				num = 0.7f;
				break;
			case Personality.Hothead:
			case Personality.Proud:
			case Personality.Loyal:
				num = 1.3f;
				break;
			}
			float num2 = num;
			int grudge = a.Grudge;
			int fear = a.Fear;
			a.Grudge = Math.Min(100, a.Grudge + (int)Math.Round(45f * num2));
			a.Fear = Math.Min(100, a.Fear + ((personality == Personality.Coward) ? 18 : 6));
			a.Like = Math.Max(0, a.Like - 25);
			a.GrudgeSettled = false;
			(a.Witnessed ?? (a.Witnessed = new List<string>())).Add(text + " - " + GameDateShort());
			TrimWitnessed(a);
			_peopleDirty = true;
			foreach (KeyValuePair<int, Acquaintance> item2 in _acqOf.Where((KeyValuePair<int, Acquaintance> kv) => kv.Value == a).ToList())
			{
				RestoreGrudge(item2.Key, a);
			}
			GrudgeLog($"NEWS {a.Name} {text} ({(float)(GameMinutesNow() - n.At) / 60f:0.#} h after): grudge {grudge} -> {a.Grudge}, fear {fear} -> {a.Fear}");
			Feed($"FRIEND {a.Name} ({personality.ToString().ToLowerInvariant()}) {text}: grudge {grudge} to {a.Grudge}, fear {fear} to {a.Fear}", Theme.Blood);
		}
	}

	private void UpdatePeopleNews()
	{
		List<DeathNews> peopleNews = Chronicle.PeopleNews;
		if (peopleNews == null || peopleNews.Count == 0)
		{
			return;
		}
		long now = GameMinutesNow();
		List<DeathNews> list = (from n in peopleNews
			where n.DueAt <= now
			orderby n.DueAt
			select n).ToList();
		if (list.Count == 0)
		{
			return;
		}
		foreach (DeathNews item in list)
		{
			peopleNews.Remove(item);
			try
			{
				DeliverPeopleNews(item);
			}
			catch (Exception ex)
			{
				GrudgeLog("NEWS failed: " + ex.Message);
			}
		}
		SaveWorld();
	}

	internal string GroupsDebugLine()
	{
		return $"groups: {People.Count((Acquaintance a) => a.Circle != null && a.Circle.Count > 0)} people in circles, {GroupsLinked} linked / {GroupIntros} introduced this session, " + $"{Chronicle.PeopleNews?.Count ?? 0} friends yet to hear of a death";
	}
}
