using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VNTextPatch.Shared.Util;

namespace VNTextPatch.Shared.Scripts
{
    internal class CatSystemScript : IScript
    {
        private static readonly byte[] Magic = { 0x43, 0x61, 0x74, 0x53, 0x63, 0x65, 0x6E, 0x65 };

        private byte[] _script;
        private int _stringAddressesOffset;
        private int _stringsOffset;
        private int _numStrings;

        public string Extension => ".cst";
        public string FontSizeCommand { get; set; } = "\\fs";

        public void Load(ScriptLocation location)
        {
            byte[] compressedData = File.ReadAllBytes(location.ToFilePath());
            _script = Decompress(compressedData);

            int dataLength = BitConverter.ToInt32(_script, 0);
            _stringAddressesOffset = 0x10 + BitConverter.ToInt32(_script, 8);
            _stringsOffset = 0x10 + BitConverter.ToInt32(_script, 0xC);
            _numStrings = (_stringsOffset - _stringAddressesOffset) / 4;
        }

        public IEnumerable<ScriptString> GetStrings()
        {
            using (var stream = new MemoryStream(_script))
            using (var reader = new BinaryReader(stream))
            {
                for (int i = 0; i < _numStrings; i++)
                {
                    int address = BitConverter.ToInt32(_script, _stringAddressesOffset + 4 * i);
                    stream.Position = _stringsOffset + address;

                    if (reader.ReadByte() != 0x01) continue;
                    CstStringType type = (CstStringType)reader.ReadByte();
                    
                    string text = ReadStringSafe(reader);
                    
                    if (Regex.IsMatch(text, @"^mpl\s+\d+", RegexOptions.IgnoreCase))
                    {
                        continue;
                    }

                    if (type == CstStringType.Message && !string.IsNullOrEmpty(text) && text != "\\n")
                    {
                        yield return new ScriptString(CstTextToScriptText(text), ScriptStringType.Message);
                    }
                    else if (type == CstStringType.Character)
                    {
                        yield return new ScriptString(text, ScriptStringType.CharacterName);
                    }
                    else if (type == CstStringType.Command)
                    {
                        Match match = Regex.Match(text, @"^\d+\s+\w+\s+(.+)");
                        if (match.Success)
                            yield return new ScriptString(match.Groups[1].Value, ScriptStringType.Message);
                    }
                }
            }
        }

        public void WritePatched(IEnumerable<ScriptString> strings, ScriptLocation location)
        {
            MemoryStream patchedStream = PatchScript(strings);
            using (Stream outputStream = File.Open(location.ToFilePath(), FileMode.Create, FileAccess.Write))
            {
                Compress(patchedStream, outputStream);
            }
        }

        private MemoryStream PatchScript(IEnumerable<ScriptString> strings)
        {
            MemoryStream originalStream = new MemoryStream(_script);
            MemoryStream patchedStream = new MemoryStream();
            BinaryPatcher patcher = new BinaryPatcher(originalStream, patchedStream, a => _stringsOffset + a, o => o - _stringsOffset);

            patcher.CopyUpTo(_stringsOffset);

            using (IEnumerator<ScriptString> enumerator = strings.GetEnumerator())
            {
                foreach (var cstStr in GetInternalCstStrings())
                {
                    if (Regex.IsMatch(cstStr.Text, @"^mpl\s+\d+", RegexOptions.IgnoreCase))
                    {
                        continue;
                    }

                    bool isMsg = cstStr.Type == CstStringType.Message && !cstStr.IsEmpty;
                    bool isCmd = cstStr.Type == CstStringType.Command && Regex.IsMatch(cstStr.Text, @"^\d+\s+\w+\s+(.+)");

                    if (cstStr.Type == CstStringType.Character)
                    {
                        if (!enumerator.MoveNext()) throw new Exception($"Not enough strings. Stuck at: {cstStr.Text}");
                        patcher.CopyUpTo(_stringsOffset + cstStr.Address + 2);
                        patcher.ReplaceZeroTerminatedSjisString(enumerator.Current.Text);
                    }
                    else if (isMsg)
                    {
                        if (!enumerator.MoveNext()) throw new Exception($"Not enough strings. Stuck at: {cstStr.Text}");
                        patcher.CopyUpTo(_stringsOffset + cstStr.Address + 2);
                        string patchedText = ScriptTextToCstText(cstStr, enumerator.Current.Text, FontSizeCommand);
                        patcher.ReplaceZeroTerminatedSjisString(patchedText);
                    }
                    else if (isCmd)
                    {
                        if (!enumerator.MoveNext()) throw new Exception($"Not enough strings. Stuck at: {cstStr.Text}");
                        Match m = Regex.Match(cstStr.Text, @"^(\d+\s+\w+\s+).+");
                        string prefix = m.Groups[1].Value;
                        string content = enumerator.Current.Text.Replace(" ", "　");

                        patcher.CopyUpTo(_stringsOffset + cstStr.Address + 2);
                        patcher.ReplaceZeroTerminatedSjisString(prefix + content);
                    }
                }
            }

            patcher.CopyUpTo(_script.Length);
            for (int i = 0; i < _numStrings; i++)
                patcher.PatchAddress(_stringAddressesOffset + 4 * i);

            patcher.PatchInt32(0, (int)patchedStream.Length - 0x10);

            patchedStream.Position = 0;
            return patchedStream;
        }

