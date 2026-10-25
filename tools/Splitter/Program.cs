// Splitter: breaks the decompiled NemesisMod class (one ~86k-line file) into
// partial-class files, one per feature, following the layout recovered from
// the shipped DLL.
//
// usage: Splitter <NemesisMod.cs> <layout.tsv> <cctor-deps.tsv> <map> <outDir>
//
//   layout.tsv      tools/Layout output for the original DLL (method order and
//                   the field-block positions behind each method)
//   cctor-deps.tsv  tools/Layout --cctor-deps (static init dependencies)
//   map             nemesismod.map: topic -> first method of each file
//
// Methods and properties go where the map puts them. Fields and nested types
// keep their declaration order and are cut into the same file sequence by a
// dynamic program that maximises how often a file uses what it declares,
// held to the field-block counts recovered from the DLL. The static
// constructor's field assignments (ILSpy gathers every static initializer
// there) go back onto the field declarations, with fields that read another
// field during static init kept in the same file, after it.
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 5) { Console.Error.WriteLine("usage: Splitter <NemesisMod.cs> <layout.tsv> <cctor-deps.tsv> <map> <outDir>"); return 2; }
var (srcPath, layoutPath, depsPath, mapPath, outDir) = (args[0], args[1], args[2], args[3], args[4]);

string text = File.ReadAllText(srcPath);
var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp12)).GetRoot();
var cls = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "NemesisMod" && c.Parent is BaseNamespaceDeclarationSyntax);
if (cls.AttributeLists.Count > 0) throw new Exception("class attributes not handled");
string header = text[..cls.FullSpan.Start].Replace("\r\n", "\n").TrimEnd('\n') + "\n\n";

// ---- layout ---------------------------------------------------------------
var layout = File.ReadAllLines(layoutPath).Select(l => l.Split('\t'))
    .Select(p => (Index: int.Parse(p[0]), Name: p[1], F: p[3] == "-" ? (int?)null : int.Parse(p[3]))).ToList();

// ---- members --------------------------------------------------------------
var members = cls.Members.Select((m, i) => new Member(m, i)).ToList();
var props = members.Where(m => m.Kind == Kind.Property).ToList();
var accessors = props.SelectMany(p => new[] { "get_" + p.Names[0], "set_" + p.Names[0] }).ToHashSet();
var metaMethods = layout.Where(r => !accessors.Contains(r.Name)).ToList();
var srcMethods = members.Where(m => m.Kind == Kind.Method).ToList();
for (int i = 0; i < Math.Max(metaMethods.Count, srcMethods.Count); i++)
{
    string a = i < metaMethods.Count ? metaMethods[i].Name : "<end>", b = i < srcMethods.Count ? srcMethods[i].Names[0] : "<end>";
    if (a != b) throw new Exception($"method #{i}: layout has {a}, source has {b} - decompile and layout disagree");
}

// ---- map: segments in file order ----------------------------------------------
var segTopic = new List<string>(); var segStart = new List<int>(); var overrides = new List<(string topic, string name)>();
{
    int pos = 0;
    foreach (var raw in File.ReadAllLines(mapPath))
    {
        var line = raw.Split('#')[0].Trim();
        if (line.Length == 0) continue;
        var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (p[0] == "=") { overrides.Add((p[1], p[2])); continue; }
        int at = layout.FindIndex(pos, r => r.Name == p[1]);
        if (at < 0) throw new Exception($"map: {p[1]} not found after row {pos}");
        if (segStart.Count == 0 && at != 0) throw new Exception("map: first file must start at the first method");
        segTopic.Add(p[0]); segStart.Add(at); pos = at + 1;
    }
}
int K = segTopic.Count;
int SegOfRow(int row) { int s = segStart.BinarySearch(row); return s >= 0 ? s : ~s - 1; }

