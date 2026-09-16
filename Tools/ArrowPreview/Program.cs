using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

// Оффлайн-превью силуэта стрелки: те же числа, что в GameConstants/ArrowController/ArrowSpriteFactory.
internal static class Program
{
    private const float PathWidth = 0.42f;
    private const float HeadWidth = 0.92f;
    private const float HeadLength = 0.72f;
    private const float TipReach = 0.48f;
    private const float HeadJoint = TipReach - HeadLength;
    private const float SingleCellTail = 0.27f;
    private const float PolylineTail = 0.26f;

    private const int Px = 96;      // пикселей на клетку
    private const int Cols = 7;
    private const int Rows = 7;

    private static void Main()
    {
        int w = Cols * Px;
        int h = Rows * Px;
        var rgba = new byte[w * h * 4];
        Fill(rgba, w, h, 26, 20, 51);

        // Одноклеточная стрелка вверх.
        DrawArrow(rgba, w, h, new[] { V(1f, 1f) }, Dir.Up, 0.15f, 0.90f, 0.45f);
        // Прямая из трёх клеток вправо.
        DrawArrow(rgba, w, h, new[] { V(3f, 1f), V(4f, 1f), V(5f, 1f) }, Dir.Right, 1.00f, 0.86f, 0.18f);
        // Угол.
        DrawArrow(rgba, w, h, new[] { V(1f, 3f), V(1f, 4f), V(2f, 4f), V(3f, 4f) }, Dir.Right, 1.00f, 0.22f, 0.72f);
        // Зигзаг.
        DrawArrow(rgba, w, h, new[] { V(5f, 3f), V(5f, 4f), V(4f, 4f), V(4f, 5f), V(5f, 5f), V(5f, 6f) }, Dir.Up, 0.62f, 0.32f, 1.00f);
        // Две соседние вертикали — проверка, что линии не сливаются.
        DrawArrow(rgba, w, h, new[] { V(1f, 5f), V(1f, 6f) }, Dir.Up, 0.15f, 0.90f, 1.00f);
        DrawArrow(rgba, w, h, new[] { V(2f, 5f), V(2f, 6f) }, Dir.Up, 0.20f, 0.95f, 0.45f);

        string path = Path.Combine(AppContext.BaseDirectory, "arrow_preview.png");
        WritePng(path, rgba, w, h);
        Console.WriteLine(path);
    }

    private enum Dir { Up, Right, Down, Left }

    private static (float x, float y) V(float x, float y) => (x, y);

    private static (float x, float y) DirVec(Dir d) => d switch
    {
        Dir.Up => (0f, 1f),
        Dir.Right => (1f, 0f),
        Dir.Down => (0f, -1f),
        _ => (-1f, 0f)
    };

    private static void DrawArrow(byte[] rgba, int w, int h, (float x, float y)[] cells, Dir dir, float r, float g, float b)
    {
        var pts = new List<(float x, float y)>();
        var (dx, dy) = DirVec(dir);

        if (cells.Length <= 1)
        {
            pts.Add((cells[0].x - dx * SingleCellTail, cells[0].y - dy * SingleCellTail));
            pts.Add((cells[0].x + dx * HeadJoint, cells[0].y + dy * HeadJoint));
        }
        else
        {
            foreach (var c in cells) pts.Add(c);

            var (fx, fy) = Normalize(pts[1].x - pts[0].x, pts[1].y - pts[0].y);
            pts[0] = (pts[0].x - fx * PolylineTail, pts[0].y - fy * PolylineTail);

            int n = pts.Count - 1;
            var (lx, ly) = Normalize(pts[n].x - pts[n - 1].x, pts[n].y - pts[n - 1].y);
            pts[n] = (pts[n].x + lx * HeadJoint, pts[n].y + ly * HeadJoint);
        }

        int last = pts.Count - 1;
        var (tx, ty) = Normalize(pts[last].x - pts[last - 1].x, pts[last].y - pts[last - 1].y);
        var headBase = pts[last];
        var tip = (x: headBase.x + tx * HeadLength, y: headBase.y + ty * HeadLength);
        float halfHead = HeadWidth * 0.5f;
        var headLeft = (x: headBase.x - ty * halfHead, y: headBase.y + tx * halfHead);
        var headRight = (x: headBase.x + ty * halfHead, y: headBase.y - tx * halfHead);

        float aa = 1.2f / Px;

        for (int py = 0; py < h; py++)
        {
            float ny = (h - py - 0.5f) / Px;
            for (int px = 0; px < w; px++)
            {
                float nx = (px + 0.5f) / Px;

                float d = float.MaxValue;
                for (int i = 0; i < pts.Count - 1; i++)
                    d = MathF.Min(d, SdSegment(nx, ny, pts[i], pts[i + 1]) - PathWidth * 0.5f);

                d = MathF.Min(d, SdTriangle(nx, ny, headLeft, headRight, tip) - 0.02f);

                float alpha = Math.Clamp(0.5f - d / aa, 0f, 1f);
                if (alpha <= 0f) continue;

                int o = (py * w + px) * 4;
                rgba[o] = Blend(rgba[o], r, alpha);
                rgba[o + 1] = Blend(rgba[o + 1], g, alpha);
                rgba[o + 2] = Blend(rgba[o + 2], b, alpha);
            }
        }
    }

