// AsmDiff: compares two builds of a managed assembly without loading them.
//
//   surface  types, fields, methods, properties, events and their signatures
//            (compiler-generated members excluded)
//   body     per user type, the multiset of members its IL calls/reads and the
//            string literals it loads - lambdas, iterators and async state
//            machines are folded into the user type that owns them, so closure
//            numbering differences between compilers don't count
//
// usage: AsmDiff <original.dll> <rebuilt.dll> [--verbose] [--by-method]
//   --by-method  key bodies by user method instead of user type (lambdas and
//                local functions fold into the method that declares them)
// exit 0 = surface identical and no body differences, 1 = differences found
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

if (args.Length < 2) { Console.Error.WriteLine("usage: AsmDiff <original.dll> <rebuilt.dll> [--verbose] [--by-method]"); return 2; }
bool verbose = args.Contains("--verbose");
bool byMethod = args.Contains("--by-method");
var a = Dump.Load(args[0], byMethod);
var b = Dump.Load(args[1], byMethod);

int problems = 0;
var onlyA = a.Surface.Except(b.Surface).OrderBy(x => x).ToList();
var onlyB = b.Surface.Except(a.Surface).OrderBy(x => x).ToList();
Console.WriteLine($"surface: {a.Surface.Count} members in original, {b.Surface.Count} in rebuilt");
foreach (var s in onlyA) Console.WriteLine($"  - {s}");
foreach (var s in onlyB) Console.WriteLine($"  + {s}");
problems += onlyA.Count + onlyB.Count;

int typesDiffer = 0;
foreach (var t in a.Bodies.Keys.Union(b.Bodies.Keys).OrderBy(x => x))
{
    a.Bodies.TryGetValue(t, out var ba); b.Bodies.TryGetValue(t, out var bb);
    ba ??= new(); bb ??= new();
    var lost = Ms.Minus(ba, bb); var gained = Ms.Minus(bb, ba);
    if (lost.Count == 0 && gained.Count == 0) continue;
    typesDiffer++;
    Console.WriteLine($"body: {t}  (-{lost.Values.Sum()} +{gained.Values.Sum()})");
    if (verbose || lost.Count + gained.Count <= 12)
    {
        foreach (var (k, n) in lost.OrderBy(x => x.Key)) Console.WriteLine($"    - {n}x {k}");
        foreach (var (k, n) in gained.OrderBy(x => x.Key)) Console.WriteLine($"    + {n}x {k}");
    }
}
Console.WriteLine($"bodies: {a.Bodies.Count} user {(byMethod ? "methods" : "types")} compared, {typesDiffer} differ");
problems += typesDiffer;
Console.WriteLine(problems == 0 ? "OK: equivalent at surface and call/string level" : $"DIFFERENCES: {problems}");
return problems == 0 ? 0 : 1;

static class Ms
{
    public static Dictionary<string, int> Minus(Dictionary<string, int> x, Dictionary<string, int> y)
    {
        var r = new Dictionary<string, int>();
        foreach (var (k, n) in x) { int m = n - y.GetValueOrDefault(k); if (m > 0) r[k] = m; }
        return r;
    }
}

sealed class Dump
{
    public HashSet<string> Surface = new();
    public Dictionary<string, Dictionary<string, int>> Bodies = new();
    MetadataReader md = null!;
    PEReader pe = null!;
    readonly SigNames sig = new();
    bool byMethod;