var seg = new Dictionary<Member, int>();
for (int i = 0; i < srcMethods.Count; i++) seg[srcMethods[i]] = SegOfRow(metaMethods[i].Index);
foreach (var p in props)
{
    int row = layout.FindIndex(r => r.Name == "get_" + p.Names[0] || r.Name == "set_" + p.Names[0]);
    if (row < 0) throw new Exception($"no accessor for property {p.Names[0]}");
    seg[p] = SegOfRow(row);
}
foreach (var (topic, name) in overrides)
{
    int s = segTopic.IndexOf(topic);
    if (s < 0) throw new Exception($"override: unknown topic {topic}");
    var hit = members.Where(m => (m.Kind is Kind.Method or Kind.Property) && m.Names[0] == name).ToList();
    if (hit.Count == 0) throw new Exception($"override: no method or property {name}");
    foreach (var m in hit) seg[m] = s;
}

// ---- static constructor -> field initializers ----------------------------------
var staticInit = new Dictionary<string, ExpressionSyntax>();
Member? cctorMember = srcMethods.FirstOrDefault(m => m.Names[0] == ".cctor");
ConstructorDeclarationSyntax? newCctor = null;
var fieldDecl = members.Where(m => m.Kind == Kind.Field).SelectMany(m => m.Names.Select(n => (n, m))).ToDictionary(x => x.n, x => x.m);
if (cctorMember != null)
{
    var ctor = (ConstructorDeclarationSyntax)cctorMember.Node;
    var keep = new List<StatementSyntax>();
    bool moving = true;
    foreach (var st in ctor.Body!.Statements)
    {
        // only a leading run of plain assignments can move: a statement in between
        // would otherwise start running after fields it used to run before
        if (moving && st is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax asg } && asg.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && asg.Left is IdentifierNameSyntax id && fieldDecl.TryGetValue(id.Identifier.Text, out var fm)
            && !staticInit.ContainsKey(id.Identifier.Text)
            && ((FieldDeclarationSyntax)fm.Node).Declaration.Variables.First(v => v.Identifier.Text == id.Identifier.Text).Initializer == null)
            staticInit[id.Identifier.Text] = asg.Right;
        else { moving = false; keep.Add(st); }
    }
    newCctor = ctor.WithBody(ctor.Body.WithStatements(SyntaxFactory.List(keep)));
}

// ---- fields and nested types: monotone cut into segments --------------------------
var usage = new Dictionary<string, int>[K];
for (int s = 0; s < K; s++) usage[s] = new();
foreach (var (m, s) in seg)
    foreach (var t in m.Node.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)))
        usage[s][t.Text] = usage[s].GetValueOrDefault(t.Text) + 1;
// a static initializer is used where its field ends up; count it with the field's users
var types = members.Where(m => m.Kind == Kind.Type).ToList();
var fields = members.Where(m => m.Kind == Kind.Field).ToList();

// Bounds on how many non-method members (fields, properties, nested types) are
// declared before each segment, from the anchors of the methods around it.
var lo = new int[K + 1]; var hi = new int[K + 1];
{
    var minF = new int?[K]; var maxF = new int?[K];
    foreach (var r in layout.Where(r => r.F != null))
    {
        int s = SegOfRow(r.Index);
        minF[s] = Math.Min(minF[s] ?? int.MaxValue, r.F!.Value); maxF[s] = Math.Max(maxF[s] ?? int.MinValue, r.F!.Value);
    }
    int total = fields.Sum(f => f.Names.Length) + props.Count + types.Count;
    int run = 0;
    for (int s = 0; s <= K; s++) { lo[s] = run; if (s < K && maxF[s] != null) run = Math.Max(run, maxF[s]!.Value); }
    run = total;
    for (int s = K; s >= 0; s--) { if (s < K && minF[s] != null) run = Math.Min(run, minF[s]!.Value); hi[s] = run; }
}
var propsBefore = new int[K + 1];
foreach (var p in props) for (int s = seg[p] + 1; s <= K; s++) propsBefore[s]++;

