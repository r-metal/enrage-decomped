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
	private sealed class VoiceProbe
	{
		public Ped Who;

		public string Name;

		public List<(string kind, string line)> Todo;

		public int Next;

		public int Tried;

		public List<string> Played = new List<string>();

		public bool Repaired;

		public int Search = -1;

		public string Model;

		public string Line;
	}

	private sealed class GreetTalk
	{
		public int Man;

		public int Stage;

		public int At;

		public int Tries;

		public bool Curt;

		public bool YouSpoke;

		public bool HeSpoke;

		public string YourLine;

		public string HisLine;

		public string Who;

		public (string line, string param, int variation, string voice) Trying;

		public List<(string line, string param, int variation, string voice)> Queue;
	}

	internal static readonly string[] GreetLines = new string[17]
	{
		"GREET_GENERAL_STRANGER", "GREET_GENERAL_FAMILIAR", "GREET_MORNING", "GREET_AFTERNOON", "GREET_EVENING", "GREET_NIGHT", "HOWS_IT_GOING", "GENERIC_HI", "GENERIC_HOWDY", "HOWDY",
		"HELLO", "GREET", "GREETING", "GREET_PLAYER", "GREET_MALE", "CHAT_GREET", "GREET_SHOP"
	};

	internal static readonly string[] CurtLines = new string[17]
	{
		"GREET_GENERAL_INSULT", "GREET_HATED", "GET_LOST", "GO_AWAY", "WHAT_DO_YOU_WANT", "GENERIC_INSULT_MED", "GENERIC_INSULT_LOW", "GENERIC_INSULT_HIGH", "GENERIC_INSULT", "INSULT",
		"INSULT_RESPONSE", "PROVOKE_GENERIC", "PROVOKE_STARE", "ANTAGONIZE", "MELEE_ANTAGONIZE_MALE", "GENERIC_CURSE_MED", "GENERIC_CURSE_HIGH"
	};

	private static readonly (string kind, string[] lines)[] ProbeSets = new(string, string[])[6]
	{
		("greet", GreetLines),
		("curt", CurtLines),
		("threat", new string[10] { "CHALLENGE_THREATEN", "THREATEN", "GENERIC_THREATEN", "WITNESS_THREATEN_MALE", "INTIMIDATE", "COMBAT_TAUNT", "TAUNT", "GENERIC_WAR_CRY", "CALL_OUT", "FIGHT" }),
		("fear", new string[10] { "PLEAD", "PLEAD_HOGTIED", "SURRENDER", "SCARED_HELP", "PANIC", "GENERIC_FRIGHTENED_HIGH", "GENERIC_FRIGHTENED_MED", "INTIMIDATED", "INTIMIDATED_GEN", "FLEE" }),
		("shocked", new string[6] { "GENERIC_SHOCKED_HIGH", "GENERIC_SHOCKED_MED", "SHOCKED", "SEE_WEIRD", "LOOK_AT_THAT", "WHAT_THE" }),
		("thanks", new string[10] { "GENERIC_THANKS", "THANKS", "GENERIC_BYE", "GOODBYE", "FAREWELL", "GENERIC_YES", "GENERIC_NO", "RESPONSE_YES", "RESPONSE_NO", "WITNESS_DISMISS" })
	};

	private HashSet<string> _goodLines;

	private const bool VoiceNameSearch = false;

	private static readonly string[] VoiceSuffixes = new string[8] { "", "_WHITE_01", "_WHITE_02", "_WHITE_03", "_BLACK_01", "_HISPANIC_01", "_01", "_02" };

	private const int VoiceNumbers = 3000;

	private const int VoiceTriesPerTick = 240;

	private static readonly Dictionary<string, string[]> BorrowVoice = new Dictionary<string, string[]>
	{
		["u_m_m_unidusterhenchman_01"] = new string[3] { "g_m_m_uniduster_01", "g_m_m_uniduster_02", "g_m_m_uniduster_03" },
		["u_m_m_unidusterhenchman_02"] = new string[3] { "g_m_m_uniduster_02", "g_m_m_uniduster_03", "g_m_m_uniduster_01" },
		["u_m_m_unidusterhenchman_03"] = new string[3] { "g_m_m_uniduster_03", "g_m_m_uniduster_01", "g_m_m_uniduster_02" },
		["g_m_y_uniexconfeds_02"] = new string[2] { "g_m_y_uniexconfeds_01", "g_m_o_uniexconfeds_01" },
		["re_rally_males_01"] = new string[2] { "a_m_m_rhdtownfolk_01", "a_m_m_rhdupperclass_01" },
		["re_rallysetup_males_01"] = new string[2] { "a_m_m_rhdupperclass_01", "a_m_m_rhdtownfolk_01" },
		["u_m_m_nbxbronteasc_01"] = new string[1] { "g_m_m_unibrontegoons_01" },
		["u_m_m_nbxbrontesecform_01"] = new string[1] { "g_m_m_unibrontegoons_01" },
		["u_m_m_nbxbrontegoon_01"] = new string[1] { "g_m_m_unibrontegoons_01" },
		["u_m_m_bht_skinnerbrother"] = new string[1] { "g_m_m_unimountainmen_01" },
		["re_laramiegangrustling_males_01"] = new string[2] { "g_m_y_uniexconfeds_01", "g_m_o_uniexconfeds_01" }
	};

	private static readonly Dictionary<string, string> BuiltInVoices = new Dictionary<string, string>
	{
		["g_m_m_uniduster_01"] = "0355_G_M_M_UNIDUSTER_01_WHITE_01",
		["g_m_m_uniduster_02"] = "0357_G_M_M_UNIDUSTER_02_WHITE_01",
		["g_m_m_uniduster_03"] = "0361_G_M_M_UNIDUSTER_03_WHITE_01",
		["g_m_o_uniexconfeds_01"] = "0174_G_M_O_UNIEXCONFEDS_01_WHITE_01",
		["g_m_y_uniexconfeds_01"] = "0177_G_M_Y_UNIEXCONFEDS_01_WHITE_01"
	};

	private readonly HashSet<string> _borrowLogged = new HashSet<string>();

	private Dictionary<string, string> _voiceNames;

	private static IntPtr _voiceBuf = IntPtr.Zero;

	private VoiceProbe _probe;

	private readonly HashSet<string> _voiceCensus = new HashSet<string>();

	private int _nextVoiceCensus;

	private List<(uint model, string outfits)> _modelCheck;

	private int _modelCheckAt;

	private Ped _modelCheckPed;

	private readonly List<string> _modelCheckNo = new List<string>();

	private int _modelSearch = -1;

	private readonly Dictionary<int, GreetTalk> _greetTalks = new Dictionary<int, GreetTalk>();

	private readonly Dictionary<string, (string line, string param, int variation, string voice)> _heardLine = new Dictionary<string, (string, string, int, string)>();

	private const int SpokeWithinMs = 900;

	private const int YouFinishMaxMs = 4000;

	private const int GreetTriesMax = 4;

	private const ulong DraftAnimalCountFn = 11643009930546700959uL;

	private const ulong SetDraftSpeedFn = 882441092585915516uL;

	private string VoicesPath => Path.Combine(_saveDir, "voices.txt");

	private HashSet<string> GoodLines
	{
		get
		{
			if (_goodLines != null)
			{
				return _goodLines;
			}
			_goodLines = new HashSet<string>();
			try
			{
				if (File.Exists(VoicesPath))
				{
					string[] array = File.ReadAllLines(VoicesPath);
					foreach (string text in array)
					{
						if (text.Trim().Length > 0)
						{
							_goodLines.Add(text.Trim());
						}
					}
				}
			}
			catch (Exception ex)
			{
				GrudgeLog("VOICE voices.txt unreadable: " + ex.Message);
			}
			return _goodLines;
		}
	}

	private void NoteGoodLine(string line)
	{
		if (!GoodLines.Add(line))
		{
			return;
		}
		try
		{
			SafeFile.Write(VoicesPath, string.Join("\r\n", GoodLines.OrderBy((string x) => x)));
		}
		catch (Exception ex)
		{
			GrudgeLog("VOICE voices.txt not saved: " + ex.Message);
		}
	}

	private string[] GoodFirst(string[] lines)
	{
		return lines.OrderBy((string l) => (!GoodLines.Contains(l)) ? 1 : 0).ToArray();
	}

	private string VoiceNamesPath => Path.Combine(_saveDir, "voicenames.txt");

	private string KnownVoice(string model)
	{
		if (!VoiceNames.TryGetValue(model, out var value))
		{
			if (!BuiltInVoices.TryGetValue(model, out var value2))
			{
				return null;
			}
			return value2;
		}
		return value;
	}

	internal string BorrowedVoice(uint modelHash)
	{
		string key = Models.Name(modelHash);
		if (!BorrowVoice.TryGetValue(key, out var value))
		{
			return null;
		}
		string[] array = value;
		foreach (string model in array)
		{
			string text = KnownVoice(model);
			if (text != null)
			{
				return text;
			}
		}
		return null;
	}

	internal void GiveVoice(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return;
		}
		string text = BorrowedVoice((uint)p.Model.Hash);
		if (text == null)
		{
			return;
		}
		try
		{
			AUDIO.SET_AMBIENT_VOICE_NAME(p.Handle, text);
		}
		catch
		{
			return;
		}
		string text2 = Models.Name(p);
		if (_borrowLogged.Add(text2))
		{
			bool flag = false;
			try
			{
				flag = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(p.Handle, "GREET_GENERAL_STRANGER", unk: false);
			}
			catch
			{
			}
			GrudgeLog("VOICE " + text2 + " borrows '" + text + "' - greeting now " + (flag ? "there" : "still missing"));
		}
	}

	internal Dictionary<string, string> VoiceNames
	{
		get
		{
			if (_voiceNames != null)
			{
				return _voiceNames;
			}
			_voiceNames = new Dictionary<string, string>();
			try
			{
				if (File.Exists(VoiceNamesPath))
				{
					string[] array = File.ReadAllLines(VoiceNamesPath);
					for (int i = 0; i < array.Length; i++)
					{
						string[] array2 = array[i].Split('|');
						if (array2.Length == 2)
						{
							_voiceNames[array2[0]] = array2[1];
						}
					}
				}
			}
			catch (Exception ex)
			{
				GrudgeLog("VOICE voicenames.txt unreadable: " + ex.Message);
			}
			return _voiceNames;
		}
	}

	private void NoteVoiceName(string model, string voice)
	{
		VoiceNames[model] = voice;
		try
		{
			SafeFile.Write(VoiceNamesPath, string.Join(Environment.NewLine, VoiceNames.Select((KeyValuePair<string, string> kv) => kv.Key + "|" + kv.Value)));
		}
		catch (Exception ex)
		{
			GrudgeLog("VOICE voicenames.txt not saved: " + ex.Message);
		}
	}

	private static ulong VoiceBuf(string voice)
	{
		if (_voiceBuf == IntPtr.Zero)
		{
			_voiceBuf = Marshal.AllocHGlobal(96);
		}
		byte[] bytes = Encoding.ASCII.GetBytes(voice);
		int num = Math.Min(bytes.Length, 95);
		Marshal.Copy(bytes, 0, _voiceBuf, num);
		Marshal.WriteByte(_voiceBuf, num, 0);
		return (ulong)_voiceBuf.ToInt64();
	}

	private unsafe bool TrySayVoice(Ped speaker, string line, string voice, Ped listener)
	{
		if (speaker == null || !speaker.Exists())
		{
			return false;
		}
		ulong* ptr = stackalloc ulong[8];
		for (int i = 0; i < 8; i++)
		{
			ptr[i] = 0uL;
		}
		*ptr = UiDraw.CString(line);
		ptr[1] = VoiceBuf(voice);
		ptr[3] = MISC.GET_HASH_KEY("SPEECH_PARAMS_FORCE_NORMAL_CLEAR");
		ptr[4] = ((listener != null && listener.Exists()) ? ((uint)listener.Handle) : 0);
		ptr[6] = 1uL;
		ptr[7] = 1uL;
		return Function.Call<bool>(10233584479118828642uL, new InputArgument[2]
		{
			speaker.Handle,
			new InputArgument((ulong)ptr)
		});
	}

	private string VoiceState(Ped p)
	{
		int handle = p.Handle;
		bool flag = ENTITY.IS_ENTITY_A_MISSION_ENTITY(handle);
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		try
		{
			flag2 = AUDIO.IS_AMBIENT_SPEECH_DISABLED(handle);
		}
		catch
		{
		}
		try
		{
			flag3 = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(handle, "GREET_GENERAL_STRANGER", unk: false);
		}
		catch
		{
		}
		try
		{
			flag4 = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(handle, "CHALLENGE_THREATEN", unk: false);
		}
		catch
		{
		}
		return $"mission entity {flag}, speech off {flag2}, has GREET_GENERAL_STRANGER {flag3}, has CHALLENGE_THREATEN {flag4}";
	}

	private void DebugVoiceProbe(Ped player)
	{
		Ped ped = (from p in PedsAround(player)
			where p != null && p.Exists() && !p.IsPlayer && p.IsHuman && !p.IsDead && p.Position.DistanceTo(player.Position) < 20f
			orderby p.Position.DistanceTo(player.Position)
			select p).FirstOrDefault();
		if (ped == null)
		{
			SpawnerSays("nobody within 20 m to test.");
			return;
		}
		Named value;
		string text = ((_named.TryGetValue(ped.Handle, out value) && !string.IsNullOrEmpty(value.Name)) ? value.Name : Models.Name(ped));
		_probe = new VoiceProbe
		{
			Who = ped,
			Name = text,
			Next = Game.GameTime + 500,
			Todo = ProbeSets.SelectMany(((string kind, string[] lines) s) => s.lines.Select((string l) => (kind: s.kind, l: l))).ToList()
		};
		GrudgeLog(string.Format("VOICE test: {0} ({1}{2}) tries {3} lines - {4}", text, Models.Name(ped), IsOurs(ped.Handle) ? ", one of the mod's" : ", the game's", _probe.Todo.Count, VoiceState(ped)));
		SpawnerSays($"voice test: {text} tries {_probe.Todo.Count} lines - stay close; the ones he says go in the log.");
	}

	private void UpdateVoiceProbe(Ped player)
	{
		VoiceProbe probe = _probe;
		if (probe == null || Game.GameTime < probe.Next)
		{
			return;
		}
		if (probe.Who == null || !probe.Who.Exists() || probe.Who.IsDead || probe.Who.Position.DistanceTo(player.Position) > 40f)
		{
			GrudgeLog($"VOICE test: {probe.Name} is gone - stopped after {probe.Tried} lines");
			EndProbe();
		}
		else if (AUDIO.IS_ANY_SPEECH_PLAYING(probe.Who.Handle))
		{
			probe.Next = Game.GameTime + 300;
		}
		else if (probe.Tried >= probe.Todo.Count)
		{
			if (probe.Played.Count == 0 && !probe.Repaired)
			{
				probe.Repaired = true;
				try
				{
					AUDIO.STOP_PED_SPEAKING(probe.Who.Handle, shaking: false);
				}
				catch (Exception ex)
				{
					GrudgeLog("VOICE STOP_PED_SPEAKING failed: " + ex.Message);
				}
				List<string> list = ((GoodLines.Count > 0) ? GoodLines.ToList() : GreetLines.ToList());
				probe.Todo.AddRange(list.Select((string l) => ("again", l: l)));
				GrudgeLog($"VOICE test: {probe.Name} said nothing - speech let on (STOP_PED_SPEAKING false), {list.Count} lines again - {VoiceState(probe.Who)}");
				probe.Next = Game.GameTime + 400;
				return;
			}
			_ = probe.Search;
			_ = 0;
			if (probe.Search >= 0 && probe.Search < 3000 * VoiceSuffixes.Length)
			{
				int num = 0;
				while (num < 240 && probe.Search < 3000 * VoiceSuffixes.Length)
				{
					string text = $"{probe.Search % 3000:0000}_{probe.Model}{VoiceSuffixes[probe.Search / 3000]}";
					if (TrySayVoice(probe.Who, probe.Line, text, player))
					{
						if (probe.Played.Count > 0 && !probe.Played.Any((string x) => x.StartsWith("voice:")))
						{
							GrudgeLog("VOICE test: " + probe.Name + "'s model answers to the voice name '" + text + "' (the form for " + probe.Model + ")");
							NoteVoiceName(probe.Model.ToLowerInvariant(), text);
							probe.Search = int.MaxValue;
							probe.Next = Game.GameTime + 2500;
							return;
						}
						NoteVoiceName(probe.Model.ToLowerInvariant(), text);
						bool flag = false;
						try
						{
							flag = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(probe.Who.Handle, "GREET_GENERAL_STRANGER", unk: false);
						}
						catch
						{
						}
						try
						{
							AUDIO.SET_AMBIENT_VOICE_NAME(probe.Who.Handle, text);
						}
						catch (Exception ex2)
						{
							GrudgeLog("VOICE SET_AMBIENT_VOICE_NAME failed: " + ex2.Message);
						}
						GrudgeLog($"VOICE test: found {probe.Model}'s voice - '{text}' ({probe.Name} said '{probe.Line}'); given to him - {VoiceState(probe.Who)} (before: has GREET {flag})");
						probe.Played.Add("voice:" + text);
						probe.Search = int.MaxValue;
						probe.Next = Game.GameTime + 2500;
						return;
					}
					num++;
					probe.Search++;
				}
				if (probe.Search >= 3000 * VoiceSuffixes.Length)
				{
					GrudgeLog($"VOICE test: no voice name NNNN_{probe.Model} answered ({probe.Search} tried)");
				}
				probe.Next = Game.GameTime + 50;
			}
			else
			{
				EndProbe();
			}
		}
		else
		{
			var (text2, text3) = probe.Todo[probe.Tried++];
			if (TrySay(probe.Who, text3, player))
			{
				probe.Played.Add(text2 + ":" + text3);
				NoteGoodLine(text3);
				GrudgeLog("VOICE test: " + probe.Name + " said '" + text3 + "' (" + text2 + ")");
				probe.Next = Game.GameTime + 2500;
			}
			else
			{
				probe.Next = Game.GameTime + 120;
			}
		}
	}

	private void EndProbe()
	{
		VoiceProbe probe = _probe;
		_probe = null;
		if (probe != null)
		{
			GrudgeLog(string.Format("VOICE test done: {0} said {1} of {2}: {3}", probe.Name, probe.Played.Count, probe.Tried, (probe.Played.Count > 0) ? string.Join(", ", probe.Played) : "none"));
			ShowNotification((probe.Played.Count > 0) ? $"Voice test: {probe.Name} said {probe.Played.Count} of {probe.Tried} lines (in the log)." : $"Voice test: {probe.Name} said none of {probe.Tried} lines.");
		}
	}

	private void UpdateVoiceCensus(Ped player)
	{
		if (Game.GameTime < _nextVoiceCensus)
		{
			return;
		}
		_nextVoiceCensus = Game.GameTime + 20000;
		foreach (Ped item in PedsAround(player))
		{
			if (item == null || !item.Exists() || item.IsPlayer || !item.IsHuman || item.IsDead || item.Position.DistanceTo(player.Position) > 40f)
			{
				continue;
			}
			bool flag = IsOurs(item.Handle);
			string text = Models.Name(item);
			if (_voiceCensus.Add(text + (flag ? "|ours" : "|game")))
			{
				bool flag2 = false;
				bool flag3 = false;
				try
				{
					flag2 = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(item.Handle, "GREET_GENERAL_STRANGER", unk: false);
				}
				catch
				{
				}
				try
				{
					flag3 = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(item.Handle, "CHALLENGE_THREATEN", unk: false);
				}
				catch
				{
				}
				GrudgeLog(string.Format("VOICECENSUS {0} ({1}): {2} (greet {3}, challenge {4}){5}", text, flag ? "the mod's" : "the game's", (flag2 || flag3) ? "has a voice" : "NO voice", flag2, flag3, (!flag) ? "" : (ENTITY.IS_ENTITY_A_MISSION_ENTITY(item.Handle) ? ", held" : ", released")));
			}
		}
	}

	private void DebugModelVoices(Ped player)
	{
		Dictionary<uint, List<string>> dictionary = new Dictionary<uint, List<string>>();
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			foreach (PedHash item in (gang.Seed ?? new(int, int, PedHash[])[0]).SelectMany(((int Rank, int Count, PedHash[] Models) s) => s.Models ?? new PedHash[0]).Concat(gang.TroopModels ?? new PedHash[0]))
			{
				if (!dictionary.TryGetValue((uint)item, out var value))
				{
					value = (dictionary[(uint)item] = new List<string>());
				}
				if (!value.Contains(gang.Id))
				{
					value.Add(gang.Id);
				}
			}
		}
		PedHash[] array = new PedHash[2]
		{
			PedHash.a_m_m_rhdtownfolk_01,
			PedHash.a_m_m_rhdupperclass_01
		};
		foreach (PedHash key in array)
		{
			if (!dictionary.ContainsKey((uint)key))
			{
				dictionary[(uint)key] = new List<string> { "townsfolk" };
			}
		}
		_modelCheck = dictionary.Select((KeyValuePair<uint, List<string>> kv) => (Key: kv.Key, string.Join("/", kv.Value))).ToList();
		_modelCheckNo.Clear();
		_modelCheckAt = Game.GameTime + 300;
		_modelCheckPed = null;
		GrudgeLog($"MODELVOICE checking {_modelCheck.Count} outfit models");
		SpawnerSays($"checking {_modelCheck.Count} outfit models for a voice - a few seconds each, nothing shows.");
	}

	private unsafe void UpdateModelVoices(Ped player)
	{
		if (_modelCheck == null || Game.GameTime < _modelCheckAt)
		{
			return;
		}
		if (_modelCheckPed != null)
		{
			Ped modelCheckPed = _modelCheckPed;
			(uint model, string outfits) tuple = _modelCheck[0];
			uint item = tuple.model;
			string item2 = tuple.outfits;
			bool flag = false;
			bool flag2 = false;
			if (modelCheckPed.Exists())
			{
				try
				{
					flag = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(modelCheckPed.Handle, "GREET_GENERAL_STRANGER", unk: false);
				}
				catch
				{
				}
				try
				{
					flag2 = AUDIO.DOES_CONTEXT_EXIST_FOR_THIS_PED(modelCheckPed.Handle, "CHALLENGE_THREATEN", unk: false);
				}
				catch
				{
				}
			}
			string mn = Models.Name(item);
			string text = mn.ToUpperInvariant();
			BorrowVoice.Values.Any((string[] ds) => ds.Contains(mn));
			_modelSearch = -1;
			_modelCheckPed = null;
			_modelCheck.RemoveAt(0);
			if (modelCheckPed.Exists())
			{
				int handle = modelCheckPed.Handle;
				ENTITY.SET_ENTITY_AS_MISSION_ENTITY(handle, p1: true, p2: true);
				PED.DELETE_PED(&handle);
			}
			if (!flag && !flag2)
			{
				_modelCheckNo.Add(mn);
			}
			GrudgeLog(string.Format("MODELVOICE {0} ({1}): {2} (greet {3}, challenge {4}){5}", mn, item2, (flag || flag2) ? "has a voice" : "NO voice", flag, flag2, VoiceNames.TryGetValue(mn, out var value) ? (" - '" + value + "'") : ""));
			_modelCheckAt = Game.GameTime + 200;
			if (_modelCheck.Count == 0)
			{
				GrudgeLog(string.Format("MODELVOICE done: {0} outfit model(s) with no voice: {1}", _modelCheckNo.Count, string.Join(", ", _modelCheckNo)));
				ShowNotification($"Voice check: {_modelCheckNo.Count} outfit model(s) with no voice (in the log).");
				_modelCheck = null;
			}
		}
		else if (_modelCheck.Count == 0)
		{
			_modelCheck = null;
		}
		else
		{
			Vector3 pos = player.Position + new Vector3(0f, 0f, -12f);
			Ped ped = SpawnPed((PedHash)_modelCheck[0].model, pos, 0f);
			if (ped == null)
			{
				GrudgeLog("MODELVOICE " + Models.Name(_modelCheck[0].model) + ": couldn't be made");
				_modelCheck.RemoveAt(0);
				_modelCheckAt = Game.GameTime + 200;
			}
			else
			{
				ENTITY.FREEZE_ENTITY_POSITION(ped.Handle, toggle: true);
				ENTITY.SET_ENTITY_VISIBLE(ped.Handle, toggle: false);
				ENTITY.SET_ENTITY_COLLISION(ped.Handle, toggle: false, keepPhysics: false);
				_modelCheckPed = ped;
				_modelCheckAt = Game.GameTime + 1000;
			}
		}
	}

	internal void NoteGreetReply(int handle)
	{
		if (IsOurs(handle) && ENTITY.DOES_ENTITY_EXIST(handle) && !ENTITY.IS_ENTITY_DEAD(handle) && !PED.IS_PED_IN_COMBAT(handle, 0) && !_greetTalks.ContainsKey(handle) && ENTITY.IS_ENTITY_A_MISSION_ENTITY(handle))
		{
			NemesisRecord nemesisRecord = RecordAt(handle);
			_greetTalks[handle] = new GreetTalk
			{
				Man = handle,
				Stage = 0,
				At = Game.GameTime + 400,
				Curt = ((nemesisRecord?.Hate ?? 0) >= 55),
				Who = (nemesisRecord?.Name ?? (_named.TryGetValue(handle, out var value) ? value.Name : $"ped {handle}"))
			};
		}
	}

	private void UpdateGreetReplies(Ped player)
	{
		if (_greetTalks.Count == 0)
		{
			return;
		}
		foreach (GreetTalk item in _greetTalks.Values.ToList())
		{
			if (Game.GameTime < item.At && item.Stage != 1 && item.Stage != 2 && item.Stage != 4)
			{
				continue;
			}
			int man = item.Man;
			if (!ENTITY.DOES_ENTITY_EXIST(man) || ENTITY.IS_ENTITY_DEAD(man) || PED.IS_PED_IN_COMBAT(man, 0) || player.IsDead || ENTITY.GET_ENTITY_COORDS(man, alive: true, realCoords: true).DistanceTo(player.Position) > 20f)
			{
				_greetTalks.Remove(man);
				continue;
			}
			Ped ped = new Ped(man);
			switch (item.Stage)
			{
			case 0:
				if (AUDIO.IS_ANY_SPEECH_PLAYING(player.Handle))
				{
					item.YouSpoke = true;
					item.YourLine = "(the game's)";
					item.Stage = 2;
					item.At = Game.GameTime;
					break;
				}
				if (item.Queue == null)
				{
					item.Queue = Attempts(player, YourGreetLines(ped), null);
				}
				item.Stage = (StartLine(player, ped, item) ? 1 : 2);
				item.At = Game.GameTime;
				break;
			case 1:
				if (AUDIO.IS_ANY_SPEECH_PLAYING(player.Handle))
				{
					item.YouSpoke = true;
					item.YourLine = item.Trying.line;
					Heard(player, "you", item.Trying);
					item.Stage = 2;
					item.At = Game.GameTime;
				}
				else if (Game.GameTime - item.At > 900)
				{
					item.Stage = ((item.Tries < 4 && StartLine(player, ped, item)) ? 1 : 2);
					item.At = Game.GameTime;
				}
				break;
			case 2:
				if (!AUDIO.IS_ANY_SPEECH_PLAYING(player.Handle) || Game.GameTime - item.At >= 4000)
				{
					item.Stage = 3;
					item.At = Game.GameTime + 250;
					item.Queue = null;
					item.Tries = 0;
				}
				break;
			case 3:
				if (AUDIO.IS_ANY_SPEECH_PLAYING(man))
				{
					item.HeSpoke = true;
					item.HisLine = "(the game's)";
					item.Stage = 5;
				}
				else
				{
					TASK.TASK_LOOK_AT_ENTITY(man, player.Handle, 3500, 0, 51, 0);
					item.Queue = Attempts(ped, item.Curt ? HisCurtLines() : HisGreetLines(), BorrowedVoice((uint)ped.Model.Hash));
					item.Stage = (StartLine(ped, player, item) ? 4 : 5);
					item.At = Game.GameTime;
				}
				break;
			case 4:
				if (AUDIO.IS_ANY_SPEECH_PLAYING(man))
				{
					item.HeSpoke = true;
					item.HisLine = item.Trying.line + ((item.Trying.voice != null) ? " (borrowed voice)" : "");
					Heard(ped, "he", item.Trying);
					item.Stage = 5;
				}
				else if (Game.GameTime - item.At > 900)
				{
					item.Stage = ((item.Tries < 4 && StartLine(ped, player, item)) ? 4 : 5);
					item.At = Game.GameTime;
				}
				break;
			}
			if (item.Stage == 5)
			{
				_greetTalks.Remove(man);
				NemesisRecord nemesisRecord = RecordAt(man);
				GrudgeLog(Pron.Her("GREETREPLY " + item.Who + ": you " + (item.YouSpoke ? ("said " + item.YourLine) : "said nothing - none of your lines was heard") + "; he " + (item.HeSpoke ? ((item.Curt ? "answered curtly" : "answered") + " " + item.HisLine) : "said nothing - none of his lines was heard (run the voice test by him)") + ((nemesisRecord != null) ? $" (hate {nemesisRecord.Hate})" : ""), !PED.IS_PED_MALE(man)));
			}
		}
	}

	private bool StartLine(Ped speaker, Ped listener, GreetTalk g)
	{
		while (g.Queue != null && g.Queue.Count > 0 && g.Tries < 4)
		{
			(string, string, int, string) trying = g.Queue[0];
			g.Queue.RemoveAt(0);
			if (SayOnce(speaker, trying.Item1, listener, trying.Item2, trying.Item3, trying.Item4))
			{
				g.Trying = trying;
				g.Tries++;
				return true;
			}
		}
		return false;
	}

	private List<(string line, string param, int variation, string voice)> Attempts(Ped speaker, IEnumerable<string> lines, string borrowed)
	{
		List<(string, string, int, string)> list = new List<(string, string, int, string)>();
		List<string> list2 = lines.Distinct().ToList();
		if (_heardLine.TryGetValue(VoiceKey(speaker), out (string, string, int, string) value) && list2.Contains(value.Item1))
		{
			list.Add(value);
		}
		foreach (string item in list2)
		{
			list.Add((item, "SPEECH_PARAMS_FORCE_NORMAL_CLEAR", 0, null));
			list.Add((item, "SPEECH_PARAMS_FORCE", 1, null));
		}
		if (borrowed != null)
		{
			foreach (string item2 in lines.Take(3))
			{
				list.Add((item2, "SPEECH_PARAMS_FORCE_NORMAL_CLEAR", 0, borrowed));
			}
		}
		return list;
	}

	private void Heard(Ped speaker, string who, (string line, string param, int variation, string voice) a)
	{
		_heardLine[VoiceKey(speaker)] = a;
		NoteGoodLine(a.line);
	}

	private static string VoiceKey(Ped p)
	{
		return (uint)p.Model.Hash + (p.IsPlayer ? "|you" : "|him");
	}

	private static IEnumerable<string> YourGreetLines(Ped to)
	{
		bool flag = PED.IS_PED_MALE(to.Handle);
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		string sex = (flag ? "MALE" : "FEMALE");
		if (cLOCK_HOURS >= 5 && cLOCK_HOURS < 12)
		{
			yield return "GREET_MORNING_" + sex;
		}
		else if (cLOCK_HOURS >= 17)
		{
			yield return "GREET_EVENING_" + sex;
		}
		yield return "GREET_" + sex;
		yield return "GREET_" + sex + "_MED";
		yield return "GREET_GENERAL";
	}

	private IEnumerable<string> HisGreetLines()
	{
		int hour = CLOCK.GET_CLOCK_HOURS();
		yield return "GREET_GENERAL_STRANGER";
		if (hour >= 5 && hour < 12)
		{
			yield return "GREET_MORNING_MALE";
		}
		else if (hour >= 17)
		{
			yield return "GREET_EVENING_MALE";
		}
		yield return "GREET_MALE";
		yield return "GREET_GENERAL_FAMILIAR";
		string[] array = GoodFirst(GreetLines);
		for (int i = 0; i < array.Length; i++)
		{
			yield return array[i];
		}
	}

	private IEnumerable<string> HisCurtLines()
	{
		yield return "GREET_REJECT";
		string[] array = GoodFirst(CurtLines);
		for (int i = 0; i < array.Length; i++)
		{
			yield return array[i];
		}
		yield return "GREET_GENERAL_STRANGER";
	}

	private unsafe bool SayOnce(Ped speaker, string line, Ped listener, string param, int variation, string voice)
	{
		if (speaker == null || !speaker.Exists() || DeadNow(speaker))
		{
			return false;
		}
		ulong* ptr = stackalloc ulong[8];
		for (int i = 0; i < 8; i++)
		{
			ptr[i] = 0uL;
		}
		*ptr = UiDraw.CString(line);
		if (voice != null)
		{
			ptr[1] = VoiceBuf(voice);
		}
		ptr[2] = (ulong)variation;
		ptr[3] = MISC.GET_HASH_KEY(param);
		ptr[4] = ((listener != null && listener.Exists()) ? ((uint)listener.Handle) : 0);
		ptr[6] = 1uL;
		ptr[7] = 1uL;
		return Function.Call<bool>(10233584479118828642uL, new InputArgument[2]
		{
			speaker.Handle,
			new InputArgument((ulong)ptr)
		});
	}
}
