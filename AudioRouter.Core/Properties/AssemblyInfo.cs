using System.Runtime.CompilerServices;

// Core 的实现细节（pactl 解析器、表格宽度工具等）对测试工程开放，
// 这样纯逻辑就能在没有对应操作系统的机器上被验证。
[assembly: InternalsVisibleTo("AudioRouter.Tests")]
