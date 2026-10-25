using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class TownCast
{
	public static readonly Dictionary<string, string> KnownNames = new Dictionary<string, string>
	{
		["u_m_m_bht_benedictallbright"] = "Benedict Allbright",
		["u_m_m_bht_nathankirk"] = "Nathan Kirk",
		["u_m_m_bht_laramiesleeping"] = "Bart Cavanaugh",
		["u_m_m_bht_strawberryduel"] = "Joshua Brown",
		["u_f_m_bht_wife"] = "Ellie Anne Swan",
		["u_m_m_bht_lover"] = "Ellie Anne Swan's lover",
		["u_m_m_bht_exconfedcampreturn"] = "Camille de Millemont",
		["u_m_o_bht_docwormwood"] = "J.P. \"Doc\" Wormwood",
		["u_m_y_duellistbounty_01"] = "Joshua Brown",
		["u_m_m_bht_outlawmauled"] = "Jim McGoGarty",
		["u_m_m_bht_skinnersearch"] = "a wanted man",
		["u_m_m_bht_blackwaterhunt"] = "a wanted man",
		["u_m_m_bht_shackescape"] = "a wanted man",
		["u_m_m_bht_banditomine"] = "a wanted bandito",
		["u_m_m_bht_banditoshack"] = "a wanted bandito",
		["u_m_m_bht_mineforeman"] = "a wanted mine foreman",
		["u_m_m_bht_saintdenissaloon"] = "a wanted man",
		["u_m_m_bht_oldman"] = "a wanted old man",
		["u_m_m_htlrancherbounty_01"] = "a wanted rancher",
		["u_m_m_nbxbankerbounty_01"] = "a wanted banker",
		["u_m_m_emrfather_01"] = "Eugene Wegner",
		["u_f_m_emrdaughter_01"] = "Miriam Wegner",
		["u_m_y_emrson_01"] = "Seamus",
		["u_m_m_chelonianjumper_01"] = "a Chelonian",
		["u_m_m_chelonianjumper_02"] = "a Chelonian",
		["u_m_m_chelonianjumper_03"] = "a Chelonian",
		["u_m_m_chelonianjumper_04"] = "a Chelonian",
		["u_m_m_story_chelonian_01"] = "a Chelonian"
	};

	private static readonly (string token, string town)[] Towns = new(string, string)[15]
	{
		("val", "Valentine"),
		("rhd", "Rhodes"),
		("rho", "Rhodes"),
		("nbx", "Saint Denis"),
		("sd", "Saint Denis"),
		("str", "Strawberry"),
		("blw", "Blackwater"),
		("asb", "Annesburg"),
		("vht", "Van Horn"),
		("arm", "Armadillo"),
		("tum", "Tumbleweed"),
		("wal", "Wallace Station"),
		("emr", "Emerald Ranch"),
		("rig", "Riggs Station"),
		("bwm", "Bluewater Marsh")
	};

	private static readonly (string token, string job)[] Jobs = new(string, string)[32]
	{
		("generalstoreowner", "general store"),
		("genstoreowner", "general store"),
		("trainstationworker", "station agent"),
		("freightstationowner", "freight station"),
		("exoticsshopkeeper", "exotics shop"),
		("exoticshopkeeper", "exotics shop"),
		("boatticketseller", "ticket seller"),
		("welcomecenter", "welcome centre"),
		("auctionforman", "auctioneer"),
		("hotelowner", "hotel owner"),
		("liveryworker", "stable hand"),
		("stablehand", "stable hand"),
		("policechief", "police chief"),
		("bankguard", "bank guard"),
		("stationclerk", "station clerk"),
		("doctor", "doctor"),
		("gunsmith", "gunsmith"),
		("butcher", "butcher"),
		("barber", "barber"),
		("bartender", "bartender"),
		("sherriff", "sheriff"),
		("sheriff", "sheriff"),
		("undertaker", "undertaker"),
		("preacher", "preacher"),
		("priest", "priest"),
		("photographer", "photographer"),
		("fiddleplayer", "fiddler"),
		("pokerplayer", "card player"),
		("backupdeputy", "deputy"),
		("deputy", "deputy"),
		("customvendor", "tailor"),
		("trapper", "trapper")
	};

	public static string NameFor(string modelLower)
	{
		if (string.IsNullOrEmpty(modelLower))
		{
			return null;
		}
		if (KnownNames.TryGetValue(modelLower, out var value))
		{
			return value;
		}
		string[] array = modelLower.Split('_');
		string core;
		if (array[0] == "u" && array.Length >= 4)
		{
			core = array[3];
		}
		else
		{
			if (!(array[0] == "cs") || array.Length < 2)
			{
				return null;
			}
			core = array[1];
		}
		(string, string) tuple = Jobs.FirstOrDefault(((string token, string job) j) => core.Contains(j.token));
		if (tuple.Item1 == null)
		{
			return null;
		}
		(string, string) tuple2 = Towns.FirstOrDefault(((string token, string town) t) => core.StartsWith(t.token));
		string text = ((tuple2.Item1 != null) ? (tuple2.Item2 + " ") : "") + tuple.Item2;
		return char.ToUpperInvariant(text[0]) + text.Substring(1);
	}
}
