using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NemesisSystem;

public class NemesisRecord
{
	[JsonIgnore]
	public static Func<long> GameClock;

	public const int HistoryMax = 60;

	private int _hate = -1;

	[JsonIgnore]
	public string StoryGone;

	[JsonIgnore]
	public static bool ShowSecrets;

	public string Id { get; set; }

	public string Name { get; set; }

	public string Title { get; set; }

	[JsonIgnore]
	public string FullName
	{
		get
		{
			if (!string.IsNullOrEmpty(Title))
			{
				return Name + " " + Title;
			}
			return Name;
		}
	}

	public int Rank { get; set; } = 1;

	public int RankXp { get; set; }

	public uint ModelHash { get; set; }

	public uint OutfitHash { get; set; }

	[JsonIgnore]
	public bool Story { get; set; }

	[JsonIgnore]
	public bool LeaderPage { get; set; }

	public bool CardLife { get; set; }

	public bool SeatFell { get; set; }

	[JsonIgnore]
	public bool Person { get; set; }

	[JsonIgnore]
	internal Acquaintance PersonAcq { get; set; }

	public bool CampUnsure { get; set; }

	public int LiedToYou { get; set; }

	public bool Informant { get; set; }

	public int InformedDay { get; set; }

	public bool OwesYou { get; set; }

	public List<string> Quirks { get; set; } = new List<string>();

	public bool QuirksRolled { get; set; }

	public List<string> Skills { get; set; } = new List<string>();

	public bool SkillsV2 { get; set; }

	public int SkillTierRolled { get; set; }

	public string LastOutcome { get; set; }

	public int ArcStep { get; set; }

	public long SocialDoneAt { get; set; }

	public long BargainAt { get; set; }

	public int IntelGiven { get; set; }

	public List<string> Asked { get; set; } = new List<string>();

	public string KillLocation { get; set; }

	public DateTime KilledPlayerAt { get; set; }

	public long RestUntil { get; set; }

	public long LastCameAt { get; set; }

	public DateTime? LastInteraction { get; set; }

	public long LastGameMin { get; set; }

	public List<HistoryEntry> History { get; set; } = new List<HistoryEntry>();

	public List<Thought> Thoughts { get; set; } = new List<Thought>();

	[JsonIgnore]
	public DateTime LastDealt => LastInteraction ?? KilledPlayerAt;

	public bool IsAlive { get; set; } = true;

	public bool KilledByPlayer { get; set; }

	public int TimesKilledPlayer { get; set; }

	public int TimesAntagonized { get; set; }

	public int TimesAmbushed { get; set; }

	public int TimesKnockedOutByPlayer { get; set; }

	public bool Limp { get; set; }

	public bool Paralyzed { get; set; }

	public string Gang { get; set; }

	public string PlainModel { get; set; }

	public bool CoverBlown { get; set; }

	public string LoreModel { get; set; }

	public int PlainGreets { get; set; }

	public string Affiliation { get; set; }

	public bool SeedMember { get; set; }

	public string FormerGang { get; set; }

	public string CampId { get; set; }

	public string BossId { get; set; }

	public bool GaveCamp { get; set; }

	public bool GaveKin { get; set; }

	public bool CampKnown { get; set; }

	public int TimesInterrogated { get; set; }

	public bool HasOrigin { get; set; }

	public float OriginX { get; set; }

	public float OriginY { get; set; }

	public float OriginZ { get; set; }

	[JsonIgnore]
	public bool CanHunt
	{
		get
		{
			if (IsAlive)
			{
				return !Paralyzed;
			}
			return false;
		}
	}

	[JsonIgnore]
	public int Hate
	{
		get
		{
			if (_hate < 0 || Gang == null)
			{
				return _hate;
			}
			Gang gang = Gangs.ById(Gang);
			if (gang == null || !gang.OrdersNotFeelings)
			{
				return _hate;
			}
			return Math.Max(gang.FixedHate, NemesisSystem.Hate.MurderousAt);
		}
		set
		{
			_hate = value;
		}
	}

