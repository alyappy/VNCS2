using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using VNTextPatch.Shared.Scripts;

namespace VNTextPatch.Shared.Util
{
    public static class StringUtil
    {
        public static Encoding SjisEncoding { get; } = Encoding.GetEncoding(932);
        public static SjisTunnelEncoding SjisTunnelEncoding { get; } = new SjisTunnelEncoding();

        public static bool IsShiftJisLeadByte(byte b)
        {
            return (b >= 0x81 && b < 0xA0) || (b >= 0xE0 && b < 0xFD);
        }

        public static bool ContainsJapaneseText(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            foreach (char c in str)
            {
                if ((c >= 0x3040 && c <= 0x309F) || // Hiragana
                    (c >= 0x30A0 && c <= 0x30FF) || // Katakana
                    (c >= 0x4E00 && c <= 0x9FBF) || // Kanji
                    (c >= 0xFF00 && c <= 0xFFEF))   // Full-width
                    return true;
            }
            return false;
        }

        public static string ToFullWidth(string input)
        {
            if (input == null) return null;
            char[] c = input.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == 32)
                {
                    c[i] = (char)12288;
                    continue;
                }
                if (c[i] < 127)
                    c[i] = (char)(c[i] + 0xFEE0);
            }
            return new string(c);
        }
    }
}