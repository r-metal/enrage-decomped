using System.Collections.Generic;
using System.Linq;
using RDR2;

namespace NemesisSystem;

internal sealed class Gang
{
	public string Id;

	public string Name;

	public string[] ModelTokens;

	public string LeaderName;

	public string LeaderBlurb;

	public string LeaderModel;

	public float[] LeaderSpot;

	public string LoreLeader;

	public string LoreBlurb;

	public string[] FirstNames;

	public string[] LastNames;

	public string[] Titles;

	public (int Rank, int Count, PedHash[] Models)[] Seed;

	public (string Label, int MinRank, int Seats)[] Tiers;

	public (int r, int g, int b) Accent = (r: 206, g: 200, b: 188);

	public bool Placeholder;

	public double AmbushBase = 1.0;

	public double EncounterRate = 1.0;

	public bool OrdersNotFeelings;

	public int FixedHate = 80;

	public bool UniformKit;

	public eWeapon UniformSide = eWeapon.RevolverSchofield;

	public eWeapon UniformLong = eWeapon.RepeaterCarbine;

	public string UniformSideName = "a Schofield";

	public string UniformLongName = "a carbine";

	public (eWeapon w, string name, int weight, int minRank)[] ExtraSidearms = new(eWeapon, string, int, int)[0];

	public (eWeapon w, string name, int weight)[] ExtraLongGuns = new(eWeapon, string, int)[0];

	public int OldArmyKnifeWeight = 1;

	public bool NeedsReason;

	public bool SquadsOnly;

	public bool RankInName;

	public bool KeepsToItsOwn;

	public bool Feuds = true;

	public bool GangLifeEvents = true;

	public bool OwnLifeOnly;

	public bool MixedFootSoldiers;

	public double WeightNorth = 1.0;

	public double WeightSouth = 1.0;

	public double WeightElsewhere = 1.0;

	public double WeightNearFort = -1.0;

	public HashSet<uint> OnlyDistricts;

	public bool NoTowns;

	public double DayWeight = 1.0;

	public double NightWeight = 1.0;

	public string[] TownModels;

	public int TownChancePercent = 30;

	public double DistrictWeight = 1.5;

	public int? CombatMovement;

	public int? CombatAbility;

	public int? CombatRange;

	public int AccuracyMin;

	public int AccuracyMax = 100;

	public bool MayFlee;

	public string[] Faiths;

	public Wealth? MemberWealth;

	public string[] MemberTrades;

	public PedHash[] TroopModels = new PedHash[0];

	public bool TroopLongGuns;

	public int TroopAccuracy = 22;

	public string PatrolsIn;

	public bool HasCamps;

	public bool TribalKit;

	public eWeapon KitBow = eWeapon.Bow;

	public eWeapon KitBlade = eWeapon.ThrownTomahawkMeleeonly;

	public string KitBowName = "a bow";

	public string KitBladeName = "a tomahawk";

	public int CrewExtra;

	public bool StoryOnly;

	public bool ReasonFromBounty = true;

	public bool FamilyNames = true;

	public int? UnnamedKillsPerPoint;

	public (int min, int max)? SeedHate;

	public string[] ExcludedNames;

	public Dictionary<uint, double> DistrictWeights;

	public bool OwnLives;

	public bool AnswersGunfire = true;

	public string HomeTown;

	public (string title, string place, int crew)[] Jobs;

	public string[] JobTowns;

	public Dictionary<string, int> Relations = new Dictionary<string, int>();

	public bool TurfWars = true;

	public bool LoreLeaderProtected;

	public int Era;

	public bool IsMemberModel(uint model)
	{
		string text = Models.Name(model);
		if (text != "")
		{
			return ModelTokens.Any(text.Contains);
		}
		return false;
	}
}