    private static byte Blend(byte dst, float src, float a)
        => (byte)Math.Clamp(dst * (1f - a) + src * 255f * a, 0f, 255f);

    private static (float, float) Normalize(float x, float y)
    {
        float m = MathF.Sqrt(x * x + y * y);
        return m < 1e-5f ? (0f, 1f) : (x / m, y / m);
    }

    private static float SdSegment(float px, float py, (float x, float y) a, (float x, float y) b)
    {
        float pax = px - a.x, pay = py - a.y;
        float bax = b.x - a.x, bay = b.y - a.y;
        float denom = bax * bax + bay * bay;
        float t = denom < 1e-8f ? 0f : Math.Clamp((pax * bax + pay * bay) / denom, 0f, 1f);
        float cx = pax - bax * t, cy = pay - bay * t;
        return MathF.Sqrt(cx * cx + cy * cy);
    }

    private static float SdTriangle(float px, float py, (float x, float y) a, (float x, float y) b, (float x, float y) c)
    {
        float e0x = b.x - a.x, e0y = b.y - a.y;
        float e1x = c.x - b.x, e1y = c.y - b.y;
        float e2x = a.x - c.x, e2y = a.y - c.y;
        float v0x = px - a.x, v0y = py - a.y;
        float v1x = px - b.x, v1y = py - b.y;
        float v2x = px - c.x, v2y = py - c.y;

        float t0 = Math.Clamp((v0x * e0x + v0y * e0y) / (e0x * e0x + e0y * e0y), 0f, 1f);
        float t1 = Math.Clamp((v1x * e1x + v1y * e1y) / (e1x * e1x + e1y * e1y), 0f, 1f);
        float t2 = Math.Clamp((v2x * e2x + v2y * e2y) / (e2x * e2x + e2y * e2y), 0f, 1f);

        float p0x = v0x - e0x * t0, p0y = v0y - e0y * t0;
        float p1x = v1x - e1x * t1, p1y = v1y - e1y * t1;
        float p2x = v2x - e2x * t2, p2y = v2y - e2y * t2;

        float s = MathF.Sign(e0x * e2y - e0y * e2x);
        float dsq = MathF.Min(p0x * p0x + p0y * p0y, MathF.Min(p1x * p1x + p1y * p1y, p2x * p2x + p2y * p2y));
        float sgn = MathF.Min(s * (v0x * e0y - v0y * e0x), MathF.Min(s * (v1x * e1y - v1y * e1x), s * (v2x * e2y - v2y * e2x)));
        return -MathF.Sqrt(dsq) * MathF.Sign(sgn);
    }

    private static void Fill(byte[] rgba, int w, int h, byte r, byte g, byte b)
    {
        for (int i = 0; i < w * h; i++)
        {
            rgba[i * 4] = r;
            rgba[i * 4 + 1] = g;
            rgba[i * 4 + 2] = b;
            rgba[i * 4 + 3] = 255;
        }
    }

    private static void WritePng(string path, byte[] rgba, int w, int h)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var ihdr = new byte[13];
        WriteBe(ihdr, 0, w);
        WriteBe(ihdr, 4, h);
        ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        WriteChunk(fs, "IHDR", ihdr);

        var raw = new byte[h * (w * 4 + 1)];
        for (int y = 0; y < h; y++)
        {
            raw[y * (w * 4 + 1)] = 0;
            Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
        }

        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x01);
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, true))
            deflate.Write(raw, 0, raw.Length);
        WriteBe32(ms, Adler32(raw));
        WriteChunk(fs, "IDAT", ms.ToArray());
        WriteChunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void WriteBe(byte[] dst, int offset, int value)
    {
        dst[offset] = (byte)(value >> 24);
        dst[offset + 1] = (byte)(value >> 16);
        dst[offset + 2] = (byte)(value >> 8);
        dst[offset + 3] = (byte)value;
    }

    private static void WriteBe32(Stream s, uint value)
    {
        s.WriteByte((byte)(value >> 24));
        s.WriteByte((byte)(value >> 16));
        s.WriteByte((byte)(value >> 8));
        s.WriteByte((byte)value);
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var head = new byte[4];
        WriteBe(head, 0, data.Length);
        s.Write(head);

        var full = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) full[i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, full, 4, data.Length);
        s.Write(full);
        WriteBe32(s, Crc32(full));
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (byte v in data)
        {
            a = (a + v) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte v in data)
        {
            crc ^= v;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320 & (uint)-(crc & 1));
        }
        return crc ^ 0xFFFFFFFF;
    }
}
