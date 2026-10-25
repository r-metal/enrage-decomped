using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class Taunts
{
	private static readonly Dictionary<string, string> ScarBoasts = new Dictionary<string, string>
	{
		{ "PD_Right_cut_cheek", "See this cheek? Every mornin' in the mirror, I think of you." },
		{ "PD_ODR6_Duster_Slash_Belly", "Stitched my own gut shut. Had a lot of time to think about you." },
		{ "PD_Right_bruise_eye_A", "This eye never did heal right. Neither did my temper." },
		{ "PD_Nose_cut", "You broke my nose. I'm gonna break the rest of you." },
		{ "PD_Animal_attack_left_face1_Enemy", "Crawled outta that den with half a face. Worth it, to see you again." },
		{ "PD_Victim_Dead_Arrow_Wounds1", "Pulled your arrows out myself. Kept one for you." },
		{ "PD_ODR3_Arthur_Wound_Shoulder_L_Cauterized", "You burned me. I don't burn twice." },
		{ "PD_Ped_Throat_slash", "Ain't nobody ever cut a throat that didn't take. 'Til you." },
		{ "PD_ODR3_Arthur_Wound_Shoulder_L", "Dug your bullet out with a bowie knife. Here, have it back." }
	};

	private static readonly Dictionary<string, string> WoundBoasts = new Dictionary<string, string>
	{
		{ "lung", "You put a bullet through my lung, and I lived!" },
		{ "liver", "Your bullet went clean through my gut. Didn't take." },
		{ "stomach", "Ate your lead and kept it down. Still hungry." },
		{ "intestines", "Spent a month holdin' my guts in. Thinkin' of you the whole time." },
		{ "kidney", "Pissed blood for a month 'cause of you." },
		{ "ribs", "Broke my ribs. I'm still breathin'. You won't be." },
		{ "sternum", "Cracked my chest open and I'm still standin' here." },
		{ "scapula", "Shot me in the back. Coward's bullet. Didn't even kill me." },
		{ "femur", "Walked here on the leg you broke." },
		{ "knee", "Knee's full of lead now. Still walked all the way here." },
		{ "tibia", "Limped every mile of the way to you. Worth it." },
		{ "femoral", "Nearly bled out from my leg. Nearly." },
		{ "humerus", "Shot my arm to pieces. Still works fine for this." },
		{ "forearm", "Shot my arm to pieces. Still works fine for this." },
		{ "hand", "Took two fingers off. Only need one for the trigger." },
		{ "carotid", "Bullet through the throat. Still got the voice to tell you you're dead." },
		{ "windpipe", "Bullet through the throat. Still got the voice to tell you you're dead." },
		{ "jaw", "Jaw's wired, but I can still say it: you're dead." },
		{ "skull", "Your bullet bounced off my skull. Aim's the problem, not me." },
		{ "pelvis", "My hip grinds every step. Every step, I think of you." },
		{ "spine", "Doc said I'd never walk again. Doc was wrong." }
	};

	private static readonly string[] AntagonizeReplies = new string[3] { "Keep talkin', friend. I buried you once already.", "That mouth's gonna get you killed. Again.", "Say that one more time." };

	private static readonly string[] GreetReplies = new string[3] { "Polite. Won't save you.", "Howdy yourself. Now draw.", "Don't you 'evenin'' me." };

	private static readonly string[] EscalateReplies = new string[3] { "You brought a gun to a fistfight? Fine.", "So that's how it is. Have it your way.", "Coward's move. Now we do this proper." };

	private static readonly string[] CampAlarms = new string[5] { "We got company! Get him!", "It's Morgan! Kill him!", "Stranger in camp! Light him up!", "You walked into the wrong camp, friend.", "Boys! Grab your irons!" };

	public const string EnragedReply = "That's it. No more talkin'.";

	private static readonly string[] ParalyzedLines = new string[5] { "Look at me. Look what you did. Go on, look.", "Can't feel my legs. Still feel the hate, though.", "Doc says I'll never walk again. Says it like it's news.", "Finish it, or get out of my sight.", "Came to admire your work?" };

	public static string Ambush(NemesisRecord rec, bool returningFromDead, Random rng, Func<Gang, NemesisRecord, string> leaderSaidBy = null)
	{
		List<string> list = new List<string>();
		if (returningFromDead)
		{
			list.Add("You left me for dead in " + rec.LastDefeatLocation + ". Shoulda checked.");
			list.Add("Gonna take more'n that to put me in the ground.");
			if (rec.Limp)
			{
				list.Add("I walk with a limp 'cause of you. Ain't gonna need to run.");
			}
			Scar scar = rec.Scars.LastOrDefault();
			string value2;
			if (scar != null && scar.Wound != null && WoundBoasts.TryGetValue(WoundKeys.Base(scar.Wound), out var value))
			{
				list.Add(value);
			}
			else if (scar != null && ScarBoasts.TryGetValue(scar.DamagePack, out value2))
			{
				list.Add(value2);
			}
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.Insulted)
		{
			list.Add("Shoulda kept your mouth shut in " + rec.KillLocation + ".");
			list.Add("Still got somethin' to say? Said plenty in " + rec.KillLocation + ".");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.Greeted)
		{
			list.Add("Tipped your hat to me in " + rec.KillLocation + ". Manners won't save you twice.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.GangMember)
		{
			Gang gang = Gangs.ById(rec.Gang);
			string text = ((leaderSaidBy != null) ? leaderSaidBy(gang, rec) : (gang?.LeaderName ?? "The boss"));
			if (text != null)
			{
				list.Add(text + " sends his regards.");
			}
			list.Add("You're one of Dutch's, ain't ya? Thought so.");
			list.Add("The " + (gang?.Name ?? "boys") + " never forget a Van der Linde.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.Wounded)
		{
			list.Add("You shot me in " + rec.KillLocation + " and didn't finish the job. I will.");
			list.Add("Still carry your bullet. Came to give it back.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.Slashed)
		{
			list.Add("You opened me up in " + rec.KillLocation + ". Stitches held. Let's see if yours do.");
			list.Add("Still got that blade? I got somethin' that reaches farther.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.HorseKilled)
		{
			list.Add("You killed my horse in " + rec.KillLocation + ". Best friend I ever had.");
			list.Add("Raised that horse from a foal. You're gonna pay for every year.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.Kidnapped)
		{
			list.Add("Hogtied me like a calf in " + rec.KillLocation + ". Rope burns ain't healed.");
			list.Add("Took me hours to chew through that rope. Plenty of time to think about you.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.Robbed)
		{
			list.Add("Robbed me blind in " + rec.KillLocation + ". Came to collect, with interest.");
			list.Add("You took everything I had. Now I take everything you got.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.KnockedOut)
		{
			list.Add("Woke up face-down in the mud in " + rec.KillLocation + ". Your turn to sleep.");
			list.Add("Last time you put me to sleep. This time I'm returnin' the favor. Permanent.");
		}
		else if (rec.TimesAmbushed == 0 && rec.Origin == GrudgeOrigin.Beaten)
		{
			list.Add("You beat me like a dog in " + rec.KillLocation + ". I been practicin'.");
			list.Add("Still got them fists? I brought somethin' better.");
		}
		else if (rec.TimesAntagonized > 0)
		{
			list.Add("All that big talk in " + rec.KillLocation + "... let's finish it.");
			list.Add("You still got that smart mouth? Let's hear it.");
		}
		else if (rec.TimesKilledPlayer > 1)
		{
			list.Add($"I've put you in the dirt {rec.TimesKilledPlayer} times, boy.");
		}
		else if (rec.TimesKilledPlayer == 0)
		{
			list.Add("We ain't done, you and me.");
			list.Add("Thought I'd forget " + rec.KillLocation + "? I don't forget nothin'.");
		}
		else
		{
			list.Add("Remember " + rec.KillLocation + "? I don't miss twice.");
		}
		if (!returningFromDead && rec.HateTier == HateTier.Resentful)
		{
			list.Add("Ain't gonna waste a bullet on you. These'll do.");
		}
		else if (!returningFromDead && rec.HateTier == HateTier.Vengeful)
		{
			list.Add("Brought somethin' sharp. Thought of you while I honed it.");
		}
		if (rec.Rank >= 5)
		{
			list.Add("They tell stories about me from here to Blackwater. All of 'em end with you dead.");
		}
		return list[rng.Next(list.Count)];
	}

	public static string EscalateReply(Random rng)
	{
		return EscalateReplies[rng.Next(EscalateReplies.Length)];
	}

	public static string AntagonizeReply(Random rng)
	{
		return AntagonizeReplies[rng.Next(AntagonizeReplies.Length)];
	}

	public static string CampAlarm(NemesisRecord boss, Random rng)
	{
		if (boss.TimesAmbushed <= 0 && boss.TimesKilledPlayer <= 0)
		{
			return CampAlarms[rng.Next(CampAlarms.Length)];
		}
		return "You again? Here? You got a death wish!";
	}

	public static string GreetReply(Random rng)
	{
		return GreetReplies[rng.Next(GreetReplies.Length)];
	}

	public static string Paralyzed(NemesisRecord rec, Random rng)
	{
		return ParalyzedLines[rng.Next(ParalyzedLines.Length)];
	}
}
