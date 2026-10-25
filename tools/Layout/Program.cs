// Layout: recovers the source declaration order of one type's members from a
// release build, using the member ordinals Roslyn bakes into compiler-generated
// names:  <M>b__ORD_k (lambdas), <M>g__F|ORD_k (local functions), <M>d__ORD
// (iterators/async), <>c__DisplayClassORD_k (closures, whose lambdas name M).
// ORD is M's index in the type's declaration-ordered member list, so
// ORD - (methods declared before M) = non-method members (fields, properties,
// events, nested types) declared before M.
//
// usage: Layout <assembly.dll> <Namespace.Type>              method order
//        Layout <assembly.dll> <Namespace.Type> --cctor-deps  init dependencies
// method order: one TSV row per method in metadata (= declaration) order:
//   index  name  ordinal|-  nonMethodsBefore|-
// --cctor-deps: "Field<TAB>ReadsField" for every static field of the type that
//   the static constructor reads (directly, or through own-type methods and
//   lambdas it calls, transitively) before assigning Field. Partial-class field
//   initializers run in file order, so each pair must stay in one file, in order.
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

var pe = new PEReader(File.OpenRead(args[0]));
var md = pe.GetMetadataReader();
TypeDefinition? target = null; TypeDefinitionHandle targetH = default;
foreach (var h in md.TypeDefinitions)
{
    var t = md.GetTypeDefinition(h);
    if (!t.GetDeclaringType().IsNil) continue;
    if (md.GetString(t.Namespace) + "." + md.GetString(t.Name) == args[1]) { target = t; targetH = h; }
}
if (target is null) { Console.Error.WriteLine("type not found"); return 2; }
var T = target.Value;

if (args.Contains("--cctor-deps")) return CctorDeps.Run(pe, md, targetH);

var ordOf = new Dictionary<string, int>();
void Note(string method, int ord)
{
    if (ordOf.TryGetValue(method, out int o) && o != ord) ordOf[method] = -1; // overloads: ambiguous
    else ordOf[method] = ord;
}
var lambda = new Regex(@"^<(?<m>[^>]+)>b__(?<o>\d+)_\d+$");
var local = new Regex(@"^<(?<m>[^>]+)>g__.+\|(?<o>\d+)_\d+$");
var machine = new Regex(@"^<(?<m>[^>]+)>d__(?<o>\d+)$");
var display = new Regex(@"^<>c__DisplayClass(?<o>\d+)_\d+$");
var inner = new Regex(@"^<(?<m>[^>]+)>b__\d+$");

void Scan(TypeDefinition t)
{
    foreach (var mh in t.GetMethods())
    {
        string n = md.GetString(md.GetMethodDefinition(mh).Name);
        foreach (var re in new[] { lambda, local })
        { var m = re.Match(n); if (m.Success) Note(m.Groups["m"].Value, int.Parse(m.Groups["o"].Value)); }
    }
    foreach (var nh in t.GetNestedTypes())
    {
        var nt = md.GetTypeDefinition(nh); string nn = md.GetString(nt.Name);
        var sm = machine.Match(nn);
        if (sm.Success) Note(sm.Groups["m"].Value, int.Parse(sm.Groups["o"].Value));
        var dm = display.Match(nn);
        if (dm.Success)
            foreach (var mh in nt.GetMethods())
            {
                var im = inner.Match(md.GetString(md.GetMethodDefinition(mh).Name));
                if (im.Success) Note(im.Groups["m"].Value, int.Parse(dm.Groups["o"].Value));
            }
        if (nn.StartsWith("<")) Scan(nt); // <>c, display classes
    }
}
Scan(T);

var names = T.GetMethods().Select(mh => md.GetString(md.GetMethodDefinition(mh).Name))
    .Where(n => !n.StartsWith("<")).ToList(); // skip compiler-generated (hoisted lambdas, local functions)
// An overloaded name can't say which overload owns the ordinal.
var overloaded = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
var raw = names.Select((n, i) => !overloaded.Contains(n) && ordOf.TryGetValue(n, out int o) && o >= 0 ? o - i : -1).ToArray();
// Keep the longest non-decreasing run of anchors; anything else is a misattribution.
var anchored = Enumerable.Range(0, names.Count).Where(i => raw[i] >= 0).ToList();
var best = new int[anchored.Count]; var prev = new int[anchored.Count];
for (int x = 0; x < anchored.Count; x++)
{
    best[x] = 1; prev[x] = -1;
    for (int y = 0; y < x; y++)
        if (raw[anchored[y]] <= raw[anchored[x]] && best[y] + 1 > best[x]) { best[x] = best[y] + 1; prev[x] = y; }
}
var keep = new HashSet<int>();
for (int x = anchored.Count == 0 ? -1 : Array.IndexOf(best, best.Max()); x >= 0; x = prev[x]) keep.Add(anchored[x]);
for (int i = 0; i < names.Count; i++)
{
    bool k = keep.Contains(i);
    Console.WriteLine($"{i}\t{names[i]}\t{(k ? (raw[i] + i).ToString() : "-")}\t{(k ? raw[i].ToString() : "-")}");
}
int index = names.Count;
Console.Error.WriteLine($"dropped {anchored.Count - keep.Count} non-monotonic anchors, {overloaded.Count} overloaded names");
int fields = T.GetFields().Count(f => !md.GetString(md.GetFieldDefinition(f).Name).StartsWith("<"));
Console.Error.WriteLine($"methods {index}, fields {fields}, properties {T.GetProperties().Count}, events {T.GetEvents().Count}, nested user types {T.GetNestedTypes().Count(h => !md.GetString(md.GetTypeDefinition(h).Name).StartsWith("<"))}, anchored {keep.Count}");
return 0;

