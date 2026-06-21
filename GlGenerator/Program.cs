using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

Options options = Options.Parse(args);
Registry registry = Registry.Load(options);
string source = CSharpGenerator.Generate(registry, options);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath))!);
File.WriteAllText(options.OutputPath, source, Encoding.UTF8);
Console.WriteLine($"Generated {registry.Commands.Count} commands and {registry.Enums.Count} constants to {options.OutputPath}");

internal sealed record Options
{
    public required string RegistryPath { get; init; }

    public required string OutputPath { get; init; }

    public string Namespace { get; init; } = "Glutin.OpenGL";

    public string ClassName { get; init; } = "GL";

    public string Api { get; init; } = "gles2";

    public VersionSpec Version { get; init; } = new(3, 0);

    public string Profile { get; init; } = "core";

    public List<string> Extensions { get; init; } = [];

    public static Options Parse(string[] args)
    {
        string? registry = null;
        string? output = null;
        string ns = "Glutin.OpenGL";
        string className = "GL";
        string api = "gles2";
        VersionSpec version = new(3, 0);
        string profile = "core";
        List<string> extensions = [];

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string Next()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--registry":
                    registry = Next();
                    break;
                case "--output":
                    output = Next();
                    break;
                case "--namespace":
                    ns = Next();
                    break;
                case "--class":
                    className = Next();
                    break;
                case "--api":
                    api = Next();
                    break;
                case "--version":
                    version = VersionSpec.Parse(Next());
                    break;
                case "--profile":
                    profile = Next();
                    break;
                case "--extension":
                    extensions.Add(Next());
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        registry ??= FindDefaultRegistry();
        output ??= Path.Combine("Glutin.OpenGL", "GL.Generated.cs");

        return new Options
        {
            RegistryPath = registry,
            OutputPath = output,
            Namespace = ns,
            ClassName = className,
            Api = api,
            Version = version,
            Profile = profile,
            Extensions = extensions,
        };
    }

    private static string FindDefaultRegistry()
    {
        string cwd = Directory.GetCurrentDirectory();
        DirectoryInfo? dir = new(cwd);

        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "gl_generator-rust",
                "khronos_api",
                "api",
                "xml",
                "gl.xml");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(
                dir.FullName,
                "..",
                "gl_generator-rust",
                "khronos_api",
                "api",
                "xml",
                "gl.xml");

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find gl.xml. Pass --registry C:\\path\\to\\gl.xml.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        Usage:
          dotnet run --project GlGenerator -- [options]

