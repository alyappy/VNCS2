using System.Collections.Generic;
using VNTextPatch.Shared.Util;

// Definisi Interface Script
namespace VNTextPatch.Shared.Scripts
{
    public interface IScript
    {
        void Load(ScriptLocation location);
        IEnumerable<ScriptString> GetStrings();
        void WritePatched(IEnumerable<ScriptString> strings, ScriptLocation location);
    }
}

// Definisi Tipe Data Dasar
namespace VNTextPatch.Shared.Util
{
    using VNTextPatch.Shared.Scripts;

    public class ScriptLocation
    {
        public string FullPath { get; }

        public ScriptLocation(string path)
        {
            FullPath = path;
        }

        public string ToFilePath() => FullPath;
    }

    public enum ScriptStringType
    {
        Message,
        CharacterName
    }

    public class ScriptString
    {
        public string Text { get; set; }
        public ScriptStringType Type { get; set; }

        public ScriptString() { }

        public ScriptString(string text, ScriptStringType type)
        {
            Text = text;
            Type = type;
        }
    }

    // Struct Range diperlukan oleh WordWrapper dan StringUtil versi Full
    public struct Range
    {
        public int Offset;
        public int Length;
        public ScriptStringType Type;

        public Range(int offset, int length, ScriptStringType type)
        {
            Offset = offset;
            Length = length;
            Type = type;
        }
    }
}