int[] Cut(List<Member> items, int[] otherBefore, double penalty)
{
    int n = items.Count;
    var w = items.Select(m => m.Names.Length).ToArray();
    var u = items.Select(m => Enumerable.Range(0, K).Select(s => (double)m.Names.Sum(nm => usage[s].GetValueOrDefault(nm))).ToArray()).ToArray();
    double Viol(int s, int before) { int c = before + otherBefore[s] + propsBefore[s]; return c < lo[s] ? lo[s] - c : c > hi[s] ? c - hi[s] : 0; }
    var best = new double[n + 1, K]; var from = new int[n + 1, K];
    for (int s = 0; s < K; s++) { best[0, s] = -Enumerable.Range(1, s).Sum(m => Viol(m, 0)) * penalty; from[0, s] = -1; }
    int before = 0;
    for (int i = 0; i < n; i++)
    {
        for (int s = 0; s < K; s++)
        {
            double b = double.NegativeInfinity; int arg = 0, crossing = 0; double crossCost = 0;
            for (int s2 = s; s2 >= 0; s2--)
            {
                if (s2 < s) crossCost += Viol(s2 + 1, before) * penalty;
                double v = (i == 0 ? best[0, s2] : best[i, s2]) - crossCost;
                if (v > b) { b = v; arg = s2; crossing = s - s2; }
            }
            best[i + 1, s] = b + u[i][s]; from[i + 1, s] = arg;
        }
        before += w[i];
    }
    int last = 0; double top = double.NegativeInfinity;
    for (int s = 0; s < K; s++)
    {
        double tail = Enumerable.Range(s + 1, K - 1 - s).Sum(m => Viol(m, before)) * penalty;
        if (best[n, s] - tail > top) { top = best[n, s] - tail; last = s; }
    }
    var at = new int[n];
    for (int i = n, s = last; i >= 1; i--) { at[i - 1] = s; s = from[i, s]; }
    return at;
}
int[] Before(List<Member> items, int[] at)
{
    var r = new int[K + 1];
    for (int i = 0; i < items.Count; i++) for (int s = at[i] + 1; s <= K; s++) r[s] += items[i].Names.Length;
    return r;
}
var typeAt = Cut(types, new int[K + 1], 0.0);
var fieldAt = Cut(fields, Before(types, typeAt), 10.0);
typeAt = Cut(types, Before(fields, fieldAt), 10.0);
fieldAt = Cut(fields, Before(types, typeAt), 10.0);
for (int i = 0; i < types.Count; i++) seg[types[i]] = typeAt[i];
for (int i = 0; i < fields.Count; i++) seg[fields[i]] = fieldAt[i];

// Static-init dependencies: a field read during static init stays in the
// reader's file, ahead of it (declaration order inside a file is kept).
var deps = File.ReadAllLines(depsPath).Where(l => l.Length > 0).Select(l => l.Split('\t')).Select(p => (dep: fieldDecl[p[0]], src: fieldDecl[p[1]])).ToList();
var group = new Dictionary<Member, Member>();
Member Find(Member m) { while (group.TryGetValue(m, out var up) && up != m) m = up; return m; }
foreach (var (dep, src) in deps)
{
    if (src.Order > dep.Order) throw new Exception($"{src.Names[0]} is declared after {dep.Names[0]}, which reads it during static init");
    var a = Find(dep); var b = Find(src);
    if (a != b) group[b] = a;
}
// each group lands where its last-declared member (the end of the chain) lives
foreach (var g in deps.SelectMany(d => new[] { d.dep, d.src }).Distinct().GroupBy(Find))
{
    int target = seg[g.MaxBy(m => m.Order)!];
    foreach (var m in g)
    {
        if (seg[m] != target) Console.Error.WriteLine($"static-init dependency: {m.Names[0]} moves to {segTopic[target]} (was {segTopic[seg[m]]})");
        seg[m] = target;
    }
}

// ---- report ------------------------------------------------------------------
var finalBefore = new int[K + 1];
foreach (var m in members.Where(m => m.Kind is Kind.Field or Kind.Type or Kind.Property))
    for (int s = seg[m] + 1; s <= K; s++) finalBefore[s] += m.Kind == Kind.Field ? m.Names.Length : 1;
int violations = Enumerable.Range(0, K + 1).Count(s => finalBefore[s] < lo[s] || finalBefore[s] > hi[s]);
int usedHere = fields.Count(f => f.Names.Any(n => usage[seg[f]].GetValueOrDefault(n) > 0));
Console.Error.WriteLine($"{K} segments, {segTopic.Distinct().Count()} files; field-block bounds missed at {violations} boundaries; {usedHere}/{fields.Count} fields used in their own file; {staticInit.Count} static initializers restored");

