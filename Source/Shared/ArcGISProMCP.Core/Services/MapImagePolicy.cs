namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-063 ★ 回图策略（纯逻辑，无 SDK、无 IO —— 可单测；供服务层与 LIVE 判据共用同一口径）。
/// <para>硬约束（工单）：默认 96 DPI、长边 ≤1,200 px、PNG ≤1 MB（超限降采样重试，仍超限 ⇒ 明确报错）。</para>
/// </summary>
public static class MapImagePolicy
{
    /// <summary>长边默认上限（px）。</summary>
    public const int DefaultMaxEdge = 1200;

    /// <summary>默认渲染 DPI。</summary>
    public const int DefaultDpi = 96;

    /// <summary>PNG 字节上限（1 MB）。</summary>
    public const int MaxPngBytes = 1024 * 1024;

    /// <summary>降采样重试次数上限（超限后仍不满足 ⇒ 报错，不截断）。</summary>
    public const int MaxDownscaleAttempts = 3;

    /// <summary>等比把 (w,h) 钳制到长边 ≤ maxEdge（maxEdge ≤ 0 ⇒ 不钳制）。</summary>
    public static void ClampToMaxEdge(ref int w, ref int h, int maxEdge)
    {
        if (maxEdge <= 0)
        {
            return;
        }

        if (w < 1)
        {
            w = 1;
        }

        if (h < 1)
        {
            h = 1;
        }

        var longest = w > h ? w : h;
        if (longest <= maxEdge)
        {
            return;
        }

        var factor = (double)maxEdge / longest;
        w = (int)System.Math.Max(1, System.Math.Round(w * factor));
        h = (int)System.Math.Max(1, System.Math.Round(h * factor));
    }

    /// <summary>PNG magic 判定（89 50 4E 47 0D 0A 1A 0A）。</summary>
    public static bool IsPng(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 8)
        {
            return false;
        }

        return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
    }

    /// <summary>从 PNG IHDR 读取宽高（big-endian）；非 PNG 或长度不足 ⇒ false。</summary>
    public static bool TryReadPngSize(byte[]? bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!IsPng(bytes) || bytes!.Length < 24)
        {
            return false;
        }

        // IHDR 从第 16 字节起：4 长度 + 4 类型 + 4 宽 + 4 高
        width = ReadBigEndianInt32(bytes, 16);
        height = ReadBigEndianInt32(bytes, 20);
        return width > 0 && height > 0;
    }

    private static int ReadBigEndianInt32(byte[] b, int offset)
        => (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];

    /// <summary>是否超出 PNG 字节上限。</summary>
    public static bool ExceedsByteCap(int bytes) => bytes > MaxPngBytes;
}
