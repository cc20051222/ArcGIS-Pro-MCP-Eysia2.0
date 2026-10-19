using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Static metadata/IL reading only: never loads or executes vendor code.
if (args is ["--folder-probe", var proposedRoot])
{
    var before = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    Environment.SetEnvironmentVariable("LOCALAPPDATA", proposedRoot);
    var after = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        observedAt = DateTimeOffset.Now,
        probe = "read-only child-process known-folder resolution; no vendor code execution",
        before,
        environmentOverride = Environment.GetEnvironmentVariable("LOCALAPPDATA"),
        after,
        redirected = string.Equals(after, proposedRoot, StringComparison.OrdinalIgnoreCase),
        createsDirectories = false
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return;
}
bool assemblyInfo = args[0] == "--assembly-info";
var path = args[assemblyInfo ? 1 : 0];
using var file = File.OpenRead(path);
using var pe = new PEReader(file);
var md = pe.GetMetadataReader();
if (assemblyInfo)
{
    var definition = md.GetAssemblyDefinition();
    string? framework = null;
    foreach (var h in definition.GetCustomAttributes())
    {
        var a = md.GetCustomAttribute(h);
        if (a.Constructor.Kind != HandleKind.MemberReference) continue;
        var parent = md.GetMemberReference((MemberReferenceHandle)a.Constructor).Parent;
        if (parent.Kind != HandleKind.TypeReference) continue;
        if (md.GetString(md.GetTypeReference((TypeReferenceHandle)parent).Name) != "TargetFrameworkAttribute") continue;
        var blob = md.GetBlobReader(a.Value);
        if (blob.ReadUInt16() == 1) framework = blob.ReadSerializedString();
    }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        path,
        name = md.GetString(definition.Name),
        version = definition.Version.ToString(),
        targetFramework = framework,
        references = md.AssemblyReferences.Select(h => md.GetAssemblyReference(h))
            .Select(a => new { name = md.GetString(a.Name), version = a.Version.ToString() }).ToArray(),
        inspection = "PE metadata only; no Assembly.Load or vendor method invocation"
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return;
}
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(o => unchecked((ushort)o.Value));
var patterns = args.Skip(1).ToArray();
foreach (var th in md.TypeDefinitions)
{
    var type = md.GetTypeDefinition(th);
    foreach (var mh in type.GetMethods())
    {
        var method = md.GetMethodDefinition(mh);
        var name = md.GetString(method.Name);
        if (!patterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)) || method.RelativeVirtualAddress == 0) continue;
        Console.WriteLine($"\n{md.GetString(type.Namespace)}.{md.GetString(type.Name)}::{name}");
        var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
        for (var i = 0; i < il.Length;)
        {
            int start = i;
            ushort code = il[i++];
            if (code == 0xfe) code = (ushort)(0xfe00 | il[i++]);
            if (!opcodes.TryGetValue(code, out var op)) break;
            int size = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4
            };
            var operand = size == 0 ? "" : Convert.ToHexString(il.AsSpan(i, size));
            if (size == 4 && op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok or OperandType.InlineString)
            {
                int token = BitConverter.ToInt32(il, i);
                try
                {
                    var handle = MetadataTokens.Handle(token);
                    operand = handle.Kind switch
                    {
                        HandleKind.UserString => "\"" + md.GetUserString((UserStringHandle)handle) + "\"",
                        HandleKind.MemberReference => Member((MemberReferenceHandle)handle),
                        HandleKind.MethodDefinition => md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name),
                        HandleKind.FieldDefinition => md.GetString(md.GetFieldDefinition((FieldDefinitionHandle)handle).Name),
                        HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)handle).Name),
                        _ => $"token 0x{token:X8} ({handle.Kind})"
                    };
                } catch { operand = $"token 0x{token:X8}"; }
            }
            Console.WriteLine($"  IL_{start:X4}: {op.Name} {operand}");
            i += size;
        }
    }
}
string Member(MemberReferenceHandle h)
{
    var m = md.GetMemberReference(h);
    var parent = m.Parent.Kind == HandleKind.TypeReference ? md.GetString(md.GetTypeReference((TypeReferenceHandle)m.Parent).Name) : m.Parent.Kind.ToString();
    return parent + "::" + md.GetString(m.Name);
}
