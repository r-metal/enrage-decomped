using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace NemesisSystem.BulletImpact;

internal sealed class XRayRenderer
{
	private enum Kind
	{
		Line,
		Bone,
		Dot,
		Ring,
		Sphere
	}

	private enum Style
	{
		Bone,
		Fine,
		Cartilage,
		Organ,
		Vessel,
		Incoming,
		Inside,
		Exit,
		PelletIn,
		PelletInside,
		PelletExit,
		EntryMark,
		StopMark,
		BruiseMark,
		KickMark,
		GunMark,
		ChokeMark
	}

	private sealed class Prim
	{
		public Kind Kind;

		public Style Style;

		public V3 A;

		public V3 B;

		public float R;

		public V3[] Pts;

		public bool Open;

		public string Key;

		public int Priority;

		public bool Hit;

		public bool Fracture;

		public V3 HitAt;

		public (int r, int g, int b) Color;

		public float Depth;

		public bool Drawn;
	}

	private struct Cam
	{
		public V3 C;

		public V3 S;

		public V3 U;

		public V3 F;

		public float Scale;

		public float Cx;

		public float Cy;
	}

	private const int SphereSegs = 18;

	private int _mp = 6;

	private List<Prim> _prims = new List<Prim>();

	private readonly Dictionary<XRayQuality, List<Prim>> _levels = new Dictionary<XRayQuality, List<Prim>>();

	private XRayQuality _preference = XRayQuality.Auto;

	private List<V3> _framePoints = new List<V3>();

	private V3 _center;

	private V3 _focus;

	private float _baseYaw;

	private float _fit = -1f;

	public float Yaw;

	public float Pitch = 8f;

	public float Zoom = 1f;

	public float PanX;

	public float PanY;

	public bool AutoSpin;

	private bool _left;

	private bool _right;

	private bool _up;

	private bool _down;

	private bool _zoomIn;

	private bool _zoomOut;

	private const float CameraDistance = 3.2f;

	private static readonly (int r, int g, int b) BoneColor = (r: 222, g: 230, b: 234);

	private static readonly (int r, int g, int b) FineColor = (r: 185, g: 205, b: 214);

	private static readonly (int r, int g, int b) CartilageColor = (r: 105, g: 135, b: 146);

	private (float x0, float y0, float x1, float y1) _clip;

	private Cam _lastCam;

	private static float _zoomThick = 1f;

	private static (float x0, float y0, float x1, float y1)? _costClip;

	private readonly Dictionary<string, (XRayQuality level, int mp, bool rich, HashSet<Prim> planned, int est, int richCost)> _planCache = new Dictionary<string, (XRayQuality, int, bool, HashSet<Prim>, int, int)>();

	private int _planCacheBuild = -1;

	private bool _rich;

	public int LastSpentEstimate;

	public int LastRichCost;

	private int _buildId;

	private int _planMp;

	private bool _planRich;

	private string _planKey;

	private HashSet<Prim> _planned = new HashSet<Prim>();

	private const int PathSteps = 12;

	private int _start;

	private int _budget;

	public static Action<string> Diag;

	private int _replanLogAt = -100000;

	public int LastStepCap => _mp;

	public XRayQuality ActiveLevel { get; private set; } = XRayQuality.Medium;

	public int LastRects { get; private set; }

	public int LastDropped { get; private set; }

	public bool Rich => _rich;

	public int LastCut { get; private set; }

	public int PlanCount { get; private set; }

	public void ResetView()
	{
		Yaw = 0f;
		Pitch = 8f;
		Zoom = 1f;
		PanX = (PanY = 0f);
		_fit = -1f;
	}

	public bool SetHeld(Keys key, bool down)
	{
		switch (key)
		{
		case Keys.Left:
		case Keys.A:
			_left = down;
			return true;
		case Keys.Right:
		case Keys.D:
			_right = down;
			return true;
		case Keys.W:
			_up = down;
			return true;
		case Keys.S:
			_down = down;
			return true;
		case Keys.Z:
			_zoomIn = down;
			return true;
		case Keys.X:
			_zoomOut = down;
			return true;
		default:
			return false;
		}
	}

	public void ReleaseAll()
	{
		_left = (_right = (_up = (_down = (_zoomIn = (_zoomOut = false)))));
	}

	public void Orbit(float dYaw, float dPitch)
	{
		Yaw += dYaw;
		Pitch = Math.Max(-80f, Math.Min(80f, Pitch + dPitch));
	}

	public void ZoomBy(float factor)
	{
		Zoom = Math.Max(0.6f, Math.Min(5f, Zoom * factor));
	}

	public void Build(Anatomy.Body body, IList<(PathResult path, bool thin)> paths, ISet<string> hitKeys, XRayQuality preference)
	{
		_preference = preference;
		_buildId++;
		XRayQuality[] array = new XRayQuality[3]
		{
			XRayQuality.Low,
			XRayQuality.Medium,
			XRayQuality.High
		};
		foreach (XRayQuality xRayQuality in array)
		{
			_prims = new List<Prim>();
			BuildLevel(body, paths, hitKeys, xRayQuality);
			_levels[xRayQuality] = _prims;
		}
		ActiveLevel = ((preference == XRayQuality.Auto) ? XRayQuality.Medium : preference);
		_prims = _levels[ActiveLevel];
	}

