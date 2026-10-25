using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using RDR2;

namespace NemesisSystem;

internal sealed class NemesisSettings
{
	private readonly string _path;

	private readonly string _statePath;

	public bool RandomAmbushes = true;

	public int AmbushIntervalSeconds = 120;

	public int AmbushChancePercent = 35;

	public int CheatDeathBaseChance = 35;

	public int CheatDeathPerRank = 5;

	public int CheatDeathMaxChance = 60;

	public bool CheatDeathAlways;

	public bool FatalWoundsFinal = true;

	public int BeatingHits = 3;

	public float ResentfulRange = 500f;

	public float VengefulRange = 1500f;

	public int MessageLevel = 4;

	public static readonly string[] MessageLevelNames = new string[5] { "No messages", "Speech only", "Speech & deaths", "All messages", "Deaths only" };

	public static readonly string[] MessageLevelShort = new string[5] { "Off", "Speech", "Speech & Deaths", "All", "Deaths" };

	public int MaxLivingNemeses = 100;

	public int FollowersRank1;

	public int FollowersRank2 = 1;

	public int FollowersRank3 = 3;

	public int FollowersRank4 = 5;

	public int FollowersRank5 = 10;

	public int GangAlonePercent = 15;

	public int LonerAlonePercent = 65;

	public int ParalyzedSightingChance = 35;

	public float GangAmbushWeight = 0.15f;

	public string SeededGangs = "";

	public bool ArmyTopUp;

	public bool Succession = true;

	public int SuccessionHour = 6;

	public int SuccessionKinChance = 35;

	public int LastSuccessionDay;

	public int NextSuccessionDay;

	public int SuccessionMinDays = 2;

	public int SuccessionMaxDays = 6;

	public bool ArmyGeneral = true;

	public bool CalmEnabled = true;

	public int CalmGraceSeconds = 15;

	public int CalmLoadGraceSeconds = 10;

	public string WorldMarkerStyle = "NEUTRAL_SECRET";

	public int WorldSurvivorChance = 35;

	public float WorldSurvivorSpotRange = 30f;

	public float CalmCampRadius = 180f;

	public bool CalmWhileWanted = true;

	public int StoryChapterSet;

	public string GangCamps = "";

	public bool CompanionsEnabled = true;

	public int WorldCatchUpMax = 24;

	public bool WorldEvents = true;

	public int WorldMinHours = 1;

	public int WorldMaxHours = 1;

	public bool SavePointsEnabled = true;

	public bool SavePointAutoRestore = true;

	public int SavePointBackMinutes = 60;

	public int SavePointEveryHours = 3;

	public int SavePointMaxMB = 150;

	public Keys TelegraphKey = Keys.T;

	public int TelegraphMinDays = 1;

	public int TelegraphMaxDays = 7;

	public float WorldMinDistance = 600f;

	public int WarLine = -35;

	public int ClashLine = -25;

	public bool WarMarkers = true;

	public bool TownRepMarkers = true;

	public string WarRegionStyle = "BLIP_STYLE_DEBUG_RED";

	public int WorldNews = 2;

	public static readonly string[] WorldNewsShort = new string[4] { "Off", "Deaths", "Big News", "All" };

	public float WorldMarkerRadius = 150f;

	public int WorldAftermathHours = 24;

	public int WorldLethality = 100;

	public int WorldBatchMin = 1;

	public int WorldBatchMax = 2;

	public int WorldBigBatchChance = 10;

	public int WorldBigBatchMin = 3;

	public int WorldBigBatchMax = 5;

	public int WorldMaxClashesPerDay = 3;

	public int WorldClashWeight = 20;

	public int WorldFeudWeight = 10;

	public int WorldBrawlWeight = 10;

	public int WorldLifeWeight = 60;

	public int WorldRivalryWeight = 8;

	public int WorldVdlWeight = 4;

	public int WorldWordWeight = 5;

	public Dictionary<string, int> Notoriety = new Dictionary<string, int>();

	public Keys RosterMenuKey = Keys.F11;

	public Keys InterrogateKey = Keys.I;

	public string HeldPanel = "Auto";

	public eInputType InterrogateReleaseControl = eInputType.ContextB;

	public bool CampsEnabled = true;

	public int CrewRosterChance = 40;

	public bool HiredGunsJoin = true;

	public int EncAmbush = 35;

	public int EncRiders = 25;

	public int EncAhead = 15;

	public int EncSniper = 10;

	public int EncCallout = 100;

	public bool NamingEnabled = true;

	public bool WarHud = true;

	public float WarHudX = -1f;

	public float WarHudY = 0.166f;

	public float WarHudW = 0.215f;

	public int NameCards = 1;

	public float NameCardRange = 30f;

	public float NameCardKnownRange = 75f;

	public float NameCardSteady = 2.5f;

	public float NameCardLead = 1f;

	public int HandshakeVariant = 2;

	public bool GsWeather = true;

	public bool GsMinds = true;

	public bool GsBody = true;

	public bool GsMusic;

	public bool GsMap = true;

	public bool GsGestures = true;

	public bool LinkLooks = true;

	public int LookMemoryDays = 3;

	public int LookCap = 5000;

	public bool RememberPeople = true;

	public bool TownsfolkReactions = true;

	public int PeopleCap = 2000;

	public int RegularsMax = 6;

	public int RegularChance = 60;

	public bool RegularsAmbient;

	public bool SocialAmbient;

	public float NamingRadius = 40f;

	public int NamingCap = 250;

	public int NicknameGangPercent = 30;

	public int NicknameLonerPercent = 12;

	public float AffiliatedPercent = 4f;

	public bool DisguiseEnabled = true;

	public string DisguiseCategories = "MASKS,MASKS_LARGE";

	public string EncounterForce = "";

	public bool PauseInMenus = true;

	public bool WelcomeSeen;

	public int PadMenuOpen;

	internal static readonly string[] PadMenuOpenNames = new string[5] { "Double-tap D-pad Right", "Double-tap D-pad Left", "Double-tap D-pad Down", "Hold D-pad Right", "Off" };

	public bool SetupSeen;

	public string WhatsNewSeen = "";

	public int EncounterAmount = 1;

	internal static readonly string[] EncounterAmountNames = new string[4] { "Fewer", "Normal", "More", "Lots" };

	internal static readonly float[] EncounterAmountMult = new float[4] { 0.5f, 1f, 1.6f, 2.4f };

	public bool GameFont = true;

	public bool FitTo169 = true;

	public string PadIcons = "Auto";

	public bool GameFeed;

	public bool MenuArrowText = true;

	public int TextSlotLimit = 230;

	public bool TextUnderline = true;

	public bool SelectFrame = true;

	public bool SelectFrameTint;

	public int SelectFrameSize = 16;

	public int SelectFrameOut = 4;

	public int SelectFrameEdge = 16;

	public int SelectFrameInsetX = 28;

	public int SelectFrameInsetY = 12;

	public int CardFrameUp = 1;

	public bool MenuInk = true;

	public string MenuInkDict = "pausemenu_textures";

	public string MenuInkTexture = "menu_ink_large1";

	public string BodyFont = "$body";

	public string TitleFont = "$title";

	public int CampClearDays = 1;

	public string ClearedCamps = "";

	public float CampSpawnRange = 170f;

	public Keys ForceAmbushKey = Keys.F10;

	public int ConfigVersion;

	public int LoadedVersion = -1;

	public int LastDecayDay;

	public int LastDecayDayBefore;

	public int DebugSeed;

	public int DistantExplosionTag = 25;

	public bool DistantBattles = true;

	public bool LockLeaders = true;

	public bool DebugEnabled;

	public bool Verbose;

	public bool TensionEnabled = true;

	public float TensionRisePerMin = 8.5f;

	public float TensionTownMult = 1.4f;

	public float TensionNightBonus = 2f;

	public float TensionNotorietyBonus = 1f;

	public float TensionHurtMult = 0.3f;

	public float TensionCombatDrainPerSec = 3f;

	public float TensionFightStartDrop = 12f;

	public float TensionDrainBase = 0.3f;

	public float TensionDrainPerMan = 0.2f;

	public float TensionKillDrain = 3f;

	public float TensionOursMult = 2f;

	public float TensionFireMin = 55f;

	public float TensionFireMax = 70f;

	public float TensionWildFireMin = 30f;

	public float TensionWildFireMax = 50f;

	public float TensionAfterEncounter = 35f;

	public float TensionAfterFight = 15f;

	public float TensionFirstMult = 0.4f;

	public float TensionFirstMinDays = 1f;

	public float TensionFirstMaxDays = 2f;

	public float TensionStrangerKeep = 0.5f;

	public const int CurrentVersion = 50;

	private (long t, long n) _iniSeen;

	public Action EditedRead;

	public string KeysNote;

	private static readonly string[] KeyLines = new string[4] { "RosterMenu", "Interrogate", "Telegraph", "ForceAmbush" };

	private bool _iniFailed;

	private bool _stateFailed;

	public bool GrudgeMessages => MessageLevel == 3;

	public bool SpeechMessages
	{
		get
		{
			if (MessageLevel >= 1)
			{
				return MessageLevel <= 3;
			}
			return false;
		}
	}

	public bool DeathMessages => MessageLevel >= 2;

	public bool WorldKnowAll => true;

	internal string PadMenuText
	{
		get
		{
			if (PadMenuOpen != 4)
			{
				return PadMenuOpenNames[Math.Max(0, Math.Min(4, PadMenuOpen))].ToLowerInvariant().Replace("d-pad", "D-pad");
			}
			return "the keyboard key";
		}
	}

	internal float EncounterMult => EncounterAmountMult[Math.Max(0, Math.Min(3, EncounterAmount))];

	public bool FindLeaders => !LockLeaders;

	public NemesisSettings(string path, string statePath = null)
	{
		_path = path;
		_statePath = statePath;
		Load();
	}

	public void ReadNewFactions()
	{
		IniFile iniFile = IniFile.Load(_path);
		IniFile iniFile2 = ((_statePath != null && File.Exists(_statePath)) ? IniFile.Load(_statePath) : iniFile);
		Gang[] defined = Gangs.Defined;
		foreach (Gang gang in defined)
		{
			if (!Notoriety.ContainsKey(gang.Id))
			{
				Notoriety[gang.Id] = Math.Max(0, Math.Min(100, iniFile2.GetValue("Notoriety", gang.Id, 0)));
				gang.AmbushBase = iniFile.GetValue("Factions", gang.Id + "Encounters", gang.AmbushBase);
			}
		}
	}

