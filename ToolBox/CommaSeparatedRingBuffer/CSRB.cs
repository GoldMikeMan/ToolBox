using System.Text;
namespace ToolBox.CommaSeparatedRingBuffer
{
    public class CSRB
    {
        static readonly Encoding UTF8 = new UTF8Encoding(false);
        static string? lastSavedCommand;
        static readonly string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static void ValidateAndRepair()
        {

        }
        public static List<string> Load(List<string> loadCommands)
        {
            lastSavedCommand = loadCommands.FirstOrDefault() ?? String.Empty;
            return [.. loadCommands];
        }
        public static void Save(string? commandToSave)
        {
            int maxIndex = 4096;      // Maximum number of records in the csrb file
            int maxIndexLength = 256; // Maximum byte length of each record (arbitrary fixed size for simplicity, can be adjusted as needed)
            int maxConcat = 1024;     // Maximum number of concatenated indexes for a single entry (arbitrary limit to prevent excessive fragmentation)
            int prefixTotal = 14;     // Total bytes for INDEX + COMMA + WRITEHEAD + COMMA + READHEAD + COMMA + CONCAT fields (4 + 1 + 1 + 1 + 1 + 1 + 4 + 1)
            int lf = 1;               // Line Feed character at the end of each record
            int whIndexCurrent = 0;
            int rhIndexCurrent = 0;
            int wh;
            int rh;
            int concat = 1;
            int nullPayload = maxIndexLength - prefixTotal - lf;
            string payload = new('\u0020', nullPayload);
            string record;
            if (commandToSave == null) return;
            commandToSave = commandToSave.Trim().Replace("\r", "").Replace("\n", "");
            if (commandToSave.Length == 0 || (lastSavedCommand != null && StringComparer.Ordinal.Equals(lastSavedCommand, commandToSave))) { return; }
            if (!File.Exists(Path.Combine(userProfile, ".dotnet", "tools", "ToolBoxCommandHistory.csrb")))
            {
                Directory.CreateDirectory(Path.Combine(userProfile, ".dotnet", "tools"));
                using FileStream fs = File.Create(Path.Combine(userProfile, ".dotnet", "tools", "ToolBoxCommandHistory.csrb"));
                for (int i = 1; i <= maxIndex; i++)
                {
                    wh = rh = (i == 1 ? 1 : 0);
                    record = $"{i:0000},{wh},{rh},{concat:0000},{payload}" + "\n"; // INDEX,WRITEHEAD,READHEAD,CONCAT,PAYLOAD + LF
                    fs.Write(UTF8.GetBytes(record));
                }
            }
            using FileStream fsrw = new(Path.Combine(userProfile, ".dotnet", "tools", "ToolBoxCommandHistory.csrb"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            for (int i = 1; i <= maxIndex; i++)
            {
                fsrw.Seek((i - 1) * 256 + 5, SeekOrigin.Begin);
                int whb = fsrw.ReadByte();
                fsrw.ReadByte(); // comma at +6
                int rhb = fsrw.ReadByte();
                if (whb == (byte)'1') whIndexCurrent = i;
                if (rhb == (byte)'1') rhIndexCurrent = i;
                if (whIndexCurrent != 0 && rhIndexCurrent != 0) break;
            }
            if (whIndexCurrent == 0) { fsrw.Seek((0) * 256 + 5, SeekOrigin.Begin); fsrw.WriteByte((byte)'1'); whIndexCurrent = 1; }
            if (rhIndexCurrent == 0) { fsrw.Seek((0) * 256 + 7, SeekOrigin.Begin); fsrw.WriteByte((byte)'1'); rhIndexCurrent = 1; }
            int whIndexNew = (whIndexCurrent % maxIndex) + 1;
            int rhIndexNew = (rhIndexCurrent % maxIndex) + 1;
            int ctsBytes = UTF8.GetByteCount(commandToSave);
            int padding;
            if (ctsBytes <= nullPayload)
            {
                wh = rh = 1;
                concat = 1;
                padding = nullPayload - ctsBytes;
                payload = commandToSave + new string('\u0020', padding);
                record = $"{whIndexCurrent:0000},{wh},{rh},{concat:0000},{payload}" + "\n";
                fsrw.Seek((whIndexCurrent - 1) * 256, SeekOrigin.Begin);
                if (UTF8.GetByteCount(record) == 256) fsrw.Write(UTF8.GetBytes(record)); else return;
                fsrw.Seek((whIndexNew - 1) * 256 + 5, SeekOrigin.Begin);
                fsrw.WriteByte((byte)'1');
                fsrw.Seek((whIndexCurrent - 1) * 256 + 5, SeekOrigin.Begin);
                fsrw.WriteByte((byte)'0');
                if (rhIndexCurrent != whIndexCurrent) { fsrw.Seek((rhIndexCurrent - 1) * 256 + 7, SeekOrigin.Begin); fsrw.WriteByte((byte)'0'); }
                lastSavedCommand = commandToSave;
            }
            else //ctsBytes > nullPayload
            {
                concat = 1;
                var payloadChunkBytes = 0;
                foreach (Rune r in commandToSave.EnumerateRunes())
                {
                    int runeBytes = r.Utf8SequenceLength;
                    if (payloadChunkBytes + runeBytes > nullPayload) { concat++; payloadChunkBytes = 0; }
                    payloadChunkBytes += runeBytes;
                }
                if (concat > maxConcat) return;
                var multiIndexCommand = commandToSave;
                var payloadRunes = "";
                var payloadChunkText = "";
                rh = 1;
                while (concat > 1)
                {
                    wh = 1;
                    payloadChunkBytes = 0;
                    payloadChunkText = "";
                    payloadRunes = "";
                    foreach (Rune r in commandToSave.EnumerateRunes())
                    {
                        int runeBytes = r.Utf8SequenceLength;
                        if (payloadChunkBytes + runeBytes > nullPayload)
                        {
                            payloadChunkText = payloadRunes;
                            padding = nullPayload - payloadChunkBytes;
                            payload = payloadChunkText + new string('\u0020', padding);
                            record = $"{whIndexCurrent:0000},{wh},{rh},{concat:0000},{payload}" + "\n";
                            fsrw.Seek((whIndexCurrent - 1) * 256, SeekOrigin.Begin);
                            if (UTF8.GetByteCount(record) == 256) fsrw.Write(UTF8.GetBytes(record)); else return;
                            fsrw.Seek((whIndexNew - 1) * 256 + 5, SeekOrigin.Begin);
                            fsrw.WriteByte((byte)'1');
                            fsrw.Seek((whIndexCurrent - 1) * 256 + 5, SeekOrigin.Begin);
                            fsrw.WriteByte((byte)'0');
                            whIndexCurrent = whIndexNew;
                            whIndexNew = (whIndexCurrent % maxIndex) + 1;
                            commandToSave = commandToSave[payloadRunes.Length..];
                            rh = 0;
                            concat--;
                            break;
                        }
                        payloadChunkBytes += runeBytes;
                        payloadRunes += r.ToString();
                    }
                }
                wh = 1;
                padding = nullPayload - UTF8.GetByteCount(commandToSave);
                payload = commandToSave + new string('\u0020', padding);
                record = $"{whIndexCurrent:0000},{wh},{rh},{concat:0000},{payload}" + "\n";
                fsrw.Seek((whIndexCurrent - 1) * 256, SeekOrigin.Begin);
                if (UTF8.GetByteCount(record) == 256) fsrw.Write(UTF8.GetBytes(record)); else return;
                fsrw.Seek((whIndexNew - 1) * 256 + 5, SeekOrigin.Begin);
                fsrw.WriteByte((byte)'1');
                fsrw.Seek((whIndexCurrent - 1) * 256 + 5, SeekOrigin.Begin);
                fsrw.WriteByte((byte)'0');
                if (rhIndexCurrent != whIndexCurrent) { fsrw.Seek((rhIndexCurrent - 1) * 256 + 7, SeekOrigin.Begin); fsrw.WriteByte((byte)'0'); }
                lastSavedCommand = multiIndexCommand;
            }
        }
    }
}