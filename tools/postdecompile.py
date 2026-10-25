"""Repairs applied to raw ILSpy output so it compiles and matches the shipped
NemesisMod.dll. Idempotent: run it on a fresh decompile (make decompile) or on
src/ again - already-applied edits are skipped.

usage: python3 -I tools/postdecompile.py <src-dir>

1. Tuple element names. ILSpy drops them on some local declarations and on
   explicit LINQ type arguments while the lambdas consuming them keep the
   names, which doesn't compile. Restore the names at the declaration.
2. Dead local functions. The original declares two local functions it never
   calls; ILSpy omits them. Restore them from the IL so the rebuild carries
   the same code (tools/AsmDiff shows them otherwise).
3. Compiler-emitted attributes ILSpy writes back as source.
"""
import re, sys, pathlib

root = pathlib.Path(sys.argv[1])

def sub(path, old, new, count=None, regex=False):
    p = root / path
    s = p.read_text(encoding="utf-8-sig")
    n = len(re.findall(old, s)) if regex else s.count(old)
    if n == 0:
        if new == "" or new in s:
            return  # already applied
        raise SystemExit(f"{path}: pattern not found: {old!r}")
    if count is not None and n != count:
        raise SystemExit(f"{path}: expected {count} matches, got {n}: {old!r}")
    s = re.sub(old, new, s) if regex else s.replace(old, new)
    p.write_text(s, encoding="utf-8")
    print(f"{path}: {n}x {old[:60]!r}")

M = "NemesisSystem/NemesisMod.cs"
sub("NemesisSystem/Crumbs.cs", "List<(long, string)> list2 = new List<(long, string)>();",
    "List<(long seq, string what)> list2 = new List<(long seq, string what)>();", 1)
sub(M, "List<(Ped, NemesisRecord)> list = new List<(Ped, NemesisRecord)>();",
    "List<(Ped ped, NemesisRecord rec)> list = new List<(Ped ped, NemesisRecord rec)>();", 1)
sub(M, "List<(string, ICollection<int>, Action<int>)> list = BookkeepingMaps()",
    "List<(string name, ICollection<int> keys, Action<int> remove)> list = BookkeepingMaps()", 1)
sub(M, "List<(string, ICollection<int>, Action<int>)> list2 = LifecycleMaps()",
    "List<(string name, ICollection<int> keys, Action<int> remove)> list2 = LifecycleMaps()", 1)
sub(M, "List<(Ped, int, float, TownMan)> list = new List<(Ped, int, float, TownMan)>();",
    "List<(Ped p, int tier, float d, TownMan tm)> list = new List<(Ped p, int tier, float d, TownMan tm)>();", 1)
sub(M, "List<(string, int)> list2 = new List<(string, int)>();",
    "List<(string t, int r)> list2 = new List<(string t, int r)>();")
sub(M, "g.Seed?.Where<(int, int, PedHash[])>(", "g.Seed?.Where(", 1)
sub(M, "List<(NemesisRecord, string)> list = (from k in KinOf(d)",
    "List<(NemesisRecord who, string rel)> list = (from k in KinOf(d)", 1)
sub(M, "(string, SavePointMeta)", "(string dir, SavePointMeta m)")
sub(M, "List<(string, int, int)> list2 = (from k in ChapterRegard.Keys",
    "List<(string k, int a, int b)> list2 = (from k in ChapterRegard.Keys", 1)
sub(M, "List<(NemesisRecord, string)> list3 = new List<(NemesisRecord, string)>();",
    "List<(NemesisRecord rec, string label)> list3 = new List<(NemesisRecord rec, string label)>();", 1)
sub(M, "list.OrderBy<(int, float), float>(", "list.OrderBy(")
sub(M, "List<(int, float)> list = new List<(int, float)>();\n\t\t\t\tforeach (KeyValuePair<int, Named> item in _named)",
    "List<(int h, float score)> list = new List<(int h, float score)>();\n\t\t\t\tforeach (KeyValuePair<int, Named> item in _named)", 1)
sub(M, "List<KeyValuePair<string, (int, bool)>> list = (from x in _missionCast",
    "List<KeyValuePair<string, (int seen, bool gang)>> list = (from x in _missionCast", 1)
sub(M, "List<(District, Gang, Gang, double)> list = new List<(District, Gang, Gang, double)>();",
    "List<(District d, Gang a, Gang b, double pct)> list = new List<(District d, Gang a, Gang b, double pct)>();", 1)
sub(M, "List<(int, Acquaintance, int, string)> list = _recentWitnesses",
    "List<(int handle, Acquaintance a, int at, string crime)> list = _recentWitnesses", 1)
sub("NemesisSystem/NemesisSettings.cs", "List<(string, string)> list = new List<(string, string)>();",
    "List<(string sec, string line)> list = new List<(string sec, string line)>();", 1)

# Explicit LINQ type arguments spelled without names strip the names from the
# result sequence; let inference carry the lambda's named tuple through instead.
for path, needles in {
    "NemesisSystem/Crumbs.cs": ["list2.OrderBy<(long, string), long>"],
    M: ["list2.Any<(string, ICollection<int>, Action<int>)>",
        "list2.OrderBy<(string, int), int>",
        "list2.Select<(string, int, int), string>",
        "list2.OrderByDescending<(string, int, int), int>",
        "list.OrderByDescending<(District, Gang, Gang, double), double>",
        "list.OrderBy<(District, Gang, Gang, double), int>"],
}.items():
    for needle in needles:
        sub(path, needle + "(", needle.split("<")[0] + "(")

# 2. Dead local functions (reconstructed from IL; never called in 0.9.5.3).
sub("NemesisSystem/Traits.cs", """		return Also(ManFromModel(m, rng), MenAlso, rng);
	}
""", """		return Also(ManFromModel(m, rng), MenAlso, rng);
		// Unused in 0.9.5.3 (restored from IL - ILSpy drops uncalled local functions).
		bool Has(string[] t)
		{
			return t.Any(m.Contains);
		}
	}
""", 1)
sub("NemesisSystem/RosterMenu.cs", """		void MoodRow(string text11, string text12, string text13, string left, (int r, int g, int b) tuple3)
""", """		// Unused in 0.9.5.3 (restored from IL - ILSpy drops uncalled local functions).
		void Meter(float x, int value, (int r, int g, int b) c)
		{
			float num = x + 0.095f;
			float num2 = 0.275f;
			UiDraw.Rect(num, y - 0.005f, num2, 0.0037037036f, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 230);
			UiDraw.Rect(num, y - 0.005f, num2 * (float)Math.Max(0, Math.Min(100, value)) / 100f, 0.0037037036f, c.r, c.g, c.b, 255);
			y += 0.008f;
		}
		void MoodRow(string text11, string text12, string text13, string left, (int r, int g, int b) tuple3)
""", 1)

# 3. The compiler emits [module: RefSafetyRules(11)] itself; source can't declare it.
sub("Properties/AssemblyInfo.cs", "[module: RefSafetyRules(11)]\n", "")
