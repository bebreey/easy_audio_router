# 已归档：WPF 前端（AudioRouter.Gui）

这个工程**已退休**，不再随解决方案构建。保留在仓库里作为参考实现。

## 为什么退休

它被 `AudioRouter.Desktop` 取代了。两者已经达到**视觉与功能对等**：

| 能力 | WPF 版 | Avalonia 版 |
| --- | --- | --- |
| 真实 WASAPI 枚举 | ✅ | ✅ |
| 拖拽路由（应用→设备） | ✅ 自绘拖拽引擎 | ✅ 自绘拖拽引擎（指针捕获 + 命中测试） |
| chip 拖回左侧移除（含撤销） | ✅ | ✅ |
| 右键菜单（路由到 / 复制到其他设备） | ✅ | ✅ |
| 「复制到多个设备」对话框 | ✅ | ✅ |
| 语言切换 + 导入 + 打开目录 | ✅ | ✅ |
| 真实 exe 图标 | ✅ | ✅ |
| Toast + 撤销 | ✅ | ✅ |
| 设备格式信息（位深/采样率） | ✅ | ✅ |
| 自绘标题栏 | ✅ | ✅ |
| 跨平台 | ❌ 仅 Windows | ✅ Windows / Linux / macOS |

## 共享资产已搬走

**语言包不再属于这个工程**，已移到核心层：

```
AudioRouter.Core/Languages/{zh-CN,en-US}.json
```

CLI、Avalonia 以及本工程都通过 `Link` 引用同一份（单一来源，不复制内容）。
**所以不要在这里再建 `Languages/` 目录** —— 会出现两份、日后不一致。

## 如果要重新构建它

```powershell
dotnet build AudioRouter.Gui\AudioRouter.Gui.csproj -c Debug
```

它仍然引用 `AudioRouter.Core`，因此核心层的能力演进对它是自动生效的。

## 相关记录

设计与实现记录见 `docs/UI-DESIGN.md`：
- A.11 把 GUI 收敛到 Core
- A.15 右键菜单与复制对话框
- A.16 真实图标与语言菜单
- A.19 自绘标题栏（含 Avalonia 12 的 API 变更记录）
