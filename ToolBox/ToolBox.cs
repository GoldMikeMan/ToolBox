using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
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
        static readonly string[] allTools = ["ToolBox", "WrapHDL"];
        static readonly string[] availableCommands = ["All Tools", "Installed Tools", "Exit"];
        static readonly string author = "GoldMike";
        static List<string> installedTools = [];
        static readonly Lock outputLock = new();
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Clear();
            Console.WriteLine(header);
            Console.WriteLine($" Ver {GetToolVersion()}");
            Console.WriteLine("");
            Console.WriteLine(" 📁 All Tools");
            Console.WriteLine(" 📂 Installed Tools:");
            installedTools = GetInstalledToolCommandsByAuthor(author);
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
            string? input = Console.ReadLine();
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
            if (input == "All Tools")
            {
                Console.WriteLine();
                Console.WriteLine(" 📂 All Tools:");
                for (int i = 0; i < allTools.Length; i++) Console.WriteLine($"   🔧 {allTools[i]}");
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
                Console.WriteLine(" Installed Tools:");
                Console.WriteLine("  - None");
                return;
            }
            Console.WriteLine(" Installed Tools:");
            for (int i = 0; i < tools.Count; i++) Console.WriteLine($" - {tools[i]}");
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
            if (tool == "SteeleTerm" && tokens.Any(t => t == "--serial" || t == "--ssh")) { RunPassthroughTool(tokens); return; }
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
            const int minSpinnerMs = 0;
            int acked = 0;
            int spinnerActive = 0;
            int stopScheduled = 0;
            long spinnerStartedAt = 0;
            bool spinning = false;
            Thread? spinnerThread = null;
            var pending = new Queue<string>();
            object pendingLock = new();
            void Enqueue(string line) { lock (pendingLock) pending.Enqueue(line); }
            void StartSpinner()
            {
                if (Interlocked.Exchange(ref spinnerActive, 1) != 0) return;
                spinnerStartedAt = Environment.TickCount64;
                spinning = true;
                spinnerThread = new Thread(() => {
                    char[] frames = ['|', '/', '-', '\\'];
                    int i = 0;
                    while (spinning)
                    {
                        lock (outputLock) { try { Console.Write("\r" + prompt + "⏳ Waiting for ToolBox " + frames[i++ & 3] + " "); } catch { } }
                        Thread.Sleep(100);
                    }
                }) { IsBackground = true };
                lock (outputLock) { try { Console.Write("\r" + prompt + "⏳ Waiting for ToolBox | "); } catch { } }
                spinnerThread.Start();
            }
            void StopSpinnerAndFlush()
            {
                if (Interlocked.Exchange(ref spinnerActive, 0) == 0) return;
                spinning = false;
                try { spinnerThread?.Join(); } catch { }
                lock (outputLock)
                {
                    try { Console.Write("\r" + new string(' ', Math.Max(0, Console.BufferWidth - 1)) + "\r"); } catch { }
                    while (true)
                    {
                        string? line = null;
                        lock (pendingLock) { if (pending.Count > 0) line = pending.Dequeue(); }
                        if (line == null) break;
                        if (line.Length == 0) Console.WriteLine(); else Console.WriteLine(prompt + line);
                    }
                }
            }
            void RequestStop()
            {
                if (spinnerActive == 0) return;
                long elapsed = Environment.TickCount64 - spinnerStartedAt;
                if (elapsed >= minSpinnerMs) { StopSpinnerAndFlush(); return; }
                if (Interlocked.Exchange(ref stopScheduled, 1) != 0) return;
                Task.Run(() => {
                    int wait = (int)Math.Max(0, minSpinnerMs - (Environment.TickCount64 - spinnerStartedAt));
                    if (wait > 0) Thread.Sleep(wait);
                    Interlocked.Exchange(ref stopScheduled, 0);
                    StopSpinnerAndFlush();
                });
            }
            p.OutputDataReceived += (_, e) => {
                if (e.Data == null) return;
                var raw = e.Data;
                int cr = raw.LastIndexOf('\r');
                var line = cr >= 0 ? raw[(cr + 1)..] : raw;
                var trimmed = line.Trim();
                if (string.Equals(trimmed, sentinel, StringComparison.Ordinal))
                {
                    lock (outputLock) { if (line.Length == 0) Console.WriteLine(); else Console.WriteLine(prompt + line); }
                    StartSpinner();
                    if (Interlocked.Exchange(ref acked, 1) == 0) { try { p.StandardInput.WriteLine("ToolBox is open"); p.StandardInput.Flush(); } catch { } }
                    return;
                }
                if (spinnerActive != 0) { Enqueue(line); RequestStop(); return; }
                lock (outputLock) { if (Console.CursorLeft != 0) Console.WriteLine(); if (line.Length == 0) Console.WriteLine(); else Console.WriteLine(prompt + line); }
            };
            p.ErrorDataReceived += (_, e) => {
                if (e.Data == null) return;
                var line = e.Data.Replace("\r", "");
                if (spinnerActive != 0) { Enqueue(line); RequestStop(); return; }
                lock (outputLock) { if (Console.CursorLeft != 0) Console.WriteLine(); if (line.Length == 0) Console.WriteLine(); else Console.Error.WriteLine(prompt + line); }
            };
            try { p.Start(); }
            catch (Exception ex) { lock (outputLock) Console.WriteLine(prompt + ex.Message); return; }
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            StopSpinnerAndFlush();
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
    }
}