        private static string ScriptTextToCstText(CstStringStruct cstString, string text, string fontCmd)
        {
            text = Regex.Replace(text, @"\S+", m => Regex.IsMatch(m.Value, @"\\[^@]") ? m.Value : $"[{m.Value}]");
            text = fontCmd + "\\@" + text;
            text = ReplaceSpecialChars(text);
            text = text.Replace("\r\n", "\\n");

            if (cstString.Text.EndsWith("\\@")) text += "\\@";
            return text;
        }

        private static string ReplaceSpecialChars(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if (c == '\'' || c == '"') sb.Append($@"\${(int)c};");
                else if (StringUtil.SjisEncoding.GetByteCount(new char[] { c }) > 0) sb.Append(c);
                else sb.Append($@"\${(int)c};");
            }
            return sb.ToString();
        }

        private static string CstTextToScriptText(string text) => text.Replace("\\n", "\r\n").Replace("\\@", "");

        private static byte[] Decompress(byte[] inputData)
        {
            using (MemoryStream ms = new MemoryStream(inputData))
            using (BinaryReader r = new BinaryReader(ms))
            {
                byte[] magic = r.ReadBytes(8);
                int compLen = r.ReadInt32();
                int uncompLen = r.ReadInt32();

                if (compLen == 0) return r.ReadBytes(uncompLen);

                ms.Seek(2, SeekOrigin.Current);

                byte[] buffer = new byte[uncompLen];
                using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    int offset = 0;
                    int remaining = buffer.Length;
                    while (remaining > 0)
                    {
                        int read = deflate.Read(buffer, offset, remaining);
                        if (read == 0) break;
                        offset += read;
                        remaining -= read;
                    }
                }

                return buffer;
            }
        }

        private static void Compress(Stream uncompressed, Stream compressed)
        {
            BinaryWriter w = new BinaryWriter(compressed);
            w.Write(Magic);
            w.Write(0);
            w.Write((int)uncompressed.Length);

            long start = compressed.Position;
            using (var zlib = new ZlibStream(compressed))
            {
                uncompressed.CopyTo(zlib);
            }
            long end = compressed.Position;

            compressed.Position = 8;
            w.Write((int)(end - start));
        }

        private IEnumerable<CstStringStruct> GetInternalCstStrings()
        {
            using (var ms = new MemoryStream(_script))
            using (var r = new BinaryReader(ms))
            {
                for (int i = 0; i < _numStrings; i++)
                {
                    int addr = BitConverter.ToInt32(_script, _stringAddressesOffset + 4 * i);
                    ms.Position = _stringsOffset + addr;
                    if (r.ReadByte() != 0x01) continue;
                    var type = (CstStringType)r.ReadByte();
                    string txt = ReadStringSafe(r);
                    yield return new CstStringStruct(addr, type, txt);
                }
            }
        }

        private static string ReadStringSafe(BinaryReader reader)
        {
            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return StringUtil.SjisEncoding.GetString(bytes.ToArray());
        }

        private enum CstStringType { Message = 0x20, Character = 0x21, Command = 0x30 }
        private struct CstStringStruct
        {
            public int Address; public CstStringType Type; public string Text;
            public CstStringStruct(int a, CstStringType t, string x) { Address = a; Type = t; Text = x; }
            public bool IsEmpty => string.IsNullOrEmpty(Text) || Text == "\\n" || Text == "\\p";
        }
    }
}