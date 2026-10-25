using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem;

public static class Ray
{
	public static bool Blocked(Vector3 from, Vector3 to, int flags, int ignore)
	{
		Vector3 at;
		return Hit(from, to, flags, ignore, out at);
	}

	public unsafe static bool Hit(Vector3 from, Vector3 to, int flags, int ignore, out Vector3 at)
	{
		int shapeTestHandle = SHAPETEST.START_EXPENSIVE_SYNCHRONOUS_SHAPE_TEST_LOS_PROBE(from.X, from.Y, from.Z, to.X, to.Y, to.Z, flags, ignore, 7);
		int num = 0;
		ulong* ptr = stackalloc ulong[4];
		ulong* surfaceNormal = stackalloc ulong[4];
		int num2 = 0;
		SHAPETEST.GET_SHAPE_TEST_RESULT(shapeTestHandle, (bool*)(&num), (Vector3*)ptr, (Vector3*)surfaceNormal, &num2);
		at = ((num != 0) ? new Vector3(*(float*)ptr, *(float*)(ptr + 1), *(float*)(ptr + 2)) : to);
		return num != 0;
	}
}
