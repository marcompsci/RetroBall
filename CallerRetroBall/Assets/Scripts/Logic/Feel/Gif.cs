using System;
using System.Collections.Generic;
using System.IO;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Small, original animated-GIF encoder for sharing highlights (no plugins, no third-party code).
    /// Colours are mapped to a fixed 252-colour cube (6 red × 7 green × 6 blue levels), which suits the
    /// game's flat pixel art. Frames are LZW-compressed per the GIF89a spec and the file loops forever.
    /// </summary>
    public static class GifEncoder
    {
        private const int RLevels = 6, GLevels = 7, BLevels = 6;
        public const int PaletteSize = RLevels * GLevels * BLevels; // 252

        /// <summary>Palette index for an RGB colour (nearest level per channel).</summary>
        public static byte Index(byte r, byte g, byte b)
        {
            int ri = (r * (RLevels - 1) + 127) / 255;
            int gi = (g * (GLevels - 1) + 127) / 255;
            int bi = (b * (BLevels - 1) + 127) / 255;
            return (byte)((ri * GLevels + gi) * BLevels + bi);
        }

        /// <summary>The colour a palette index stands for.</summary>
        public static void PaletteColor(int index, out byte r, out byte g, out byte b)
        {
            int bi = index % BLevels;
            int gi = (index / BLevels) % GLevels;
            int ri = index / (BLevels * GLevels);
            r = (byte)(ri * 255 / (RLevels - 1));
            g = (byte)(gi * 255 / (GLevels - 1));
            b = (byte)(bi * 255 / (BLevels - 1));
        }

        /// <summary>
        /// Encodes frames of RGB24 pixels (rows top to bottom, 3 bytes per pixel).
        /// <paramref name="delayCs"/> is the time per frame in hundredths of a second.
        /// </summary>
        public static byte[] Encode(int width, int height, IList<byte[]> rgbFrames, int delayCs)
        {
            if (width <= 0 || height <= 0 || width > 65535 || height > 65535) throw new ArgumentOutOfRangeException(nameof(width));
            if (rgbFrames == null || rgbFrames.Count == 0) throw new ArgumentException("No frames.", nameof(rgbFrames));
            using (var ms = new MemoryStream())
            {
                var w = new BinaryWriter(ms);
                w.Write(new[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' });
                w.Write((ushort)width);
                w.Write((ushort)height);
                w.Write((byte)0xF7); // global colour table, 8 bits/colour, 256 entries
                w.Write((byte)0);    // background colour
                w.Write((byte)0);    // aspect ratio
                for (int i = 0; i < 256; i++)
                {
                    if (i < PaletteSize)
                    {
                        PaletteColor(i, out byte r, out byte g, out byte b);
                        w.Write(r); w.Write(g); w.Write(b);
                    }
                    else { w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); }
                }
                // Loop forever (NETSCAPE2.0 application extension).
                w.Write(new byte[] { 0x21, 0xFF, 0x0B });
                w.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                w.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });

                var indexed = new byte[width * height];
                foreach (var frame in rgbFrames)
                {
                    if (frame == null || frame.Length < width * height * 3) throw new ArgumentException("Frame is the wrong size.");
                    for (int p = 0, q = 0; p < indexed.Length; p++, q += 3) indexed[p] = Index(frame[q], frame[q + 1], frame[q + 2]);
                    // Graphic control extension: delay, no transparency.
                    w.Write(new byte[] { 0x21, 0xF9, 0x04, 0x00 });
                    w.Write((ushort)Math.Max(2, delayCs));
                    w.Write(new byte[] { 0x00, 0x00 });
                    // Image descriptor: full frame, no local table.
                    w.Write((byte)0x2C);
                    w.Write((ushort)0); w.Write((ushort)0);
                    w.Write((ushort)width); w.Write((ushort)height);
                    w.Write((byte)0);
                    WriteLzw(w, indexed, 8);
                }
                w.Write((byte)0x3B); // trailer
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>GIF variable-width LZW with clear codes, written as sub-blocks.</summary>
        private static void WriteLzw(BinaryWriter w, byte[] data, int minCodeSize)
        {
            w.Write((byte)minCodeSize);
            int clear = 1 << minCodeSize, end = clear + 1;
            var packer = new BitPacker(w);
            var table = new Dictionary<int, int>(4096);
            int codeSize = minCodeSize + 1, next = end + 1;
            packer.Put(clear, codeSize);
            int prefix = data.Length > 0 ? data[0] : 0;
            for (int i = 1; i < data.Length; i++)
            {
                int k = data[i];
                int key = (prefix << 8) | k;
                if (table.TryGetValue(key, out int code))
                {
                    prefix = code;
                    continue;
                }
                packer.Put(prefix, codeSize);
                if (next < 4096)
                {
                    table[key] = next++;
                    if (next > (1 << codeSize) && codeSize < 12) codeSize++;
                }
                else
                {
                    // Table full: start over.
                    packer.Put(clear, codeSize);
                    table.Clear();
                    codeSize = minCodeSize + 1;
                    next = end + 1;
                }
                prefix = k;
            }
            packer.Put(prefix, codeSize);
            packer.Put(end, codeSize);
            packer.Flush();
            w.Write((byte)0); // block terminator
        }

        private sealed class BitPacker
        {
            private readonly BinaryWriter _w;
            private readonly byte[] _block = new byte[255];
            private int _count, _bits, _acc;

            public BitPacker(BinaryWriter w) => _w = w;

            public void Put(int code, int size)
            {
                _acc |= code << _bits;
                _bits += size;
                while (_bits >= 8)
                {
                    Byte((byte)(_acc & 0xFF));
                    _acc >>= 8;
                    _bits -= 8;
                }
            }

            private void Byte(byte b)
            {
                _block[_count++] = b;
                if (_count == 255) WriteBlock();
            }

            private void WriteBlock()
            {
                if (_count == 0) return;
                _w.Write((byte)_count);
                _w.Write(_block, 0, _count);
                _count = 0;
            }

            public void Flush()
            {
                if (_bits > 0) Byte((byte)(_acc & 0xFF));
                _acc = 0;
                _bits = 0;
                WriteBlock();
            }
        }
    }

    /// <summary>Which replay frames to grab for a highlight GIF.</summary>
    public static class HighlightClip
    {
        public const int Fps = 15;
        public const int Width = 270;
        public const int MaxFrames = 60;

        /// <summary>Output height for a screen aspect, rounded to even pixels.</summary>
        public static int HeightFor(int screenW, int screenH) =>
            Math.Max(2, (int)Math.Round(Width * (screenH / (double)Math.Max(1, screenW)) / 2.0) * 2);

        public static string FileName(DateTime now) => "RetroBall_" + now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".gif";
    }
}
