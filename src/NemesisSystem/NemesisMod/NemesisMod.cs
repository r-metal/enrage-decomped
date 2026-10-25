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

public partial class NemesisMod : Script
{
	private struct ProcMem
	{
		public uint cb;

		public uint PageFaultCount;

		public UIntPtr PeakWorkingSetSize;

		public UIntPtr WorkingSetSize;

		public UIntPtr QuotaPeakPagedPoolUsage;

		public UIntPtr QuotaPagedPoolUsage;

		public UIntPtr QuotaPeakNonPagedPoolUsage;

		public UIntPtr QuotaNonPagedPoolUsage;

		public UIntPtr PagefileUsage;

		public UIntPtr PeakPagefileUsage;

		public UIntPtr PrivateUsage;
	}

	internal const int MaxRank = 5;

	private readonly string _saveDir;

	private readonly string _savePath;

	internal readonly NemesisSettings Config;

	private readonly IniPanel _iniPanel;

	private readonly WelcomeGuide _welcome;

	private bool _welcomeTried;

	private readonly RosterMenu _menu;

	private readonly BulletImpactModule _bulletImpact;

	private Ped _activeNemesisPed;

	private Blip _activeBlip;

	private readonly Dictionary<int, NemesisRecord> _worldNemeses = new Dictionary<int, NemesisRecord>();

	private bool _playerWasDead;

	private Random _rng = new Random();

	internal int SessionSeed;

	private int _nextAmbushCheck;

	private bool _forceAmbushRequested;

	private NemesisRecord _summonRequested;

	private const int ZoneTypeTown = 1;

	private const int ZoneTypeDistrict = 10;

	private static readonly Dictionary<uint, string> ZoneNames = new Dictionary<uint, string>
	{
		{ 1654810713u, "Aguasdulces" },
		{ 201158410u, "Aguasdulces" },
		{ 3087833527u, "Aguasdulces" },
		{ 7359335u, "Annesburg" },
		{ 3550472498u, "Armadillo" },
		{ 2586580314u, "Beecher's Hope" },
		{ 1053078005u, "Blackwater" },
		{ 1778899666u, "Braithwaite Manor" },
		{ 2347551651u, "Butcher Creek" },
		{ 1862420670u, "Caliga Hall" },
		{ 2443661614u, "Cornwall Kerosene & Tar" },
		{ 3821916002u, "Emerald Ranch" },
		{ 406627834u, "Lagras" },
		{ 1299204683u, "Manicato" },
		{ 1463094051u, "Manzanita Post" },
		{ 2046780049u, "Rhodes" },
		{ 2147354003u, "Sisika Penitentiary" },
		{ 3529426767u, "Saint Denis" },
		{ 427683330u, "Strawberry" },
		{ 2770008149u, "Tumbleweed" },
		{ 459833523u, "Valentine" },
		{ 2126321341u, "Van Horn" },
		{ 3422345262u, "Wallace Station" },
		{ 1663398575u, "Wapiti" },
		{ 2025841068u, "Bayou Nwa" },
		{ 822658194u, "Big Valley" },
		{ 1308232528u, "Bluewater Marsh" },
		{ 4186119282u, "Cholla Springs" },
		{ 1835499550u, "Cumberland Forest" },
		{ 426773653u, "Diez Coronas" },
		{ 2228727054u, "Gaptooth Ridge" },
		{ 476637847u, "Great Plains" },
		{ 4174810561u, "the Grizzlies" },
		{ 1645618177u, "the Grizzlies" },
		{ 3782438103u, "Guarma" },
		{ 131399519u, "the Heartlands" },
		{ 892930832u, "Hennigan's Stead" },
		{ 2975011176u, "Perdido" },
		{ 1453836102u, "Punta Orgullo" },
		{ 2148975167u, "Rio Bravo" },
		{ 178647645u, "Roanoke Ridge" },
		{ 3430691604u, "Scarlett Meadows" },
		{ 1684533001u, "Tall Trees" }
	};

	private int _quietEndedAt = -100000;

	private readonly HashSet<Action> _mapPending = new HashSet<Action>();

	private readonly int _loadedAt = Game.GameTime;

	internal const string DataFolderName = "enRAGE";

	internal const string OldDataFolderName = "NemesisSystem";

	internal static string DataFolderNote;

	private string _loadNotice;

	private string _apiNotice;

	private int _forcePatrol;

	private int _nextHeartbeat;

	private bool _wasQuiet;

	private int _quietUntil;

	private int _lastJumpAt = -100000;

	private int _nextBusyCheck;

	private bool _busy;

	private int _lastFadeAt;

	internal string QuietWhy = "-";

	private bool _seedLonersOnLoad;

	private static readonly PedHash[] TestLonerModels = new PedHash[15]
	{
		PedHash.a_m_m_valtownfolk_01,
		PedHash.a_m_m_valtownfolk_02,
		PedHash.a_m_m_valcriminals_01,
		PedHash.a_m_m_valfarmer_01,
		PedHash.a_m_m_vallaborer_01,
		PedHash.a_m_m_rancher_01,
		PedHash.a_m_m_rhdtownfolk_01,
		PedHash.a_m_m_strtownfolk_01,
		PedHash.a_m_m_htlroughtravellers_01,
		PedHash.a_m_m_rkrroughtravellers_01,
		PedHash.a_m_m_sdroughtravellers_01,
		PedHash.a_m_m_rhdupperclass_01,
		PedHash.a_m_m_unigunslinger_01,
		PedHash.a_m_m_bivworker_01,
		PedHash.a_m_m_asbminer_01
	};

	internal List<NemesisRecord> Roster { get; private set; } = new List<NemesisRecord>();

	internal NemesisRecord ActiveRecord { get; private set; }