	public void Load()
	{
		IniFile iniFile = IniFile.Load(_path);
		_iniSeen = IniOnDisk();
		IniFile iniFile2 = ((_statePath != null && File.Exists(_statePath)) ? IniFile.Load(_statePath) : iniFile);
		ConfigVersion = iniFile.GetValue("Meta", "Version", 0);
		if (LoadedVersion < 0)
		{
			LoadedVersion = ConfigVersion;
		}
		bool flag = ConfigVersion < 50;
		foreach (Setting item in Layout())
		{
			item.Read?.Invoke(item.State ? iniFile2 : iniFile);
		}
		MessageLevel = (iniFile.GetValue("Menu", "GrudgeMessages", defaultValue: true) ? MessageLevel : 0);
		MessageLevel = Math.Max(0, Math.Min(4, iniFile.GetValue("Menu", "Messages", MessageLevel)));
		Gang[] defined = Gangs.Defined;
		foreach (Gang gang in defined)
		{
			gang.AmbushBase = iniFile.GetValue("Factions", gang.Id + "Encounters", gang.AmbushBase);
		}
		defined = Gangs.Defined;
		foreach (Gang gang2 in defined)
		{
			Notoriety[gang2.Id] = Math.Max(0, Math.Min(100, iniFile2.GetValue("Notoriety", gang2.Id, 0)));
		}
		Hate.MurderousAt = Math.Max(Hate.VengefulAt + 5, Hate.MurderousAt);
		if (flag && ConfigVersion < 3)
		{
			if (Math.Abs(NamingRadius - 25f) < 0.01f)
			{
				NamingRadius = 40f;
			}
			if (NamingCap == 120)
			{
				NamingCap = 250;
			}
			if (Balance.VengefulHuntAggression == 60)
			{
				Balance.VengefulHuntAggression = 75;
			}
			if ((Balance.InterroBaseBoy == 50 && Balance.InterroBaseCaptain == 35) || (Balance.InterroBaseBoy == 30 && Balance.InterroBaseCaptain == 20))
			{
				Balance.InterroBaseBoy = 12;
				Balance.InterroBaseCaptain = 8;
				Balance.InterroBaseLieutenant = 3;
				Balance.InterroBaseAmbient = 15;
			}
			if (Balance.InterroFamilyFearExtra == 30)
			{
				Balance.InterroFamilyFearExtra = 15;
			}
			if (Math.Abs(Balance.PatrolHearingRange - 300f) < 0.5f)
			{
				Balance.PatrolHearingRange = 600f;
			}
		}
		if (flag && ConfigVersion < 4)
		{
			if (WorldMinHours == 3 && WorldMaxHours == 6)
			{
				WorldMinHours = 1;
				WorldMaxHours = 1;
			}
			if (WorldBatchMin == 1 && WorldBatchMax == 5)
			{
				WorldBatchMax = 2;
			}
		}
		if (flag && ConfigVersion < 6)
		{
			if (Balance.TownTopUpSeconds == 180)
			{
				Balance.TownTopUpSeconds = 90;
			}
			if (Balance.TownTopUpChance == 40)
			{
				Balance.TownTopUpChance = 50;
			}
			if (Balance.TownMaxMen == 3)
			{
				Balance.TownMaxMen = 4;
			}
		}
		if (flag && ConfigVersion < 7)
		{
			if (Balance.TownVengefulPct == 60)
			{
				Balance.TownVengefulPct = 35;
			}
			if (Balance.TownMurderousPct == 25)
			{
				Balance.TownMurderousPct = 15;
			}
			if (RegularsMax == 3)
			{
				RegularsMax = 6;
			}
			if (RegularChance == 50)
			{
				RegularChance = 60;
			}
		}
		if (flag && ConfigVersion < 8)
		{
			SocialAmbient = false;
			RegularsAmbient = false;
		}
		if (flag && ConfigVersion < 9)
		{
			if (Balance.TownTopUpSeconds == 90)
			{
				Balance.TownTopUpSeconds = 60;
			}
			if (Balance.TownTopUpChance == 50)
			{
				Balance.TownTopUpChance = 65;
			}
		}
		if (flag && ConfigVersion < 10 && CalmGraceSeconds == 90)
		{
			CalmGraceSeconds = 15;
		}
		if (flag && ConfigVersion < 11 && WorldMarkerStyle == "POI")
		{
			WorldMarkerStyle = "RCM";
		}
		if (flag && ConfigVersion < 12 && WorldMarkerStyle == "RCM")
		{
			WorldMarkerStyle = "CLUE";
		}
		if (flag && ConfigVersion < 13)
		{
			if (WorldMarkerStyle == "CLUE")
			{
				WorldMarkerStyle = "CLUE_RC";
			}
			GameFeed = false;
		}
		if (flag && ConfigVersion < 14)
		{
			WorldMarkerStyle = "POI_SPRITE";
		}
		if (flag && ConfigVersion < 15)
		{
			WorldMarkerStyle = "NEUTRAL_SECRET";
		}
		if (flag && ConfigVersion < 16 && PeopleCap == 300)
		{
			PeopleCap = 1000;
		}
		if (flag && ConfigVersion < 21 && GangAlonePercent == 35)
		{
			GangAlonePercent = 15;
		}
		if (flag && ConfigVersion < 23)
		{
			GsMusic = false;
		}
		if (flag && ConfigVersion < 24)
		{
			GsGestures = true;
		}
		if (flag && ConfigVersion < 22)
		{
			if (Balance.PatrolRespondChance == 35)
			{
				Balance.PatrolRespondChance = 15;
			}
			if (Balance.PatrolRespondCooldown == 180)
			{
				Balance.PatrolRespondCooldown = 600;
			}
			if (Balance.PatrolChance == 20)
			{
				Balance.PatrolChance = 12;
			}
		}
		if (flag && ConfigVersion < 17 && Balance.PatrolRespondMin == 250 && Balance.PatrolRespondMax == 350)
		{
			Balance.PatrolRespondMin = 150;
			Balance.PatrolRespondMax = 200;
		}
		if (flag && ConfigVersion < 25)
		{
			if (SelectFrameInsetX == 42)
			{
				SelectFrameInsetX = 28;
			}
			if (SelectFrameInsetY == 12)
			{
				SelectFrameInsetY = 22;
			}
		}
		if (flag && ConfigVersion < 26 && SelectFrameInsetY == 22)
		{
			SelectFrameInsetY = 12;
		}
		if (flag && ConfigVersion < 27)
		{
			if (Math.Abs(TensionTownMult - 2.5f) < 0.01f)
			{
				TensionTownMult = 1.4f;
			}
			if (Math.Abs(TensionRisePerMin - 7.5f) < 0.01f || Math.Abs(TensionRisePerMin - 6f) < 0.01f)
			{
				TensionRisePerMin = 8.5f;
			}
			if (CheatDeathBaseChance == 50 && CheatDeathPerRank == 0 && CheatDeathMaxChance == 50)
			{
				CheatDeathBaseChance = 35;
				CheatDeathPerRank = 5;
				CheatDeathMaxChance = 60;
			}
		}
		if (flag && ConfigVersion < 29)
		{
			CardFrameUp = 1;
		}
		if (flag && ConfigVersion < 31)
		{
			WarHudX = 0.03f;
			WarHudY = 0.06f;
		}
		if (flag && ConfigVersion < 32 && Balance.GsWounded == 65f)
		{
			Balance.GsWounded = 50f;
		}
		if (flag && ConfigVersion < 33 && TensionAfterFight == 10f)
		{
			TensionAfterFight = 25f;
		}
		if (flag && ConfigVersion < 34 && CardFrameUp == 1)
		{
			CardFrameUp = 0;
		}
		if (flag && ConfigVersion < 35 && CardFrameUp == 0)
		{
			CardFrameUp = 1;
		}
		if (flag && ConfigVersion < 36)
		{
			if (TensionFireMax == 85f)
			{
				TensionFireMax = 70f;
			}
			if (TensionAfterFight == 25f)
			{
				TensionAfterFight = 15f;
			}
		}
		if (flag && ConfigVersion < 37)
		{
			if (TensionFireMin == 55f)
			{
				TensionFireMin = 45f;
			}
			if (TensionFireMax == 70f)
			{
				TensionFireMax = 60f;
			}
			if (TensionWildFireMin == 40f)
			{
				TensionWildFireMin = 30f;
			}
			if (TensionWildFireMax == 65f)
			{
				TensionWildFireMax = 50f;
			}
			if (TensionAfterEncounter == 35f)
			{
				TensionAfterEncounter = 25f;
			}
		}
		if (flag && ConfigVersion < 38)
		{
			if (TensionFireMin == 45f)
			{
				TensionFireMin = 55f;
			}
			if (TensionFireMax == 60f)
			{
				TensionFireMax = 70f;
			}
			if (TensionAfterEncounter == 25f)
			{
				TensionAfterEncounter = 35f;
			}
		}
		if (flag && ConfigVersion < 39 && Math.Abs(WarHudX - 0.03f) < 0.001f && Math.Abs(WarHudY - 0.06f) < 0.001f)
		{
			WarHudX = -1f;
			WarHudY = 0.105f;
		}
		if (flag && ConfigVersion < 40 && Math.Abs(WarHudY - 0.105f) < 0.001f)
		{
			WarHudY = 0.13f;
		}
		if (flag && ConfigVersion < 41 && Math.Abs(WarHudY - 0.13f) < 0.001f)
		{
			WarHudY = 0.16f;
		}
		if (flag && ConfigVersion < 42 && Math.Abs(WarHudY - 0.16f) < 0.001f)
		{
			WarHudY = 0.166f;
		}
		if (flag && ConfigVersion < 43 && Balance.InterroTriesPerScare == 2)
		{
			Balance.InterroTriesPerScare = 1;
		}
		if (flag && ConfigVersion < 44 && SuccessionMinDays == 3 && SuccessionMaxDays == 5)
		{
			SuccessionMinDays = 2;
			SuccessionMaxDays = 6;
		}
		if (flag && ConfigVersion < 28)
		{
			if (MessageLevel == 3)
			{
				MessageLevel = 4;
			}
			if (NameCards == 0)
			{
				NameCards = 2;
			}
		}
		PushOut();
		if (flag && ConfigVersion < 45 && Balance.PatrolRespondChance == 15)
		{
			Balance.PatrolRespondChance = 10;
		}
		if (flag && ConfigVersion < 45 && Balance.PatrolRespondCooldown == 600)
		{
			Balance.PatrolRespondCooldown = 900;
		}
		if (flag && ConfigVersion < 46)
		{
			DebugEnabled = false;
		}
		if (flag && ConfigVersion < 47 && CampClearDays == 3)
		{
			CampClearDays = 1;
		}
		if (flag && ConfigVersion < 48 && Math.Abs(WorldMarkerRadius - 300f) < 0.5f)
		{
			WorldMarkerRadius = 150f;
		}
		if (flag && ConfigVersion < 49 && !RandomAmbushes)
		{
			RandomAmbushes = true;
			if (SafeFile.PendingNotice == null)
			{
				SafeFile.PendingNotice = "random encounters were switched off - they're back on. (To turn them off: Alt+F11, Ambush, RandomAmbushes.)";
			}
		}
		if (flag && ConfigVersion < 50 && PeopleCap == 1000)
		{
			PeopleCap = 2000;
		}
		IniPanel.Dev = DebugEnabled;
		string value = iniFile.GetValue("World", "FindLeaders", "");
		if (!string.IsNullOrEmpty(value))
		{
			LockLeaders = !value.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
		}
		CheckKeys(iniFile);
		if (flag)
		{
			ConfigVersion = 50;
		}
		Save();
	}

	private void PushOut()
	{
		UiDraw.GameFont = GameFont;
		UiDraw.FitTo169 = FitTo169;
		UiFeed.Enabled = GameFeed;
		XPad.IconsSetting = PadIcons;
		UiDraw.BodyFont = BodyFont;
		UiDraw.TextSlotLimit = TextSlotLimit;
		UiDraw.TitleFont = TitleFont;
		UiDraw.MarkupUnderline = TextUnderline;
		Theme.FrameOn = SelectFrame;
		Theme.FrameTint = SelectFrameTint;
		Theme.FrameSize = SelectFrameSize;
		Theme.FrameOut = SelectFrameOut;
		Theme.FrameEdge = SelectFrameEdge;
		Theme.FrameInsetX = SelectFrameInsetX;
		Theme.FrameInsetY = SelectFrameInsetY;
		Theme.CardFrameUp = CardFrameUp;
		Theme.InkOn = MenuInk;
		Theme.InkDict = MenuInkDict;
		Theme.InkTexture = MenuInkTexture;
	}

	private (long t, long n) IniOnDisk()
	{
		try
		{
			FileInfo fileInfo = new FileInfo(_path);
			return fileInfo.Exists ? (t: fileInfo.LastWriteTimeUtc.Ticks, n: fileInfo.Length) : (t: 0L, n: 0L);
		}
		catch
		{
			return _iniSeen;
		}
	}

	public bool EditedOutside()
	{
		(long, long) tuple = IniOnDisk();
		if (tuple.Item1 == _iniSeen.t)
		{
			return tuple.Item2 != _iniSeen.n;
		}
		return true;
	}

	public void ReadEdited()
	{
		IniFile iniFile = IniFile.Load(_path);
		_iniSeen = IniOnDisk();
		foreach (Setting item in Layout())
		{
			if (!item.State)
			{
				item.Read?.Invoke(iniFile);
			}
		}
		MessageLevel = (iniFile.GetValue("Menu", "GrudgeMessages", defaultValue: true) ? MessageLevel : 0);
		MessageLevel = Math.Max(0, Math.Min(4, iniFile.GetValue("Menu", "Messages", MessageLevel)));
		Gang[] defined = Gangs.Defined;
		foreach (Gang gang in defined)
		{
			gang.AmbushBase = iniFile.GetValue("Factions", gang.Id + "Encounters", gang.AmbushBase);
		}
		Hate.MurderousAt = Math.Max(Hate.VengefulAt + 5, Hate.MurderousAt);
		PushOut();
		IniPanel.Dev = DebugEnabled;
		string value = iniFile.GetValue("World", "FindLeaders", "");
		if (!string.IsNullOrEmpty(value))
		{
			LockLeaders = !value.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
		}
		CheckKeys(iniFile);
		EditedRead?.Invoke();
	}

	private void CheckKeys(IniFile s)
	{
		List<string> list = new List<string>();
		string[] keyLines = KeyLines;
		foreach (string text in keyLines)
		{
			string text2 = s.Raw("Keys", text);
			if (text2 != null && !IniFile.IsKey(text2))
			{
				list.Add(text + "=" + text2 + " isn't a key name the mod knows");
			}
			if (s.Twice.Contains("Keys/" + text))
			{
				list.Add(text + " is in it twice (the first one counts)");
			}
		}
		KeysNote = ((list.Count == 0) ? null : ("NemesisMod.ini [Keys]: " + string.Join("; ", list) + ". The menu key is " + KeyCombo.Name(RosterMenuKey) + ". Names like F3, NumPad5, PageUp or Shift+F9 work, or set it in game (" + KeyCombo.SettingsText + ", Keys)."));
	}

	private static string F(float v)
	{
		return v.ToString("0.0##", CultureInfo.InvariantCulture);
	}

	public void Save()
	{
		if (_path != null && EditedOutside())
		{
			try
			{
				ReadEdited();
			}
			catch (Exception ex)
			{
				SafeFile.Log?.Invoke("SETTINGS an edit to NemesisMod.ini couldn't be read: " + ex.Message);
			}
		}
		StringBuilder stringBuilder = new StringBuilder();
		List<(string sec, string line)> list = new List<(string sec, string line)>();
		try
		{
			foreach (Setting item in Layout())
			{
				if (item.Write != null)
				{
					if (item.State && _statePath != null)
					{
						list.Add((item.Section, item.Write()));
					}
					else
					{
						stringBuilder.Append(item.Write()).Append("\r\n");
					}
				}
			}
		}
		catch (Exception ex2)
		{
			SafeFile.Log?.Invoke("SAVE settings couldn't be put together: " + ex2.Message);
			return;
		}
		try
		{
			SafeFile.Write(_path, stringBuilder.ToString());
			_iniSeen = IniOnDisk();
			WriteWorked(ref _iniFailed, _path);
		}
		catch (Exception ex3)
		{
			WriteFailed(ref _iniFailed, _path, ex3, "your settings won't be kept");
		}
		if (_statePath == null)
		{
			return;
		}
		try
		{
			StringBuilder stringBuilder2 = new StringBuilder("; enRAGE world bookkeeping - written by the mod. Not settings: leave it alone.\r\n");
			foreach (IGrouping<string, (string, string)> item2 in from x in list
				group x by x.sec)
			{
				stringBuilder2.Append("\r\n[").Append(item2.Key).Append("]\r\n");
				foreach (var item3 in item2)
				{
					stringBuilder2.Append(item3.Item2).Append("\r\n");
				}
			}
			SafeFile.Write(_statePath, stringBuilder2.ToString());
			WriteWorked(ref _stateFailed, _statePath);
		}
		catch (Exception ex4)
		{
			WriteFailed(ref _stateFailed, _statePath, ex4, "notoriety and the outfits' bookkeeping won't be kept");
		}
	}

	private static void WriteFailed(ref bool failed, string path, Exception ex, string what)
	{
		if (!failed && SafeFile.Log != null)
		{
			failed = true;
			string fileName = Path.GetFileName(path);
			SafeFile.Log?.Invoke("SAVE " + fileName + " failed: " + ex.Message + " (" + path + ") - " + what + " until it can be written (read-only? an antivirus such as Windows Defender's Controlled folder access?)");
			if (SafeFile.PendingNotice == null)
			{
				SafeFile.PendingNotice = fileName + " can't be saved, so " + what + ". Is the folder read-only, or blocked by your antivirus? (grudges.log: SAVE)";
			}
		}
	}

	private static void WriteWorked(ref bool failed, string path)
	{
		if (failed)
		{
			failed = false;
			SafeFile.Log?.Invoke("SAVE " + Path.GetFileName(path) + " written again");
		}
	}

