using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
[assembly: SupportedOSPlatform("windows")]
namespace ToolBox
{
    class ToolBox
    {
        static readonly string header = @"
  ______                ___    _____                     
 /\__  _\              /\_ \  /\  _ `\                   
 \/_/\ \/   ___     ___\//\ \ \ \ \L\ \    ___   __  _  
    \ \ \  / __`\  / __`\\ \ \ \ \  _ <'  / __`\/\ \/'\ 
     \ \ \/\ \L\ \/\ \L\ \\_\ \_\ \ \L\ \/\ \L\ \/>  </ 
      \ \_\ \____/\ \____//\____\\ \____/\ \____//\_/\_\
       \/_/\/___/  \/___/ \/____/ \/___/  \/___/ \//\/_/
 -------------------------------------------------------";
        static readonly string prompt = " 🧰 > ";
        static readonly string[] allTools = ["ToolBox", "SteeleTerm", "WrapHDL"];
        static readonly string author = "GoldMike";
        static readonly Lock outputLock = new();
        static List<string> installedTools = [];
        static readonly string[] availableCommands = ["allTools", "exit", "help", "installedTools", "reset"];
        static readonly Dictionary<string, string[]> argsPrimary = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string[]> argsSecondary = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string[]> argsSecondaryUpdate = new(StringComparer.Ordinal) { ["--update"] = ["--forceUpdate"], ["--updateMajor"] = ["--forceUpdate"], ["--updateMinor"] = ["--forceUpdate"] };
        static readonly Dictionary<string, string[]> argsTertiary = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string[]> argsTertiaryUpdate = new(StringComparer.Ordinal) { ["--forceUpdate"] = ["--skipVersion"] };
        static void Main()
        {
        Reset:
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(header);
            Console.WriteLine($" Ver {GetToolVersion()}");
            Console.WriteLine("");
            Console.WriteLine(" 📁 All Tools");
            Console.WriteLine(" 📂 Installed Tools:");
            var spinner = new ConsoleSpinner(outputLock, prompt, 100, 150);
            spinner.Start("⏳ Scanning installed tools");
            installedTools = GetInstalledToolCommandsByAuthor(author);
            BuildAutocomplete(installedTools);
            Console.WriteLine(" 🔍 Autocomplete Dictionary:");
            spinner.StopAndFlush();
            if (installedTools.Count == 0)
            {
                installedTools = ["None"];
                Console.WriteLine("    ❌ None");
            }
            else
            {
                if (installedTools.Contains("ToolBox")) Console.WriteLine("    🧰 ToolBox:");
                for (int i = 0; i < installedTools.Count; i++)
                {
                    if (installedTools[i] == "ToolBox") continue;
                    Console.WriteLine($"       🔧 {installedTools[i]}");
                }
            }
            Console.WriteLine(" 🚪 Exit");
            Console.WriteLine("");
        Prompt:
            int lineTop = Console.CursorTop;
            Console.Write(prompt);
            string? input = Autocomplete();
            if (input == null)
            {
                ClearLine(lineTop);
                goto Prompt;
            }
            if (installedTools.Any(t => input!.Contains(t)))
            {
                ToolRunner(input);
                Console.WriteLine("");
                goto Prompt;
            }
            if (!IsValidCommand(input))
            {
                ClearLine(lineTop);
                goto Prompt;
            }
            if (input == "Reset") goto Reset;
            else if (input == "Help")
            {
                Console.WriteLine();
                Console.WriteLine("      ToolBox Commands:");
                Console.WriteLine("        \'allTools\'                            List all available tools.");
                Console.WriteLine("        \'exit\'                                Close ToolBox.");
                Console.WriteLine("        \'help\'                                Print help to console.");
                Console.WriteLine("        \'installedTools\'                      List all installed tools.");
                Console.WriteLine("        \'reset\'                               Reloads ToolBox.");
                Console.WriteLine();
                Console.WriteLine("      For dedicated tool help use \'<toolname> --help\'");
                Console.WriteLine();
                goto Prompt;
            }
            else if (input == "All Tools")
            {
                Console.WriteLine();
                Console.WriteLine(" 📂 All Tools:");
                if (allTools.Contains("ToolBox")) Console.WriteLine("    🧰 ToolBox:");
                for (int i = 0; i < allTools.Length; i++)
                {
                    if (allTools[i] == "ToolBox") continue;
                    Console.WriteLine($"       🔧 {allTools[i]}");
                }
                Console.WriteLine();
                goto Prompt;
            }
            else if (input == "Installed Tools")
            {
                Console.WriteLine();
                PrintInstalledToolsByAuthor(author);
                Console.WriteLine();
                goto Prompt;
            }
            else if (input == "Exit") return;
        }
        static string Autocomplete()
        {
            string currentInput = "";
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (currentInput.Length > 0)
                    {
                        currentInput = currentInput[..^1];
                        Console.Write("\b \b");
                    }
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return currentInput;
                }
                else if (key.Key == ConsoleKey.Tab)
                {
                    var matches = installedTools.Where(t => (t.StartsWith(currentInput, StringComparison.Ordinal)) || t.StartsWith(currentInput, StringComparison.Ordinal)).ToList();
                    if (matches.Count >= 1)
                    {
                        ClearLine(Console.CursorTop);
                        string ghost = matches[0];
                        Console.Write(prompt + currentInput + ghost);
                    }
                }
                else
                {
                    currentInput += key.KeyChar;
                    Console.Write(key.KeyChar);
                }
            }
        }
        static bool IsValidCommand(string input)
        {
            if (input == null) return false;
            for (int i = 0; i < availableCommands.Length; i++) { if (input == availableCommands[i]) return true; }
            return false;
        }
        static void ClearLine(int top)
        {
            int width = Math.Max(1, Console.BufferWidth);
            Console.SetCursorPosition(0, top);
            Console.Write(new string(' ', Math.Max(0, width - 1)));
            Console.SetCursorPosition(0, top);
        }
        static void PrintInstalledToolsByAuthor(string authorExact)
        {
            var tools = GetInstalledGlobalTools().Where(t => PackageAuthorsContainExact(t.PackageId, t.Version, authorExact)).SelectMany(t => t.Commands).Distinct().OrderBy(x => x).ToList();
            if (tools.Count == 0)
            {
                Console.WriteLine(" 📂 Installed Tools:");
                Console.WriteLine("    🚫 None");
                return;
            }
            Console.WriteLine(" 📂 Installed Tools:");
            if (installedTools.Contains("ToolBox")) Console.WriteLine("    🧰 ToolBox:");
            for (int i = 0; i < installedTools.Count; i++)
            {
                if (installedTools[i] == "ToolBox") continue;
                Console.WriteLine($"       🔧 {allTools[i]}");
            }
        }
        static List<string> GetInstalledToolCommandsByAuthor(string authorExact)
        {
            return [.. GetInstalledGlobalTools().Where(t => PackageAuthorsContainExact(t.PackageId, t.Version, authorExact)).SelectMany(t => t.Commands).Distinct().OrderBy(x => x)];
        }
        static string GetToolVersion()
        {
            var asm = typeof(ToolBox).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                int plus = info.IndexOf('+');
                if (plus >= 0) info = info[..plus];
                return info;
            }
            return asm.GetName().Version?.ToString() ?? "0.0.0";
        }
        static void ToolRunner(string input)
        {
            var tokens = Tokenise(input);
            if (tokens.Count == 0) return;
            var tool = tokens[0];
            if (tool == "None") { Console.WriteLine(prompt + "None means none dumbass."); return; }
            if (tool == "ToolBox") { Console.WriteLine(prompt + "ToolBox is already running."); return; }
            if (tool == "SteeleTerm" && tokens.Any(t => t == "--fileBrowser" || t == "--serial" || t == "--ssh")) { RunPassthroughTool(tokens); return; }
            var psi = new ProcessStartInfo
            {
                FileName = tool,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                StandardInputEncoding = System.Text.Encoding.UTF8
            };
            for (int i = 1; i < tokens.Count; i++) psi.ArgumentList.Add(tokens[i]);
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            const string sentinel = "🔍 Verifying parent is ToolBox...";
            var spinner = new ConsoleSpinner(outputLock, prompt, 100, 0);
            int acked = 0;
            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                var raw = e.Data;
                int cr = raw.LastIndexOf('\r');
                var line = cr >= 0 ? raw[(cr + 1)..] : raw;
                var trimmed = line.Trim();
                if (string.Equals(trimmed, sentinel, StringComparison.Ordinal))
                {
                    lock (outputLock) { if (line.Length == 0) Console.WriteLine(); else Console.WriteLine(prompt + line); }
                    spinner.Start("⏳ Waiting for ToolBox");
                    if (Interlocked.Exchange(ref acked, 1) == 0) { try { p.StandardInput.WriteLine("ToolBox is open"); p.StandardInput.Flush(); } catch { } }
                    return;
                }
                if (spinner.Active) { spinner.Enqueue(line, false); spinner.RequestStopAndFlush(); return; }
                lock (outputLock) { if (Console.CursorLeft != 0) Console.WriteLine(); if (line.Length == 0) Console.WriteLine(); else Console.WriteLine(prompt + line); }
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                var line = e.Data.Replace("\r", "");
                if (spinner.Active) { spinner.Enqueue(line, true); spinner.RequestStopAndFlush(); return; }
                lock (outputLock) { if (Console.CursorLeft != 0) Console.WriteLine(); if (line.Length == 0) Console.WriteLine(); else Console.Error.WriteLine(prompt + line); }
            };
            try { p.Start(); }
            catch (Exception ex) { lock (outputLock) Console.WriteLine(prompt + ex.Message); return; }
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            try { p.WaitForExit(); }
            finally { spinner.StopAndFlush(); }
        }
        static List<string> Tokenise(string input)
        {
            var tokens = new List<string>();
            var cur = "";
            bool inQuotes = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (!inQuotes && char.IsWhiteSpace(c)) { if (cur.Length > 0) { tokens.Add(cur); cur = ""; } continue; }
                cur += c;
            }
            if (cur.Length > 0) tokens.Add(cur);
            return tokens;
        }
        sealed class ToolRow
        {
            public string PackageId { get; init; } = "";
            public string Version { get; init; } = "";
            public List<string> Commands { get; init; } = [];
        }
        static List<ToolRow> GetInstalledGlobalTools()
        {
            var output = RunAndCapture("dotnet", "tool list --global");
            var lines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
            var result = new List<ToolRow>();
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.StartsWith("Package", StringComparison.Ordinal)) continue;
                if (line.All(ch => ch == '-' || ch == ' ')) continue;
                var parts = SplitByWhitespace(line);
                if (parts.Count < 3) continue;
                var packageId = parts[0];
                var version = parts[1];
                var commands = parts.Skip(2).SelectMany(p => p.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                result.Add(new ToolRow { PackageId = packageId, Version = version, Commands = commands });
            }
            return result;
        }
        static bool PackageAuthorsContainExact(string packageId, string version, string authorExact)
        {
            string? nuspecPath = FindNuspecInDotnetToolStore(packageId, version) ?? FindNuspecInGlobalPackages(packageId, version);
            if (nuspecPath == null) return false;
            try
            {
                var doc = XDocument.Load(nuspecPath);
                var authorsElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "authors");
                if (authorsElement == null) return false;
                var authors = authorsElement.Value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList();
                return authors.Any(a => a == authorExact);
            }
            catch { return false; }
        }
        static string? FindNuspecInDotnetToolStore(string packageId, string version)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home)) return null;
            var storeRoot = Path.Combine(home, ".dotnet", "tools", ".store");
            var pkgRoot = Path.Combine(storeRoot, packageId.ToLowerInvariant(), version.ToLowerInvariant());
            if (!Directory.Exists(pkgRoot)) return null;
            return Directory.EnumerateFiles(pkgRoot, "*.nuspec", SearchOption.AllDirectories).FirstOrDefault();
        }
        static string? FindNuspecInGlobalPackages(string packageId, string version)
        {
            var globalPackages = GetGlobalPackagesFolder();
            if (string.IsNullOrWhiteSpace(globalPackages)) return null;
            var pkgRoot = Path.Combine(globalPackages, packageId.ToLowerInvariant(), version.ToLowerInvariant());
            if (!Directory.Exists(pkgRoot)) return null;
            return Directory.EnumerateFiles(pkgRoot, "*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }
        static string GetGlobalPackagesFolder()
        {
            var output = RunAndCapture("dotnet", "nuget locals global-packages --list");
            var idx = output.IndexOf("global-packages:", StringComparison.Ordinal);
            if (idx < 0) return "";
            var after = output[(idx + "global-packages:".Length)..].Trim();
            var firstLine = after.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return firstLine.Trim();
        }
        static string RunAndCapture(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return "";
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (stdout + "\n" + stderr).Trim();
        }
        static List<string> SplitByWhitespace(string s)
        {
            var list = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i >= s.Length) break;
                int start = i;
                while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
                list.Add(s[start..i]);
            }
            return list;
        }
        static void RunPassthroughTool(List<string> tokens)
        {
            var psi = new ProcessStartInfo
            {
                FileName = tokens[0],
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = false
            };
            psi.Environment["TOOLBOX_HOST"] = "1";
            psi.Environment["TOOLBOX_PREFIX"] = prompt;
            for (int i = 1; i < tokens.Count; i++) psi.ArgumentList.Add(tokens[i]);
            using var p = Process.Start(psi);
            if (p == null) return;
            p.WaitForExit();
        }
        static void BuildAutocomplete(List<string> installedTools)
        {
            if (installedTools == null || installedTools.Count == 0) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < installedTools.Count; i++)
            {
                var cmd = installedTools[i];
                if (string.IsNullOrWhiteSpace(cmd) || cmd == "None") continue;
                if (!seen.Add(cmd)) continue;
                if (cmd == "ToolBox") continue;
                string text = "";
                try { text = RunAndCapture(cmd, "--help"); } catch { continue; }
                if (string.IsNullOrWhiteSpace(text)) { continue; }
                using StringReader currentLine = new(text);
                while (true)
                {
                    var autocompleteDictionary = "";
                    int argStart;
                    int argEnd;
                    List<string> args = [];
                    string updateArgCheck;
                    var line = currentLine.ReadLine();
                    if (line == null) break;
                    line = line.Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("Primary args:", StringComparison.Ordinal)) autocompleteDictionary = "Primary";
                    while (autocompleteDictionary == "Primary")
                    {
                        line = currentLine.ReadLine();
                        if (line == null) break;
                        line = line.Trim();
                        if (line.Length == 0) { continue; }
                        argStart = line.IndexOf('-');
                        if (argStart != -1)
                        {
                            argEnd = line.IndexOf(' ', argStart);
                            args.Add(line[argStart..argEnd]);
                        }
                        if (line.StartsWith("Secondary args:", StringComparison.Ordinal))
                        {
                            args = [.. args.OrderBy(x => x, StringComparer.Ordinal)];
                            argsPrimary[cmd] = [.. args];
                            args.Clear();
                            autocompleteDictionary = "Secondary";
                            break;
                        }
                    }
                    while (autocompleteDictionary == "Secondary")
                    {
                        line = currentLine.ReadLine();
                        if (line == null) break;
                        line = line.Trim();
                        if (line.Length == 0) { continue; }
                        argStart = line.IndexOf('-');
                        if (argStart != -1)
                        {
                            argEnd = line.IndexOf(' ', argStart);
                            updateArgCheck = line[argStart..argEnd];
                            if (updateArgCheck == "--forceUpdate") continue;
                            args.Add(line[argStart..argEnd]);
                        }
                        if (line.StartsWith("Tertiary args:", StringComparison.Ordinal))
                        {
                            args = [.. args.OrderBy(x => x, StringComparer.Ordinal)];
                            argsSecondary[cmd] = [.. args];
                            args.Clear();
                            autocompleteDictionary = "Tertiary";
                            break;
                        }
                    }
                    while (autocompleteDictionary == "Tertiary")
                    {
                        line = currentLine.ReadLine();
                        if (line == null) break;
                        line = line.Trim();
                        if (line.Length == 0) { continue; }
                        argStart = line.IndexOf('-');
                        if (argStart != -1)
                        {
                            argEnd = line.IndexOf(' ', argStart);
                            updateArgCheck = line[argStart..argEnd];
                            if (updateArgCheck == "--skipVersion") continue;
                            args.Add(line[argStart..argEnd]);
                        }
                    }
                    if (autocompleteDictionary == "Tertiary")
                    {
                        args = [.. args.OrderBy(x => x, StringComparer.Ordinal)];
                        argsTertiary[cmd] = [.. args];
                        args.Clear();
                        break;
                    }
                }
            }
        }
        sealed class ConsoleSpinner(Lock outputLock, string prefix, int intervalMs = 100, int minSpinnerMs = 0)
        {
            readonly Lock outputLock = outputLock;
            readonly string prefix = prefix;
            readonly int intervalMs = intervalMs;
            readonly int minSpinnerMs = minSpinnerMs;
            readonly Queue<(string Line, bool IsErr)> pending = new();
            readonly Lock pendingLock = new();
            Thread? spinnerThread;
            volatile bool spinning;
            long spinnerStartedAt;
            int active;
            int stopScheduled;
            string text = "";
            bool cursorOldVisible;
            bool cursorCaptured;
            public bool Active => Volatile.Read(ref active) != 0;
            public void Start(string text)
            {
                if (Console.IsOutputRedirected) return;
                if (Interlocked.Exchange(ref active, 1) != 0) return;
                this.text = text;
                spinnerStartedAt = Environment.TickCount64;
                try { cursorOldVisible = Console.CursorVisible; Console.CursorVisible = false; cursorCaptured = true; } catch { cursorCaptured = false; }
                spinning = true;
                spinnerThread = new Thread(() =>
                {
                    char[] frames = ['|', '/', '-', '\\'];
                    int i = 0;
                    while (spinning)
                    {
                        lock (outputLock) { try { Console.Write("\r" + prefix + this.text + " " + frames[i++ & 3] + " "); } catch { } }
                        Thread.Sleep(intervalMs);
                    }
                }) { IsBackground = true };
                lock (outputLock) { try { Console.Write("\r" + prefix + this.text + " | "); } catch { } }
                spinnerThread.Start();
            }
            public void Enqueue(string line, bool isErr) { lock (pendingLock) pending.Enqueue((line, isErr)); }
            public void RequestStopAndFlush()
            {
                if (!Active) return;
                long elapsed = Environment.TickCount64 - spinnerStartedAt;
                if (elapsed >= minSpinnerMs) { StopAndFlush(); return; }
                if (Interlocked.Exchange(ref stopScheduled, 1) != 0) return;
                Task.Run(() =>
                {
                    int wait = (int)Math.Max(0, minSpinnerMs - (Environment.TickCount64 - spinnerStartedAt));
                    if (wait > 0) Thread.Sleep(wait);
                    Interlocked.Exchange(ref stopScheduled, 0);
                    StopAndFlush();
                });
            }
            public void StopAndFlush()
            {
                if (Interlocked.Exchange(ref active, 0) == 0) return;
                spinning = false;
                try { spinnerThread?.Join(); } catch { }
                if (cursorCaptured) { try { Console.CursorVisible = cursorOldVisible; } catch { } cursorCaptured = false; }
                lock (outputLock)
                {
                    try { Console.Write("\r" + new string(' ', Math.Max(0, Console.BufferWidth - 1)) + "\r"); } catch { try { Console.Write("\r"); } catch { } }
                    while (true)
                    {
                        (string Line, bool IsErr) item;
                        lock (pendingLock) { if (pending.Count == 0) break; item = pending.Dequeue(); }
                        if (item.Line.Length == 0) { if (item.IsErr) Console.Error.WriteLine(); else Console.WriteLine(); }
                        else { if (item.IsErr) Console.Error.WriteLine(prefix + item.Line); else Console.WriteLine(prefix + item.Line); }
                    }
                }
            }
        }
    }
}