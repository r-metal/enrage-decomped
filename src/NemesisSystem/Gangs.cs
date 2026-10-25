using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using RDR2;

namespace NemesisSystem;

internal static class Gangs
{
	public static readonly Gang ODriscolls = new Gang
	{
		CrewExtra = 1,
		Era = 1899,
		Id = "odriscoll",
		Jobs = new(string, string, int)[5]
		{
			("rob the stagecoach on the road out of {t}", "the road out of {t}", 4),
			("hit a homestead outside {t}", "a homestead outside {t}", 3),
			("run off horses from a ranch near {t}", "a ranch near {t}", 3),
			("rob travellers on the road by {t}", "the road by {t}", 3),
			("settle a score with a man in {t}", "the edge of {t}", 2)
		},
		JobTowns = new string[4] { "Valentine", "Strawberry", "Annesburg", "Van Horn" },
		Relations = new Dictionary<string, int>
		{
			["lemoyneraiders"] = -60,
			["murfree"] = -45,
			["skinners"] = -40,
			["bronte"] = -40,
			["army"] = -70,
			["klan"] = -10,
			["wapiti"] = -20
		},
		LoreLeaderProtected = true,
		Name = "O'Driscoll Boys",
		ModelTokens = new string[2] { "duster", "odriscoll" },
		LeaderName = "Colm O'Driscoll",
		LeaderBlurb = "Leader of the O'Driscoll Boys. Dutch's oldest enemy.",
		LeaderModel = "cs_colmodriscoll",
		FirstNames = NameBank.IrishFirst,
		LastNames = NameBank.IrishLast,
		Titles = NameBank.ODriscollTitles,
		Tiers = new(string, int, int)[3]
		{
			("CAPTAINS", 4, 3),
			("LIEUTENANTS", 2, 5),
			("BOYS", 1, 10)
		},
		Accent = (r: 92, g: 150, b: 98),
		MixedFootSoldiers = true,
		HasCamps = true,
		WeightNorth = 1.6,
		WeightSouth = 0.2,
		Faiths = new string[1] { "Catholic" },
		ExtraSidearms = new(eWeapon, string, int, int)[1] { (eWeapon.RevolverCattleman, "a Cattleman", 3, 1) },
		TroopModels = new PedHash[4]
		{
			PedHash.g_m_m_uniduster_01,
			PedHash.g_m_m_uniduster_02,
			PedHash.g_m_m_uniduster_03,
			PedHash.g_m_m_uniduster_04
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 3, new PedHash[3]
			{
				PedHash.u_m_m_unidusterhenchman_01,
				PedHash.u_m_m_unidusterhenchman_02,
				PedHash.u_m_m_unidusterhenchman_03
			}),
			(3, 3, new PedHash[3]
			{
				PedHash.g_m_m_uniduster_01,
				PedHash.g_m_m_uniduster_02,
				PedHash.g_m_m_uniduster_03
			}),
			(2, 2, new PedHash[2]
			{
				PedHash.g_m_m_uniduster_04,
				PedHash.g_m_m_uniduster_05
			}),
			(1, 10, new PedHash[5]
			{
				PedHash.g_m_m_uniduster_01,
				PedHash.g_m_m_uniduster_02,
				PedHash.g_m_m_uniduster_03,
				PedHash.g_m_m_uniduster_04,
				PedHash.g_m_m_uniduster_05
			})
		}
	};

	public static readonly Gang LemoyneRaiders = new Gang
	{
		CrewExtra = 2,
		Id = "lemoyneraiders",
		EncounterRate = 0.3,
		Jobs = new(string, string, int)[4]
		{
			("burn out a farm outside {t}", "a farm outside {t}", 4),
			("ambush lawmen on the road to {t}", "the road to {t}", 4),
			("run guns through the woods by {t}", "the woods by {t}", 3),
			("rough up a carpetbagger near {t}", "the road near {t}", 3)
		},
		JobTowns = new string[2] { "Rhodes", "Saint Denis" },
		Relations = new Dictionary<string, int>
		{
			["army"] = -70,
			["bronte"] = -55,
			["klan"] = -40,
			["murfree"] = -30,
			["skinners"] = -25,
			["wapiti"] = -15
		},
		LoreLeaderProtected = true,
		Name = "Lemoyne Raiders",
		ModelTokens = new string[2] { "uniexconfeds", "exconfed" },
		LeaderName = "Lindsey Wofford",
		LeaderBlurb = "Runs the Raiders out of Fort Brennand like the war never ended.",
		LeaderModel = "u_m_m_uniexconfedsbounty_01|cs_exconfedsleader_01",
		LeaderSpot = new float[3] { 2440f, 285f, 500f },
		FirstNames = NameBank.SouthernFirst,
		LastNames = NameBank.SouthernLast,
		Titles = NameBank.RaiderTitles,
		Tiers = new(string, int, int)[3]
		{
			("CAPTAINS", 4, 3),
			("SERGEANTS", 2, 4),
			("RAIDERS", 1, 7)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 2, new PedHash[1] { PedHash.g_m_o_uniexconfeds_01 }),
			(3, 2, new PedHash[2]
			{
				PedHash.g_m_y_uniexconfeds_01,
				PedHash.g_m_o_uniexconfeds_01
			}),
			(2, 2, new PedHash[1] { PedHash.g_m_y_uniexconfeds_02 }),
			(1, 7, new PedHash[2]
			{
				PedHash.g_m_y_uniexconfeds_01,
				PedHash.g_m_y_uniexconfeds_02
			})
		},
		Accent = (r: 126, g: 142, b: 168),
		AmbushBase = 0.15,
		MixedFootSoldiers = true,
		HasCamps = true,
		WeightNorth = 0.03,
		WeightSouth = 2.5,
		WeightElsewhere = 0.03,
		CombatMovement = 2,
		CombatAbility = 1,
		CombatRange = 1,
		Faiths = new string[2] { "Baptist", "Methodist" },
		MemberWealth = Wealth.Poor,
		MemberTrades = new string[1] { "Ex-soldier" },
		OldArmyKnifeWeight = 5,
		ExtraSidearms = new(eWeapon, string, int, int)[2]
		{
			(eWeapon.RevolverNavy, "an old Navy revolver", 4, 1),
			(eWeapon.RevolverLemat, "a LeMat", 3, 3)
		},
		ExtraLongGuns = new(eWeapon, string, int)[1] { (eWeapon.RifleSpringfield, "a Springfield", 4) },
		TroopModels = new PedHash[3]
		{
			PedHash.g_m_y_uniexconfeds_01,
			PedHash.g_m_y_uniexconfeds_02,
			PedHash.g_m_o_uniexconfeds_01
		},
		PatrolsIn = "south"
	};

	public static readonly Gang Army = new Gang
	{
		Id = "army",
		EncounterRate = 0.4,
		Jobs = new(string, string, int)[3]
		{
			("sweep the road outside {t} for O'Driscolls", "the road outside {t}", 4),
			("escort a supply wagon to {t}", "the road to {t}", 4),
			("hunt deserters in the hills by {t}", "the hills by {t}", 3)
		},
		JobTowns = new string[5] { "Valentine", "Strawberry", "Annesburg", "Rhodes", "Van Horn" },
		Relations = new Dictionary<string, int>
		{
			["klan"] = -35,
			["wapiti"] = -45,
			["bronte"] = -25,
			["murfree"] = -50,
			["skinners"] = -50
		},
		TurfWars = false,
		LoreLeaderProtected = true,
		Name = "U.S. Army",
		ModelTokens = new string[1] { "army" },
		LeaderName = "Col. Henry Favours",
		LeaderBlurb = "Commands Fort Wallace and keeps the Wapiti on their reservation. Wants the outlaw problem buried.",
		LeaderModel = "cs_colfavours",
		FirstNames = NameBank.AmericanFirst,
		LastNames = NameBank.ArmyLast,
		Titles = NameBank.ArmyTitles,
		Tiers = new(string, int, int)[3]
		{
			("OFFICERS", 4, 3),
			("SERGEANTS", 2, 5),
			("PRIVATES", 1, 14)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 3, new PedHash[1] { PedHash.s_m_m_army_01 }),
			(3, 2, new PedHash[1] { PedHash.s_m_m_army_01 }),
			(2, 3, new PedHash[2]
			{
				PedHash.s_m_m_army_01,
				PedHash.s_m_y_army_01
			}),
			(1, 14, new PedHash[2]
			{
				PedHash.s_m_y_army_01,
				PedHash.s_m_m_army_01
			})
		},
		Accent = (r: 112, g: 124, b: 68),
		AmbushBase = 0.12,
		OrdersNotFeelings = true,
		UniformKit = true,
		NeedsReason = true,
		SquadsOnly = true,
		RankInName = true,
		UnnamedKillsPerPoint = 1,
		FamilyNames = false,
		KeepsToItsOwn = true,
		Feuds = false,
		GangLifeEvents = false,
		WeightNorth = 1.5,
		WeightSouth = 0.3,
		WeightElsewhere = 0.4,
		WeightNearFort = 3.0,
		CombatMovement = 1,
		CombatAbility = 2,
		CombatRange = 2,
		AccuracyMin = 40,
		MemberWealth = Wealth.Working,
		MemberTrades = new string[1] { "Soldier" },
		TroopModels = new PedHash[2]
		{
			PedHash.s_m_y_army_01,
			PedHash.s_m_m_army_01
		},
		TroopLongGuns = true,
		TroopAccuracy = 30,
		PatrolsIn = "north"
	};

	public static readonly Gang Klan = new Gang
	{
		Id = "klan",
		EncounterRate = 0.3,
		Jobs = new(string, string, int)[2]
		{
			("ride by night outside {t}", "the fields outside {t}", 4),
			("burn a cross outside {t}", "a field outside {t}", 3)
		},
		JobTowns = new string[1] { "Rhodes" },
		Relations = new Dictionary<string, int>
		{
			["bronte"] = -30,
			["murfree"] = -20,
			["skinners"] = -10,
			["wapiti"] = -40
		},
		Name = "The Klan",
		ModelTokens = new string[0],
		LeaderName = "High Warden Jebediah Pruitt",
		LeaderBlurb = "Hides his face, lights the fires, sends others to do the dying.",
		FirstNames = NameBank.SouthernFirst,
		LastNames = NameBank.SouthernLast,
		Titles = NameBank.KlanTitles,
		Tiers = new(string, int, int)[3]
		{
			("WARDENS", 4, 1),
			("TORCHBEARERS", 2, 2),
			("HOODS", 1, 5)
		},
		Seed = new(int, int, PedHash[])[3]
		{
			(4, 1, new PedHash[1] { PedHash.re_rallysetup_males_01 }),
			(2, 2, new PedHash[2]
			{
				PedHash.re_rallysetup_males_01,
				PedHash.re_rally_males_01
			}),
			(1, 5, new PedHash[2]
			{
				PedHash.re_rally_males_01,
				PedHash.re_rallysetup_males_01
			})
		},
		Accent = (r: 226, g: 222, b: 210),
		AmbushBase = 0.08,
		OnlyDistricts = new HashSet<uint> { 3430691604u, 687704893u, 1308232528u, 3824751532u },
		CombatMovement = 3,
		CombatAbility = 0,
		AccuracyMax = 18,
		MayFlee = true,
		NoTowns = true,
		DayWeight = 0.2,
		NightWeight = 1.5,
		TownModels = new string[8] { "a_m_m_rhdtownfolk_01", "a_m_m_rhdtownfolk_02", "a_m_m_rhdtownfolk_01_laborer", "a_m_m_rhdforeman_01", "a_m_m_rhdupperclass_01", "a_m_m_rhdobesemen_01", "a_m_m_valfarmer_01", "a_m_m_vhttownfolk_01" },
		Faiths = new string[2] { "Baptist", "Methodist" },
		MemberTrades = new string[2] { "Farmer", "Store clerk" },
		ExtraLongGuns = new(eWeapon, string, int)[1] { (eWeapon.ShotgunDoubleBarrel, "a double-barrel shotgun", 5) },
		TroopModels = new PedHash[2]
		{
			PedHash.re_rally_males_01,
			PedHash.re_rallysetup_males_01
		}
	};

	public static readonly Gang Wapiti = new Gang
	{
		Id = "wapiti",
		EncounterRate = 0.4,
		Era = 1899,
		Jobs = new(string, string, int)[2]
		{
			("hunt in the hills above {t}", "the hills above {t}", 3),
			("watch the soldiers' road by {t}", "the road by {t}", 3)
		},
		JobTowns = new string[2] { "Strawberry", "Valentine" },
		Relations = new Dictionary<string, int>
		{
			["murfree"] = -20,
			["skinners"] = -45
		},
		TurfWars = false,
		LoreLeaderProtected = true,
		Name = "Wapiti",
		ModelTokens = new string[1] { "wapwarriors" },
		LeaderName = "Rains Fall",
		LeaderModel = "cs_rainsfall",
		LeaderBlurb = "Chief of the Wapiti. Wants his people to live through the reservation, not die fighting the Army.",
		FirstNames = NameBank.WapitiFirst,
		LastNames = NameBank.WapitiLast,
		Titles = NameBank.WapitiTitles,
		ExcludedNames = NameBank.WapitiExcluded,
		Tiers = new(string, int, int)[3]
		{
			("WAR LEADERS", 4, 2),
			("SCOUTS", 2, 4),
			("WARRIORS", 1, 8)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 2, new PedHash[1] { PedHash.a_m_m_wapwarriors_01 }),
			(3, 2, new PedHash[1] { PedHash.a_m_m_wapwarriors_01 }),
			(2, 2, new PedHash[1] { PedHash.a_m_m_wapwarriors_01 }),
			(1, 8, new PedHash[1] { PedHash.a_m_m_wapwarriors_01 })
		},
		Accent = (r: 178, g: 112, b: 74),
		AmbushBase = 0.08,
		HasCamps = true,
		NeedsReason = true,
		ReasonFromBounty = false,
		SquadsOnly = true,
		KeepsToItsOwn = true,
		Feuds = false,
		GangLifeEvents = false,
		FamilyNames = false,
		OwnLives = true,
		TribalKit = true,
		AnswersGunfire = false,
		SeedHate = (25, 50),
		OnlyDistricts = new HashSet<uint> { 2311828277u, 4174810561u, 1835499550u },
		DistrictWeights = new Dictionary<uint, double>
		{
			{ 2311828277u, 1.5 },
			{ 4174810561u, 1.5 },
			{ 1835499550u, 0.5 }
		},
		CombatMovement = 2,
		CombatAbility = 1,
		CombatRange = 1,
		AccuracyMin = 25,
		Faiths = new string[1] { "the old ways" },
		MemberWealth = Wealth.Poor,
		MemberTrades = new string[2] { "Hunter", "Horse breaker" },
		TroopModels = new PedHash[1] { PedHash.a_m_m_wapwarriors_01 },
		TroopAccuracy = 20,
		PatrolsIn = "districts"
	};

	public static readonly Gang SaintDenisMob = new Gang
	{
		Id = "bronte",
		EncounterRate = 0.5,
		Era = 0,
		Jobs = new(string, string, int)[3]
		{
			("collect from a shopkeeper who's behind in {t}", "a back street of {t}", 3),
			("move crates off a riverboat near {t}", "the docks road by {t}", 4),
			("teach a rival bootlegger a lesson outside {t}", "the bayou road outside {t}", 3)
		},
		JobTowns = new string[1] { "Saint Denis" },
		Relations = new Dictionary<string, int>
		{
			["murfree"] = -20,
			["skinners"] = -10
		},
		LoreLeaderProtected = true,
		Name = "Bronte Family",
		ModelTokens = new string[2] { "brontegoon", "nbxbronte" },
		LeaderName = "Angelo Bronte",
		LeaderModel = "cs_bronte",
		LeaderBlurb = "Owns Saint Denis - the police, the docks, the mayor's ear. Smiles while he does it.",
		FirstNames = NameBank.ItalianFirst,
		LastNames = NameBank.ItalianLast,
		Titles = NameBank.MobTitles,
		Tiers = new(string, int, int)[3]
		{
			("CAPOS", 4, 2),
			("ENFORCERS", 2, 4),
			("SOLDIERS", 1, 8)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 2, new PedHash[2]
			{
				PedHash.u_m_m_nbxbronteasc_01,
				PedHash.u_m_m_nbxbrontesecform_01
			}),
			(3, 2, new PedHash[2]
			{
				PedHash.g_m_m_unibrontegoons_01,
				PedHash.u_m_m_nbxbrontegoon_01
			}),
			(2, 2, new PedHash[1] { PedHash.g_m_m_unibrontegoons_01 }),
			(1, 8, new PedHash[2]
			{
				PedHash.g_m_m_unibrontegoons_01,
				PedHash.u_m_m_nbxbrontegoon_01
			})
		},
		Accent = (r: 150, g: 72, b: 84),
		AmbushBase = 0.12,
		HasCamps = true,
		NeedsReason = true,
		ReasonFromBounty = false,
		HomeTown = "Saint Denis",
		OnlyDistricts = new HashSet<uint> { 2025841068u, 3374368891u },
		DistrictWeight = 2.0,
		DayWeight = 0.8,
		NightWeight = 1.4,
		CombatMovement = 1,
		CombatAbility = 1,
		CombatRange = 1,
		AccuracyMin = 25,
		Faiths = new string[1] { "Catholic" },
		MemberWealth = Wealth.Working,
		ExtraSidearms = new(eWeapon, string, int, int)[3]
		{
			(eWeapon.RevolverDoubleAction, "a Double-Action", 4, 1),
			(eWeapon.PistolVolcanic, "a Volcanic pistol", 2, 2),
			(eWeapon.PistolMauser, "a Mauser", 2, 4)
		},
		ExtraLongGuns = new(eWeapon, string, int)[2]
		{
			(eWeapon.ShotgunSawedoff, "a sawed-off", 5),
			(eWeapon.ShotgunPump, "a pump shotgun", 2)
		},
		TroopModels = new PedHash[2]
		{
			PedHash.g_m_m_unibrontegoons_01,
			PedHash.u_m_m_nbxbrontegoon_01
		},
		TroopAccuracy = 26
	};

	public static readonly Gang MurfreeBrood = new Gang
	{
		Id = "murfree",
		EncounterRate = 0.6,
		Jobs = new(string, string, int)[3]
		{
			("take travellers off the road near {t}", "the road near {t}", 3),
			("raid a homestead in the hills by {t}", "a homestead by {t}", 4),
			("set a trap on the trail out of {t}", "the trail out of {t}", 3)
		},
		JobTowns = new string[2] { "Annesburg", "Van Horn" },
		Relations = new Dictionary<string, int> { ["skinners"] = -30 },
		Name = "Murfree Brood",
		ModelTokens = new string[1] { "uniinbred" },
		LeaderName = "Old Esau Murfree",
		LeaderBlurb = "Nobody's seen the head of the Brood in years. The Brood answers to him anyway.",
		FirstNames = NameBank.BackwoodsFirst,
		LastNames = NameBank.MurfreeLast,
		Titles = NameBank.MurfreeTitles,
		Tiers = new(string, int, int)[3]
		{
			("ELDERS", 4, 2),
			("HUNTERS", 2, 4),
			("BROOD", 1, 8)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 2, new PedHash[1] { PedHash.g_m_m_uniinbred_01 }),
			(3, 2, new PedHash[1] { PedHash.g_m_m_uniinbred_01 }),
			(2, 2, new PedHash[1] { PedHash.g_m_m_uniinbred_01 }),
			(1, 8, new PedHash[1] { PedHash.g_m_m_uniinbred_01 })
		},
		Accent = (r: 124, g: 116, b: 72),
		AmbushBase = 0.1,
		MixedFootSoldiers = true,
		HasCamps = true,
		KeepsToItsOwn = true,
		NoTowns = true,
		OnlyDistricts = new HashSet<uint> { 178647645u, 1921384287u, 2101155660u },
		DistrictWeight = 2.0,
		DayWeight = 0.5,
		NightWeight = 1.8,
		CombatMovement = 2,
		CombatAbility = 1,
		CombatRange = 0,
		AccuracyMax = 30,
		Faiths = new string[1] { "none" },
		MemberWealth = Wealth.Poor,
		MemberTrades = new string[2] { "Trapper", "Hunter" },
		ExtraLongGuns = new(eWeapon, string, int)[1] { (eWeapon.ShotgunDoubleBarrel, "a double-barrel shotgun", 6) },
		TroopModels = new PedHash[1] { PedHash.g_m_m_uniinbred_01 },
		TroopAccuracy = 16
	};

	public static readonly Gang SkinnerBrothers = new Gang
	{
		Id = "skinners",
		EncounterRate = 0.6,
		Jobs = new(string, string, int)[3]
		{
			("hunt men in the woods past {t}", "the woods past {t}", 3),
			("rob a wagon on the road out of {t}", "the road out of {t}", 4),
			("leave a warning on the trail by {t}", "the trail by {t}", 2)
		},
		JobTowns = new string[2] { "Blackwater", "Strawberry" },
		Name = "Skinner Brothers",
		ModelTokens = new string[2] { "unimountainmen", "skinnerbrother" },
		LeaderName = "Elias Skinner",
		LeaderBlurb = "Eldest of the brothers. Every man who crosses Tall Trees leaves part of himself behind.",
		FirstNames = NameBank.AmericanFirst,
		LastNames = NameBank.SkinnerLast,
		Titles = NameBank.SkinnerTitles,
		Tiers = new(string, int, int)[3]
		{
			("BROTHERS", 4, 2),
			("TRAPPERS", 2, 4),
			("SKINNERS", 1, 8)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 2, new PedHash[1] { PedHash.g_m_m_unimountainmen_01 }),
			(3, 2, new PedHash[1] { PedHash.g_m_m_unimountainmen_01 }),
			(2, 2, new PedHash[1] { PedHash.g_m_m_unimountainmen_01 }),
			(1, 8, new PedHash[1] { PedHash.g_m_m_unimountainmen_01 })
		},
		Accent = (r: 146, g: 112, b: 84),
		AmbushBase = 0.1,
		MixedFootSoldiers = true,
		HasCamps = true,
		KeepsToItsOwn = true,
		NoTowns = true,
		OnlyDistricts = new HashSet<uint> { 1684533001u, 1706859953u, 210917480u, 476637847u },
		DistrictWeights = new Dictionary<uint, double>
		{
			{ 1684533001u, 2.0 },
			{ 1706859953u, 2.0 },
			{ 210917480u, 0.6 },
			{ 476637847u, 0.6 }
		},
		DayWeight = 0.8,
		NightWeight = 1.4,
		CombatMovement = 2,
		CombatAbility = 2,
		CombatRange = 2,
		AccuracyMin = 30,
		Faiths = new string[1] { "none" },
		MemberWealth = Wealth.Poor,
		MemberTrades = new string[2] { "Trapper", "Hunter" },
		ExtraLongGuns = new(eWeapon, string, int)[2]
		{
			(eWeapon.RifleBoltAction, "a bolt-action rifle", 4),
			(eWeapon.RifleVarmint, "a varmint rifle", 2)
		},
		TroopModels = new PedHash[1] { PedHash.g_m_m_unimountainmen_01 },
		TroopLongGuns = true,
		TroopAccuracy = 30
	};

	public static readonly Gang LaramieGang = new Gang
	{
		Id = "laramie",
		Era = 1907,
		EncounterRate = 0.5,
		CrewExtra = 1,
		Jobs = new(string, string, int)[4]
		{
			("run off cattle from a ranch near {t}", "a ranch near {t}", 4),
			("change the brands on stolen stock outside {t}", "a corral outside {t}", 3),
			("hold up the stage on the road out of {t}", "the road out of {t}", 4),
			("rob a homestead outside {t}", "a homestead outside {t}", 3)
		},
		JobTowns = new string[2] { "Blackwater", "Armadillo" },
		Relations = new Dictionary<string, int>
		{
			["skinners"] = -40,
			["delobo"] = -45,
			["army"] = -40,
			["lemoyneraiders"] = -20,
			["murfree"] = -20,
			["klan"] = -10
		},
		Name = "Laramie Gang",
		ModelTokens = new string[2] { "laramie", "uniranchers" },
		LeaderName = "Wade Haskell",
		LeaderBlurb = "Runs the Laramie boys - he'll take a man's herd, and his life if he argues.",
		LeaderModel = "cs_laramie",
		FirstNames = NameBank.AmericanFirst,
		LastNames = NameBank.AmericanLast,
		Titles = NameBank.LaramieTitles,
		Tiers = new(string, int, int)[3]
		{
			("FOREMEN", 4, 2),
			("WRANGLERS", 2, 4),
			("RUSTLERS", 1, 8)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 2, new PedHash[1] { PedHash.g_m_m_uniranchers_01 }),
			(3, 2, new PedHash[1] { PedHash.g_m_m_uniranchers_01 }),
			(2, 2, new PedHash[1] { PedHash.g_m_m_uniranchers_01 }),
			(1, 8, new PedHash[1] { PedHash.g_m_m_uniranchers_01 })
		},
		Accent = (r: 184, g: 140, b: 80),
		AmbushBase = 0.12,
		MixedFootSoldiers = true,
		HasCamps = true,
		OnlyDistricts = new HashSet<uint> { 892930832u, 4241435510u, 210917480u, 476637847u, 822658194u, 3485840081u },
		DistrictWeights = new Dictionary<uint, double>
		{
			{ 892930832u, 2.0 },
			{ 4241435510u, 2.0 },
			{ 210917480u, 1.6 },
			{ 476637847u, 1.6 },
			{ 822658194u, 0.5 },
			{ 3485840081u, 0.5 }
		},
		DayWeight = 0.8,
		NightWeight = 1.4,
		CombatMovement = 2,
		CombatAbility = 1,
		CombatRange = 1,
		AccuracyMin = 20,
		MemberWealth = Wealth.Poor,
		MemberTrades = new string[2] { "Ranch hand", "Drover" },
		ExtraSidearms = new(eWeapon, string, int, int)[1] { (eWeapon.RevolverCattleman, "a Cattleman", 4, 1) },
		ExtraLongGuns = new(eWeapon, string, int)[1] { (eWeapon.RepeaterCarbine, "a carbine", 3) },
		TroopModels = new PedHash[1] { PedHash.g_m_m_uniranchers_01 },
		TroopAccuracy = 24
	};

	public static readonly Gang DelLobos = new Gang
	{
		Id = "delobo",
		Era = 1907,
		EncounterRate = 0.6,
		CrewExtra = 1,
		Jobs = new(string, string, int)[4]
		{
			("rob the stagecoach on the road out of {t}", "the road out of {t}", 4),
			("raid a ranch outside {t}", "a ranch outside {t}", 4),
			("hold up a train near {t}", "the tracks near {t}", 4),
			("run guns across the river by {t}", "the river by {t}", 3)
		},
		JobTowns = new string[2] { "Tumbleweed", "Armadillo" },
		Relations = new Dictionary<string, int>
		{
			["laramie"] = -45,
			["army"] = -50,
			["skinners"] = -20
		},
		Name = "Del Lobo Gang",
		ModelTokens = new string[1] { "unibanditos" },
		LeaderName = "Hector Sandoval",
		LeaderBlurb = "Jefe of the Del Lobos. Rides both sides of the river, answers to neither.",
		FirstNames = NameBank.MexicanFirst,
		LastNames = NameBank.MexicanLast,
		Titles = NameBank.DelLoboTitles,
		Tiers = new(string, int, int)[3]
		{
			("JEFES", 4, 2),
			("PISTOLEROS", 2, 4),
			("BANDIDOS", 1, 8)
		},
		Seed = new(int, int, PedHash[])[4]
		{
			(4, 2, new PedHash[1] { PedHash.g_m_m_unibanditos_01 }),
			(3, 2, new PedHash[1] { PedHash.g_m_m_unibanditos_01 }),
			(2, 2, new PedHash[1] { PedHash.g_m_m_unibanditos_01 }),
			(1, 8, new PedHash[1] { PedHash.g_m_m_unibanditos_01 })
		},
		Accent = (r: 198, g: 92, b: 58),
		AmbushBase = 0.14,
		MixedFootSoldiers = true,
		HasCamps = true,
		OnlyDistricts = new HashSet<uint> { 4186119282u, 736160934u, 2228727054u, 3946348553u, 2148975167u, 779743808u, 892930832u, 4241435510u },
		DistrictWeights = new Dictionary<uint, double>
		{
			{ 4186119282u, 1.5 },
			{ 736160934u, 1.5 },
			{ 2228727054u, 2.0 },
			{ 3946348553u, 2.0 },
			{ 2148975167u, 2.0 },
			{ 779743808u, 2.0 },
			{ 892930832u, 0.4 },
			{ 4241435510u, 0.4 }
		},
		CombatMovement = 2,
		CombatAbility = 1,
		CombatRange = 1,
		AccuracyMin = 20,
		Faiths = new string[1] { "Catholic" },
		MemberWealth = Wealth.Poor,
		MemberTrades = new string[2] { "Vaquero", "Horse thief" },
		ExtraSidearms = new(eWeapon, string, int, int)[1] { (eWeapon.RevolverSchofield, "a Schofield", 3, 1) },
		ExtraLongGuns = new(eWeapon, string, int)[1] { (eWeapon.RepeaterCarbine, "a carbine", 4) },
		TroopModels = new PedHash[1] { PedHash.g_m_m_unibanditos_01 },
		TroopAccuracy = 24
	};

	public static readonly Gang NightFolk = new Gang
	{
		Id = "nightfolk",
		Era = 1907,
		EncounterRate = 0.4,
		Jobs = new(string, string, int)[6]
		{
			("take a traveller off the bayou road near {t}", "the bayou road near {t}", 3),
			("leave a warning hanging in the trees by {t}", "the trees by {t}", 2),
			("hang lanterns in the cypress along the road out of {t}", "the road out of {t}", 2),
			("dig up a fresh grave in the cemetery at {t}", "the cemetery at {t}", 2),
			("take a fishing boat off the water near {t}", "the water near {t}", 2),
			("leave something on a doorstep in {t}", "a doorstep in {t}", 1)
		},
		JobTowns = new string[1] { "Saint Denis" },
		Relations = new Dictionary<string, int>
		{
			["lemoyneraiders"] = -20,
			["army"] = -30,
			["bronte"] = -35
		},
		TurfWars = false,
		Name = "Night Folk",
		ModelTokens = new string[1] { "uniswamp" },
		LeaderName = "Silas Thibodeaux",
		LeaderBlurb = "Nobody has seen him in daylight. The bayou does what he says anyway.",
		FirstNames = NameBank.CajunFirst,
		LastNames = NameBank.CajunLast,
		Titles = NameBank.NightFolkTitles,
		Tiers = new(string, int, int)[3]
		{
			("ELDERS", 4, 1),
			("STALKERS", 2, 3),
			("FOLK", 1, 8)
		},
		Seed = new(int, int, PedHash[])[3]
		{
			(4, 1, new PedHash[1] { PedHash.g_m_m_uniswamp_01 }),
			(2, 3, new PedHash[1] { PedHash.g_m_m_uniswamp_01 }),
			(1, 8, new PedHash[1] { PedHash.g_m_m_uniswamp_01 })
		},
		Accent = (r: 104, g: 128, b: 108),
		AmbushBase = 0.08,
		MixedFootSoldiers = true,
		HasCamps = true,
		KeepsToItsOwn = true,
		NoTowns = true,
		Feuds = false,
		GangLifeEvents = false,
		OwnLifeOnly = true,
		OnlyDistricts = new HashSet<uint> { 2025841068u, 3374368891u },
		DistrictWeight = 1.5,
		DayWeight = 0.2,
		NightWeight = 2.0,
		CombatMovement = 2,
		CombatAbility = 0,
		CombatRange = 0,
		AccuracyMax = 20,
		Faiths = new string[1] { "the old ways" },
		MemberWealth = Wealth.Poor,
		MemberTrades = new string[2] { "Trapper", "Fisherman" },
		TroopModels = new PedHash[1] { PedHash.g_m_m_uniswamp_01 },
		TroopAccuracy = 14
	};

	private static readonly Gang[] BuiltIn = new Gang[11]
	{
		ODriscolls, LemoyneRaiders, Army, Klan, Wapiti, SaintDenisMob, MurfreeBrood, SkinnerBrothers, LaramieGang, DelLobos,
		NightFolk
	};

	private static Gang[] _defined = BuiltIn;

	private static JObject[] _builtInExport;

	private static readonly Dictionary<uint, Gang> _byModel = new Dictionary<uint, Gang>();

	private static Dictionary<string, Gang> _byId;

	public static readonly Gang VanDerLinde = new Gang
	{
		Id = "vdl",
		Name = "Van der Linde Gang",
		LeaderName = "Dutch van der Linde",
		StoryOnly = true,
		Feuds = false,
		LeaderBlurb = "Dutch's gang, on the run since Blackwater",
		ModelTokens = new string[0],
		FirstNames = new string[0],
		LastNames = new string[0],
		Titles = new string[0],
		Seed = new(int, int, PedHash[])[0],
		Tiers = new(string, int, int)[4]
		{
			("LEADERS", 5, 1),
			("SENIOR HANDS", 4, 2),
			("GUNS", 2, 9),
			("CAMP HANDS", 1, 12)
		},
		Accent = (r: 170, g: 140, b: 110)
	};

	public static Gang[] Defined => _defined;

	public static int Era { get; private set; } = 1899;

	public static Gang[] All { get; private set; } = BuiltIn.Where((Gang g) => g.Era == 0 || g.Era == 1899).ToArray();

	public static int Version { get; private set; }

	internal static JObject[] BuiltInExport => _builtInExport ?? (_builtInExport = BuiltIn.Select(FactionContent.Export).ToArray());

	internal static void SetAll(Gang[] defined)
	{
		_defined = defined;
		Rebuild();
	}

	internal static bool SetEra(int era)
	{
		era = ((era >= 1907) ? 1907 : 1899);
		if (era == Era)
		{
			return false;
		}
		Era = era;
		Rebuild();
		return true;
	}

	internal static bool InEra(Gang g)
	{
		if (g != null)
		{
			if (g.Era != 0)
			{
				return g.Era == Era;
			}
			return true;
		}
		return false;
	}

	private static void Rebuild()
	{
		All = _defined.Where(InEra).ToArray();
		lock (_byModel)
		{
			_byModel.Clear();
		}
		_byId = null;
		Version++;
	}

	public static Gang ForModel(uint model)
	{
		lock (_byModel)
		{
			if (_byModel.TryGetValue(model, out var value))
			{
				return value;
			}
			value = All.FirstOrDefault((Gang x) => x.IsMemberModel(model));
			_byModel[model] = value;
			return value;
		}
	}

	public static Gang ById(string id)
	{
		if (id == null)
		{
			return null;
		}
		Dictionary<string, Gang> dictionary = _byId;
		if (dictionary == null)
		{
			dictionary = _defined.ToDictionary((Gang g) => g.Id);
			dictionary[VanDerLinde.Id] = VanDerLinde;
			_byId = dictionary;
		}
		if (!dictionary.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public static Gang Of(NemesisRecord r)
	{
		return ById(r?.Gang);
	}

	public static bool Orders(NemesisRecord r)
	{
		return Of(r)?.OrdersNotFeelings ?? false;
	}

	public static bool Squads(NemesisRecord r)
	{
		return Of(r)?.SquadsOnly ?? false;
	}
}