	[JsonProperty("Hate")]
	private int RawHate
	{
		get
		{
			return _hate;
		}
		set
		{
			_hate = value;
		}
	}

	[JsonIgnore]
	public int FeltHate => _hate;

	[JsonConverter(typeof(StringEnumConverter))]
	public Personality? Personality { get; set; }

	public bool PersonalityKnown { get; set; }

	[JsonIgnore]
	public bool TemperKnown
	{
		get
		{
			if (!PersonalityKnown && TimesAmbushed <= 0)
			{
				return TimesInterrogated > 0;
			}
			return true;
		}
	}

	public bool TraitsRolled { get; set; }

	public int Aggression { get; set; }

	[JsonConverter(typeof(StringEnumConverter))]
	public Sociability Sociability { get; set; }

	[JsonConverter(typeof(StringEnumConverter))]
	public Disposition Disposition { get; set; }

	[JsonConverter(typeof(StringEnumConverter))]
	public Smarts Smarts { get; set; }

	[JsonConverter(typeof(StringEnumConverter))]
	public Wealth Wealth { get; set; }

	[JsonConverter(typeof(StringEnumConverter))]
	public Devotion Devotion { get; set; }

	public string Trade { get; set; }

	public string Faith { get; set; }

	public string Family { get; set; }

	public int Children { get; set; }

	public string KinId { get; set; }

	public string KinRelation { get; set; }

	public List<string> FriendIds { get; set; } = new List<string>();

	public string FormerAcqId { get; set; }

	public List<string> CircleAcq { get; set; }

	public bool FriendsRolled { get; set; }

	public string Pet { get; set; }

	public List<PastEntry> Past { get; set; } = new List<PastEntry>();

	public int BornYear { get; set; }

	public string Birthplace { get; set; }

	public bool PastRolled { get; set; }

	public bool Woven { get; set; }

	public Dictionary<string, int> Facets { get; set; }

	public Dictionary<string, int> Values { get; set; }

	public Dictionary<string, int> Needs { get; set; }

	public int Stress { get; set; }

	public List<Memory> Memories { get; set; } = new List<Memory>();

	public List<Tie> Ties { get; set; } = new List<Tie>();

	public List<Goal> Goals { get; set; } = new List<Goal>();

	public bool MindRolled { get; set; }

	public int LifeRules { get; set; }

	public List<string> LifeFx { get; set; }

	public bool WantsKnown { get; set; }

	public bool GoalsSet { get; set; }

	public int Fear { get; set; }

	public int YourNotoriety { get; set; }

	public int TimesRobbedPlayer { get; set; }

	public int TimesSpared { get; set; }

	public int TimesHogtied { get; set; }

	[JsonIgnore]
	public float HateGainMult => Math.Max(Balance.GainClampMin, Math.Min(Balance.GainClampMax, (Personality.HasValue ? Personalities.GainMult(Personality.Value) : 1f) * Traits.HateGain(this) * Mind.HateGain(this)));

	[JsonIgnore]
	public float FearGainMult => Math.Max(Balance.GainClampMin, Math.Min(Balance.GainClampMax, Traits.FearGain(this) * Mind.FearGain(this)));

	public bool PresumedDead { get; set; }

	public int TimesCheatedDeath { get; set; }

	public string LastDefeatLocation { get; set; }

	public List<Scar> Scars { get; set; } = new List<Scar>();

	[JsonConverter(typeof(StringEnumConverter))]
	public GrudgeOrigin Origin { get; set; }

	public int TimesGreeted { get; set; }

	public int TimesRobbed { get; set; }

	public int Fury { get; set; }

	public int LastHandle { get; set; }

	public uint LastHandleModel { get; set; }

	public DateTime? LastHandleUtc { get; set; }

	public List<Habit> Habits { get; set; }

	public bool Buried { get; set; }

	public string Fate { get; set; }