	private IEnumerable<Setting> Layout()
	{
		yield return Setting.Line("[Ambush]");
		yield return Setting.Line("; Random encounters on/off (RandomAmbushes). IntervalSeconds and ChancePercent are only used when [Tension] Enabled=False");
		yield return Setting.Line("; (the tension director decides when otherwise).");
		yield return Setting.Key("Ambush", "RandomAmbushes", () => RandomAmbushes, delegate(bool v)
		{
			RandomAmbushes = v;
		}, () => $"{RandomAmbushes}");
		yield return Setting.Key("Ambush", "IntervalSeconds", () => AmbushIntervalSeconds, delegate(int v)
		{
			AmbushIntervalSeconds = Math.Max(10, v);
		}, () => $"{AmbushIntervalSeconds}");
		yield return Setting.Key("Ambush", "ChancePercent", () => AmbushChancePercent, delegate(int v)
		{
			AmbushChancePercent = v;
		}, () => $"{AmbushChancePercent}");
		yield return Setting.Line("; How far from where the grudge started (or his camp) a nemesis will come for you, by hate.");
		yield return Setting.Line("; Murderous nemeses hunt you anywhere. The encounter spawner / Summon ignore this.");
		yield return Setting.Key("Ambush", "ResentfulRange", () => ResentfulRange, delegate(float v)
		{
			ResentfulRange = v;
		}, () => F(ResentfulRange) ?? "");
		yield return Setting.Key("Ambush", "VengefulRange", () => VengefulRange, delegate(float v)
		{
			VengefulRange = v;
		}, () => F(VengefulRange) ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[CheatDeath]");
		yield return Setting.Line("; Chance a killed nemesis secretly survives with a scar. Headshots are always final.");
		yield return Setting.Key("CheatDeath", "BaseChance", () => CheatDeathBaseChance, delegate(int v)
		{
			CheatDeathBaseChance = v;
		}, () => $"{CheatDeathBaseChance}");
		yield return Setting.Key("CheatDeath", "PerRank", () => CheatDeathPerRank, delegate(int v)
		{
			CheatDeathPerRank = v;
		}, () => $"{CheatDeathPerRank}");
		yield return Setting.Key("CheatDeath", "MaxChance", () => CheatDeathMaxChance, delegate(int v)
		{
			CheatDeathMaxChance = v;
		}, () => $"{CheatDeathMaxChance}");
		yield return Setting.Key("CheatDeath", "Always", () => CheatDeathAlways, delegate(bool v)
		{
			CheatDeathAlways = v;
		}, () => $"{CheatDeathAlways}");
		yield return Setting.Line("; Like a headshot, an X-ray-confirmed destroyed brain, pierced heart or shattered neck is final.");
		yield return Setting.Key("CheatDeath", "FatalWoundsFinal", () => FatalWoundsFinal, delegate(bool v)
		{
			FatalWoundsFinal = v;
		}, () => $"{FatalWoundsFinal}");
		yield return Setting.Line("");
		yield return Setting.Line("[Grudges]");
		yield return Setting.Line("; Robbing a nemesis: hate + RobbedHateGain (+ RobbedGreedyExtra for a greedy man, +5 poor / -4 rich, +4 each time again, max +12),");
		yield return Setting.Line("; then his temper. A stranger you rob starts at 46 + temper (greedy +20, coward -25...) + RobbedPoorShift if poor / - RobbedRichShift if rich.");
		yield return Setting.Key("Grudges", "RobbedHateGain", () => Balance.RobbedHateGain, delegate(int v)
		{
			Balance.RobbedHateGain = v;
		}, () => $"{Balance.RobbedHateGain}");
		yield return Setting.Key("Grudges", "RobbedGreedyExtra", () => Balance.RobbedGreedyExtra, delegate(int v)
		{
			Balance.RobbedGreedyExtra = v;
		}, () => $"{Balance.RobbedGreedyExtra}");
		yield return Setting.Key("Grudges", "RobbedPoorShift", () => Balance.RobbedPoorShift, delegate(int v)
		{
			Balance.RobbedPoorShift = v;
		}, () => $"{Balance.RobbedPoorShift}");
		yield return Setting.Key("Grudges", "RobbedRichShift", () => Balance.RobbedRichShift, delegate(int v)
		{
			Balance.RobbedRichShift = v;
		}, () => $"{Balance.RobbedRichShift}");
		yield return Setting.Line("; People you shoot, beat up, knock out, rob or keep insulting (and leave alive) can become nemeses.");
		yield return Setting.Line("; Unarmed hits that count as a beating (the [GrudgeScore] weights decide who turns nemesis).");
		yield return Setting.Key("Grudges", "BeatingHits", () => BeatingHits, delegate(int v)
		{
			BeatingHits = v;
		}, () => $"{BeatingHits}");
		yield return Setting.Key("Grudges", "MaxLivingNemeses", () => MaxLivingNemeses, delegate(int v)
		{
			MaxLivingNemeses = v;
		}, () => $"{MaxLivingNemeses}");
		yield return Setting.Line("");
		yield return Setting.Line("[Followers]");
		yield return Setting.Line("; Men who ride with a nemesis, by rank. Rank 5 is the Boss.");
		yield return Setting.Key("Followers", "Rank1", () => FollowersRank1, delegate(int v)
		{
			FollowersRank1 = v;
		}, () => $"{FollowersRank1}");
		yield return Setting.Key("Followers", "Rank2", () => FollowersRank2, delegate(int v)
		{
			FollowersRank2 = v;
		}, () => $"{FollowersRank2}");
		yield return Setting.Key("Followers", "Rank3", () => FollowersRank3, delegate(int v)
		{
			FollowersRank3 = v;
		}, () => $"{FollowersRank3}");
		yield return Setting.Key("Followers", "Rank4", () => FollowersRank4, delegate(int v)
		{
			FollowersRank4 = v;
		}, () => $"{FollowersRank4}");
		yield return Setting.Key("Followers", "Rank5", () => FollowersRank5, delegate(int v)
		{
			FollowersRank5 = v;
		}, () => $"{FollowersRank5}");
		yield return Setting.Line("; Crews vary: a gang man comes alone GangAlonePercent of the time, else with his rank's crew -1..+2 men (rank 2+ at least 2) + his faction's extra (Raiders +2, O'Driscolls +1).");
		yield return Setting.Line("; A loner comes alone LonerAlonePercent of the time, else with 1-2 men.");
		yield return Setting.Key("Followers", "GangAlonePercent", () => GangAlonePercent, delegate(int v)
		{
			GangAlonePercent = Math.Max(0, Math.Min(100, v));
		}, () => $"{GangAlonePercent}");
		yield return Setting.Key("Followers", "LonerAlonePercent", () => LonerAlonePercent, delegate(int v)
		{
			LonerAlonePercent = Math.Max(0, Math.Min(100, v));
		}, () => $"{LonerAlonePercent}");
		yield return Setting.Line("");
		yield return Setting.Line("[Sightings]");
		yield return Setting.Line("; Each time you ride into a town: % chance one of your paralyzed nemeses is laid up there");
		yield return Setting.Line("; (the doctor's in Valentine or Saint Denis, a hotel bed you've marked), still crippled.");
		yield return Setting.Key("Sightings", "ParalyzedChance", () => ParalyzedSightingChance, delegate(int v)
		{
			ParalyzedSightingChance = v;
		}, () => $"{ParalyzedSightingChance}");
		yield return Setting.Line("");
		yield return Setting.Line("[Gangs]");
		yield return Setting.Line("; Unmet gang members ambush this often compared with a personal nemesis (1.0 = same).");
		yield return Setting.Key("Gangs", "AmbushWeight", () => GangAmbushWeight, delegate(float v)
		{
			GangAmbushWeight = v;
		}, () => F(GangAmbushWeight) ?? "");
		yield return Setting.Line("; Gangs whose roster has been created. Remove a name to re-roll that gang's roster.");
		yield return Setting.StateKey("Gangs", "Seeded", () => SeededGangs, delegate(string v)
		{
			SeededGangs = v;
		}, () => SeededGangs ?? "");
		yield return Setting.StateKey("Gangs", "ArmyTopUp", () => ArmyTopUp, delegate(bool v)
		{
			ArmyTopUp = v;
		}, () => $"{ArmyTopUp}");
		yield return Setting.Line("; % chance a gang officer brings ONE named man from the rank below (the rest are hired guns).");
		yield return Setting.Key("Gangs", "CrewRosterChance", () => CrewRosterChance, delegate(int v)
		{
			CrewRosterChance = v;
		}, () => $"{CrewRosterChance}");
		yield return Setting.Line("; Hired guns who live through a fight with you join the gang's bottom rank as named men.");
		yield return Setting.Key("Gangs", "HiredGunsJoin", () => HiredGunsJoin, delegate(bool v)
		{
			HiredGunsJoin = v;
		}, () => $"{HiredGunsJoin}");
		yield return Setting.Line("; Each outfit's shake-up comes every SuccessionMinDays..MaxDays game days at an hour of its own: empty seats are filled from the rank below");
		yield return Setting.Line("; (best standing man moves up) and the bottom takes fresh recruits; some are kin of men you killed.");
		yield return Setting.Key("Gangs", "Succession", () => Succession, delegate(bool v)
		{
			Succession = v;
		}, () => $"{Succession}");
		yield return Setting.Key("Gangs", "SuccessionHour", () => SuccessionHour, delegate(int v)
		{
			SuccessionHour = v;
		}, () => $"{SuccessionHour}");
		yield return Setting.Key("Gangs", "SuccessionKinChance", () => SuccessionKinChance, delegate(int v)
		{
			SuccessionKinChance = v;
		}, () => $"{SuccessionKinChance}");
		yield return Setting.Line("; % any new man (grudge, killer, seed) is kin of someone already on your roster.");
		yield return Setting.Key("Gangs", "KinChance", () => Traits.KinChance, delegate(int v)
		{
			Traits.KinChance = v;
		}, () => $"{Traits.KinChance}");
		yield return Setting.StateKey("Gangs", "LastSuccessionDay", () => LastSuccessionDay, delegate(int v)
		{
			LastSuccessionDay = v;
		}, () => $"{LastSuccessionDay}");
		yield return Setting.Line("; The next shake-up (game date) - rolled 3-5 game days after each one. Ranks only change then.");
		yield return Setting.StateKey("Gangs", "NextSuccessionDay", () => NextSuccessionDay, delegate(int v)
		{
			NextSuccessionDay = v;
		}, () => $"{NextSuccessionDay}");
		yield return Setting.Key("Gangs", "SuccessionMinDays", () => SuccessionMinDays, delegate(int v)
		{
			SuccessionMinDays = Math.Max(1, v);
		}, () => $"{SuccessionMinDays}");
		yield return Setting.Key("Gangs", "SuccessionMaxDays", () => SuccessionMaxDays, delegate(int v)
		{
			SuccessionMaxDays = Math.Max(SuccessionMinDays, v);
		}, () => $"{SuccessionMaxDays}");
		yield return Setting.Line("; The Fort Wallace HQ: once an Army man has told you about it, its commander and garrison are there (and the gates open).");
		yield return Setting.Key("Gangs", "ArmyGeneral", () => ArmyGeneral, delegate(bool v)
		{
			ArmyGeneral = v;
		}, () => $"{ArmyGeneral}");
		yield return Setting.Line("");
		yield return Setting.Line("[Calm]");
		yield return Setting.Line("; Nothing new comes at you (encounters, patrols, skirmishes, town men; camps and the General only for the first four):");
		yield return Setting.Line("; on a mission, while settling in (after loading, fast travel, a train ride, a sleep), on a train, at the gang's camp,");
		yield return Setting.Line("; indoors, resting (sitting, at a fire), or while the law is after you (only bounty hunters then).");
		yield return Setting.Key("Calm", "Enabled", () => CalmEnabled, delegate(bool v)
		{
			CalmEnabled = v;
		}, () => $"{CalmEnabled}");
		yield return Setting.Key("Calm", "GraceSeconds", () => CalmGraceSeconds, delegate(int v)
		{
			CalmGraceSeconds = v;
		}, () => $"{CalmGraceSeconds}");
		yield return Setting.Line("; Quiet after loading in / reloading the mod (seconds). The tension build-up already makes you wait.");
		yield return Setting.Key("Calm", "LoadGraceSeconds", () => CalmLoadGraceSeconds, delegate(int v)
		{
			CalmLoadGraceSeconds = Math.Max(0, v);
		}, () => $"{CalmLoadGraceSeconds}");
		yield return Setting.Line("; Metres: a remembered gang camp stays safe this far out (camps are found by Dutch, Hosea, Sadie... being near).");
		yield return Setting.Key("Calm", "CampRadius", () => CalmCampRadius, delegate(float v)
		{
			CalmCampRadius = v;
		}, () => F(CalmCampRadius) ?? "");
		yield return Setting.Key("Calm", "WhileWanted", () => CalmWhileWanted, delegate(bool v)
		{
			CalmWhileWanted = v;
		}, () => $"{CalmWhileWanted}");
		yield return Setting.Line("; StoryChapter: 0 = Auto - the mod works out the chapter when you visit the gang's camp. 1-6 = the chapter you're in,");
		yield return Setting.Line("; if it hasn't worked it out yet (it matters for Rhodes in chapter 3, where the story takes your guns). Back to 0 to let");
		yield return Setting.Line("; the camp decide again as the story moves on.");
		yield return Setting.Key("Calm", "StoryChapter", () => StoryChapterSet, delegate(int v)
		{
			StoryChapterSet = Math.Max(0, Math.Min(6, v));
		}, () => $"{StoryChapterSet}");
		yield return Setting.StateKey("Calm", "GangCamps", () => GangCamps, delegate(string v)
		{
			GangCamps = v;
		}, () => GangCamps ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Companions]");
		yield return Setting.Line("; Ride With Me: hold the prompt on a gang member at camp and he may ride out with you (up to 3). False = they stay at camp.");
		yield return Setting.Key("Companions", "Enabled", () => CompanionsEnabled, delegate(bool v)
		{
			CompanionsEnabled = v;
		}, () => $"{CompanionsEnabled}");
		yield return Setting.Line("");
		yield return Setting.Line("[People]");
		yield return Setting.Line("; Everyone you dealt with (greeted, antagonized, robbed, hit...) is remembered in people.json - name, face, town, temper,");
		yield return Setting.Line("; grudge. In a town, every TownTopUpSeconds RegularChance % that one you met there turns up again (up to RegularsMax).");
		yield return Setting.Key("People", "Remember", () => RememberPeople, delegate(bool v)
		{
			RememberPeople = v;
		}, () => $"{RememberPeople}");
		yield return Setting.Line("; TownsfolkReactions: ordinary people react to you the mod's way - walking or running off when your gun is out, staring");
		yield return Setting.Line("; you down or running when they recognise you, answering a greeting or an insult by their temper, going back to what they");
		yield return Setting.Line("; were doing after a greeting. False = they react only as the game makes them (they still remember what they saw).");
		yield return Setting.Key("People", "TownsfolkReactions", () => TownsfolkReactions, delegate(bool v)
		{
			TownsfolkReactions = v;
		}, () => $"{TownsfolkReactions}");
		yield return Setting.Key("People", "Cap", () => PeopleCap, delegate(int v)
		{
			PeopleCap = Math.Max(20, v);
		}, () => $"{PeopleCap}");
		yield return Setting.Key("People", "RegularsMax", () => RegularsMax, delegate(int v)
		{
			RegularsMax = v;
		}, () => $"{RegularsMax}");
		yield return Setting.Key("People", "RegularChance", () => RegularChance, delegate(int v)
		{
			RegularChance = v;
		}, () => $"{RegularChance}");
		yield return Setting.Line("; Names follow LOOKS: a man whose exact look (head, hair, clothes, colours) you've seen before gets the same name again,");
		yield return Setting.Line("; with his grudge if he had one (fingerprints2.json). Strangers you never dealt with are forgotten after LookMemoryDays");
		yield return Setting.Line("; game days; people you met / crossed are kept. LookCap = most looks kept.");
		yield return Setting.Key("People", "LinkLooks", () => LinkLooks, delegate(bool v)
		{
			LinkLooks = v;
		}, () => $"{LinkLooks}");
		yield return Setting.Key("People", "LookMemoryDays", () => LookMemoryDays, delegate(int v)
		{
			LookMemoryDays = Math.Max(1, v);
		}, () => $"{LookMemoryDays}");
		yield return Setting.Key("People", "LookCap", () => LookCap, delegate(int v)
		{
			LookCap = Math.Max(200, v);
		}, () => $"{LookCap}");
		yield return Setting.Line("; True = once placed, people you know are handed to the game as ordinary townsfolk. OFF by default: the game's crowd cleanup");
		yield return Setting.Line("; deletes them within seconds (greet / antagonize / rob work without it now: ped flag 130 off).");
		yield return Setting.Key("People", "RegularsAmbient", () => RegularsAmbient, delegate(bool v)
		{
			RegularsAmbient = v;
		}, () => $"{RegularsAmbient}");
		yield return Setting.Line("; True = nemeses waiting in town / encounter men are handed to the game until the fight. OFF by default: the game's crowd");
		yield return Setting.Line("; cleanup deleted them within seconds (they spawned and disappeared).");
		yield return Setting.Key("People", "SocialAmbient", () => SocialAmbient, delegate(bool v)
		{
			SocialAmbient = v;
		}, () => $"{SocialAmbient}");
		yield return Setting.Line("");
		yield return Setting.Line("[Telegraph]");
		yield return Setting.Line("; Telegrams at the station clerk: the answer comes MinDays..MaxDays game days later.");
		yield return Setting.Key("Telegraph", "MinDays", () => TelegraphMinDays, delegate(int v)
		{
			TelegraphMinDays = Math.Max(1, v);
		}, () => $"{TelegraphMinDays}");
		yield return Setting.Key("Telegraph", "MaxDays", () => TelegraphMaxDays, delegate(int v)
		{
			TelegraphMaxDays = Math.Max(TelegraphMinDays, v);
		}, () => $"{TelegraphMaxDays}");
		yield return Setting.Line("");
		yield return Setting.Line("[SavePoints]");
		yield return Setting.Line("; The mod's world follows your game save: save points are kept when the game saves and every EveryHours game hours;");
		yield return Setting.Line("; loading a save that's BackMinutes+ earlier puts back the newest save point at or before it (Shift+F11 within 2 min undoes it).");
		yield return Setting.Key("SavePoints", "Enabled", () => SavePointsEnabled, delegate(bool v)
		{
			SavePointsEnabled = v;
		}, () => $"{SavePointsEnabled}");
		yield return Setting.Key("SavePoints", "AutoRestore", () => SavePointAutoRestore, delegate(bool v)
		{
			SavePointAutoRestore = v;
		}, () => $"{SavePointAutoRestore}");
		yield return Setting.Key("SavePoints", "BackMinutes", () => SavePointBackMinutes, delegate(int v)
		{
			SavePointBackMinutes = Math.Max(10, v);
		}, () => $"{SavePointBackMinutes}");
		yield return Setting.Key("SavePoints", "EveryHours", () => SavePointEveryHours, delegate(int v)
		{
			SavePointEveryHours = Math.Max(1, v);
		}, () => $"{SavePointEveryHours}");
		yield return Setting.Line("; Save points' total size on disk (MB): past it the oldest go (never the earliest, a fresh start or the newest 5).");
		yield return Setting.Key("SavePoints", "MaxMB", () => SavePointMaxMB, delegate(int v)
		{
			SavePointMaxMB = Math.Max(20, v);
		}, () => $"{SavePointMaxMB}");
		yield return Setting.Line("");
		yield return Setting.Line("[World]");
		yield return Setting.Line("; After a time skip (sleep, trainer), up to this many hours of world events are caught up.");
		yield return Setting.Key("World", "CatchUpHours", () => WorldCatchUpMax, delegate(int v)
		{
			WorldCatchUpMax = Math.Max(1, v);
		}, () => $"{WorldCatchUpMax}");
		yield return Setting.Line("; The world goes on without you: clashes between factions, feuds, brawls, life events - on the records, far from you.");
		yield return Setting.Key("World", "Events", () => WorldEvents, delegate(bool v)
		{
			WorldEvents = v;
		}, () => $"{WorldEvents}");
		yield return Setting.Line("; Game hours between events (random in this range).");
		yield return Setting.Key("World", "MinHours", () => WorldMinHours, delegate(int v)
		{
			WorldMinHours = Math.Max(1, v);
		}, () => $"{WorldMinHours}");
		yield return Setting.Key("World", "MaxHours", () => WorldMaxHours, delegate(int v)
		{
			WorldMaxHours = Math.Max(WorldMinHours, v);
		}, () => $"{WorldMaxHours}");
		yield return Setting.Line("; Metres: events happen at least this far from you.");
		yield return Setting.Key("World", "MinDistance", () => WorldMinDistance, delegate(float v)
		{
			WorldMinDistance = v;
		}, () => F(WorldMinDistance) ?? "");
		yield return Setting.Line("; Metres: a battlefield shows a ? on the map only this close (or when you mark it in the World tab).");
		yield return Setting.Key("World", "MarkerRadius", () => WorldMarkerRadius, delegate(float v)
		{
			WorldMarkerRadius = v;
		}, () => F(WorldMarkerRadius) ?? "");
		yield return Setting.Line("; The ? marker's look: POI (a plain white ?), RCM (the stranger-mission style) or CAMP (the ? on a black disc).");
		yield return Setting.Line("; Survivors: SurvivorChance % of battlefields still have 2-4 men at them - the winners holding it or the losers come back.");
		yield return Setting.Line("; Wary men turn on you within SurvivorSpotRange m if they see you (the Army / the Wapiti only with a reason, or if you hurt one).");
		yield return Setting.Key("World", "SurvivorChance", () => WorldSurvivorChance, delegate(int v)
		{
			WorldSurvivorChance = Math.Max(0, Math.Min(100, v));
		}, () => $"{WorldSurvivorChance}");
		yield return Setting.Key("World", "SurvivorSpotRange", () => WorldSurvivorSpotRange, delegate(float v)
		{
			WorldSurvivorSpotRange = Math.Max(5f, v);
		}, () => F(WorldSurvivorSpotRange) ?? "");
		yield return Setting.Key("World", "MarkerStyle", () => WorldMarkerStyle, delegate(string v)
		{
			WorldMarkerStyle = (v ?? "NEUTRAL_SECRET").Trim().ToUpperInvariant();
		}, () => WorldMarkerStyle ?? "");
		yield return Setting.Line("; Game hours a battlefield keeps its bodies (map marker; ride within 140 m to find them).");
		yield return Setting.Key("World", "AftermathHours", () => WorldAftermathHours, delegate(int v)
		{
			WorldAftermathHours = v;
		}, () => $"{WorldAftermathHours}");
		yield return Setting.Line("; % scale on deaths in off-screen fights (killed men still roll cheat death).");
		yield return Setting.Key("World", "Lethality", () => WorldLethality, delegate(int v)
		{
			WorldLethality = v;
		}, () => $"{WorldLethality}");
		yield return Setting.Line("; Each time: BatchMin..BatchMax events at once (at most one clash), picked by these weights.");
		yield return Setting.Key("World", "BatchMin", () => WorldBatchMin, delegate(int v)
		{
			WorldBatchMin = Math.Max(1, v);
		}, () => $"{WorldBatchMin}");
		yield return Setting.Key("World", "BatchMax", () => WorldBatchMax, delegate(int v)
		{
			WorldBatchMax = Math.Max(WorldBatchMin, v);
		}, () => $"{WorldBatchMax}");
		yield return Setting.Line("; % of hours that are busy: BigBatchMin..BigBatchMax events instead.");
		yield return Setting.Key("World", "BigBatchChance", () => WorldBigBatchChance, delegate(int v)
		{
			WorldBigBatchChance = v;
		}, () => $"{WorldBigBatchChance}");
		yield return Setting.Key("World", "BigBatchMin", () => WorldBigBatchMin, delegate(int v)
		{
			WorldBigBatchMin = Math.Max(1, v);
		}, () => $"{WorldBigBatchMin}");
		yield return Setting.Key("World", "BigBatchMax", () => WorldBigBatchMax, delegate(int v)
		{
			WorldBigBatchMax = Math.Max(WorldBigBatchMin, v);
		}, () => $"{WorldBigBatchMax}");
		yield return Setting.Line("; Faction clashes kill men: at most this many a game day (the rosters only refill at the shake-up).");
		yield return Setting.Key("World", "MaxClashesPerDay", () => WorldMaxClashesPerDay, delegate(int v)
		{
			WorldMaxClashesPerDay = v;
		}, () => $"{WorldMaxClashesPerDay}");
		yield return Setting.Key("World", "ClashWeight", () => WorldClashWeight, delegate(int v)
		{
			WorldClashWeight = v;
		}, () => $"{WorldClashWeight}");
		yield return Setting.Line("; How factions feel about each other runs -100..100 (lore start, clashes / deals move it). At or under WarLine two that share");
		yield return Setting.Line("; a district go to war there; at or under ClashLine they fight in the world sim.");
		yield return Setting.Key("World", "WarLine", () => WarLine, delegate(int v)
		{
			WarLine = Math.Max(-100, Math.Min(0, v));
		}, () => $"{WarLine}");
		yield return Setting.Key("World", "ClashLine", () => ClashLine, delegate(int v)
		{
			ClashLine = Math.Max(-100, Math.Min(0, v));
		}, () => $"{ClashLine}");
		yield return Setting.Line("; War zones on the map, and every town's reputation on the map.");
		yield return Setting.Line("; DistantBattles: near a war, now and then a real fight breaks out 200-250 m away, out of sight - you hear it, and can ride over.");
		yield return Setting.Key("World", "DistantBattles", () => DistantBattles, delegate(bool v)
		{
			DistantBattles = v;
		}, () => $"{DistantBattles}");
		yield return Setting.Line("; LockLeaders: True = the outfits' leaders (Colm O'Driscoll, Angelo Bronte, Lindsey Wofford, Rains Fall and the ones the game");
		yield return Setting.Line("; never names) are only met in the game's own missions. False = they're out in the world too - each holds his outfit's");
		yield return Setting.Line("; hideout, his men can give it up, and he can be killed (his seat falls). (Col. Favours is at Fort Wallace either way.)");
		yield return Setting.Key("World", "LockLeaders", () => LockLeaders, delegate(bool v)
		{
			LockLeaders = v;
		}, () => $"{LockLeaders}");
		yield return Setting.Line("; ExplosionType: the game's explosion kind for distant battles and the wagon ambush (25 dynamite, 26 a stack of dynamite, 27 volatile dynamite).");
		yield return Setting.Key("World", "ExplosionType", () => DistantExplosionTag, delegate(int v)
		{
			DistantExplosionTag = Math.Max(0, Math.Min(60, v));
		}, () => $"{DistantExplosionTag}");
		yield return Setting.Key("World", "WarMarkers", () => WarMarkers, delegate(bool v)
		{
			WarMarkers = v;
		}, () => $"{WarMarkers}");
		yield return Setting.Line("; The colour a district at war is shaded in (follows the map's fog). Drew well before: BLIP_STYLE_DEBUG_RED / WANTED_REGION (red),");
		yield return Setting.Line("; AREA_NO_DIM (mustard), FM_EVENT_AREA (purple), EVENT_AREA / SELECTED_REGION (pale).");
		yield return Setting.Key("World", "WarRegionStyle", () => WarRegionStyle, delegate(string v)
		{
			WarRegionStyle = (string.IsNullOrWhiteSpace(v) ? "BLIP_STYLE_DEBUG_RED" : v.Trim());
		}, () => WarRegionStyle ?? "");
		yield return Setting.Key("World", "TownRepMarkers", () => TownRepMarkers, delegate(bool v)
		{
			TownRepMarkers = v;
		}, () => $"{TownRepMarkers}");
		yield return Setting.Line("; World event pop-ups (the World tab's N / pad LS): 0 off, 1 deaths, 2 big news, 3 all.");
		yield return Setting.Key("World", "WorldNews", () => WorldNews, delegate(int v)
		{
			WorldNews = Math.Max(0, Math.Min(3, v));
		}, () => $"{WorldNews}");
		yield return Setting.Key("World", "FeudWeight", () => WorldFeudWeight, delegate(int v)
		{
			WorldFeudWeight = v;
		}, () => $"{WorldFeudWeight}");
		yield return Setting.Key("World", "BrawlWeight", () => WorldBrawlWeight, delegate(int v)
		{
			WorldBrawlWeight = v;
		}, () => $"{WorldBrawlWeight}");
		yield return Setting.Key("World", "RivalryWeight", () => WorldRivalryWeight, delegate(int v)
		{
			WorldRivalryWeight = Math.Max(0, v);
		}, () => $"{WorldRivalryWeight}");
		yield return Setting.Key("World", "VanDerLindeWeight", () => WorldVdlWeight, delegate(int v)
		{
			WorldVdlWeight = Math.Max(0, v);
		}, () => $"{WorldVdlWeight}");
		yield return Setting.Line("; A frightened witness (fear 50+, not scared quiet) tells his town what you did - rare (only when someone is that afraid).");
		yield return Setting.Key("World", "WordWeight", () => WorldWordWeight, delegate(int v)
		{
			WorldWordWeight = Math.Max(0, v);
		}, () => $"{WorldWordWeight}");
		yield return Setting.Key("World", "LifeWeight", () => WorldLifeWeight, delegate(int v)
		{
			WorldLifeWeight = v;
		}, () => $"{WorldLifeWeight}");
		yield return Setting.Line("");
		yield return Setting.Line("[Balance]");
		yield return Setting.Line("; Words and fists (insults, threats, punches, knockouts) only push hate this high: most men / hothead, proud or");
		yield return Setting.Line("; violent / a violent hothead or proud man. Knives, guns, robbery and blood go past it.");
		yield return Setting.Key("Balance", "FistCap", () => Balance.FistCap, delegate(int v)
		{
			Balance.FistCap = v;
		}, () => $"{Balance.FistCap}");
		yield return Setting.Key("Balance", "FistCapHotheadOrViolent", () => Balance.FistCapHot, delegate(int v)
		{
			Balance.FistCapHot = v;
		}, () => $"{Balance.FistCapHot}");
		yield return Setting.Key("Balance", "FistCapViolentHothead", () => Balance.FistCapViolent, delegate(int v)
		{
			Balance.FistCapViolent = v;
		}, () => $"{Balance.FistCapViolent}");
		yield return Setting.Key("Balance", "FistCapRagingHothead", () => Balance.FistCapRage, delegate(int v)
		{
			Balance.FistCapRage = v;
		}, () => $"{Balance.FistCapRage}");
		yield return Setting.Line("; Hate from being knocked out cold (once per KO; half again within 5 min; half for cowards, cold men, the scared).");
		yield return Setting.Key("Balance", "KnockoutHate", () => Balance.KoHate, delegate(int v)
		{
			Balance.KoHate = v;
		}, () => $"{Balance.KoHate}");
		yield return Setting.Line("; Hate tiers: below Vengeful = fists (Resentful), Vengeful = knife, Murderous = guns.");
		yield return Setting.Key("Balance", "HateVengefulAt", () => Hate.VengefulAt, delegate(int v)
		{
			Hate.VengefulAt = v;
		}, () => $"{Hate.VengefulAt}");
		yield return Setting.Key("Balance", "HateMurderousAt", () => Hate.MurderousAt, delegate(int v)
		{
			Hate.MurderousAt = v;
		}, () => $"{Hate.MurderousAt}");
		yield return Setting.Line("; Every hate GAIN is multiplied by this (on top of temper and traits).");
		yield return Setting.Key("Balance", "HateGainScale", () => Balance.HateGainScale, delegate(float v)
		{
			Balance.HateGainScale = v;
		}, () => F(Balance.HateGainScale) ?? "");
		yield return Setting.Line("; How hard a loner's temper moves his starting hate (0 = everyone starts at the wrong's base value).");
		yield return Setting.Key("Balance", "LonerShiftScale", () => Balance.LonerShiftScale, delegate(float v)
		{
			Balance.LonerShiftScale = v;
		}, () => F(Balance.LonerShiftScale) ?? "");
		yield return Setting.Line("; Cowards, drifters and the devout cooling off over time.");
		yield return Setting.Key("Balance", "CoolingScale", () => Balance.CoolingScale, delegate(float v)
		{
			Balance.CoolingScale = v;
		}, () => F(Balance.CoolingScale) ?? "");
		yield return Setting.Line("; Faction and personal notoriety gains.");
		yield return Setting.Key("Balance", "NotorietyGainScale", () => Balance.NotorietyGainScale, delegate(float v)
		{
			Balance.NotorietyGainScale = v;
		}, () => F(Balance.NotorietyGainScale) ?? "");
		yield return Setting.Line("; Faction notoriety: lost per game day; the game's own (unnamed) faction men give 1 point per this many kills.");
		yield return Setting.Key("Balance", "NotorietyDecayPerDay", () => Balance.NotorietyDecayPerDay, delegate(int v)
		{
			Balance.NotorietyDecayPerDay = v;
		}, () => $"{Balance.NotorietyDecayPerDay}");
		yield return Setting.Key("Balance", "UnnamedKillsPerNotoriety", () => Balance.UnnamedKillsPerPoint, delegate(int v)
		{
			Balance.UnnamedKillsPerPoint = Math.Max(1, v);
		}, () => $"{Balance.UnnamedKillsPerPoint}");
		yield return Setting.StateKey("Balance", "LastNotorietyDecayDay", () => LastDecayDay, delegate(int v)
		{
			LastDecayDay = v;
		}, () => $"{LastDecayDay}");
		yield return Setting.Line("; Interrogation: % to talk before temper / traits / fear / pressure (low = Boys, mid = Lieutenants, high = Captains).");
		yield return Setting.Key("Balance", "InterrogateBaseLowRank", () => Balance.InterroBaseBoy, delegate(int v)
		{
			Balance.InterroBaseBoy = v;
		}, () => $"{Balance.InterroBaseBoy}");
		yield return Setting.Key("Balance", "InterrogateBaseMidRank", () => Balance.InterroBaseCaptain, delegate(int v)
		{
			Balance.InterroBaseCaptain = v;
		}, () => $"{Balance.InterroBaseCaptain}");
		yield return Setting.Key("Balance", "InterrogateBaseHighRank", () => Balance.InterroBaseLieutenant, delegate(int v)
		{
			Balance.InterroBaseLieutenant = v;
		}, () => $"{Balance.InterroBaseLieutenant}");
		yield return Setting.Key("Balance", "InterrogateBaseUnnamed", () => Balance.InterroBaseAmbient, delegate(int v)
		{
			Balance.InterroBaseAmbient = v;
		}, () => $"{Balance.InterroBaseAmbient}");
		yield return Setting.Line("; Hate / fear gains are temper x traits x mind; the product is clamped to GainClampMin..GainClampMax.");
		yield return Setting.Key("Balance", "GainClampMin", () => Balance.GainClampMin, delegate(float v)
		{
			Balance.GainClampMin = Math.Max(0.1f, Math.Min(1f, v));
		}, () => F(Balance.GainClampMin) ?? "");
		yield return Setting.Key("Balance", "GainClampMax", () => Balance.GainClampMax, delegate(float v)
		{
			Balance.GainClampMax = Math.Max(1f, Math.Min(4f, v));
		}, () => F(Balance.GainClampMax) ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Fear]");
		yield return Setting.Line("; How afraid of you a man is (0-100). Temper scales gains: coward x2, hothead x0.5, proud x0.6.");
		yield return Setting.Key("Fear", "GainScale", () => Balance.FearGainScale, delegate(float v)
		{
			Balance.FearGainScale = v;
		}, () => F(Balance.FearGainScale) ?? "");
		yield return Setting.Key("Fear", "DecayPer10Min", () => Balance.FearDecayPer10Min, delegate(int v)
		{
			Balance.FearDecayPer10Min = v;
		}, () => $"{Balance.FearDecayPer10Min}");
		yield return Setting.Key("Fear", "PerScar", () => Balance.FearPerScar, delegate(int v)
		{
			Balance.FearPerScar = v;
		}, () => $"{Balance.FearPerScar}");
		yield return Setting.Key("Fear", "PerCripple", () => Balance.FearPerCripple, delegate(int v)
		{
			Balance.FearPerCripple = v;
		}, () => $"{Balance.FearPerCripple}");
		yield return Setting.Key("Fear", "PerKnockout", () => Balance.FearPerKnockout, delegate(int v)
		{
			Balance.FearPerKnockout = v;
		}, () => $"{Balance.FearPerKnockout}");
		yield return Setting.Line("; A man who ran from you: fear gained, hate taken off (half for a nemesis already on your roster).");
		yield return Setting.Key("Fear", "PerFlee", () => Balance.FearPerFlee, delegate(int v)
		{
			Balance.FearPerFlee = v;
		}, () => $"{Balance.FearPerFlee}");
		yield return Setting.Line("; Gore - a man blown apart, his head blown off, butchered with a blade: the same fear for everyone who sees it (witnesses,");
		yield return Setting.Line("; the man you're questioning, nemeses) and the nerve it takes out of men fighting you.");
		yield return Setting.Key("Fear", "Gore", () => Balance.GoreFear, delegate(int v)
		{
			Balance.GoreFear = Math.Max(0, v);
		}, () => $"{Balance.GoreFear}");
		yield return Setting.Key("Fear", "FledHateCut", () => Balance.FledHateCut, delegate(int v)
		{
			Balance.FledHateCut = v;
		}, () => $"{Balance.FledHateCut}");
		yield return Setting.Key("Fear", "PerInterrogation", () => Balance.FearPerInterrogation, delegate(int v)
		{
			Balance.FearPerInterrogation = v;
		}, () => $"{Balance.FearPerInterrogation}");
		yield return Setting.Key("Fear", "PerRobbery", () => Balance.FearPerRobbery, delegate(int v)
		{
			Balance.FearPerRobbery = v;
		}, () => $"{Balance.FearPerRobbery}");
		yield return Setting.Line("; Effective fear = his fear + your notoriety (to him or his faction) divided by this.");
		yield return Setting.Key("Fear", "NotorietyDivisor", () => Balance.NotorietyFearDivisor, delegate(int v)
		{
			Balance.NotorietyFearDivisor = v;
		}, () => $"{Balance.NotorietyFearDivisor}");
		yield return Setting.Line("");
		yield return Setting.Line("[Intent]");
		yield return Setting.Line("; Who comes looking. A man this afraid won't hunt you (he waits in his town)...");
		yield return Setting.Key("Intent", "WontHuntFear", () => Balance.WontHuntFear, delegate(int v)
		{
			Balance.WontHuntFear = v;
		}, () => $"{Balance.WontHuntFear}");
		yield return Setting.Line("; ...and this afraid, he only fights if you start it.");
		yield return Setting.Key("Intent", "ProvokedOnlyFear", () => Balance.ProvokedOnlyFear, delegate(int v)
		{
			Balance.ProvokedOnlyFear = v;
		}, () => $"{Balance.ProvokedOnlyFear}");
		yield return Setting.Line("; A vengeful (knife-tier) man hunts only if he's at least this aggressive (or a hothead / proud).");
		yield return Setting.Key("Intent", "VengefulHuntAggression", () => Balance.VengefulHuntAggression, delegate(int v)
		{
			Balance.VengefulHuntAggression = v;
		}, () => $"{Balance.VengefulHuntAggression}");
		yield return Setting.Line("; While you're in a town: every TownTopUpSeconds, TownTopUpChance % that one more of them turns up (up to TownMaxMen).");
		yield return Setting.Key("Intent", "TownTopUpSeconds", () => Balance.TownTopUpSeconds, delegate(int v)
		{
			Balance.TownTopUpSeconds = v;
		}, () => $"{Balance.TownTopUpSeconds}");
		yield return Setting.Key("Intent", "TownTopUpChance", () => Balance.TownTopUpChance, delegate(int v)
		{
			Balance.TownTopUpChance = v;
		}, () => $"{Balance.TownTopUpChance}");
		yield return Setting.Line("; % per encounter roll IN TOWN (the call-outs; out in the wild it's [Ambush] ChancePercent).");
		yield return Setting.Key("Intent", "TownEncounterChance", () => Balance.TownEncounterChance, delegate(int v)
		{
			Balance.TownEncounterChance = v;
		}, () => $"{Balance.TownEncounterChance}");
		yield return Setting.Key("Intent", "TownEncounterSeconds", () => Balance.TownEncounterSeconds, delegate(int v)
		{
			Balance.TownEncounterSeconds = Math.Max(10, v);
		}, () => $"{Balance.TownEncounterSeconds}");
		yield return Setting.Line("; In town a murderous man only turns up (waiting or calling you out) at this hate or more.");
		yield return Setting.Line("; Towns (TownCalm.cs): ONE man per town visit comes for you (the call-out, murderous only, squares up first); everyone else");
		yield return Setting.Line("; waits for you to provoke him. A second: TownSecondAggressorPct %, rolled once, TownQuietAfterFight s after the first fight.");
		yield return Setting.Line("; A murderous man who sees you may wait on the road out instead: TownWaitOutsideBase % +- his temper and mind; he waits RoadAmbushMinutes.");
		yield return Setting.Line("; Our people in town stand at least TownSpreadMeters apart (inside the saloon they may sit close).");
		yield return Setting.Key("Intent", "TownSpreadMeters", () => Balance.TownSpreadMeters, delegate(float v)
		{
			Balance.TownSpreadMeters = Math.Max(5f, v);
		}, () => F(Balance.TownSpreadMeters) ?? "");
		yield return Setting.Key("Intent", "TownSecondAggressorPct", () => Balance.TownSecondAggressorPct, delegate(int v)
		{
			Balance.TownSecondAggressorPct = Math.Max(0, Math.Min(100, v));
		}, () => $"{Balance.TownSecondAggressorPct}");
		yield return Setting.Key("Intent", "TownQuietAfterFight", () => Balance.TownQuietAfterFight, delegate(int v)
		{
			Balance.TownQuietAfterFight = Math.Max(0, v);
		}, () => $"{Balance.TownQuietAfterFight}");
		yield return Setting.Key("Intent", "TownWaitOutsideBase", () => Balance.TownWaitOutsideBase, delegate(int v)
		{
			Balance.TownWaitOutsideBase = Math.Max(0, Math.Min(100, v));
		}, () => $"{Balance.TownWaitOutsideBase}");
		yield return Setting.Key("Intent", "RoadAmbushMinutes", () => Balance.RoadAmbushMinutes, delegate(int v)
		{
			Balance.RoadAmbushMinutes = Math.Max(1, v);
		}, () => $"{Balance.RoadAmbushMinutes}");
		yield return Setting.Key("Intent", "TownMurderousMinHate", () => Balance.TownMurderousMinHate, delegate(int v)
		{
			Balance.TownMurderousMinHate = v;
		}, () => $"{Balance.TownMurderousMinHate}");
		yield return Setting.Line("; Men whose grudge started elsewhere (within TownPassingRange metres) pass through a town at TownPassingPercent % of the usual chance.");
		yield return Setting.Key("Intent", "TownPassingPercent", () => Balance.TownPassingPercent, delegate(int v)
		{
			Balance.TownPassingPercent = v;
		}, () => $"{Balance.TownPassingPercent}");
		yield return Setting.Key("Intent", "TownPassingRange", () => Balance.TownPassingRange, delegate(float v)
		{
			Balance.TownPassingRange = v;
		}, () => F(Balance.TownPassingRange) ?? "");
		yield return Setting.Line("; Size of the part of town people are placed in (x each town's own radius, Valentine 130 m around saloon / doctor).");
		yield return Setting.Key("Intent", "TownCoreScale", () => Balance.TownCoreScale, delegate(float v)
		{
			Balance.TownCoreScale = v;
		}, () => F(Balance.TownCoreScale) ?? "");
		yield return Setting.Line("; Riding into a town: this many nemeses (min..max, up to TownMaxMen) are already there.");
		yield return Setting.Key("Intent", "TownArrivalMin", () => Balance.TownArrivalMin, delegate(int v)
		{
			Balance.TownArrivalMin = Math.Max(0, v);
		}, () => $"{Balance.TownArrivalMin}");
		yield return Setting.Key("Intent", "TownArrivalMax", () => Balance.TownArrivalMax, delegate(int v)
		{
			Balance.TownArrivalMax = Math.Max(Balance.TownArrivalMin, v);
		}, () => $"{Balance.TownArrivalMax}");
		yield return Setting.Line("; Most nemeses waiting in one town at once.");
		yield return Setting.Key("Intent", "TownMaxMen", () => Balance.TownMaxMen, delegate(int v)
		{
			Balance.TownMaxMen = Math.Max(1, v);
		}, () => $"{Balance.TownMaxMen}");
		yield return Setting.Line("; In town the hotter men show up less (% of the normal chance), and a second murderous man at once only this % of that.");
		yield return Setting.Key("Intent", "TownVengefulPercent", () => Balance.TownVengefulPct, delegate(int v)
		{
			Balance.TownVengefulPct = v;
		}, () => $"{Balance.TownVengefulPct}");
		yield return Setting.Key("Intent", "TownMurderousPercent", () => Balance.TownMurderousPct, delegate(int v)
		{
			Balance.TownMurderousPct = v;
		}, () => $"{Balance.TownMurderousPct}");
		yield return Setting.Key("Intent", "TownSecondMurderousPercent", () => Balance.TownSecondMurderousPct, delegate(int v)
		{
			Balance.TownSecondMurderousPct = v;
		}, () => $"{Balance.TownSecondMurderousPct}");
		yield return Setting.Line("; A man who waits still starts it if you come this close (metres) where he can see you.");
		yield return Setting.Key("Intent", "WontHuntSpotRange", () => Balance.WontHuntSpotRange, delegate(int v)
		{
			Balance.WontHuntSpotRange = v;
		}, () => $"{Balance.WontHuntSpotRange}");
		yield return Setting.Line("");
		yield return Setting.Line("[Factions]");
		yield return Setting.Line("; How often each faction's men turn up in random encounters (1.0 = like a personal nemesis), before notoriety.");
		yield return Setting.Line(() => string.Join("\r\n", Gangs.Defined.Select((Gang g) => g.Id + "Encounters=" + F((float)g.AmbushBase))) ?? "");
		yield return Setting.Line("; The Army only comes for you at this notoriety with it, or with a bounty of this many dollars.");
		yield return Setting.Key("Factions", "ArmyNotorietyGate", () => Balance.ArmyNotorietyGate, delegate(int v)
		{
			Balance.ArmyNotorietyGate = v;
		}, () => $"{Balance.ArmyNotorietyGate}");
		yield return Setting.Key("Factions", "ArmyBountyGate", () => Balance.ArmyBountyGate, delegate(int v)
		{
			Balance.ArmyBountyGate = v;
		}, () => $"{Balance.ArmyBountyGate}");
		yield return Setting.Line("; % every 10 minutes that a Raider shoots or shoves his way up the ranks.");
		yield return Setting.Key("Factions", "RaiderShakeupChance", () => Balance.RaiderShakeupChance, delegate(int v)
		{
			Balance.RaiderShakeupChance = v;
		}, () => $"{Balance.RaiderShakeupChance}");
		yield return Setting.Line("");
		yield return Setting.Line("[Naming]");
		yield return Setting.Line("; Everyone within Radius metres gets a name; the people you deal with keep theirs longer. Cap = most names remembered at");
		yield return Setting.Line("; once. (Names over heads: [Menu] NameCards - or C / pad RS in the nemesis menu.)");
		yield return Setting.Key("Naming", "Enabled", () => NamingEnabled, delegate(bool v)
		{
			NamingEnabled = v;
		}, () => $"{NamingEnabled}");
		yield return Setting.Key("Naming", "Radius", () => NamingRadius, delegate(float v)
		{
			NamingRadius = v;
		}, () => F(NamingRadius) ?? "");
		yield return Setting.Key("Naming", "Cap", () => NamingCap, delegate(int v)
		{
			NamingCap = v;
		}, () => $"{NamingCap}");
		yield return Setting.Line("; % of people who get a nickname (\"the Buzzard\", \"Calico\"): gang men more often than everyone else. Existing nicknames");
		yield return Setting.Line("; were thinned to these odds once (the same men keep theirs everywhere: by name).");
		yield return Setting.Key("Naming", "NicknameGangPercent", () => NicknameGangPercent, delegate(int v)
		{
			NicknameGangPercent = Math.Max(0, Math.Min(100, v));
		}, () => $"{NicknameGangPercent}");
		yield return Setting.Key("Naming", "NicknameLonerPercent", () => NicknameLonerPercent, delegate(int v)
		{
			NicknameLonerPercent = Math.Max(0, Math.Min(100, v));
		}, () => $"{NicknameLonerPercent}");
		yield return Setting.Line("; % of townsfolk secretly in with a gang (crimes against them reach that gang's notoriety).");
		yield return Setting.Key("Naming", "AffiliatedPercent", () => AffiliatedPercent, delegate(float v)
		{
			AffiliatedPercent = Math.Max(0f, Math.Min(100f, v));
		}, () => F(AffiliatedPercent) ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Disguise]");
		yield return Setting.Line("; Masked, a stranger you wrong never sees your face - he can't become a nemesis.");
		yield return Setting.Key("Disguise", "Enabled", () => DisguiseEnabled, delegate(bool v)
		{
			DisguiseEnabled = v;
		}, () => $"{DisguiseEnabled}");
		yield return Setting.Line("; Your clothing categories that count as a disguise (the debug overlay, Ctrl+F11, lists what you wear).");
		yield return Setting.Key("Disguise", "Categories", () => DisguiseCategories, delegate(string v)
		{
			DisguiseCategories = v;
		}, () => DisguiseCategories ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[GrudgeScore]");
		yield return Setting.Line("; How a stranger becomes a nemesis: every action adds (or takes away) its own amount from a 0-100 grudge score,");
		yield return Setting.Line("; scaled by his temper. At Threshold he's a nemesis; below it, when things go quiet (SettleSeconds) or he leaves,");
		yield return Setting.Line("; his chance is score - Floor (%). The debug overlay (Ctrl+F11) shows each stranger's meter.");
		yield return Setting.Key("GrudgeScore", "Threshold", () => Balance.GsThreshold, delegate(int v)
		{
			Balance.GsThreshold = v;
		}, () => $"{Balance.GsThreshold}");
		yield return Setting.Key("GrudgeScore", "Floor", () => Balance.GsFloor, delegate(int v)
		{
			Balance.GsFloor = v;
		}, () => $"{Balance.GsFloor}");
		yield return Setting.Key("GrudgeScore", "SettleSeconds", () => Balance.GsSettleSeconds, delegate(int v)
		{
			Balance.GsSettleSeconds = v;
		}, () => $"{Balance.GsSettleSeconds}");
		yield return Setting.Key("GrudgeScore", "Greet", () => Balance.GsGreet, delegate(float v)
		{
			Balance.GsGreet = v;
		}, () => F(Balance.GsGreet) ?? "");
		yield return Setting.Line("; Greeting a nemesis takes a little hate off (2, x0.5-1.4 by his temper and nature). Greets within GreetStreakMinutes of each other");
		yield return Setting.Line("; build a streak: the 3rd adds GreetStreakBonus more; after that a greet is worth half, after the 6th nothing.");
		yield return Setting.Key("GrudgeScore", "GreetStreakMinutes", () => Balance.GreetStreakMinutes, delegate(int v)
		{
			Balance.GreetStreakMinutes = Math.Max(1, v);
		}, () => $"{Balance.GreetStreakMinutes}");
		yield return Setting.Key("GrudgeScore", "GreetStreakBonus", () => Balance.GreetStreakBonus, delegate(float v)
		{
			Balance.GreetStreakBonus = Math.Max(0f, v);
		}, () => F(Balance.GreetStreakBonus) ?? "");
		yield return Setting.Key("GrudgeScore", "Defuse", () => Balance.GsDefuse, delegate(float v)
		{
			Balance.GsDefuse = v;
		}, () => F(Balance.GsDefuse) ?? "");
		yield return Setting.Key("GrudgeScore", "Antagonize", () => Balance.GsInsult, delegate(float v)
		{
			Balance.GsInsult = v;
		}, () => F(Balance.GsInsult) ?? "");
		yield return Setting.Key("GrudgeScore", "DrawOn", () => Balance.GsDrawOn, delegate(float v)
		{
			Balance.GsDrawOn = v;
		}, () => F(Balance.GsDrawOn) ?? "");
		yield return Setting.Key("GrudgeScore", "Intimidate", () => Balance.GsIntimidate, delegate(float v)
		{
			Balance.GsIntimidate = v;
		}, () => F(Balance.GsIntimidate) ?? "");
		yield return Setting.Key("GrudgeScore", "StickUp", () => Balance.GsStickUp, delegate(float v)
		{
			Balance.GsStickUp = v;
		}, () => F(Balance.GsStickUp) ?? "");
		yield return Setting.Key("GrudgeScore", "Punch", () => Balance.GsPunch, delegate(float v)
		{
			Balance.GsPunch = v;
		}, () => F(Balance.GsPunch) ?? "");
		yield return Setting.Key("GrudgeScore", "KnockOut", () => Balance.GsKnockOut, delegate(float v)
		{
			Balance.GsKnockOut = v;
		}, () => F(Balance.GsKnockOut) ?? "");
		yield return Setting.Key("GrudgeScore", "Hogtie", () => Balance.GsHogtie, delegate(float v)
		{
			Balance.GsHogtie = v;
		}, () => F(Balance.GsHogtie) ?? "");
		yield return Setting.Key("GrudgeScore", "Robbed", () => Balance.GsRobbed, delegate(float v)
		{
			Balance.GsRobbed = v;
		}, () => F(Balance.GsRobbed) ?? "");
		yield return Setting.Key("GrudgeScore", "HorseStolen", () => Balance.GsHorseStolen, delegate(float v)
		{
			Balance.GsHorseStolen = v;
		}, () => F(Balance.GsHorseStolen) ?? "");
		yield return Setting.Key("GrudgeScore", "Cut", () => Balance.GsSlashed, delegate(float v)
		{
			Balance.GsSlashed = v;
		}, () => F(Balance.GsSlashed) ?? "");
		yield return Setting.Line("; Shot: the first hit; ShotAgain: each later hit (two at most). A man you never hit (only shot at, fought, saw a man die) tops out at");
		yield return Setting.Line("; UnhitCap - he can still roll, never automatic. A grudge he let go only reopens once it's grown Reopen more. A kindness takes at most");
		yield return Setting.Line("; KindMax off a stranger's grudge, and nothing within KindWaitMinutes of you hurting or robbing him.");
		yield return Setting.Key("GrudgeScore", "ShotAgain", () => Balance.GsShotAgain, delegate(float v)
		{
			Balance.GsShotAgain = v;
		}, () => F(Balance.GsShotAgain) ?? "");
		yield return Setting.Key("GrudgeScore", "UnhitCap", () => Balance.GsUnhitCap, delegate(float v)
		{
			Balance.GsUnhitCap = v;
		}, () => F(Balance.GsUnhitCap) ?? "");
		yield return Setting.Line("; WoundCap: one bullet in a gunfight (plus fighting back, shots past him, men killed near him) tops out here - under the line, so it's a roll. Hit him again and the line is open.");
		yield return Setting.Key("GrudgeScore", "WoundCap", () => Balance.GsWoundCap, delegate(float v)
		{
			Balance.GsWoundCap = Math.Max(0f, Math.Min(100f, v));
		}, () => F(Balance.GsWoundCap) ?? "");
		yield return Setting.Key("GrudgeScore", "Reopen", () => Balance.GsReopen, delegate(float v)
		{
			Balance.GsReopen = v;
		}, () => F(Balance.GsReopen) ?? "");
		yield return Setting.Key("GrudgeScore", "KindMax", () => Balance.GsKindMax, delegate(float v)
		{
			Balance.GsKindMax = v;
		}, () => F(Balance.GsKindMax) ?? "");
		yield return Setting.Key("GrudgeScore", "KindWaitMinutes", () => Balance.GsKindWaitMin, delegate(float v)
		{
			Balance.GsKindWaitMin = v;
		}, () => F(Balance.GsKindWaitMin) ?? "");
		yield return Setting.Key("GrudgeScore", "Shot", () => Balance.GsWounded, delegate(float v)
		{
			Balance.GsWounded = v;
		}, () => F(Balance.GsWounded) ?? "");
		yield return Setting.Key("GrudgeScore", "FoughtBack", () => Balance.GsFoughtBack, delegate(float v)
		{
			Balance.GsFoughtBack = v;
		}, () => F(Balance.GsFoughtBack) ?? "");
		yield return Setting.Line("; Gunfights: a shot that misses him by a hair, and each man you kill near him.");
		yield return Setting.Key("GrudgeScore", "ShotAt", () => Balance.GsShotAt, delegate(float v)
		{
			Balance.GsShotAt = v;
		}, () => F(Balance.GsShotAt) ?? "");
		yield return Setting.Key("GrudgeScore", "ComradeKilled", () => Balance.GsComradeKilled, delegate(float v)
		{
			Balance.GsComradeKilled = v;
		}, () => F(Balance.GsComradeKilled) ?? "");
		yield return Setting.Line("");
		int value;
		yield return Setting.StateLine("Notoriety", () => string.Join("\r\n", Gangs.Defined.Select((Gang g) => g.Id + "=" + (Notoriety.TryGetValue(g.Id, out value) ? value : 0))) ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Menu]");
		yield return Setting.Line("; The war you're standing in, shown while you hold the game's reveal-HUD button (D-pad Down): on / off, and where (screen 0-1: left (-1 = centred), top, width).");
		yield return Setting.Key("Menu", "WarHud", () => WarHud, delegate(bool v)
		{
			WarHud = v;
		}, () => $"{WarHud}");
		yield return Setting.Key("Menu", "WarHudX", () => WarHudX, delegate(float v)
		{
			WarHudX = ((v < 0f) ? (-1f) : Math.Min(0.8f, v));
		}, () => F(WarHudX) ?? "");
		yield return Setting.Key("Menu", "WarHudY", () => WarHudY, delegate(float v)
		{
			WarHudY = Math.Max(0f, Math.Min(0.95f, v));
		}, () => F(WarHudY) ?? "");
		yield return Setting.Key("Menu", "WarHudW", () => WarHudW, delegate(float v)
		{
			WarHudW = Math.Max(0.12f, Math.Min(0.5f, v));
		}, () => F(WarHudW) ?? "");
		yield return Setting.Line("; Name cards over people's heads (outside debug): 0 off, 1 people you know (nemeses, met, anyone with a grudge), 2 everyone named,");
		yield return Setting.Line("; 3 on target (only the one you hold LT / right-click on, or aim at).");
		yield return Setting.Key("Menu", "NameCards", () => NameCards, delegate(int v)
		{
			NameCards = Math.Max(0, Math.Min(3, v));
		}, () => $"{NameCards}");
		yield return Setting.Line("; Metres: strangers' cards (NameCardRange) and nemeses' / people you know (NameCardKnownRange); they fade out near the edge.");
		yield return Setting.Key("Menu", "NameCardRange", () => NameCardRange, delegate(float v)
		{
			NameCardRange = v;
		}, () => F(NameCardRange) ?? "");
		yield return Setting.Key("Menu", "NameCardKnownRange", () => NameCardKnownRange, delegate(float v)
		{
			NameCardKnownRange = v;
		}, () => F(NameCardKnownRange) ?? "");
		yield return Setting.Line("; Name card steadiness: NameCardSteady = how hard a nearly-still card is smoothed (lower = steadier, 1-5), NameCardLead = frames");
		yield return Setting.Line("; it leads a turning camera (0 = none; more = less trailing but can overshoot).");
		yield return Setting.Key("Menu", "NameCardSteady", () => NameCardSteady, delegate(float v)
		{
			NameCardSteady = Math.Max(0.3f, v);
		}, () => F(NameCardSteady) ?? "");
		yield return Setting.Key("Menu", "NameCardLead", () => NameCardLead, delegate(float v)
		{
			NameCardLead = Math.Max(0f, Math.Min(3f, v));
		}, () => F(NameCardLead) ?? "");
		yield return Setting.Line("; On-screen nemesis text: 0 = none (runs silently), 1 = speech only (taunts, lines people say),");
		yield return Setting.Line("; 2 = speech + deaths, 3 = everything (rises, sightings, intel, rank-ups...), 4 = deaths only. N in the hierarchy menu cycles it.");
		yield return Setting.Line(() => $"Messages={MessageLevel}");
		yield return Setting.Line("; Freeze the world (time scale 0, clock stopped) while the hierarchy or X-ray log is open.");
		yield return Setting.Key("Menu", "PauseWorld", () => PauseInMenus, delegate(bool v)
		{
			PauseInMenus = v;
		}, () => $"{PauseInMenus}");
		yield return Setting.Line("; Draw the menus in the game's own fonts. If you see raw <FONT ...> text instead, set this to False.");
		yield return Setting.Line("; FitTo169: menus keep their 1920x1080 layout and sit centred on ultrawide / 16:10 / 4:3 screens (False = stretch across the screen)");
		yield return Setting.Key("Menu", "FitTo169", () => FitTo169, delegate(bool v)
		{
			FitTo169 = v;
		}, () => $"{FitTo169}");
		yield return Setting.Line("; WelcomeSeen: the welcome guide has been shown. False = it opens again on the next load (F11 then G any time).");
		yield return Setting.Key("Menu", "WelcomeSeen", () => WelcomeSeen, delegate(bool v)
		{
			WelcomeSeen = v;
		}, () => $"{WelcomeSeen}");
		yield return Setting.Line("; SetupSeen: the first-run setup (messages, name cards, encounters, menu key) has been shown. False = it shows again on the next load.");
		yield return Setting.Key("Menu", "SetupSeen", () => SetupSeen, delegate(bool v)
		{
			SetupSeen = v;
		}, () => $"{SetupSeen}");
		yield return Setting.Line("; WhatsNewSeen: the version whose What's new page has been shown (it shows once to players who had the mod before).");
		yield return Setting.Key("Menu", "WhatsNewSeen", () => WhatsNewSeen, delegate(string v)
		{
			WhatsNewSeen = v ?? "";
		}, () => WhatsNewSeen ?? "");
		yield return Setting.Key("Menu", "GameFont", () => GameFont, delegate(bool v)
		{
			GameFont = v;
		}, () => $"{GameFont}");
		yield return Setting.Line("; Controller button icons: Auto (PlayStation symbols when a Sony controller is connected), Xbox or PlayStation.");
		yield return Setting.Key("Menu", "PadIcons", () => PadIcons, delegate(string v)
		{
			PadIcons = (v ?? "Auto").Trim();
		}, () => PadIcons ?? "");
		yield return Setting.Line("; World events as toasts in the game's own top-left feed. EXPERIMENTAL - off by default: the first try crashed the game.");
		yield return Setting.Key("Menu", "GameFeed", () => GameFeed, delegate(bool v)
		{
			GameFeed = v;
		}, () => $"{GameFeed}");
		yield return Setting.Line("; Interrogation categories (What... When...) end in a plain > character. False = small drawn chevrons instead.");
		yield return Setting.Key("Menu", "MenuArrowText", () => MenuArrowText, delegate(bool v)
		{
			MenuArrowText = v;
		}, () => $"{MenuArrowText}");
		yield return Setting.Line("; Font faces: TitleFont for headings and names, BodyFont for everything else (e.g. $title, $body).");
		yield return Setting.Key("Menu", "TitleFont", () => TitleFont, delegate(string v)
		{
			TitleFont = v;
		}, () => TitleFont ?? "");
		yield return Setting.Key("Menu", "BodyFont", () => BodyFont, delegate(string v)
		{
			BodyFont = v;
		}, () => BodyFont ?? "");
		yield return Setting.Line("; Text pieces the menus may build per frame (the game reuses its ~256 string slots past that and the");
		yield return Setting.Line("; FIRST texts of the frame go blank). Lower it if names blank out; game-font text costs 2.");
		yield return Setting.Key("Menu", "TextSlotLimit", () => TextSlotLimit, delegate(int v)
		{
			TextSlotLimit = v;
		}, () => $"{TextSlotLimit}");
		yield return Setting.Line("; Underline the selected tab / faction with the game's own text underline (False = a drawn line under it).");
		yield return Setting.Key("Menu", "TextUnderline", () => TextUnderline, delegate(bool v)
		{
			TextUnderline = v;
		}, () => $"{TextUnderline}");
		yield return Setting.Line("; The selection frame = the game's own textured red highlight (False = a plain 2 px red line). Size = corner pieces in");
		yield return Setting.Line("; pixels, Edge = thickness of the side pieces, Out = how far it sits outside the box, Tint = colour the textures red.");
		yield return Setting.Key("Menu", "SelectFrame", () => SelectFrame, delegate(bool v)
		{
			SelectFrame = v;
		}, () => $"{SelectFrame}");
		yield return Setting.Key("Menu", "SelectFrameSize", () => SelectFrameSize, delegate(int v)
		{
			SelectFrameSize = Math.Max(2, Math.Min(64, v));
		}, () => $"{SelectFrameSize}");
		yield return Setting.Key("Menu", "SelectFrameEdge", () => SelectFrameEdge, delegate(int v)
		{
			SelectFrameEdge = Math.Max(1, Math.Min(64, v));
		}, () => $"{SelectFrameEdge}");
		yield return Setting.Key("Menu", "SelectFrameOut", () => SelectFrameOut, delegate(int v)
		{
			SelectFrameOut = Math.Max(-16, Math.Min(32, v));
		}, () => $"{SelectFrameOut}");
		yield return Setting.Line("; InsetX = % of the side pieces' width the frame is pushed out sideways (their line sits inside the texture).");
		yield return Setting.Key("Menu", "SelectFrameInsetX", () => SelectFrameInsetX, delegate(int v)
		{
			SelectFrameInsetX = Math.Max(0, Math.Min(100, v));
		}, () => $"{SelectFrameInsetX}");
		yield return Setting.Key("Menu", "SelectFrameInsetY", () => SelectFrameInsetY, delegate(int v)
		{
			SelectFrameInsetY = Math.Max(0, Math.Min(100, v));
		}, () => $"{SelectFrameInsetY}");
		yield return Setting.Line("; Pixels the red frame round a selected hierarchy card is moved up (it sat low on the card).");
		yield return Setting.Key("Menu", "CardFrameUp", () => CardFrameUp, delegate(int v)
		{
			CardFrameUp = Math.Max(-20, Math.Min(20, v));
		}, () => $"{CardFrameUp}");
		yield return Setting.Key("Menu", "SelectFrameTint", () => SelectFrameTint, delegate(bool v)
		{
			SelectFrameTint = v;
		}, () => $"{SelectFrameTint}");
		yield return Setting.Line("; The ragged ink edge around the menus (a game texture behind each panel). Off = plain black panels.");
		yield return Setting.Key("Menu", "Ink", () => MenuInk, delegate(bool v)
		{
			MenuInk = v;
		}, () => $"{MenuInk}");
		yield return Setting.Key("Menu", "InkDict", () => MenuInkDict, delegate(string v)
		{
			MenuInkDict = v;
		}, () => MenuInkDict ?? "");
		yield return Setting.Key("Menu", "InkTexture", () => MenuInkTexture, delegate(string v)
		{
			MenuInkTexture = v;
		}, () => MenuInkTexture ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Tension]");
		yield return Setting.Line("; The director: encounters come when TENSION (0-100) reaches a fire level (a new random one between FireMin and FireMax");
		yield return Setting.Line("; each time), not on a timer. It rises RisePerMin a minute (+NotorietyBonus per notoriety level, +NightBonus at night,");
		yield return Setting.Line("; x TownMult in town, x HurtMult when you're badly hurt), is held while nothing can come, and drains CombatDrainPerSec");
		yield return Setting.Line("; a second in a fight. Something came -> it drops to AfterEncounter; after a fight to AfterFight. Enabled=False = the old timer.");
		yield return Setting.Key("Tension", "Enabled", () => TensionEnabled, delegate(bool v)
		{
			TensionEnabled = v;
		}, () => $"{TensionEnabled}");
		yield return Setting.Key("Tension", "RisePerMin", () => TensionRisePerMin, delegate(float v)
		{
			TensionRisePerMin = Math.Max(0f, v);
		}, () => F(TensionRisePerMin) ?? "");
		yield return Setting.Key("Tension", "TownMult", () => TensionTownMult, delegate(float v)
		{
			TensionTownMult = Math.Max(0f, v);
		}, () => F(TensionTownMult) ?? "");
		yield return Setting.Key("Tension", "NightBonus", () => TensionNightBonus, delegate(float v)
		{
			TensionNightBonus = v;
		}, () => F(TensionNightBonus) ?? "");
		yield return Setting.Key("Tension", "NotorietyBonus", () => TensionNotorietyBonus, delegate(float v)
		{
			TensionNotorietyBonus = v;
		}, () => F(TensionNotorietyBonus) ?? "");
		yield return Setting.Key("Tension", "HurtMult", () => TensionHurtMult, delegate(float v)
		{
			TensionHurtMult = Math.Max(0f, v);
		}, () => F(TensionHurtMult) ?? "");
		yield return Setting.Line("; A fight starting drops it FightStartDrop at once; then it drains DrainBase a second + DrainPerMan for each man fighting you, and KillDrain for every man killed;");
		yield return Setting.Line("; all of it x OursMult when the men are the mod's own (an encounter, a camp, a patrol). AfterFight caps it when such a fight ends.");
		yield return Setting.Key("Tension", "FightStartDrop", () => TensionFightStartDrop, delegate(float v)
		{
			TensionFightStartDrop = Math.Max(0f, v);
		}, () => F(TensionFightStartDrop) ?? "");
		yield return Setting.Key("Tension", "DrainBase", () => TensionDrainBase, delegate(float v)
		{
			TensionDrainBase = Math.Max(0f, v);
		}, () => F(TensionDrainBase) ?? "");
		yield return Setting.Key("Tension", "DrainPerMan", () => TensionDrainPerMan, delegate(float v)
		{
			TensionDrainPerMan = Math.Max(0f, v);
		}, () => F(TensionDrainPerMan) ?? "");
		yield return Setting.Key("Tension", "KillDrain", () => TensionKillDrain, delegate(float v)
		{
			TensionKillDrain = Math.Max(0f, v);
		}, () => F(TensionKillDrain) ?? "");
		yield return Setting.Key("Tension", "OursMult", () => TensionOursMult, delegate(float v)
		{
			TensionOursMult = Math.Max(1f, v);
		}, () => F(TensionOursMult) ?? "");
		yield return Setting.Key("Tension", "FireMin", () => TensionFireMin, delegate(float v)
		{
			TensionFireMin = Math.Max(5f, Math.Min(100f, v));
		}, () => F(TensionFireMin) ?? "");
		yield return Setting.Key("Tension", "FireMax", () => TensionFireMax, delegate(float v)
		{
			TensionFireMax = Math.Max(TensionFireMin, Math.Min(100f, v));
		}, () => F(TensionFireMax) ?? "");
		yield return Setting.Line("; Out of town it fires lower (the same roll, mapped onto WildFireMin..WildFireMax) - the wild had no natural encounters at the town level");
		yield return Setting.Key("Tension", "WildFireMin", () => TensionWildFireMin, delegate(float v)
		{
			TensionWildFireMin = Math.Max(5f, Math.Min(100f, v));
		}, () => F(TensionWildFireMin) ?? "");
		yield return Setting.Key("Tension", "WildFireMax", () => TensionWildFireMax, delegate(float v)
		{
			TensionWildFireMax = Math.Max(TensionWildFireMin, Math.Min(100f, v));
		}, () => F(TensionWildFireMax) ?? "");
		yield return Setting.Key("Tension", "AfterEncounter", () => TensionAfterEncounter, delegate(float v)
		{
			TensionAfterEncounter = Math.Max(0f, Math.Min(100f, v));
		}, () => F(TensionAfterEncounter) ?? "");
		yield return Setting.Key("Tension", "AfterFight", () => TensionAfterFight, delegate(float v)
		{
			TensionAfterFight = Math.Max(0f, Math.Min(100f, v));
		}, () => F(TensionAfterFight) ?? "");
		yield return Setting.Line("; The first wild encounter after a quiet spell comes sooner: once FirstMinDays..FirstMaxDays game days have passed since the last");
		yield return Setting.Line("; one out of town, the wild fire level is x FirstMult until the next one comes. A fight with anyone but an encounter or a camp");
		yield return Setting.Line("; (strangers, town men, patrols) only takes it down to StrangerKeep of where the fight started.");
		yield return Setting.Key("Tension", "FirstMult", () => TensionFirstMult, delegate(float v)
		{
			TensionFirstMult = Math.Max(0.05f, Math.Min(1f, v));
		}, () => F(TensionFirstMult) ?? "");
		yield return Setting.Key("Tension", "FirstMinDays", () => TensionFirstMinDays, delegate(float v)
		{
			TensionFirstMinDays = Math.Max(0.1f, Math.Min(30f, v));
		}, () => F(TensionFirstMinDays) ?? "");
		yield return Setting.Key("Tension", "FirstMaxDays", () => TensionFirstMaxDays, delegate(float v)
		{
			TensionFirstMaxDays = Math.Max(TensionFirstMinDays, Math.Min(30f, v));
		}, () => F(TensionFirstMaxDays) ?? "");
		yield return Setting.Key("Tension", "StrangerKeep", () => TensionStrangerKeep, delegate(float v)
		{
			TensionStrangerKeep = Math.Max(0f, Math.Min(1f, v));
		}, () => F(TensionStrangerKeep) ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Encounters]");
		yield return Setting.Line("; Amount: how often someone turns up - 0 Fewer (x0.5), 1 Normal, 2 More (x1.6), 3 Lots (x2.4). It scales how fast tension builds.");
		yield return Setting.Key("Encounters", "Amount", () => EncounterAmount, delegate(int v)
		{
			EncounterAmount = Math.Max(0, Math.Min(3, v));
		}, () => $"{EncounterAmount}");
		yield return Setting.Line("; When the ambush timer fires, one of these happens - only kinds that fit where you are compete:");
		yield return Setting.Line("; Ambush (wilds), Riders (you are on horseback; they ride up behind you), Ahead (you are riding;");
		yield return Setting.Line("; they wait ahead on the road), Sniper (wilds, Murderous men only), Callout (in town: he walks up).");
		yield return Setting.Key("Encounters", "Ambush", () => EncAmbush, delegate(int v)
		{
			EncAmbush = v;
		}, () => $"{EncAmbush}");
		yield return Setting.Key("Encounters", "Riders", () => EncRiders, delegate(int v)
		{
			EncRiders = v;
		}, () => $"{EncRiders}");
		yield return Setting.Key("Encounters", "Ahead", () => EncAhead, delegate(int v)
		{
			EncAhead = v;
		}, () => $"{EncAhead}");
		yield return Setting.Key("Encounters", "Sniper", () => EncSniper, delegate(int v)
		{
			EncSniper = v;
		}, () => $"{EncSniper}");
		yield return Setting.Key("Encounters", "Callout", () => EncCallout, delegate(int v)
		{
			EncCallout = v;
		}, () => $"{EncCallout}");
		yield return Setting.Line("; % of encounter rolls that become two factions fighting each other near you (Army/O'Driscolls vs Raiders).");
		yield return Setting.Key("Encounters", "SkirmishChance", () => Balance.SkirmishChance, delegate(int v)
		{
			Balance.SkirmishChance = v;
		}, () => $"{Balance.SkirmishChance}");
		yield return Setting.Line("; % of encounter rolls that are a faction patrol on the road instead (Army in the north, Raiders in Lemoyne).");
		yield return Setting.Key("Encounters", "ArmyPatrolChance", () => Balance.PatrolChance, delegate(int v)
		{
			Balance.PatrolChance = v;
		}, () => $"{Balance.PatrolChance}");
		yield return Setting.Line("; A brand-new nemesis lies low this many game hours (no encounters, not in town). After you respawn, no encounter roll for RespawnQuietSeconds.");
		yield return Setting.Key("Encounters", "NewNemesisRestHours", () => Balance.NewNemesisRestHours, delegate(int v)
		{
			Balance.NewNemesisRestHours = v;
		}, () => $"{Balance.NewNemesisRestHours}");
		yield return Setting.Key("Encounters", "RespawnQuietSeconds", () => Balance.RespawnQuietSeconds, delegate(int v)
		{
			Balance.RespawnQuietSeconds = v;
		}, () => $"{Balance.RespawnQuietSeconds}");
		yield return Setting.Line("; How often each grudge comes for you (x his other weights): out in the wild, and in town (callouts).");
		yield return Setting.Key("Encounters", "WildResentful", () => Balance.WildResentful, delegate(float v)
		{
			Balance.WildResentful = v;
		}, () => F(Balance.WildResentful) ?? "");
		yield return Setting.Key("Encounters", "WildVengeful", () => Balance.WildVengeful, delegate(float v)
		{
			Balance.WildVengeful = v;
		}, () => F(Balance.WildVengeful) ?? "");
		yield return Setting.Key("Encounters", "WildMurderous", () => Balance.WildMurderous, delegate(float v)
		{
			Balance.WildMurderous = v;
		}, () => F(Balance.WildMurderous) ?? "");
		yield return Setting.Key("Encounters", "TownResentful", () => Balance.TownResentful, delegate(float v)
		{
			Balance.TownResentful = v;
		}, () => F(Balance.TownResentful) ?? "");
		yield return Setting.Key("Encounters", "TownVengeful", () => Balance.TownVengefulW, delegate(float v)
		{
			Balance.TownVengefulW = v;
		}, () => F(Balance.TownVengefulW) ?? "");
		yield return Setting.Key("Encounters", "TownMurderous", () => Balance.TownMurderousW, delegate(float v)
		{
			Balance.TownMurderousW = v;
		}, () => F(Balance.TownMurderousW) ?? "");
		yield return Setting.Line("; Game hours: after a murderous man came for you (wild or town), none for this long; the same man not again for ManCooldownHours.");
		yield return Setting.Key("Encounters", "MurderousCooldownHours", () => Balance.MurderousCooldownHours, delegate(int v)
		{
			Balance.MurderousCooldownHours = v;
		}, () => $"{Balance.MurderousCooldownHours}");
		yield return Setting.Line("; Seconds a man squares up and talks before the fight (greet = he may back down; antagonize / draw / hit = it starts).");
		yield return Setting.Key("Encounters", "StandoffResentful", () => Balance.StandoffResentful, delegate(int v)
		{
			Balance.StandoffResentful = v;
		}, () => $"{Balance.StandoffResentful}");
		yield return Setting.Key("Encounters", "StandoffVengeful", () => Balance.StandoffVengeful, delegate(int v)
		{
			Balance.StandoffVengeful = v;
		}, () => $"{Balance.StandoffVengeful}");
		yield return Setting.Key("Encounters", "StandoffMurderous", () => Balance.StandoffMurderous, delegate(int v)
		{
			Balance.StandoffMurderous = v;
		}, () => $"{Balance.StandoffMurderous}");
		yield return Setting.Key("Encounters", "ManCooldownHours", () => Balance.ManCooldownHours, delegate(int v)
		{
			Balance.ManCooldownHours = v;
		}, () => $"{Balance.ManCooldownHours}");
		yield return Setting.Line("; Metres: an Army patrol rides (or runs) toward gunfire this close - yours or anyone's.");
		yield return Setting.Key("Encounters", "ArmyPatrolHearing", () => Balance.PatrolHearingRange, delegate(float v)
		{
			Balance.PatrolHearingRange = v;
		}, () => F(Balance.PatrolHearingRange) ?? "");
		yield return Setting.Line("; A fight in patrol country (Army north, Raiders south): % per minute of shooting that a patrol rides in from 450-600 m; then a cooldown (s).");
		yield return Setting.Key("Encounters", "PatrolRespondChance", () => Balance.PatrolRespondChance, delegate(int v)
		{
			Balance.PatrolRespondChance = v;
		}, () => $"{Balance.PatrolRespondChance}");
		yield return Setting.Line("; A responding patrol starts PatrolRespondMin..PatrolRespondMax m from the shooting (never within 120 m of you, out of view).");
		yield return Setting.Line("; Past ~250 m the game may not simulate them at all (no ground loaded): they stand still.");
		yield return Setting.Key("Encounters", "PatrolRespondMin", () => Balance.PatrolRespondMin, delegate(int v)
		{
			Balance.PatrolRespondMin = Math.Max(100, v);
		}, () => $"{Balance.PatrolRespondMin}");
		yield return Setting.Key("Encounters", "PatrolRespondMax", () => Balance.PatrolRespondMax, delegate(int v)
		{
			Balance.PatrolRespondMax = Math.Max(Balance.PatrolRespondMin, v);
		}, () => $"{Balance.PatrolRespondMax}");
		yield return Setting.Key("Encounters", "PatrolRespondCooldown", () => Balance.PatrolRespondCooldown, delegate(int v)
		{
			Balance.PatrolRespondCooldown = v;
		}, () => $"{Balance.PatrolRespondCooldown}");
		yield return Setting.Line("; Patrols ride; only around Fort Wallace PatrolMountedPercent % of them are cavalry (the rest on foot). PatrolHurryPercent = % that move at a gallop / run (else slow) until they hear shots.");
		yield return Setting.Key("Encounters", "PatrolMountedPercent", () => Balance.PatrolMountedPct, delegate(int v)
		{
			Balance.PatrolMountedPct = v;
		}, () => $"{Balance.PatrolMountedPct}");
		yield return Setting.Key("Encounters", "PatrolHurryPercent", () => Balance.PatrolHurryPct, delegate(int v)
		{
			Balance.PatrolHurryPct = v;
		}, () => $"{Balance.PatrolHurryPct}");
		yield return Setting.Line("; Testing: make every encounter this kind (e.g. Force=Riders). Leave empty for normal play.");
		yield return Setting.Key("Encounters", "Force", () => EncounterForce, delegate(string v)
		{
			EncounterForce = v;
		}, () => EncounterForce ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Camps]");
		yield return Setting.Line("; Gang officers hold up in the game's own gang camps and hideouts; Boys ride with their Lieutenant.");
		yield return Setting.Key("Camps", "Enabled", () => CampsEnabled, delegate(bool v)
		{
			CampsEnabled = v;
		}, () => $"{CampsEnabled}");
		yield return Setting.Key("Camps", "SpawnRange", () => CampSpawnRange, delegate(float v)
		{
			CampSpawnRange = v;
		}, () => F(CampSpawnRange) ?? "");
		yield return Setting.Line("; Named men at a camp besides the officer: % none / % one (the rest: two). Hired guns fill it out.");
		yield return Setting.Key("Camps", "ExtraNamedNone", () => Balance.CampExtraNamed0, delegate(int v)
		{
			Balance.CampExtraNamed0 = v;
		}, () => $"{Balance.CampExtraNamed0}");
		yield return Setting.Key("Camps", "ExtraNamedOne", () => Balance.CampExtraNamed1, delegate(int v)
		{
			Balance.CampExtraNamed1 = v;
		}, () => $"{Balance.CampExtraNamed1}");
		yield return Setting.Line("; % an O'Driscoll camp in the north has an Army patrol marching on it while you're there (they fight each other).");
		yield return Setting.Key("Camps", "RaidChance", () => Balance.CampRaidChance, delegate(int v)
		{
			Balance.CampRaidChance = v;
		}, () => $"{Balance.CampRaidChance}");
		yield return Setting.Line("; Wipe a camp out and it stays empty this many game days (nobody moves in, nobody respawns).");
		yield return Setting.Key("Camps", "ClearDays", () => CampClearDays, delegate(int v)
		{
			CampClearDays = v;
		}, () => $"{CampClearDays}");
		yield return Setting.StateKey("Camps", "Cleared", () => ClearedCamps, delegate(string v)
		{
			ClearedCamps = v;
		}, () => ClearedCamps ?? "");
		yield return Setting.Line("");
		yield return Setting.Line("[Keys]");
		yield return Setting.Line("; RosterMenu: the enRAGE menu - one key (F11) or a combination (Shift+F9, Ctrl+End). The settings are that + Alt, the undo of a");
		yield return Setting.Line("; save-point restore that + Shift (a modifier the combination already uses: the next free one). Don't use Insert (it reloads scripts).");
		yield return Setting.Line("; In the settings panel: pick a key and press Enter, then the new key (Escape cancels). Only RosterMenu takes a combination.");
		yield return Setting.Key("Keys", "RosterMenu", () => RosterMenuKey, delegate(Keys v)
		{
			RosterMenuKey = v;
			KeyCombo.Current = v;
		}, () => KeyCombo.Name(RosterMenuKey));
		yield return Setting.Key("Keys", "ForceAmbush", () => ForceAmbushKey, delegate(Keys v)
		{
			ForceAmbushKey = v;
		}, () => $"{ForceAmbushKey}");
		yield return Setting.Key("Keys", "Interrogate", () => InterrogateKey, delegate(Keys v)
		{
			InterrogateKey = v;
		}, () => $"{InterrogateKey}");
		yield return Setting.Key("Keys", "Telegraph", () => TelegraphKey, delegate(Keys v)
		{
			TelegraphKey = v;
		}, () => $"{TelegraphKey}");
		yield return Setting.Line("; PadMenu: how a controller opens the enRAGE menu - 0 double-tap D-pad Right, 1 double-tap Left, 2 double-tap Down, 3 hold Right, 4 off.");
		yield return Setting.Key("Keys", "PadMenu", () => PadMenuOpen, delegate(int v)
		{
			PadMenuOpen = Math.Max(0, Math.Min(4, v));
			KeyCombo.PadMode = PadMenuOpen;
		}, () => $"{PadMenuOpen}");
		yield return Setting.Line("; The game control for Let Him Go on the interrogation prompts. Any eInputType name. (Interrogate is always D-pad Up.)");
		yield return Setting.Key("Keys", "LetGoPromptControl", () => InterrogateReleaseControl, delegate(eInputType v)
		{
			InterrogateReleaseControl = v;
		}, () => $"{InterrogateReleaseControl}");
		yield return Setting.Line("");
		yield return Setting.Line("[Interrogation]");
		yield return Setting.Line("; While you grapple a man, which of the game's prompt panels gets Interrogate: Auto (try, keep what");
		yield return Setting.Line("; shows), Target (the held man's panel), Player (Arthur's), Own (a separate panel of its own).");
		yield return Setting.Key("Interrogation", "HeldPanel", () => HeldPanel, delegate(string v)
		{
			HeldPanel = v;
		}, () => HeldPanel ?? "");
		yield return Setting.Line("; Interrogation FEAR (0-100) gained per act, times his fear gain (coward x2, hothead x.5 ...).");
		yield return Setting.Key("Interrogation", "FearPerHit", () => Balance.TerrorHit, delegate(int v)
		{
			Balance.TerrorHit = v;
		}, () => $"{Balance.TerrorHit}");
		yield return Setting.Key("Interrogation", "FearPerStab", () => Balance.TerrorStab, delegate(int v)
		{
			Balance.TerrorStab = v;
		}, () => $"{Balance.TerrorStab}");
		yield return Setting.Key("Interrogation", "FearPerShot", () => Balance.TerrorShot, delegate(int v)
		{
			Balance.TerrorShot = v;
		}, () => $"{Balance.TerrorShot}");
		yield return Setting.Key("Interrogation", "FearPerShotNearHim", () => Balance.TerrorNearShot, delegate(int v)
		{
			Balance.TerrorNearShot = v;
		}, () => $"{Balance.TerrorNearShot}");
		yield return Setting.Key("Interrogation", "FearPerThreat", () => Balance.TerrorThreat, delegate(int v)
		{
			Balance.TerrorThreat = v;
		}, () => $"{Balance.TerrorThreat}");
		yield return Setting.Key("Interrogation", "FearPerSecondAimedAt", () => Balance.TerrorAimPerSec, delegate(int v)
		{
			Balance.TerrorAimPerSec = v;
		}, () => $"{Balance.TerrorAimPerSec}");
		yield return Setting.Key("Interrogation", "FearPerLoot", () => Balance.TerrorLoot, delegate(int v)
		{
			Balance.TerrorLoot = v;
		}, () => $"{Balance.TerrorLoot}");
		yield return Setting.Line("; He won't answer at all below a fear set by his temper (coward 25 ... loyal 55); family needs this much more.");
		yield return Setting.Key("Interrogation", "FamilyFearExtra", () => Balance.InterroFamilyFearExtra, delegate(int v)
		{
			Balance.InterroFamilyFearExtra = v;
		}, () => $"{Balance.InterroFamilyFearExtra}");
		yield return Setting.Line("; Failed tries on a question before it greys out, and how much more fear opens it again.");
		yield return Setting.Key("Interrogation", "TriesPerScare", () => Balance.InterroTriesPerScare, delegate(int v)
		{
			Balance.InterroTriesPerScare = v;
		}, () => $"{Balance.InterroTriesPerScare}");
		yield return Setting.Key("Interrogation", "ScareToRetry", () => Balance.InterroScareToRetry, delegate(int v)
		{
			Balance.InterroScareToRetry = v;
		}, () => $"{Balance.InterroScareToRetry}");
		yield return Setting.Line("; Fear from watching you kill near him: in an interrogation / on a nemesis's sheet.");
		yield return Setting.Key("Interrogation", "FearPerKillSeen", () => Balance.TerrorComradeKilled, delegate(int v)
		{
			Balance.TerrorComradeKilled = v;
		}, () => $"{Balance.TerrorComradeKilled}");
		yield return Setting.Key("Interrogation", "SheetFearPerKillSeen", () => Balance.FearPerComradeKilled, delegate(int v)
		{
			Balance.FearPerComradeKilled = v;
		}, () => $"{Balance.FearPerComradeKilled}");
		yield return Setting.Line("");
		yield return Setting.Line("[Debug]");
		yield return Setting.Line("; Every session logs 'SEED session N' in grudges.log. Put N here to replay that session's random rolls (0 = fresh each time).");
		yield return Setting.Line("; Enabled: the developer's tools - Ctrl+F11 debug overlay (and its reset panel), Ctrl+F10 spawner, clearing the X-ray log, the hidden settings. Turn off before release.");
		yield return Setting.Key("Debug", "Enabled", () => DebugEnabled, delegate(bool v)
		{
			DebugEnabled = v;
		}, () => $"{DebugEnabled}");
		yield return Setting.Key("Debug", "Seed", () => DebugSeed, delegate(int v)
		{
			DebugSeed = v;
		}, () => $"{DebugSeed}");
		yield return Setting.Line("; True = full logs: grudges.log keeps the chatty lines (TEXT, SEAT, SETTLE, NERVE, UNEASE, CASH, FEED...) and fingerprints.log is written.");
		yield return Setting.Key("Debug", "Verbose", () => Verbose, delegate(bool v)
		{
			Verbose = v;
		}, () => $"{Verbose}");
		yield return Setting.Line("");
		yield return Setting.Line("[GameSystems]");
		yield return Setting.Line("; The game's own systems the mod drives (NemesisMod.GameSystems.cs). Weather = fog / storms / rain cut sight and calm things;");
		yield return Setting.Line("; Minds = his fear / anger / bravery fed to the game's AI, chargers and cautious men, drunks stagger; Body = taunt emotes,");
		yield return Setting.Line("; faces under questioning; Music = the score's duel cues (OFF: the music natives CRASHED the game, 2026-10-08); Map = new camps flash, fog lifts.");
		yield return Setting.Key("GameSystems", "Weather", () => GsWeather, delegate(bool v)
		{
			GsWeather = v;
		}, () => $"{GsWeather}");
		yield return Setting.Key("GameSystems", "Minds", () => GsMinds, delegate(bool v)
		{
			GsMinds = v;
		}, () => $"{GsMinds}");
		yield return Setting.Key("GameSystems", "Body", () => GsBody, delegate(bool v)
		{
			GsBody = v;
		}, () => $"{GsBody}");
		yield return Setting.Key("GameSystems", "Music", () => GsMusic, delegate(bool v)
		{
			GsMusic = v;
		}, () => $"{GsMusic}");
		yield return Setting.Key("GameSystems", "Map", () => GsMap, delegate(bool v)
		{
			GsMap = v;
		}, () => $"{GsMap}");
		yield return Setting.Line("; Gestures = the game's money handover / handshake / hat tip animations (on; the old crash came from a path that's gone - turn off if a social action ever freezes).");
		yield return Setting.Key("GameSystems", "Gestures", () => GsGestures, delegate(bool v)
		{
			GsGestures = v;
		}, () => $"{GsGestures}");
		yield return Setting.Line("; HandshakeVariant = which handshake clip: 0 upper action, 1 upper action_alt1, 2 upper action_alt2 (default - the best), 3 upper intro, 4 full body, 5 full body alt.");
		yield return Setting.Key("GameSystems", "HandshakeVariant", () => HandshakeVariant, delegate(int v)
		{
			HandshakeVariant = Math.Max(0, Math.Min(5, v));
		}, () => $"{HandshakeVariant}");
		yield return Setting.Line("");
		yield return Setting.Line("[Meta]");
		yield return Setting.Line("; Settings format version - old defaults are migrated once, then your values are left alone.");
		yield return Setting.Line(() => $"Version={ConfigVersion}");
	}
}
