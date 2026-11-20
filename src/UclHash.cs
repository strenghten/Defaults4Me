// UserChoiceLatest hash engine (managed only).
// Token encoder, lookup tables and mixing routines adapted from cssxn/UserChoiceLatestHash
// via ComoEstaisAmigos/PS-SFTA (SFTA.ps1 2.0.1). MIT License:
//   Copyright (c) 2022 Danysys <danysys.com>
//   Copyright (c) 2026 cssxn
//   Copyright (c) 2026 ComoEstaisAmigos
// Full license text: THIRD-PARTY-NOTICES.md
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Defaults4Me
{
    static class UclHash
    {
    
        // -- lookup tables (filled by LoadTables) --
        static byte[] ByteClass, TokenRegion, Fallback, BitWidth, ShortRewind, LongRewind;
        static bool _loaded;
    
        // Post-mix magic seeds (recovered from the DLL's two mixing routines).
        static readonly uint[] SeedA = { 0x69FB0000U, 0x79F8A395U, 0x3CE8EC25U, 0x10FA9605U };
        static readonly uint[] SeedB = { 0x12CEB96DU, 0x2B890000U, 0x5B9F0000U, 0x2C7C6901U };
    
        const ulong SENTINEL = 0xFBFFFFFFFFFFFFFFUL;
        const ulong TOK_A = 0xFE00000000000000UL;
        const ulong TOK_B = 0xFE00800000000000UL;
        const ulong TOK_C = 0xFF00000000000000UL;
        const ulong TOK_D = 0xFE007DFFFFFFFFFFUL;
        const ulong TOK_E = 0xFE00801F7FFFFFFFUL;
        const uint  SEEN_MOD = 0x2803U;
    
        public static void LoadTables(string gzipBase64)
        {
            if (_loaded) return;
            byte[] raw;
            using (var ms = new MemoryStream(Convert.FromBase64String(gzipBase64)))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var outp = new MemoryStream())
            {
                gz.CopyTo(outp);
                raw = outp.ToArray();
            }
            int pos = 0;
            ByteClass   = ReadChunk(raw, ref pos);
            TokenRegion = ReadChunk(raw, ref pos);
            Fallback    = ReadChunk(raw, ref pos);
            BitWidth    = ReadChunk(raw, ref pos);
            ShortRewind = ReadChunk(raw, ref pos);
            LongRewind  = ReadChunk(raw, ref pos);
            _loaded = true;
        }
    
        static byte[] ReadChunk(byte[] src, ref int pos)
        {
            int len = BitConverter.ToInt32(src, pos); pos += 4;
            byte[] b = new byte[len];
            Buffer.BlockCopy(src, pos, b, 0, len); pos += len;
            return b;
        }
    
        // --------------------------- public entry -----------------------------
        // Lowercase ASCII only - matches the CRT _wcslwr (C locale) that the OS applies
        // to the canonical input. Non-ASCII bytes are left untouched, as the OS does.
        static string AsciiLower(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) if (c[i] >= 'A' && c[i] <= 'Z') c[i] = (char)(c[i] + 32);
            return new string(c);
        }
    
    
        // The hash = tokenize(canonical) -> MD5 + two mixes. The tokenizer is delegated to
        // Windows' own OpenWithHost.dll when it was found and agreed with the managed tokenizer;
        // the managed tokenizer is the self-contained fallback (exact for all real associations).
        public static string ComputeHash(string canonicalInput)
        {
            string lowered = AsciiLower(canonicalInput);
            ushort[] packed = null;
            
            if (packed == null) packed = EncodeManaged(lowered);
            return HashFromPacked(packed);
        }
    
        // Same as ComputeHash but never uses the native encoder, so the fallback can be checked
        // against real hashes on a machine that has the DLL.
        public static string ComputeHashManaged(string canonicalInput)
        {
            return HashFromPacked(EncodeManaged(AsciiLower(canonicalInput)));
        }
    
        public static ushort[] EncodeManaged(string lowered)
        {
            char[] inp = lowered.ToCharArray();
            var s = new State();
            uint firstConsumed;
            FirstPass(s, inp, inp.Length, out firstConsumed);
            SecondPass(s, inp, (int)firstConsumed, inp.Length);
            s.Entries[0] = (ulong)s.TotalUnits | (s.Count << 32);
    
            var packed = new List<ushort>();
            foreach (ulong v in s.Entries)
                for (int sh = 0; sh < 64; sh += 16)
                {
                    ushort part = (ushort)(v >> sh);
                    if (part != 0) packed.Add(part);
                }
            packed.Add(0);
            return packed.ToArray();
        }
    
        // The legacy UserChoice hash is the same MD5 + two mixes, applied to the UTF-16 string plus
        // its terminator instead of the token stream. Inputs too short to mix give "", as in the
        // original PS-SFTA PowerShell implementation this replaces.
        public static string LegacyHash(string baseInfo)
        {
            ushort[] units = new ushort[baseInfo.Length + 1];
            for (int i = 0; i < baseInfo.Length; i++) units[i] = baseInfo[i];
            if (MixCount(units.Length * 2) < 2) return "";
            return HashFromPacked(units);
        }
    
        static int MixCount(int byteLen)
        {
            int dwordLen = byteLen >> 2;
            return ((byteLen & 4) == 0) ? dwordLen : dwordLen - 1;
        }
    
        static string HashFromPacked(ushort[] packed)
        {
            byte[] bytes = new byte[packed.Length * 2];
            for (int i = 0; i < packed.Length; i++)
            {
                bytes[2 * i]     = (byte)packed[i];
                bytes[2 * i + 1] = (byte)(packed[i] >> 8);
            }
    
            uint[] md5 = new uint[4];
            using (var md = MD5.Create())
            {
                byte[] h = md.ComputeHash(bytes);
                for (int i = 0; i < 4; i++) md5[i] = BitConverter.ToUInt32(h, i * 4);
            }
    
            int mixCount = MixCount(bytes.Length);
            uint[] dw = new uint[(packed.Length + 1) / 2];
            for (int i = 0; i < dw.Length; i++)
            {
                uint lo = packed[i * 2];
                uint hi = (i * 2 + 1 < packed.Length) ? packed[i * 2 + 1] : 0U;
                dw[i] = lo | (hi << 16);
            }
    
            uint[] pa = new uint[2], pb = new uint[2];
            if (mixCount >= 2)
            {
                MixA(dw, mixCount, md5, SeedA, pa);
                MixB(dw, mixCount, md5, SeedB, pb);
            }
    
            byte[] final8 = new byte[8];
            Array.Copy(BitConverter.GetBytes(pa[0] ^ pb[0]), 0, final8, 0, 4);
            Array.Copy(BitConverter.GetBytes(pa[1] ^ pb[1]), 0, final8, 4, 4);
            return Convert.ToBase64String(final8);
        }
    
        // ----------------------------- internals -------------------------------
        class State
        {
            public List<ulong> Entries = new List<ulong>();
            public ulong[] Seen = new ulong[161];
            public ulong Last = SENTINEL;
            public uint Repeat;
            public uint TotalUnits;
            public State() { Entries.Add(0UL); }
            public ulong Count { get { return (ulong)Entries.Count; } }
        }
    
        static uint Idx(ulong v) { return (uint)(v % SEEN_MOD); }
        static bool TestSeen(State s, ulong v) { uint i = Idx(v); return (s.Seen[i >> 6] & (1UL << (int)(i & 63))) != 0; }
        static void MarkSeen(State s, ulong v) { uint i = Idx(v); s.Seen[i >> 6] |= (1UL << (int)(i & 63)); }
        static void Push(State s, ulong v) { s.Entries.Add(v); }
    
        static bool FindPrevious(State s, ulong value, out ulong distance)
        {
            distance = 0;
            if (!TestSeen(s, value) || s.Count == 0) return false;
            ulong start = s.Count > 0x400 ? s.Count - 0x400 : 0;
            for (ulong idx = s.Count - 1; ; --idx)
            {
                if (s.Entries[(int)idx] == value) { distance = s.Count - idx; return true; }
                if (idx == start) break;
            }
            return false;
        }
    
        static byte ClassFromWord(ushort ch)
        {
            byte hi = (byte)(ch >> 8);
            if (hi != 0) return (byte)(ByteClass[hi] + 8);
            return ByteClass[ch & 0xFF];
        }
    
        static bool BuildToken(char[] inp, int cursor, int end, out ulong token, out byte kind, out byte len)
        {
            token = 0; kind = 0; len = 0;
            if (cursor >= end) return false;
            uint code = TokenRegion[ClassFromWord(inp[cursor]) + 256];
            uint tokenLen = TokenRegion[code];
            if (cursor + tokenLen > end) return false;
            uint tableBase = 168U * code;
            for (;;)
            {
                uint p = 0;
                if (tokenLen == 0) break;
                int probe = cursor;
                bool done = false;
                while ((ulong)inp[probe] < (1UL << BitWidth[tableBase + p]))
                {
                    ++p; ++probe;
                    if (p >= tokenLen) { done = true; break; }
                }
                if (done) break;
                ++code; tableBase = 168U * code; tokenLen = TokenRegion[code];
                if (tokenLen > (uint)(end - cursor)) break;
            }
            ulong t = (ulong)(byte)code << 56;
            int shift = 56;
            for (uint i = 0; i < (byte)tokenLen; ++i)
            {
                shift -= BitWidth[tableBase + i];
                t |= (ulong)inp[cursor + i] << shift;
            }
            token = t; kind = (byte)code; len = (byte)tokenLen;
            return true;
        }
    
        static bool BuildTokenSecond(char[] inp, int cursor, int end, out ulong token, out byte kind, out byte len)
        {
            token = 0; kind = 0; len = 0;
            if (cursor >= end) return false;
            uint remain = (uint)(end - cursor);
            uint code = TokenRegion[ClassFromWord(inp[cursor]) + 256];
            uint tokenLen = TokenRegion[code];
            if (tokenLen > remain) { code = Fallback[remain]; tokenLen = TokenRegion[code]; }
            uint tableBase = 168U * code;
        retry:
            uint p = 0;
            if (tokenLen != 0)
            {
                int probe = cursor;
                while (probe < end)
                {
                    if ((ulong)inp[probe] >= (1UL << BitWidth[tableBase + p]))
                    {
                        ++code; tableBase = 168U * code; tokenLen = TokenRegion[code];
                        if (tokenLen <= remain) goto retry;
                        break;
                    }
                    ++p; ++probe;
                    if (p >= tokenLen) break;
                }
            }
            ulong t = (ulong)(byte)code << 56;
            int shift = 56;
            for (uint i = 0; i < (byte)tokenLen; ++i)
            {
                if (cursor + i >= end) break;
                shift -= BitWidth[tableBase + i];
                t |= (ulong)inp[cursor + i] << shift;
            }
            token = t; kind = (byte)code; len = (byte)tokenLen;
            return true;
        }
    
        // A run of repeated tokens just ended: emit it as a back-reference and rewind the cursor.
        // False when the repeated token is outside the window (the caller then emits the token).
        static bool TryFlushRepeat(State s, ref int cursor, ulong token, byte kind)
        {
            ulong distance;
            if (!FindPrevious(s, s.Last, out distance)) return false;
            uint rep1 = s.Repeat - 1;
            ulong entry;
            if (distance >= 0x800)
            {
                ulong packed = (token >> 17) | ((ulong)rep1 << 48) | TOK_A;
                MarkSeen(s, packed); Push(s, packed);
                entry = s.Last;
            }
            else
            {
                entry = (token >> 27) | ((ulong)((((uint)(ushort)distance - 1U) & 0x3FFU) | (rep1 << 11)) << 37) | TOK_B;
            }
            MarkSeen(s, entry); Push(s, entry);
            s.Last = SENTINEL; s.Repeat = 0;
            cursor -= (distance >= 0x800) ? LongRewind[kind] : ShortRewind[kind];
            return true;
        }
    
        static void EmitFirst(State s, ref int cursor, int end, char[] inp, ulong token, byte kind)
        {
            if (token == s.Last && s.Repeat < 0xFF) { ++s.Repeat; return; }
            if (s.Repeat != 0)
            {
                if (TryFlushRepeat(s, ref cursor, token, kind)) return;
            }
            else
            {
                ulong distance;
                if (FindPrevious(s, token, out distance) && cursor < end)
                {
                    byte nextKind = TokenRegion[ClassFromWord(inp[cursor]) + 256];
                    ulong entry = distance >= 0x800 ? token : ((token >> 18) | ((distance - 1UL) << 46) | TOK_C);
                    MarkSeen(s, entry); Push(s, entry);
                    if (distance < 0x800) cursor -= ShortRewind[nextKind + 256];
                    s.Last = token;
                    return;
                }
            }
            MarkSeen(s, token); Push(s, token); s.Last = token;
        }
    
        static void EmitSecond(State s, ref int cursor, ulong token, byte kind)
        {
            if (token == s.Last && s.Repeat < 0xFF) { ++s.Repeat; return; }
            if (s.Repeat != 0 && TryFlushRepeat(s, ref cursor, token, kind)) return;
            MarkSeen(s, token); Push(s, token); s.Last = token;
        }
    
        static void FlushSecond(State s)
        {
            if (s.Repeat == 0) return;
            ulong entry;
            if (s.Last == SENTINEL)
            {
                entry = ((ulong)s.Repeat << 48) | TOK_D;
            }
            else
            {
                ulong distance;
                if (FindPrevious(s, s.Last, out distance) && distance != 0)
                {
                    entry = ((ulong)((((uint)(ushort)distance - 1U) & 0x3FFU) | ((s.Repeat - 1U) << 11)) << 37) | TOK_E;
                }
                else
                {
                    ulong rt = ((ulong)s.Repeat << 48) | TOK_D;
                    MarkSeen(s, rt); Push(s, rt);
                    entry = s.Last;
                }
            }
            MarkSeen(s, entry); Push(s, entry);
            s.Repeat = 0; s.Last = SENTINEL;
        }
    
        static void FirstPass(State s, char[] inp, int n, out uint consumed)
        {
            int end = n, cursor = 0;
            ulong result = (2249744775UL * (uint)n) >> 32;
            consumed = 0;
            ulong quota = (result + (((ulong)n - result) >> 1)) >> 7;
            while ((uint)quota > 1)
            {
                uint limit = (uint)quota - 1;
                uint processed = 0;
                while (processed < limit)
                {
                    ulong token; byte kind, len;
                    if (!BuildToken(inp, cursor, end, out token, out kind, out len)) break;
                    consumed += len; cursor += len;
                    EmitFirst(s, ref cursor, end, inp, token, kind);
                    ++processed;
                }
                result = (2249744775UL * (uint)(end - cursor)) >> 32;
                quota = ((uint)result + (((uint)(end - cursor) - (uint)result) >> 1)) >> 7;
            }
        }
    
        static void SecondPass(State s, char[] inp, int start, int end)
        {
            int cursor = start;
            while (cursor < end)
            {
                ulong token; byte kind, len;
                if (!BuildTokenSecond(inp, cursor, end, out token, out kind, out len)) break;
                s.TotalUnits += len; cursor += len;
                EmitSecond(s, ref cursor, token, kind);
            }
            FlushSecond(s);
        }
    
        static void MixA(uint[] p, int count, uint[] md5, uint[] seed, uint[] outPair)
        {
            unchecked
            {
                uint md0 = md5[0] | 1U, md1 = (md5[1] | 1U) + 0x13DB0000U;
                uint sum = 0, acc = 0; int i = 0;
                uint loops = (((uint)count - 2U) >> 1) + 1U;
                uint mul = md0 + seed[0];
                while (loops-- != 0)
                {
                    acc += p[i];
                    uint x = acc, hi = x >> 16; i += 2;
                    uint t = x * mul - hi * seed[3];
                    uint ss = t * seed[1] + (t >> 16) * 0x689B6B9FU;
                    uint part1 = ss * 0xEA970001U - (ss >> 16) * 0x3C101569U;
                    uint w = p[i - 1] + part1;
                    uint t2 = w * md1 - (w >> 16) * seed[2];
                    uint temp = t2 * 0x59C3AF2DU - (t2 >> 16) * 0x2232E0F1U;
                    acc = (temp >> 16) * 0x35BD1EC9U + temp * 0x1EC90001U;
                    sum += part1 + acc;
                }
                outPair[0] = acc; outPair[1] = sum;
            }
        }
    
        static void MixB(uint[] p, int count, uint[] md5, uint[] seed, uint[] outPair)
        {
            unchecked
            {
                uint md1 = md5[1] | 1U, md0 = md5[0] | 1U;
                uint sum = 0, acc = 0; int idx = 0;
                uint loops = (((uint)count - 2U) >> 1) + 1U;
                while (loops-- != 0)
                {
                    uint x = p[idx] + acc; idx += 2;
                    uint t = x * md0;
                    uint a = t * 0xB1110000U - (t >> 16) * 0x30674EEFU;
                    uint b = a * seed[2];
                    uint c = b - (a >> 16) * 0x78F7A461U;
                    c = (c >> 16) * seed[0] - (a >> 16) * 0x164D0000U - b * 0x46930000U;
                    uint d = (c >> 16) * 0x257E1D83U + c * 0x1D830000U;
                    uint e = p[idx - 1] + d;
                    uint f = e * md1;
                    uint g = f * 0x16F50000U - (f >> 16) * 0x5D8BE90BU;
                    uint h = (g >> 16) * seed[3];
                    uint ii = g * 0x69010000U + h;
                    uint j = ii * seed[1];
                    uint k = ((0U - h) - g * 0x69010000U) >> 16;
                    uint m = k * 0x7C932B89U - j;
                    uint nn = k * 0xF2310000U - (m >> 16) * 0x405B6097U;
                    nn += j * 0x60970000U;
                    acc = nn; sum += d + acc;
                }
                outPair[0] = acc; outPair[1] = sum;
            }
        }
    }
}
