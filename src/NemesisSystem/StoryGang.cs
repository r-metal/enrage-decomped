using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class StoryGang
{
	public const string Id = "vdl";

	private static NemesisRecord P(string key, string name, string title, int rank, int born, string birthplace, Personality temper, Sociability soc, Disposition disp, int aggr, Smarts smarts, Wealth wealth, string trade, string faith, Devotion dev, string family, int children, string facets, string values, string needs, string[] quirks, (string type, string target, string text)[] goals, params (int y, int m, string kind, string text)[] past)
	{
		return new NemesisRecord
		{
			Id = "vdl_" + key,
			Name = name,
			Title = title,
			Rank = rank,
			Gang = "vdl",
			IsAlive = true,
			Story = true,
			Personality = temper,
			PersonalityKnown = true,
			TraitsRolled = true,
			Sociability = soc,
			Disposition = disp,
			Aggression = aggr,
			Smarts = smarts,
			Wealth = wealth,
			Trade = trade,
			Faith = faith,
			Devotion = dev,
			Family = family,
			Children = children,
			BornYear = born,
			Birthplace = birthplace,
			PastRolled = true,
			Woven = true,
			MindRolled = true,
			QuirksRolled = true,
			WantsKnown = true,
			Origin = GrudgeOrigin.GangMember,
			Hate = 0,
			Quirks = (quirks?.ToList() ?? new List<string>()),
			Facets = Parse(facets),
			Values = Parse(values),
			Needs = Parse(needs),
			Goals = (goals?.Select<(string, string, string), Goal>(delegate((string type, string target, string text) g)
			{
				Goal goal = new Goal();
				(goal.Type, goal.Target, goal.Text) = g;
				return goal;
			}).ToList() ?? new List<Goal>()),
			Past = past.Select(((int y, int m, string kind, string text) e) => new PastEntry
			{
				Year = e.y,
				Month = e.m,
				Kind = e.kind,
				Text = e.text,
				Public = true,
				Known = true
			}).ToList()
		};
	}

	private static Dictionary<string, int> Parse(string s)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		string[] array = (s ?? "").Split(' ');
		for (int i = 0; i + 1 < array.Length; i += 2)
		{
			if (int.TryParse(array[i + 1], out var result))
			{
				dictionary[array[i]] = result;
			}
		}
		return dictionary;
	}

	private static (string, string, string)[] G(params (string, string, string)[] g)
	{
		return g;
	}

	private static string[] Q(params string[] q)
	{
		return q;
	}

	public static List<NemesisRecord> Build()
	{
		Personality temper = Personality.Loyal;
		List<NemesisRecord> list = new List<NemesisRecord>
		{
			P("dutch", "Dutch van der Linde", "the leader", 5, 1855, null, Personality.Proud, Sociability.Outgoing, Disposition.Friendly, 55, Smarts.Sharp, Wealth.Working, "Outlaw", "none", Devotion.Lapsed, "single", 0, "Anger 50 Bravery 80 Greed 45 Loyalty 55 Pride 92 Honesty 30 Piety 20 Cruelty 40 Humour 55 Ambition 90", "Family 1 Gang 3 Money 1 Honour 2 Law -3 God -1", "Drink 30 Company 70 Faith 10 Money 70 Violence 30", Q("braggart"), G(("Rich", null, "one more big score - then somewhere the government can't reach")), (1855, 0, "born", "Born"), (1863, 0, "loss", "Lost his father to the war"), (1875, 0, "gang", "Met Hosea Matthews - each tried to rob the other; they rode together after"), (1877, 0, "gang", "Took in a fourteen-year-old orphan, Arthur Morgan"), (1885, 0, "gang", "Cut a twelve-year-old John Marston down from a hanging"), (1889, 0, "loss", "(about) Lost Annabelle to Colm O'Driscoll's men - the feud never ended"), (1895, 0, "family", "Took up with Molly O'Shea"), (1899, 2, "crime", "The Blackwater ferry job went wrong - the gang ran with the law behind it"), (1899, 3, "gang", "Led the gang up into the snow of the Grizzlies, to Colter")),
			P("hosea", "Hosea Matthews", "Dutch's oldest friend", 4, 1844, null, Personality.Loyal, Sociability.Outgoing, Disposition.Friendly, 20, Smarts.Sharp, Wealth.Working, "Con man", "none", Devotion.Lapsed, "widower", 0, "Anger 20 Bravery 60 Greed 40 Loyalty 85 Pride 50 Honesty 55 Piety 30 Cruelty 10 Humour 80 Ambition 35", "Family 2 Gang 3 Money 1 Honour 2 Law -1 God 0", "Drink 35 Company 60 Faith 20 Money 40 Violence 5", Q("gambler"), G(("Settle", null, "one last big con, then a quiet bit of land")), (1844, 0, "born", "Born"), (1870, 0, "family", "(about) Married Bessie"), (1875, 0, "gang", "Met Dutch van der Linde - each tried to rob the other"), (1877, 0, "gang", "Helped raise a young Arthur Morgan"), (1893, 0, "loss", "(about) Buried Bessie"), (1899, 2, "crime", "Ran from Blackwater with the rest")),
			P("arthur", "Arthur Morgan", "(you)", 4, 1863, null, Personality.Cold, Sociability.Introvert, Disposition.Surly, 60, Smarts.Sharp, Wealth.Poor, "Outlaw", "none", Devotion.Lapsed, "single", 0, "Anger 50 Bravery 85 Greed 30 Loyalty 90 Pride 55 Honesty 50 Piety 20 Cruelty 40 Humour 50 Ambition 25", "Family 1 Gang 3 Money 0 Honour 1 Law -2 God -1", "Drink 40 Company 30 Faith 10 Money 30 Violence 40", Q(), G(), (1863, 0, "born", "Born"), (1874, 0, "loss", "His mother, Beatrice, died"), (1877, 0, "gang", "Taken in by Dutch and Hosea at fourteen"), (1878, 0, "loss", "His father, Lyle, an outlaw, died"), (1889, 0, "family", "(about) Mary Linton broke it off - her father wouldn't have an outlaw"), (1890, 0, "loss", "(about) Lost Eliza and their boy, Isaac, to robbers - over ten dollars"), (1899, 2, "crime", "Blackwater: the ferry job, and the run")),
			P("micah", "Micah Bell", "Dutch's new favourite", 3, 1860, null, Personality.Hothead, Sociability.Outgoing, Disposition.Surly, 80, Smarts.Average, Wealth.Poor, "Outlaw", "none", Devotion.Godless, "single", 0, "Anger 75 Bravery 70 Greed 75 Loyalty 15 Pride 80 Honesty 10 Piety 5 Cruelty 85 Humour 40 Ambition 85", "Family 0 Gang 0 Money 3 Honour -2 Law -3 God -2", "Drink 50 Company 40 Faith 0 Money 80 Violence 75", Q("sadist", "braggart"), G(("Rise", null, "Dutch's ear - and everything after")), (1860, 0, "born", "Born - his father and grandfather were outlaws before him"), (1898, 10, "gang", "Saved Dutch's life in a deal gone bad; Dutch took him in"), (1899, 2, "crime", "Brought Dutch the Blackwater ferry job")),
			P("john", "John Marston", "a gun", 3, 1873, null, Personality.Drifter, Sociability.Introvert, Disposition.Surly, 55, Smarts.Average, Wealth.Poor, "Outlaw", "none", Devotion.Lapsed, "single", 1, "Anger 55 Bravery 75 Greed 30 Loyalty 65 Pride 60 Honesty 45 Piety 15 Cruelty 30 Humour 40 Ambition 30", "Family 1 Gang 2 Money 1 Honour 1 Law -2 God -1", "Drink 40 Company 35 Faith 5 Money 40 Violence 40", Q(), G(("Settle", null, "something he won't say out loud - a life away from all this")), (1873, 0, "born", "Born"), (1885, 0, "gang", "Saved from a hanging at twelve by Dutch; raised in the gang"), (1895, 0, "family", "His son Jack was born, to Abigail"), (1897, 0, "gang", "(about) Ran off from the gang for a year; came back"), (1899, 2, "crime", "Wounded in the Blackwater job"), (1899, 3, "loss", "Wolves tore his face in the Grizzlies")),
			P("javier", "Javier Escuella", "a gun", 2, 1871, "Mexico", temper, Sociability.Outgoing, Disposition.Friendly, 55, Smarts.Average, Wealth.Poor, "Outlaw", "Catholic", Devotion.Lapsed, "single", 0, "Anger 45 Bravery 80 Greed 25 Loyalty 92 Pride 65 Honesty 55 Piety 30 Cruelty 35 Humour 60 Ambition 30", "Family 1 Gang 3 Money 0 Honour 2 Law -2 God 0", "Drink 35 Company 60 Faith 20 Money 30 Violence 40", Q(), G(("Protect", "vdl_dutch", "follow Dutch - he believes in him")), (1871, 0, "born", "Born in Mexico"), (1894, 0, "crime", "(about) Killed an army officer over a woman and fled north"), (1895, 0, "gang", "Joined Dutch's gang"), (1899, 2, "crime", "Ran from Blackwater with the rest")),
			P("bill", "Bill Williamson", "a gun", 2, 1866, null, Personality.Hothead, Sociability.Outgoing, Disposition.Surly, 75, Smarts.Dim, Wealth.Poor, "Outlaw", "none", Devotion.Lapsed, "single", 0, "Anger 75 Bravery 70 Greed 40 Loyalty 80 Pride 45 Honesty 50 Piety 10 Cruelty 45 Humour 35 Ambition 20", "Family 0 Gang 3 Money 1 Honour 0 Law -2 God -1", "Drink 70 Company 50 Faith 5 Money 40 Violence 60", Q("drunk"), G(("Protect", "vdl_dutch", "stay in Dutch's good books")), (1866, 0, "born", "Born Marion Williamson"), (1884, 0, "work", "(about) Joined the Army"), (1892, 0, "crime", "Dishonourably discharged for attempted murder"), (1892, 0, "gang", "Dutch found him starving and took him in"), (1899, 2, "crime", "Ran from Blackwater with the rest")),
			P("charles", "Charles Smith", "a gun, a tracker", 2, 1873, null, temper, Sociability.Introvert, Disposition.Friendly, 40, Smarts.Sharp, Wealth.Poor, "Hunter", "none", Devotion.Lapsed, "single", 0, "Anger 30 Bravery 85 Greed 15 Loyalty 70 Pride 40 Honesty 92 Piety 25 Cruelty 10 Humour 40 Ambition 20", "Family 1 Gang 2 Money 0 Honour 3 Law -1 God 0", "Drink 15 Company 30 Faith 15 Money 20 Violence 25", Q("honest"), G(), (1873, 0, "born", "Born - his mother Native, his father African American"), (1880, 0, "loss", "(about) His mother was taken away by soldiers"), (1898, 9, "gang", "Joined the gang, six months before Blackwater")),
			P("lenny", "Lenny Summers", "a gun", 2, 1880, null, temper, Sociability.Outgoing, Disposition.Friendly, 40, Smarts.Sharp, Wealth.Poor, "Outlaw", "none", Devotion.Lapsed, "single", 0, "Anger 30 Bravery 70 Greed 25 Loyalty 80 Pride 45 Honesty 70 Piety 20 Cruelty 10 Humour 80 Ambition 40", "Family 2 Gang 2 Money 0 Honour 2 Law -1 God 0", "Drink 30 Company 70 Faith 10 Money 25 Violence 20", Q(), G(), (1880, 0, "born", "Born; schooled - he reads"), (1895, 0, "loss", "His father was killed; he killed the man who did it"), (1898, 0, "gang", "After three years on the run, joined the gang")),
			P("sean", "Sean MacGuire", "a gun", 2, 1877, null, Personality.Hothead, Sociability.Outgoing, Disposition.Friendly, 60, Smarts.Average, Wealth.Poor, "Outlaw", "Catholic", Devotion.Lapsed, "single", 0, "Anger 55 Bravery 70 Greed 45 Loyalty 70 Pride 75 Honesty 40 Piety 15 Cruelty 25 Humour 90 Ambition 45", "Family 1 Gang 2 Money 1 Honour 1 Law -3 God 0", "Drink 70 Company 85 Faith 10 Money 50 Violence 40", Q("braggart", "drunk"), G(), (1877, 0, "born", "Born to an Irish family of rebels and thieves; his father was killed"), (1896, 0, "gang", "Tried to rob Dutch; joined him instead"), (1899, 2, "crime", "Taken by bounty hunters after Blackwater")),
			P("sadie", "Sadie Adler", "the widow", 2, 1874, "the mountains", Personality.Hothead, Sociability.Introvert, Disposition.Surly, 75, Smarts.Sharp, Wealth.Working, "Rancher", "none", Devotion.Lapsed, "widower", 0, "Anger 85 Bravery 90 Greed 15 Loyalty 70 Pride 70 Honesty 75 Piety 15 Cruelty 45 Humour 30 Ambition 30", "Family 3 Gang 1 Money 0 Honour 2 Law -1 God 0", "Drink 30 Company 25 Faith 10 Money 15 Violence 70", Q(), G(("Avenge", "odriscoll", "the O'Driscolls who killed Jake")), (1874, 0, "born", "Born; grew up riding, shooting and hunting in the mountains"), (1894, 0, "family", "(about) Married Jake Adler; they ran a ranch"), (1899, 3, "loss", "The O'Driscolls killed Jake; she hid, then the gang found her")),
			P("karen", "Karen Jones", "a gun", 2, 1874, null, Personality.Hothead, Sociability.Outgoing, Disposition.Friendly, 55, Smarts.Average, Wealth.Poor, "Con woman", "none", Devotion.Lapsed, "single", 0, "Anger 50 Bravery 75 Greed 45 Loyalty 70 Pride 55 Honesty 45 Piety 10 Cruelty 25 Humour 75 Ambition 35", "Family 0 Gang 2 Money 1 Honour 1 Law -2 God -1", "Drink 75 Company 80 Faith 5 Money 50 Violence 35", Q("drunk"), G(), (1874, 0, "born", "Born"), (1896, 0, "gang", "(about) Joined the gang - a thief and a fine liar")),
			P("grimshaw", "Susan Grimshaw", "runs the camp", 1, 1857, null, Personality.Cold, Sociability.Outgoing, Disposition.Surly, 60, Smarts.Sharp, Wealth.Poor, "Outlaw", "none", Devotion.Lapsed, "single", 0, "Anger 60 Bravery 80 Greed 20 Loyalty 92 Pride 70 Honesty 60 Piety 15 Cruelty 45 Humour 25 Ambition 30", "Family 1 Gang 3 Money 0 Honour 2 Law -2 God -1", "Drink 25 Company 50 Faith 5 Money 20 Violence 25", Q(), G(("Protect", "vdl", "keep the camp in order")), (1857, 0, "born", "Born"), (1877, 0, "gang", "(about) Rode with Dutch since the early days - his woman, once")),
			P("pearson", "Simon Pearson", "the cook", 1, 1850, null, Personality.Proud, Sociability.Outgoing, Disposition.Friendly, 30, Smarts.Average, Wealth.Poor, "Cook", "none", Devotion.Lapsed, "single", 0, "Anger 35 Bravery 45 Greed 35 Loyalty 70 Pride 70 Honesty 55 Piety 20 Cruelty 15 Humour 55 Ambition 30", "Family 1 Gang 2 Money 1 Honour 1 Law -1 God 0", "Drink 40 Company 60 Faith 10 Money 35 Violence 10", Q("braggart"), G(), (1850, 0, "born", "Born to a family of whalers"), (1870, 0, "work", "(about) Served in the Navy, briefly"), (1889, 0, "gang", "(about) Dutch found him in money trouble and took him in")),
			P("strauss", "Leopold Strauss", "the moneylender", 1, 1846, "Vienna", Personality.Greedy, Sociability.Introvert, Disposition.Neutral, 15, Smarts.Sharp, Wealth.Working, "Moneylender", "Catholic", Devotion.Lapsed, "single", 0, "Anger 20 Bravery 25 Greed 85 Loyalty 65 Pride 45 Honesty 35 Piety 15 Cruelty 35 Humour 20 Ambition 50", "Family 0 Gang 2 Money 3 Honour 0 Law -1 God 0", "Drink 10 Company 25 Faith 15 Money 85 Violence 0", Q(), G(("Rich", null, "the money owed to the gang - every cent")), (1846, 0, "born", "Born poor in Vienna"), (1870, 0, "work", "(about) Came to America and lived by scams"), (1887, 0, "gang", "(about) Joined Dutch for protection; keeps the books")),
			P("uncle", "Uncle", "the old hand", 1, 1843, null, Personality.Coward, Sociability.Outgoing, Disposition.Friendly, 15, Smarts.Average, Wealth.Poor, "Drifter", "none", Devotion.Lapsed, "single", 0, "Anger 15 Bravery 20 Greed 45 Loyalty 55 Pride 40 Honesty 20 Piety 10 Cruelty 10 Humour 80 Ambition 5", "Family 0 Gang 2 Money 1 Honour 0 Law -1 God 0", "Drink 85 Company 70 Faith 5 Money 40 Violence 5", Q("drunk"), G(), (1843, 0, "born", "Born - he jokes it was between the fall of 1849 and the fall of Rome"), (1887, 0, "gang", "(about) Joined the gang"), (1894, 0, "gang", "Brought Abigail Roberts to the gang")),
			P("swanson", "Reverend Swanson", "the reverend", 1, 1851, null, Personality.Coward, Sociability.Outgoing, Disposition.Friendly, 15, Smarts.Sharp, Wealth.Poor, "Preacher", "Protestant", Devotion.Churchgoer, "single", 0, "Anger 15 Bravery 30 Greed 20 Loyalty 65 Pride 35 Honesty 55 Piety 60 Cruelty 5 Humour 55 Ambition 10", "Family 0 Gang 2 Money 0 Honour 1 Law 0 God 2", "Drink 85 Company 55 Faith 60 Money 20 Violence 0", Q("drunk"), G(), (1851, 0, "born", "Born"), (1880, 0, "faith", "(about) A minister - who lost his way to morphine, then whiskey"), (1890, 0, "gang", "(about) Once saved Dutch's life; rode with the gang after")),
			P("trelawny", "Josiah Trelawny", "the gentleman", 1, 1849, null, Personality.Drifter, Sociability.Outgoing, Disposition.Friendly, 20, Smarts.Sharp, Wealth.Comfortable, "Con man", "none", Devotion.Lapsed, "single", 0, "Anger 20 Bravery 45 Greed 55 Loyalty 50 Pride 70 Honesty 30 Piety 15 Cruelty 10 Humour 80 Ambition 40", "Family 2 Gang 1 Money 2 Honour 1 Law -1 God 0", "Drink 35 Company 70 Faith 5 Money 55 Violence 5", Q(), G(), (1849, 0, "born", "Born"), (1880, 0, "gang", "(about) Became Dutch's friend; comes and goes as he pleases")),
			P("abigail", "Abigail Roberts", "John's woman", 1, 1877, null, Personality.Proud, Sociability.Outgoing, Disposition.Neutral, 45, Smarts.Sharp, Wealth.Poor, "Outlaw", "none", Devotion.Lapsed, "single", 1, "Anger 55 Bravery 70 Greed 20 Loyalty 75 Pride 70 Honesty 70 Piety 20 Cruelty 10 Humour 55 Ambition 40", "Family 3 Gang 1 Money 1 Honour 2 Law -1 God 0", "Drink 20 Company 55 Faith 10 Money 40 Violence 5", Q("family man"), G(("Protect", "vdl_jack", "a better life for Jack")), (1877, 0, "born", "Born; orphaned young"), (1894, 0, "gang", "Uncle brought her to the gang"), (1895, 0, "family", "Her son Jack was born")),
			P("jack", "Jack Marston", "the boy", 1, 1895, null, Personality.Loyal, Sociability.Introvert, Disposition.Friendly, 5, Smarts.Sharp, Wealth.Poor, "Child", "none", Devotion.Lapsed, "single", 0, "Anger 10 Bravery 70 Greed 10 Loyalty 70 Pride 20 Honesty 85 Piety 15 Cruelty 0 Humour 50 Ambition 20", "Family 3 Gang 1 Money 0 Honour 1 Law 0 God 0", "Drink 0 Company 60 Faith 5 Money 0 Violence 0", Q(), G(), (1895, 0, "born", "Born into the gang, to John and Abigail")),
			P("tilly", "Tilly Jackson", "camp", 1, 1876, null, temper, Sociability.Outgoing, Disposition.Friendly, 35, Smarts.Sharp, Wealth.Poor, "Outlaw", "none", Devotion.Lapsed, "single", 0, "Anger 35 Bravery 65 Greed 20 Loyalty 80 Pride 55 Honesty 70 Piety 20 Cruelty 10 Humour 60 Ambition 40", "Family 1 Gang 3 Money 0 Honour 2 Law -1 God 0", "Drink 20 Company 70 Faith 10 Money 25 Violence 10", Q("honest"), G(), (1876, 0, "born", "Born"), (1888, 0, "loss", "Taken at twelve by the Foreman Brothers' gang"), (1891, 0, "gang", "(about) Taken in by Dutch, who taught her to read")),
			P("marybeth", "Mary-Beth Gaskill", "camp", 1, 1876, null, temper, Sociability.Outgoing, Disposition.Friendly, 15, Smarts.Sharp, Wealth.Poor, "Pickpocket", "none", Devotion.Churchgoer, "single", 0, "Anger 15 Bravery 45 Greed 25 Loyalty 75 Pride 40 Honesty 60 Piety 35 Cruelty 5 Humour 65 Ambition 55", "Family 1 Gang 2 Money 1 Honour 2 Law -1 God 1", "Drink 10 Company 65 Faith 30 Money 30 Violence 0", Q(), G(("Settle", null, "write her books, somewhere quiet")), (1876, 0, "born", "Born"), (1882, 0, "loss", "(about) Her mother died of typhoid; raised in an orphanage"), (1896, 0, "gang", "(about) Joined the gang - a fine pickpocket who'd rather be reading")),
			P("molly", "Molly O'Shea", "Dutch's girl", 1, 1873, "Ireland", Personality.Proud, Sociability.Outgoing, Disposition.Surly, 40, Smarts.Average, Wealth.Poor, "Outlaw", "Catholic", Devotion.Lapsed, "single", 0, "Anger 60 Bravery 40 Greed 35 Loyalty 55 Pride 80 Honesty 50 Piety 20 Cruelty 20 Humour 35 Ambition 50", "Family 1 Gang 1 Money 1 Honour 1 Law -1 God 0", "Drink 50 Company 80 Faith 15 Money 35 Violence 5", Q(), G(("Protect", "vdl_dutch", "keep Dutch's eye on her")), (1873, 0, "born", "Born in Ireland"), (1895, 0, "family", "Took up with Dutch van der Linde")),
			P("kieran", "Kieran Duffy", "the prisoner", 1, 1875, null, Personality.Coward, Sociability.Introvert, Disposition.Friendly, 20, Smarts.Average, Wealth.Poor, "Horseman", "Catholic", Devotion.Lapsed, "single", 0, "Anger 20 Bravery 30 Greed 20 Loyalty 50 Pride 25 Honesty 60 Piety 20 Cruelty 10 Humour 35 Ambition 15", "Family 0 Gang 1 Money 0 Honour 1 Law 0 God 0", "Drink 20 Company 60 Faith 15 Money 20 Violence 5", Q("superstitious"), G(("Settle", null, "be one of them - not tied to a tree")), (1875, 0, "born", "Born"), (1897, 0, "gang", "(about) Rode with Colm O'Driscoll's gang"), (1899, 3, "gang", "Captured by the gang in the Grizzlies"))
		};
		Tie("dutch", "hosea", "friend", 90, "his oldest friend");
		Tie("dutch", "arthur", "protege", 80, "raised him");
		Tie("dutch", "john", "protege", 60, "saved him from the rope");
		Tie("dutch", "micah", "protege", 60, "his new favourite");
		Tie("dutch", "molly", "friend", 40, "his woman");
		Tie("dutch", "grimshaw", "friend", 50, "his woman, once");
		Tie("hosea", "arthur", "protege", 85, "like a son");
		Tie("arthur", "john", "rival", -10, "brothers who don't always get on");
		Tie("arthur", "micah", "enemy", -60, "can't stand him");
		Tie("arthur", "charles", "friend", 60, "trusts him");
		Tie("arthur", "lenny", "friend", 65, "looks out for him");
		Tie("arthur", "marybeth", "friend", 55, "they read");
		Tie("john", "abigail", "kin", 60, "the mother of his son");
		Tie("john", "jack", "kin", 70, "his son");
		Tie("abigail", "jack", "kin", 95, "her son");
		Tie("uncle", "abigail", "friend", 50, "brought her in");
		Tie("javier", "dutch", "mentor", 85, "believes in him");
		Tie("bill", "dutch", "mentor", 80, "owes him everything");
		Tie("tilly", "dutch", "mentor", 70, "taught her to read");
		Tie("swanson", "dutch", "friend", 50, "saved his life once");
		Tie("trelawny", "dutch", "friend", 55, "old friends");
		Tie("grimshaw", "pearson", "friend", 40, "runs the camp together");
		Tie("sean", "kieran", "enemy", -45, "an O'Driscoll in camp");
		Tie("tilly", "marybeth", "friend", 70, "close");
		Tie("tilly", "karen", "friend", 65, "close");
		Tie("karen", "sean", "friend", 55, "drinking partners");
		Tie("lenny", "charles", "friend", 45, "rode in together");
		NemesisRecord nemesisRecord = list.First((NemesisRecord r) => r.Id == "vdl_jack");
		nemesisRecord.KinId = "vdl_john";
		nemesisRecord.KinRelation = "son";
		list.Add(Dead("davey", "Davey Callender", "a gun", 2, 1872, "Shot at Blackwater; died of his wounds in the snow at Colter", (1899, 2, "gang", "Wounded in the shooting at Blackwater"), (1899, 3, "death", "Died of his wounds at Colter, in the snow")));
		list.Add(Dead("mac", "Mac Callender", "a gun", 2, 1870, "Lost at Blackwater - left behind in the shooting", (1899, 2, "death", "Lost at Blackwater during the ferry job - left behind")));
		list.Add(Dead("jenny", "Jenny Kirk", "camp", 1, 1880, "Killed at Blackwater", (1899, 2, "death", "Killed in the shooting after the Blackwater ferry job")));
		list.Add(Dead("heidi", "Heidi McCourt", "camp", 1, 1871, "Killed at Blackwater", (1899, 2, "death", "Killed at Blackwater when the ferry job went wrong")));
		list.Add(Dead("annabelle", "Annabelle", "Dutch's woman", 1, 1862, "Killed by Colm O'Driscoll's men (about 1889)", (1889, 0, "death", "Killed by Colm O'Driscoll's men (about) - the start of the feud")));
		return list;
		void Tie(string a, string b, string type, int strength, string note)
		{
			NemesisRecord nemesisRecord2 = list.First((NemesisRecord r) => r.Id == "vdl_" + a);
			NemesisRecord nemesisRecord3 = list.First((NemesisRecord r) => r.Id == "vdl_" + b);
			nemesisRecord2.Ties.Add(new Tie
			{
				Id = nemesisRecord3.Id,
				Type = type,
				Strength = strength,
				Note = note
			});
			string type2 = ((type == "protege") ? "mentor" : ((type == "mentor") ? "protege" : type));
			nemesisRecord3.Ties.Add(new Tie
			{
				Id = nemesisRecord2.Id,
				Type = type2,
				Strength = strength,
				Note = note
			});
		}
	}

	private static NemesisRecord Dead(string key, string name, string title, int rank, int born, string how, params (int y, int m, string kind, string text)[] past)
	{
		return new NemesisRecord
		{
			Id = "vdl_" + key,
			Name = name,
			Title = title,
			Rank = rank,
			Gang = "vdl",
			IsAlive = false,
			Story = true,
			BornYear = born,
			PastRolled = true,
			Woven = true,
			MindRolled = true,
			QuirksRolled = true,
			Origin = GrudgeOrigin.GangMember,
			Hate = 0,
			Quirks = new List<string>(),
			Goals = new List<Goal>(),
			Past = past.Select(((int y, int m, string kind, string text) e) => new PastEntry
			{
				Year = e.y,
				Month = e.m,
				Kind = e.kind,
				Text = e.text,
				Public = true,
				Known = true
			}).ToList()
		};
	}
}