static class CctorDeps
{
    public static int Run(PEReader pe, MetadataReader md, TypeDefinitionHandle typeH)
    {
        var self = new HashSet<TypeDefinitionHandle>();
        void Collect(TypeDefinitionHandle h) { self.Add(h); foreach (var n in md.GetTypeDefinition(h).GetNestedTypes()) Collect(n); }
        Collect(typeH); // the type and its compiler-generated nested types (<>c, closures)
        var cctor = md.GetTypeDefinition(typeH).GetMethods().First(m => md.GetString(md.GetMethodDefinition(m).Name) == ".cctor");
        var memo = new Dictionary<MethodDefinitionHandle, HashSet<string>>();

        HashSet<string> Reads(MethodDefinitionHandle mh, HashSet<MethodDefinitionHandle> stack)
        {
            if (memo.TryGetValue(mh, out var done)) return done;
            var r = new HashSet<string>();
            if (!stack.Add(mh)) return r;
            foreach (var (op, tok) in Ops(mh))
            {
                if (op == 0x7E && Own(tok, out var f)) r.Add(f);                       // ldsfld
                else if ((op == 0x28 || op == 0x6F || op == 0x73 || op == 0xFE06) && OwnMethod(tok, out var callee))
                    r.UnionWith(Reads(callee, stack));                                  // call/callvirt/newobj/ldftn
            }
            stack.Remove(mh);
            return memo[mh] = r;
        }

        var seen = new HashSet<string>(); var pending = new HashSet<string>();
        foreach (var (op, tok) in Ops(cctor))
        {
            if (op == 0x7E && Own(tok, out var f)) pending.Add(f);
            else if ((op == 0x28 || op == 0x6F || op == 0x73 || op == 0xFE06) && OwnMethod(tok, out var callee))
                pending.UnionWith(Reads(callee, new()));
            else if (op == 0x80 && Own(tok, out var stored))                             // stsfld
            {
                foreach (var dep in pending.Where(d => d != stored).OrderBy(d => d)) Console.WriteLine($"{stored}\t{dep}{(seen.Contains(dep) ? "" : "\t(read before assigned!)")}");
                pending.Clear(); seen.Add(stored);
            }
        }
        return 0;

        bool Own(int tok, out string name)
        {
            name = "";
            var h = MetadataTokens.EntityHandle(tok);
            if (h.Kind != HandleKind.FieldDefinition) return false;
            var fd = md.GetFieldDefinition((FieldDefinitionHandle)h);
            if (fd.GetDeclaringType() != typeH) return false;
            name = md.GetString(fd.Name);
            return !name.StartsWith("<");
        }
        bool OwnMethod(int tok, out MethodDefinitionHandle mh)
        {
            mh = default;
            var h = MetadataTokens.EntityHandle(tok);
            if (h.Kind == HandleKind.MethodSpecification) h = md.GetMethodSpecification((MethodSpecificationHandle)h).Method;
            if (h.Kind != HandleKind.MethodDefinition) return false;
            mh = (MethodDefinitionHandle)h;
            return self.Contains(md.GetMethodDefinition(mh).GetDeclaringType());
        }
        IEnumerable<(int op, int tok)> Ops(MethodDefinitionHandle mh)
        {
            var m = md.GetMethodDefinition(mh);
            if (m.RelativeVirtualAddress == 0) yield break;
            var r = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader();
            var list = new List<(int, int)>();
            while (r.RemainingBytes > 0)
            {
                int op = r.ReadByte();
                if (op == 0xFE) op = 0xFE00 | r.ReadByte();
                int size = OperandSize(op, ref r);
                int tok = size == 4 && IsTokenOp(op) ? r.ReadInt32() : 0;
                if (!(size == 4 && IsTokenOp(op))) r.Offset += size;
                list.Add((op, tok));
            }
            foreach (var x in list) yield return x;
        }
    }

    static bool IsTokenOp(int op) => op is 0x28 or 0x6F or 0x73 or 0x7B or 0x7C or 0x7D or 0x7E or 0x7F or 0x80 or 0xFE06 or 0xFE07 or 0x74 or 0x75 or 0x8C or 0x8D or 0x79 or 0xA5 or 0x70 or 0xD0 or 0x72 or 0x29 or 0xC2 or 0xC6 or 0x71 or 0x81 or 0xA3 or 0xA4 or 0x8F or 0xFE15 or 0xFE16 or 0xFE1C or 0x27;

    static readonly Dictionary<short, System.Reflection.Emit.OperandType> Kinds = typeof(System.Reflection.Emit.OpCodes)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)!).ToDictionary(o => o.Value, o => o.OperandType);

    static int OperandSize(int op, ref BlobReader r)
    {
        switch (Kinds[unchecked((short)op)])
        {
            case System.Reflection.Emit.OperandType.InlineNone: return 0;
            case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
            case System.Reflection.Emit.OperandType.ShortInlineI:
            case System.Reflection.Emit.OperandType.ShortInlineVar: return 1;
            case System.Reflection.Emit.OperandType.InlineVar: return 2;
            case System.Reflection.Emit.OperandType.InlineI8:
            case System.Reflection.Emit.OperandType.InlineR: return 8;
            case System.Reflection.Emit.OperandType.InlineSwitch:
                { int n = r.ReadInt32(); return 4 * n; }
            default: return 4;
        }
    }
}