    static readonly Dictionary<short, OperandType> Operands = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value, o => o.OperandType);

    public static Dump Load(string path, bool byMethod = false)
    {
        var d = new Dump { byMethod = byMethod };
        d.pe = new PEReader(File.OpenRead(path));
        d.md = d.pe.GetMetadataReader();
        d.sig.Md = d.md;
        d.Walk();
        return d;
    }

    static bool Generated(string name) => name.Contains('<') || name.Contains('>') || name.StartsWith("__StaticArrayInit");

    string TypeName(TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h);
        string n = md.GetString(t.Name);
        var outer = t.GetDeclaringType();
        if (!outer.IsNil) return TypeName(outer) + "/" + n;
        string ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? n : ns + "." + n;
    }

    // The nearest enclosing type that the user wrote (closures fold into it).
    string OwnerName(TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h);
        while (Generated(md.GetString(t.Name)) && !t.GetDeclaringType().IsNil) { h = t.GetDeclaringType(); t = md.GetTypeDefinition(h); }
        return TypeName(h);
    }

    void Walk()
    {
        foreach (var th in md.TypeDefinitions)
        {
            var t = md.GetTypeDefinition(th);
            string tn = TypeName(th);
            bool gen = tn.Split('/', '.').Any(Generated);
            if (!gen)
            {
                Surface.Add($"type {tn} : {(t.BaseType.IsNil ? "-" : sig.Name(t.BaseType))} [{t.Attributes & (TypeAttributes.VisibilityMask | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Interface)}]");
                foreach (var ih in t.GetInterfaceImplementations())
                    Surface.Add($"impl {tn} : {sig.Name(md.GetInterfaceImplementation(ih).Interface)}");
                foreach (var fh in t.GetFields())
                {
                    var f = md.GetFieldDefinition(fh); string fn = md.GetString(f.Name);
                    if (Generated(fn)) continue;
                    Surface.Add($"field {tn}::{fn} : {f.DecodeSignature(sig, null)} [{f.Attributes & (FieldAttributes.FieldAccessMask | FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Literal)}]");
                }
                foreach (var ph in t.GetProperties())
                {
                    var p = md.GetPropertyDefinition(ph);
                    Surface.Add($"prop {tn}::{md.GetString(p.Name)} : {p.DecodeSignature(sig, null).ReturnType}");
                }
                foreach (var eh in t.GetEvents())
                {
                    var e = md.GetEventDefinition(eh);
                    Surface.Add($"event {tn}::{md.GetString(e.Name)} : {sig.Name(e.Type)}");
                }
            }
            string owner = OwnerName(th);
            foreach (var mh in t.GetMethods())
            {
                var m = md.GetMethodDefinition(mh); string mn = md.GetString(m.Name);
                string key = byMethod ? owner + "::" + UserMethod(th, mn) : owner;
                if (!Bodies.TryGetValue(key, out var bag)) Bodies[key] = bag = new();
                if (!gen && !Generated(mn))
                {
                    var s = m.DecodeSignature(sig, null);
                    Surface.Add($"method {tn}::{mn}({string.Join(", ", s.ParameterTypes)}) : {s.ReturnType} [{m.Attributes & (MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.Virtual | MethodAttributes.Abstract)}]");
                }
                if (m.RelativeVirtualAddress != 0) ScanBody(m, bag);
            }
        }
    }

    void ScanBody(MethodDefinition m, Dictionary<string, int> bag)
    {
        var body = pe.GetMethodBody(m.RelativeVirtualAddress);
        var r = body.GetILReader();
        while (r.RemainingBytes > 0)
        {
            short code = r.ReadByte();
            if (code == 0xFE) code = unchecked((short)(0xFE00 | r.ReadByte()));
            switch (Operands[code])
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: r.ReadByte(); break;
                case OperandType.InlineVar: r.ReadInt16(); break;
                case OperandType.InlineI8: case OperandType.InlineR: r.ReadInt64(); break;
                case OperandType.ShortInlineR: case OperandType.InlineI: case OperandType.InlineBrTarget: r.ReadInt32(); break;
                case OperandType.InlineSwitch: int n = r.ReadInt32(); for (int i = 0; i < n; i++) r.ReadInt32(); break;
                case OperandType.InlineString:
                    string str = md.GetUserString(MetadataTokens.UserStringHandle(r.ReadInt32()));
                    Add(bag, "str " + str); break;
                case OperandType.InlineMethod: case OperandType.InlineField: case OperandType.InlineTok: case OperandType.InlineType: case OperandType.InlineSig:
                    int tok = r.ReadInt32();
                    if (Operands[code] is OperandType.InlineMethod or OperandType.InlineField)
                    {
                        string target = MemberName(MetadataTokens.EntityHandle(tok));
                        if (!target.Contains("<")) Add(bag, (Operands[code] == OperandType.InlineMethod ? "call " : "fld ") + target);
                    }
                    break;
                default: throw new InvalidOperationException($"opcode {code:x}");
            }
        }
    }

    // "<Foo>b__12_0" / "<Foo>g__Local|3_0" / state machine "<Foo>d__7" -> "Foo"
    static string Unmangle(string n) => n.StartsWith('<') && n.IndexOf('>') > 1 ? n[1..n.IndexOf('>')] : n;

    string UserMethod(TypeDefinitionHandle th, string mn)
    {
        if (mn.StartsWith('<') && mn.IndexOf('>') > 1) return Unmangle(mn);
        var t = md.GetTypeDefinition(th); string tn = md.GetString(t.Name);
        // methods of a state machine (<Foo>d__7.MoveNext) belong to Foo
        if (tn.StartsWith('<') && tn.IndexOf('>') > 1 && !tn.StartsWith("<>")) return Unmangle(tn);
        if (tn.StartsWith("<>")) return "(closure)." + mn;
        return mn;
    }

    static void Add(Dictionary<string, int> bag, string k) => bag[k] = bag.GetValueOrDefault(k) + 1;

    string MemberName(EntityHandle h)
    {
        switch (h.Kind)
        {
            case HandleKind.MethodDefinition:
                { var m = md.GetMethodDefinition((MethodDefinitionHandle)h); var s = m.DecodeSignature(sig, null);
                  return $"{TypeName(m.GetDeclaringType())}::{md.GetString(m.Name)}({string.Join(", ", s.ParameterTypes)})"; }
            case HandleKind.FieldDefinition:
                { var f = md.GetFieldDefinition((FieldDefinitionHandle)h); return $"{TypeName(f.GetDeclaringType())}::{md.GetString(f.Name)}"; }
            case HandleKind.MemberReference:
                { var mr = md.GetMemberReference((MemberReferenceHandle)h); string parent = sig.Name(mr.Parent);
                  if (mr.GetKind() == MemberReferenceKind.Method)
                  { var s = mr.DecodeMethodSignature(sig, null); return $"{parent}::{md.GetString(mr.Name)}({string.Join(", ", s.ParameterTypes)})"; }
                  return $"{parent}::{md.GetString(mr.Name)}"; }
            case HandleKind.MethodSpecification:
                { var ms = md.GetMethodSpecification((MethodSpecificationHandle)h);
                  return MemberName(ms.Method) + "<" + string.Join(", ", ms.DecodeSignature(sig, null)) + ">"; }
            default: return h.Kind.ToString();
        }
    }
}

