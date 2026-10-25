namespace NemesisSystem;

public static class Balance
{
	public static float HateGainScale = 1f;

	public static float LonerShiftScale = 1f;

	public static float CoolingScale = 1f;

	public static float FearGainScale = 1f;

	public static int FearDecayPer10Min = 1;

	public static int FearPerScar = 20;

	public static int FearPerCripple = 25;

	public static int FearPerKnockout = 10;

	public static int KoHate = 12;

	public static int FistCap = 48;

	public static int FistCapHot = 50;

	public static int FistCapViolent = 62;

	public static int FistCapRage = 78;

	public static int FearPerFlee = 25;

	public static int FledHateCut = 15;

	public static int FearPerInterrogation = 10;

	public static int FearPerRobbery = 5;

	public static int NotorietyFearDivisor = 4;

	public static int WontHuntFear = 60;

	public static int ProvokedOnlyFear = 75;

	public static int VengefulHuntAggression = 75;

	public static int RobbedHateGain = 10;

	public static int RobbedGreedyExtra = 10;

	public static int RobbedPoorShift = 10;

	public static int RobbedRichShift = 10;

	public static float WildResentful = 1f;

	public static float WildVengeful = 0.8f;

	public static float WildMurderous = 0.45f;

	public static float TownResentful = 1f;

	public static float TownVengefulW = 0.6f;

	public static float TownMurderousW = 0.25f;

	public static int StandoffResentful = 14;

	public static int StandoffVengeful = 10;

	public static int StandoffMurderous = 6;

	public static int MurderousCooldownHours = 6;

	public static int ManCooldownHours = 12;

	public static int TownTopUpSeconds = 60;

	public static int TownTopUpChance = 65;

	public static int TownEncounterChance = 60;

	public static float TownCoreScale = 1f;

	public static int TownArrivalMin = 2;

	public static int TownArrivalMax = 4;

	public static int TownPassingPercent = 35;

	public static float TownPassingRange = 2500f;

	public static int TownArrivalWalkMin = 30;

	public static int TownArrivalWalkMax = 60;

	public static int TownArrivalIdleSeconds = 75;

	public static float TownDotNear = 35f;

	public static float TownDotFar = 55f;

	public static int TownArrivalGapMin = 8;

	public static int TownArrivalGapMax = 18;

	public static int TownOffLorePercent = 25;

	public static int TownSeatPercent = 55;

	public static int TownStrollPercent = 30;

	public static float MaskedUneaseMult = 3f;

	public static float MaskedUneaseRange = 15f;

	public static float GroupCalm = 0.4f;

	public static int MaskedRunBonus = 35;

	public static int UneaseCrowd = 4;

	public static float MaskedFloor = 0.5f;

	public static float MeleeUneaseMult = 0.5f;

	public static float MaskOnlyFear = 18f;

	public static float MaskOffDrain = 30f;

	public static int MaskedWalkOffCap = 40;

	public static int SaloonFightChance = 12;

	public static int TownMaxMen = 4;

	public static int NewNemesisRestHours = 24;

	public static int RespawnQuietSeconds = 180;

	public static int TownVengefulPct = 35;

	public static int TownMurderousPct = 15;

	public static int TownSecondMurderousPct = 15;

	public static int TownMurderousMinHate = 85;

	public static int TownSecondAggressorPct = 10;

	public static int TownQuietAfterFight = 600;

	public static int TownWaitOutsideBase = 45;

	public static int RoadAmbushMinutes = 15;

	public static float TownSpreadMeters = 25f;

	public static float GainClampMin = 0.5f;

	public static float GainClampMax = 1.8f;

	public static int TownEncounterSeconds = 45;

	public static int WontHuntSpotRange = 25;

	public static float NotorietyGainScale = 1f;

	public static int NotorietyDecayPerDay = 2;

	public static int UnnamedKillsPerPoint = 3;

	public static int CampExtraNamed0 = 55;

	public static int CampExtraNamed1 = 33;

	public static int CampRaidChance = 30;

	public static int WarCampRaidOutside = 18;

	public static int WarCampRaidChance = 65;

	public static int PatrolChance = 12;

	public static float PatrolHearingRange = 600f;

	public static int PatrolMountedPct = 60;

	public static int PatrolHurryPct = 50;

	public static int PatrolRespondMin = 150;

	public static int PatrolRespondMax = 200;

	public static int PatrolRespondChance = 10;

	public static int PatrolRespondCooldown = 900;

	public static int ArmyNotorietyGate = 25;

	public static int ArmyBountyGate = 150;

	public static int RaiderShakeupChance = 40;

	public static int GsThreshold = 60;

	public static int GsFloor = 25;

	public static int GsSettleSeconds = 20;

	public static int GreetStreakMinutes = 5;

	public static float GreetStreakBonus = 3f;

	public static float GsGreet = -6f;

	public static float GsDefuse = -12f;

	public static float GsInsult = 15f;

	public static float GsDrawOn = 6f;

	public static float GsIntimidate = 10f;

	public static float GsStickUp = 20f;

	public static float GsPunch = 8f;

	public static float GsKnockOut = 40f;

	public static float GsHogtie = 35f;

	public static float GsRobbed = 45f;

	public static float GsHorseStolen = 40f;

	public static float GsSlashed = 55f;

	public static float GsWounded = 50f;

	public static float GsFoughtBack = 15f;

	public static float GsShotAt = 10f;

	public static float GsComradeKilled = 14f;

	public static float GsShotAgain = 15f;

	public static float GsUnhitCap = 55f;

	public static float GsWoundCap = 58f;

	public static float GsReopen = 15f;

	public static float GsKindMax = 8f;

	public static float GsKindWaitMin = 5f;

	public static int SkirmishChance = 15;

	public static int InterroBaseBoy = 12;

	public static int InterroBaseCaptain = 8;

	public static int InterroBaseLieutenant = 3;

	public static int InterroBaseAmbient = 15;

	public static int TerrorComradeKilled = 14;

	public static int FearPerComradeKilled = 6;

	public static int GoreFear = 45;

	public static int TerrorLoot = 12;

	public static int InterroFamilyFearExtra = 15;

	public static int InterroTriesPerScare = 1;

	public static int InterroScareToRetry = 10;

	public static int TerrorHit = 6;

	public static int TerrorStab = 15;

	public static int TerrorShot = 28;

	public static int TerrorNearShot = 10;

	public static int TerrorThreat = 5;

	public static int TerrorAimPerSec = 2;
}
