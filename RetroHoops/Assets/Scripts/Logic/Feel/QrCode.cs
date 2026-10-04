using System;
using System.Collections.Generic;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Minimal QR Code encoder (ISO/IEC 18004): byte mode, error correction level M, versions 1–10
    /// (up to 213 bytes), automatic mask choice. Used to share Kit Studio kits as a scannable image:
    /// the iPhone camera opens the retrohoops://kit/ link. Original implementation; unit-tested and
    /// checked against a reference decoder.
    /// </summary>
    public sealed class QrCode
    {
        public const int MinVersion = 1;
        public const int MaxVersion = 10;

        // Error correction level M, versions 1..10 (index 0 unused).
        private static readonly int[] EccPerBlock = { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
        private static readonly int[] NumBlocks = { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 };

        public readonly int Version;
        public readonly int Size;
        public readonly int Mask;
        private readonly bool[,] _modules;
        private readonly bool[,] _isFunction;

        /// <summary>Dark module at (x, y), with (0, 0) the top-left corner.</summary>
        public bool Get(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size && _modules[y, x];

        /// <summary>Encodes UTF-8 text. Throws if it doesn't fit in version 10-M (213 bytes).</summary>
        public static QrCode EncodeText(string text) => EncodeBytes(Encoding.UTF8.GetBytes(text ?? ""));

        public static QrCode EncodeBytes(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            for (int v = MinVersion; v <= MaxVersion; v++)
            {
                int capacityBits = DataCodewords(v) * 8;
                int needed = 4 + (v <= 9 ? 8 : 16) + data.Length * 8;
                if (needed <= capacityBits) return new QrCode(v, data);
            }
            throw new ArgumentException("Too much data for a version 1-10 QR code (" + data.Length + " bytes).");
        }

        private QrCode(int version, byte[] data)
        {
            Version = version;
            Size = version * 4 + 17;
            _modules = new bool[Size, Size];
            _isFunction = new bool[Size, Size];
            DrawFunctionPatterns();
            var codewords = AddEccAndInterleave(MakeDataCodewords(version, data));
            DrawCodewords(codewords);

            // Pick the mask with the lowest penalty.
            int best = 0;
            long bestPenalty = long.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                ApplyMask(m);
                DrawFormatBits(m);
                long p = Penalty();
                if (p < bestPenalty)
                {
                    bestPenalty = p;
                    best = m;
                }
                ApplyMask(m); // undo (XOR)
            }
            Mask = best;
            ApplyMask(best);
            DrawFormatBits(best);
        }

        // ------------------------------------------------------------------ data

        private static List<byte> MakeDataCodewords(int version, byte[] data)
        {
            var bits = new List<bool>();
            void Append(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
            }
            Append(0x4, 4); // byte mode
            Append(data.Length, version <= 9 ? 8 : 16);
            foreach (byte b in data) Append(b, 8);
            int capacity = DataCodewords(version) * 8;
            Append(0, Math.Min(4, capacity - bits.Count)); // terminator
            Append(0, (8 - bits.Count % 8) % 8);
            var result = new List<byte>();
            for (int i = 0; i < bits.Count; i += 8)
            {
                int v = 0;
                for (int j = 0; j < 8; j++) v = (v << 1) | (bits[i + j] ? 1 : 0);
                result.Add((byte)v);
            }
            for (int pad = 0xEC; result.Count < DataCodewords(version); pad ^= 0xEC ^ 0x11) result.Add((byte)pad);
            return result;
        }

        private byte[] AddEccAndInterleave(List<byte> data)
        {
            int blocks = NumBlocks[Version];
            int ecc = EccPerBlock[Version];
            int raw = RawDataModules(Version) / 8;
            int shortBlocks = blocks - raw % blocks;
            int shortLen = raw / blocks;
            var divisor = RsDivisor(ecc);
            var all = new List<byte[]>();
            int k = 0;
            for (int i = 0; i < blocks; i++)
            {
                int dataLen = shortLen - ecc + (i < shortBlocks ? 0 : 1);
                var dat = data.GetRange(k, dataLen).ToArray();
                k += dataLen;
                var rem = RsRemainder(dat, divisor);
                var block = new byte[shortLen + 1];
                Array.Copy(dat, 0, block, 0, dat.Length);
                // Short blocks leave a gap at index dataLen so columns line up when interleaving.
                Array.Copy(rem, 0, block, block.Length - ecc, ecc);
                all.Add(block);
            }
            var result = new List<byte>(raw);
            for (int i = 0; i < shortLen + 1; i++)
                for (int j = 0; j < all.Count; j++)
                    if (i != shortLen - ecc || j >= shortBlocks) result.Add(all[j][i]);
            return result.ToArray();
        }

        // ------------------------------------------------------------------ modules

        private void SetFunction(int x, int y, bool dark)
        {
            _modules[y, x] = dark;
            _isFunction[y, x] = true;
        }

        private void DrawFunctionPatterns()
        {
            for (int i = 0; i < Size; i++)
            {
                SetFunction(6, i, i % 2 == 0); // timing
                SetFunction(i, 6, i % 2 == 0);
            }
            DrawFinder(3, 3);
            DrawFinder(Size - 4, 3);
            DrawFinder(3, Size - 4);
            var align = AlignmentPositions(Version);
            int n = align.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)) continue; // finder corners
                    DrawAlignment(align[i], align[j]);
                }
            DrawFormatBits(0); // reserve
            DrawVersion();
        }

        private void DrawFinder(int cx, int cy)
        {
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && x < Size && y >= 0 && y < Size) SetFunction(x, y, d != 2 && d != 4);
                }
        }

        private void DrawAlignment(int cx, int cy)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    SetFunction(cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        private void DrawFormatBits(int mask)
        {
            int data = (0 << 3) | mask; // level M = 00
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = ((data << 10) | rem) ^ 0x5412;
            bool Bit(int i) => ((bits >> i) & 1) != 0;
            // First copy (around the top-left finder).
            for (int i = 0; i <= 5; i++) SetFunction(8, i, Bit(i));
            SetFunction(8, 7, Bit(6));
            SetFunction(8, 8, Bit(7));
            SetFunction(7, 8, Bit(8));
            for (int i = 9; i < 15; i++) SetFunction(14 - i, 8, Bit(i));
            // Second copy (top-right and bottom-left).
            for (int i = 0; i < 8; i++) SetFunction(Size - 1 - i, 8, Bit(i));
            for (int i = 8; i < 15; i++) SetFunction(8, Size - 15 + i, Bit(i));
            SetFunction(8, Size - 8, true); // dark module
        }

        private void DrawVersion()
        {
            if (Version < 7) return;
            int rem = Version;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = (Version << 12) | rem;
            for (int i = 0; i < 18; i++)
            {
                bool bit = ((bits >> i) & 1) != 0;
                int a = Size - 11 + i % 3, b = i / 3;
                SetFunction(a, b, bit);
                SetFunction(b, a, bit);
            }
        }

        private void DrawCodewords(byte[] data)
        {
            int i = 0;
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5; // skip the vertical timing column
                for (int vert = 0; vert < Size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? Size - 1 - vert : vert;
                        if (_isFunction[y, x] || i >= data.Length * 8) continue;
                        _modules[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                        i++;
                    }
            }
        }

        private void ApplyMask(int mask)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (_isFunction[y, x]) continue;
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (invert) _modules[y, x] = !_modules[y, x];
                }
        }

        // ------------------------------------------------------------------ penalty (mask choice)

        private long Penalty()
        {
            long result = 0;
            // Runs of five or more same-colour modules in a row or column, and finder-like patterns.
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < Size; a++)
                {
                    int run = 0;
                    bool prev = false;
                    for (int b = 0; b < Size; b++)
                    {
                        bool m = pass == 0 ? _modules[a, b] : _modules[b, a];
                        if (b > 0 && m == prev) run++;
                        else
                        {
                            if (run >= 5) result += run - 2;
                            run = 1;
                        }
                        prev = m;
                    }
                    if (run >= 5) result += run - 2;
                    for (int b = 0; b + 10 < Size; b++)
                    {
                        if (FinderLike(pass, a, b, true) || FinderLike(pass, a, b, false)) result += 40;
                    }
                }
            // 2x2 blocks.
            for (int y = 0; y < Size - 1; y++)
                for (int x = 0; x < Size - 1; x++)
                {
                    bool c = _modules[y, x];
                    if (c == _modules[y, x + 1] && c == _modules[y + 1, x] && c == _modules[y + 1, x + 1]) result += 3;
                }
            // Balance of dark and light.
            int dark = 0;
            foreach (bool m in _modules) if (m) dark++;
            int total = Size * Size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += Math.Max(0, k) * 10;
            return result;
        }

        private bool FinderLike(int pass, int a, int b, bool lightFirst)
        {
            // 1:1:3:1:1 dark pattern with four light modules on one side.
            bool[] pattern = lightFirst
                ? new[] { false, false, false, false, true, false, true, true, true, false, true }
                : new[] { true, false, true, true, true, false, true, false, false, false, false };
            for (int i = 0; i < 11; i++)
            {
                bool m = pass == 0 ? _modules[a, b + i] : _modules[b + i, a];
                if (m != pattern[i]) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ tables & maths

        private static int RawDataModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7) result -= 36;
            }
            return result;
        }

        public static int DataCodewords(int ver) => RawDataModules(ver) / 8 - EccPerBlock[ver] * NumBlocks[ver];

        private static int[] AlignmentPositions(int ver)
        {
            if (ver == 1) return new int[0];
            int numAlign = ver / 7 + 2;
            int size = ver * 4 + 17;
            int step = (ver * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
            var result = new int[numAlign];
            result[0] = 6;
            for (int i = numAlign - 1, pos = size - 7; i >= 1; i--, pos -= step) result[i] = pos;
            return result;
        }

        private static byte[] RsDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < result.Length; j++)
                {
                    result[j] = (byte)Multiply(result[j], root);
                    if (j + 1 < result.Length) result[j] ^= result[j + 1];
                }
                root = Multiply(root, 0x02);
            }
            return result;
        }

        private static byte[] RsRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (byte b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++) result[i] ^= (byte)Multiply(divisor[i], factor);
            }
            return result;
        }

        private static int Multiply(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return z & 0xFF;
        }

        /// <summary>Draws the code with a 4-module quiet zone, <paramref name="scale"/> pixels per module.</summary>
        public PixelArt.PixelCanvas ToCanvas(int scale, RgbColor dark, RgbColor light)
        {
            const int quiet = 4;
            int n = (Size + quiet * 2) * scale;
            var c = new PixelArt.PixelCanvas(n, n);
            c.Fill(light);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (Get(x, y))
                        // Canvas row 0 is the bottom: flip so the code reads the right way up.
                        c.FillRect((x + quiet) * scale, (Size - 1 - y + quiet) * scale, scale, scale, dark);
            return c;
        }
    }
}
