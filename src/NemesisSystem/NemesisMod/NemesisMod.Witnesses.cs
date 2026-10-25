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
	private readonly Dictionary<int, NemesisRecord> _witnessShadow = new Dictionary<int, NemesisRecord>();

	internal int WitnessesTotal;

	internal int WitnessCrimes;

	internal string LastWitnessNote = "";

	private static readonly Dictionary<string, int> LawmanKoFear = new Dictionary<string, int>
	{
		["Porter Kent"] = 8,
		["Owen Blevins"] = 7,
		["Edmund Boone"] = 25,
		["Alpheus Manning"] = 20,
		["Grover Cowan"] = 9,
		["Jake Gates"] = 8,
		["Erastus Pierce"] = 20,
		["Mabel Birch"] = 7
	};

	private readonly HashSet<int> _cameAtYou = new HashSet<int>();

	private readonly Dictionary<uint, int> _cameAtYouGroup = new Dictionary<uint, int>();

	private readonly Dictionary<int, int> _rescuedAt = new Dictionary<int, int>();

	private uint _civMale;

	private uint _civFemale;

	private readonly Dictionary<int, int> _recognisedAt = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _calmlyLeft = new Dictionary<int, int>();

	private readonly List<(int handle, Acquaintance a, int at, string crime)> _recentWitnesses = new List<(int, Acquaintance, int, string)>();

	private int _lastBounty = -1;

	private int _nextReportCheck;

	private int _nextRecognise;

	private uint _lastCrimeType;

	private int _lastCrimeAt = -100000;

	private const int CrimeSeenMs = 15000;

	private const int RumourFearCap = 40;

	private const float BusyBoltFear = 75f;

	private readonly Dictionary<string, (float v, int at)> _townWordCache = new Dictionary<string, (float, int)>();

	internal bool _rememberingWitness;

	private void FixPearseKeogh()
	{
		WorldState chronicle = Chronicle;
		if (chronicle.PearseFixed)
		{
			return;
		}
		chronicle.PearseFixed = true;
		SaveWorld();
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord r) => r.Name == "Joseph Casey" && r.Gang == "odriscoll" && r.IsAlive);
		int num = People.RemoveAll((Acquaintance a) => a.Name == "Pearse Keogh" && a.Model == MISC.GET_HASH_KEY("g_m_m_uniduster_04"));
		foreach (KeyValuePair<int, Named> item in _named.Where((KeyValuePair<int, Named> kv) => kv.Value.Name == "Pearse Keogh").ToList())
		{
			if (nemesisRecord != null)
			{
				item.Value.Name = nemesisRecord.Name;
				item.Value.Title = nemesisRecord.Title ?? "";
			}
			else
			{
				_named.Remove(item.Key);
			}
		}
		if (num > 0)
		{
			_peopleDirty = true;
			GrudgeLog("PEOPLE 'Pearse Keogh' was " + ((nemesisRecord != null) ? "Joseph Casey" : "a renamed man") + " - the stray entry removed");
		}
	}

	private void FixValentineSelfDefence()
	{
		WorldState chronicle = Chronicle;
		if (chronicle.SelfDefenceFixed)
		{
			return;
		}
		chronicle.SelfDefenceFixed = true;
		SaveWorld();
		int num = 0;
		foreach (Acquaintance person in People)
		{
			if (person.Town != "Valentine" || person.Witnessed == null)
			{
				continue;
			}
			bool flag = person.Witnessed.Any((string w) => w == "saw you knock a man cold - Valentine, Feb 22");
			bool flag2 = person.Witnessed.Any((string w) => w == "saw you beat a man - Valentine, Feb 22");
			if ((!flag && !flag2) || person.Witnessed.Any((string w) => w.StartsWith("saw you") && !w.Contains("beat a man") && !w.Contains("knock a man cold")))
			{
				continue;
			}
			int fear = person.Fear;
			if (flag && LawmanKoFear.TryGetValue(person.Name, out var value))
			{
				person.Fear = Math.Max(0, person.Fear - value);
			}
			person.Fear = (int)Math.Round((float)person.Fear / 4f);
			for (int num2 = 0; num2 < person.Witnessed.Count; num2++)
			{
				if (person.Witnessed[num2] == "saw you knock a man cold - Valentine, Feb 22" || person.Witnessed[num2] == "saw you beat a man - Valentine, Feb 22")
				{
					person.Witnessed[num2] = "saw you fight off a man who went for you - Valentine, Feb 22";
				}
			}
			person.Witnessed = person.Witnessed.Distinct().ToList();
			num++;
			GrudgeLog($"PEOPLE {person.Name}: Valentine Feb 22 was self-defence - fear {fear} -> {person.Fear}");
		}
		if (num > 0)
		{
			_peopleDirty = true;
			_townWordCache.Clear();
			GrudgeLog($"PEOPLE {num} Valentine witnesses: those fights were self-defence (fear cut)");
		}
	}

	private bool CameAtYou(int h)
	{
		if (h == 0)
		{
			return false;
		}
		if (ENTITY.DOES_ENTITY_EXIST(h) && IsLawman(new Ped(h)))
		{
			return false;
		}
		if (_selfDefence.Contains(h) || _cameAtYou.Contains(h))
		{
			return true;
		}
		OurRole ourRole = RoleOf(h);
		if (ourRole != OurRole.Active && ourRole != OurRole.Crew && ourRole != OurRole.HiredGun && ourRole != OurRole.Hunter)
		{
			return ourRole == OurRole.Rival;
		}
		return true;
	}

	private string AttackerLabel(int h)
	{
		Gang gang = Gangs.ById(RecordAt(h)?.Gang) ?? (ENTITY.DOES_ENTITY_EXIST(h) ? Gangs.ForModel(ENTITY.GET_ENTITY_MODEL(h)) : null);
		if (gang == null || gang.StoryOnly)
		{
			return NameOf(h);
		}
		string text = OutfitWord(gang);
		return (("AEIOU".IndexOf(char.ToUpperInvariant(text.FirstOrDefault())) >= 0) ? "an " : "a ") + text;
	}

	private string KilledHow(int h, string who)
	{
		string text = Downed.How(h);
		if (!(text == "choked"))
		{
			if (text == "stomped")
			{
				return "saw you stomp to death " + who + " who came at you";
			}
			uint num = (ENTITY.DOES_ENTITY_EXIST(h) ? PED.GET_PED_CAUSE_OF_DEATH(h) : 0u);
			object obj;
			if (num == 0 || !Enum.IsDefined(typeof(eWeapon), num))
			{
				obj = "";
			}
			else
			{
				eWeapon eWeapon = (eWeapon)num;
				obj = eWeapon.ToString();
			}
			string text2 = (string)obj;
			if (num != 0 && (num == MISC.GET_HASH_KEY("WEAPON_EXPLOSION") || text2.Contains("Dynamite")))
			{
				return "saw you blow up " + who + " who came at you";
			}
			if (num != 0 && (num == MISC.GET_HASH_KEY("WEAPON_FIRE") || text2.Contains("Molotov") || text2.Contains("FireBottle")))
			{
				return "saw you burn to death " + who + " who came at you";
			}
			switch ((num != 0) ? WEAPON.GET_WEAPONTYPE_GROUP(num) : 0)
			{
			case 2685387236u:
				return "saw you beat to death " + who + " who came at you";
			case 3566412244u:
			case 1548507267u:
				return "saw you cut down " + who + " who came at you";
			case 3053283277u:
				return "saw you put an arrow in " + who + " who came at you";
			case 2725924767u:
			case 3082541095u:
			case 3193669993u:
			case 3700405225u:
			case 416676503u:
			case 860033945u:
			case 970310034u:
				return "saw you gun down " + who + " who came at you";
			default:
				return "saw you kill " + who + " who came at you";
			}
		}
		return "saw you strangle " + who + " who came at you";
	}

	private static string GoreHow(string text, string who)
	{
		if (text != null && text.Contains("blow a man apart"))
		{
			return "saw you blow apart " + who + " who came at you";
		}
		if (text != null && text.Contains(" a man"))
		{
			return text.Replace(" a man", " " + who) + " who came at you";
		}
		return "saw you kill " + who + " who came at you";
	}

	private void Witness(string crime, int victim, Vector3 at, string text = null)
	{
		try
		{
			LawfulPatrolsSee(crime, victim, at);
		}
		catch (Exception ex)
		{
			GrudgeLog("PATROL crime check failed: " + ex.Message);
		}
		(int, float, bool, string) value;
		if (crime == "gore")
		{
			value = (Balance.GoreFear, 60f, true, text ?? "saw you gore a man");
		}
		else
		{
			if (!Crimes.TryGetValue(crime, out value))
			{
				return;
			}
			if (text != null)
			{
				value.Item4 = text;
			}
		}
		int num;
		switch (crime)
		{
		case "shot":
		case "kill":
		case "gore":
			num = (CameAtYou(victim) ? 1 : 0);
			break;
		default:
			num = 0;
			break;
		}
		bool flag = (byte)num != 0;
		int num2;
		switch (crime)
		{
		case "punch":
		case "ko":
		case "hogtie":
			num2 = (_selfDefence.Contains(victim) ? 1 : 0);
			break;
		default:
			num2 = 0;
			break;
		}
		bool flag2 = (byte)((uint)num2 | (flag ? 1u : 0u)) != 0;
		if (flag2)
		{
			value.Item1 = Math.Max(1, value.Item1 / 4);
			string text2 = AttackerLabel(victim);
			value.Item4 = crime switch
			{
				"shot" => "saw you shoot " + text2 + " who came at you", 
				"gore" => GoreHow(value.Item4, text2), 
				"kill" => KilledHow(victim, text2), 
				"hogtie" => "saw you tie up " + NameOf(victim) + " after he went for you", 
				_ => "saw you fight off " + NameOf(victim), 
			};
		}
		else
		{
			_yourCrimeAt = Game.GameTime;
			_yourCrimeWhat = crime;
			_yourCrimePos = at;
		}
		if (_witnessedAt.Count > 400)
		{
			foreach (var item in (from kv in _witnessedAt
				where Game.GameTime - kv.Value > 60000
				select kv.Key).ToList())
			{
				_witnessedAt.Remove(item);
			}
		}
		Ped character = Game.Player.Character;
		if (character == null || !character.Exists())
		{
			return;
		}
		bool flag3 = PlayerMasked(character);
		uint victimGroup = (ENTITY.DOES_ENTITY_EXIST(victim) ? ModGroupOf(victim) : 0u);
		Vector3 position = character.Position;
		List<string> list = new List<string>();
		int num3 = 0;
		int num4 = 0;
		List<(Ped, float, int)> list2 = new List<(Ped, float, int)>();
		Ped ped = Entity.FromHandle(victim) as Ped;
		if (ped != null && ped.Exists())
		{
			try
			{
				List<Ped> list3 = RecentMates(victim);
				foreach (Ped item2 in GroupOf(ped, allowDead: true))
				{
					if (!list3.Contains(item2))
					{
						list3.Add(item2);
					}
				}
				if (list3.Count > 0)
				{
					LinkGroup(ped, list3, met: false);
				}
			}
			catch (Exception ex2)
			{
				GrudgeLog("GROUP failed: " + ex2.Message);
			}
		}
		List<(Ped p, bool mate, float d)> list4 = (from x in PedsAround(character)
			where x != null && x.Exists() && x.Handle != victim
			select (p: x, mate: IsCircleMate(x.Handle, victim), d: x.Position.DistanceTo(at)) into t
			orderby t.mate descending, t.d
			select t).ToList();
		Ped ped2 = Entity.FromHandle(victim) as Ped;
		foreach (var (ped3, flag4, _) in list4)
		{
			if (num3 >= 12 && !flag4)
			{
				break;
			}
			if (ped3.IsDead || ped3.IsPlayer || !ped3.IsHuman)
			{
				continue;
			}
			float num5 = ped3.Position.DistanceTo(at);
			int num6;
			if (!(crime == "gore") || !Crimes.TryGetValue("kill", out (int, float, bool, string) value2))
			{
				(num6, _, _, _) = value;
			}
			else
			{
				num6 = GoreAt(num5, value2.Item1);
			}
			int num7 = num6;
			if (flag2 && crime == "gore")
			{
				num7 = Math.Max(1, num7 / 4);
			}
			if ((ped2 != null && ped2.Exists() && Rescued(ped3, ped2, crime)) || num5 > value.Item2 || Kids.IsChild(ped3) || IsStoryPed(ped3) || IsLawman(ped3))
			{
				continue;
			}
			if (_combatants.ContainsKey(ped3.Handle) || InTheFight(ped3, victimGroup))
			{
				if (flag4 && !flag3 && RecordAt(ped3.Handle) == null && !GameOwned(ped3))
				{
					Provocation provocation = RememberStranger(ped3.Handle);
					(int, string) key = (ped3.Handle, "friend:" + crime);
					if (provocation != null && provocation.Record == null && (!_witnessedAt.TryGetValue(key, out var value3) || Game.GameTime - value3 >= 20000))
					{
						_witnessedAt[key] = Game.GameTime;
						Grudge(ped3.Handle, provocation, (crime == "kill" || crime == "gore") ? GrudgeAction.FriendKilled : GrudgeAction.FriendHurt);
						list.Add(WhoIs(ped3.Handle) + " (fighting you, " + (Pron.FemaleAt(ped3.Handle) ? "her" : "his") + " friend)");
					}
				}
				continue;
			}
			if ((_activeNemesisPed != null && ped3.Handle == _activeNemesisPed.Handle && _enc != null && _enc.Engaged) || GameOwned(ped3))
			{
				continue;
			}
			float num8 = ped3.Position.DistanceTo(position);
			bool num9;
			if (!value.Item3)
			{
				if (!(num8 < 6f))
				{
					num9 = ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT(ped3.Handle, character.Handle, 17);
					goto IL_0720;
				}
			}
			else if (!(num8 < 20f))
			{
				num9 = ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(ped3.Handle, character.Handle, 17);
				goto IL_0720;
			}
			goto IL_0722;
			IL_0722:
			(int, string) key2 = (ped3.Handle, crime);
			if (!_witnessedAt.TryGetValue(key2, out var value4) || Game.GameTime - value4 >= 20000)
			{
				_witnessedAt[key2] = Game.GameTime;
				num3++;
				if (flag3)
				{
					num4++;
					NoteMaskedWitness(ped3, crime, value.Item4, num7, at);
				}
				else
				{
					list2.Add((ped3, num5, num7));
				}
			}
			continue;
			IL_0720:
			if (!num9)
			{
				continue;
			}
			goto IL_0722;
		}
		if (num3 == 0)
		{
			return;
		}
		WitnessCrimes++;
		WitnessesTotal += num3;
		WitnessBatch witnessBatch = new WitnessBatch
		{
			Crime = crime,
			Text = value.Item4,
			At = at,
			Victim = victim,
			SelfDefence = flag2,
			Count = num3,
			Masked = num4,
			Seen = list
		};
		foreach (var item3 in list2)
		{
			witnessBatch.Jobs.Enqueue(item3);
		}
		for (int num10 = 0; num10 < 3; num10++)
		{
			if (witnessBatch.Jobs.Count <= 0)
			{
				break;
			}
			var (p, d, fearHere) = witnessBatch.Jobs.Dequeue();
			WitnessOne(witnessBatch, p, d, fearHere);
		}
		if (witnessBatch.Jobs.Count == 0)
		{
			WitnessDone(witnessBatch);
		}
		else
		{
			_witnessBatches.Enqueue(witnessBatch);
		}
	}

	private void WitnessDone(WitnessBatch b)
	{
		string text = $"WITNESSES ({b.Count}) {b.Text}";
		LastWitnessNote = ((b.Masked == b.Count) ? (text + " - you were masked") : (text + ": " + string.Join("; ", b.Seen)));
		if (b.Masked == b.Count)
		{
			Feed(text + " - you were masked: they don't know it was you", Theme.Dim);
		}
		else if (b.Seen.Count > 0)
		{
			Feed(text + ": " + string.Join("; ", b.Seen.Take(4)) + ((b.Seen.Count > 4) ? $"; +{b.Seen.Count - 4} more" : ""), (150, 170, 210));
		}
	}

	private void WitnessOne(WitnessBatch b, Ped p, float d, int fearHere)
	{
		if (p == null || !p.Exists() || p.IsDead)
		{
			return;
		}
		string crime = b.Crime;
		Vector3 at = b.At;
		int victim = b.Victim;
		bool selfDefence = b.SelfDefence;
		List<string> seen = b.Seen;
		NemesisRecord nemesisRecord = RecordAt(p.Handle);
		if (nemesisRecord != null)
		{
			if (crime == "kill" || crime == "gore")
			{
				NemesisRecord nemesisRecord2 = RecordAt(victim);
				if (nemesisRecord2 != null && nemesisRecord2 != nemesisRecord)
				{
					if (nemesisRecord.KinId == nemesisRecord2.Id || nemesisRecord2.KinId == nemesisRecord.Id)
					{
						return;
					}
					List<string> friendIds = nemesisRecord.FriendIds;
					if (friendIds != null && friendIds.Contains(nemesisRecord2.Id))
					{
						return;
					}
					List<string> friendIds2 = nemesisRecord2.FriendIds;
					if (friendIds2 != null && friendIds2.Contains(nemesisRecord.Id))
					{
						return;
					}
				}
			}
			if ((crime == "kill" || crime == "gore") && IsCircleMate(p.Handle, victim))
			{
				int hate = nemesisRecord.Hate;
				nemesisRecord.AddHate(15);
				Named value;
				string text = (_named.TryGetValue(victim, out value) ? value.Name : (RecordAt(victim)?.Name ?? "a friend"));
				Remember(nemesisRecord, "Grief", 70, "you killed his friend " + text + " in front of him");
				History(nemesisRecord, "SAW YOU KILL his friend " + text, Theme.Blood);
				nemesisRecord.Touch();
				seen.Add(string.Format("{0} (nemesis, {1} friend) hate {2} to {3}", nemesisRecord.Name, Pron.FemaleAt(p.Handle) ? "her" : "his", hate, nemesisRecord.Hate));
			}
			else
			{
				if (crime == "kill" || crime == "gore")
				{
					return;
				}
				if (IsCircleMate(p.Handle, victim))
				{
					int hate2 = nemesisRecord.Hate;
					nemesisRecord.AddHate(6);
					if (nemesisRecord.Hate != hate2)
					{
						seen.Add(string.Format("{0} (nemesis, {1} friend) hate {2} to {3}", nemesisRecord.Name, Pron.FemaleAt(p.Handle) ? "her" : "his", hate2, nemesisRecord.Hate));
					}
				}
				int fear = nemesisRecord.Fear;
				nemesisRecord.AddFear(Math.Max(1, fearHere / 3));
				if (nemesisRecord.Fear != fear)
				{
					nemesisRecord.Touch();
					seen.Add($"{nemesisRecord.Name} (nemesis) fear {fear} to {nemesisRecord.Fear}");
				}
			}
			return;
		}
		if (!_named.ContainsKey(p.Handle))
		{
			if (!LookReadable(p))
			{
				return;
			}
			_named[p.Handle] = NameByLook(p);
			_namesGiven++;
		}
		_rememberingWitness = true;
		try
		{
			RememberPerson(p.Handle, met: false);
		}
		finally
		{
			_rememberingWitness = false;
		}
		if (!_acqOf.TryGetValue(p.Handle, out var value2))
		{
			return;
		}
		NemesisRecord nemesisRecord3 = ShadowOf(p);
		float num = nemesisRecord3.FearGainMult * Balance.FearGainScale;
		bool flag = IsCircleMate(p.Handle, victim);
		if (flag)
		{
			num *= 1.4f;
		}
		int num2 = Math.Max(1, (int)Math.Round((float)fearHere * num));
		int fear2 = value2.Fear;
		value2.Fear = Math.Min(100, value2.Fear + num2);
		value2.LastScaredMin = GameMinutesNow();
		if (string.IsNullOrEmpty(value2.Temper) && nemesisRecord3.Personality.HasValue)
		{
			value2.Temper = nemesisRecord3.Personality.ToString();
		}
		string text2 = (flag ? Pron.Her(b.Text.Replace("a man", "his friend"), Pron.FemaleAt(p.Handle)) : b.Text);
		(value2.Witnessed ?? (value2.Witnessed = new List<string>())).Add(text2 + " - " + GetZoneName(at) + ", " + GameDateShort());
		TrimWitnessed(value2);
		value2.LastSeen = DateTime.Now;
		value2.LastGameMin = GameMinutesNow();
		_peopleDirty = true;
		MarkNamed(p.Handle, 3);
		if (!selfDefence)
		{
			NoteWitness(p.Handle, value2, b.Text);
		}
		seen.Add(string.Format("{0} ({1}, x{2:0.0}{3}) fear {4} to {5}", value2.Name, (value2.Temper ?? "?").ToLowerInvariant(), num, (!flag) ? "" : (Pron.FemaleAt(p.Handle) ? ", her friend" : ", his friend"), fear2, value2.Fear));
		if (flag)
		{
			Provocation provocation = RememberStranger(p.Handle);
			if (provocation != null && provocation.Record == null)
			{
				Grudge(p.Handle, provocation, (crime == "kill" || crime == "gore") ? GrudgeAction.FriendKilled : GrudgeAction.FriendHurt);
			}
			LikeBy(p.Handle, (crime == "kill" || crime == "gore") ? (-30) : (-15), "you hurt his friend");
		}
		GrudgeLog(string.Format("WITNESS {0} {1} at {2:0} m ({3}, fear x{4:0.00}): fear {5} -> {6}", value2.Name, b.Text, d, value2.Temper ?? "?", num, fear2, value2.Fear));
	}

	private bool Rescued(Ped p, Ped victim, string crime)
	{
		if (p.Position.DistanceTo(victim.Position) > 12f || IsLawman(p) || Kids.IsChild(p) || IsStoryPed(p) || IsPlaced(p.Handle))
		{
			return false;
		}
		if (RecordAt(p.Handle) != null || _combatants.ContainsKey(p.Handle))
		{
			return false;
		}
		Gang gang = Gangs.ForModel((uint)victim.Model.Hash);
		bool flag = Models.Name((uint)p.Model.Hash).StartsWith("re_") && Gangs.ForModel((uint)p.Model.Hash) == null;
		if ((gang == null || Gangs.ForModel((uint)p.Model.Hash) == gang || (!flag && PED.GET_RELATIONSHIP_BETWEEN_PEDS(victim.Handle, p.Handle) < 4)) && PED.GET_RELATIONSHIP_BETWEEN_PEDS(victim.Handle, p.Handle) < 5 && (victim.IsDead || !PED.IS_PED_IN_COMBAT(victim.Handle, p.Handle)))
		{
			return false;
		}
		if (_rescuedAt.TryGetValue(p.Handle, out var value) && Game.GameTime - value < 300000)
		{
			return true;
		}
		_rescuedAt[p.Handle] = Game.GameTime;
		if (!_named.ContainsKey(p.Handle) && RecordAt(p.Handle) == null && LookReadable(p))
		{
			_named[p.Handle] = NameByLook(p);
			_namesGiven++;
		}
		string text = ((gang != null) ? ("the " + gang.Name) : ("the men who were after " + (Pron.FemaleAt(p.Handle) ? "her" : "him")));
		string text2 = "you saved " + (Pron.FemaleAt(p.Handle) ? "her" : "him") + " from " + text;
		Favour(p.Handle, text2);
		GrudgeLog("RESCUE " + WhoIs(p.Handle) + ": " + text2 + " (" + crime + " on " + WhoIs(victim.Handle) + ")");
		return true;
	}

	private bool InTheFight(Ped p, uint victimGroup)
	{
		int handle = p.Handle;
		switch (RoleOf(handle))
		{
		case OurRole.Active:
		case OurRole.Crew:
		case OurRole.HiredGun:
		case OurRole.Camp:
		case OurRole.Patrol:
		case OurRole.Skirmish:
		case OurRole.Hunter:
		case OurRole.General:
		case OurRole.Rival:
			return true;
		default:
			if (PED.IS_PED_IN_COMBAT(handle, 0))
			{
				return true;
			}
			if (victimGroup != 0)
			{
				if (_civMale == 0)
				{
					_civMale = MISC.GET_HASH_KEY("REL_CIVMALE");
					_civFemale = MISC.GET_HASH_KEY("REL_CIVFEMALE");
				}
				if (victimGroup != _civMale && victimGroup != _civFemale && ModGroupOf(handle) == victimGroup)
				{
					return true;
				}
			}
			return false;
		}
	}

	private static string GameDateShort()
	{
		return GameDate.TodayShort();
	}

	internal IEnumerable<string> WitnessDebugLines(Ped player)
	{
		yield return $"crimes seen {WitnessCrimes}, witnesses {WitnessesTotal}, remembered with fear {People.Count((Acquaintance a) => a.Fear > 0)}";
		Vector3 me = player.Position;
		List<(Acquaintance, float)> list = (from kv in _acqOf
			where kv.Value.Fear > 0 && ENTITY.DOES_ENTITY_EXIST(kv.Key)
			select (a: kv.Value, d: new Ped(kv.Key).Position.DistanceTo(me)) into t
			where t.d < 80f
			orderby t.d
			select t).Take(8).ToList();
		foreach (var (acquaintance, num) in list)
		{
			yield return string.Format("{0} {1:0} m - {2}, fear {3} - {4}", acquaintance.Name, num, (acquaintance.Temper ?? "?").ToLowerInvariant(), acquaintance.Fear, acquaintance.Witnessed?.LastOrDefault() ?? "");
		}
		if (!string.IsNullOrEmpty(LastWitnessNote))
		{
			yield return "last: " + LastWitnessNote;
		}
		yield return TownWordDebugLine();
		yield return UneaseNote;
	}

	private void PressedAfterLeaving(int h, GrudgeAction act)
	{
		if (!_calmlyLeft.TryGetValue(h, out var value))
		{
			return;
		}
		_calmlyLeft.Remove(h);
		if (Config.TownsfolkReactions && Game.GameTime - value <= 300000 && ENTITY.DOES_ENTITY_EXIST(h) && !ENTITY.IS_ENTITY_DEAD(h))
		{
			Ped ped = new Ped(h);
			Ped character = Game.Player.Character;
			Acquaintance value2;
			string text = (_acqOf.TryGetValue(h, out value2) ? value2.Name : WhoIs(h));
			string arg;
			if (ped.Position.DistanceTo(character.Position) < 6f)
			{
				TASK.CLEAR_PED_TASKS(h, p1: false, p2: false);
				TASK.TASK_COWER(h, 6000, character.Handle, "");
				Say(ped, "GENERIC_SHOCKED_HIGH", character);
				ShowSubtitle(text + ": Please - I don't want no trouble!");
				arg = "cowers";
			}
			else
			{
				Leave(ped, character.Handle, "runs");
				Say(ped, "GENERIC_SHOCKED_HIGH", character);
				arg = "runs";
			}
			GrudgeLog($"RECOGNISED {text}: you went after him ({act}) after he walked off - {arg}");
		}
	}

	private string CrimeSeen()
	{
		if (Game.GameTime - _lastCrimeAt >= 15000)
		{
			return null;
		}
		return CrimeNames.Name(_lastCrimeType);
	}

	private void NoteWitness(int handle, Acquaintance a, string crime)
	{
		_recognisedAt[handle] = Game.GameTime;
		_recentWitnesses.Add((handle, a, Game.GameTime, crime));
		_recentWitnesses.RemoveAll(((int handle, Acquaintance a, int at, string crime) w) => Game.GameTime - w.at > 300000);
	}

	private void UpdateWitnessRecognition(Ped player)
	{
		if (Game.GameTime < _nextRecognise)
		{
			return;
		}
		_nextRecognise = Game.GameTime + 1000;
		if (PlayerMasked(player) || PlayingTableGame() || !Config.TownsfolkReactions || MISC.GET_MISSION_FLAG())
		{
			return;
		}
		Vector3 position = player.Position;
		foreach (KeyValuePair<int, Acquaintance> item in _acqOf.ToList())
		{
			Acquaintance value = item.Value;
			if (value.Fear < 20 || value.Dead)
			{
				continue;
			}
			int h = item.Key;
			if (!ENTITY.DOES_ENTITY_EXIST(h) || ENTITY.IS_ENTITY_DEAD(h) || (_recognisedAt.TryGetValue(h, out var value2) && Game.GameTime - value2 < 600000))
			{
				continue;
			}
			Ped ped = new Ped(h);
			float num = ped.Position.DistanceTo(position);
			if (num > 22f || IsPlaced(h) || _combatants.ContainsKey(h) || PED.IS_PED_IN_COMBAT(h, 0) || ped.IsHogtied || ENTITY.IS_ENTITY_ATTACHED(h) || ped.IsInVehicle || IsLawman(ped) || HardMan(ped) || GameOwned(ped) || GameEventPed(ped) || !ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT(h, player.Handle, 17))
			{
				continue;
			}
			_recognisedAt[h] = Game.GameTime;
			if (AtTableGame(h))
			{
				continue;
			}
			string text = BusyNote(h);
			bool flag = PED.IS_PED_USING_ANY_SCENARIO(h);
			string text2 = value.Temper ?? "";
			float num2 = (float)value.Fear - (float)value.Like / 2f;
			if (num2 < 20f)
			{
				GrudgeLog($"RECOGNISED {value.Name} ({text2}, fear {value.Fear}, likes you {value.Like}) - lets you by");
				continue;
			}
			bool flag2 = Tempers.Of(text2).StaresBack && num2 < 60f;
			bool flag3 = (num2 >= 60f || text2 == "Coward") && value.Like <= value.Fear;
			string text3 = value.Witnessed?.LastOrDefault((string w) => w.StartsWith("saw ")) ?? value.Witnessed?.LastOrDefault() ?? "knows what you did";
			string text5;
			if (flag && num2 < 75f)
			{
				TASK.TASK_LOOK_AT_ENTITY(h, player.Handle, flag2 ? 4000 : 1500, 0, 51, 0);
				if (flag2)
				{
					string text4 = ((text2 == "Hothead") ? "I saw what you did. I ain't scared of you." : ((text2 == "Proud") ? "I know who you are. So does everyone else." : "...I remember you."));
					ShowSubtitle(value.Name + ": " + text4);
					text5 = "stares you down from where he is";
				}
				else
				{
					text5 = "keeps to what he's doing - a look";
				}
				GrudgeLog($"RECOGNISED {value.Name} ({text2}, fear {value.Fear}) - {text3} - {text5} [{text}]");
				Feed($"RECOGNISED {value.Name} ({text2.ToLowerInvariant()}, fear {value.Fear}): {text3} - {text5}", (150, 170, 210));
				continue;
			}
			if (flag)
			{
				_toldToFlee[h] = Game.GameTime;
				TASK.CLEAR_PED_TASKS(h, p1: false, p2: false);
				_pendingExits.RemoveAll((PendingExit x) => x.P != null && x.P.Handle == h);
				_pendingExits.Add(new PendingExit
				{
					P = ped,
					Player = player.Handle,
					How = "runs",
					From = Game.GameTime + 1200,
					Deadline = Game.GameTime + 3000
				});
				text5 = "terrified - gets up and runs";
				GrudgeLog($"RECOGNISED {value.Name} ({text2}, fear {value.Fear}) - {text3} - {text5} [{text}]");
				Feed($"RECOGNISED {value.Name} ({text2.ToLowerInvariant()}, fear {value.Fear}): {text3} - {text5}", (150, 170, 210));
				continue;
			}
			if (!flag2 && !SawFirstHand(value))
			{
				Leave(ped, player.Handle, "walks off");
				_calmlyLeft[h] = Game.GameTime;
				text5 = "walks off calmly (he only heard of you)";
			}
			else if (flag2)
			{
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(h, player.Handle, 3000, 0f, 0f, 0f);
				TASK.TASK_LOOK_AT_ENTITY(h, player.Handle, 6000, 0, 51, 0);
				string text6 = ((text2 == "Hothead") ? "I saw what you did. I ain't scared of you." : ((text2 == "Proud") ? "I know who you are. So does everyone else." : "...I remember you."));
				ShowSubtitle(value.Name + ": " + text6);
				text5 = "stares you down";
			}
			else if (flag3 && num < 6f)
			{
				TASK.TASK_COWER(h, 6000, player.Handle, "");
				Say(ped, "GENERIC_SHOCKED_HIGH", player);
				ShowSubtitle(value.Name + ": Please - I didn't see nothing!");
				text5 = "cowers";
			}
			else if (value.Like > value.Fear)
			{
				Leave(ped, player.Handle, "walks off, glancing back");
				text5 = "walks off, glancing back (he likes you)";
			}
			else
			{
				text5 = GoAway(ped, player, text2, num2);
				if (text5 == "runs" && flag3)
				{
					ShowSubtitle(value.Name + ": It's him!");
				}
			}
			GrudgeLog($"RECOGNISED {value.Name} ({text2}, fear {value.Fear}) - {text3} - {text5} [{text}]");
			Feed($"RECOGNISED {value.Name} ({text2.ToLowerInvariant()}, fear {value.Fear}): {text3} - {text5}", (150, 170, 210));
		}
	}

	private void UpdateWitnessReports(Ped player)
	{
		try
		{
			uint num = LAW._GET_HUD_PLAYER_CRIME_TYPE(Game.Player.Handle);
			if (num != 0)
			{
				_lastCrimeType = num;
				_lastCrimeAt = Game.GameTime;
			}
		}
		catch
		{
		}
		if (Game.GameTime < _nextReportCheck)
		{
			return;
		}
		_nextReportCheck = Game.GameTime + 2000;
		int bountyDollars = BountyDollars;
		if (_lastBounty < 0)
		{
			_lastBounty = bountyDollars;
			return;
		}
		if (bountyDollars > _lastBounty)
		{
			Vector3 me = player.Position;
			List<(int handle, Acquaintance a, int at, string crime)> list = _recentWitnesses.Where(((int handle, Acquaintance a, int at, string crime) x) => !x.a.Dead && !x.a.KeptQuiet && !x.a.Reported && Game.GameTime - x.at >= 1000 && ENTITY.DOES_ENTITY_EXIST(x.handle) && !ENTITY.IS_ENTITY_DEAD(x.handle) && !OutlawMan(x.handle)).ToList();
			int latest = ((list.Count > 0) ? list.Max<(int, Acquaintance, int, string)>(((int handle, Acquaintance a, int at, string crime) x) => x.at) : 0);
			(int, Acquaintance, int, string) tuple = (from x in list
				where latest - x.at < 1500
				orderby new Ped(x.handle).Position.DistanceTo(me) descending
				select x).FirstOrDefault();
			if (tuple.Item2 == null)
			{
				string text = CrimeSeen() ?? "?";
				int num2 = Game.GameTime - _modBoomAt;
				string text2 = ((num2 >= 0 && num2 < 8000) ? $" - A MOD EXPLOSION {(double)num2 / 1000.0:0.0} s before, {_modBoomPos.DistanceTo(me):0} m off" : "");
				GrudgeLog($"REPORT bounty ${_lastBounty} -> ${bountyDollars}: no witness had gone to tell it (the law saw it, or it's the game's own) - crime {text}{text2}");
			}
			if (tuple.Item2 != null)
			{
				tuple.Item2.Reported = true;
				AddSeen(tuple.Item2, "told the law about you");
				GrudgeLog(string.Format("REPORTED {0} told the law (bounty ${1} -> ${2}) - {3}, {4} s before - the game's charge: {5}", tuple.Item2.Name, _lastBounty, bountyDollars, tuple.Item4, (Game.GameTime - tuple.Item3) / 1000, CrimeSeen() ?? "?"));
				Feed($"REPORTED {tuple.Item2.Name} told the law about you (bounty ${_lastBounty} to ${bountyDollars})", Theme.Warn);
			}
		}
		_lastBounty = bountyDollars;
	}

	private void WitnessIntimidated(int handle)
	{
		if (_acqOf.TryGetValue(handle, out var value) && (value.Fear > 0 || (value.Witnessed != null && value.Witnessed.Count != 0)))
		{
			NemesisRecord value2;
			float num = (_witnessShadow.TryGetValue(handle, out value2) ? value2.FearGainMult : 1f);
			int fear = value.Fear;
			value.Fear = Math.Min(100, value.Fear + Math.Max(1, (int)Math.Round(15f * num)));
			if (!value.Reported)
			{
				value.KeptQuiet = true;
			}
			AddSeen(value, value.Reported ? "you threatened him (too late - he told the law)" : "you scared him into keeping quiet");
			value.LastScaredMin = GameMinutesNow();
			GrudgeLog(string.Format("KEPT QUIET {0}: fear {1} -> {2}{3}", value.Name, fear, value.Fear, value.Reported ? " (he had already reported)" : ""));
			Feed(string.Format("KEPT QUIET {0}: fear {1} to {2}{3}", value.Name, fear, value.Fear, value.Reported ? " - too late, he told the law" : " - he won't talk"), (150, 170, 210));
		}
	}

	private void WitnessSilenced(Ped dead)
	{
		if (_acqOf.TryGetValue(dead.Handle, out var value) && value.Witnessed != null && value.Witnessed.Count != 0 && !OutlawMan(dead.Handle))
		{
			bool flag = _recentWitnesses.Any(((int handle, Acquaintance a, int at, string crime) w) => w.handle == dead.Handle);
			GrudgeLog("SILENCED " + value.Name + (flag ? " (right after he saw you)" : "") + (value.Reported ? " - he had already told the law" : ""));
			Feed("SILENCED " + value.Name + (value.Reported ? " - too late, he'd told the law" : ""), Theme.Blood);
		}
	}

	private void AddSeen(Acquaintance a, string what)
	{
		(a.Witnessed ?? (a.Witnessed = new List<string>())).Add(what + " - " + GameDateShort());
		TrimWitnessed(a);
		_peopleDirty = true;
	}

	internal float TownWord(string town)
	{
		if (string.IsNullOrEmpty(town))
		{
			return 0f;
		}
		if (_townWordCache.TryGetValue(town, out (float, int) value) && UiDraw.RealMs - value.Item2 < 1000)
		{
			return value.Item1;
		}
		float num = TownWordNow(town);
		_townWordCache[town] = (num, UiDraw.RealMs);
		return num;
	}

	private float TownWordNow(string town)
	{
		float num = People.Where((Acquaintance x) => !x.Dead && x.Town == town && x.Fear > 0 && SawFirstHand(x)).Sum((Func<Acquaintance, float>)((Acquaintance x) => x.Fear)) * 0.3f;
		TownTalk value;
		float num2 = ((Chronicle.TownWord != null && Chronicle.TownWord.TryGetValue(town, out value)) ? value.Word : 0f);
		Vector3 centre;
		float radius;
		float num3 = (TownCore(town, out centre, out radius) ? Math.Max(1f, radius / 130f * (radius / 130f)) : 1f);
		return 100f * (1f - (float)Math.Exp((0f - (num + num2)) / (100f * num3)));
	}

	internal float NewcomerWord(string town)
	{
		float num = TownWord(town);
		if (string.IsNullOrEmpty(town) || num <= 0f)
		{
			return num;
		}
		TownTalk value;
		float num2 = ((Chronicle.TownWord != null && Chronicle.TownWord.TryGetValue(town, out value)) ? value.Good : 0f);
		float num3 = 0f;
		try
		{
			num3 = TownRep(town).Goodwill;
		}
		catch
		{
		}
		return Math.Max(0f, num - num2 - num3 / 2f);
	}

	private static bool SawFirstHand(Acquaintance a)
	{
		if (a.Witnessed != null)
		{
			return a.Witnessed.Any((string w) => w.StartsWith("saw you"));
		}
		return false;
	}

	private void WaryFromWord(Acquaintance a, string temper, Ped who = null)
	{
		if (_rememberingWitness || (who != null && HardMan(who)))
		{
			return;
		}
		float num = NewcomerWord(a.Town);
		if (num < 20f || a.Fear > 0)
		{
			return;
		}
		int num2 = HeardChance(num);
		if (_rng.Next(100) >= num2)
		{
			GrudgeLog($"PEOPLE {a.Name} hasn't heard about you ({num2}% at infamy {num:0})");
			return;
		}
		a.Fear = StartFear(num, temper);
		if (a.Fear > 0)
		{
			if (string.IsNullOrEmpty(a.Temper) && !string.IsNullOrEmpty(temper))
			{
				a.Temper = temper;
			}
			AddSeen(a, "heard what you did in " + a.Town);
		}
	}

	private static float TemperFearGain(string temper)
	{
		return Tempers.Of(temper).FearGain;
	}

	private WorldEvent SimWord(long now)
	{
		DateTime d = FromGameMinutes(now);
		int today = GameDate.DayKey(d);
		TownTalk value2;
		List<Acquaintance> list = People.Where((Acquaintance x) => !x.Dead && !x.KeptQuiet && x.Fear >= 50 && !string.IsNullOrEmpty(x.Town) && SawFirstHand(x) && (Chronicle.TownWord == null || !Chronicle.TownWord.TryGetValue(x.Town, out value2) || value2.LastWordDay != today)).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		double num = ((IEnumerable<Acquaintance>)list).Sum((Func<Acquaintance, double>)((Acquaintance x) => x.Fear));
		double num2 = _rng.NextDouble() * num;
		Acquaintance a = list.Last();
		foreach (Acquaintance item in list)
		{
			num2 -= (double)item.Fear;
			if (num2 <= 0.0)
			{
				a = item;
				break;
			}
		}
		WorldEvent worldEvent = NewEvent("WORD", now);
		worldEvent.Gangs.Add("");
		worldEvent.Place = "in " + a.Town;
		bool flag = a.Model != 0 && ModelName(a.Model).Contains("_f_");
		worldEvent.Title = a.Name + " told " + a.Town + " what " + (flag ? "she" : "he") + " saw you do";
		string text = a.Witnessed?.LastOrDefault((string w) => w.StartsWith("saw ")) ?? a.Witnessed?.LastOrDefault() ?? "what you are";
		worldEvent.Lines.Add(a.Name + ": " + text);
		Dictionary<string, TownTalk> dictionary = Chronicle.TownWord ?? (Chronicle.TownWord = new Dictionary<string, TownTalk>());
		if (!dictionary.TryGetValue(a.Town, out var value))
		{
			value = (dictionary[a.Town] = new TownTalk());
		}
		float num3 = TownWord(a.Town);
		value.Word = Math.Min(100f, value.Word + (float)a.Fear / 3f * (1f - value.Word / 100f));
		value.LastWordDay = today;
		_townWordCache.Remove(a.Town);
		_townRepCache.Remove(a.Town);
		int num4 = 0;
		foreach (Acquaintance item2 in (from _ in People
			where _ != a && !_.Dead && _.Town == a.Town
			orderby _rng.Next()
			select _).Take(3 + _rng.Next(6)))
		{
			int num5 = Math.Max(1, (int)Math.Round((float)(3 + _rng.Next(5)) * TemperFearGain(item2.Temper)));
			if (item2.Fear < 40)
			{
				item2.Fear = Math.Min(40, item2.Fear + num5);
			}
			AddSeen(item2, "heard from " + a.Name + " what you did");
			num4++;
		}
		worldEvent.Lines.Add($"{num4} people you know in {a.Town} heard it - your infamy there {num3:0} to {TownWord(a.Town):0}");
		_peopleDirty = true;
		RepNote($"WORD {a.Name} told {a.Town}: {num4} you know heard it, infamy {num3:0} to {TownWord(a.Town):0}");
		GrudgeLog($"WORD {a.Name} (fear {a.Fear}) told {a.Town}: {num4} you know heard it, infamy {num3:0} -> {TownWord(a.Town):0}");
		return worldEvent;
	}

	private void FadeWitnessFear()
	{
		int num = GameDayKey();
		WorldState chronicle = Chronicle;
		if (chronicle.LastFearFadeDay == 0)
		{
			chronicle.LastFearFadeDay = num;
		}
		else
		{
			if (num == chronicle.LastFearFadeDay)
			{
				return;
			}
			try
			{
				if (DayOf(num) < DayOf(chronicle.LastFearFadeDay))
				{
					chronicle.LastFearFadeDay = num;
					return;
				}
			}
			catch
			{
			}
			int num2 = 1;
			try
			{
				num2 = Math.Max(1, Math.Min(30, (int)(DayOf(num) - DayOf(chronicle.LastFearFadeDay)).TotalDays));
			}
			catch
			{
			}
			chronicle.LastFearFadeDay = num;
			int num3 = 0;
			long num4 = GameMinutesNow();
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (Acquaintance item in People.Where((Acquaintance x) => x.Fear > 0))
			{
				if (item.LastScaredMin > 0 && num4 - item.LastScaredMin < 1440)
				{
					continue;
				}
				TemperProfile temperProfile = Tempers.Of(item.Temper);
				float num5 = (item.KeptQuiet ? 1f : temperProfile.FadePerDay);
				int value = 0;
				if (!string.IsNullOrEmpty(item.Town) && !dictionary.TryGetValue(item.Town, out value))
				{
					try
					{
						value = TownReputation(item.Town);
					}
					catch
					{
					}
					dictionary[item.Town] = value;
				}
				if (value > 0 && !item.KeptQuiet)
				{
					num5 += (float)(value / 20);
				}
				int fear = item.Fear;
				for (int num6 = 0; num6 < num2; num6++)
				{
					if (item.Fear <= 0)
					{
						break;
					}
					item.Fear = Math.Max(0, item.Fear - (int)Math.Ceiling(Math.Max(num5, (float)item.Fear * 0.08f * (num5 / 3f))));
				}
				if (item.Fear != fear)
				{
					num3++;
				}
			}
			try
			{
				HealHate(num2, num4);
			}
			catch (Exception ex)
			{
				GrudgeLog("HEAL failed: " + ex.Message);
			}
			int num7 = 0;
			foreach (Acquaintance item2 in People.Where((Acquaintance x) => x.Grudge > 0 && x.GrudgeSettled && !x.Dead))
			{
				if (!((DateTime.Now - item2.LastDealt).TotalMinutes < 30.0))
				{
					int grudge = item2.Grudge;
					item2.Grudge = Math.Max(0, item2.Grudge - Math.Max(1, (int)Math.Round(Tempers.Of(item2.Temper).HealPerDay * 1.5f * (float)num2)));
					if (item2.Grudge != grudge)
					{
						num7++;
					}
				}
			}
			if (num7 > 0)
			{
				_peopleDirty = true;
				GrudgeLog(string.Format("HEAL {0} strangers' let-go grudges eased ({1} day{2})", num7, num2, (num2 == 1) ? "" : "s"));
			}
			if (chronicle.TownWord != null)
			{
				foreach (TownTalk value2 in chronicle.TownWord.Values)
				{
					value2.Word *= (float)Math.Pow(0.85, num2);
					value2.Good *= (float)Math.Pow(0.9, num2);
				}
			}
			if (num3 > 0)
			{
				_peopleDirty = true;
				GrudgeLog(string.Format("WITNESS fear faded for {0} people ({1} day{2})", num3, num2, (num2 == 1) ? "" : "s"));
			}
			SaveWorld();
		}
	}

	private void HealHate(int days, long nowMin)
	{
		int num = 0;
		List<string> list = new List<string>();
		foreach (NemesisRecord item in Roster.Where((NemesisRecord x) => x.IsAlive && !x.PresumedDead && x.Gang == null && !Gangs.Orders(x)).ToList())
		{
			if (item == ActiveRecord || _worldNemeses.ContainsValue(item) || (item.LastGameMin > 0 && nowMin - item.LastGameMin < 1440) || (item.Goals != null && item.Goals.Any((Goal g) => g.Type == "Avenge" && g.Target == "you")))
			{
				continue;
			}
			float num2 = Tempers.Of(item.Personality).HealPerDay;
			if (item.Memories != null && item.Memories.Any((Memory m) => m.LongTerm && m.Cause == "you" && (m.Emotion == "Anger" || m.Emotion == "Shame")))
			{
				num2 *= 0.5f;
			}
			int num3 = SocialFloor(item);
			int hate = item.Hate;
			if (hate <= num3)
			{
				continue;
			}
			int num4 = Math.Max(1, (int)Math.Round(num2 * (float)days));
			item.Hate = Math.Max(num3, hate - num4);
			if (item.Hate != hate)
			{
				num++;
				if (list.Count < 6)
				{
					list.Add($"{item.Name} {hate}->{item.Hate}");
				}
			}
		}
		if (num > 0)
		{
			SaveRoster();
			GrudgeLog(string.Format("HEAL time heals ({0} day{1}): {2} loners cooled - {3}", days, (days == 1) ? "" : "s", num, string.Join(", ", list)));
		}
	}

	internal string TownWordDebugLine()
	{
		string town = _currentTown;
		IEnumerable<string> first = (from x in People
			where x.Fear > 0 && x.Town != null
			select x.Town).Distinct();
		IEnumerable<string> enumerable = Chronicle.TownWord?.Keys;
		List<(string, float)> list = (from t in first.Concat(enumerable ?? Enumerable.Empty<string>()).Distinct()
			select (t: t, w: TownWord(t)) into x
			where x.w >= 1f
			orderby x.w descending
			select x).Take(5).ToList();
		return "infamy by town: " + ((list.Count == 0) ? "nobody talks about you yet" : string.Join(", ", list.Select<(string, float), string>(((string t, float w) x) => string.Format("{0} {1:0}{2}", x.t, x.w, (x.t == town) ? " (here)" : "")))) + $"  -  reported {People.Count((Acquaintance x) => x.Reported)}, kept quiet {People.Count((Acquaintance x) => x.KeptQuiet)}";
	}

	internal static void TrimWitnessed(Acquaintance a)
	{
		while (a.Witnessed != null && a.Witnessed.Count > 6)
		{
			int num = a.Witnessed.FindIndex((string w) => !w.StartsWith("saw "));
			a.Witnessed.RemoveAt((num >= 0) ? num : 0);
		}
	}

	private int AddFightBlip(Vector3 at, string name)
	{
		Crumbs.Native("fight blip add");
		int num = MAP.BLIP_ADD_FOR_COORDS(1291203949u, at.X, at.Y, at.Z);
		if (num != 0)
		{
			Function.Call(11290983183885202434uL, num, new InputArgument(UiDraw.ForeverVarString(name)));
		}
		return num;
	}
}