	internal bool MapSettled()
	{
		try
		{
			Ped character = Game.Player.Character;
			if (character == null || !character.Exists() || WorldBusy(character))
			{
				return false;
			}
			if (Game.GameTime - _loadedAt < 15000 || Game.GameTime - _quietEndedAt < 15000)
			{
				return false;
			}
			if (CAM.IS_SCREEN_FADED_OUT() || CAM.IS_SCREEN_FADING_OUT() || CAM.IS_SCREEN_FADING_IN())
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void RunPendingMap()
	{
		if (_mapPending.Count == 0 || !MapSettled())
		{
			return;
		}
		List<Action> list = _mapPending.ToList();
		_mapPending.Clear();
		foreach (Action item in list)
		{
			try
			{
				item();
			}
			catch (Exception ex)
			{
				GrudgeLog("MAP refresh failed: " + ex.Message);
			}
		}
	}

	static NemesisMod()
	{
		StartupTrace.Write("static init done");
	}

	internal static string DocsDataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Rockstar Games", "Red Dead Redemption 2", "enRAGE");

	private static string DataFolder(string scripts)
	{
		string docsDataFolder = DocsDataFolder;
		string text = DateTime.Now.ToString("yyyyMMdd-HHmmss");
		string text2 = null;
		string[] array = new string[2] { "enRAGE", "NemesisSystem" };
		foreach (string path in array)
		{
			string text3 = Path.Combine(scripts, path);
			if (text2 == null && File.Exists(Path.Combine(text3, "nemeses.json")))
			{
				text2 = text3;
			}
		}
		if (!Writable(docsDataFolder))
		{
			string text4 = text2 ?? Path.Combine(scripts, "enRAGE");
			DataFolderNote = "DATA " + docsDataFolder + " can't be written - using " + text4;
			StartupTrace.Write(DataFolderNote);
			return text4;
		}
		if (text2 != null)
		{
			try
			{
				string path2 = Path.Combine(docsDataFolder, "nemeses.json");
				bool flag = File.Exists(path2);
				bool flag2 = flag && File.GetLastWriteTimeUtc(Path.Combine(text2, "nemeses.json")) > File.GetLastWriteTimeUtc(path2).AddMinutes(1.0);
				if (flag && flag2)
				{
					Directory.Move(docsDataFolder, docsDataFolder + ".replaced-" + text);
					Directory.CreateDirectory(docsDataFolder);
					flag = false;
				}
				if (!flag)
				{
					CopyTree(text2, docsDataFolder);
					if (!File.Exists(path2))
					{
						throw new IOException("the roster didn't copy");
					}
					DataFolderNote = "DATA moved " + text2 + " -> " + docsDataFolder + " (the old folder is kept as " + Path.GetFileName(text2) + ".moved-to-Documents-" + text + ")";
				}
				else
				{
					DataFolderNote = "DATA " + text2 + " is older than " + docsDataFolder + " - set aside as " + Path.GetFileName(text2) + ".moved-to-Documents-" + text;
				}
				try
				{
					Directory.Move(text2, text2 + ".moved-to-Documents-" + text);
				}
				catch (Exception ex)
				{
					DataFolderNote = DataFolderNote + " (couldn't rename the old folder: " + ex.Message + " - tried again next load)";
				}
			}
			catch (Exception ex2)
			{
				DataFolderNote = "DATA couldn't move " + text2 + " -> " + docsDataFolder + " (" + ex2.Message + ") - using the old folder this session";
				StartupTrace.Write(DataFolderNote);
				return text2;
			}
			StartupTrace.Write(DataFolderNote);
		}
		StartupTrace.Retarget(docsDataFolder);
		return docsDataFolder;
	}

	private static void CopyTree(string from, string to)
	{
		Directory.CreateDirectory(to);
		string[] files = Directory.GetFiles(from);
		foreach (string text in files)
		{
			if (!string.Equals(Path.GetFileName(text), "startup.log", StringComparison.OrdinalIgnoreCase))
			{
				File.Copy(text, Path.Combine(to, Path.GetFileName(text)), overwrite: true);
			}
		}
		files = Directory.GetDirectories(from);
		foreach (string text2 in files)
		{
			CopyTree(text2, Path.Combine(to, Path.GetFileName(text2)));
		}
	}

	private static bool Writable(string dir)
	{
		try
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, ".write-test");
			File.WriteAllText(path, "ok");
			File.Delete(path);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public NemesisMod()
	{
		StartupTrace.Write("ctor start - enRAGE v" + Theme.ModVersion);
		_saveDir = DataFolder(base.BaseDirectory);
		NemesisRecord.GameClock = () => GameMinutesNow();
		LeaderCopyAt = (int h) => BelongsToUs(h) && IsLeaderCopy(RecordAt(h));
		try
		{
			Directory.CreateDirectory(_saveDir);
			Crumbs.Init(_saveDir);
		}
		catch
		{
		}
		StartupTrace.Write("crumbs ready");
		_savePath = Path.Combine(_saveDir, "nemeses.json");
		Config = new NemesisSettings(Path.Combine(base.BaseDirectory, "NemesisMod.ini"), Path.Combine(_saveDir, "state.ini"));
		StartupTrace.Write("settings loaded");
		Config.EditedRead = delegate
		{
			_settingsEdited = true;
		};
		_menu = new RosterMenu(this);
		_iniPanel = new IniPanel(Path.Combine(base.BaseDirectory, "NemesisMod.ini"), delegate
		{
			Config.Load();
		});
		LandingLog = GrudgeLog;
		GiveVoiceHook = GiveVoice;
		SafeFile.Log = GrudgeLog;
		_welcome = new WelcomeGuide(delegate
		{
			GrudgeLog("WELCOME guide closed");
			if (!Config.WelcomeSeen || Config.WhatsNewSeen != Theme.ModVersion)
			{
				Config.WelcomeSeen = true;
				Config.WhatsNewSeen = Theme.ModVersion;
				Config.Save();
			}
		});
		_welcome.SetupRows = SetupRows;
		_welcome.Log = GrudgeLog;
		TelegramLines.LeaderNameOf = (string fid) => LeaderAsSaidBy(Gangs.ById(fid), null);
		KeyCombo.Bound = (string k) => k switch
		{
			"Telegraph" => Config.TelegraphKey, 
			"Interrogate" => Config.InterrogateKey, 
			"ForceAmbush" => Config.ForceAmbushKey, 
			"RosterMenu" => Config.RosterMenuKey, 
			_ => Keys.None, 
		};
		_welcome.SetMenuKey = delegate(Keys k)
		{
			Config.RosterMenuKey = k;
			KeyCombo.Current = k;
			Config.Save();
			GrudgeLog("SETUP menu key: " + KeyCombo.Name(k));
		};
		_welcome.OnSetupDone = delegate
		{
			Config.SetupSeen = true;
			Config.Save();
			GrudgeLog("SETUP done - messages " + NemesisSettings.MessageLevelShort[Config.MessageLevel] + ", name cards " + NameCardModes[Config.NameCards] + ", encounters " + NemesisSettings.EncounterAmountNames[Config.EncounterAmount] + ", menu " + KeyCombo.Name(Config.RosterMenuKey));
		};
		try
		{
			Directory.CreateDirectory(_saveDir);
		}
		catch (Exception ex)
		{
			StartupTrace.Write("data folder can't be made: " + ex.Message);
		}
		SessionSeed = ((Config.DebugSeed != 0) ? Config.DebugSeed : Seeds.NewSessionSeed());
		_rng = new Random(SessionSeed);
		GrudgeLog($"VERSION enRAGE v{Theme.ModVersion} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
		if (DataFolderNote != null)
		{
			GrudgeLog(DataFolderNote);
		}
		GrudgeLog(string.Format("SEED session {0}{1} - put it in [Debug] Seed to replay this session's rolls", SessionSeed, (Config.DebugSeed != 0) ? " (from [Debug] Seed)" : ""));
		GrudgeLog($"KEYS menu {KeyCombo.Name(Config.RosterMenuKey)} (settings {KeyCombo.SettingsText}, undo {KeyCombo.UndoText}), interrogate {Config.InterrogateKey}, telegraph {Config.TelegraphKey}, controller {Config.PadMenuOpen} ({KeyCombo.PadOpenHint.Item2})");
		if (Config.KeysNote != null)
		{
			GrudgeLog("KEYS " + Config.KeysNote);
			_keysNotice = Config.KeysNote;
		}
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			List<string> list = ApiCheck.Missing(typeof(NemesisMod).Assembly);
			string arg = typeof(Game).Assembly.GetName().Version.ToString();
			if (list.Count == 0)
			{
				GrudgeLog($"API CHECK ScriptHookRDRNetAPI {arg}: every game function the mod calls is there ({stopwatch.ElapsedMilliseconds} ms)");
			}
			else
			{
				GrudgeLog($"API CHECK ScriptHookRDRNetAPI {arg}: {list.Count} game function(s) the mod calls are missing from this copy - the parts of the mod that use them won't work:");
				foreach (string item in list)
				{
					GrudgeLog("API CHECK   missing: " + item);
				}
				_apiNotice = string.Format("{0}: your ScriptHookRDR2DotNet is missing {1} game function(s) the mod uses. Some features may not work. Details in grudges.log (API CHECK).", "enRAGE", list.Count);
			}
		}
		catch (Exception ex2)
		{
			GrudgeLog("API CHECK couldn't run: " + ex2.Message);
		}
		try
		{
			CheckOtherCopies();
		}
		catch (Exception ex3)
		{
			GrudgeLog("COPIES check failed: " + ex3.Message);
		}
		if (_standingDown)
		{
			try
			{
				FlushGrudgeLog(force: true);
				return;
			}
			catch
			{
				return;
			}
		}
		_bulletImpact = new BulletImpactModule(base.BaseDirectory, _saveDir, GetZoneName, NemesisTagFor, FateOf, OnNemesisCrippled);
		_bulletImpact.HandsOff = XRayHandsOff;
		_bulletImpact.AnimalHandsOff = XRayAnimalHandsOff;
		_bulletImpact.SwitchToHierarchy += delegate
		{
			_bulletImpact.CloseViewer();
			_menu.Open();
		};
		StartupTrace.Write("bullet impact ready");
		try
		{
			LoadContent();
		}
		catch (Exception ex4)
		{
			GrudgeLog("ERROR startup content: " + ex4);
		}
		try
		{
			Config.ReadNewFactions();
		}
		catch (Exception ex5)
		{
			GrudgeLog("ERROR startup faction notoriety: " + ex5.Message);
		}
		try
		{
			PruneSideCopies();
		}
		catch
		{
		}
		StartupTrace.Write("content loaded");
		try
		{
			LoadRoster();
		}
		catch (Exception ex6)
		{
			_rosterLoadFailed = true;
			GrudgeLog("ERROR startup roster (saving it is off this session): " + ex6);
		}
		if (!_rosterLoadFailed)
		{
			try
			{
				MoveForeignXRays();
			}
			catch (Exception ex7)
			{
				GrudgeLog("XRAY move-aside failed: " + ex7.Message);
			}
		}
		StartupTrace.Write("roster loaded");
		try
		{
			foreach (NemesisRecord item2 in Roster.Where((NemesisRecord x) => x.PresumedDead && x.History != null && !x.History.Any((HistoryEntry h) => h.Secret || h.Cover)))
			{
				int num = item2.History.FindLastIndex((HistoryEntry h) => h.Text != null && h.Text.Contains("he lived"));
				if (num < 0)
				{
					continue;
				}
				for (int num2 = num; num2 < item2.History.Count; num2++)
				{
					item2.History[num2].Secret = true;
				}
				string text = item2.History[num].Text;
				int num3 = text.IndexOf(" - he lived");
				string text2 = ((num3 > 0) ? text.Substring(0, num3) : text).Replace("YOU LEFT HIM FOR DEAD", "KILLED BY YOU").Replace("LEFT FOR DEAD by ", "KILLED BY ");
				item2.History.Insert(num, new HistoryEntry
				{
					At = item2.History[num].At,
					When = item2.History[num].When,
					Text = text2,
					Color = item2.History[num].Color,
					Cover = true
				});
				if (item2.PresumedDeadAt == 0L && item2.Memories != null && item2.Memories.Count > 0)
				{
					item2.PresumedDeadAt = item2.Memories.Max((Memory m) => m.At);
				}
				_rosterDirty = true;
			}
		}
		catch (Exception ex8)
		{
			GrudgeLog("CHEATDEATH old-save fix failed: " + ex8.Message);
		}
		if (_personalitiesAssigned)
		{
			SaveRoster();
		}
		try
		{
			ResetGangsIfFlagged();
		}
		catch (Exception ex9)
		{
			GrudgeLog("ERROR startup gang reset: " + ex9);
		}
		Downed.Log = GrudgeLog;
		try
		{
			if (Chronicle.Era < 1907 && !Chronicle.HistorySeeded && !Roster.Any((NemesisRecord n) => n.Gang != null) && IsEpiloguePlayer())
			{
				StartWorldIn1907("the first run");
			}
		}
		catch (Exception ex10)
		{
			GrudgeLog("EPILOGUE first-run check failed: " + ex10.Message);
		}
		try
		{
			SeedGangRosters();
		}
		catch (Exception ex11)
		{
			GrudgeLog("ERROR startup gang seeding: " + ex11);
		}
		StartupTrace.Write("gangs seeded");
		if (_seedLonersOnLoad)
		{
			_seedLonersOnLoad = false;
			try
			{
				SeedTestLoners(12);
			}
			catch (Exception ex12)
			{
				GrudgeLog("ERROR startup test loners: " + ex12);
			}
		}
		try
		{
			long val = (from n in Roster
				where n.HateTier == HateTier.Murderous && n.LastCameAt > 0
				select n.LastCameAt).DefaultIfEmpty(-100000L).Max();
			_lastMurderousAt = Math.Max(_lastMurderousAt, val);
		}
		catch
		{
		}
		try
		{
			AssignCamps();
		}
		catch (Exception ex13)
		{
			GrudgeLog("ERROR startup camps: " + ex13);
		}
		StartupTrace.Write("camps assigned");
		try
		{
			EnsureWorldHistory();
		}
		catch (Exception ex14)
		{
			GrudgeLog("HISTORY seed failed: " + ex14);
		}
		StartupTrace.Write("world history done");
		try
		{
			ValidateTimeline();
		}
		catch (Exception ex15)
		{
			GrudgeLog("TIMELINE check failed: " + ex15.Message);
		}
		bool num4 = Crumbs.Previous != null && Crumbs.Previous.Contains("CLEAN EXIT");
		bool crashed = false;
		if (num4)
		{
			GrudgeLog("EXIT the last run ended cleanly (" + Crumbs.Previous.Substring(Crumbs.Previous.IndexOf("CLEAN EXIT")) + ")");
		}
		else if (Crumbs.Previous != null)
		{
			string text3 = ExitVerdict(Crumbs.Previous, out crashed);
			GrudgeLog("EXIT " + text3);
			if (crashed)
			{
				Crumbs.SaveCrash($"enRAGE v{Theme.ModVersion} - the run before the load at {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {text3}");
			}
		}
		if (crashed)
		{
			GrudgeLog("CRASH breadcrumb: the last run stopped in: " + Crumbs.Previous + " (all of it: lastcrash.txt)");
		}
		if (crashed && Crumbs.PreviousCalls != null)
		{
			GrudgeLog("CRASH last game calls (oldest first): " + Crumbs.PreviousCalls);
		}
		StartupTrace.Write("timeline checked");
		try
		{
			FixFemaleNames();
		}
		catch (Exception ex16)
		{
			GrudgeLog("NAMING female fix failed: " + ex16.Message);
		}
		try
		{
			FixSoldierNames();
		}
		catch (Exception ex17)
		{
			GrudgeLog("NAMING soldier fix failed: " + ex17.Message);
		}
		try
		{
			ReviveFellMen();
		}
		catch (Exception ex18)
		{
			GrudgeLog("SIGHTING repair failed: " + ex18.Message);
		}
		try
		{
			FixTheKlanOnce();
		}
		catch (Exception ex19)
		{
			GrudgeLog("NAMING Klan text fix failed: " + ex19.Message);
		}
		try
		{
			RenameMobOnce();
		}
		catch (Exception ex20)
		{
			GrudgeLog("NAMING mob rename failed: " + ex20.Message);
		}
		try
		{
			FixKinSurnames();
		}
		catch (Exception ex21)
		{
			GrudgeLog("NAMING kin surname fix failed: " + ex21.Message);
		}
		try
		{
			MoveLaramieModels();
		}
		catch (Exception ex22)
		{
			GrudgeLog("NAMING Laramie model move failed: " + ex22.Message);
		}
		try
		{
			CollapsePunchRuns();
		}
		catch (Exception ex23)
		{
			GrudgeLog("HISTORY punch fold failed: " + ex23.Message);
		}
		StartupTrace.Write("name fixes done");
		if (Config.LoadedVersion < 20)
		{
			try
			{
				int num5 = 0;
				foreach (Acquaintance item3 in People.Where((Acquaintance x) => x.Fear > 12 && (x.Witnessed == null || !x.Witnessed.Any((string w) => w.StartsWith("saw you")))))
				{
					item3.Fear = 12;
					num5++;
				}
				if (num5 > 0)
				{
					_peopleDirty = true;
					GrudgeLog($"WITNESS one-time fix: {num5} people who only heard about you capped at fear 12");
				}
			}
			catch (Exception ex24)
			{
				GrudgeLog("WITNESS cap failed: " + ex24.Message);
			}
		}
		if (Config.LoadedVersion < 19)
		{
			try
			{
				int num6 = 0;
				foreach (Acquaintance item4 in People.Where((Acquaintance x) => x.Witnessed != null && x.Witnessed.Any((string w) => w.StartsWith("heard what you did")) && x.Witnessed.Any((string w) => w.StartsWith("saw you"))))
				{
					item4.Witnessed.RemoveAll((string w) => w.StartsWith("heard what you did"));
					item4.Fear = Math.Max(0, item4.Fear - 20);
					num6++;
				}
				if (num6 > 0)
				{
					_peopleDirty = true;
					GrudgeLog($"WITNESS one-time fix: {num6} witnesses lose the +20 'heard about it' they got before their own fear");
				}
			}
			catch (Exception ex25)
			{
				GrudgeLog("WITNESS fix failed: " + ex25.Message);
			}
		}
		if (Config.LoadedVersion < 18)
		{
			try
			{
				ThinNicknames();
			}
			catch (Exception ex26)
			{
				GrudgeLog("NAMING nickname thinning failed: " + ex26.Message);
			}
		}
		try
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == "d945c5e4c71f" && n.KillLocation == "Saint Denis" && n.Origin != GrudgeOrigin.GangMember);
			if (nemesisRecord != null)
			{
				nemesisRecord.KillLocation = "Rhodes";
				SaveRoster();
				GrudgeLog("FIX " + nemesisRecord.Name + ": his grudge began in Rhodes (was Saint Denis, his outfit's home town)");
			}
		}
		catch (Exception ex27)
		{
			GrudgeLog("FIX grudge town failed: " + ex27.Message);
		}
		StartupTrace.Write("ctor done - handlers next");
		base.Tick += delegate(object o, EventArgs e)
		{
			if (ActiveCopy())
			{
				Guard("tick", delegate
				{
					OnTick(o, e);
				});
			}
		};
		base.KeyDown += delegate(object o, KeyEventArgs e)
		{
			_keyQueue.Enqueue((true, e));
		};
		base.KeyUp += delegate(object s, KeyEventArgs e)
		{
			_keyQueue.Enqueue((false, e));
		};
		base.Aborted += OnAborted;
		UiMouse.SendKey = delegate(Keys k)
		{
			OnKeyDown(null, new KeyEventArgs((Keys)((int)k | (((Control.ModifierKeys & Keys.Shift) != Keys.None) ? 65536 : 0))));
		};
		UiMouse.TabClick = GoToTab;
		SoundLog = GrudgeLog;
		CampSite.OwnerOf = delegate(CampSite s)
		{
			try
			{
				return SiteOwner(s);
			}
			catch
			{
				return (string)null;
			}
		};
		base.Interval = 0;
		_nextAmbushCheck = Game.GameTime + 60000;
		int num7 = Roster.Count((NemesisRecord n) => n.IsAlive && !n.PresumedDead && n.Origin != GrudgeOrigin.GangMember);
		GrudgeLog(string.Format("LOAD {0} {1} a grudge against you", num7, (num7 == 1) ? "man holds" : "people hold"));
		_loadNotice = "enRAGE v" + Theme.ModVersion + " loaded - " + KeyCombo.MenuText + ((Config.PadMenuOpen < 4) ? (" or " + Config.PadMenuText) : "") + " for the menu";
	}

