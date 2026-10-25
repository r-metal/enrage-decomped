using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class WelcomePics
{
	private static (int r, int g, int b) RealGold => (r: 212, g: 176, b: 82);

	public static void Draw(int page, float x, float y, float w, float h)
	{
		switch (page)
		{
		case 0:
			Factions(x, y, w, h);
			break;
		case 1:
			Menu(x, y, w, h);
			break;
		case 2:
			Grudge(x, y, w, h);
			break;
		case 3:
			Tempers(x, y, w, h);
			break;
		case 4:
			Hierarchy(x, y, w, h);
			break;
		case 5:
			Tension(x, y, w, h);
			break;
		case 6:
			Interrogate(x, y, w, h);
			break;
		case 7:
			Camps(x, y, w, h);
			break;
		case 8:
			World(x, y, w, h);
			break;
		case 9:
			NameCard(x, y, w, h);
			break;
		case 10:
			XRay(x, y, w, h);
			break;
		case 11:
			Wordmark(x, y, w, h);
			break;
		}
	}

	private static void TitleC(string s, float cx, float y, float sc, (int r, int g, int b) c)
	{
		UiDraw.TextFont(s, UiDraw.TitleFont, cx - Theme.TitleWidth(s, sc) / 2f, y, sc, c.r, c.g, c.b);
	}

	private static void BodyC(string s, float cx, float y, float sc, (int r, int g, int b) c)
	{
		UiDraw.Text(s, cx - Theme.MixedWidth(s, sc, 0.0222f) / 2f, y, sc, c.r, c.g, c.b);
	}

	private static void Bar(float x, float y, float w, float h, float frac, (int r, int g, int b) c)
	{
		UiDraw.Rect(x, y, w, h, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 120);
		UiDraw.Rect(x, y, w * Math.Max(0f, Math.Min(1f, frac)), h, c.r, c.g, c.b, 235);
	}

	private static void Tick(float x, float y, float h, (int r, int g, int b) c)
	{
		UiDraw.Rect(x - 0.00052083336f, y - 0.004f, 0.0010416667f, h + 0.008f, c.r, c.g, c.b, 255);
	}

	private static void Stars(float x, float cy, int rank, float sh)
	{
		float num = sh / UiDraw.Aspect;
		bool flag = UiDraw.EnsureTextureDict("generic_textures");
		for (int i = 0; i < 5; i++)
		{
			float num2 = x + (float)i * (num + 0.0008f) + num / 2f;
			bool flag2 = i < rank;
			if (flag)
			{
				UiDraw.Sprite("generic_textures", "star", num2, cy, num, sh, 0f, flag2 ? Theme.Gold.r : 78, flag2 ? Theme.Gold.g : 75, flag2 ? Theme.Gold.b : 70, flag2 ? 255 : 235);
			}
			else
			{
				UiDraw.Rect(num2 - num * 0.3f, cy - sh * 0.3f, num * 0.6f, sh * 0.6f, flag2 ? Theme.Gold.r : 70, flag2 ? Theme.Gold.g : 68, flag2 ? Theme.Gold.b : 64, 230);
			}
		}
	}

	private static void Card(float x, float y, float w, float h, string name, int rank, (int r, int g, int b) hate, bool selected = false)
	{
		UiDraw.Rect(x, y, w, h, 24, 23, 21, 235);
		Theme.Hairline(x, y, w, h, hate.r, hate.g, hate.b, 255, 2);
		if (selected)
		{
			Theme.RedFrame(x, y, w, h, 0.95f, 5);
		}
		UiDraw.TextFont(name, UiDraw.TitleFont, x + 0.005f, y + 0.004f, 0.18f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		if (rank > 0)
		{
			Stars(x + w - 0.005f - 5f * (0.0115f / UiDraw.Aspect + 0.0008f), y + h - 0.009f, rank, 0.0115f);
		}
	}

	private static void Factions(float x, float y, float w, float h)
	{
		List<Gang> list = (from g in new string[8] { "odriscoll", "lemoyneraiders", "army", "wapiti", "bronte", "klan", "murfree", "skinners" }.Select(Gangs.ById)
			where g != null
			select g).ToList();
		TitleC("EIGHT OUTFITS - EACH WITH ITS OWN RANKS, CAMPS, LEADER AND WARS", x + w / 2f, y + 0.012f, 0.22f, Theme.Dim);
		float num = w / 4f;
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			Gang gang = list[num2];
			float num3 = x + num * (float)(num2 % 4) + num / 2f;
			float num4 = y + 0.05f + (float)(num2 / 4) * 0.042f;
			string s = gang.Name.ToUpperInvariant();
			TitleC(s, num3, num4, 0.25f, Theme.Title);
			float num5 = Theme.TitleWidth(s, 0.25f);
			UiDraw.Rect(num3 - num5 / 2f, num4 + 0.024f, num5, 0.0027777776f, gang.Accent.r, gang.Accent.g, gang.Accent.b, 255);
		}
	}

	private static void Menu(float x, float y, float w, float h)
	{
		float num = x + 0.03f;
		float num2 = y + 0.03f;
		string[] array = new string[3] { "HIERARCHY", "WORLD", "X-RAY LOG" };
		for (int i = 0; i < array.Length; i++)
		{
			(int, int, int) tuple = ((i == 0) ? Theme.Title : Theme.Dim);
			UiDraw.TextFont(array[i], UiDraw.TitleFont, num, num2, 0.34f, tuple.Item1, tuple.Item2, tuple.Item3, 255, i == 0);
			num += Theme.CapsWidth(array[i], 0.34f) + 0.03f;
		}
		BodyC("one menu, three pages - Tab / View flips between them", x + 0.03f + (num - x - 0.06f) / 2f, num2 + 0.05f, 0.22f, Theme.Dim);
		bool recentlyUsed = XPad.RecentlyUsed;
		List<(string, string)> hints = (recentlyUsed ? new List<(string, string)>
		{
			KeyCombo.PadOpenHint,
			("View", "Tabs"),
			("LB/RB", "Outfits"),
			("A", "His page"),
			("B", "Back")
		} : new List<(string, string)>
		{
			(KeyCombo.MenuText, "Open"),
			("Tab", "Tabs"),
			("Q/E", "Outfits"),
			("Enter", "His page"),
			("Backspace", "Back")
		});
		Theme.HintRow(x + w * 0.52f, x + w - 0.02f, y + 0.04f, hints, 0.23f, Theme.Text);
		List<(string, string)> hints2 = (recentlyUsed ? new List<(string, string)>
		{
			("LS", "Messages"),
			("RS", "Name Cards"),
			("Y", "This Guide"),
			("Start", "Settings")
		} : new List<(string, string)>
		{
			("N", "Messages"),
			("C", "Name Cards"),
			("G", "This Guide"),
			("O", "Settings")
		});
		Theme.HintRow(x + w * 0.52f, x + w - 0.02f, y + 0.085f, hints2, 0.23f, Theme.Text);
	}

	private static void Grudge(float x, float y, float w, float h)
	{
		float num = x + 0.03f;
		float num2 = w * 0.46f;
		float num3 = y + 0.05f;
		UiDraw.TextFont("A STRANGER'S GRUDGE", UiDraw.TitleFont, num, y + 0.014f, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		Bar(num, num3, num2, 0.01f, 0.72f, Theme.Blood);
		Tick(num + num2 * 0.25f, num3, 0.01f, Theme.Dim);
		Tick(num + num2 * 0.6f, num3, 0.01f, RealGold);
		UiDraw.Text("he might let it go", num + num2 * 0.25f - 0.004f, num3 + 0.016f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		UiDraw.Text("nemesis", num + num2 * 0.6f - 0.004f, num3 + 0.016f, 0.19f, RealGold.r, RealGold.g, RealGold.b);
		float num4 = num;
		(string, (int, int, int))[] array = new(string, (int, int, int))[4]
		{
			("+15 ANTAGONIZED", Theme.Warn),
			("+45 ROBBED", Theme.Blood),
			("+8 PUNCHED", Theme.Warn),
			("-6 GREETED", Theme.Good)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, (int, int, int)) tuple = array[i];
			string item = tuple.Item1;
			(int, int, int) item2 = tuple.Item2;
			num4 += Theme.Tag(item, num4, num3 + 0.05f, item2) + 0.008f;
		}
		float num5 = x + w * 0.56f;
		float num6 = w * 0.4f / 3f - 0.008f;
		UiDraw.TextFont("HIS HATE", UiDraw.TitleFont, num5, y + 0.014f, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		(string, HateTier, string)[] array2 = new(string, HateTier, string)[3]
		{
			("RESENTFUL", HateTier.Resentful, "fists"),
			("VENGEFUL", HateTier.Vengeful, "knife, maybe a gun"),
			("MURDEROUS", HateTier.Murderous, "wants you dead")
		};
		for (int j = 0; j < 3; j++)
		{
			float num7 = num5 + (float)j * (num6 + 0.012f);
			Theme.Badge(array2[j].Item1, num7, y + 0.046f, num6, UiDraw.HateColor(array2[j].Item2), filled: true);
			BodyC(array2[j].Item3, num7 + num6 / 2f, y + 0.08f, 0.2f, Theme.Dim);
		}
	}

	private static void Tempers(float x, float y, float w, float h)
	{
		Personality[] array = new Personality[7]
		{
			Personality.Hothead,
			Personality.Proud,
			Personality.Cold,
			Personality.Coward,
			Personality.Greedy,
			Personality.Loyal,
			Personality.Drifter
		};
		float num = w / (float)array.Length;
		for (int i = 0; i < array.Length; i++)
		{
			(int, int, int) c = NemesisMod.TemperColor(array[i]);
			float num2 = x + num * (float)i + num / 2f;
			float w2 = 0.032f / UiDraw.Aspect;
			float num3 = y + (h - 0.060000002f) / 2f;
			if (UiDraw.EnsureTextureDict("itemtype_textures"))
			{
				UiDraw.Sprite("itemtype_textures", Theme.TemperIcon(array[i]), num2 + 0.0015f, num3 + 0.016f, w2, 0.032f, 0f, c.Item1, c.Item2, c.Item3, 255);
			}
			TitleC(array[i].ToString().ToUpperInvariant(), num2, num3 + 0.032f + 0.01f - 0.004f, 0.24f, c);
		}
	}

	private static void Hierarchy(float x, float y, float w, float h)
	{
		float num = 0.09f;
		float num2 = 0.032f;
		float num3 = x + w * 0.3f;
		(int, int, int) tuple = UiDraw.HateColor(HateTier.Murderous);
		(int, int, int) tuple2 = UiDraw.HateColor(HateTier.Vengeful);
		(int, int, int) tuple3 = UiDraw.HateColor(HateTier.Resentful);
		float num4 = y + 0.012f;
		UiDraw.Rect(num3 - num / 2f, num4, num, num2, 24, 23, 21, 235);
		Theme.Hairline(num3 - num / 2f, num4, num, num2, RealGold.r, RealGold.g, RealGold.b, 255, 2);
		UiDraw.TextFont("SEAT OPEN", UiDraw.TitleFont, num3 - Theme.CapsWidth("SEAT OPEN", 0.18f) / 2f, num4 + 0.008f, 0.18f, Theme.Warn.r, Theme.Warn.g, Theme.Warn.b);
		float num5 = num4 + num2 + 0.012f;
		float y2 = num5 + num2 + 0.012f;
		UiDraw.Rect(num3 - 0.00052083336f, num4 + num2, 0.0010416667f, 0.008f, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 160);
		UiDraw.Rect(num3 - num * 0.6f, num4 + num2 + 0.008f, num * 1.2f, 0.0009259259f, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 160);
		Card(num3 - num * 1.1f, num5, num, num2, "Maj. Stockton", 4, tuple, selected: true);
		Card(num3 + num * 0.1f, num5, num, num2, "Capt. Rourke", 4, tuple);
		float num6 = num3 - 2f * num - 0.012f;
		UiDraw.Rect(num6 + num / 2f, num5 + num2 + 0.008f, 3f * num + 0.036f, 0.0009259259f, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 160);
		string[] array = new string[4] { "Sgt. Denton", "Sgt. Dillard", "Sgt. Eatlin", "Sgt. Ketchum" };
		(int, int, int)[] array2 = new(int, int, int)[4] { tuple, tuple2, tuple2, tuple3 };
		int[] array3 = new int[4] { 3, 3, 2, 2 };
		for (int i = 0; i < 4; i++)
		{
			Card(num6 + (float)i * (num + 0.012f), y2, num, num2, array[i], array3[i], array2[i]);
		}
		float num7 = x + w * 0.62f;
		float num8 = w * 0.34f;
		float num9 = y + 0.04f;
		UiDraw.TextFont("NOTORIETY", UiDraw.TitleFont, num7, num9 - 0.026f, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		int[] array4 = new int[5] { 10, 15, 25, 25, 25 };
		float num10 = num7;
		for (int j = 0; j < 5; j++)
		{
			float num11 = num8 * (float)array4[j] / 100f - 0.002f;
			(int, int, int) tuple4 = Notoriety.LevelColors[j];
			UiDraw.Rect(num10, num9, num11, 0.01f, tuple4.Item1, tuple4.Item2, tuple4.Item3, (j <= 3) ? 235 : 90);
			num10 += num11 + 0.002f;
		}
		_ = new string[5] { "Unknown", "Noticed", "Wanted", "Hunted", "Blood Feud" };
		BodyC("Unknown - Noticed - Wanted - Hunted - Blood Feud", num7 + num8 / 2f, num9 + 0.02f, 0.19f, Theme.Dim);
		BodyC("kill their men and they learn your name", num7 + num8 / 2f, num9 + 0.05f, 0.2f, Theme.Text);
	}

	private static void Tension(float x, float y, float w, float h)
	{
		float num = x + 0.03f;
		float num2 = w * 0.5f;
		float num3 = y + 0.022f;
		float num4 = 0.09f;
		float num5 = 0.7f;
		UiDraw.TextFont("TENSION", UiDraw.TitleFont, num, y + 0.006f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		int num6 = 34;
		float num7 = num2 / (float)num6;
		for (int i = 0; i < num6; i++)
		{
			float num8 = ((i < 24) ? (0.18f + (float)i * 0.023f) : ((i == 24) ? 0.74f : (0.3f + (float)(i - 25) * 0.02f)));
			(int, int, int) tuple = ((num8 >= num5) ? Theme.Blood : ((num8 >= num5 * 0.75f) ? Theme.Warn : Theme.Faint));
			float num9 = num4 * num8;
			UiDraw.Rect(num + (float)i * num7, num3 + num4 - num9, num7 - 0.0012f, num9, tuple.Item1, tuple.Item2, tuple.Item3, 230);
		}
		UiDraw.Rect(num, num3 + num4 * (1f - num5), num2, 0.0018518518f, RealGold.r, RealGold.g, RealGold.b, 255);
		UiDraw.Text("someone turns up", num + num7 * 22f, num3 + num4 * (1f - num5) - 0.024f, 0.19f, RealGold.r, RealGold.g, RealGold.b);
		float num10 = x + w * 0.6f;
		float num11 = y + 0.026f;
		UiDraw.TextFont("THE STANDOFF", UiDraw.TitleFont, num10, num11 - 0.016f, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		Theme.Tag("GREET", num10, num11 + 0.016f, Theme.Good);
		UiDraw.Text("he may back down", num10 + 0.08f, num11 + 0.018f, 0.21f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		Theme.Tag("ANTAGONIZE", num10, num11 + 0.046f, Theme.Blood);
		UiDraw.Text("it starts", num10 + 0.08f, num11 + 0.048f, 0.21f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		Theme.Tag("DRAW / SHOOT", num10, num11 + 0.076f, Theme.Blood);
		UiDraw.Text("it starts", num10 + 0.08f, num11 + 0.078f, 0.21f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
	}

	private static void Interrogate(float x, float y, float w, float h)
	{
		float num = x + 0.04f;
		float num2 = y + 0.022f;
		float num3 = 0.2f;
		UiDraw.Rect(num, num2, num3, 0.098f, 0, 0, 0, 170);
		string[] array = new string[3] { "Pick Up", "Cut Free", "Interrogate" };
		string[] array2 = new string[3] { "DLeft", "DRight", "DUp" };
		for (int i = 0; i < 3; i++)
		{
			float num4 = num2 + 0.01f + (float)i * 0.026f;
			(int, int, int) tuple = ((i == 2) ? Theme.Title : Theme.Dim);
			UiDraw.TextFont(array[i], UiDraw.TitleFont, num + 0.012f, num4, 0.22f, tuple.Item1, tuple.Item2, tuple.Item3);
			Theme.Glyph(array2[i], num + num3 - 0.012f - Theme.GlyphWidth(array2[i]), num4 - 0.001f);
		}
		UiDraw.Rect(num + 0.008f, num2 + 0.084f, num3 - 0.016f, 0.0009259259f, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 170);
		UiDraw.Text("D-pad Up - or I on the keyboard", num, num2 + 0.104f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		float num5 = x + w * 0.42f;
		float num6 = w * 0.52f;
		float num7 = y + 0.046f;
		UiDraw.TextFont("FEAR", UiDraw.TitleFont, num5, num7 - 0.03f, 0.24f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		Bar(num5, num7, num6, 0.008f, 0.58f, Theme.Warn);
		Tick(num5 + num6 * 0.45f, num7, 0.008f, RealGold);
		UiDraw.Text("scared enough to talk", num5 + num6 * 0.45f - 0.004f, num7 + 0.014f, 0.19f, RealGold.r, RealGold.g, RealGold.b);
		float num8 = num5;
		(string, (int, int, int))[] array3 = new(string, (int, int, int))[4]
		{
			("SHOT NEAR HIM", Theme.Warn),
			("HIT", Theme.Gold),
			("GUN ON HIM", Theme.Dim),
			("SAW A KILL", Theme.Blood)
		};
		for (int j = 0; j < array3.Length; j++)
		{
			(string, (int, int, int)) tuple2 = array3[j];
			string item = tuple2.Item1;
			(int, int, int) item2 = tuple2.Item2;
			num8 += Theme.Tag(item, num8, num7 + 0.046f, item2) + 0.008f;
		}
	}

	private static void Camps(float x, float y, float w, float h)
	{
		bool flag = UiDraw.EnsureTextureDict("blips");
		(string, string, string)[] array = new(string, string, string)[2]
		{
			("blip_region_hideout", "A CAMP OR HIDEOUT", "learned by interrogation"),
			("blip_ambient_secret", "A BATTLEFIELD", "the dead - and survivors")
		};
		for (int i = 0; i < array.Length; i++)
		{
			float num = x + 0.04f;
			float num2 = y + 0.022f + (float)i * 0.052f;
			float num3 = 0.036f;
			if (flag)
			{
				UiDraw.Sprite("blips", array[i].Item1, num + num3 / UiDraw.Aspect / 2f, num2 + num3 / 2f, num3 / UiDraw.Aspect, num3, 0f, 235, 232, 225, 255);
			}
			else
			{
				Theme.Disc(num + num3 / UiDraw.Aspect / 2f, num2 + num3 / 2f, num3 * 0.7f, 200, 60, 50, 255);
			}
			UiDraw.TextFont(array[i].Item2, UiDraw.TitleFont, num + 0.036f, num2 + 0.002f, 0.22f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
			UiDraw.Text(array[i].Item3, num + 0.036f, num2 + 0.02f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		float num4 = x + w * 0.44f;
		float num5 = w * 0.5f;
		float num6 = y + 0.05f;
		UiDraw.TextFont("SUSPICION", UiDraw.TitleFont, num4, num6 - 0.03f, 0.24f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		Bar(num4, num6, num5, 0.008f, 0.38f, Theme.Warn);
		Tick(num4 + num5 * 0.45f, num6, 0.008f, Theme.Warn);
		Tick(num4 + num5, num6, 0.008f, Theme.Blood);
		UiDraw.Text("a man calls out", num4 + num5 * 0.45f - 0.03f, num6 + 0.014f, 0.19f, Theme.Warn.r, Theme.Warn.g, Theme.Warn.b);
		UiDraw.Text("alerted", num4 + num5 - 0.03f, num6 + 0.014f, 0.19f, Theme.Blood.r, Theme.Blood.g, Theme.Blood.b);
		BodyC("crouch, come at night or in fog, stay out of sight", num4 + num5 / 2f, num6 + 0.048f, 0.21f, Theme.Text);
	}

	private static void World(float x, float y, float w, float h)
	{
		float num = x + 0.03f;
		float num2 = w * 0.56f;
		float num3 = y + 0.016f;
		(string, string, bool)[] array = new(string, string, bool)[3]
		{
			("Feb 23 18:53", "You killed Pvt. Atwood, Pvt. Spurlock  -  2 dead", false),
			("Feb 23 18:07", "Col. Henry Favours was killed at Fort Wallace  -  1 dead", true),
			("Feb 23 16:40", "The Murfree Brood hit the O'Driscoll Boys outside Annesburg", false)
		};
		for (int i = 0; i < array.Length; i++)
		{
			float num4 = num3 + (float)i * 0.03f;
			UiDraw.Rect(num, num4, 0.0015625001f, 0.024f, RealGold.r, RealGold.g, RealGold.b, 230);
			if (array[i].Item3)
			{
				Theme.SelectFill(num + 0.004f, num4, num2 - 0.004f, 0.024f);
				Theme.RedFrame(num + 0.004f, num4, num2 - 0.004f, 0.024f, 0.9f, 3);
			}
			UiDraw.Text(array[i].Item1, num + 0.012f, num4 + 0.004f, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			UiDraw.Text(array[i].Item2, num + 0.09f, num4 + 0.004f, 0.2f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		}
		float num5 = x + w * 0.62f;
		float num6 = w * 0.34f;
		TitleC("AT WAR", num5 + num6 / 2f, y + 0.02f, 0.26f, Theme.Warn);
		BodyC("Bayou Nwa: Lemoyne Raiders vs Bronte Family", num5 + num6 / 2f, y + 0.052f, 0.2f, Theme.Warn);
		TitleC("?", num5 + num6 / 2f, y + 0.074f, 0.4f, Theme.Title);
	}

	private static void NameCard(float x, float y, float w, float h)
	{
		float num = x + w * 0.27f;
		TitleC("Silas Boone", num, y + 0.016f, 0.3f, Theme.Title);
		TitleC("Hothead - Valentine", num, y + 0.046f, 0.22f, Theme.Dim);
		float num2 = 0.12f;
		float num3 = num - num2 / 2f;
		UiDraw.Text("FEAR", num3 - 0.04f, y + 0.074f, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		Bar(num3, y + 0.079f, num2, 0.006f, 0.35f, (r: 110, g: 150, b: 205));
		UiDraw.Text("LIKES", num3 - 0.04f, y + 0.092f, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		Bar(num3, y + 0.097f, num2, 0.006f, 0.6f, Theme.Good);
		float num4 = x + w * 0.58f;
		UiDraw.TextFont("TOWN REPUTATION", UiDraw.TitleFont, num4, y + 0.014f, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		(string, string, (int, int, int))[] array = new(string, string, (int, int, int))[3]
		{
			("Valentine", "+34  Liked", Theme.Good),
			("Rhodes", "+5  Known", Theme.Text),
			("Saint Denis", "-61  Hated", Theme.Blood)
		};
		for (int i = 0; i < array.Length; i++)
		{
			float y2 = y + 0.042f + (float)i * 0.026f;
			UiDraw.TextFont(array[i].Item1, UiDraw.TitleFont, num4, y2, 0.22f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			UiDraw.TextFont(array[i].Item2, UiDraw.TitleFont, num4 + 0.12f, y2, 0.22f, array[i].Item3.Item1, array[i].Item3.Item2, array[i].Item3.Item3);
		}
	}

	private static void XRay(float x, float y, float w, float h)
	{
		float cx = x + w * 0.2f;
		float top = y + 0.012f;
		float s = 0.11f;
		(int, int, int) bone = (196, 196, 190);
		float a = UiDraw.Aspect;
		Theme.Disc(X(0f), Y(0.06f), 0.02f, bone.Item1, bone.Item2, bone.Item3, 255);
		Theme.Disc(X(0f), Y(0.06f), 0.012f, 12, 12, 11, 255);
		L(0f, 0.11f, 0f, 0.52f);
		for (int i = 0; i < 5; i++)
		{
			float num = 0.2f + (float)i * 0.045f;
			L(0f, num, -0.11f + (float)i * 0.006f, num + 0.025f);
			L(0f, num, 0.11f - (float)i * 0.006f, num + 0.025f);
		}
		L(-0.13f, 0.16f, 0.13f, 0.16f);
		L(-0.13f, 0.16f, -0.17f, 0.36f);
		L(-0.17f, 0.36f, -0.19f, 0.54f);
		L(0.13f, 0.16f, 0.17f, 0.36f);
		L(0.17f, 0.36f, 0.19f, 0.54f);
		L(-0.08f, 0.52f, 0.08f, 0.52f);
		L(-0.07f, 0.52f, -0.08f, 0.76f);
		L(-0.08f, 0.76f, -0.08f, 0.99f);
		L(0.07f, 0.52f, 0.08f, 0.76f);
		L(0.08f, 0.76f, 0.08f, 0.99f);
		UiDraw.Line(X(-0.32f), Y(0.22f), X(-0.05f), Y(0.29f), 0.0012f, 150, 60, 50, 160);
		UiDraw.Line(X(-0.05f), Y(0.29f), X(0.1f), Y(0.33f), 0.0024f, 215, 50, 40);
		Theme.Disc(X(-0.05f), Y(0.29f), 0.01f, 110, 160, 225, 255);
		Theme.Disc(X(0.1f), Y(0.33f), 0.01f, RealGold.r, RealGold.g, RealGold.b, 255);
		float num2 = x + w * 0.42f;
		float num3 = y + 0.016f;
		UiDraw.TextFont("ALL INJURIES", UiDraw.TitleFont, num2, num3, 0.24f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		(string, string, (int, int, int))[] array = new(string, string, (int, int, int))[3]
		{
			("Right lung punctured", "SEVERE", Theme.Warn),
			("Fourth rib fractured", "SERIOUS", Theme.Warn),
			("Bullet exited the back", "", Theme.Dim)
		};
		for (int j = 0; j < array.Length; j++)
		{
			float num4 = num3 + 0.032f + (float)j * 0.026f;
			UiDraw.Rect(num2, num4 + 0.004f, 0.006f, 0.01f, array[j].Item3.Item1, array[j].Item3.Item2, array[j].Item3.Item3, 255);
			UiDraw.Text(array[j].Item1, num2 + 0.014f, num4, 0.22f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			if (array[j].Item2 != "")
			{
				UiDraw.TextFont(array[j].Item2, UiDraw.TitleFont, num2 + w * 0.4f, num4, 0.21f, array[j].Item3.Item1, array[j].Item3.Item2, array[j].Item3.Item3);
			}
		}
		void L(float u1, float v1, float u2, float v2)
		{
			UiDraw.Line(X(u1), Y(v1), X(u2), Y(v2), 0.0016f, bone.Item1, bone.Item2, bone.Item3);
		}
		float X(float u)
		{
			return cx + u * s / a;
		}
		float Y(float v)
		{
			return top + v * s;
		}
	}

	private static void Wordmark(float x, float y, float w, float h)
	{
		float num = Theme.TitleWidth("en", 0.95f);
		float num2 = Theme.CapsWidth("RAGE", 0.95f);
		float num3 = x + w / 2f - (num + num2) / 2f;
		float y2 = y + h / 2f - 0.038f;
		UiDraw.TextFont("en", UiDraw.TitleFont, num3, y2, 0.95f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.TextFont("RAGE", UiDraw.TitleFont, num3 + num, y2, 0.95f, Theme.NativeRed.r, Theme.NativeRed.g, Theme.NativeRed.b);
	}
}