	private void BuildLevel(Anatomy.Body body, IList<(PathResult path, bool thin)> paths, ISet<string> hitKeys, XRayQuality q)
	{
		_fit = -1f;
		Dictionary<int, V3> bones = body.Bones;
		_framePoints = bones.Values.ToList();
		_center = new V3(_framePoints.Average((V3 p) => p.X), _framePoints.Average((V3 p) => p.Y), (_framePoints.Min((V3 p) => p.Z) + _framePoints.Max((V3 p) => p.Z) + 0.15f) / 2f);
		_focus = ((paths.Count > 0) ? paths[0].path.Entry : _center);
		V3 v = ((paths.Count > 0) ? (paths[0].path.End - paths[0].path.Start) : new V3(1f, 0f, 0f));
		V3 v2 = new V3(v.X, v.Y, 0f).Normalized();
		if (v2.Length() < 0.5f)
		{
			v2 = new V3(1f, 0f, 0f);
		}
		_baseYaw = (float)(Math.Atan2(0f - v2.Y, v2.X) * 180.0 / Math.PI);
		V3 up = body.Up;
		V3 fwd = body.Fwd;
		V3 right = body.Right;
		bool flag = q == XRayQuality.High;
		bool flag2 = q == XRayQuality.Low;
		if (body.Animal)
		{
			BuildAnimal(body, q);
		}
		else
		{
			float right2;
			if (!flag2)
			{
				foreach (Anatomy.Sphere organ in body.Organs)
				{
					V3 c = organ.C;
					right2 = organ.R * 0.8f;
					Add(key: organ.Key, kind: Kind.Dot, style: Style.Organ, a: c, b: default(V3), r: right2).Color = OrganColor(organ.Key);
				}
			}
			List<V3> list = Anatomy.SpineChain.Where(body.Has).Select(body.Get).ToList();
			float fwd2;
			for (int num = 0; num + 1 < list.Count; num++)
			{
				body.TorsoCoords(list[num], out var frac, out right2, out fwd2);
				Add(Kind.Line, Style.Bone, list[num], list[num + 1], 0f, SpineKey(frac), 0);
			}
			foreach (V3 item4 in Resample(list, flag2 ? 0.045f : 0.026f))
			{
				body.TorsoCoords(item4, out var frac2, out fwd2, out right2);
				right2 = ((frac2 > 1f) ? 0.012f : 0.016f);
				Add(key: SpineKey(frac2), kind: Kind.Dot, style: Style.Bone, a: item4, b: default(V3), r: right2, pri: 1);
				if (!flag2)
				{
					Add(Kind.Line, Style.Fine, item4, item4 - fwd * 0.035f - up * 0.01f, 0f, SpineKey(frac2), 3);
				}
			}
			V3 v3 = (body.Has(21030) ? body.Get(21030) : (body.HeadCenter - up * 0.07f));
			V3 v4 = (body.Has(14283) ? (v3 - body.Get(14283)).Normalized() : up);
			if (v4.Length() < 0.5f)
			{
				v4 = up;
			}
			V3 v5 = fwd;
			if (body.Has(58559))
			{
				V3 v6 = body.Get(58559) - v3;
				V3 v7 = v6 - v4 * V3.Dot(v6, v4);
				if (v7.Length() > 0.01f)
				{
					v5 = v7.Normalized();
				}
			}
			V3 hr = V3.Cross(v5, v4).Normalized();
			V3 v8 = v3 + v4 * 0.07f + v5 * 0.02f;
			BuildSkull(v8, v5, hr, v4, q);
			_framePoints.Add(v8 + v4 * 0.09f);
			_framePoints.Add(v8 + v5 * 0.09f - v4 * 0.15f);
			int num2 = (flag ? 10 : ((q == XRayQuality.Medium) ? 8 : 5));
			float[] array;
			for (int num3 = 0; num3 < num2; num3++)
			{
				float num4 = 0.9f - (float)num3 * (0.4f / (float)Math.Max(1, num2 - 1));
				float num5 = Math.Max(0f, 1f - (float)Math.Pow((num4 - 0.68f) / 0.26f, 2.0));
				float num6 = 0.075f + 0.055f * num5;
				float num7 = 0.085f + 0.015f * num5;
				array = new float[2] { -1f, 1f };
				foreach (float num9 in array)
				{
					List<V3> list2 = new List<V3>();
					int num10 = (flag ? 5 : 4);
					for (int num11 = 0; num11 <= num10; num11++)
					{
						double num12 = -1.4507963267948965 + (double)num11 * 2.7074333882308137 / (double)num10;
						float right3 = num9 * num6 * (float)Math.Cos(num12);
						float fwd3 = num7 * (float)Math.Sin(num12) - 0.01f;
						float num13 = -0.045f * (float)((num12 + Math.PI / 2.0) / Math.PI);
						list2.Add(body.TorsoPoint(num4, right3, fwd3) + up * num13);
					}
					string key = ((num9 < 0f) ? "ribs_l" : "ribs_r");
					for (int num14 = 0; num14 + 1 < list2.Count; num14++)
					{
						Add(Kind.Line, Style.Fine, list2[num14], list2[num14 + 1], 0f, key, 1);
					}
					if (!flag2 && num4 > 0.55f)
					{
						Add(Kind.Line, Style.Cartilage, list2[list2.Count - 1], body.TorsoPoint(num4 - 0.04f, num9 * 0.012f, 0.105f), 0f, null, 3);
					}
				}
			}
			Add(Kind.Bone, Style.Bone, body.TorsoPoint(0.93f, 0f, 0.1f), body.TorsoPoint(0.58f, 0f, 0.108f), 0.012f, "sternum", 1);
			(int, int, int, int, int[][], float, string)[] array2 = new(int, int, int, int, int[][], float, string)[2]
			{
				(30226, 37873, 53675, 34606, Anatomy.LFingers, -1f, "_l"),
				(54802, 46065, 54187, 22798, Anatomy.RFingers, 1f, "_r")
			};
			for (int num8 = 0; num8 < array2.Length; num8++)
			{
				var (id, id2, id3, id4, source, num15, text) = array2[num8];
				if (body.Has(id2))
				{
					V3 v9 = body.TorsoPoint(0.95f, num15 * 0.02f, 0.08f);
					Add(Kind.Bone, Style.Bone, body.Has(id) ? body.Get(id) : v9, body.Get(id2), 0.008f, "clavicle" + text, 0);
				}
				if (!flag2)
				{
					V3 v10 = body.TorsoPoint(0.93f, num15 * 0.05f, -0.085f);
					V3 v11 = body.TorsoPoint(0.93f, num15 * 0.15f, -0.06f);
					V3 v12 = body.TorsoPoint(0.68f, num15 * 0.08f, -0.09f);
					Add(Kind.Line, Style.Fine, v10, v11, 0f, "scapula" + text);
					Add(Kind.Line, Style.Fine, v11, v12, 0f, "scapula" + text);
					Add(Kind.Line, Style.Fine, v12, v10, 0f, "scapula" + text);
				}
				string key2;
				if (body.Has(id2) && body.Has(id3))
				{
					Add(Kind.Bone, Style.Bone, body.Get(id2), body.Get(id3), 0.012f, "humerus" + text, 0);
					V3 a = body.Get(id2);
					key2 = "humerus" + text;
					Add(Kind.Dot, Style.Bone, a, default(V3), 0.018f, key2, 1);
				}
				if (!flag2 && body.Has(id2) && body.Has(id3))
				{
					V3 v13 = body.Get(id2);
					V3 v14 = body.Get(id3);
					V3 v15 = (v14 - v13).Normalized();
					V3 v16 = Perp(v15, right * num15, fwd);
					V3 v17 = V3.Cross(v15, v16).Normalized();
					if (V3.Dot(v17, fwd) > 0f)
					{
						v17 *= -1f;
					}
					V3 a2 = v14 + v16 * 0.024f + v15 * 0.005f;
					key2 = "humerus" + text;
					Add(Kind.Dot, Style.Bone, a2, default(V3), 0.01f, key2, 3);
					V3 a3 = v14 - v16 * 0.026f + v15 * 0.005f;
					key2 = "humerus" + text;
					Add(Kind.Dot, Style.Bone, a3, default(V3), 0.011f, key2, 3);
					V3 a4 = v14 + v17 * 0.018f;
					key2 = "forearm" + text;
					Add(Kind.Dot, Style.Bone, a4, default(V3), 0.01f, key2, 3);
					Add(Kind.Line, Style.Fine, v14 + v16 * 0.024f, v14 - v16 * 0.026f, 0f, "humerus" + text, 3);
					V3 a5 = v13 + v16 * 0.02f - v15 * 0.004f;
					key2 = "humerus" + text;
					Add(Kind.Dot, Style.Bone, a5, default(V3), 0.01f, key2, 3);
				}
				if (body.Has(id3) && body.Has(id4))
				{
					V3 v18 = body.Get(id3);
					V3 v19 = body.Get(id4);
					if (!flag2)
					{
						V3 v20 = (v19 - v18).Normalized();
						V3 v21 = Perp(v20, V3.Cross(v20, up), right);
						V3 v22 = V3.Lerp(v18, v19, 0.94f);
						V3 a6 = v22 + v21 * 0.016f;
						key2 = "forearm" + text;
						Add(Kind.Dot, Style.Bone, a6, default(V3), 0.008f, key2, 3);
						V3 a7 = v22 - v21 * 0.014f;
						key2 = "forearm" + text;
						Add(Kind.Dot, Style.Bone, a7, default(V3), 0.008f, key2, 3);
					}
					V3 v23 = V3.Cross((v19 - v18).Normalized(), up).Normalized() * 0.011f;
					if (v23.Length() < 0.005f)
					{
						v23 = right * 0.011f;
					}
					Add(Kind.Bone, Style.Bone, v18 + v23, v19 + v23 * 0.8f, 0.007f, "forearm" + text, 0);
					Add(Kind.Bone, Style.Bone, v18 - v23, v19 - v23 * 0.6f, 0.007f, "forearm" + text, 1);
				}
				if (!body.Has(id4))
				{
					continue;
				}
				V3 a8 = body.Get(id4);
				key2 = "hand" + text;
				Add(Kind.Dot, Style.Bone, a8, default(V3), 0.016f, key2, 1);
				if (flag2)
				{
					continue;
				}
				foreach (int[] item5 in source.Where((int[] f) => body.Has(f[0])))
				{
					List<V3> list3 = item5.Where(body.Has).Select(body.Get).ToList();
					Add(Kind.Line, Style.Fine, a8, list3[0], 0f, "hand" + text);
					V3 a9 = V3.Lerp(a8, list3[0], 0.2f);
					key2 = "hand" + text;
					Add(Kind.Dot, Style.Bone, a9, default(V3), 0.006f, key2, 3);
					V3 a10 = list3[0];
					key2 = "hand" + text;
					Add(Kind.Dot, Style.Bone, a10, default(V3), 0.0065f, key2, 3);
					for (int num16 = 0; num16 + 1 < list3.Count; num16++)
					{
						Add(Kind.Line, Style.Fine, list3[num16], list3[num16 + 1], 0f, "hand" + text, 3);
						if (flag)
						{
							V3 a11 = list3[num16 + 1];
							key2 = "hand" + text;
							Add(Kind.Dot, Style.Bone, a11, default(V3), 0.005f, key2, 3);
						}
					}
				}
			}
			V3 pelvisPos = body.PelvisPos;
			array = new float[2] { -1f, 1f };
			foreach (float num17 in array)
			{
				int id5 = ((num17 < 0f) ? 65478 : 6884);
				V3 v24 = (body.Has(id5) ? body.Get(id5) : (pelvisPos + right * (num17 * 0.1f)));
				V3 a12 = pelvisPos + right * (num17 * 0.04f) - fwd * 0.06f + up * 0.07f;
				V3 v25 = pelvisPos + right * (num17 * 0.13f) - fwd * 0.01f + up * 0.08f;
				V3 v26 = pelvisPos + right * (num17 * 0.13f) + fwd * 0.05f + up * 0.04f;
				V3 b = pelvisPos + right * (num17 * 0.09f) + fwd * 0.07f;
				V3 b2 = pelvisPos + fwd * 0.07f - up * 0.06f;
				V3 v27 = pelvisPos + right * (num17 * 0.05f) - fwd * 0.02f - up * 0.09f;
				Add(Kind.Line, Style.Bone, a12, v25, 0f, "pelvis", 0);
				Add(Kind.Line, Style.Bone, v25, v26, 0f, "pelvis", 0);
				Add(Kind.Line, Style.Bone, v26, v24, 0f, "pelvis", 0);
				Add(Kind.Line, Style.Bone, v24, b2, 0f, "pelvis", 0);
				Add(Kind.Line, Style.Fine, a12, v24, 0f, "pelvis");
				Add(Kind.Line, Style.Fine, v26, b, 0f, "pelvis");
				Add(Kind.Line, Style.Fine, v24, v27, 0f, "pelvis", 1);
				Add(Kind.Line, Style.Fine, v27, b2, 0f, "pelvis");
			}
			V3 v28 = pelvisPos - fwd * 0.07f + up * 0.06f;
			V3 b3 = pelvisPos - fwd * 0.06f - up * 0.05f;
			Add(Kind.Line, Style.Fine, v28 + right * 0.04f, v28 - right * 0.04f, 0f, "pelvis");
			Add(Kind.Line, Style.Fine, v28 + right * 0.04f, b3, 0f, "pelvis");
			Add(Kind.Line, Style.Fine, v28 - right * 0.04f, b3, 0f, "pelvis");
			(int, int, int, int, int, float, string)[] array3 = new(int, int, int, int, int, float, string)[2]
			{
				(65478, 55120, 45454, 53081, 53082, -1f, "_l"),
				(6884, 43312, 33646, 41273, 41274, 1f, "_r")
			};
			for (int num8 = 0; num8 < array3.Length; num8++)
			{
				var (id6, id7, id8, id9, id10, num18, text2) = array3[num8];
				if (body.Has(id6) && body.Has(id7))
				{
					V3 v29 = body.Get(id6);
					V3 v30 = body.Get(id7);
					string key2 = "femur" + text2;
					Add(Kind.Dot, Style.Bone, v29, default(V3), 0.02f, key2, 1);
					Add(Kind.Bone, Style.Bone, v29, v30, 0.015f, "femur" + text2, 0);
					V3 a13 = v30 + fwd * 0.045f;
					key2 = "knee" + text2;
					Add(Kind.Dot, Style.Bone, a13, default(V3), 0.018f, key2);
					key2 = "knee" + text2;
					Add(Kind.Dot, Style.Bone, v30, default(V3), 0.017f, key2);
					if (!flag2)
					{
						V3 v31 = (v30 - v29).Normalized();
						V3 v32 = Perp(v31, right * num18, fwd);
						V3 v33 = v29 + v32 * 0.05f + v31 * 0.035f;
						Add(Kind.Line, Style.Bone, v29, v33, 0f, "femur" + text2, 3);
						key2 = "femur" + text2;
						Add(Kind.Dot, Style.Bone, v33, default(V3), 0.016f, key2, 3);
						V3 a14 = v29 - v32 * 0.005f + v31 * 0.075f - fwd * 0.012f;
						key2 = "femur" + text2;
						Add(Kind.Dot, Style.Bone, a14, default(V3), 0.009f, key2, 3);
						V3 a15 = v30 + v32 * 0.026f - v31 * 0.012f - fwd * 0.006f;
						key2 = "femur" + text2;
						Add(Kind.Dot, Style.Bone, a15, default(V3), 0.014f, key2, 3);
						V3 a16 = v30 - v32 * 0.026f - v31 * 0.012f - fwd * 0.006f;
						key2 = "femur" + text2;
						Add(Kind.Dot, Style.Bone, a16, default(V3), 0.014f, key2, 3);
						Add(Kind.Line, Style.Fine, v30 + v32 * 0.03f + v31 * 0.028f, v30 - v32 * 0.03f + v31 * 0.028f, 0f, "tibia" + text2, 3);
					}
					if (!flag2)
					{
						Add(Kind.Line, Style.Vessel, v29 + fwd * 0.02f, V3.Lerp(v29, v30, 0.5f) + fwd * 0.02f, 0f, "femoral" + text2, 3);
					}
				}
				if (body.Has(id7) && body.Has(id8))
				{
					V3 v34 = body.Get(id7);
					V3 v35 = body.Get(id8);
					V3 v36 = V3.Cross((v35 - v34).Normalized(), fwd).Normalized() * (0f - num18);
					if (v36.Length() < 0.5f)
					{
						v36 = right * num18;
					}
					Add(Kind.Bone, Style.Bone, v34 - v36 * 0.006f, v35 - v36 * 0.004f, 0.011f, "tibia" + text2, 0);
					Add(Kind.Bone, Style.Bone, v34 + v36 * 0.03f, v35 + v36 * 0.02f, 0.006f, "tibia" + text2, 1);
					if (!flag2)
					{
						V3 v37 = (v34 - v35).Normalized();
						V3 a17 = v35 + v37 * 0.02f + v36 * 0.026f;
						string key2 = "tibia" + text2;
						Add(Kind.Dot, Style.Bone, a17, default(V3), 0.01f, key2, 3);
						V3 a18 = v35 + v37 * 0.03f - v36 * 0.022f;
						key2 = "tibia" + text2;
						Add(Kind.Dot, Style.Bone, a18, default(V3), 0.01f, key2, 3);
						V3 a19 = v34 - v37 * 0.012f + fwd * 0.02f;
						key2 = "tibia" + text2;
						Add(Kind.Dot, Style.Bone, a19, default(V3), 0.01f, key2, 3);
					}
				}
				if (!body.Has(id8))
				{
					continue;
				}
				V3 v38 = body.Get(id8);
				V3 v39 = (body.Has(id9) ? body.Get(id9) : (v38 + fwd * 0.12f - up * 0.06f));
				V3 v40 = v38 - (v39 - v38).Normalized() * 0.06f - up * 0.04f;
				Add(Kind.Line, Style.Bone, v38, v39, 0f, "foot" + text2, 0);
				Add(Kind.Line, Style.Fine, v40, v39, 0f, "foot" + text2);
				Add(Kind.Line, Style.Fine, v38, v40, 0f, "foot" + text2, 1);
				if (!flag2)
				{
					V3 v41 = (v39 - v38).Normalized();
					V3 v42 = Perp(v41, right * num18, up);
					string key2 = "foot" + text2;
					Add(Kind.Dot, Style.Bone, v40, default(V3), 0.016f, key2, 3);
					V3 a20 = V3.Lerp(v38, v39, 0.25f) - up * 0.01f;
					key2 = "foot" + text2;
					Add(Kind.Dot, Style.Bone, a20, default(V3), 0.012f, key2, 3);
					V3 v43 = V3.Lerp(v38, v39, 0.38f) - up * 0.015f;
					array = ((!flag) ? new float[2] { -0.025f, 0.025f } : new float[4] { -0.03f, -0.012f, 0.012f, 0.03f });
					foreach (float num20 in array)
					{
						V3 v44 = v39 + v42 * num20 - v41 * Math.Abs(num20) * 0.6f;
						Add(Kind.Line, Style.Fine, v43 + v42 * (num20 * 0.4f), v44, 0f, "foot" + text2, 3);
						key2 = "foot" + text2;
						Add(Kind.Dot, Style.Bone, v44, default(V3), 0.005f, key2, 3);
					}
				}
				if (body.Has(id10))
				{
					Add(Kind.Line, Style.Fine, v39, body.Get(id10), 0f, "foot" + text2, 3);
				}
			}
		}
		foreach (var path in paths)
		{
			PathResult item = path.path;
			bool item2 = path.thin;
			V3 v45 = (((item.End - item.Entry).Length() > 0.01f) ? (item.End - item.Entry).Normalized() : (item.Entry - item.Start).Normalized());
			V3 v46 = (item.Entry - item.Start).Normalized();
			if (item.Blunt)
			{
				Add(Kind.Line, item2 ? Style.PelletIn : Style.Incoming, item.Entry - v46 * (item2 ? 0.1f : 0.2f), item.Entry, 0f, null, item2 ? 2 : 0);
				int style = ((item.Blow == BlowKind.Kick) ? 14 : ((item.Blow == BlowKind.Gun) ? 15 : ((item.Blow == BlowKind.Choke) ? 16 : 13)));
				V3 entry = item.Entry;
				float right2 = (item2 ? 0.016f : 0.024f);
				Add(Kind.Dot, (Style)style, entry, default(V3), right2, null, 0);
				continue;
			}
			Add(Kind.Line, item2 ? Style.PelletIn : Style.Incoming, item.Entry - v46 * (item2 ? 0.12f : 0.3f), item.Entry, 0f, null, item2 ? 2 : 0);
			Add(Kind.Line, item2 ? Style.PelletInside : Style.Inside, item.Entry, item.End, 0f, null, item2 ? 1 : 0);
			if (item.Exited)
			{
				Add(Kind.Line, item2 ? Style.PelletExit : Style.Exit, item.End, item.End + v45 * (item2 ? 0.1f : 0.22f), 0f, null, item2 ? 2 : 0);
			}
			if (!item2)
			{
				Add(Kind.Dot, Style.EntryMark, item.Entry, default(V3), 0.012f, null, 0);
				if (!item.Exited)
				{
					Add(Kind.Dot, Style.StopMark, item.End, default(V3), 0.014f, null, 0);
				}
			}
		}
		MarkHits(paths, hitKeys);
		foreach (Prim prim in _prims)
		{
			if (prim.Hit)
			{
				prim.Priority = 0;
			}
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (Prim item6 in from p in _prims
			where p.Fracture
			orderby (p.Kind == Kind.Dot) ? 1 : 0
			select p)
		{
			string item3 = item6.Key + "|" + Math.Round(item6.HitAt.X, 1) + "," + Math.Round(item6.HitAt.Y, 1) + "," + Math.Round(item6.HitAt.Z, 1);
			if (!hashSet.Add(item3))
			{
				item6.Fracture = false;
			}
		}
	}

	private void BuildAnimal(Anatomy.Body body, XRayQuality q)
	{
		bool flag = q == XRayQuality.High;
		bool flag2 = q == XRayQuality.Low;
		V3 up = body.Up;
		V3 fwd = body.Fwd;
		V3 right = body.Right;
		float scale = body.Scale;
		float depth = body.Depth;
		float halfWidth = body.HalfWidth;
		float right2;
		if (!flag2)
		{
			foreach (Anatomy.Sphere organ in body.Organs)
			{
				V3 c = organ.C;
				right2 = organ.R * 0.8f;
				Add(key: organ.Key, kind: Kind.Dot, style: Style.Organ, a: c, b: default(V3), r: right2).Color = OrganColor(organ.Key);
			}
		}
		List<(int, V3)> list = (from num23 in new int[10] { 56200, 14410, 14411, 14412, 14413, 14283, 14284, 14285, 14286, 21030 }.Where(body.Has)
			select (id: num23, p: Get(num23))).ToList();
		for (int num = 0; num + 1 < list.Count; num++)
		{
			Add(Kind.Line, Style.Bone, list[num].Item2, list[num + 1].Item2, 0f, KeyOf(list[num].Item1), 0);
		}
		foreach (V3 item14 in Resample(list.Select<(int, V3), V3>(((int id, V3 p) tuple2) => tuple2.p).ToList(), (flag2 ? 0.1f : 0.06f) * scale))
		{
			body.TorsoCoords(item14, out var frac, out right2, out var fwd2);
			string text = ((frac > 1.02f) ? "cervical" : ((frac > 0.55f) ? "spine_t" : "spine_l"));
			fwd2 = 0.022f * scale;
			string key = text;
			Add(Kind.Dot, Style.Bone, item14, default(V3), fwd2, key, 1);
			if (!flag2 && !(frac > 1.05f))
			{
				float num2 = ((frac > 0.55f) ? (0.05f + 0.1f * Math.Max(0f, 1f - Math.Abs(frac - 0.85f) / 0.35f)) : 0.045f);
				Add(Kind.Line, Style.Fine, item14, item14 - fwd * (num2 * scale) - up * (0.02f * scale), 0f, text, 3);
			}
		}
		List<V3> list2 = AnimalAnatomy.TailIds.Where(body.Has).Select(Get).ToList();
		if (list2.Count > 0 && body.Has(56200))
		{
			List<V3> list3 = new List<V3> { Get(56200) };
			list3.AddRange(list2);
			for (int num3 = 0; num3 + 1 < list3.Count; num3++)
			{
				Add(Kind.Line, Style.Bone, list3[num3], list3[num3 + 1], 0f, "tail", 1);
			}
			if (!flag2)
			{
				foreach (V3 item15 in Resample(list3, 0.05f * scale))
				{
					float fwd2 = 0.012f * scale;
					Add(Kind.Dot, Style.Bone, item15, default(V3), fwd2, "tail", 3);
				}
			}
		}
		V3 head;
		V3 hf;
		V3 hu;
		V3 hr;
		float sl;
		float[] array;
		if (body.Has(21030))
		{
			head = Get(21030);
			hf = body.Snout;
			hu = (fwd * -1f - hf * V3.Dot(fwd * -1f, hf)).Normalized();
			if (hu.Length() < 0.5f)
			{
				hu = new V3(0f, 0f, 1f);
			}
			hr = V3.Cross(hf, hu).Normalized();
			sl = 0.42f * scale;
			int segs = (flag ? 16 : (flag2 ? 10 : 12));
			AddEllipse(P(0.12f, 0f, 0.05f), hf, 0.2f * sl, hu, 0.17f * sl, segs, 0.0, Math.PI * 2.0, Style.Bone, "skull", 0);
			if (!flag2)
			{
				AddEllipse(P(0.12f, 0f, 0.05f), hf, 0.2f * sl, hr, 0.15f * sl, segs, 0.0, Math.PI * 2.0, Style.Fine, "skull", 2);
			}
			array = new float[2] { -1f, 1f };
			foreach (float num5 in array)
			{
				Add(Kind.Line, Style.Bone, P(0.28f, num5 * 0.1f, 0.14f), P(1f, num5 * 0.05f, 0.06f), 0f, "skull", 0);
				Add(Kind.Line, Style.Fine, P(0.28f, num5 * 0.12f, -0.06f), P(1f, num5 * 0.05f, -0.02f), 0f, "skull", 1);
				if (!flag2)
				{
					AddEllipse(P(0.36f, num5 * 0.13f, 0.08f), hf, 0.07f * sl, hu, 0.06f * sl, 8, 0.0, Math.PI * 2.0, Style.Fine, "skull", 2);
				}
			}
			Add(Kind.Line, Style.Fine, P(1f, -0.05f, 0.06f), P(1f, 0.05f, 0.06f), 0f, "skull");
			Add(Kind.Line, Style.Fine, P(1f, -0.05f, -0.02f), P(1f, 0.05f, -0.02f), 0f, "skull");
			array = new float[2] { -1f, 1f };
			foreach (float num6 in array)
			{
				Add(Kind.Line, Style.Bone, P(0.06f, num6 * 0.12f, -0.1f), P(0.3f, num6 * 0.11f, -0.24f), 0f, "jaw", 0);
				Add(Kind.Line, Style.Bone, P(0.3f, num6 * 0.11f, -0.24f), P(0.95f, num6 * 0.04f, -0.1f), 0f, "jaw", 0);
			}
			if (!flag2)
			{
				int num7 = (flag ? 7 : 4);
				for (int num8 = 0; num8 < num7; num8++)
				{
					float num9 = 0.45f + (float)num8 * (0.48f / (float)Math.Max(1, num7 - 1));
					array = new float[2] { -1f, 1f };
					foreach (float num10 in array)
					{
						V3 a = P(num9, num10 * (0.1f - num9 * 0.05f), -0.07f);
						float fwd2 = 0.009f * scale;
						Add(Kind.Dot, Style.Bone, a, default(V3), fwd2, "jaw", 3);
					}
				}
			}
			_framePoints.Add(P(1.05f, 0f, 0f));
			_framePoints.Add(P(0.1f, 0f, 0.25f));
		}
		int num11 = (flag ? 13 : ((q == XRayQuality.Medium) ? 9 : 6));
		for (int num12 = 0; num12 < num11; num12++)
		{
			float num13 = 0.98f - (float)num12 * (0.46f / (float)Math.Max(1, num11 - 1));
			float num14 = Math.Max(0.55f, 1f - (float)Math.Pow((num13 - 0.72f) / 0.4f, 2.0));
			float num15 = ((num13 > 0.68f) ? 1f : (0.62f + (num13 - 0.52f) * 2.3f));
			array = new float[2] { -1f, 1f };
			foreach (float num16 in array)
			{
				List<V3> list4 = new List<V3>();
				int num17 = (flag ? 6 : 4);
				for (int num18 = 0; num18 <= num17; num18++)
				{
					double num19 = -1.4207963267948966 + (double)num18 * (1.4207963267948966 + Math.PI / 2.0 * (double)num15 - 0.05) / (double)num17;
					float right3 = num16 * halfWidth * num14 * (float)Math.Cos(num19);
					float fwd3 = depth * (float)Math.Sin(num19);
					float num20 = -0.05f * scale * (float)((num19 + Math.PI / 2.0) / Math.PI);
					list4.Add(body.TorsoPoint(num13, right3, fwd3) + up * num20);
				}
				string key2 = ((num16 < 0f) ? "ribs_l" : "ribs_r");
				for (int num21 = 0; num21 + 1 < list4.Count; num21++)
				{
					Add(Kind.Line, Style.Fine, list4[num21], list4[num21 + 1], 0f, key2, 1);
				}
			}
		}
		Add(Kind.Bone, Style.Bone, body.TorsoPoint(1f, 0f, depth * 0.98f), body.TorsoPoint(0.66f, 0f, depth * 0.98f), 0.018f * scale, "sternum", 1);
		(float, string, int, int, int, int, int, int, int, int, int, int, int)[] array2 = new(float, string, int, int, int, int, int, int, int, int, int, int, int)[2]
		{
			(-1f, "_l", 30226, 37873, 53675, 34606, 41403, 41404, 65478, 55120, 45454, 53081, 53082),
			(1f, "_r", 54802, 46065, 54187, 22798, 16827, 16828, 6884, 43312, 33646, 41273, 41274)
		};
		for (int num4 = 0; num4 < array2.Length; num4++)
		{
			(float, string, int, int, int, int, int, int, int, int, int, int, int) tuple = array2[num4];
			float item = tuple.Item1;
			string item2 = tuple.Item2;
			int item3 = tuple.Item3;
			int item4 = tuple.Item4;
			int item5 = tuple.Item5;
			int item6 = tuple.Item6;
			int item7 = tuple.Item7;
			int item8 = tuple.Rest.Item1;
			int item9 = tuple.Rest.Item2;
			int item10 = tuple.Rest.Item3;
			int item11 = tuple.Rest.Item4;
			int item12 = tuple.Rest.Item5;
			int item13 = tuple.Rest.Item6;
			if (body.Has(item3) && body.Has(item4))
			{
				V3 v = Get(item3);
				V3 b = Get(item4);
				V3 a2 = v - fwd * (0.1f * scale) + up * (0.06f * scale);
				V3 v2 = v - fwd * (0.1f * scale) - up * (0.07f * scale);
				Add(Kind.Line, Style.Bone, a2, b, 0f, "scapula" + item2, 0);
				Add(Kind.Line, Style.Bone, v2, b, 0f, "scapula" + item2, 0);
				if (!flag2)
				{
					Add(Kind.Line, Style.Fine, a2, v2, 0f, "scapula" + item2);
				}
				if (!flag2)
				{
					Add(Kind.Line, Style.Fine, V3.Lerp(a2, v2, 0.5f), b, 0f, "scapula" + item2, 3);
				}
			}
			LegBone(body, item4, item5, "humerus" + item2, 0.022f * scale, 0);
			LegBone(body, item5, item6, "forearm" + item2, 0.017f * scale, 0);
			LegBone(body, item6, item7, "cannon_f" + item2, 0.013f * scale, 0);
			LegBone(body, item7, item8, "foot_f" + item2, 0.012f * scale, 1);
			foreach (int item16 in new int[4] { item4, item5, item6, item7 }.Where(body.Has))
			{
				V3 a3 = Get(item16);
				float fwd2 = 0.022f * scale;
				string key = ((item16 == item4) ? ("humerus" + item2) : ((item16 == item5) ? ("forearm" + item2) : ((item16 == item6) ? ("cannon_f" + item2) : ("foot_f" + item2))));
				Add(Kind.Dot, Style.Bone, a3, default(V3), fwd2, key, 1);
			}
			if (body.Has(item8))
			{
				V3 a4 = Get(item8);
				float fwd2 = 0.02f * scale;
				string key = "foot_f" + item2;
				Add(Kind.Dot, Style.Bone, a4, default(V3), fwd2, key, 1);
			}
			if (!flag2 && body.Has(item5) && body.Has(item4))
			{
				V3 a5 = Get(item5) - up * (0.035f * scale);
				float fwd2 = 0.016f * scale;
				string key = "forearm" + item2;
				Add(Kind.Dot, Style.Bone, a5, default(V3), fwd2, key, 3);
			}
			LegBone(body, item9, item10, "femur" + item2, 0.025f * scale, 0);
			LegBone(body, item10, item11, "tibia" + item2, 0.019f * scale, 0);
			LegBone(body, item11, item12, "cannon_h" + item2, 0.013f * scale, 0);
			LegBone(body, item12, item13, "foot_h" + item2, 0.012f * scale, 1);
			foreach (int item17 in new int[4] { item9, item10, item11, item12 }.Where(body.Has))
			{
				V3 a6 = Get(item17);
				float fwd2 = 0.024f * scale;
				string key = ((item17 == item9) ? ("femur" + item2) : ((item17 == item10) ? ("tibia" + item2) : ((item17 == item11) ? ("cannon_h" + item2) : ("foot_h" + item2))));
				Add(Kind.Dot, Style.Bone, a6, default(V3), fwd2, key, 1);
			}
			if (body.Has(item13))
			{
				V3 a7 = Get(item13);
				float fwd2 = 0.02f * scale;
				string key = "foot_h" + item2;
				Add(Kind.Dot, Style.Bone, a7, default(V3), fwd2, key, 1);
			}
			if (!flag2)
			{
				if (body.Has(item10))
				{
					V3 a8 = Get(item10) + up * (0.03f * scale);
					float fwd2 = 0.015f * scale;
					string key = "tibia" + item2;
					Add(Kind.Dot, Style.Bone, a8, default(V3), fwd2, key, 3);
				}
				if (body.Has(item11))
				{
					V3 a9 = Get(item11) - up * (0.045f * scale);
					float fwd2 = 0.016f * scale;
					string key = "tibia" + item2;
					Add(Kind.Dot, Style.Bone, a9, default(V3), fwd2, key, 3);
				}
			}
			if (!flag2 && body.Has(item9) && body.Has(item10))
			{
				Add(Kind.Line, Style.Vessel, Get(item9) - right * (item * 0.04f * scale), V3.Lerp(Get(item9), Get(item10), 0.6f) - right * (item * 0.04f * scale), 0f, "femoral" + item2, 3);
			}
		}
		if (!body.Has(56200))
		{
			return;
		}
		V3 v3 = Get(56200);
		array = new float[2] { -1f, 1f };
		foreach (float num22 in array)
		{
			int id = ((num22 < 0f) ? 65478 : 6884);
			V3 v4 = (body.Has(id) ? Get(id) : (v3 + right * (num22 * 0.1f * scale) + fwd * (0.12f * scale)));
			V3 a10 = v3 + up * (0.16f * scale) + right * (num22 * 0.14f * scale) - fwd * (0.02f * scale);
			V3 v5 = v3 - up * (0.12f * scale) + right * (num22 * 0.07f * scale) + fwd * (0.1f * scale);
			Add(Kind.Line, Style.Bone, a10, v4, 0f, "pelvis", 0);
			Add(Kind.Line, Style.Bone, v4, v5, 0f, "pelvis", 0);
			Add(Kind.Line, Style.Fine, a10, v3, 0f, "pelvis");
			if (!flag2)
			{
				Add(Kind.Line, Style.Fine, v5, v3 - up * (0.08f * scale), 0f, "pelvis");
			}
		}
		V3 Get(int id2)
		{
			return body.Get(id2);
		}
		static string KeyOf(int num23)
		{
			switch (num23)
			{
			default:
				return "spine_l";
			case 14412:
			case 14413:
				return "spine_t";
			case 14283:
			case 14284:
			case 14285:
			case 14286:
			case 21030:
				return "cervical";
			}
		}
		V3 P(float f, float r, float u)
		{
			return head + hf * (f * sl) + hr * (r * sl) + hu * (u * sl);
		}
	}

	private void LegBone(Anatomy.Body body, int a, int b, string key, float r, int pri)
	{
		if (body.Has(a) && body.Has(b))
		{
			Add(Kind.Bone, Style.Bone, body.Get(a), body.Get(b), r, key, pri);
		}
	}

	private void BuildSkull(V3 hc, V3 hf, V3 hr, V3 hu, XRayQuality q)
	{
		bool flag = q == XRayQuality.High;
		bool flag2 = q == XRayQuality.Low;
		V3 v = hc - hf * 0.012f;
		int segs = (flag ? 20 : (flag2 ? 12 : 16));
		(float, Style, int)[] array = new(float, Style, int)[4]
		{
			(-0.03f, Style.Fine, 3),
			(0f, Style.Bone, 0),
			(0.045f, Style.Fine, 1),
			(0.072f, Style.Fine, 2)
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (num, style, pri) = array[i];
			if (!flag2 || num == 0f)
			{
				float num2 = (float)Math.Sqrt(Math.Max(0f, 1f - num / 0.088f * (num / 0.088f)));
				AddEllipse(v + hu * num, hf, 0.097f * num2, hr, 0.076f * num2, segs, 0.0, Math.PI * 2.0, style, "skull", pri);
			}
		}
		AddEllipse(v, hf, 0.097f, hu, 0.088f, segs, -0.3, 3.891592653589793, Style.Bone, "skull", 0);
		AddEllipse(v, hr, 0.076f, hu, 0.088f, segs, -0.55, 3.691592653589793, flag2 ? Style.Fine : Style.Bone, "skull", 1);
		float[] array2;
		if (!flag2)
		{
			array2 = new float[2] { -1f, 1f };
			foreach (float num3 in array2)
			{
				V3 axisA = (hf * 0.7071f + hr * (0.7071f * num3)).Normalized();
				float ra = (float)Math.Sqrt(0.007592500187456608);
				AddEllipse(v, axisA, ra, hu, 0.088f, flag ? 14 : 10, 0.1, 3.041592653589793, Style.Fine, "skull", 3);
			}
			array2 = new float[2] { -1f, 1f };
			foreach (float num4 in array2)
			{
				AddEllipse(v + hu * 0.02f, hf, 0.07566f, hr, num4 * 0.076f * 0.92f, flag ? 8 : 6, -0.2, 1.7278759594743864, Style.Fine, "skull", 3);
			}
			AddEllipse(v - hu * 0.035f, hr, 0.0418f, hf, -0.08924001f, 8, 0.25, 2.891592653589793, Style.Fine, "skull", 3);
			Add(Kind.Dot, Style.Bone, v - hf * 0.095060006f - hu * 0.03f, default(V3), 0.008f, "skull", 3);
		}
		AddEllipse(P(0.08f, 0f, 0.005f), hr, 0.058f, hf, 0.02f, 8, 0.15, 2.991592653589793, Style.Fine, "skull", 1);
		array2 = new float[2] { -1f, 1f };
		foreach (float num5 in array2)
		{
			AddEllipse(P(0.084f, num5 * 0.033f, -0.022f), hr, 0.019f, hu, 0.017f, flag ? 10 : 8, 0.0, Math.PI * 2.0, Style.Fine, "skull", 1);
		}
		V3 v2 = P(0.097f, 0f, -0.035f);
		V3 v3 = P(0.094f, -0.013f, -0.066f);
		V3 v4 = P(0.094f, 0.013f, -0.066f);
		V3 b = P(0.098f, 0f, -0.072f);
		Add(Kind.Line, Style.Fine, v2, v3, 0f, "skull");
		Add(Kind.Line, Style.Fine, v2, v4, 0f, "skull");
		Add(Kind.Line, Style.Fine, v3, b, 0f, "skull");
		Add(Kind.Line, Style.Fine, v4, b, 0f, "skull");
		Add(Kind.Line, Style.Fine, P(0.092f, 0f, -0.008f), v2, 0f, "skull");
		array2 = new float[2] { -1f, 1f };
		foreach (float num6 in array2)
		{
			V3 v5 = P(0.072f, num6 * 0.05f, -0.048f);
			V3 b2 = P(-0.004f, num6 * 0.064f, -0.052f);
			V3 v6 = P(0.082f, num6 * 0.04f, -0.078f);
			if (!flag2)
			{
				Add(Kind.Dot, Style.Bone, P(-0.012f, num6 * 0.058f, -0.075f), default(V3), 0.009f, "skull", 3);
				Add(Kind.Dot, Style.Bone, v5, default(V3), 0.009f, "skull", 3);
			}
			Add(Kind.Line, Style.Fine, P(0.08f, num6 * 0.05f, -0.03f), v5, 0f, "skull");
			Add(Kind.Line, Style.Fine, v5, b2, 0f, "skull");
			Add(Kind.Line, Style.Fine, v5, v6, 0f, "skull");
			Add(Kind.Line, Style.Fine, v6, P(0.096f, num6 * 0.012f, -0.084f), 0f, "skull");
			V3 a = P(-0.002f, num6 * 0.062f, -0.06f);
			V3 v7 = P(0.004f, num6 * 0.054f, -0.128f);
			V3 v8 = P(0.06f, num6 * 0.04f, -0.14f);
			V3 b3 = P(0.09f, num6 * 0.012f, -0.15f);
			Add(Kind.Line, Style.Bone, a, v7, 0f, "jaw", 0);
			Add(Kind.Line, Style.Bone, v7, v8, 0f, "jaw", 0);
			Add(Kind.Line, Style.Bone, v8, b3, 0f, "jaw", 0);
			if (!flag2)
			{
				Add(Kind.Line, Style.Fine, P(0.01f, num6 * 0.058f, -0.075f), P(0.03f, num6 * 0.05f, -0.098f), 0f, "jaw");
			}
		}
		Add(Kind.Line, Style.Bone, P(0.09f, -0.012f, -0.15f), P(0.09f, 0.012f, -0.15f), 0f, "jaw", 0);
		if (!flag2)
		{
			int num7 = (flag ? 5 : 3);
			for (int j = -num7; j <= num7; j++)
			{
				float num8 = (float)j * (0.032f / (float)num7);
				float num9 = 0.093f - num8 * num8 * 18f;
				V3 a2 = P(num9, num8, -0.092f);
				int i = (flag ? 2 : 3);
				Add(Kind.Dot, Style.Bone, a2, default(V3), 0.0042f, "jaw", i);
				V3 a3 = P(num9 - 0.003f, num8, -0.11f);
				i = (flag ? 2 : 3);
				Add(Kind.Dot, Style.Bone, a3, default(V3), 0.0042f, "jaw", i);
			}
			Add(Kind.Line, Style.Fine, P(0.075f, -0.034f, -0.101f), P(0.075f, 0.034f, -0.101f), 0f, "jaw", 3);
		}
		V3 P(float f, float r, float u)
		{
			return hc + hf * f + hr * r + hu * u;
		}
	}

	private void AddEllipse(V3 center, V3 axisA, float ra, V3 axisB, float rb, int segs, double t0, double t1, Style style, string key, int pri)
	{
		bool flag = t1 - t0 >= 6.282185307179586;
		int num = (flag ? segs : Math.Max(2, (int)Math.Ceiling((double)segs * (t1 - t0) / (Math.PI * 2.0)) + 1));
		V3[] array = new V3[num];
		for (int i = 0; i < num; i++)
		{
			double num2 = (flag ? (t0 + (double)i * (t1 - t0) / (double)num) : (t0 + (double)i * (t1 - t0) / (double)(num - 1)));
			array[i] = center + axisA * (float)(Math.Cos(num2) * (double)ra) + axisB * (float)(Math.Sin(num2) * (double)rb);
		}
		_prims.Add(new Prim
		{
			Kind = Kind.Ring,
			Style = style,
			A = center,
			B = center,
			R = Math.Max(ra, rb),
			Pts = array,
			Key = key,
			Priority = pri,
			Open = !flag
		});
	}

	private static V3 Perp(V3 axis, V3 prefer, V3 fallback)
	{
		V3 v = prefer - axis * V3.Dot(prefer, axis);
		if (v.Length() < 0.2f)
		{
			v = fallback - axis * V3.Dot(fallback, axis);
		}
		return v.Normalized();
	}

	private static string SpineKey(float frac)
	{
		if (!(frac > 1f))
		{
			if (!(frac > 0.45f))
			{
				return "spine_l";
			}
			return "spine_t";
		}
		return "cervical";
	}

	private void MarkHits(IList<(PathResult path, bool thin)> paths, ISet<string> keys)
	{
		List<(V3, V3)> list = (from p in paths
			where !p.path.Blunt
			select (Entry: p.path.Entry, End: p.path.End)).ToList();
		List<(V3, V3)> list2 = paths.Select(((PathResult path, bool thin) p) => (Entry: p.path.Entry, End: p.path.End)).ToList();
		foreach (Prim prim2 in _prims)
		{
			if (prim2.Key == null)
			{
				continue;
			}
			if (prim2.Style == Style.Organ || prim2.Style == Style.Vessel || prim2.Kind == Kind.Sphere || prim2.Kind == Kind.Ring)
			{
				prim2.Hit = keys.Contains(prim2.Key);
			}
			else
			{
				if (!Skeletal(prim2))
				{
					continue;
				}
				foreach (var item5 in list)
				{
					V3 item = item5.Item1;
					V3 item2 = item5.Item2;
					V3 onP = prim2.A;
					float num = ((prim2.Kind == Kind.Dot) ? Anatomy.DistToSegment(prim2.A, item, item2) : Anatomy.SegmentDistance(prim2.A, prim2.B, item, item2, out onP));
					float num2 = ((prim2.Kind == Kind.Line) ? 0.004f : prim2.R) + 0.014f;
					if (!(num > num2))
					{
						prim2.Hit = true;
						prim2.Fracture = true;
						prim2.HitAt = onP;
						break;
					}
				}
			}
		}
		foreach (string key in keys)
		{
			List<Prim> list3 = _prims.Where((Prim p) => p.Key == key && Skeletal(p)).ToList();
			if (list3.Count == 0 || list3.Any((Prim p) => p.Hit))
			{
				continue;
			}
			Prim prim = null;
			float num3 = float.MaxValue;
			V3 hitAt = default(V3);
			foreach (Prim item6 in list3)
			{
				foreach (var item7 in list2)
				{
					V3 item3 = item7.Item1;
					V3 item4 = item7.Item2;
					V3 onP2 = item6.A;
					float num4 = ((item6.Kind == Kind.Dot) ? Anatomy.DistToSegment(item6.A, item3, item4) : Anatomy.SegmentDistance(item6.A, item6.B, item3, item4, out onP2));
					if (num4 < num3)
					{
						num3 = num4;
						prim = item6;
						hitAt = onP2;
					}
				}
			}
			if (prim != null)
			{
				prim.Hit = true;
				prim.Fracture = true;
				prim.HitAt = hitAt;
			}
		}
		static bool Skeletal(Prim p)
		{
			if (p.Key != null)
			{
				if (p.Style != Style.Bone)
				{
					return p.Style == Style.Fine;
				}
				return true;
			}
			return false;
		}
	}

	private Prim Add(Kind kind, Style style, V3 a, V3 b = default(V3), float r = 0f, string key = null, int pri = 2)
	{
		Prim prim = new Prim
		{
			Kind = kind,
			Style = style,
			A = a,
			B = ((kind == Kind.Line || kind == Kind.Bone) ? b : a),
			R = r,
			Key = key,
			Priority = pri
		};
		_prims.Add(prim);
		return prim;
	}

	private static List<V3> Resample(List<V3> poly, float spacing)
	{
		List<V3> list = new List<V3>();
		if (poly.Count == 0)
		{
			return list;
		}
		list.Add(poly[0]);
		float num = 0f;
		for (int i = 0; i + 1 < poly.Count; i++)
		{
			V3 v = poly[i];
			V3 v2 = poly[i + 1];
			float num2 = (v2 - v).Length();
			if (!(num2 < 1E-05f))
			{
				float num3;
				for (num3 = spacing - num; num3 <= num2; num3 += spacing)
				{
					list.Add(V3.Lerp(v, v2, num3 / num2));
				}
				num = num2 - (num3 - spacing);
			}
		}
		return list;
	}

	public int Draw(float vx, float vy, float vw, float vh, float dt, int timeMs, int budget)
	{
		int num = (_start = UiDraw.RectsThisFrame);
		_budget = budget;
		LastCut = 0;
		StepCamera(dt);
		double num2 = (double)(_baseYaw + Yaw) * Math.PI / 180.0;
		double num3 = (double)Pitch * Math.PI / 180.0;
		V3 v = new V3((float)Math.Sin(num2), (float)Math.Cos(num2), 0f);
		V3 v2 = v * (float)Math.Cos(num3) + new V3(0f, 0f, 0f - (float)Math.Sin(num3));
		V3 v3 = new V3(v.Y, 0f - v.X, 0f);
		V3 v4 = V3.Cross(v3, v2);
		V3 center = _center;
		float num4 = 0.05f;
		float num5 = 0.05f;
		foreach (V3 framePoint in _framePoints)
		{
			V3 a = framePoint - center;
			num4 = Math.Max(num4, Math.Abs(V3.Dot(a, v3)));
			num5 = Math.Max(num5, Math.Abs(V3.Dot(a, v4)));
		}
		float num6 = Math.Min(vh * 0.85f / (2f * num5 + 0.12f), vw * UiDraw.Aspect * 0.85f / (2f * num4 + 0.12f));
		_fit = ((_fit < 0f) ? num6 : (_fit + (num6 - _fit) * Math.Min(1f, dt * 6f)));
		Cam cam = (_lastCam = new Cam
		{
			C = center,
			S = v3,
			U = v4,
			F = v2,
			Scale = _fit * Zoom,
			Cx = vx + vw / 2f + PanX,
			Cy = vy + vh / 2f + PanY
		});
		_clip = (x0: vx, y0: vy, x1: vx + vw, y1: vy + vh);
		bool flag = timeMs / 220 % 2 == 0;
		(int, int, int) hitColor = (flag ? (255, 70, 55) : (190, 30, 30));
		int num7 = Math.Max(100, budget / 25 * 25);
		float num8 = (float)Math.Ceiling(Zoom * 4f) / 4f;
		_zoomThick = Math.Min(1.8f, (float)Math.Sqrt(Math.Max(1f, num8 - 0.25f)));
		string text = $"{_buildId}|{_preference}|{num7}|{num8}|{vw:0.000}|{vh:0.000}";
		if (text != _planKey)
		{
			int budget2 = num7 - (int)((float)num7 * (6f + Math.Max(0f, num8 - 3f) * 3f) / 100f);
			if (_planCacheBuild != _buildId)
			{
				_planCache.Clear();
				_planCacheBuild = _buildId;
			}
			if (_planCache.TryGetValue(text, out (XRayQuality, int, bool, HashSet<Prim>, int, int) value))
			{
				(ActiveLevel, _, _, _, _, _) = value;
				if (_preference == XRayQuality.Auto)
				{
					_prims = _levels[ActiveLevel];
				}
				_planMp = value.Item2;
				_planRich = value.Item3;
				_planned = value.Item4;
				LastSpentEstimate = value.Item5;
				LastRichCost = value.Item6;
			}
			else
			{
				if (_preference == XRayQuality.Auto)
				{
					ChooseAutoLevel(budget2, num8, vw, vh);
				}
				Plan(budget2, num8, vw, vh);
				PlanCount++;
				if (_planCache.Count > 40)
				{
					_planCache.Clear();
				}
				_planCache[text] = (ActiveLevel, _planMp, _planRich, _planned, LastSpentEstimate, LastRichCost);
			}
			if (_planKey != null && _planKey.StartsWith(_buildId + "|") && UiDraw.RealMs - _replanLogAt > 500 && (_replanLogAt = UiDraw.RealMs) >= 0)
			{
				Diag?.Invoke($"XRAY re-plan (same shot): {_planKey} -> {text}  [build|detail|budget|zoom step|w|h] - now {ActiveLevel}, smoothness {_planMp}");
			}
			_costClip = null;
			_planKey = text;
		}
		_mp = _planMp;
		_rich = _planRich;
		foreach (Prim prim in _prims)
		{
			prim.Depth = V3.Dot(V3.Lerp(prim.A, prim.B, 0.5f) - cam.C, cam.F);
			prim.Drawn = _planned.Contains(prim);
		}
		LastDropped = _prims.Count - _planned.Count;
		foreach (Prim item9 in from p in _prims
			where p.Drawn && p.Style == Style.Organ
			orderby p.Depth descending
			select p)
		{
			DrawPrim(item9, cam, hitColor, flag);
		}
		foreach (Prim item10 in from p in _prims
			where p.Drawn && p.Style != Style.Organ
			orderby p.Priority, p.Depth descending
			select p)
		{
			DrawPrim(item10, cam, hitColor, flag);
		}
		foreach (Prim item11 in _prims.Where((Prim p) => p.Drawn && p.Fracture))
		{
			(float, float, float, float, float, float, float, float) tuple2 = FractureMark(cam, item11);
			float item = tuple2.Item1;
			float item2 = tuple2.Item2;
			float item3 = tuple2.Item3;
			float item4 = tuple2.Item4;
			float item5 = tuple2.Item5;
			float item6 = tuple2.Item6;
			float item7 = tuple2.Item7;
			float item8 = tuple2.Rest.Item1;
			Seg(item, item2, item3, item4, 0.003f, (r: 255, g: 220, b: 120), 255);
			Seg(item5, item6, item7, item8, 0.003f, (r: 255, g: 220, b: 120), 255);
		}
		LastRects = UiDraw.RectsThisFrame - num;
		return LastRects;
	}

	private static (float, float, float, float, float, float, float, float) FractureMark(Cam cam, Prim p)
	{
		(float x, float y, float k) tuple = Project(cam, p.HitAt);
		float item = tuple.x;
		float item2 = tuple.y;
		float item3 = tuple.k;
		float num = 0.007f * item3;
		float aspect = UiDraw.Aspect;
		return (item - num / aspect, item2 - num, item + num / aspect, item2 + num, item - num / aspect, item2 + num, item + num / aspect, item2 - num);
	}

	private static int FractureCost(Prim p, Cam cam, int mp)
	{
		if (!p.Fracture)
		{
			return 0;
		}
		(float, float, float, float, float, float, float, float) tuple = FractureMark(cam, p);
		float item = tuple.Item1;
		float item2 = tuple.Item2;
		float item3 = tuple.Item3;
		float item4 = tuple.Item4;
		float item5 = tuple.Item5;
		float item6 = tuple.Item6;
		float item7 = tuple.Item7;
		float item8 = tuple.Rest.Item1;
		return PlanCost(item, item2, item3, item4, 0.003f, mp) + PlanCost(item5, item6, item7, item8, 0.003f, mp);
	}

	private static (float x, float y, float k) Project(Cam c, V3 p)
	{
		V3 a = p - c.C;
		float num = V3.Dot(a, c.F);
		float num2 = 3.2f / Math.Max(0.5f, 3.2f + num);
		return (x: c.Cx + V3.Dot(a, c.S) * c.Scale * num2 / UiDraw.Aspect, y: c.Cy - V3.Dot(a, c.U) * c.Scale * num2, k: num2);
	}

	private static float Thick(Prim p, float k, float scale)
	{
		return ThickRaw(p, k, scale) * _zoomThick;
	}

	private static float ThickRaw(Prim p, float k, float scale)
	{
		switch (p.Kind)
		{
		case Kind.Bone:
			return Math.Max(0.0042f, Math.Min(0.009f, 1.6f * p.R * scale * k));
		case Kind.Sphere:
			return Math.Max(0.003f, 0.0034f * k);
		default:
			switch (p.Style)
			{
			case Style.Bone:
				return Math.Max(0.0036f, 0.004f * k);
			case Style.Fine:
				return Math.Max(0.0033f, 0.0036f * k);
			case Style.Cartilage:
			case Style.Vessel:
				return 0.003f;
			case Style.Inside:
				return Math.Max(0.004f, 0.0048f * k);
			case Style.Incoming:
				return Math.Max(0.003f, 0.0034f * k);
			case Style.Exit:
				return 0.003f;
			default:
				return 0.0028f;
			}
		}
	}

	private static int Cost(Prim p, Cam cam, int mp)
	{
		switch (p.Kind)
		{
		case Kind.Dot:
			if (p.Style != Style.Organ)
			{
				return 1;
			}
			return 2;
		case Kind.Sphere:
		{
			(float x, float y, float k) tuple5 = Project(cam, p.A);
			float item6 = tuple5.x;
			float item7 = tuple5.y;
			float item8 = tuple5.k;
			float num2 = p.R * cam.Scale * item8;
			float t = Thick(p, item8, cam.Scale);
			int num3 = 0;
			for (int j = 0; j < 18; j++)
			{
				double num4 = (double)(j * 2) * Math.PI / 18.0;
				double num5 = (double)((j + 1) * 2) * Math.PI / 18.0;
				num3 += PlanCost(item6 + (float)Math.Cos(num4) * num2 / UiDraw.Aspect, item7 + (float)Math.Sin(num4) * num2 * 1.08f, item6 + (float)Math.Cos(num5) * num2 / UiDraw.Aspect, item7 + (float)Math.Sin(num5) * num2 * 1.08f, t, mp);
			}
			return num3;
		}
		case Kind.Ring:
		{
			int num = 0;
			for (int i = 0; i < (p.Open ? (p.Pts.Length - 1) : p.Pts.Length); i++)
			{
				(float x, float y, float k) tuple3 = Project(cam, p.Pts[i]);
				float item = tuple3.x;
				float item2 = tuple3.y;
				float item3 = tuple3.k;
				(float x, float y, float k) tuple4 = Project(cam, p.Pts[(i + 1) % p.Pts.Length]);
				float item4 = tuple4.x;
				float item5 = tuple4.y;
				num += PlanCost(item, item2, item4, item5, Thick(p, item3, cam.Scale), Steps(p, mp));
			}
			return num;
		}
		default:
			var (x, y, k) = Project(cam, p.A);
			var (x2, y2, _) = Project(cam, p.B);
			return PlanCost(x, y, x2, y2, Thick(p, k, cam.Scale), Steps(p, mp));
		}
	}

	private static int HighlightCost(Prim p, Cam cam, int mp)
	{
		var (x, y, k) = Project(cam, p.A);
		var (x2, y2, _) = Project(cam, p.B);
		return PlanCost(x, y, x2, y2, Thick(p, k, cam.Scale) * 0.5f, Steps(p, mp));
	}

	private static int PlanCost(float x0, float y0, float x1, float y1, float t, int mp)
	{
		(float, float, float, float)? costClip = _costClip;
		if (costClip.HasValue)
		{
			var (minX, minY, maxX, maxY) = costClip.GetValueOrDefault();
			if (!UiDraw.Clip(ref x0, ref y0, ref x1, ref y1, minX, minY, maxX, maxY))
			{
				return 0;
			}
		}
		return UiDraw.LineCost(x0, y0, x1, y1, t, mp);
	}

	private static float Shade(float depth)
	{
		return 1f - Math.Max(0f, Math.Min(1f, (depth + 0.3f) / 0.6f)) * 0.45f;
	}

	private static (int, int, int) Mul((int r, int g, int b) c, float k)
	{
		return (Math.Min(255, (int)((float)c.r * k)), Math.Min(255, (int)((float)c.g * k)), Math.Min(255, (int)((float)c.b * k)));
	}

	private void DrawPrim(Prim p, Cam cam, (int, int, int) hitColor, bool pulse)
	{
		float num = Shade(p.Depth);
		var (num2, num3, num4) = Project(cam, p.A);
		switch (p.Kind)
		{
		case Kind.Dot:
		{
			if (num2 < _clip.x0 || num2 > _clip.x1 || num3 < _clip.y0 || num3 > _clip.y1)
			{
				return;
			}
			float num5 = Math.Max(0.0025f, p.R * cam.Scale * num4);
			float aspect = UiDraw.Aspect;
			(int, int, int) tuple4;
			int a;
			switch (p.Style)
			{
			case Style.Organ:
				tuple4 = (p.Hit ? hitColor : p.Color);
				a = ((!p.Hit) ? 38 : (pulse ? 170 : 110));
				break;
			case Style.EntryMark:
				tuple4 = (255, 255, 255);
				a = 255;
				num5 = Math.Max(0.006f, num5);
				break;
			case Style.StopMark:
				tuple4 = (255, 215, 70);
				a = 255;
				num5 = Math.Max(0.007f, num5);
				break;
			case Style.BruiseMark:
				tuple4 = XRayViewer.PunchColor;
				a = 230;
				num5 = Math.Max(0.006f, num5);
				break;
			case Style.KickMark:
				tuple4 = XRayViewer.KickColor;
				a = 230;
				num5 = Math.Max(0.006f, num5);
				break;
			case Style.GunMark:
				tuple4 = XRayViewer.GunColor;
				a = 230;
				num5 = Math.Max(0.006f, num5);
				break;
			case Style.ChokeMark:
				tuple4 = XRayViewer.ChokeColor;
				a = 230;
				num5 = Math.Max(0.006f, num5);
				break;
			default:
				tuple4 = (p.Hit ? hitColor : Mul(BoneColor, num));
				a = (p.Hit ? 255 : 200);
				break;
			}
			if (p.Style == Style.Organ)
			{
				if (Spend(2))
				{
					UiDraw.Blob(num2, num3, num5 * (p.Hit ? 0.75f : 0.9f), tuple4.Item1, tuple4.Item2, tuple4.Item3, a);
				}
			}
			else if (_rich && p.Style == Style.Bone && num5 > 0.0032f)
			{
				if (Spend(2))
				{
					UiDraw.Blob(num2, num3, num5, tuple4.Item1, tuple4.Item2, tuple4.Item3, 255);
				}
			}
			else if (Spend(1))
			{
				UiDraw.Rect(num2 - num5 / aspect, num3 - num5, 2f * num5 / aspect, 2f * num5, tuple4.Item1, tuple4.Item2, tuple4.Item3, a);
			}
			return;
		}
		case Kind.Sphere:
		{
			float num6 = p.R * cam.Scale * num4;
			float aspect2 = UiDraw.Aspect;
			float t = Thick(p, num4, cam.Scale);
			(int, int, int) c2 = (p.Hit ? hitColor : Mul(BoneColor, num));
			for (int j = 0; j < 18; j++)
			{
				double num7 = (double)(j * 2) * Math.PI / 18.0;
				double num8 = (double)((j + 1) * 2) * Math.PI / 18.0;
				Seg(num2 + (float)Math.Cos(num7) * num6 / aspect2, num3 + (float)Math.Sin(num7) * num6 * 1.08f, num2 + (float)Math.Cos(num8) * num6 / aspect2, num3 + (float)Math.Sin(num8) * num6 * 1.08f, t, c2, 255);
			}
			return;
		}
		case Kind.Ring:
		{
			(int, int, int) c = (p.Hit ? hitColor : Mul(StyleColor(p.Style), num));
			for (int i = 0; i < (p.Open ? (p.Pts.Length - 1) : p.Pts.Length); i++)
			{
				var (x, y, k) = Project(cam, p.Pts[i]);
				var (x2, y2, _) = Project(cam, p.Pts[(i + 1) % p.Pts.Length]);
				Seg(x, y, x2, y2, Thick(p, k, cam.Scale), c, 255, Steps(p, _mp));
			}
			return;
		}
		}
		(float x, float y, float k) tuple5 = Project(cam, p.B);
		float item = tuple5.x;
		float item2 = tuple5.y;
		float num9 = Thick(p, num4, cam.Scale);
		int steps = Steps(p, _mp);
		if (p.Style == Style.Inside)
		{
			Seg(num2, num3, item, item2, num9, (r: 255, g: 85, b: 60), 255, steps);
			return;
		}
		(int, int, int) c3 = (p.Hit ? hitColor : Mul(StyleColor(p.Style), (p.Style >= Style.Incoming) ? 1f : num));
		if (_rich && !p.Hit && p.Kind == Kind.Bone)
		{
			Seg(num2, num3, item, item2, num9, Mul(c3, 0.62f), 255, steps);
			float aspect3 = UiDraw.Aspect;
			float num10 = (item - num2) * aspect3;
			float num11 = item2 - num3;
			float num12 = (float)Math.Sqrt(num10 * num10 + num11 * num11);
			if (num12 > 0.0001f)
			{
				float num13 = (0f - num11) / num12;
				float num14 = num10 / num12;
				if (num14 > 0f || (Math.Abs(num14) < 0.001f && num13 > 0f))
				{
					num13 = 0f - num13;
					num14 = 0f - num14;
				}
				float num15 = num9 * 0.2f;
				float num16 = num13 * num15 / aspect3;
				float num17 = num14 * num15;
				Seg(num2 + num16, num3 + num17, item + num16, item2 + num17, num9 * 0.5f, Mul(c3, 1.12f), 255, steps);
			}
		}
		else
		{
			Seg(num2, num3, item, item2, p.Hit ? (num9 * 1.25f) : num9, c3, 255, steps);
		}
	}

	private Cam CamAt(float yaw, float pitch, float zq, float vw, float vh)
	{
		double num = (double)(_baseYaw + yaw) * Math.PI / 180.0;
		double num2 = (double)pitch * Math.PI / 180.0;
		V3 v = new V3((float)Math.Sin(num), (float)Math.Cos(num), 0f);
		V3 v2 = v * (float)Math.Cos(num2) + new V3(0f, 0f, 0f - (float)Math.Sin(num2));
		V3 v3 = new V3(v.Y, 0f - v.X, 0f);
		V3 v4 = V3.Cross(v3, v2);
		V3 center = _center;
		float num3 = 0.05f;
		float num4 = 0.05f;
		foreach (V3 framePoint in _framePoints)
		{
			V3 a = framePoint - center;
			num3 = Math.Max(num3, Math.Abs(V3.Dot(a, v3)));
			num4 = Math.Max(num4, Math.Abs(V3.Dot(a, v4)));
		}
		float num5 = Math.Min(vh * 0.85f / (2f * num4 + 0.12f), vw * UiDraw.Aspect * 0.85f / (2f * num3 + 0.12f));
		return new Cam
		{
			C = center,
			S = v3,
			U = v4,
			F = v2,
			Scale = num5 * zq,
			Cx = 0.5f,
			Cy = 0.5f
		};
	}

	private List<Cam> PlanCams(float zq, float vw, float vh)
	{
		_costClip = (0.5f - vw / 2f, 0.5f - vh / 2f, 0.5f + vw / 2f, 0.5f + vh / 2f);
		List<Cam> list = new List<Cam>();
		float[] array = ((!(zq > 1f)) ? new float[1] { zq } : new float[2]
		{
			zq,
			Math.Max(0.6f, zq - 0.25f)
		});
		foreach (float zq2 in array)
		{
			for (int j = 0; j < 360; j += 30)
			{
				float[] array2 = new float[7] { -80f, -45f, -15f, 10f, 35f, 60f, 80f };
				foreach (float pitch in array2)
				{
					list.Add(CamAt(j, pitch, zq2, vw, vh));
				}
			}
		}
		return list;
	}

	private static int CoreWorstOf(List<Prim> prims, List<Cam> cams, int mp)
	{
		List<Prim> core = prims.Where((Prim p) => p.Priority <= 2).ToList();
		return cams.Max((Cam cam) => core.Sum((Prim p) => Cost(p, cam, mp) + FractureCost(p, cam, mp)));
	}

	private void ChooseAutoLevel(int budget, float zq, float vw, float vh)
	{
		List<Cam> cams = PlanCams(zq, vw, vh);
		ActiveLevel = XRayQuality.Low;
		XRayQuality[] array = new XRayQuality[2]
		{
			XRayQuality.High,
			XRayQuality.Medium
		};
		foreach (XRayQuality xRayQuality in array)
		{
			if (CoreWorstOf(_levels[xRayQuality], cams, 8) <= budget)
			{
				ActiveLevel = xRayQuality;
				break;
			}
		}
		_prims = _levels[ActiveLevel];
	}

	private void Plan(int budget, float zq, float vw, float vh)
	{
		List<Cam> cams = PlanCams(zq, vw, vh);
		List<Prim> core = _prims.Where((Prim prim) => prim.Priority <= 2).ToList();
		int num = 2;
		int num2 = 16;
		int best = 2;
		while (num <= num2)
		{
			int num3 = (num + num2) / 2;
			if (CoreWorst(num3) <= budget)
			{
				best = num3;
				num = num3 + 1;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		_planMp = best;
		int[] array = cams.Select((Cam cam) => core.Sum((Prim p2) => Cost(p2, cam, best) + FractureCost(p2, cam, best))).ToArray();
		int[] richCost = cams.Select((Cam cam) => core.Where((Prim prim) => !prim.Hit && prim.Kind == Kind.Bone).Sum((Prim p2) => HighlightCost(p2, cam, best)) + core.Count(BoneDot)).ToArray();
		_planRich = best >= 4 && array.Select((int t, int i) => t + richCost[i]).All((int t) => t <= budget);
		if (_planRich)
		{
			for (int num4 = 0; num4 < array.Length; num4++)
			{
				array[num4] += richCost[num4];
			}
		}
		_planned = new HashSet<Prim>(core);
		foreach (Prim p in from prim in _prims
			where prim.Priority > 2
			orderby prim.Priority
			select prim)
		{
			int[] array2 = cams.Select((Cam cam) => Cost(p, cam, best) + FractureCost(p, cam, best) + ((_planRich && BoneDot(p)) ? 1 : 0)).ToArray();
			bool flag = true;
			for (int num5 = 0; num5 < array.Length && flag; num5++)
			{
				flag = array[num5] + array2[num5] <= budget;
			}
			if (flag)
			{
				for (int num6 = 0; num6 < array.Length; num6++)
				{
					array[num6] += array2[num6];
				}
				_planned.Add(p);
			}
		}
		LastSpentEstimate = array.Max();
		LastRichCost = richCost.Max();
		static bool BoneDot(Prim prim)
		{
			if (prim.Kind == Kind.Dot)
			{
				return prim.Style == Style.Bone;
			}
			return false;
		}
		int CoreWorst(int mp)
		{
			return cams.Max((Cam cam) => core.Sum((Prim p2) => Cost(p2, cam, mp) + FractureCost(p2, cam, mp)));
		}
	}

	private static bool IsPath(Prim p)
	{
		if (p.Style >= Style.Incoming)
		{
			return p.Style <= Style.PelletExit;
		}
		return false;
	}

	private static int Steps(Prim p, int mp)
	{
		if (!IsPath(p))
		{
			if (p.Kind != Kind.Bone && p.Style != Style.Bone)
			{
				return mp;
			}
			return Math.Min(24, mp * 3);
		}
		if (p.Style != Style.Inside)
		{
			return 12;
		}
		return 24;
	}

	private bool Spend(int n)
	{
		if (UiDraw.RectsThisFrame - _start + n <= _budget)
		{
			return true;
		}
		LastCut++;
		return false;
	}

	private void Seg(float x0, float y0, float x1, float y1, float t, (int r, int g, int b) c, int a, int steps = 0)
	{
		if (UiDraw.Clip(ref x0, ref y0, ref x1, ref y1, _clip.x0, _clip.y0, _clip.x1, _clip.y1))
		{
			int maxPieces = ((steps > 0) ? steps : _mp);
			if (Spend(UiDraw.LineCost(x0, y0, x1, y1, t, maxPieces)))
			{
				UiDraw.Line(x0, y0, x1, y1, t, c.r, c.g, c.b, a, maxPieces);
			}
		}
	}

	private void StepCamera(float dt)
	{
		dt = Math.Min(0.1f, Math.Max(0f, dt));
		Yaw += (float)((_right ? 1 : 0) - (_left ? 1 : 0)) * 100f * dt + (AutoSpin ? (25f * dt) : 0f);
		Pitch = Math.Max(-80f, Math.Min(80f, Pitch + (float)((_up ? 1 : 0) - (_down ? 1 : 0)) * 70f * dt));
		Zoom = Math.Max(0.6f, Math.Min(5f, Zoom * (1f + (float)((_zoomIn ? 1 : 0) - (_zoomOut ? 1 : 0)) * 1.3f * dt)));
	}

	private static (int r, int g, int b) StyleColor(Style s)
	{
		switch (s)
		{
		case Style.Bone:
			return BoneColor;
		case Style.Fine:
			return FineColor;
		case Style.Cartilage:
			return CartilageColor;
		case Style.Vessel:
			return (r: 120, g: 55, b: 55);
		case Style.Incoming:
			return (r: 245, g: 205, b: 80);
		case Style.PelletIn:
			return (r: 215, g: 175, b: 70);
		case Style.Exit:
		case Style.PelletExit:
			return (r: 240, g: 140, b: 50);
		case Style.PelletInside:
			return (r: 230, g: 70, b: 50);
		default:
			return BoneColor;
		}
	}

	private static (int r, int g, int b) OrganColor(string key)
	{
		if (key == "heart")
		{
			return (r: 205, g: 50, b: 60);
		}
		if (key.StartsWith("lung"))
		{
			return (r: 195, g: 125, b: 145);
		}
		if (key == "liver")
		{
			return (r: 160, g: 75, b: 50);
		}
		if (key == "stomach")
		{
			return (r: 195, g: 145, b: 85);
		}
		if (key.StartsWith("kidney"))
		{
			return (r: 165, g: 75, b: 75);
		}
		return key switch
		{
			"intestines" => (r: 185, g: 145, b: 115), 
			"brain" => (r: 205, g: 175, b: 185), 
			"bladder" => (r: 175, g: 175, b: 95), 
			_ => (r: 205, g: 85, b: 85), 
		};
	}
}
