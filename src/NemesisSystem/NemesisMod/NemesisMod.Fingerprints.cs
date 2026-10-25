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
	private sealed class Print
	{
		public string Fp { get; set; }

		public string Look { get; set; }

		public string Name { get; set; }

		public string Title { get; set; }

		public string Model { get; set; }

		public string Town { get; set; }

		public int Pieces { get; set; }

		public int Seen { get; set; }

		public bool Met { get; set; }

		public bool Dead { get; set; }

		public int Grudge { get; set; }

		public string Temper { get; set; }

		public string AcqId { get; set; }

		public string NemesisId { get; set; }

		public string Affil { get; set; }

		public long LastGameMin { get; set; }

		public DateTime First { get; set; }

		public DateTime Last { get; set; }

		public int Handle { get; set; }
	}

	private sealed class Gesture
	{
		public Ped A;

		public Ped B;

		public string Dict;

		public string ClipA;

		public string ClipB;

		public string What;

		public int Since;

		public int WalkSince;

		public bool Shake;

		public bool Requested;

		public bool YouWalk;

		public float StartGap;

		public bool Money;

		public bool YouGive;

		public bool OrigBlock;

		public bool Swapped;

		public bool Checked;

		public bool Upper;

		public int Prop;

		public int Started;

		public int DurMs;
	}

	private const ulong CategoryAtIndex = 11209604758464182439uL;

	private Dictionary<string, Print> _prints;

	private readonly Dictionary<int, string> _fpOf = new Dictionary<int, string>();

	private HashSet<string> _keptNames;

	private bool _printsDirty;

	private int _nextPrintsSave;

	private int _lookLinked;

	private int _lookTwins;

	private int _lookBlocked;

	private int _lookNew;

	private int _lookEmpty;

	private const ulong PedReadyToRender = 11582290322814659388uL;

	private readonly HashSet<uint> _presetsLogged = new HashSet<uint>();

	private const ulong NumMetaOutfits = 1208946369520219911uL;

	private readonly Dictionary<int, int> _antagAt = new Dictionary<int, int>();

	private string PrintsPath => Path.Combine(_saveDir, "fingerprints2.json");

	private string PrintsLog => Path.Combine(_saveDir, "fingerprints.log");

	private Dictionary<string, Print> Prints
	{
		get
		{
			if (_prints != null)
			{
				return _prints;
			}
			_prints = SafeFile.Read<Dictionary<string, Print>>(PrintsPath, GrudgeLog) ?? new Dictionary<string, Print>();
			long lastGameMin = GameMinutesNow();
			foreach (Print value in _prints.Values)
			{
				if (value.LastGameMin <= 0)
				{
					value.LastGameMin = lastGameMin;
				}
			}
			_keptNames = new HashSet<string>(from x in _prints.Values.Where(Kept)
				select x.Name);
			GrudgeLog($"LOOKS loaded {_prints.Count} looks ({_keptNames.Count} of people you know)");
			return _prints;
		}
	}

	private static bool Kept(Print pr)
	{
		if (!pr.Met && pr.Grudge <= 0 && pr.AcqId == null)
		{
			return pr.NemesisId != null;
		}
		return true;
	}

	private bool NameKept(string name)
	{
		_ = Prints;
		return _keptNames.Contains(name);
	}

	private static uint Fnv(IEnumerable<string> parts)
	{
		uint num = 2166136261u;
		foreach (string part in parts)
		{
			foreach (char c in part)
			{
				num ^= c;
				num *= 16777619;
			}
			num ^= 0x7C;
			num *= 16777619;
		}
		return num;
	}

	private bool LookReadable(Ped p)
	{
		if (p != null && p.Exists() && !p.IsDead && Game.GameTime >= _quietUntil + 5000)
		{
			return Function.Call<bool>(11582290322814659388uL, new InputArgument[1] { p.Handle });
		}
		return false;
	}

	private unsafe (string full, string look, int pieces, int n) Fingerprint(Ped p)
	{
		if (!LookReadable(p))
		{
			return (full: "", look: "", pieces: 0, n: 0);
		}
		Crumbs.Mark($"look read ped {p.Handle} model 0x{(uint)p.Model.Hash:X8}");
		int num = Function.Call<int>(10394376663872179841uL, new InputArgument[1] { p.Handle });
		ulong* ptr = stackalloc ulong[4];
		ulong* ptr2 = stackalloc ulong[4];
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		for (int i = 0; i < num && i < 64; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				ptr[j] = 0uL;
				ptr2[j] = 0uL;
			}
			Crumbs.Mark($"look read ped {p.Handle} piece {i}/{num}");
			if (Function.Call<bool>(12232485870226939222uL, new InputArgument[6]
			{
				p.Handle,
				i,
				new InputArgument((ulong)ptr),
				new InputArgument((ulong)(ptr + 1)),
				new InputArgument((ulong)(ptr + 2)),
				new InputArgument((ulong)(ptr + 3))
			}))
			{
				Function.Call<bool>(16688528139376343998uL, new InputArgument[6]
				{
					p.Handle,
					i,
					new InputArgument((ulong)ptr2),
					new InputArgument((ulong)(ptr2 + 1)),
					new InputArgument((ulong)(ptr2 + 2)),
					new InputArgument((ulong)(ptr2 + 3))
				});
				uint num2 = 0u;
				try
				{
					num2 = Function.Call<uint>(11209604758464182439uL, new InputArgument[3] { p.Handle, i, 0 });
				}
				catch
				{
				}
				uint num3 = (uint)(*ptr);
				uint num4 = (uint)ptr[1];
				if (num3 != 0 || num4 != 0)
				{
					string text = $"{num2:X8}:{num3:X8}/{num4:X8}";
					list2.Add(text);
					list.Add($"{text} {(uint)(*ptr2):X8}.{(int)ptr2[1]}.{(int)ptr2[2]}.{(int)ptr2[3]}");
				}
			}
		}
		list.Sort(StringComparer.Ordinal);
		list2.Sort(StringComparer.Ordinal);
		uint hash = (uint)p.Model.Hash;
		return (full: $"{hash:X8}-{Fnv(list):X8}", look: $"{hash:X8}-{Fnv(list2):X8}", pieces: list.Count, n: num);
	}

	private string LinkBlock(Print pr, int handle)
	{
		if (pr.Dead)
		{
			return "he died";
		}
		if (pr.NemesisId != null)
		{
			if (FindRecord(pr.NemesisId) == null)
			{
				return "he was a nemesis";
			}
			return "he's a nemesis";
		}
		if (pr.AcqId != null)
		{
			Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == pr.AcqId);
			if (acquaintance != null && acquaintance.Dead)
			{
				return "he died";
			}
		}
		if (pr.Handle != 0 && pr.Handle != handle && ENTITY.DOES_ENTITY_EXIST(pr.Handle) && !ENTITY.IS_ENTITY_DEAD(pr.Handle) && _named.ContainsKey(pr.Handle))
		{
			return "twin";
		}
		if (_named.Any((KeyValuePair<int, Named> kv) => kv.Key != handle && kv.Value.Name == pr.Name && ENTITY.DOES_ENTITY_EXIST(kv.Key)))
		{
			return "his name is on someone else here";
		}
		if (!Kept(pr) && Math.Abs(GameMinutesNow() - pr.LastGameMin) > (long)Config.LookMemoryDays * 1440L)
		{
			return "expired";
		}
		return null;
	}

	private Named NameByLook(Ped p)
	{
		if (LookReadable(p) && _presetsLogged.Add((uint)p.Model.Hash))
		{
			int num = -1;
			try
			{
				num = Function.Call<int>(1208946369520219911uL, new InputArgument[1] { p.Handle });
			}
			catch
			{
			}
			LookLog($"MODEL {ModelName((uint)p.Model.Hash)}: {num} outfit presets in the game");
		}
		string text = Models.Name(p);
		string model = ((text != null && text != "") ? text : $"0x{(uint)p.Model.Hash:X8}");
		string text2 = _currentTown ?? TownAt(p.Position) ?? GetZoneName(p.Position);
		if (Game.GameTime - _loadedAt < 180000)
		{
			uint mh = (uint)p.Model.Hash;
			NemesisRecord was = Roster.FirstOrDefault((NemesisRecord r) => r.LastHandle == p.Handle && r.LastHandleModel == mh && r.IsAlive && !r.PresumedDead && r.LastHandleUtc.HasValue && (DateTime.UtcNow - r.LastHandleUtc.Value).TotalMinutes < 30.0);
			if (was != null && !_worldNemeses.Any((KeyValuePair<int, NemesisRecord> kv) => kv.Key != p.Handle && kv.Value == was && ENTITY.DOES_ENTITY_EXIST(kv.Key)))
			{
				_worldNemeses[p.Handle] = was;
				NoteModel(p);
				was.LastHandle = 0;
				LookLog(was.Name + " | " + model + " | " + text2 + " | SAME NEMESIS after a reload (same ped) - back on his record");
				GrudgeLog("NAMING " + was.Name + " is the same ped as before the reload - keeps his name");
				return new Named
				{
					Name = was.Name,
					Title = (was.Title ?? ""),
					Met = true,
					Importance = 3,
					SeenAt = Game.GameTime,
					NamedAt = Game.GameTime
				};
			}
		}
		string text3 = null;
		string look = null;
		string text4 = null;
		int num2 = 0;
		int num3 = 0;
		Print pr = null;
		if (Config.LinkLooks)
		{
			try
			{
				(text3, look, num2, num3) = Fingerprint(p);
			}
			catch (Exception ex)
			{
				GrudgeLog("LOOKS read failed: " + ex.Message);
			}
			if (num2 == 0)
			{
				text3 = null;
				_lookEmpty++;
			}
			else
			{
				_fpOf[p.Handle] = text3;
				Print print = Prints.Values.FirstOrDefault((Print x) => x.Handle == p.Handle && x.Model == model && !x.Dead && (DateTime.Now - x.Last).TotalMinutes < 15.0 && Game.GameTime - _loadedAt < 60000);
				if (print != null && print.Fp != text3)
				{
					pr = print;
					text4 = LinkBlock(pr, p.Handle);
					if (text4 == null)
					{
						LookLog(pr.Name + " | " + model + " | " + text2 + " | SAME MAN after a reload (his look changed: " + pr.Fp + " -> " + text3 + ") - keeps his name");
					}
				}
				else if (Prints.TryGetValue(text3, out pr))
				{
					text4 = LinkBlock(pr, p.Handle);
				}
			}
		}
		if (pr != null && text4 == null)
		{
			TimeSpan t = DateTime.Now - pr.Last;
			Named named = new Named
			{
				Name = pr.Name,
				Title = (pr.Title ?? ""),
				Met = pr.Met,
				Importance = (Kept(pr) ? 3 : 0),
				SeenAt = Game.GameTime,
				NamedAt = Game.GameTime,
				Affil = pr.Affil
			};
			pr.Seen++;
			pr.Last = DateTime.Now;
			pr.LastGameMin = GameMinutesNow();
			pr.Handle = p.Handle;
			pr.Town = text2;
			_lookLinked++;
			_printsDirty = true;
			Acquaintance acquaintance = ((pr.AcqId != null) ? People.FirstOrDefault((Acquaintance x) => x.Id == pr.AcqId) : null);
			if (acquaintance != null && !acquaintance.Dead)
			{
				_acqOf[p.Handle] = acquaintance;
				acquaintance.LastSeen = DateTime.Now;
				acquaintance.LastGameMin = GameMinutesNow();
				acquaintance.TimesSeen++;
				_peopleSoft = true;
				_named[p.Handle] = named;
				if (acquaintance.Grudge > 0)
				{
					RestoreGrudge(p.Handle, acquaintance);
				}
			}
			LookLog(string.Format("{0} | {1} | {2} | LINKED (seen {3}x, last {4} ago{5}{6}) | {7}", pr.Name, model, text2, pr.Seen, Ago(t), pr.Met ? ", you've met" : "", (acquaintance != null && acquaintance.Grudge > 0) ? $", grudge {acquaintance.Grudge}" : "", text3));
			if (pr.Met || (acquaintance != null && acquaintance.Grudge > 0))
			{
				Feed("KNOWN " + pr.Name + " - seen " + (PED.IS_PED_MALE(p.Handle) ? "him" : "her") + " before (" + Ago(t) + " ago" + ((acquaintance != null && acquaintance.Grudge > 0) ? $", still sore: grudge {acquaintance.Grudge}" : "") + ")", Theme.Dim);
			}
			return named;
		}
		if (pr != null && text4 == "he's a nemesis")
		{
			NemesisRecord rec = FindRecord(pr.NemesisId);
			bool flag = rec != null && _worldNemeses.Any((KeyValuePair<int, NemesisRecord> kv) => kv.Key != p.Handle && kv.Value == rec && ENTITY.DOES_ENTITY_EXIST(kv.Key) && !ENTITY.IS_ENTITY_DEAD(kv.Key));
			int num4;
			if (rec != null)
			{
				Gang gang = Gangs.ById(rec.Gang);
				num4 = (((gang != null && gang.UniformKit) || rec.Gang == "lemoyneraiders") ? 1 : 0);
			}
			else
			{
				num4 = 0;
			}
			bool flag2 = (byte)num4 != 0;
			bool flag3 = pr.Handle == p.Handle && Game.GameTime - _loadedAt < 60000 && (DateTime.Now - pr.Last).TotalMinutes < 15.0;
			if (rec != null && !flag3)
			{
				LookLog(rec.Name + " | " + model + " | " + text2 + " | NOT " + rec.Name + ": only looks like him (not the same ped right after a reload)" + (flag2 ? ", and they wear a uniform" : ""));
			}
			if (rec != null && rec.IsAlive && !rec.PresumedDead && !flag && flag3 && !flag2)
			{
				_worldNemeses[p.Handle] = rec;
				NoteModel(p);
				pr.Seen++;
				pr.Last = DateTime.Now;
				pr.LastGameMin = GameMinutesNow();
				pr.Handle = p.Handle;
				pr.Town = text2;
				_printsDirty = true;
				LookLog(rec.Name + " | " + model + " | " + text2 + " | SAME NEMESIS - back on his record | " + text3);
				return new Named
				{
					Name = rec.Name,
					Title = (rec.Title ?? ""),
					Met = true,
					Importance = 3,
					SeenAt = Game.GameTime,
					NamedAt = Game.GameTime
				};
			}
		}
		(string, string) tuple2 = FreshName((uint)p.Model.Hash, !PED.IS_PED_MALE(p.Handle));
		string item = tuple2.Item1;
		string item2 = tuple2.Item2;
		Named named2 = new Named
		{
			Name = item,
			Title = item2,
			SeenAt = Game.GameTime,
			NamedAt = Game.GameTime,
			Affil = RollAffiliation(p)
		};
		if (text3 != null)
		{
			if (pr == null || text4 == "expired")
			{
				Prints[text3] = new Print
				{
					Fp = text3,
					Look = look,
					Name = item,
					Title = item2,
					Model = model,
					Town = text2,
					Pieces = num2,
					Seen = 1,
					First = DateTime.Now,
					Last = DateTime.Now,
					LastGameMin = GameMinutesNow(),
					Handle = p.Handle,
					Affil = named2.Affil
				};
				_lookNew++;
				_printsDirty = true;
				LookLog(string.Format("{0} | {1} | {2} | {3} | {4} | {5}/{6} pieces", item, model, text2, (pr == null) ? "NEW" : ("NEW (the look of " + pr.Name + " expired)"), text3, num2, num3));
			}
			else
			{
				if (text4 == "twin")
				{
					_lookTwins++;
				}
				else
				{
					_lookBlocked++;
				}
				LookLog(item + " | " + model + " | " + text2 + " | NOT " + pr.Name + ": " + text4 + " | " + text3);
			}
		}
		return named2;
	}

	private static string Ago(TimeSpan t)
	{
		if (!(t.TotalMinutes < 1.0))
		{
			if (!(t.TotalHours < 1.0))
			{
				if (!(t.TotalDays < 1.0))
				{
					return $"{t.TotalDays:0.0} days";
				}
				return $"{t.TotalHours:0.0} h";
			}
			return $"{t.TotalMinutes:0} min";
		}
		return $"{t.TotalSeconds:0} s";
	}

	private void LookLog(string text)
	{
		if (!Config.Verbose)
		{
			return;
		}
		try
		{
			SafeFile.Append(PrintsLog, $"{DateTime.Now:HH:mm:ss} | {text}{Environment.NewLine}");
		}
		catch
		{
		}
	}

	private void LookSync(Ped player)
	{
		if (!Config.LinkLooks)
		{
			return;
		}
		Vector3 position = player.Position;
		long num = GameMinutesNow();
		int num2 = 0;
		foreach (KeyValuePair<int, Named> item in _named.ToList())
		{
			int key = item.Key;
			Named value = item.Value;
			if (value.Story || _fpOf.ContainsKey(key) || num2 >= 6 || !ENTITY.DOES_ENTITY_EXIST(key) || !ENTITY.IS_ENTITY_A_PED(key) || ENTITY.IS_ENTITY_DEAD(key))
			{
				continue;
			}
			Ped ped = new Ped(key);
			if (!(ped.Position.DistanceTo(position) > Config.NamingRadius))
			{
				num2++;
				(string, string, int, int) tuple;
				try
				{
					tuple = Fingerprint(ped);
				}
				catch
				{
					_fpOf[key] = "";
					continue;
				}
				Dictionary<int, string> fpOf = _fpOf;
				object value2;
				if (tuple.Item3 > 0)
				{
					(value2, _, _, _) = tuple;
				}
				else
				{
					value2 = "";
				}
				fpOf[key] = (string)value2;
				if (tuple.Item3 != 0 && !Prints.ContainsKey(tuple.Item1))
				{
					Prints[tuple.Item1] = new Print
					{
						Fp = tuple.Item1,
						Look = tuple.Item2,
						Name = value.Name,
						Title = value.Title,
						Pieces = tuple.Item3,
						Seen = 1,
						First = DateTime.Now,
						Last = DateTime.Now,
						LastGameMin = num,
						Handle = key,
						Town = (_currentTown ?? TownAt(ped.Position) ?? GetZoneName(ped.Position)),
						Model = Models.Name((uint)ped.Model.Hash)
					};
					_printsDirty = true;
					LookLog(value.Name + " | " + Prints[tuple.Item1].Model + " | " + Prints[tuple.Item1].Town + " | STORED (named elsewhere) | " + tuple.Item1);
				}
			}
		}
		foreach (int item2 in _worldNemeses.Keys.Concat(from t in _townMen
			where t.Ped != null
			select t.Ped.Handle).Concat((!(_activeNemesisPed != null)) ? new int[0] : new int[1] { _activeNemesisPed.Handle }).Distinct()
			.ToList())
		{
			if (num2 >= 6 || _fpOf.ContainsKey(item2) || !ENTITY.DOES_ENTITY_EXIST(item2) || !ENTITY.IS_ENTITY_A_PED(item2) || ENTITY.IS_ENTITY_DEAD(item2))
			{
				continue;
			}
			NemesisRecord nemesisRecord = RecordAt(item2);
			if (nemesisRecord == null)
			{
				continue;
			}
			Ped ped2 = new Ped(item2);
			if (ped2.Position.DistanceTo(position) > Config.NamingRadius || !LookReadable(ped2))
			{
				continue;
			}
			num2++;
			(string, string, int, int) tuple3;
			try
			{
				tuple3 = Fingerprint(ped2);
			}
			catch
			{
				_fpOf[item2] = "";
				continue;
			}
			Dictionary<int, string> fpOf2 = _fpOf;
			object value3;
			if (tuple3.Item3 > 0)
			{
				(value3, _, _, _) = tuple3;
			}
			else
			{
				value3 = "";
			}
			fpOf2[item2] = (string)value3;
			if (tuple3.Item3 != 0)
			{
				if (!Prints.TryGetValue(tuple3.Item1, out var value4))
				{
					Prints[tuple3.Item1] = new Print
					{
						Fp = tuple3.Item1,
						Look = tuple3.Item2,
						Name = nemesisRecord.Name,
						Title = nemesisRecord.Title,
						Pieces = tuple3.Item3,
						Seen = 1,
						First = DateTime.Now,
						Last = DateTime.Now,
						LastGameMin = num,
						Handle = item2,
						Town = (_currentTown ?? TownAt(ped2.Position) ?? GetZoneName(ped2.Position)),
						Model = Models.Name((uint)ped2.Model.Hash),
						NemesisId = nemesisRecord.Id,
						Met = true
					};
					LookLog(nemesisRecord.Name + " | " + Prints[tuple3.Item1].Model + " | " + Prints[tuple3.Item1].Town + " | STORED (nemesis in the world) | " + tuple3.Item1);
				}
				else if (value4.NemesisId != nemesisRecord.Id)
				{
					value4.NemesisId = nemesisRecord.Id;
					value4.Name = nemesisRecord.Name;
					value4.Handle = item2;
					LookLog(nemesisRecord.Name + " | look now his (nemesis in the world) | " + tuple3.Item1);
				}
				_printsDirty = true;
			}
		}
		foreach (KeyValuePair<int, string> item3 in _fpOf.ToList())
		{
			if (item3.Value == "" || !Prints.TryGetValue(item3.Value, out var value5) || value5.Handle != item3.Key)
			{
				continue;
			}
			int key2 = item3.Key;
			if (!ENTITY.DOES_ENTITY_EXIST(key2))
			{
				continue;
			}
			bool flag = false;
			NemesisRecord nemesisRecord2 = RecordAt(key2);
			if (nemesisRecord2 != null && value5.NemesisId != nemesisRecord2.Id)
			{
				value5.NemesisId = nemesisRecord2.Id;
				value5.Name = nemesisRecord2.Name;
				flag = true;
			}
			if (_named.TryGetValue(key2, out var value6) && value6.Name == value5.Name)
			{
				if (value6.Met && !value5.Met)
				{
					value5.Met = true;
					flag = true;
				}
				if (!string.IsNullOrEmpty(value6.Title) && value6.Title != value5.Title)
				{
					value5.Title = value6.Title;
					flag = true;
				}
			}
			if (_acqOf.TryGetValue(key2, out var value7) && (value5.AcqId != value7.Id || value5.Grudge != value7.Grudge || value5.Temper != value7.Temper))
			{
				value5.AcqId = value7.Id;
				value5.Grudge = value7.Grudge;
				value5.Temper = value7.Temper;
				flag = true;
			}
			Ped ped3 = new Ped(key2);
			if (!value5.Dead && ped3.IsDead && !LooksKnockedOut(ped3))
			{
				value5.Dead = true;
				flag = true;
				LookLog(value5.Name + " | died - his look won't come back as him");
			}
			if (!ped3.IsDead && ped3.Position.DistanceTo(position) <= Config.NamingRadius && num - value5.LastGameMin >= 10)
			{
				value5.LastGameMin = num;
				value5.Last = DateTime.Now;
				flag = true;
			}
			if (flag)
			{
				_printsDirty = true;
				if (Kept(value5))
				{
					_keptNames.Add(value5.Name);
				}
			}
		}
	}

	private void SavePrints(bool later = false)
	{
		if (_prints == null || !_printsDirty)
		{
			return;
		}
		try
		{
			long now = GameMinutesNow();
			foreach (string item in (from kv in _prints
				where !Kept(kv.Value) && !kv.Value.Dead && Math.Abs(now - kv.Value.LastGameMin) > (long)Config.LookMemoryDays * 1440L
				select kv.Key).ToList())
			{
				_prints.Remove(item);
			}
			if (_prints.Count > Config.LookCap)
			{
				foreach (string item2 in (from kv in (from kv in _prints
						orderby Kept(kv.Value), kv.Value.LastGameMin
						select kv).Take(_prints.Count - Config.LookCap)
					select kv.Key).ToList())
				{
					_prints.Remove(item2);
				}
			}
			string text = JsonConvert.SerializeObject(_prints, Formatting.None);
			if (later)
			{
				SafeFile.WriteLater(PrintsPath, text, keepBak: true, delegate(Exception err)
				{
					if (err != null)
					{
						_printsDirty = true;
					}
				});
			}
			else if (SafeFile.WaitWrites())
			{
				SafeFile.Write(PrintsPath, text, keepBak: true);
			}
			else
			{
				SafeFile.WriteLater(PrintsPath, text, keepBak: true, delegate(Exception err)
				{
					if (err != null)
					{
						_printsDirty = true;
					}
				});
			}
			_printsDirty = false;
		}
		catch (Exception ex)
		{
			GrudgeLog("LOOKS save failed: " + ex.Message);
		}
	}

	private void RestoreGrudge(int handle, Acquaintance a)
	{
		Provocation provocation = RememberStranger(handle);
		if (provocation == null)
		{
			return;
		}
		provocation.Score = a.Grudge;
		provocation.LastActionAt = Game.GameTime;
		provocation.SawFace = a.SawFace ?? true;
		provocation.Settled = a.GrudgeSettled;
		provocation.SettledScore = provocation.Score;
		if (Enum.TryParse<Personality>(a.Temper, out var result))
		{
			provocation.Temper = result;
			if (provocation.Shadow != null)
			{
				provocation.Shadow.Personality = result;
			}
		}
	}

	internal string FingerprintDebugLine()
	{
		if (Config.LinkLooks)
		{
			return $"looks: {Prints.Count} stored ({_keptNames?.Count ?? 0} people you know) - this session {_lookLinked} recognised, {_lookNew} new, {_lookTwins} twins, {_lookBlocked} blocked, {_lookEmpty} unreadable";
		}
		return "looks: name linking OFF ([People] LinkLooks)";
	}
}
