using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MiniExcelLibs;
using VNTextPatch.Shared.Scripts;
using VNTextPatch.Shared.Util;

namespace VNCS2
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var positionalArgs = new List<string>();
            string fontCommand = "\\fs";

            foreach (var arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    string flag = arg.Substring(2).ToLower();
                    if (flag == "fs" || flag == "fss" || flag == "fn" ||
                        flag == "f" || flag == "fl" || flag == "fll" ||
                        Regex.IsMatch(flag, @"^f\d+$"))
                    {
                        fontCommand = "\\" + flag;
                    }
                }
                else
                {
                    positionalArgs.Add(arg);
                }
            }

            if (positionalArgs.Count < 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  VNCS2.exe extract <inputfolder> <scriptfolder|scriptfile(.xlsx)>");
                Console.WriteLine("  VNCS2.exe insert <inputfolder> <scriptfolder|scriptfile(.xlsx)> <outputfolder>");
                Console.WriteLine("  VNCS2.exe insert [fontsize option] <inputfolder> <scriptfolder|scriptfile(.xlsx)> <outputfolder>");
                Console.WriteLine("Ingfo Fontsize:");
                Console.WriteLine("  --fss          = font extra small");
                Console.WriteLine("  --fs           = font small (Default)");
                Console.WriteLine("  --fn           = font normal");
                Console.WriteLine("  --f            = font normal");
                Console.WriteLine("  --fl           = font large");
                Console.WriteLine("  --fl           = font extra large");
                Console.WriteLine("  --f[number]    = font custom number");
                Console.WriteLine("Example:");
                Console.WriteLine("  VNCS2.exe insert --fss Amakano2 Amakano2.xlsx Amakano2TL");
                Console.WriteLine("  VNCS2.exe insert --f18 Amakano2 Amakano2.xlsx Amakano2TL");
                Console.WriteLine("  ");
                Console.WriteLine("BIJIKKKK");
                return;
            }

            string mode = positionalArgs[0].ToLower();
            string inputFolder = positionalArgs[1];
            string target = positionalArgs[2];
            string resultFolder = positionalArgs.Count > 3 ? positionalArgs[3] : null;

            try
            {
                if (mode == "extract")
                {
                    if (target.EndsWith(".xlsx"))
                        ExtractToExcel(inputFolder, target);
                    else
                        ExtractToJson(inputFolder, target);
                }
                else if (mode == "insert")
                {
                    if (string.IsNullOrEmpty(resultFolder))
                    {
                        Console.WriteLine("Error: Result folder required for insert mode.");
                        return;
                    }
                    Console.WriteLine($"Using Font Command: {fontCommand}");

                    if (target.EndsWith(".xlsx"))
                        InsertFromExcel(inputFolder, target, resultFolder, fontCommand);
                    else
                        InsertFromJson(inputFolder, target, resultFolder, fontCommand);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void ExtractToJson(string inputFolder, string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            foreach (var file in Directory.GetFiles(inputFolder, "*.cst"))
            {
                try
                {
                    var script = new CatSystemScript();
                    script.Load(new ScriptLocation(file));
                    var rawStrings = script.GetStrings().ToList();

                    var formattedList = new List<JsonEntry>();
                    string pendingName = null;

                    foreach (var s in rawStrings)
                    {
                        if (s.Type == ScriptStringType.CharacterName)
                        {
                            pendingName = s.Text;
                        }
                        else if (s.Type == ScriptStringType.Message)
                        {
                            var entry = new JsonEntry
                            {
                                Name = pendingName,
                                Message = s.Text.Replace("\r\n", "")
                            };
                            formattedList.Add(entry);

                            pendingName = null;
                        }
                    }

                    if (formattedList.Count > 0)
                    {
                        string jsonPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file) + ".json");
                        string json = JsonSerializer.Serialize(formattedList, options);
                        File.WriteAllText(jsonPath, json);
                        Console.WriteLine($"Extracted: {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipped {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        static void InsertFromJson(string inputFolder, string jsonFolder, string resultFolder, string fontCommand)
        {
            Directory.CreateDirectory(resultFolder);
            foreach (var file in Directory.GetFiles(inputFolder, "*.cst"))
            {
                string jsonPath = Path.Combine(jsonFolder, Path.GetFileNameWithoutExtension(file) + ".json");
                if (!File.Exists(jsonPath)) continue;

                var script = new CatSystemScript();
                script.Load(new ScriptLocation(file));
                script.FontSizeCommand = fontCommand;

                var jsonContent = File.ReadAllText(jsonPath);
                var jsonEntries = JsonSerializer.Deserialize<List<JsonEntry>>(jsonContent);

                var scriptStrings = new List<ScriptString>();
                foreach (var entry in jsonEntries)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        scriptStrings.Add(new ScriptString(entry.Name, ScriptStringType.CharacterName));
                    }
                    scriptStrings.Add(new ScriptString(entry.Message, ScriptStringType.Message));
                }

                script.WritePatched(scriptStrings, new ScriptLocation(Path.Combine(resultFolder, Path.GetFileName(file))));
                Console.WriteLine($"Patched: {Path.GetFileName(file)}");
            }
        }

        static void ExtractToExcel(string inputFolder, string excelPath)
        {
            var rows = new List<ExcelRow>();
            foreach (var file in Directory.GetFiles(inputFolder, "*.cst"))
            {
                try
                {
                    var script = new CatSystemScript();
                    script.Load(new ScriptLocation(file));
                    foreach (var s in script.GetStrings())
                    {
                        string cleanText = s.Text.Replace("\r\n", "");
                        rows.Add(new ExcelRow { FileName = Path.GetFileName(file), Type = s.Type.ToString(), Original = cleanText, Translation = cleanText });
                    }
                    Console.WriteLine($"Processed: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipped {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            MiniExcel.SaveAs(excelPath, rows);
        }

        static void InsertFromExcel(string inputFolder, string excelPath, string resultFolder, string fontCommand)
        {
            Directory.CreateDirectory(resultFolder);
            var rows = MiniExcel.Query<ExcelRow>(excelPath).ToList();
            var fileGroups = rows.GroupBy(r => r.FileName);

            foreach (var group in fileGroups)
            {
                string srcFile = Path.Combine(inputFolder, group.Key);
                if (!File.Exists(srcFile)) continue;

                var script = new CatSystemScript();
                script.Load(new ScriptLocation(srcFile));
                script.FontSizeCommand = fontCommand;

                var translatedStrings = group.Select(r => new ScriptString(r.Translation ?? r.Original,
                    Enum.Parse<ScriptStringType>(r.Type))).ToList();

                script.WritePatched(translatedStrings, new ScriptLocation(Path.Combine(resultFolder, group.Key)));
                Console.WriteLine($"Patched: {group.Key}");
            }
        }

        public class JsonEntry
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }
        }

        public class ExcelRow
        {
            public string FileName { get; set; }
            public string Type { get; set; }
            public string Original { get; set; }
            public string Translation { get; set; }
        }
    }
}