sealed class SigNames : ISignatureTypeProvider<string, object?>
{
    public MetadataReader Md = null!;
    public string Name(EntityHandle h) => h.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeFromDefinition(Md, (TypeDefinitionHandle)h, 0),
        HandleKind.TypeReference => GetTypeFromReference(Md, (TypeReferenceHandle)h, 0),
        HandleKind.TypeSpecification => Md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(this, null),
        HandleKind.MethodDefinition => "method",
        HandleKind.ModuleReference => "module",
        _ => h.Kind.ToString(),
    };
    public string GetArrayType(string e, ArrayShape s) => e + "[" + new string(',', s.Rank - 1) + "]";
    public string GetByReferenceType(string e) => e + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) => $"fnptr({string.Join(",", s.ParameterTypes)}):{s.ReturnType}";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(", ", a) + ">";
    public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object? c, int i) => "!" + i;
    public string GetModifiedType(string mod, string u, bool req) => u;
    public string GetPinnedType(string e) => e;
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k)
    {
        var t = r.GetTypeDefinition(h); string n = r.GetString(t.Name);
        if (!t.GetDeclaringType().IsNil) return GetTypeFromDefinition(r, t.GetDeclaringType(), k) + "/" + n;
        string ns = r.GetString(t.Namespace); return ns.Length == 0 ? n : ns + "." + n;
    }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k)
    {
        var t = r.GetTypeReference(h); string n = r.GetString(t.Name);
        if (t.ResolutionScope.Kind == HandleKind.TypeReference) return GetTypeFromReference(r, (TypeReferenceHandle)t.ResolutionScope, k) + "/" + n;
        string ns = r.GetString(t.Namespace); return ns.Length == 0 ? n : ns + "." + n;
    }
}
