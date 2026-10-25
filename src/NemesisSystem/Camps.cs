using System.Collections.Generic;
using System.Linq;
using RDR2.Math;

namespace NemesisSystem;

internal static class Camps
{
	public static readonly CampSite[] ODriscoll = new CampSite[30]
	{
		new CampSite("six_point_cabin", 0u, -43.4f, 1230.9f, 172.7f, "Cumberland Forest", hideout: true, "Six Point Cabin"),
		new CampSite("hanging_dog_ranch", 0u, -2197.1f, 700.3f, 121.5f, "Big Valley", hideout: true, "Hanging Dog Ranch"),
		new CampSite("gang_odriscollboys_bgv001", 4186667583u, -1118.4f, -220.2f, 91.2f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv002", 2285934507u, -980.4f, -212.6f, 90.2f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv003", 3472794918u, -2601.2f, -24.6f, 161.5f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv004", 3954957984u, -1602.5f, -893f, 86.5f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv005", 1122045165u, -2763.2f, 113.3f, 165.2f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv006", 1352837232u, -1448.7f, 101.4f, 91.6f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv007", 2554279848u, -1580.8f, 496.3f, 115.5f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv008", 2786710365u, -1345.1f, 492.8f, 97.3f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv009", 1739445890u, -1711.5f, 605.2f, 119.3f, "Big Valley"),
		new CampSite("gang_odriscollboys_bgv010", 3477907118u, -1622.6f, 822.5f, 153.1f, "Big Valley"),
		new CampSite("gang_odriscollboys_cml001", 1548242606u, -2.8f, 949.6f, 211.4f, "Cumberland Forest"),
		new CampSite("gang_odriscollboys_cml002", 3102214124u, 645.5f, 1298.7f, 197.1f, "Cumberland Forest"),
		new CampSite("gang_odriscollboys_cml003", 3812711582u, 102.4f, 1406.9f, 168.4f, "Cumberland Forest"),
		new CampSite("gang_odriscollboys_cml004", 4058053085u, 586.5f, 1431.2f, 185.7f, "Cumberland Forest"),
		new CampSite("gang_odriscollboys_cml005", 2130187273u, 497.3f, 1498.1f, 173.9f, "Cumberland Forest"),
		new CampSite("gang_odriscollboys_hrt001", 3007696601u, -647.6f, -391.5f, 80.3f, "the Heartlands"),
		new CampSite("gang_odoriscollboys_hrt002", 3358762491u, 499.1f, -259.1f, 144.8f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt003", 2409367430u, 649.7f, -111.8f, 150.6f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt004", 4176796214u, 1059.1f, 34.3f, 87.3f, "the Heartlands"),
		new CampSite("gang_odoriscollboys_hrt005", 2118488614u, -1172.4f, 94.5f, 45.1f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt006", 1726559781u, -961f, 296.2f, 96.1f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt007", 1455068616u, -645.9f, 277.5f, 91.5f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt008", 3225381068u, -191.8f, 294.7f, 97.5f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt009", 3606222394u, 182.8f, 344.3f, 121.4f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt010", 2301131135u, -849.9f, 465.8f, 91f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt011", 402954041u, 439.7f, 830f, 147.1f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt012", 616706228u, 275.6f, 835.1f, 191.2f, "the Heartlands"),
		new CampSite("gang_odriscollboys_hrt013", 3015134876u, 1000.1f, 991.4f, 152.3f, "the Heartlands")
	};

	public static readonly CampSite[] GameGangCamps = new CampSite[67]
	{
		new CampSite("gang_lemoyne_scm001", 1971923782u, 1173.2f, -2180.8f, 55.8f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm002", 401043460u, 1426.4f, -2185f, 50f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm003", 104287396u, 1562.1f, -2048.9f, 49f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm004", 1014446371u, 1705f, -2008.8f, 45.4f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm005", 715920781u, 1380.3f, -1696.1f, 68.3f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm006", 3394426076u, 1622.1f, -1649.4f, 57.7f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm007", 3095671103u, 1503f, -1470.5f, 73f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm008", 4004421011u, 868.5f, -1330.5f, 56.3f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm009", 3741777476u, 948f, -1318.5f, 59.8f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm010", 546408124u, 1083.4f, -1268.5f, 69.2f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm011", 1313890873u, 701.8f, -1173.6f, 48.6f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm012", 129717520u, 856.7f, -1134.3f, 59f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm013", 3062182565u, 683.4f, -992.4f, 54.1f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm014", 3812068361u, 911.2f, -986.3f, 58.6f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm015", 301558156u, 708.5f, -885.4f, 49.9f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm016", 1859330882u, 1334.9f, -594.7f, 76.2f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm017", 1683033662u, 879.3f, -538.3f, 89.7f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm018", 2875595879u, 961.2f, -572.9f, 90.9f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_lemoyne_scm019", 3651926258u, 1012.5f, -392.5f, 92.1f, "Scarlett Meadows", hideout: false, null, "lemoyneraiders"),
		new CampSite("gang_skinner_tal001", 3926071903u, -2014.5f, -1828.5f, 114f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("gang_skinner_tal002", 4189370818u, -2136.9f, -1783.6f, 139.3f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("gang_skinner_tal003", 190078209u, -2667.6f, -1462.4f, 146.2f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("gang_skinner_tal004", 561187138u, -2429.4f, -1329.6f, 151.9f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("gang_skinner_tal005", 859352269u, -2109.8f, -1275f, 123.7f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("gang_skinner_tal006", 1023328345u, -2033.1f, -1278.6f, 115.2f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("murfree_beaver_hollow", 0u, 2351.4f, 1358.2f, 106.9f, "Roanoke Ridge", hideout: true, "Beaver Hollow", "murfree"),
		new CampSite("murfree_ransack07", 0u, 2357.8f, 836.1f, 79.6f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("murfree_ransack11", 0u, 2299.3f, 1050.8f, 116.6f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("murfree_ransack12", 0u, 2517.6f, 1746.4f, 88.3f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("murfree_ransack13", 0u, 2792.8f, 2008.3f, 165.3f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("murfree_ransack01", 0u, 2119.9f, -139.3f, 50.3f, "Bluewater Marsh", hideout: false, null, "murfree"),
		new CampSite("murfree_mossy_flats", 0u, 2453.5f, 290.5f, 70.4f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("murfree_rocky_seven", 0u, 2996.8f, 2197.3f, 166.1f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("bronte_mansion", 0u, 2338.6f, -1204.4f, 44f, "Bayou Nwa", hideout: true, "the Bronte Mansion", "bronte"),
		new CampSite("bronte_docks", 0u, 2806.9f, -1417.9f, 43f, "Saint Denis", hideout: false, null, "bronte"),
		new CampSite("bronte_warehouses", 0u, 2574.1f, -1405.9f, 45f, "Saint Denis", hideout: false, null, "bronte"),
		new CampSite("placed_bronte_c43b22", 0u, 2367.1f, -1435.6f, 45.5f, "Saint Denis", hideout: false, null, "bronte"),
		new CampSite("bronte_slums", 0u, 2840f, -1229.3f, 47.6f, "Saint Denis", hideout: false, null, "bronte"),
		new CampSite("bronte_lakay", 0u, 2264.4f, -764.2f, 43.1f, "Bayou Nwa", hideout: true, "Lakay", "bronte"),
		new CampSite("bronte_boathouse", 0u, 2020.9f, -775.8f, 42.7f, "Bayou Nwa", hideout: false, null, "bronte"),
		new CampSite("bronte_copperhead", 0u, 2040.7f, -1639.4f, 41.3f, "Bayou Nwa", hideout: false, null, "bronte"),
		new CampSite("laramie_manteca", 0u, -1657f, -2760f, 71f, "Hennigan's Stead", hideout: true, "Manteca Falls", "laramie"),
		new CampSite("laramie_hen_shack", 0u, -1810.8f, -2393.1f, 41.5f, "Hennigan's Stead", hideout: false, null, "laramie"),
		new CampSite("laramie_hen_cabin", 0u, -1412.4f, -2671.5f, 43.4f, "Hennigan's Stead", hideout: false, null, "laramie"),
		new CampSite("laramie_quakers", 0u, -1199.9f, -1938.9f, 42.6f, "the Great Plains", hideout: false, null, "laramie"),
		new CampSite("delobo_gaptooth", 0u, -5990f, -3261f, -21f, "Gaptooth Ridge", hideout: true, "Gaptooth Breach", "delobo"),
		new CampSite("delobo_gap_shack", 0u, -5850.3f, -3743.9f, -25.9f, "Gaptooth Ridge", hideout: false, null, "delobo"),
		new CampSite("delobo_ben_shack", 0u, -5204.8f, -3490.9f, -23.4f, "Gaptooth Ridge", hideout: false, null, "delobo"),
		new CampSite("delobo_rio_camp", 0u, -5248.1f, -3851.5f, -2.1f, "Rio Bravo", hideout: false, null, "delobo"),
		new CampSite("delobo_cho_shack", 0u, -3400.6f, -3305.4f, -6.2f, "Cholla Springs", hideout: false, null, "delobo"),
		new CampSite("delobo_cho_shack2", 0u, -3552.2f, -3009.9f, 10.1f, "Cholla Springs", hideout: false, null, "delobo"),
		new CampSite("delobo_cho_rocks", 0u, -3956.8f, -2124.6f, -5.8f, "Cholla Springs", hideout: false, null, "delobo"),
		new CampSite("nightfolk_shack", 0u, 1707.4f, -1005.4f, 40.2f, "Bayou Nwa", hideout: false, null, "nightfolk"),
		new CampSite("nightfolk_shady", 0u, 1870f, -1853f, 43f, "Bayou Nwa", hideout: false, null, "nightfolk"),
		new CampSite("placed_delobo_1d0d56", 0u, -4350.9f, -2950.6f, -14.5f, "Cholla Springs", hideout: false, null, "delobo"),
		new CampSite("placed_delobo_03907f", 0u, -3835.7f, -3150.5f, -12.4f, "Cholla Springs", hideout: false, null, "delobo"),
		new CampSite("placed_delobo_80f73c", 0u, -5600.3f, -3306.9f, -21.5f, "Gaptooth Ridge", hideout: false, null, "delobo"),
		new CampSite("placed_laramie_a7c059", 0u, -1949.7f, -2993.8f, -4.4f, "Hennigan's Stead", hideout: false, null, "laramie"),
		new CampSite("placed_skinners_4fa891", 0u, -1545f, -1958.1f, 47.7f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("placed_skinners_7ac923", 0u, -2236f, -1291.2f, 134.9f, "Tall Trees", hideout: false, null, "skinners"),
		new CampSite("placed_murfree_db3b49", 0u, 2476.3f, 1756.8f, 87.5f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("placed_murfree_70f4d7", 0u, 3244.3f, 1488.8f, 51.7f, "Roanoke Ridge", hideout: false, null, "murfree"),
		new CampSite("placed_lemoyneraiders_c2958d", 0u, 2172.3f, -399.5f, 42.4f, "Bluewater Marsh", hideout: false, null, "lemoyneraiders"),
		new CampSite("placed_lemoyneraiders_701490", 0u, 1053.3f, 1248.7f, 201.2f, "the Grizzlies", hideout: false, null, "lemoyneraiders"),
		new CampSite("wapiti_hunting_camp", 0u, 712.7f, 1882.3f, 239.1f, "Grizzlies East", hideout: false, null, "wapiti"),
		new CampSite("wapiti_lowland_camp", 0u, 794f, 1511f, 204f, "Grizzlies East", hideout: false, null, "wapiti"),
		new CampSite("wapiti_ridge", 0u, 992.4f, 1452.6f, 276f, "Grizzlies East", hideout: false, null, "wapiti")
	};

	public static List<CampSite> Extra = new List<CampSite>();

	public static readonly CampSite[] Retired = new CampSite[1]
	{
		new CampSite("wapiti_village", 0u, 440.8f, 2229.5f, 247.8f, "Grizzlies East", hideout: true, "the Wapiti Reservation", "wapiti")
	};

	public static readonly CampSite[] Removed = new CampSite[2]
	{
		new CampSite("bronte_railyard", 0u, 2327.5f, -1502.4f, 46.2f, "Saint Denis", hideout: false, "the Bronte rail yard camp in Saint Denis", "bronte"),
		new CampSite("lemoyneraiders_556783", 0u, 2356.25f, -1422.886f, 45.53f, "Saint Denis", hideout: false, "the Raiders' camp in Saint Denis", "lemoyneraiders")
	};

	public const float RetiredCalmRadius = 300f;

	public static IEnumerable<CampSite> AllSites => ODriscoll.Concat(GameGangCamps).Concat(Extra);

	public static string RetiredName(string id)
	{
		if (id != null)
		{
			return Retired.Concat(Removed).FirstOrDefault((CampSite s) => s.Id == id)?.Name;
		}
		return null;
	}

	public static CampSite RetiredNear(Vector3 p, float radius)
	{
		return Retired.FirstOrDefault((CampSite s) => new Vector3(p.X - s.X, p.Y - s.Y, 0f).Length() < radius);
	}

	public static CampSite[] ForGang(string gangId)
	{
		return ((gangId == "odriscoll") ? ODriscoll : new CampSite[0]).Concat(GameGangCamps.Where((CampSite c) => c.Gang == gangId)).Concat(Extra.Where((CampSite c) => c.Gang == gangId)).ToArray();
	}

	public static string StartingOwner(CampSite s)
	{
		object obj;
		if (s != null)
		{
			obj = s.Gang;
			if (obj == null)
			{
				return "odriscoll";
			}
		}
		else
		{
			obj = null;
		}
		return (string)obj;
	}

	public static CampSite ById(string id)
	{
		object obj;
		if (id != null)
		{
			obj = ODriscoll.FirstOrDefault((CampSite c) => c.Id == id);
			if (obj == null)
			{
				obj = GameGangCamps.FirstOrDefault((CampSite c) => c.Id == id);
				if (obj == null)
				{
					return Extra.FirstOrDefault((CampSite c) => c.Id == id);
				}
			}
		}
		else
		{
			obj = null;
		}
		return (CampSite)obj;
	}
}