	public string Status
	{
		get
		{
			if (IsAlive)
			{
				if (!PresumedDead)
				{
					if (!Paralyzed)
					{
						return "Alive";
					}
					return "Paralyzed";
				}
				if (!ShowSecrets)
				{
					return "Dead";
				}
				return "Presumed dead";
			}
			return StoryGone ?? Fate ?? "Dead";
		}
	}

	public long PresumedDeadAt { get; set; }

	[JsonIgnore]
	public HateTier HateTier => NemesisSystem.Hate.TierOf(Hate);

	[JsonIgnore]
	public bool ScarredByYou
	{
		get
		{
			if (Scars != null)
			{
				return Scars.Any((Scar s) => s.FromYou == true || (!s.FromYou.HasValue && s.Wound != null));
			}
			return false;
		}
	}

	[JsonIgnore]
	public string GrudgeSummary => Origin switch
	{
		GrudgeOrigin.Wounded => "you shot him in " + KillLocation + " and left him breathing", 
		GrudgeOrigin.GangMember => "rides with the " + (Gangs.ById(Gang)?.Name ?? "gang") + "; knows your name", 
		GrudgeOrigin.KnockedOut => "you knocked him out in " + KillLocation, 
		GrudgeOrigin.Robbed => "you robbed him in " + KillLocation, 
		GrudgeOrigin.HorseStolen => "you stole his horse in " + KillLocation, 
		GrudgeOrigin.WagonStolen => "you stole his wagon in " + KillLocation, 
		GrudgeOrigin.Kidnapped => "you hogtied him and hauled him off in " + KillLocation, 
		GrudgeOrigin.HorseKilled => "you killed his horse in " + KillLocation, 
		GrudgeOrigin.Slashed => "you cut him in " + KillLocation + " and left him bleeding", 
		GrudgeOrigin.Beaten => "you beat him bloody in " + KillLocation, 
		GrudgeOrigin.Insulted => "you mouthed off to him in " + KillLocation, 
		GrudgeOrigin.Threatened => "you stuck him up in " + KillLocation, 
		GrudgeOrigin.Gunfight => "he lived through a gunfight with you in " + KillLocation, 
		GrudgeOrigin.FriendKilled => "you killed his friend in front of him in " + KillLocation, 
		GrudgeOrigin.Greeted => "you greeted him in " + KillLocation + "; he shot you", 
		_ => "he killed you in " + KillLocation, 
	};

	public void Touch()
	{
		LastInteraction = DateTime.Now;
		if (GameClock != null)
		{
			LastGameMin = GameClock();
		}
	}

	public void AddFear(int delta)
	{
		if (delta > 0)
		{
			delta = Math.Max(1, (int)Math.Round((float)delta * FearGainMult * Balance.FearGainScale));
		}
		Fear = Math.Max(0, Math.Min(100, Fear + delta));
	}

	public void AddYourNotoriety(int delta)
	{
		YourNotoriety = Math.Max(0, Math.Min(100, YourNotoriety + (int)Math.Round((float)delta * Balance.NotorietyGainScale)));
	}

	public void EnsureId()
	{
		if (string.IsNullOrEmpty(Id))
		{
			Id = Guid.NewGuid().ToString("N").Substring(0, 12);
		}
	}

	public void AddHate(int delta)
	{
		if (delta > 0)
		{
			delta = Math.Max(1, (int)Math.Round((float)delta * HateGainMult * Balance.HateGainScale));
			int num = Math.Max(0, _hate);
			if (num >= 80)
			{
				delta = Math.Max(1, (int)Math.Round((float)delta * (1f - (float)num / 120f)));
			}
		}
		_hate = Math.Max(0, Math.Min(100, Math.Max(0, _hate) + delta));
	}

	public void EnsureHate()
	{
		if (Hate < 0)
		{
			Hate = NemesisSystem.Hate.Initial(Origin);
			if (TimesKilledPlayer > 0)
			{
				Hate = Math.Max(Hate, NemesisSystem.Hate.MurderousAt);
			}
			AddHate(10 * Math.Max(0, TimesKilledPlayer - 1) + 25 * TimesCheatedDeath);
		}
	}
}
