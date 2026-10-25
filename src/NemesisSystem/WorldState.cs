using System.Collections.Generic;

namespace NemesisSystem;

internal class WorldState
{
	public long NextEventAt { get; set; }

	public List<WorldEvent> Events { get; set; } = new List<WorldEvent>();

	public bool HistorySeeded { get; set; }

	public int Era { get; set; }

	public bool JohnRecordFixed { get; set; }

	public long EraTurnAt { get; set; }

	public bool EraFuneralsFixed { get; set; }

	public bool WoffordFixed { get; set; }

	public bool BronteBack { get; set; }

	public bool WomenTradesFixed { get; set; }

	public bool BronteFull { get; set; }

	public int HistoryYear { get; set; }

	public List<WorldEvent> Past { get; set; } = new List<WorldEvent>();

	public Dictionary<string, LeaderSeat> Leaders { get; set; }

	public int SaloonFights { get; set; }

	public string GpsCamp { get; set; }

	public Dictionary<string, int> ShakeUpHour { get; set; }

	public Dictionary<string, int> NextShakeUp { get; set; }

	public Dictionary<string, int> UnnamedKills { get; set; }

	public Dictionary<string, long> CompanionHurt { get; set; }

	public Dictionary<string, int> StoryRegard { get; set; }

	public Dictionary<string, int> StoryRegardEarned { get; set; }

	public Dictionary<string, int> NotorietyGainDay { get; set; }

	public List<Plan> Plans { get; set; } = new List<Plan>();

	public List<DeathNews> DeathNews { get; set; } = new List<DeathNews>();

	public Dictionary<string, Dictionary<string, double>> ControlBase { get; set; }

	public Dictionary<string, Dictionary<string, int>> Control { get; set; }

	public bool FortCleared { get; set; }

	public bool FortKnown { get; set; }

	public long FortClearedAt { get; set; }

	public Dictionary<string, string> CampOwner { get; set; }

	public Dictionary<string, int> Grievance { get; set; }

	public Dictionary<string, int> Treasury { get; set; }

	public Dictionary<string, int> Men { get; set; }

	public Dictionary<string, int> BrokeDays { get; set; }

	public bool MenRescaled2 { get; set; }

	public bool MenRescaled { get; set; }

	public List<string> BrokeSaid { get; set; }

	public Dictionary<string, Agenda> Agendas { get; set; }

	public int WarRollDay { get; set; }

	public long LastWarStartAt { get; set; }

	public List<string> Bars { get; set; }

	public Dictionary<string, CampTakeover> CampTakeovers { get; set; }

	public bool HomeStrongholdsRestored { get; set; }

	public Dictionary<string, int> EmptyTakeovers { get; set; }

	public bool TakeoverSpreeFixed { get; set; }

	public long WildFreshAt { get; set; }

	public bool SelfDefenceFixed { get; set; }

	public bool PearseFixed { get; set; }

	public bool FuneralsSeeded { get; set; }

	public bool OutfitFolkCleaned { get; set; }

	public bool OutfitModelsFixed { get; set; }

	public bool OutfitModelsFixed2 { get; set; }

	public bool RallyKlanFixed { get; set; }

	public List<string> TreasuryStash { get; set; }

	public bool DefectorWeeksFixed { get; set; }

	public long ClockShift { get; set; }

	public bool ShiftRepaired { get; set; }

	public bool MixedTimelineCleaned { get; set; }

	public Dictionary<string, long> Destroyed { get; set; }

	public List<string> Truces { get; set; }

	public List<NemesisRecord> BenchedLeaders { get; set; }

	public List<string> Alliances { get; set; }

	public HashSet<string> CompanionNever { get; set; }

	public List<string> WarZones { get; set; }

	public Dictionary<string, int> Relations { get; set; }

	public Dictionary<string, string> RelationWhy { get; set; }

	public List<WarRecord> Wars { get; set; }

	public List<WarRecord> PastWars { get; set; }

	public List<string> Bodies { get; set; }

	public bool MillemontFix { get; set; }

	public long SleepRollWindow { get; set; } = -1L;

	public Dictionary<string, long> Beaten { get; set; }

	public Dictionary<string, long> WarEndedAt { get; set; }

	public List<DeathNews> PeopleNews { get; set; } = new List<DeathNews>();

	public float Tension { get; set; } = -1f;

	public float TensionFireAt { get; set; }

	public Dictionary<string, MercyNote> Mercy { get; set; }

	public Dictionary<string, TownTalk> TownWord { get; set; } = new Dictionary<string, TownTalk>();

	public int LastFearFadeDay { get; set; }

	public long LastClockMin { get; set; }

	public int StoryChapter { get; set; }

	public long StoryChapterAt { get; set; }

	public List<Telegram> Telegrams { get; set; } = new List<Telegram>();

	public Dictionary<string, int> Supplies { get; set; }

	public Dictionary<string, string> SupplyCamps { get; set; }

	public Dictionary<string, long> SupplyCampHitAt { get; set; }

	public List<string> KnownSites { get; set; }

	public Dictionary<string, long> SupplyCaches { get; set; }

	public Dictionary<string, string> SupplyKnownAt { get; set; }

	public List<string> KeeperGone { get; set; }

	public Dictionary<string, string> SupplyCampHitSite { get; set; }

	public Dictionary<string, string> Treasuries { get; set; }

	public List<string> TreasuryKnown { get; set; }

	public Dictionary<string, long> TreasuryRobbedAt { get; set; }

	public Dictionary<string, string> TreasuryRobbedSite { get; set; }

	public Dictionary<string, PenPal> PenPals { get; set; }

	public List<string> RetiredNames { get; set; } = new List<string>();

	public Dictionary<string, float> MaskedWithheld { get; set; }

	public Dictionary<string, float> MaskedNotoriety { get; set; }

	public Dictionary<string, int> YourCrimes { get; set; } = new Dictionary<string, int>();

	public List<NemesisRecord> Recruits { get; set; }
}