// ---- emit --------------------------------------------------------------------
Directory.CreateDirectory(outDir);
string Body(Member m)
{
    SyntaxNode node = m.Node;
    if (m == cctorMember) node = newCctor!;
    if (m.Kind == Kind.Field)
    {
        var fd = (FieldDeclarationSyntax)node;
        foreach (var v in fd.Declaration.Variables.ToList())
        {
            if (!staticInit.TryGetValue(v.Identifier.Text, out var e)) continue;
            var cur = fd.Declaration.Variables.First(x => x.Identifier.Text == v.Identifier.Text);
            var expr = SyntaxFactory.ParseExpression(Dedent(e.ToString()));
            fd = fd.ReplaceNode(cur, cur.WithInitializer(SyntaxFactory.EqualsValueClause(
                SyntaxFactory.Token(SyntaxKind.EqualsToken).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space), expr)));
        }
        node = fd;
    }
    var s = node.ToFullString().Replace("\r\n", "\n");
    s = s.TrimStart('\n');
    while (s.StartsWith("\t\n") || s.StartsWith(" \n")) s = s[(s.IndexOf('\n') + 1)..];
    return s.TrimEnd() + "\n";
}
static string Dedent(string s) => string.Join("\n", s.Replace("\r\n", "\n").Split('\n').Select((l, i) => i > 0 && l.StartsWith('\t') ? l[1..] : l));

string clsHeader = cls.ToFullString()[..(cls.OpenBraceToken.FullSpan.End - cls.FullSpan.Start)].Replace("\r\n", "\n").TrimStart('\n');
foreach (var topic in segTopic.Distinct())
{
    var mine = members.Where(m => segTopic[seg[m]] == topic).ToList();
    int Rank(Member m) => m.Kind switch { Kind.Type => 0, Kind.Field => 1, _ => 2 };
    int Pos(Member m) => m.Kind switch
    {
        Kind.Method => metaMethods[srcMethods.IndexOf(m)].Index,
        Kind.Property => layout.FindIndex(r => r.Name == "get_" + m.Names[0] || r.Name == "set_" + m.Names[0]),
        _ => m.Order,
    };
    var sb = new StringBuilder(header);
    sb.Append(topic == "-" ? clsHeader.Replace("public class NemesisMod", "public partial class NemesisMod") : "public partial class NemesisMod\n{\n");
    sb.Append(string.Join("\n", mine.OrderBy(Rank).ThenBy(Pos).Select(Body)));
    sb.Append("}\n");
    File.WriteAllText(Path.Combine(outDir, topic == "-" ? "NemesisMod.cs" : $"NemesisMod.{topic}.cs"), sb.ToString());
}
return 0;

enum Kind { Type, Field, Property, Method }

sealed class Member
{
    public readonly MemberDeclarationSyntax Node; public readonly int Order; public readonly Kind Kind; public readonly string[] Names;
    public Member(MemberDeclarationSyntax node, int order)
    {
        Node = node; Order = order;
        (Kind, Names) = node switch
        {
            BaseTypeDeclarationSyntax t => (Kind.Type, new[] { t.Identifier.Text }),
            DelegateDeclarationSyntax d => (Kind.Type, new[] { d.Identifier.Text }),
            FieldDeclarationSyntax f => (Kind.Field, f.Declaration.Variables.Select(v => v.Identifier.Text).ToArray()),
            PropertyDeclarationSyntax p => (Kind.Property, new[] { p.Identifier.Text }),
            IndexerDeclarationSyntax => (Kind.Property, new[] { "Item" }),
            ConstructorDeclarationSyntax c => (Kind.Method, new[] { c.Modifiers.Any(SyntaxKind.StaticKeyword) ? ".cctor" : ".ctor" }),
            MethodDeclarationSyntax m => (Kind.Method, new[] { m.Identifier.Text }),
            _ => throw new Exception($"unhandled member kind {node.Kind()}"),
        };
    }
}
