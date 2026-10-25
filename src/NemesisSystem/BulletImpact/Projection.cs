using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem.BulletImpact;

internal static class Projection
{
	public unsafe static bool ToScreen(Vector3 w, out float x, out float y)
	{
		float num = default(float);
		float num2 = default(float);
		bool result = GRAPHICS.GET_SCREEN_COORD_FROM_WORLD_COORD(w.X, w.Y, w.Z, &num, &num2);
		x = num;
		y = num2;
		return result;
	}
}