	internal bool SafeToDelete(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return false;
		}
		if (!BelongsToUs(p.Handle))
		{
			return false;
		}
		if (p.IsHogtied || ENTITY.IS_ENTITY_ATTACHED(p.Handle))
		{
			return false;
		}
		if (_interro?.Ped != null && _interro.Ped.Exists() && _interro.Ped.Handle == p.Handle)
		{
			return false;
		}
		if (_interroMemory.ContainsKey(p.Handle))
		{
			return false;
		}
		return p.Position.DistanceTo(Game.Player.Character.Position) > 30f;
	}

	[DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
	private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcMem mem, uint size);

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	private static string MemoryLine()
	{
		string text = $"mod {(double)GC.GetTotalMemory(forceFullCollection: false) / 1048576.0:0} MB";
		try
		{
			ProcMem mem = new ProcMem
			{
				cb = (uint)Marshal.SizeOf(typeof(ProcMem))
			};
			if (!GetProcessMemoryInfo(GetCurrentProcess(), ref mem, mem.cb))
			{
				return text;
			}
			return $"game RAM {gb(mem.WorkingSetSize):0.0} GB (peak {gb(mem.PeakWorkingSetSize):0.0}, committed {gb(mem.PrivateUsage):0.0}) | {text}";
		}
		catch
		{
			return text;
		}
		static double gb(UIntPtr v)
		{
			return (double)v.ToUInt64() / 1073741824.0;
		}
	}

	private bool WorldBusy(Ped player)
	{
		if (Game.GameTime < _nextBusyCheck)
		{
			if (!_busy)
			{
				return Game.GameTime < _quietUntil;
			}
			return true;
		}
		_nextBusyCheck = Game.GameTime + 250;
		int handle = Game.Player.Handle;
		bool flag = player.IsDead || !PLAYER.IS_PLAYER_CONTROL_ON(handle) || PLAYER.IS_PLAYER_BEING_ARRESTED(handle, atArresting: false) || CAM.IS_SCREEN_FADED_OUT() || CAM.IS_SCREEN_FADING_OUT();
		if (flag)
		{
			_quietUntil = Math.Max(_quietUntil, Game.GameTime + ((Game.GameTime - _lastJumpAt < 30000) ? 5000 : 2000));
		}
		if (player.IsDead || CAM.IS_SCREEN_FADED_OUT() || CAM.IS_SCREEN_FADING_OUT())
		{
			_lastFadeAt = Game.GameTime;
		}
		if (flag)
		{
			QuietWhy = (player.IsDead ? "dead" : (PLAYER.IS_PLAYER_BEING_ARRESTED(handle, atArresting: false) ? "arrest" : ((CAM.IS_SCREEN_FADED_OUT() || CAM.IS_SCREEN_FADING_OUT()) ? "fade" : "no control")));
		}
		else if (Game.GameTime < _quietUntil)
		{
			QuietWhy = "settling after " + QuietWhy.Replace("settling after ", "");
		}
		_busy = flag;
		if (!flag)
		{
			return Game.GameTime < _quietUntil;
		}
		return true;
	}

	private void OnTick(object sender, EventArgs e)
	{
		NoteFrame();
		WritePerf();
		if (Game.GameTime >= _nextHeartbeat)
		{
			_nextHeartbeat = Game.GameTime + 60000;
			GrudgeLog($"ALIVE {DateTime.Now:yyyy-MM-dd} roster {Roster.Count((NemesisRecord n) => n.IsAlive)} | strangers {_provoked.Count} | named {_named.Count} | errors {ErrorCount} | {MemoryLine()}");
		}
		Crumbs.Mark("tick start");
		if (StartupTrace.TicksLeft > 0)
		{
			StartupTrace.Write("tick " + (3 - StartupTrace.TicksLeft) + " start");
		}
		Ped player = Game.Player.Character;
		UiDraw.BeginFrame();
		Guard("menu pause", UpdateMenuPause);
		Guard("game events", delegate
		{
			CheckGameEventNear(Game.Player.Character);
		});
		Guard("mouse", delegate
		{
			UiMouse.Update(MouseMenuOpen);
		});
		Guard("gun-melee tick", delegate
		{
			GunMelee.Tick(player);
		});
		Guard("bullet impact tick", delegate
		{
			_bulletImpact.Tick(player);
		});
		if (player.IsDead && !_playerWasDead)
		{
			_playerWasDead = true;
			Guard("player death", delegate
			{
				HandlePlayerDeath(player);
			});
		}
		else if (!player.IsDead && _playerWasDead)
		{
			_playerWasDead = false;
			Guard("respawn", WipeWorldAfterRespawn);
		}
		Guard("calm", delegate
		{
			UpdateCalm(player);
		});
		Guard("save points", delegate
		{
			UpdateSavePoints(player);
		});
		bool quiet = WorldBusy(player);
		if (quiet != _wasQuiet)
		{
			_wasQuiet = quiet;
			if (!quiet)
			{
				_quietEndedAt = Game.GameTime;
			}
			Guard("quiet scene", delegate
			{
				NoteQuietForScene(player, quiet);
			});
			GrudgeLog(quiet ? "QUIET world scans paused (no control / arrest / death / fade)" : "QUIET over - scans resume");
			if (quiet)
			{
				Guard("quiet prompts", delegate
				{
					QuietPrompts("the world went quiet");
				});
			}
			if (quiet && _tg != null)
			{
				Guard("quiet telegraph", delegate
				{
					CloseTelegraph();
					GrudgeLog("TELEGRAPH closed - the game took over");
				});
			}
			if (quiet && (_menu.IsOpen || _bulletImpact.ViewerOpen))
			{
				Guard("quiet menus", delegate
				{
					_menu.Close();
					_bulletImpact.CloseViewer();
					UpdateMenuPause();
					GrudgeLog("MENU closed - the game took over");
				});
			}
		}
		if (_loadNotice != null && !quiet && Game.GameTime - _loadedAt > 3000)
		{
			ShowNotification(_loadNotice);
			_loadNotice = null;
		}
		if (_apiNotice != null && !quiet && Game.GameTime - _loadedAt > 8000)
		{
			ShowNotification(_apiNotice);
			_apiNotice = null;
		}
		if (_copiesNotice != null && !_copiesWarning.HasValue && !quiet && Game.GameTime - _loadedAt > 10000)
		{
			ShowNotification(_copiesNotice);
			_copiesNotice = null;
		}
		if (SafeFile.PendingNotice != null && !quiet && Game.GameTime - _loadedAt > 5000)
		{
			ShowNotification("enRAGE: " + SafeFile.PendingNotice);
			SafeFile.PendingNotice = null;
		}
		if (_keysNotice != null && !quiet && Game.GameTime - _loadedAt > 6000)
		{
			ShowNotification("enRAGE: " + _keysNotice);
			_keysNotice = null;
		}
		if (Game.GameTime >= _nextIniLook)
		{
			_nextIniLook = Game.GameTime + 2000;
			Guard("settings file", WatchSettingsFile);
		}
		bool flag = Config.WelcomeSeen && Config.WhatsNewSeen != Theme.ModVersion;
		if (_copiesWarning.HasValue && !_welcome.IsOpen && !quiet && Game.GameTime - _loadedAt > 4000 && !MISC.GET_MISSION_FLAG() && !_menu.IsOpen && !_bulletImpact.ViewerOpen && _convo == null && _tg == null && !_iniPanel.IsOpen && PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle) && WelcomeCalm())
		{
			OpenCopiesWarning();
		}
		if ((!Config.WelcomeSeen || !Config.SetupSeen || flag) && !_welcomeTried && !_welcome.IsOpen && !quiet && Game.GameTime - _loadedAt > 4000 && !MISC.GET_MISSION_FLAG() && !_menu.IsOpen && !_bulletImpact.ViewerOpen && _convo == null && _tg == null && !_iniPanel.IsOpen && PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle) && WelcomeCalm())
		{
			_welcomeTried = true;
			if (!Config.SetupSeen)
			{
				_welcome.OpenSetup(!Config.WelcomeSeen, flag);
				GrudgeLog("SETUP opened (first load)");
			}
			else if (!Config.WelcomeSeen)
			{
				_welcome.Open();
				GrudgeLog("WELCOME guide opened (first load)");
			}
			else
			{
				_welcome.OpenWhatsNew();
				GrudgeLog("WHATSNEW shown for " + Theme.ModVersion);
			}
		}
		Guard("lock prompt", CheckLockPrompt);
		Guard("landing", TickLanding);
		if (quiet && _saloonStep != 0 && SaloonMayRunQuiet(player))
		{
			Guard("saloon fight (no control)", delegate
			{
				UpdateSaloonFight(player);
			});
		}
		CrippleTracker.Hold = quiet;
		Guard("events", delegate
		{
			PollInteractionEvents(player);
		});
		if (!quiet)
		{
			Guard("witnesses", WitnessQueueTick);
			Guard("grudges", delegate
			{
				UpdateGrudges(player);
			});
			Guard("world nemeses", delegate
			{
				UpdateWorldNemeses(player);
			});
			Guard("general", delegate
			{
				UpdateGeneral(player);
			});
			Guard("region", delegate
			{
				UpdateRegion(player);
			});
			Guard("theft", delegate
			{
				UpdateTheft(player);
			});
			Guard("naming", delegate
			{
				UpdateNaming(player);
			});
			Guard("settling", UpdateSettling);
			Guard("sightings", delegate
			{
				UpdateTownSightings(player);
			});
			Guard("town spots", delegate
			{
				TownSpotTick(player);
			});
			if (WorldClockSettled)
			{
				Guard("camps", delegate
				{
					UpdateCamps(player);
				});
			}
			if (WorldClockSettled)
			{
				Guard("treasuries", delegate
				{
					UpdateTreasuries(player);
				});
			}
			Guard("fair game", delegate
			{
				UpdateFairGame(player);
			});
			if (WorldClockSettled)
			{
				Guard("supply camps", delegate
				{
					UpdateSupplyCamps(player);
				});
			}
			if (WorldClockSettled)
			{
				Guard("supply wagons", delegate
				{
					UpdateSupplyWagons(player);
				});
			}
			Guard("self-check", delegate
			{
				UpdateSelfCheck(player);
			});
			Guard("train robbery", delegate
			{
				UpdateTrainRobbery(player);
			});
			Guard("patrols", delegate
			{
				UpdateRaids(player);
			});
			Guard("staged", RunStaged);
			Guard("aftermath", delegate
			{
				UpdateAftermath(player);
			});
			if (WorldClockSettled)
			{
				Guard("people", delegate
				{
					UpdatePeople(player);
				});
			}
			Guard("klan honor", KlanHonorTick);
			Guard("unmasked", delegate
			{
				UpdateUnmasking(player);
			});
			Guard("pending exits", RunPendingExits);
			Guard("groups", delegate
			{
				UpdateGroupWatch(player);
			});
			if (WorldClockSettled)
			{
				Guard("history", UpdateHistory);
				Guard("minds", UpdateMinds);
				Guard("death news", UpdateDeathNews);
				Guard("war zones", UpdateWarZones);
			}
			Guard("saloon fight", delegate
			{
				UpdateSaloonFight(player);
			});
			Guard("map settled", RunPendingMap);
			Guard("town reputation", UpdateTownRepBlips);
			Guard("greet scenes", delegate
			{
				UpdateGreetScenes(player);
			});
			Guard("voices", delegate
			{
				UpdateVoiceProbe(player);
				UpdateGreetReplies(player);
				UpdateVoiceCensus(player);
				UpdateModelVoices(player);
			});
			Guard("fight over", delegate
			{
				UpdateFightOver(player);
			});
			Guard("enemy dots", delegate
			{
				UpdateEnemyDots(player);
			});
			Guard("spawner", delegate
			{
				RunSpawnMenuRequests(player);
			});
			if (_forceWar != 0)
			{
				int fw = _forceWar;
				_forceWar = 0;
				Guard("test war", delegate
				{
					TestWarCard(fw, player);
				});
			}
			if (_forcePatrol != 0)
			{
				int fp = _forcePatrol;
				_forcePatrol = 0;
				Guard("test patrol", delegate
				{
					if (fp == 1)
					{
						if (!SpawnRoadPatrol(player, force: true))
						{
							ShowNotification("No room for a patrol here.");
						}
					}
					else
					{
						LiveCamp liveCamp = _liveCamps.Values.OrderBy((LiveCamp c) => SitePos(c.Site).DistanceTo(player.Position)).FirstOrDefault();
						if (liveCamp == null)
						{
							ShowNotification("No camp is live near you - ride within 170 m of one first.");
						}
						else
						{
							SpawnRaid(liveCamp, player);
						}
					}
				});
			}
			Guard("interrogation", delegate
			{
				UpdateInterrogation(player);
			});
			Guard("social", delegate
			{
				UpdateSocial(player);
			});
			Guard("gestures", delegate
			{
				UpdateGestureSystems(player);
			});
			UiFeed.Breadcrumb = delegate(string line)
			{
				GrudgeLog(line);
				FlushGrudgeLog(force: true);
			};
		}
		Guard("prune", PruneProvocations);
		Guard("entities", PruneEntities);
		Guard("ped cap", CapOurPeds);
		if (Game.FrameCount % 300 == 0)
		{
			Guard("prune followers", PruneFollowerMap);
		}
		Guard("active nemesis", delegate
		{
			if (_activeNemesisPed != null && ActiveRecord != null)
			{
				if (_activeNemesisPed.Exists())
				{
					Downed.Saw(_activeNemesisPed, LooksKnockedOut(_activeNemesisPed));
					NoteHeld(_activeNemesisPed);
				}
				if (!_activeNemesisPed.Exists() || _activeNemesisPed.Position.DistanceTo(player.Position) > 350f)
				{
					if (_enc == null || _enc.Engaged)
					{
						GrudgeNotice(ActiveRecord.Name + " slipped away. He'll remember this.");
					}
					if (_enc == null || _enc.Engaged)
					{
						_encEnd = ("GOT AWAY", "he slipped away");
					}
					EndEncounter();
					ReleaseActive();
				}
				else if (_activeNemesisPed.IsDead && !LooksKnockedOut(_activeNemesisPed))
				{
					ResolveNemesisDeath(_activeNemesisPed, ActiveRecord, Executed(_activeNemesisPed.Handle));
					EndEncounter();
					ReleaseActive();
				}
				else if (_activeNemesisPed.IsDead && !ActiveNemesisKnockedOut() && !KoByYou(_activeNemesisPed, player))
				{
					_knockedOutWorld.Add(_activeNemesisPed.Handle);
					_encEnd = ("GOT AWAY", "someone else knocked him out cold");
					EndEncounter();
					ReleaseActive();
				}
				else if (_activeNemesisPed.IsDead || ActiveNemesisKnockedOut())
				{
					NemesisRecord activeRecord = ActiveRecord;
					_knockedOutWorld.Add(_activeNemesisPed.Handle);
					int hate = activeRecord.Hate;
					int fear = activeRecord.Fear;
					activeRecord.TimesKnockedOutByPlayer++;
					activeRecord.Touch();
					KnockoutReaction(activeRecord);
					Feed("KNOCKED OUT " + activeRecord.Name + ": hate " + Delta(hate, activeRecord.Hate) + "  fear " + Delta(fear, activeRecord.Fear), Theme.Warn, activeRecord);
					activeRecord.AddYourNotoriety(10);
					SaveRoster();
					GrudgeNotice("You knocked " + activeRecord.Name + " out cold. He'll wake up angrier.");
					_encEnd = ("KNOCKED OUT", $"you knocked him out cold - hate {hate} -> {activeRecord.Hate}, fear {fear} -> {activeRecord.Fear}");
					EndEncounter();
					ReleaseActive();
				}
				else
				{
					DetectAntagonizeFallback();
				}
			}
		});
		Guard("encounter roll", delegate
		{
			bool flag3 = _activeNemesisPed == null && !player.IsDead && !_menu.IsOpen && !_bulletImpact.ViewerOpen && !_busy && Game.GameTime >= _quietUntil;
			if (_forceAmbushRequested || _summonRequested != null)
			{
				NemesisRecord summonRequested = _summonRequested;
				_forceAmbushRequested = false;
				_summonRequested = null;
				List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.CanHunt).ToList();
				string spawnForce = _spawnForce;
				_spawnForce = null;
				if (!(summonRequested == null && flag3) || spawnForce != null || (_currentTown == null && TownAt(player.Position) == null) || !SpawnTownHunter(player))
				{
					if (_activeNemesisPed != null)
					{
						ShowNotification(ActiveRecord.Name + " is already hunting you.");
					}
					else if (summonRequested != null && summonRequested.Paralyzed)
					{
						ShowNotification(summonRequested.Name + " can't ride after anyone anymore.");
					}
					else if (list.Count == 0)
					{
						ShowNotification("No nemesis left who can come after you. Go lose a gunfight.");
					}
					else if (flag3 && summonRequested != null)
					{
						SummonWithDot(summonRequested, (_currentTown != null) ? EncounterKind.Callout : EncounterKind.Idle, player);
					}
					else
					{
						if (flag3)
						{
							_spawnForce = spawnForce;
							try
							{
								RunRandomEncounter(player, test: true);
								return;
							}
							finally
							{
								_spawnForce = null;
							}
						}
						ShowNotification("Not now - the world is settling (fade, load, menu or no control).");
					}
				}
			}
			else if (Config.TensionEnabled)
			{
				UpdateTension(player, flag3);
			}
			else if (Game.GameTime > _nextAmbushCheck)
			{
				bool flag4 = _currentTown != null || TownAt(player.Position) != null;
				_nextAmbushCheck = Game.GameTime + (flag4 ? (Balance.TownEncounterSeconds * 1000) : EncounterIntervalMs());
				if (Config.RandomAmbushes && flag3 && _rng.Next(100) < (flag4 ? Balance.TownEncounterChance : Config.AmbushChancePercent))
				{
					RunRandomEncounter(player, test: false);
				}
			}
		});
		if (!quiet)
		{
			Guard("encounter", delegate
			{
				UpdateEncounter(player);
			});
			Guard("conversation", delegate
			{
				UpdateConvo(player);
			});
			if (WorldClockSettled)
			{
				Guard("telegraph", delegate
				{
					UpdateTelegraph(player);
				});
			}
			Guard("tack", UpdateTack);
			Guard("gamesystems", delegate
			{
				UpdateGameSystems(player);
			});
			if (WorldClockSettled)
			{
				Guard("leaders", delegate
				{
					UpdateLeaders(player);
				});
			}
			Guard("companion", delegate
			{
				UpdateCompanion(player);
			});
			Guard("recruits", delegate
			{
				UpdateRecruits(player);
			});
			if (WorldClockSettled)
			{
				Guard("story", delegate
				{
					if (!_storyApplied)
					{
						_storyApplied = true;
						ApplyStoryChapter(announce: false);
					}
					UpdateStoryProgress(player);
					RhodesBanTick(player);
				});
			}
			Guard("his people", delegate
			{
				HisPeopleAfterTick(player);
			});
			Guard("law test", delegate
			{
				LawTestTick(player);
			});
			Guard("outlaw groups", delegate
			{
				UpdateOutlawGroups(player);
			});
			Guard("skirmish", delegate
			{
				UpdateSkirmish(player);
			});
			Guard("bands", delegate
			{
				UpdateBands(player);
			});
			if (WorldClockSettled)
			{
				Guard("checkpoint", delegate
				{
					UpdateCheckpoint(player);
				});
			}
			Guard("road wagon", delegate
			{
				UpdateRoadWagon(player);
			});
			if (WorldClockSettled)
			{
				Guard("distant battle", delegate
				{
					UpdateDistantBattle(player);
				});
			}
			if (WorldClockSettled)
			{
				Guard("spared", delegate
				{
					UpdateLeaving(player);
				});
			}
			Guard("hunters", delegate
			{
				UpdateHunters(player);
			});
			Guard("rival", delegate
			{
				UpdateRival(player);
			});
		}
		else if (_convo != null)
		{
			Guard("conversation end", delegate
			{
				EndConvo("interrupted");
			});
		}
		if (WorldClockSettled)
		{
			Guard("succession", UpdateSuccession);
			if (!quiet)
			{
				Guard("world sim", delegate
				{
					UpdateWorldSim(player);
				});
			}
			if (!quiet)
			{
				Guard("plans", delegate
				{
					UpdatePlans(player);
				});
			}
			Guard("missions", delegate
			{
				UpdateMissionDeeds(player);
			});
		}
		Guard("succession card", DrawSuccessionCard);
		if (!quiet)
		{
			Guard("name cards", delegate
			{
				DrawNameCards(player);
			});
		}
		if (!quiet)
		{
			Guard("war hud", delegate
			{
				DrawWarHud(player);
			});
		}
		if (_iniPanel.IsOpen)
		{
			Game.DisableAllControlsThisFrame();
		}
		Guard("settings panel", delegate
		{
			_iniPanel.Draw();
		});
		if (_welcome.IsOpen)
		{
			Game.DisableAllControlsThisFrame();
			HUD.HIDE_HUD_AND_RADAR_THIS_FRAME();
		}
		Guard("welcome", delegate
		{
			_welcome.Draw();
		});
		if (_paperShown != null)
		{
			Game.DisableAllControlsThisFrame();
			HUD.HIDE_HUD_AND_RADAR_THIS_FRAME();
			if (player.IsDead || player.IsInCombat || (_enc != null && _enc.Engaged))
			{
				GrudgeLog("PAPER put down - trouble");
				_paperShown = null;
			}
		}
		Guard("newspaper", DrawNewspaper);
		Guard("spawn menu", DrawSpawnMenu);
		FlushSaves();
		if (!quiet)
		{
			Guard("feed", UiFeed.Tick);
		}
		Guard("debug", delegate
		{
			if (_debugOn)
			{
				DrawDebug(player);
			}
		});
		if (_iconBlips.Count > 0)
		{
			Guard("map icons", MapIconsTick);
		}
		Guard("keys", DrainKeys);
		Guard("dpad up", HoldDpadUp);
		Guard("gamepad", PollGamepad);
		Crumbs.Mark("menu pause (end)");
		Guard("menu pause", UpdateMenuPause);
		Crumbs.Mark("xray draw");
		Guard("tab hold", UpdateTabHold);
		Guard("search", UpdateSearch);
		if (_search != null)
		{
			Guard("search draw", DrawSearch);
		}
		else if (!_menu.IsOpen)
		{
			_bulletImpact.Draw();
		}
		if (_menu.IsOpen && _search == null)
		{
			Game.DisableAllControlsThisFrame();
			Guard("menu draw", DrawMenu);
		}
		Crumbs.Mark("mouse");
		Guard("mouse end", UiMouse.EndFrame);
		bool flag2 = false;
		try
		{
			flag2 = HUD.IS_PAUSE_MENU_ACTIVE();
		}
		catch
		{
		}
		Crumbs.Mark(flag2 ? "tick end - PAUSE MENU OPEN" : "tick end (outside the mod: another script or the game)");
		if (StartupTrace.TicksLeft > 0)
		{
			StartupTrace.Write("tick " + (3 - StartupTrace.TicksLeft--) + " end");
		}
	}

	private void HandlePlayerDeath(Ped player)
	{
		int num = PED.GET_PED_SOURCE_OF_DEATH(player.Handle);
		Ped ped = ((num != 0) ? (Entity.FromHandle(num) as Ped) : null);
		NemesisRecord nemesisRecord = null;
		NemesisRecord value;
		NemesisRecord value2;
		if (ped != null && _activeNemesisPed != null && ped.Handle == _activeNemesisPed.Handle)
		{
			nemesisRecord = ActiveRecord;
		}
		else if (ped != null && _worldNemeses.TryGetValue(ped.Handle, out value) && Roster.Contains(value))
		{
			nemesisRecord = value;
		}
		else if (ped != null && _followerOf.TryGetValue(ped.Handle, out value2) && Roster.Contains(value2))
		{
			nemesisRecord = value2;
		}
		if (nemesisRecord != null)
		{
			NemesisRecord nemesisRecord2 = nemesisRecord;
			if (nemesisRecord2.Origin == GrudgeOrigin.GangMember)
			{
				nemesisRecord2.Origin = GrudgeOrigin.Killed;
				nemesisRecord2.KillLocation = GetZoneName(player.Position);
			}
			nemesisRecord2.TimesKilledPlayer++;
			nemesisRecord2.Touch();
			NoteOutcome(nemesisRecord2, "killed you");
			CheckSkills(nemesisRecord2, Loadout.For(nemesisRecord2).Band.ToString());
			Remember(nemesisRecord2, "Pride", 65, "killed you");
			if (Mind.HasAgainstYou(nemesisRecord2, "Prove"))
			{
				nemesisRecord2.Goals.RemoveAll((Goal g) => g.Type == "Prove");
				Remember(nemesisRecord2, "Joy", 50, "proved himself on you");
			}
			nemesisRecord2.KilledPlayerAt = DateTime.Now;
			int hate = nemesisRecord2.Hate;
			nemesisRecord2.AddHate(10);
			bool flag = nemesisRecord2.Gang == null && nemesisRecord2.Rank < 5;
			if (flag)
			{
				nemesisRecord2.Rank++;
			}
			else if (nemesisRecord2.Gang != null)
			{
				GainRankXp(nemesisRecord2, 3, "killed you");
			}
			History(nemesisRecord2, "KILLED YOU near " + GetZoneName(player.Position) + ": hate " + Delta(hate, nemesisRecord2.Hate) + (flag ? $"  -> rank {nemesisRecord2.Rank}" : ""), Theme.Blood);
			WorldPart worldPart = PartFor(nemesisRecord2, "KILLED YOU", nemesisRecord2.Title);
			worldPart.Effects.Add($"hate {hate} -> {nemesisRecord2.Hate}");
			if (flag)
			{
				worldPart.Effects.Add($"rank {nemesisRecord2.Rank}");
			}
			ChronicleDeed("died", nemesisRecord2.Name + " killed you", "near " + GetZoneName(player.Position), player.Position, nemesisRecord2.Gang, worldPart);
			SaveRoster();
			GrudgeNotice(flag ? $"{nemesisRecord2.FullName} got you again. His legend grows: Rank {nemesisRecord2.Rank}." : (nemesisRecord2.FullName + " got you again."));
			ReleaseActive();
		}
		else
		{
			if (ped == null || ped.IsPlayer || !ped.IsHuman)
			{
				return;
			}
			bool flag2 = MissionMemoryOk(ped);
			if (Kids.IsChild(ped) || IsStoryPed(ped) || IsCompanion(ped.Handle) || (GameOwned(ped) && !flag2) || IsLawman(ped) || (MISC.GET_MISSION_FLAG() && !flag2))
			{
				GrudgeLog($"KILLER ped={ped.Handle} is not nemesis material (child / story / companion / mission / law) -> no nemesis");
				return;
			}
			string zoneName = GetZoneName(player.Position);
			_provoked.TryGetValue(ped.Handle, out var value3);
			_provoked.Remove(ped.Handle);
			if (PlayerMasked(player) && (value3 == null || !value3.SawFace))
			{
				DisguiseSaves++;
				GrudgeLog($"KILLER ped={ped.Handle} never saw your face (masked) -> no nemesis");
				Feed("KILLED BY " + WhoIs(ped.Handle) + ": never saw your face (masked) -> no nemesis", Theme.Good);
				return;
			}
			NemesisRecord nemesisRecord3 = NewRecord((uint)ped.Model.Hash, zoneName);
			nemesisRecord3.TimesKilledPlayer = 1;
			nemesisRecord3.Touch();
			StartRest(nemesisRecord3);
			if (_named.TryGetValue(ped.Handle, out var value4))
			{
				nemesisRecord3.Name = value4.Name;
				_named.Remove(ped.Handle);
			}
			nemesisRecord3.HasOrigin = true;
			nemesisRecord3.OriginX = player.Position.X;
			nemesisRecord3.OriginY = player.Position.Y;
			nemesisRecord3.OriginZ = player.Position.Z;
			nemesisRecord3.Origin = OriginForKiller(value3);
			nemesisRecord3.TimesAntagonized = value3?.Insults ?? 0;
			nemesisRecord3.TimesGreeted = value3?.Greetings ?? 0;
			if (value3 != null)
			{
				nemesisRecord3.Personality = value3.Temper;
				CopyTraits(value3.Shadow, nemesisRecord3);
				FitKinToLife(nemesisRecord3);
			}
			nemesisRecord3.Hate = Math.Max(Hate.Initial(nemesisRecord3.Origin), Hate.MurderousAt);
			if (value3 != null && value3.Ledger.Count > 0)
			{
				History(nemesisRecord3, "as a stranger: " + string.Join(", ", value3.Ledger), Theme.Dim);
			}
			History(nemesisRecord3, $"KILLED YOU near {zoneName} and became your nemesis ({nemesisRecord3.Origin}): hate {nemesisRecord3.Hate}", Theme.Blood);
			Roster.Add(nemesisRecord3);
			_bulletImpact.TagVictim(ped.Handle, nemesisRecord3.Id, nemesisRecord3.FullName ?? "");
			ForgetPerson(ped.Handle, nemesisRecord3.Name);
			WorldPart worldPart2 = PartFor(nemesisRecord3, "KILLED YOU", "a new nemesis");
			worldPart2.Effects.Add($"hate {nemesisRecord3.Hate}");
			ChronicleDeed("died", nemesisRecord3.Name + " killed you - and swore to do it again", "near " + zoneName, player.Position, nemesisRecord3.Gang, worldPart2);
			SaveRoster();
			string text = ((nemesisRecord3.Origin == GrudgeOrigin.Wounded) ? "You shot him and left him breathing. He didn't return the favor." : ((nemesisRecord3.Origin == GrudgeOrigin.Slashed) ? "You cut him. He answered with lead." : ((nemesisRecord3.Origin == GrudgeOrigin.HorseKilled) ? "You killed his horse. He killed you." : ((nemesisRecord3.Origin == GrudgeOrigin.Kidnapped) ? "You hogtied him. He got loose." : ((nemesisRecord3.Origin == GrudgeOrigin.Robbed) ? "You robbed him. He took your life instead." : ((nemesisRecord3.Origin == GrudgeOrigin.KnockedOut) ? "You knocked him out. He came to, and came for you." : ((nemesisRecord3.Origin == GrudgeOrigin.Beaten) ? "You beat him with your fists. He answered with lead." : ((nemesisRecord3.Origin == GrudgeOrigin.Insulted) ? "You ran your mouth; he answered with lead." : ((nemesisRecord3.Origin == GrudgeOrigin.Greeted) ? "You tipped your hat. He didn't." : ("He killed you near " + zoneName + "."))))))))));
			GrudgeNotice("A nemesis rises: " + nemesisRecord3.FullName + ". " + text);
		}
	}

	private void ResetGangsIfFlagged()
	{
		string path = Path.Combine(_saveDir, "reset-xray.flag");
		if (File.Exists(path))
		{
			try
			{
				_bulletImpact.ClearXRayLog();
				File.Delete(path);
				ShowNotification("X-ray log cleared.");
			}
			catch (Exception ex)
			{
				ShowNotification("X-ray reset failed: " + ex.Message);
			}
		}
		string path2 = Path.Combine(_saveDir, "forget.flag");
		if (File.Exists(path2))
		{
			try
			{
				List<string> keys = (from l in File.ReadAllLines(path2)
					select l.Trim() into l
					where l.Length > 0
					select l).ToList();
				List<NemesisRecord> list = Roster.Where((NemesisRecord r) => keys.Any((string k) => string.Equals(k, r.Name, StringComparison.OrdinalIgnoreCase) || k == r.Id)).ToList();
				foreach (NemesisRecord item in list)
				{
					RemoveRecord(item, "forget.flag");
				}
				int count = list.Count;
				File.Delete(path2);
				if (count > 0)
				{
					SaveRoster();
					ShowNotification($"Forgot {count} nemesis record(s).");
				}
			}
			catch (Exception ex2)
			{
				ShowNotification("Forget failed: " + ex2.Message);
			}
		}
		string path3 = Path.Combine(_saveDir, "seed-loners.flag");
		if (File.Exists(path3))
		{
			try
			{
				File.Delete(path3);
				_seedLonersOnLoad = true;
			}
			catch
			{
			}
		}
		string path4 = Path.Combine(_saveDir, "reset-all.flag");
		if (File.Exists(path4))
		{
			try
			{
				int num = ResetEverything();
				File.Delete(path4);
				ShowNotification($"Fresh start: {num} records cleared, notoriety wiped. New faction rosters are being drawn up.");
				return;
			}
			catch (Exception ex3)
			{
				ShowNotification("Full reset failed: " + ex3.Message);
				return;
			}
		}
		string path5 = Path.Combine(_saveDir, "reset-gangs.flag");
		if (!File.Exists(path5))
		{
			return;
		}
		try
		{
			if (File.Exists(_savePath))
			{
				File.Copy(_savePath, Path.Combine(_saveDir, $"nemeses.before-gang-reset-{DateTime.Now:yyyyMMdd-HHmmss}.json"), overwrite: true);
			}
			List<NemesisRecord> list2 = Roster.Where((NemesisRecord n) => n.Gang != null).ToList();
			foreach (NemesisRecord item2 in list2)
			{
				RemoveRecord(item2, "gang reset");
			}
			ReapplyLeaders(list2);
			int count2 = list2.Count;
			Config.SeededGangs = "";
			Config.Save();
			SaveRoster();
			File.Delete(path5);
			ShowNotification($"Gangs reset: {count2} gang members cleared. Fresh rosters are being drawn up.");
		}
		catch (Exception ex4)
		{
			ShowNotification("Gang reset failed: " + ex4.Message);
		}
	}

	private void BackupRoster(string tag)
	{
		try
		{
			if (File.Exists(_savePath))
			{
				File.Copy(_savePath, Path.Combine(_saveDir, $"nemeses.before-{tag}-{DateTime.Now:yyyyMMdd-HHmmss}.json"), overwrite: true);
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("BACKUP failed: " + ex.Message);
		}
	}

	internal void GiveHomeTown(NemesisRecord rec, Gang gang)
	{
		if (gang == null || gang.HomeTown == null || rec.HasOrigin)
		{
			return;
		}
		rec.KillLocation = gang.HomeTown;
		if (TownSpots.TryGetValue(gang.HomeTown, out (Vector3?, Vector3?) value))
		{
			Vector3? vector = value.Item2 ?? value.Item1;
			if (vector.HasValue)
			{
				Vector3 valueOrDefault = vector.GetValueOrDefault();
				rec.HasOrigin = true;
				rec.OriginX = valueOrDefault.X + (float)_rng.Next(-120, 121);
				rec.OriginY = valueOrDefault.Y + (float)_rng.Next(-120, 121);
				rec.OriginZ = valueOrDefault.Z;
			}
		}
	}

	private int ResetEverything()
	{
		_rosterGen++;
		_worldGen++;
		BackupRoster("full-reset");
		int count = Roster.Count;
		Roster.Clear();
		Config.SeededGangs = "";
		Config.ArmyTopUp = true;
		Gang[] defined = Gangs.Defined;
		foreach (Gang gang in defined)
		{
			Config.Notoriety[gang.Id] = 0;
		}
		_bulletImpact.ClearXRayLog();
		_chron = new WorldState();
		Gangs.SetEra(1899);
		if (IsEpiloguePlayer())
		{
			StartWorldIn1907("a fresh start");
		}
		SaveWorld();
		Config.ClearedCamps = "";
		_cleared = null;
		Config.LastSuccessionDay = 0;
		Config.NextSuccessionDay = 0;
		Config.LastDecayDay = 0;
		_successionCard = null;
		_people = new List<Acquaintance>();
		_acqOf.Clear();
		_witnessBatches.Clear();
		_peopleDirty = true;
		SavePeople(force: true);
		_prints = new Dictionary<string, Print>();
		_keptNames = new HashSet<string>();
		_fpOf.Clear();
		_printsDirty = true;
		SavePrints();
		_named.Clear();
		_provoked.Clear();
		Tension = 20f;
		StoreTension();
		Config.Save();
		SaveRoster();
		return count;
	}

	internal int SeedTestLoners(int count)
	{
		Personality[] array = new Personality[6]
		{
			Personality.Hothead,
			Personality.Coward,
			Personality.Proud,
			Personality.Cold,
			Personality.Greedy,
			Personality.Drifter
		};
		string[] array2 = new string[7] { "Valentine", "Valentine", "Valentine", "Rhodes", "Strawberry", "Blackwater", "Saint Denis" };
		int num = 0;
		for (int i = 0; i < count; i++)
		{
			string text = array2[_rng.Next(array2.Length)];
			NemesisRecord nemesisRecord = NewRecord((uint)TestLonerModels[_rng.Next(TestLonerModels.Length)], text);
			nemesisRecord.Gang = null;
			nemesisRecord.Personality = array[i % array.Length];
			Traits.Roll(nemesisRecord, _rng, null);
			nemesisRecord.PastRolled = false;
			EnsurePast(nemesisRecord);
			nemesisRecord.MindRolled = false;
			EnsureMind(nemesisRecord);
			int num2 = _rng.Next(100);
			if (num2 < 40)
			{
				nemesisRecord.Hate = 15 + _rng.Next(24);
				nemesisRecord.Origin = (new GrudgeOrigin[4]
				{
					GrudgeOrigin.Insulted,
					GrudgeOrigin.Beaten,
					GrudgeOrigin.KnockedOut,
					GrudgeOrigin.Threatened
				})[_rng.Next(4)];
			}
			else if (num2 < 75)
			{
				nemesisRecord.Hate = 42 + _rng.Next(27);
				nemesisRecord.Origin = (new GrudgeOrigin[4]
				{
					GrudgeOrigin.Robbed,
					GrudgeOrigin.Kidnapped,
					GrudgeOrigin.Slashed,
					GrudgeOrigin.Gunfight
				})[_rng.Next(4)];
			}
			else
			{
				nemesisRecord.Hate = 72 + _rng.Next(24);
				nemesisRecord.Origin = (new GrudgeOrigin[3]
				{
					GrudgeOrigin.Wounded,
					GrudgeOrigin.HorseKilled,
					GrudgeOrigin.Killed
				})[_rng.Next(3)];
			}
			if (nemesisRecord.Origin == GrudgeOrigin.Killed)
			{
				nemesisRecord.TimesKilledPlayer = 1;
			}
			nemesisRecord.Fear = ((_rng.Next(100) < 35) ? (20 + _rng.Next(45)) : _rng.Next(15));
			if (TownSpots.TryGetValue(text, out (Vector3?, Vector3?) value))
			{
				Vector3? vector = value.Item2 ?? value.Item1;
				if (vector.HasValue)
				{
					Vector3 valueOrDefault = vector.GetValueOrDefault();
					nemesisRecord.HasOrigin = true;
					nemesisRecord.OriginX = valueOrDefault.X + (float)_rng.Next(-60, 60);
					nemesisRecord.OriginY = valueOrDefault.Y + (float)_rng.Next(-60, 60);
					nemesisRecord.OriginZ = valueOrDefault.Z;
				}
			}
			nemesisRecord.RestUntil = 0L;
			nemesisRecord.Touch();
			History(nemesisRecord, $"TEST loner seeded in {text}: {nemesisRecord.Origin}, hate {nemesisRecord.Hate}, fear {nemesisRecord.Fear}", Theme.Dim);
			Roster.Add(nemesisRecord);
			num++;
		}
		SaveRoster();
		GrudgeLog($"TEST seeded {num} loners");
		return num;
	}
}
