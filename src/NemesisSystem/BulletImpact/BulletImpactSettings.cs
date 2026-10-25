using System;
using System.Globalization;

namespace NemesisSystem.BulletImpact;

internal sealed class BulletImpactSettings
{
	private readonly string _path;

	public bool Enabled = true;

	public bool LogHits;

	public bool Crippling = true;

	public float PlayerDamage = 0.6f;

	public bool KillShots = true;

	public int XRayMaxEntries = 2000;

	public XRayQuality XRayQuality = XRayQuality.Auto;

	public int XRayRectLimit = 1500;

	public float XRayGlyphCost = 0.8f;

	public BulletImpactSettings(string path)
	{
		_path = path;
		IniFile iniFile = IniFile.Load(path);
		Enabled = iniFile.GetValue("BulletImpact", "Enabled", Enabled);
		LogHits = iniFile.GetValue("Debug", "LogHits", LogHits);
		Crippling = iniFile.GetValue("Wounds", "Crippling", Crippling);
		PlayerDamage = iniFile.GetValue("Wounds", "PlayerDamage", PlayerDamage);
		KillShots = iniFile.GetValue("Wounds", "KillShots", KillShots);
		XRayMaxEntries = Math.Max(1, iniFile.GetValue("XRay", "MaxEntries", XRayMaxEntries));
		XRayQuality = iniFile.GetValue("XRay", "Detail", XRayQuality);
		XRayRectLimit = iniFile.GetValue("XRay", "RectLimit", XRayRectLimit);
		XRayGlyphCost = Math.Max(0f, Math.Min(4f, iniFile.GetValue("XRay", "GlyphCost", XRayGlyphCost)));
		int value = iniFile.GetValue("Meta", "Version", 0);
		if (value < 1 && (XRayGlyphCost == 1f || XRayGlyphCost == 0.4f || XRayGlyphCost == 0.6f))
		{
			XRayGlyphCost = 0.8f;
		}
		if (value < 2 && XRayRectLimit == 500)
		{
			XRayRectLimit = 1200;
		}
		if (value < 3 && (XRayRectLimit == 500 || XRayRectLimit == 1200))
		{
			XRayRectLimit = 1500;
		}
		if (value < 5 && XRayMaxEntries == 300)
		{
			XRayMaxEntries = 2000;
		}
		if (value < 4 && XRayMaxEntries == 80)
		{
			XRayMaxEntries = 2000;
		}
		Save();
	}

	public void Save()
	{
		try
		{
			SafeFile.Write(_path, $"[Meta]\r\nVersion=5\r\n\r\n[BulletImpact]\r\nEnabled={Enabled}\r\n\r\n[Debug]\r\n; True = also write the per-frame DIAG lines to BulletImpact.log (hits, kill shots and crippling are always logged).\r\nLogHits={LogHits}\r\n\r\n[Wounds]\r\n; Shattered spine / pelvis / knee / leg bones put people down for good (they crawl), a broken neck\r\n; paralyzes, a shattered foot leaves a limp. Decided from each hit's X-ray.\r\nCrippling={Crippling}\r\n; Your weapon damage against everyone, 1.0 = vanilla. Lower = people soak more hits before dying.\r\nPlayerDamage={PlayerDamage.ToString(CultureInfo.InvariantCulture)}\r\n; A shot whose X-ray destroys the brain or pierces the heart kills outright, whatever the damage.\r\nKillShots={KillShots}\r\n\r\n[XRay]\r\n; X-ray log (F11, then Tab): the newest MaxEntries people and the newest MaxEntries animals are kept in enRAGE\\xray_log.json.\r\nMaxEntries={XRayMaxEntries}\r\n; Detail: Auto (best that fits the rect limit) / High / Medium / Low.\r\nDetail={XRayQuality}\r\n; The game's per-frame limit on script-drawn rects (it silently drops the rest). The X-ray uses\r\n; whatever the menu leaves under it. Measure it with the calibration screen: K in the X-ray log.\r\nRectLimit={XRayRectLimit}\r\n; Every letter of text on screen seems to come out of the same per-frame limit (the X-ray report's text left the skeleton\r\n; half drawn). The X-ray budget takes this many units off per drawn letter. Raise it if bones still go missing.\r\nGlyphCost={XRayGlyphCost.ToString(CultureInfo.InvariantCulture)}\r\n");
		}
		catch
		{
		}
	}
}
