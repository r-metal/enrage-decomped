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
	internal List<EncounterCard> Cards = new List<EncounterCard>();

	private readonly Dictionary<string, long> _cardUsedAt = new Dictionary<string, long>();

	internal string LastCardNote = "";

	private static readonly string[] NemesisStages = new string[6] { "idle", "callout", "riders", "ahead", "sniper", "wagon" };

	private static readonly string[] KnownStages = new string[13]
	{
		"idle", "callout", "riders", "ahead", "sniper", "wagon", "skirmish", "patrol", "ridge", "hunters",
		"warband", "ambush", "aftermath"
	};

	internal int _worldCardsPlayed;

	internal List<(string id, int weight, string why)> CardReport = new List<(string, int, string)>();

	internal string CardReportFor = "";

	internal static List<EncounterCard> BuiltInCards()
	{
		return new List<EncounterCard>
		{
			new EncounterCard
			{
				Id = "idle",
				Title = "Out in the world",
				Stage = "idle",
				IniWeight = "Ambush",
				Weight = 35,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "He and his men are found doing something (a fire, a smoke, a bottle, keeping watch, on the move). They see you, he squares up."
			},
			new EncounterCard
			{
				Id = "riders",
				Title = "Riders from behind",
				Stage = "riders",
				IniWeight = "Riders",
				Weight = 25,
				Standoff = false,
				TensionMin = 50,
				When = new CardWhen
				{
					Place = "wild",
					Mounted = true
				},
				Notes = "You're riding: they come up behind you on horses and run you down."
			},
			new EncounterCard
			{
				Id = "ahead",
				Title = "Waiting on the road",
				Stage = "ahead",
				IniWeight = "Ahead",
				Weight = 15,
				Standoff = false,
				When = new CardWhen
				{
					Place = "wild",
					Mounted = true
				},
				Notes = "You're riding: they wait ahead of you on the road and open up within 45 m."
			},
			new EncounterCard
			{
				Id = "sniper",
				Title = "Sniper on the high ground",
				Stage = "sniper",
				IniWeight = "Sniper",
				Weight = 10,
				Standoff = false,
				TensionMin = 62,
				When = new CardWhen
				{
					Place = "wild",
					Tiers = new List<string> { "Murderous" }
				},
				Notes = "A murderous man takes the high ground with a rifle; only marked once he fires."
			},
			new EncounterCard
			{
				Id = "callout",
				Title = "Call-out in town",
				Stage = "callout",
				IniWeight = "Callout",
				Weight = 100,
				When = new CardWhen
				{
					Place = "town"
				},
				Notes = "In town: idling somewhere until he spots you, then he walks over and squares up."
			},
			new EncounterCard
			{
				Id = "army-cavalry",
				Title = "Cavalry",
				Stage = "riders",
				Weight = 100,
				Standoff = false,
				When = new CardWhen
				{
					Place = "wild",
					Factions = new List<string> { "army" },
					MinRank = 4
				},
				Notes = "An Army officer rides with his troopers."
			},
			new EncounterCard
			{
				Id = "army-road",
				Title = "Army on the road",
				Stage = "ahead",
				Weight = 50,
				Standoff = false,
				When = new CardWhen
				{
					Place = "wild",
					Mounted = true,
					Factions = new List<string> { "army" },
					MaxRank = 3
				},
				Notes = "A squad waits ahead on the road."
			},
			new EncounterCard
			{
				Id = "army-squad",
				Title = "Army squad",
				Stage = "idle",
				Weight = 50,
				Standoff = false,
				Activity = "patrol",
				When = new CardWhen
				{
					Place = "wild",
					Factions = new List<string> { "army" },
					MaxRank = 3
				},
				Notes = "A squad on patrol. The Army doesn't talk: once they see you, it's on."
			},
			new EncounterCard
			{
				Id = "skirmish",
				Title = "Faction skirmish",
				Stage = "skirmish",
				IniChance = "SkirmishChance",
				Chance = 15,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "Two factions going at each other ahead of you."
			},
			new EncounterCard
			{
				Id = "patrol",
				Title = "Patrol on the road",
				Stage = "patrol",
				IniChance = "ArmyPatrolChance",
				Chance = 12,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "A patrol marches past (or raids a camp nearby)."
			},
			new EncounterCard
			{
				Id = "wapiti-ridge",
				Title = "Riders on the ridge",
				Stage = "ridge",
				Faction = "wapiti",
				Chance = 12,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "Wapiti riders sit their horses on high ground and watch you go by, then ride off. Get close, fire near them or wait them out and they leave; shoot one and they come down at you."
			},
			new EncounterCard
			{
				Id = "wapiti-patrol",
				Title = "Wapiti patrol",
				Stage = "patrol",
				Faction = "wapiti",
				Chance = 4,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "Rarely, a few Wapiti riders cross their country. They leave you be unless you give them a reason - and they keep clear of gunfire."
			},
			new EncounterCard
			{
				Id = "wapiti-band",
				Title = "Wapiti war party",
				Stage = "idle",
				Weight = 50,
				Standoff = false,
				Taunts = new List<string> { "You were warned to leave us be.", "You came back. That was a mistake.", "You don't ride here any more." },
				When = new CardWhen
				{
					Place = "wild",
					Factions = new List<string> { "wapiti" }
				},
				Notes = "Once you've given them a reason: a war party of a named warrior and his men, found in their country."
			},
			new EncounterCard
			{
				Id = "wapiti-riders",
				Title = "Wapiti riders",
				Stage = "riders",
				Weight = 50,
				Standoff = false,
				Taunts = new List<string> { "There he is!", "You should have stayed off our land." },
				When = new CardWhen
				{
					Place = "wild",
					Mounted = true,
					Factions = new List<string> { "wapiti" }
				},
				Notes = "Once you've given them a reason: they ride you down."
			},
			new EncounterCard
			{
				Id = "war-warband",
				Title = "Warband",
				Stage = "warband",
				Chance = 22,
				CooldownMinutes = 120,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "In a war zone: riders of one side go past you to hit the other. Follow them and the fight is there."
			},
			new EncounterCard
			{
				Id = "war-ambush",
				Title = "Ambush in progress",
				Stage = "ambush",
				Chance = 18,
				CooldownMinutes = 120,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "In a war zone: one side waits by a road ahead for the other's riders. Warn them, join in or ride on."
			},
			new EncounterCard
			{
				Id = "war-aftermath",
				Title = "Just after",
				Stage = "aftermath",
				Chance = 18,
				CooldownMinutes = 120,
				When = new CardWhen
				{
					Place = "wild"
				},
				Notes = "In a war zone: you come upon a fight that has just ended - the dead, and the men still standing."
			},
			new EncounterCard
			{
				Id = "hunters",
				Title = "Bounty hunters",
				Stage = "hunters",
				Weight = 100,
				Notes = "A rich man who won't come himself pays bounty hunters. Disable to stop it."
			},
			new EncounterCard
			{
				Id = "wagon",
				Title = "Up the road on a wagon",
				Stage = "wagon",
				Weight = 12,
				When = new CardWhen
				{
					Place = "wild",
					NotFactions = new List<string> { "army", "wapiti" }
				},
				Cast = new CardCast
				{
					Crew = new List<int> { 1, 3 }
				},
				History = "CAME UP THE ROAD on a wagon ({place})",
				Notes = "He drives a wagon (a stagecoach if he's rich or an officer) up the road toward you, his men aboard. He climbs down when he sees you."
			},
			new EncounterCard
			{
				Id = "arc-ko-revenge",
				Title = "Back with friends",
				Stage = "idle",
				Weight = 70,
				Standoff = false,
				When = new CardWhen
				{
					Place = "wild",
					LastOutcome = new List<string> { "knocked out" }
				},
				Cast = new CardCast
				{
					Crew = new List<int> { 3, 5 }
				},
				Taunts = new List<string> { "You put me in the dirt, {name}. Brought some friends this time.", "Remember me? I remember you." },
				History = "CAME BACK WITH MORE MEN after you knocked him out ({place})",
				Notes = "You knocked him out last time: he comes back with more men, no talking."
			},
			new EncounterCard
			{
				Id = "arc-back-from-dead",
				Title = "Back from the dead",
				Stage = "idle",
				Weight = 80,
				When = new CardWhen
				{
					Place = "wild",
					LastOutcome = new List<string> { "cheated death" }
				},
				Taunts = new List<string> { "Surprised? You should've made sure.", "Takes more than that to kill me." },
				History = "CAME BACK FROM THE DEAD for you ({place})",
				Notes = "You left him for dead: he walks up to show you the scar."
			},
			new EncounterCard
			{
				Id = "arc-squared",
				Title = "Squaring it",
				Stage = "idle",
				Weight = 60,
				EndsPeacefully = true,
				StandoffSeconds = 18,
				BackDownHate = -12,
				When = new CardWhen
				{
					Place = "wild",
					LastOutcome = new List<string> { "surrendered", "backed off" },
					Tempers = new List<string> { "Proud", "Loyal", "Cold", "Drifter" }
				},
				Cast = new CardCast
				{
					Crew = new List<int> { 0, 0 }
				},
				Taunts = new List<string> { "You let me walk. I came to say it - we're square. This once.", "No trouble. Just wanted you to know I ain't forgot." },
				History = "CAME TO SQUARE IT with you - no fight ({place})",
				Notes = "He surrendered / backed down last time: he comes alone, says his piece and goes - unless you start it."
			},
			new EncounterCard
			{
				Id = "arc-runner",
				Title = "Tired of chasing",
				Stage = "riders",
				Weight = 60,
				Standoff = false,
				When = new CardWhen
				{
					Place = "wild",
					Mounted = true,
					LastOutcome = new List<string> { "got away" },
					MinArcStep = 2
				},
				Cast = new CardCast
				{
					Crew = new List<int> { 3, 4 }
				},
				Taunts = new List<string> { "You keep running, {name}. Not this time." },
				History = "RODE YOU DOWN after you got away twice ({place})",
				Notes = "He got away from you twice running: he brings riders."
			},
			new EncounterCard
			{
				Id = "arc-killer",
				Title = "Again?",
				Stage = "ahead",
				Weight = 40,
				Standoff = false,
				When = new CardWhen
				{
					Place = "wild",
					Mounted = true,
					LastOutcome = new List<string> { "killed you" }
				},
				Taunts = new List<string> { "Back for more? Last time didn't take?", "I killed you once already." },
				History = "WAITED ON THE ROAD - he killed you last time ({place})",
				Notes = "He killed you last time: he waits on the road, cocky."
			}
		};
	}

	private string CardsDir => Path.Combine(ContentDir, "encounters");

	private void LoadCards()
	{
		List<EncounterCard> cards = new List<EncounterCard>();
		try
		{
			Directory.CreateDirectory(CardsDir);
			string path = Path.Combine(CardsDir, "builtin.json");
			RefreshBuiltIn(path, JsonConvert.SerializeObject(new CardFile
			{
				About = "Encounter Cards (NemesisMod.Cards.cs). These are the mod's own encounters - edit them, or add cards in any other .json file here. Stage: idle / callout / riders / ahead / sniper (a nemesis + his men) or skirmish / patrol / hunters (the world's). When: Place wild/town/any, Mounted, Tiers, Intents, Factions (ids, 'loner'), NotFactions, Tempers, MinRank/MaxRank, Hours [from,to], Regions north/south/other, MinNotoriety, MinHate, KilledYou. Weight (or IniWeight = an [Encounters] key), Chance % for world stages, CooldownMinutes, Cast.Crew [min,max], Taunts (lines, {name}), Voice, Standoff, StandoffSeconds, SightRange, Activity ('patrol' or a scenario), BackDownHate, History.",
				Cards = BuiltInCards()
			}, Formatting.Indented, new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore
			}));
			string path2 = Path.Combine(CardsDir, "examples.json");
			if (!File.Exists(path2))
			{
				File.WriteAllText(path2, JsonConvert.SerializeObject(new CardFile
				{
					About = "Examples of your own cards - set Enabled to true to try them. Copy one into a new .json file to make your own.",
					Cards = new List<EncounterCard>
					{
						new EncounterCard
						{
							Id = "night-ambush",
							Title = "Night ambush",
							Enabled = false,
							Stage = "idle",
							Weight = 40,
							Standoff = false,
							SightRange = 25f,
							When = new CardWhen
							{
								Place = "wild",
								Hours = new List<int> { 21, 5 },
								Tiers = new List<string> { "Murderous" }
							},
							Cast = new CardCast
							{
								Crew = new List<int> { 3, 5 }
							},
							Taunts = new List<string> { "Dark enough for you?", "Nobody'll hear you out here." },
							Notes = "After dark a murderous man waits with more men, sees less far, and doesn't talk."
						},
						new EncounterCard
						{
							Id = "saloon-drunk",
							Title = "Drunk and sore",
							Enabled = false,
							Stage = "callout",
							Weight = 60,
							StandoffSeconds = 20,
							BackDownHate = -10,
							When = new CardWhen
							{
								Place = "town",
								Hours = new List<int> { 18, 2 },
								Tiers = new List<string> { "Resentful", "Vengeful" }
							},
							Taunts = new List<string> { "You... I know you. You owe me.", "Hey! Hey, you! Remember me?" },
							Notes = "Evenings in town: a resentful man who's been drinking takes his time - and cools off more if you talk him down."
						}
					}
				}, Formatting.Indented, new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore
				}));
			}
			foreach (string item in from p in Directory.GetFiles(CardsDir, "*.json")
				orderby (!(Path.GetFileName(p) == "builtin.json")) ? p : ""
				select p)
			{
				try
				{
					JToken jToken = JToken.Parse(File.ReadAllText(item));
					foreach (EncounterCard c in ((jToken.Type == JTokenType.Array) ? jToken.ToObject<List<EncounterCard>>() : ((jToken["Cards"] != null || jToken["cards"] != null) ? jToken.ToObject<CardFile>().Cards : new List<EncounterCard> { jToken.ToObject<EncounterCard>() })).Where((EncounterCard encounterCard) => encounterCard != null && !string.IsNullOrEmpty(encounterCard.Stage)))
					{
						c.Id = (string.IsNullOrEmpty(c.Id) ? (Path.GetFileNameWithoutExtension(item) + "-" + cards.Count) : c.Id);
						c.Stage = c.Stage.Trim().ToLowerInvariant();
						if (c.When != null)
						{
							c.When.Place = (c.When.Place ?? "any").Trim().ToLowerInvariant();
							c.When.Regions = c.When.Regions?.Select((string x) => x.Trim().ToLowerInvariant()).ToList();
							c.When.Factions = c.When.Factions?.Select((string x) => x.Trim().ToLowerInvariant()).ToList();
							c.When.NotFactions = c.When.NotFactions?.Select((string x) => x.Trim().ToLowerInvariant()).ToList();
							c.When.LastOutcome = c.When.LastOutcome?.Select((string x) => x.Trim().ToLowerInvariant()).ToList();
							c.When.Tiers = c.When.Tiers?.Select(Cap).ToList();
							c.When.Tempers = c.When.Tempers?.Select(Cap).ToList();
						}
						if (!KnownStages.Contains(c.Stage))
						{
							GrudgeLog("CARD " + c.Id + ": unknown stage '" + c.Stage + "' - it will never play");
						}
						c.When = c.When ?? new CardWhen();
						c.Cast = c.Cast ?? new CardCast();
						cards.RemoveAll((EncounterCard x) => x.Id == c.Id);
						cards.Add(c);
					}
				}
				catch (Exception ex)
				{
					KeepBadFile(item, ex);
				}
			}
		}
		catch (Exception ex2)
		{
			GrudgeLog("CARD loading failed: " + ex2.Message);
		}
		if (cards.Count == 0)
		{
			cards = BuiltInCards();
		}
		else
		{
			List<EncounterCard> list = (from b in BuiltInCards()
				where !cards.Any((EncounterCard encounterCard) => encounterCard.Id == b.Id)
				select b).ToList();
			if (list.Count > 0)
			{
				cards.AddRange(list);
				GrudgeLog(string.Format("CARD {0} new built-in card(s) not in your files: {1}", list.Count, string.Join(", ", list.Select((EncounterCard a) => a.Id))));
			}
		}
		Cards = cards;
		GrudgeLog(string.Format("CARD {0} encounter cards loaded ({1} enabled): {2}", cards.Count, cards.Count((EncounterCard encounterCard) => encounterCard.Enabled), string.Join(", ", from encounterCard in cards
			where encounterCard.Enabled
			select encounterCard.Id)));
		static string Cap(string x)
		{
			x = x.Trim();
			if (x.Length != 0)
			{
				return char.ToUpperInvariant(x[0]) + x.Substring(1).ToLowerInvariant();
			}
			return x;
		}
	}

	private int CardWeight(EncounterCard c)
	{
		return c.IniWeight switch
		{
			"Ambush" => Config.EncAmbush, 
			"Riders" => Config.EncRiders, 
			"Ahead" => Config.EncAhead, 
			"Sniper" => Config.EncSniper, 
			"Callout" => Config.EncCallout, 
			_ => c.Weight, 
		};
	}

	private int CardChance(EncounterCard c)
	{
		if (!(c.IniChance == "SkirmishChance"))
		{
			if (!(c.IniChance == "ArmyPatrolChance"))
			{
				return c.Chance;
			}
			return Balance.PatrolChance;
		}
		return Balance.SkirmishChance;
	}

	private string RegionKey
	{
		get
		{
			if (!North)
			{
				if (!South)
				{
					return "other";
				}
				return "south";
			}
			return "north";
		}
	}

	private bool CardFits(EncounterCard c, NemesisRecord rec, bool town, bool mounted, bool test)
	{
		return CardBlock(c, rec, town, mounted, test) == null;
	}

	private string CardBlock(EncounterCard c, NemesisRecord rec, bool town, bool mounted, bool test)
	{
		if (!c.Enabled)
		{
			return "off";
		}
		CardWhen cardWhen = c.When ?? new CardWhen();
		if (cardWhen.Place == "wild" && town)
		{
			return "wild only";
		}
		if (cardWhen.Place == "town" && !town)
		{
			return "town only";
		}
		if (cardWhen.Mounted.HasValue && cardWhen.Mounted.Value != mounted)
		{
			if (!mounted)
			{
				return "riding only";
			}
			return "on foot only";
		}
		int num;
		int num2;
		if (!test)
		{
			if (Config.TensionEnabled && c.TensionMin > 0 && Tension < OnScale(c.TensionMin))
			{
				return $"tension under {OnScale(c.TensionMin):0}";
			}
			if (Config.TensionEnabled && c.TensionMax > 0 && Tension > OnScale(c.TensionMax))
			{
				return $"tension over {OnScale(c.TensionMax):0}";
			}
			if (cardWhen.Hours != null && cardWhen.Hours.Count == 2)
			{
				int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
				num = cardWhen.Hours[0];
				num2 = cardWhen.Hours[1];
				bool num3;
				if (num > num2)
				{
					if (cLOCK_HOURS >= num)
					{
						goto IL_0196;
					}
					num3 = cLOCK_HOURS > num2;
				}
				else
				{
					if (cLOCK_HOURS < num)
					{
						goto IL_017d;
					}
					num3 = cLOCK_HOURS > num2;
				}
				if (num3)
				{
					goto IL_017d;
				}
			}
			goto IL_0196;
		}
		goto IL_02a9;
		IL_017d:
		return $"hours {num}-{num2}";
		IL_02a9:
		if (rec == null && c.Faction != null && Gangs.ById(c.Faction) == null)
		{
			return "unknown faction";
		}
		if (rec == null && c.Faction != null && !Gangs.InEra(Gangs.ById(c.Faction)))
		{
			return "gone by " + Gangs.Era;
		}
		if (rec == null)
		{
			return null;
		}
		string text = rec.Gang ?? "loner";
		if (cardWhen.Factions != null && cardWhen.Factions.Count > 0)
		{
			if (!cardWhen.Factions.Contains(text))
			{
				return "factions " + string.Join("/", cardWhen.Factions);
			}
		}
		else if (Gangs.Squads(rec))
		{
			return "squads play own cards";
		}
		if (cardWhen.NotFactions != null && cardWhen.NotFactions.Contains(text))
		{
			return "not " + text;
		}
		if (cardWhen.Tiers != null && cardWhen.Tiers.Count > 0 && !cardWhen.Tiers.Contains(rec.HateTier.ToString()))
		{
			return "tier " + string.Join("/", cardWhen.Tiers);
		}
		if (cardWhen.Intents != null && cardWhen.Intents.Count > 0 && !cardWhen.Intents.Contains(IntentOf(rec).ToString()))
		{
			return "intent " + string.Join("/", cardWhen.Intents);
		}
		if (cardWhen.Tempers != null && cardWhen.Tempers.Count > 0 && (!rec.Personality.HasValue || !cardWhen.Tempers.Contains(rec.Personality.Value.ToString())))
		{
			return "temper " + string.Join("/", cardWhen.Tempers);
		}
		if (cardWhen.MinRank > 0 && rec.Rank < cardWhen.MinRank)
		{
			return $"rank {cardWhen.MinRank}+";
		}
		if (cardWhen.MaxRank > 0 && rec.Rank > cardWhen.MaxRank)
		{
			return $"rank <= {cardWhen.MaxRank}";
		}
		if (cardWhen.MinHate > 0 && rec.Hate < cardWhen.MinHate)
		{
			return $"hate {cardWhen.MinHate}+";
		}
		if (cardWhen.KilledYou && rec.TimesKilledPlayer == 0)
		{
			return "must have killed you";
		}
		if (cardWhen.LastOutcome != null && cardWhen.LastOutcome.Count > 0 && !cardWhen.LastOutcome.Contains(rec.LastOutcome ?? ""))
		{
			return "last time: " + string.Join("/", cardWhen.LastOutcome);
		}
		if (cardWhen.MinArcStep > 0 && rec.ArcStep < cardWhen.MinArcStep)
		{
			return $"{cardWhen.MinArcStep}x in a row";
		}
		if (cardWhen.MinNotoriety > 0 && NotorietyOf(Gangs.Of(rec)) < cardWhen.MinNotoriety)
		{
			return $"notoriety {cardWhen.MinNotoriety}+";
		}
		return null;
		IL_0196:
		if (cardWhen.Regions != null && cardWhen.Regions.Count > 0 && !cardWhen.Regions.Contains(RegionKey))
		{
			return "region " + string.Join("/", cardWhen.Regions);
		}
		if (cardWhen.Weather != null && cardWhen.Weather.Count > 0 && !cardWhen.Weather.Any((string x) => string.Equals(x, WeatherNow, StringComparison.OrdinalIgnoreCase)))
		{
			return "weather " + string.Join("/", cardWhen.Weather);
		}
		if (c.CooldownMinutes > 0 && _cardUsedAt.TryGetValue(c.Id, out var value) && GameMinutesNow() >= value && GameMinutesNow() - value < c.CooldownMinutes)
		{
			return $"cooling {c.CooldownMinutes - (GameMinutesNow() - value)} min";
		}
		if (rec == null && c.Faction != null && RegionMult(Gangs.ById(c.Faction)) <= 0.0)
		{
			return "not their country";
		}
		goto IL_02a9;
	}

	private bool Forced(EncounterCard c)
	{
		if (!string.IsNullOrEmpty(Config.EncounterForce))
		{
			if (!string.Equals(c.Id, Config.EncounterForce, StringComparison.OrdinalIgnoreCase) && !string.Equals(c.Stage, Config.EncounterForce, StringComparison.OrdinalIgnoreCase))
			{
				if (Config.EncounterForce.Equals("Ambush", StringComparison.OrdinalIgnoreCase))
				{
					return c.Stage == "idle";
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private EncounterCard PickCard(NemesisRecord rec, bool town, bool mounted, bool test)
	{
		List<EncounterCard> list = Cards.Where((EncounterCard c) => NemesisStages.Contains(c.Stage) && CardFits(c, rec, town, mounted, test)).ToList();
		CardReport = (from c in Cards
			where NemesisStages.Contains(c.Stage)
			select (Id: c.Id, CardWeight(c), CardBlock(c, rec, town, mounted, test))).ToList();
		CardReportFor = string.Format("{0} ({1}, {2}{3})", rec.Name, rec.HateTier, town ? "town" : "wild", mounted ? ", riding" : "");
		List<EncounterCard> list2 = list.Where(Forced).ToList();
		if (list2.Count > 0)
		{
			list = list2;
		}
		int num = list.Sum((EncounterCard c) => Math.Max(0, CardWeight(c)));
		if (num <= 0)
		{
			LastCardNote = string.Format("no card fits {0} ({1}{2}, {3})", rec.Name, town ? "town" : "wild", mounted ? ", riding" : "", rec.HateTier);
			return null;
		}
		int num2 = _rng.Next(num);
		foreach (EncounterCard item in list)
		{
			num2 -= Math.Max(0, CardWeight(item));
			if (num2 < 0)
			{
				LastCardNote = string.Format("card {0} for {1} ({2} fit: {3})", item.Id, rec.Name, list.Count, string.Join(", ", list.Select((EncounterCard x) => $"{x.Id} {CardWeight(x)}")));
				return item;
			}
		}
		return null;
	}

	private bool RunWorldCards(Ped player, bool town, bool lawOnly)
	{
		if (lawOnly)
		{
			return false;
		}
		bool hasValue = WarHere.HasValue;
		foreach (EncounterCard item in Cards.Where((EncounterCard c) => (c.Stage == "skirmish" || c.Stage == "patrol" || c.Stage == "ridge" || WarCardStage(c.Stage)) && CardFits(c, null, town, PED.IS_PED_ON_MOUNT(player.Handle), test: false)))
		{
			bool flag = Forced(item);
			if ((WarCardStage(item.Stage) && !flag) || (item.Stage == "skirmish" && hasValue && !flag))
			{
				continue;
			}
			int num = CardChance(item);
			if (flag || _rng.Next(100) < num)
			{
				Gang gang = Gangs.ById(item.Faction);
				if (WarCardStage(item.Stage) ? RunWarCard(item.Stage, player) : ((item.Stage == "skirmish") ? TrySkirmish(player, town, flag, rolled: true) : ((item.Stage == "ridge") ? SpawnRidgeWatch(player, gang ?? Gangs.Wapiti) : SpawnRoadPatrol(player, force: false, gang))))
				{
					UseCard(item);
					_worldCardsPlayed++;
					LastEncounterNote = ((item.Stage == "skirmish") ? ("skirmish: " + SkirmishNote) : ((item.Stage == "ridge") ? ("card " + item.Id + ": riders on a ridge") : (WarCardStage(item.Stage) ? ("card " + item.Id + ": " + SkirmishNote) : ("card " + item.Id + ": patrol on the road"))));
					GrudgeLog("CARD " + item.Id + " (" + item.Stage + ") played");
					return true;
				}
			}
		}
		return false;
	}

	private bool HuntersCardOn => Cards.Any((EncounterCard c) => c.Stage == "hunters" && c.Enabled);

	private void UseCard(EncounterCard c)
	{
		if (c != null)
		{
			_cardUsedAt[c.Id] = GameMinutesNow();
		}
	}

	private static EncounterKind StageKind(string stage)
	{
		return stage switch
		{
			"callout" => EncounterKind.Callout, 
			"riders" => EncounterKind.Riders, 
			"ahead" => EncounterKind.Ahead, 
			"sniper" => EncounterKind.Sniper, 
			_ => EncounterKind.Idle, 
		};
	}

	private string CardTaunt(EncounterCard c, NemesisRecord rec, bool returning)
	{
		if (c?.Taunts != null && c.Taunts.Count > 0 && (!returning || c.When?.LastOutcome?.Contains("cheated death") == true))
		{
			return c.Taunts[_rng.Next(c.Taunts.Count)].Replace("{name}", rec.Name ?? "");
		}
		return Taunts.Ambush(rec, returning, _rng, LeaderAsSaidBy);
	}

	internal IEnumerable<string> CardReportLines()
	{
		if (CardReport.Count == 0)
		{
			yield break;
		}
		int total = CardReport.Where(((string id, int weight, string why) r) => r.why == null).Sum(((string id, int weight, string why) r) => Math.Max(0, r.weight));
		yield return "cards for " + CardReportFor + ":";
		foreach (var item in (from r in CardReport
			orderby r.why != null, r.weight descending
			select r).Take(8))
		{
			yield return (item.why == null) ? $"   {item.id}  weight {item.weight}  = {((total > 0) ? (100 * Math.Max(0, item.weight) / total) : 0)}%" : ("   " + item.id + "  -  no: " + item.why);
		}
	}

	internal string CardsDebugLine()
	{
		return $"cards: {Cards.Count} loaded, {Cards.Count((EncounterCard c) => c.Enabled)} on - {LastCardNote}";
	}

	private void MakeSure(int handle, NemesisRecord rec)
	{
		_nemesisBodies.Remove(handle);
		if (rec.PresumedDead && rec.IsAlive)
		{
			rec.IsAlive = false;
			rec.PresumedDead = false;
			SaveRoster();
		}
		GrudgeLog("MADE SURE of " + rec.FullName + " (no notice - user 2026-10-08: just make it happen)");
	}
}