        Options:
          --registry <path>     Khronos gl.xml path.
          --output <path>       Generated C# file path. Default: Glutin.OpenGL/GL.Generated.cs
          --namespace <name>    Generated namespace. Default: Glutin.OpenGL
          --class <name>        Generated loader class. Default: GL
          --api <name>          gl, gles1, gles2. Default: gles2
          --version <major.minor> Default: 3.0
          --profile <name>      core or compatibility. Default: core
          --extension <name>    Include an extension. May be repeated.
        """);
    }
}

internal readonly record struct VersionSpec(int Major, int Minor) : IComparable<VersionSpec>
{
    public static VersionSpec Parse(string value)
    {
        string[] parts = value.Split('.', 2);
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor))
        {
            throw new ArgumentException($"Invalid version '{value}'. Expected major.minor.");
        }

        return new VersionSpec(major, minor);
    }

    public int CompareTo(VersionSpec other)
    {
        int major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }
}

internal sealed class Registry
{
    public SortedDictionary<string, Command> Commands { get; } = [];

    public SortedDictionary<string, EnumConstant> Enums { get; } = [];

    public static Registry Load(Options options)
    {
        XDocument document = XDocument.Load(options.RegistryPath, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new InvalidOperationException("Registry XML has no root element.");

        Dictionary<string, Command> allCommands = ParseCommands(root);
        Dictionary<string, EnumConstant> allEnums = ParseEnums(root);

        OrderedSet commandNames = [];
        OrderedSet enumNames = [];

        foreach (XElement feature in root.Elements("feature"))
        {
            if (!ApiMatches(feature.Attribute("api")?.Value, options.Api))
            {
                continue;
            }

            VersionSpec featureVersion = VersionSpec.Parse(feature.Attribute("number")?.Value ?? "0.0");
            if (featureVersion.CompareTo(options.Version) > 0)
            {
                continue;
            }

            ApplyRequirements(feature, options, commandNames, enumNames);
            ApplyRemovals(feature, options, commandNames, enumNames);
        }

        if (options.Extensions.Count > 0)
        {
            HashSet<string> requested = options.Extensions.ToHashSet(StringComparer.Ordinal);
            foreach (XElement extension in root.Element("extensions")?.Elements("extension") ?? [])
            {
                string? name = extension.Attribute("name")?.Value;
                if (name is null || !requested.Contains(name))
                {
                    continue;
                }

                if (!ExtensionSupportsApi(extension.Attribute("supported")?.Value, options.Api))
                {
                    continue;
                }

                ApplyRequirements(extension, options, commandNames, enumNames);
            }
        }

        var registry = new Registry();
        foreach (string name in commandNames)
        {
            if (allCommands.TryGetValue(name, out Command? command))
            {
                registry.Commands[name] = command;
            }
        }

        foreach (string name in enumNames)
        {
            if (allEnums.TryGetValue(name, out EnumConstant? constant))
            {
                registry.Enums[name] = constant;
            }
        }

        return registry;
    }

    private static Dictionary<string, Command> ParseCommands(XElement root)
    {
        Dictionary<string, Command> commands = [];
        foreach (XElement commandElement in root.Elements("commands").Elements("command"))
        {
            XElement? proto = commandElement.Element("proto");
            string? name = proto?.Element("name")?.Value;
            if (proto is null || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string returnType = ExtractType(proto);
            List<Parameter> parameters = [];

            foreach (XElement paramElement in commandElement.Elements("param"))
            {
                string? paramName = paramElement.Element("name")?.Value;
                if (string.IsNullOrWhiteSpace(paramName))
                {
                    continue;
                }

                parameters.Add(new Parameter(paramName, ExtractType(paramElement)));
            }

            commands[name] = new Command(name, returnType, parameters);
        }

        return commands;
    }

    private static Dictionary<string, EnumConstant> ParseEnums(XElement root)
    {
        Dictionary<string, EnumConstant> enums = [];
        foreach (XElement enumElement in root.Elements("enums").Elements("enum"))
        {
            string? name = enumElement.Attribute("name")?.Value;
            string? value = enumElement.Attribute("value")?.Value;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            enums.TryAdd(name, new EnumConstant(
                name,
                value,
                enumElement.Attribute("type")?.Value,
                enumElement.Attribute("alias")?.Value));
        }

        return enums;
    }

    private static string ExtractType(XElement element)
    {
        StringBuilder builder = new();
        foreach (XNode node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child when child.Name.LocalName == "name":
                    break;
                case XElement child:
                    builder.Append(child.Value);
                    break;
            }
        }

        return NormalizeTypeText(builder.ToString());
    }

    private static string NormalizeTypeText(string value)
    {
        string normalized = value.Replace('\n', ' ').Replace('\t', ' ');
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = normalized.Replace(" *", "*").Replace("* ", "*");
        return normalized.Trim();
    }

    private static void ApplyRequirements(
        XElement owner,
        Options options,
        OrderedSet commandNames,
        OrderedSet enumNames)
    {
        foreach (XElement require in owner.Elements("require"))
        {
            if (!ApiMatches(require.Attribute("api")?.Value, options.Api))
            {
                continue;
            }

            if (!ProfileMatches(require.Attribute("profile")?.Value, options.Profile))
            {
                continue;
            }

            foreach (XElement command in require.Elements("command"))
            {
                string? name = command.Attribute("name")?.Value;
                if (name is not null)
                {
                    commandNames.Add(name);
                }
            }

            foreach (XElement @enum in require.Elements("enum"))
            {
                string? name = @enum.Attribute("name")?.Value;
                if (name is not null)
                {
                    enumNames.Add(name);
                }
            }
        }
    }

    private static void ApplyRemovals(
        XElement feature,
        Options options,
        OrderedSet commandNames,
        OrderedSet enumNames)
    {
        foreach (XElement remove in feature.Elements("remove"))
        {
            if (!ApiMatches(remove.Attribute("api")?.Value, options.Api))
            {
                continue;
            }

            if (!ProfileMatches(remove.Attribute("profile")?.Value, options.Profile))
            {
                continue;
            }

            foreach (XElement command in remove.Elements("command"))
            {
                string? name = command.Attribute("name")?.Value;
                if (name is not null)
                {
                    commandNames.Remove(name);
                }
            }

            foreach (XElement @enum in remove.Elements("enum"))
            {
                string? name = @enum.Attribute("name")?.Value;
                if (name is not null)
                {
                    enumNames.Remove(name);
                }
            }
        }
    }

    private static bool ApiMatches(string? value, string api)
    {
        return value is null || string.Equals(value, api, StringComparison.Ordinal);
    }

    private static bool ProfileMatches(string? value, string profile)
    {
        return value is null || string.Equals(value, profile, StringComparison.Ordinal);
    }

    private static bool ExtensionSupportsApi(string? supported, string api)
    {
        if (string.IsNullOrWhiteSpace(supported))
        {
            return false;
        }

        return supported.Split('|').Any(entry =>
            string.Equals(entry, api, StringComparison.Ordinal)
            || (api.StartsWith("gles", StringComparison.Ordinal) && entry == "gles2")
            || (api == "gl" && entry == "gl"));
    }
}

internal sealed record Command(string Name, string ReturnType, IReadOnlyList<Parameter> Parameters);

internal sealed record Parameter(string Name, string Type);

internal sealed record EnumConstant(string Name, string Value, string? Type, string? Alias);

internal sealed class OrderedSet : List<string>
{
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);

    public new void Add(string item)
    {
        if (_set.Add(item))
        {
            base.Add(item);
        }
    }

    public new bool Remove(string item)
    {
        _set.Remove(item);
        return base.Remove(item);
    }
}

internal static class CSharpGenerator
{
    public static string Generate(Registry registry, Options options)
    {
        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine($"namespace {options.Namespace};");
        builder.AppendLine();
        builder.AppendLine($"public static unsafe class {options.ClassName}");
        builder.AppendLine("{");
        WriteEnums(builder, registry);
        WriteFields(builder, registry);
        WriteLoader(builder, registry, options);
        WriteMethods(builder, registry);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void WriteEnums(StringBuilder builder, Registry registry)
    {
        foreach (EnumConstant constant in registry.Enums.Values)
        {
            if (TryFormatEnum(constant, out string? type, out string? value))
            {
                builder.AppendLine($"    public const {type} {ConstantName(constant.Name)} = {value};");
            }
        }

        if (registry.Enums.Count > 0)
        {
            builder.AppendLine();
        }
    }

    private static void WriteFields(StringBuilder builder, Registry registry)
    {
        foreach (Command command in registry.Commands.Values)
        {
            string fieldName = FunctionPointerFieldName(command.Name);
            string functionPointerType = FunctionPointerType(command);
            builder.AppendLine($"    private static {functionPointerType} {fieldName};");
        }

        if (registry.Commands.Count > 0)
        {
            builder.AppendLine();
        }
    }

    private static void WriteLoader(StringBuilder builder, Registry registry, Options options)
    {
        builder.AppendLine("    public static void Load(Func<string, nint> load)");
        builder.AppendLine("    {");
        foreach (Command command in registry.Commands.Values)
        {
            string fieldName = FunctionPointerFieldName(command.Name);
            string functionPointerType = FunctionPointerType(command);
            builder.AppendLine($"        {fieldName} = ({functionPointerType})LoadFunction(load, \"{command.Name}\");");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static nint LoadFunction(Func<string, nint> load, string name)");
        builder.AppendLine("    {");
        builder.AppendLine("        nint address = load(name);");
        builder.AppendLine("        if (address == 0)");
        builder.AppendLine("        {");
        builder.AppendLine("            throw new InvalidOperationException($\"OpenGL function '{name}' is not available.\");");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return address;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static InvalidOperationException FunctionNotLoaded(string name)");
        builder.AppendLine("    {");
        builder.AppendLine($"        return new InvalidOperationException($\"OpenGL function '{{name}}' has not been loaded. Call {options.ClassName}.Load first.\");");
        builder.AppendLine("    }");
    }

    private static void WriteMethods(StringBuilder builder, Registry registry)
    {
        if (registry.Commands.Count == 0)
        {
            return;
        }

        builder.AppendLine();

        foreach (Command command in registry.Commands.Values)
        {
            string returnType = TypeMapper.ToCSharpType(command.ReturnType);
            string methodName = CommandMethodName(command.Name);
            string fieldName = FunctionPointerFieldName(command.Name);
            string parameters = string.Join(", ", command.Parameters.Select(ParameterDeclaration));
            string arguments = string.Join(", ", command.Parameters.Select(parameter => Identifier(parameter.Name, IdentifierContext.Parameter)));

            builder.AppendLine($"    public static {returnType} {methodName}({parameters})");
            builder.AppendLine("    {");
            builder.AppendLine($"        if ({fieldName} == null)");
            builder.AppendLine("        {");
            builder.AppendLine($"            throw FunctionNotLoaded(\"{command.Name}\");");
            builder.AppendLine("        }");
            builder.AppendLine();

            if (returnType == "void")
            {
                builder.AppendLine($"        {fieldName}({arguments});");
            }
            else
            {
                builder.AppendLine($"        return {fieldName}({arguments});");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }
    }

    private static string FunctionPointerType(Command command)
    {
        List<string> types = command.Parameters
            .Select(parameter => TypeMapper.ToCSharpType(parameter.Type))
            .ToList();
        types.Add(TypeMapper.ToCSharpType(command.ReturnType));
        return $"delegate* unmanaged<{string.Join(", ", types)}>";
    }

    private static string FunctionPointerFieldName(string name)
    {
        string methodName = CommandMethodName(name);
        return "_" + char.ToLowerInvariant(methodName[0]) + methodName[1..];
    }

    private static string CommandMethodName(string name)
    {
        string trimmed = name.StartsWith("gl", StringComparison.Ordinal) && name.Length > 2
            ? name[2..]
            : name;
        return Identifier(trimmed, IdentifierContext.Member);
    }

    private static string ConstantName(string name)
    {
        string trimmed = name.StartsWith("GL_", StringComparison.Ordinal)
            ? name[3..]
            : name;
        return Identifier(trimmed, IdentifierContext.Member);
    }

    private static string ParameterDeclaration(Parameter parameter)
    {
        return $"{TypeMapper.ToCSharpType(parameter.Type)} {Identifier(parameter.Name, IdentifierContext.Parameter)}";
    }

    private static string Identifier(string value, IdentifierContext context)
    {
        StringBuilder builder = new();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        string identifier = builder.ToString();
        return context == IdentifierContext.Parameter && CSharpKeywords.Contains(identifier)
            ? "@" + identifier
            : identifier;
    }

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };

    private static bool TryFormatEnum(EnumConstant constant, out string? type, out string? value)
    {
        type = null;
        value = null;

        string raw = constant.Value.Trim();
        if (raw.StartsWith('"') || raw.Contains('(') || raw.Contains('~'))
        {
            return false;
        }

        string lower = raw.ToLowerInvariant();
        bool isUnsignedLong = lower.EndsWith("ull", StringComparison.Ordinal)
            || constant.Type == "ull";
        bool isUnsigned = isUnsignedLong
            || lower.EndsWith("u", StringComparison.Ordinal)
            || constant.Type == "u"
            || raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

        raw = Regex.Replace(raw, "(?i)(ull|ul|u|l)$", "");

        if (raw.StartsWith("-", StringComparison.Ordinal))
        {
            type = "int";
            value = raw;
            return true;
        }

        if (isUnsignedLong)
        {
            type = "ulong";
            value = raw + "UL";
            return true;
        }

        type = isUnsigned ? "uint" : "int";
        value = isUnsigned ? raw + "U" : raw;
        return true;
    }
}

internal static class TypeMapper
{
    private static readonly Dictionary<string, string> s_baseTypes = new(StringComparer.Ordinal)
    {
        ["void"] = "void",
        ["GLvoid"] = "void",
        ["GLenum"] = "uint",
        ["GLbitfield"] = "uint",
        ["GLuint"] = "uint",
        ["GLuint64"] = "ulong",
        ["GLuint64EXT"] = "ulong",
        ["GLint"] = "int",
        ["GLsizei"] = "int",
        ["GLint64"] = "long",
        ["GLint64EXT"] = "long",
        ["GLboolean"] = "byte",
        ["GLbyte"] = "sbyte",
        ["GLubyte"] = "byte",
        ["GLshort"] = "short",
        ["GLushort"] = "ushort",
        ["GLchar"] = "sbyte",
        ["GLcharARB"] = "sbyte",
        ["GLfloat"] = "float",
        ["GLclampf"] = "float",
        ["GLdouble"] = "double",
        ["GLclampd"] = "double",
        ["GLfixed"] = "int",
        ["GLhalf"] = "ushort",
        ["GLhalfARB"] = "ushort",
        ["GLintptr"] = "nint",
        ["GLsizeiptr"] = "nint",
        ["GLintptrARB"] = "nint",
        ["GLsizeiptrARB"] = "nint",
        ["GLsync"] = "nint",
        ["GLhandleARB"] = "uint",
        ["GLDEBUGPROC"] = "nint",
        ["GLDEBUGPROCARB"] = "nint",
        ["GLDEBUGPROCKHR"] = "nint",
    };

    public static string ToCSharpType(string glType)
    {
        string normalized = Normalize(glType);
        int pointerDepth = normalized.Count(c => c == '*');
        string baseType = normalized.Replace("*", "", StringComparison.Ordinal).Trim();

        if (!s_baseTypes.TryGetValue(baseType, out string? csharp))
        {
            csharp = "nint";
        }

        if (pointerDepth == 0)
        {
            return csharp;
        }

        if (csharp == "void")
        {
            return "void" + new string('*', pointerDepth);
        }

        return csharp + new string('*', pointerDepth);
    }

    private static string Normalize(string value)
    {
        string normalized = value
            .Replace("const", "", StringComparison.Ordinal)
            .Replace("struct", "", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\t', ' ');

        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = normalized.Replace(" *", "*", StringComparison.Ordinal);
        normalized = normalized.Replace("* ", "*", StringComparison.Ordinal);
        return normalized.Trim();
    }
}

internal enum IdentifierContext
{
    Member,
    Parameter,
}
