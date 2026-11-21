using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using TeklaMcp.Core;
using TeklaMcp.Core.Models;
using TeklaMcp.Scripting;

namespace TeklaMcp.Server.Tools;

/// <summary>
/// Local fork addition: runs C# scripts kept as files in a trusted library folder on the user's
/// machine. The agent sends only the script name and a few arguments instead of the whole source,
/// and gets back a compact result. The library folder is the approval: only files the user put
/// there (default %LOCALAPPDATA%\TeklaMcp\scripts, override with TEKLA_MCP_SCRIPT_LIBRARY) can run.
/// </summary>
[McpServerToolType]
[Toolset(ToolExposure.Scripting)]
public static class ScriptLibraryTools
{
    private static readonly Regex Placeholder = new Regex(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);
    private static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9_\-\.]+$", RegexOptions.Compiled);

    internal static string LibraryDir
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("TEKLA_MCP_SCRIPT_LIBRARY");
            if (!string.IsNullOrWhiteSpace(env)) return env!.Trim();
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeklaMcp", "scripts");
        }
    }

    public sealed class LibraryRunResult
    {
        public bool Ok { get; set; }
        public string Script { get; set; } = "";
        public string? Value { get; set; }
        public string? Error { get; set; }
        public List<string>? CompileErrors { get; set; }
        public List<string>? Printed { get; set; }
        public long Ms { get; set; }
    }

    public sealed class LibraryEntry
    {
        public string Name { get; set; } = "";
        public string Summary { get; set; } = "";
        public List<string> Args { get; set; } = new List<string>();
    }

    [McpServerTool(Name = "tekla_run_script", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description(
        "Run a C# script from the user's trusted script library (" +
        "%LOCALAPPDATA%\\TeklaMcp\\scripts\\<name>.cs) with arguments. Placeholders {{key}} in the file are " +
        "replaced by args values before compiling; an unresolved placeholder refuses the run. Mutations are " +
        "allowed: the user approved the library by installing it. Returns a compact result (ok, value, error). " +
        "List available scripts with tekla_list_scripts.")]
    public static LibraryRunResult RunScript(
        ITeklaModelService model,
        [Description("Script file name without .cs, e.g. 'drw_props'.")] string name,
        [Description("Arguments as 'key=value' pairs separated by ';' or new lines, e.g. 'mark=[IS-IV.9 - 99];title=4.8-09'. " +
                     "Use '\\;' for a literal semicolon.")] string? args = null,
        [Description("Execution deadline in seconds (default 60, max 600).")] int timeoutSeconds = 60)
    {
        var result = new LibraryRunResult { Script = name ?? "" };
        if (ServerMode.ReadOnly)
        {
            result.Error = ToolExposure.ReadOnlyRefusal("tekla_run_script");
            return result;
        }
        if (string.IsNullOrWhiteSpace(name) || !SafeName.IsMatch(name))
        {
            result.Error = "Invalid script name. Use letters, digits, '_', '-', '.' only.";
            return result;
        }
        var path = Path.Combine(LibraryDir, name + ".cs");
        if (!File.Exists(path))
        {
            result.Error = "Script not found: " + path + ". See tekla_list_scripts.";
            return result;
        }

        var code = File.ReadAllText(path);
        var map = ParseArgs(args);
        var missing = new HashSet<string>();
        code = Placeholder.Replace(code, m =>
        {
            var key = m.Groups[1].Value;
            if (map.TryGetValue(key, out var v)) return v;
            missing.Add(key);
            return m.Value;
        });
        if (missing.Count > 0)
        {
            result.Error = "Missing args: " + string.Join(", ", missing) + ".";
            return result;
        }

        var run = model.ExecuteScript(code, allowMutations: true, timeoutSeconds: timeoutSeconds);
        result.Ok = run.Success;
        result.Value = run.ReturnValueJson;
        result.Ms = run.DurationMs;
        if (!run.Success)
        {
            result.Error = run.Error ?? run.Stage;
            if (run.CompileErrors.Count > 0) result.CompileErrors = run.CompileErrors;
            if (run.PolicyViolations.Count > 0)
                result.Error += " | policy: " + string.Join("; ", run.PolicyViolations);
        }
        if (run.PrintedOutput.Count > 0) result.Printed = run.PrintedOutput;
        return result;
    }

    [McpServerTool(Name = "tekla_list_scripts", ReadOnly = true, OpenWorld = false)]
    [Description("List the scripts in the user's trusted script library with their summary (first // comment line) " +
                 "and the {{args}} each one needs.")]
    public static List<LibraryEntry> ListScripts()
    {
        var list = new List<LibraryEntry>();
        var dir = LibraryDir;
        if (!Directory.Exists(dir)) return list;
        foreach (var file in Directory.GetFiles(dir, "*.cs").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var text = File.ReadAllText(file);
            var first = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("//")) ?? "";
            list.Add(new LibraryEntry
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Summary = first.TrimStart('/').Trim(),
                Args = Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList(),
            });
        }
        return list;
    }

    internal static Dictionary<string, string> ParseArgs(string? args)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(args)) return map;
        var parts = Regex.Split(args!.Replace("\r", ""), @"(?<!\\)[;\n]");
        foreach (var raw in parts)
        {
            var part = raw.Replace("\\;", ";");
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            map[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
        }
        return map;
    }
}
