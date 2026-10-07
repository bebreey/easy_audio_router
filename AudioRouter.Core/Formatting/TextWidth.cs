namespace AudioRouter.Core.Formatting;

/// <summary>
/// 等宽列对齐工具：必须按**显示宽度**算 —— 中日韩字符占 2 列。
/// 否则中文表格会错位（CLI 里最容易被忽略的细节，所以单独抽出来并被测试覆盖）。
/// </summary>
public static class TextWidth
{
    public static int Of(string text) => text.Sum(c => IsWide(c) ? 2 : 1);

    public static string Pad(string text, int width)
        => text + new string(' ', Math.Max(0, width - Of(text)));

    private static bool IsWide(char c) =>
        (c >= 0x1100 && c <= 0x115F) ||   // 韩文字母
        (c >= 0x2E80 && c <= 0xA4CF) ||   // 中日韩部首/汉字
        (c >= 0xAC00 && c <= 0xD7A3) ||   // 韩文音节
        (c >= 0xF900 && c <= 0xFAFF) ||   // 兼容汉字
        (c >= 0xFE30 && c <= 0xFE6F) ||   // 中日韩兼容符号
        (c >= 0xFF00 && c <= 0xFF60) ||   // 全角字符
        (c >= 0xFFE0 && c <= 0xFFE6);     // 全角符号
}
