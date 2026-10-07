# Audio Router GUI · 界面与交互设计文档

> 版本 v1.0 · 设计目标：PCL 启动器风格的现代化 Windows 桌面工具
> 适用对象：`audio-router` 项目的下一代图形界面（替换现有 `audio-router-gui`）
> 状态：设计稿，待统一验收

---

## 0. 设计原则（先定调，再画像素）

| 原则 | 含义 | 落地表现 |
| --- | --- | --- |
| **一眼看懂** | 用户打开窗口 3 秒内知道"左边是我的应用，右边是我的设备" | 两栏强分割 + 文案直白，不用图标让人猜 |
| **拖一下就完事** | 核心操作只有一个动作：拖 | 全窗口唯一的主交互路径，其余都是加速器 |
| **半透明≠真毛玻璃** | 追求质感，但不为质感牺牲性能 | 用"半透明 + 分层阴影 + 噪点"复刻 PCL 观感，窗口层才挂真 Mica |
| **状态永远可见** | 用户必须随时知道"什么被路由到哪了" | 卡片内 chip 常驻 + 左侧条目路由徽章 `⇢ 2` |
| **不丢数据** | 设备拔了、应用退了，路由不能静默消失 | 挂起态 + 明确提示 + 设备回来自动恢复 |
| **深色优先** | 深色是默认与主设计态，浅色是派生 | Token 层双主题，深色全量验收后才做浅色 |

---

## 1. 设计语言（Design Tokens）

> 全部 Token 集中在 `Theme.xaml` / `Tokens.json`，禁止在视图里写硬编码颜色与圆角。

### 1.1 颜色（深色主题 · 默认）

| Token | 值 | 用途 |
| --- | --- | --- |
| `bg.window` | `#16181D` | 窗口底色（Mica 背景层） |
| `bg.surface` | `#1E2026` @ 92% | 一级卡片（应用条目、设备卡） |
| `bg.surface2` | `#262931` | 二级容器（已路由 chip、折叠区头） |
| `bg.sunken` | `#121317` | 拖放区凹陷背景 |
| `bg.hover` | `rgba(255,255,255,0.06)` | 悬停 |
| `bg.pressed` | `rgba(255,255,255,0.10)` | 按下 |
| `border.subtle` | `rgba(255,255,255,0.08)` | 卡片描边、分隔线 |
| `border.strong` | `rgba(255,255,255,0.14)` | 输入框、选中态描边 |
| `text.primary` | `#F2F4F8` | 标题、应用名、设备名 |
| `text.secondary` | `#A8AEBB` | 副标题、次要信息 |
| `text.tertiary` | `#6E7686` | 进程名、PID、占位文案 |
| `text.disabled` | `#4A4F5A` | 已禁用设备 |
| `accent` | `#4C9DFF` | 主题色（可换肤，默认 PCL 蓝） |
| `accent.hover` | `#6BB0FF` | 主题色悬停 |
| `accent.weak` | `rgba(76,157,255,0.14)` | 选中底、默认设备徽章底、拖放高亮底 |
| `state.success` | `#3DDC97` | 路由成功、设备在线 |
| `state.warning` | `#FFB454` | 设备挂起、路由降级 |
| `state.danger` | `#FF6B6B` | 移除、断开、错误 |
| `state.muted` | `#8A8F99` | 静音图标与静音条目 |

**主题色可换肤**：预设 6 色（蓝 / 青 / 紫 / 粉 / 橙 / 绿），用户选择后只替换 `accent*` 三个 Token。

### 1.2 圆角 / 间距 / 描边

| Token | 值 | 用途 |
| --- | --- | --- |
| `radius.xs` / `sm` / `md` / `lg` / `xl` | 4 / 6 / 8 / 12 / 16 | 徽章 / chip / 条目 / 卡片 / 对话框 |
| `space.1` ~ `space.6` | 4 / 8 / 12 / 16 / 20 / 24 | 全部间距取 4 的倍数 |
| `stroke.hairline` | 1px | 卡片描边、分隔线 |
| `stroke.focus` | 2px `accent` 外发光 | 键盘焦点环 |
| `stroke.dropdash` | 1px dashed `border.strong` | 空拖放区 |

### 1.3 字体

| 层级 | 字体 / 字号 / 字重 | 用途 |
| --- | --- | --- |
| 标题 | Segoe UI Variable Display / 15 / Semibold | 栏目标题、设备名 |
| 正文强调 | Segoe UI Variable Text / 13.5 / Semibold | 应用名 |
| 正文 | Segoe UI Variable Text / 13 / Regular | 一般文本 |
| 次要 | Segoe UI Variable Text / 12 / Regular | 进程名、设备类型 |
| 数字 | Segoe UI Variable Text / 12 / Medium + 等宽数字 | 音量百分比、PID、采样率 |

中文回落链：`微软雅黑 UI → Microsoft YaHei → 思源黑体`。窗口缩放 100%–200% 全量适配（避免半像素模糊）。

### 1.4 阴影 / 层级

| 层级 | 阴影 | 用途 |
| --- | --- | --- |
| L0 平面 | 无 | 分隔线、chip |
| L1 卡片 | `0 1 3 rgba(0,0,0,0.40)` | 应用条目、设备卡 |
| L2 抬起 | `0 4 12 rgba(0,0,0,0.45)` | 卡片悬停、拖拽源 |
| L3 浮层 | `0 8 24 rgba(0,0,0,0.55)` | 右键菜单、对话框 |
| L4 拖拽 | `0 12 32 rgba(0,0,0,0.60)` | 跟随光标的拖拽预览 |

### 1.5 动效

| 场景 | 时长 | 缓动 |
| --- | --- | --- |
| 悬停 / 按下 | 120ms | ease-out |
| 进 / 出场 | 160ms | `cubic-bezier(0.2, 0, 0, 1)` |
| 拖拽抬起 / 卡片高亮 | 140ms | ease-out |
| 路由成功 chip 插入 | 180ms | 弹性 `cubic-bezier(0.34,1.2,0.64,1)` |
| 菜单弹出 | 120ms | 淡入 + Y 位移 4px |

**红线**：任何交互反馈不超过 200ms；不做无意义的装饰性动画；尊重系统「减少动态效果」设置（开启后全部降级为淡入淡出）。

### 1.6 毛玻璃实现策略（关键工程判断）

| 层级 | 方案 | 说明 |
| --- | --- | --- |
| 窗口层 | Win11：`DWMWA_SYSTEMBACKDROP_TYPE = Mica Alt`；Win10 1803+：`SetWindowCompositionAttribute(ACCENT_ENABLE_BLURBEHIND)` | 真背景模糊只在窗口层有意义 |
| 应用内卡片层 | **半透明填充 + 1px 高光描边 + 分层阴影 + 2% 噪点纹理** | 视窗内部没有"背后的东西"可模糊，硬上 Acrylic 只会掉帧 |
| 可选增强 | `CompositionBackdropBrush`（Win10 1809+ / Win2D）做卡片级模糊 | 作为「增强质感」开关，默认关闭 |
| 兜底 | 透明失效时降级为不透明 `bg.surface` | 绝不允许视觉崩坏 |

> **结论**：PCL 的"半透明质感"本质是**半透明 + 阴影 + 圆角**，不是真 blur。照抄它的观感，而不是照抄概念。

---

## 2. 整体界面结构

### 2.1 布局线框

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  ⬤ Audio Router          [🔍 搜索应用…]              ▣ 主题  ⚙  ● ─ □ ✕      │  标题栏 44px
├───────────────────────────┬──────────────────────────────────────────────────┤
│  应用程序        ⇢ 3 路由 │  音频设备                          2 默认 · 5 在线 │  栏头 40px
├───────────────────────────┼──────────────────────────────────────────────────┤
│ ┌───────────────────────┐ │ ┌──────────────────────────────────────────────┐ │
│ │ 🎵 Spotify            │ │ │ 🔊  扬声器 (Realtek)      ★ 默认   扬声器    │ │
│ │    spotify.exe · 9124 │ │ │ ──────────────────────────────────────────── │ │
│ │ 60% ▮▮▮▯▯          ⇢2 │ │ │ 🎵 Spotify  🔇  ✕   🎬 播放器  🔇  ✕        │ │
│ └───────────────────────┘ │ │ ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐ │ │
│ ┌───────────────────────┐ │ │   拖入应用以路由到此处                      │ │ │
│ │ 🎬 播放器             │ │ │ └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘ │ │
│ │   vlc.exe · 3322      │ │ └──────────────────────────────────────────────┘ │
│ │ 静音 🔇            ⇢1 │ │ ┌──────────────────────────────────────────────┐ │
│ └───────────────────────┘ │ │ 🎧  耳机 (WH-1000XM5)             耳机       │ │
│ ┌───────────────────────┐ │ │ ──────────────────────────────────────────── │ │
│ │ 🌐 Chrome             │ │ │ ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐ │ │
│ │   chrome.exe · 12044  │ │ │   拖入应用以路由到此处                      │ │ │
│ │ 100% ▮▮▮▮▮           │ │ │ └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘ │ │
│ └───────────────────────┘ │ └──────────────────────────────────────────────┘ │
│                           │ ┌──────────────────────────────────────────────┐ │
│  ▸ 空闲 / 已静音 (2)      │ │ 📺  HDMI (显示器)     ⚠ 已断开              │ │
│                           │ └──────────────────────────────────────────────┘ │
│                           │                 ▲ 垂直滚动                       │
├───────────────────────────┴──────────────────────────────────────────────────┤
│  ● 路由引擎运行中 · 管理员权限 · 5 条路由                          状态栏 28px │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 尺寸规格

| 项 | 值 |
| --- | --- |
| 默认窗口 | 1180 × 760 |
| 最小窗口 | 960 × 620（窄于此左栏折叠为图标条） |
| 标题栏 | 44px（自绘，可拖拽区域扣除按钮区） |
| 左栏宽度 | 380px（可在 300–520 之间拖拽，宽度持久化） |
| 分栏间距 | 1px 分隔线 + 左右各 16px 内边距 |
| 右栏卡片间距 | 12px，容器内边距 16px |
| 状态栏 | 28px |
| 栏头 | 40px（标题 + 计数徽章 + 工具按钮） |

### 2.3 区域职责

| 区域 | 职责 | 不做什么 |
| --- | --- | --- |
| 标题栏 | 拖拽移动、搜索入口、主题切换、设置、窗口按钮 | 不放业务操作 |
| 左栏（应用程序列表） | 音频会话的**事实来源**：谁在发声、音量多少、被路由到几个设备 | 不展示设备细节 |
| 右栏（音频设备列表） | 路由的**目标与结果**：设备卡片 + 已路由应用 + 拖放区 | 不枚举未发声的应用 |
| 状态栏 | 引擎健康度、权限提示、路由总数 | 不做操作入口 |

### 2.4 无障碍与键盘

| 按键 | 行为 |
| --- | --- |
| `Tab` / `Shift+Tab` | 在 搜索框 → 应用列表 → 设备列表 → 状态栏 之间切换焦点环 |
| `↑` `↓` | 在当前列表内移动选中项 |
| `Home` / `End` | 跳首 / 跳尾 |
| `Enter` | 打开「路由到」菜单（应用列表）/ 展开折叠（设备卡） |
| `M` | 切换选中应用静音 |
| `Delete` | 移除选中路由（焦点在设备 chip 上时） |
| `Ctrl+F` | 聚焦搜索框 |
| `Ctrl+R` | 手动刷新会话与设备 |
| `Esc` | 关闭菜单 / 取消拖拽 |
| 触摸 | 长按 500ms 唤出右键菜单；拖拽支持触摸长按后拖动 |

全部可交互元素必须有 UIA `Name` 与 `HelpText`；焦点环用 2px `accent` 外发光，不使用系统默认虚线框。

---

## 3. 左侧：应用程序列表

### 3.1 条目解剖（行高 52px）

```
┌────────────────────────────────────────────────────────────┐
│ ┌────┐  Spotify                              60%  ▮▮▮▯▯   │
│ │ 🎵 │  spotify.exe · 9124              ⇢2   🔇          │
│ └────┘                                                     │
└────────────────────────────────────────────────────────────┘
  ↑40px          ↑应用名 13.5/Semibold            ↑音量     ↑路由徽章  ↑静音
  圆角8           ↑进程名·PID 12/tertiary
```

| 元素 | 规格 |
| --- | --- |
| 应用图标 | 40×40，圆角 8，1px `border.subtle` 描边；取不到图标时用主色 hash 字母块（同名稳定同色） |
| 应用名 | 13.5 Semibold，单行省略，Tooltip 显示全名（优先 `FileDescription`，回退窗口标题，再回退进程名去 `.exe`） |
| 进程名 · PID | `spotify.exe · 9124`，12 `text.tertiary`，等宽数字 |
| 音量 | 右对齐，`60%` + 40×4 迷你条（`accent` 填充，静音时 `state.muted`） |
| 路由徽章 | `⇢ 2`（已路由设备数），`accent.weak` 底 + `accent` 文字；0 条时不显示 |
| 静音图标 | 16px 喇叭划线，`state.muted`；未静音时不显示（减法：只在异常态出图标） |
| 峰值指示 | 图标外圈 2px 弧形（peak 0–1 映射 0–360°），仅播放中可见；峰值条不用动画，30fps 足够 |

### 3.2 状态矩阵

| 状态 | 视觉 |
| --- | --- |
| 默认 | `bg.surface` + L1 阴影 |
| 悬停 | `bg.hover` 叠加 + L2 阴影 + 轻微上移 1px |
| 选中（单选/多选） | `accent.weak` 底 + 左侧 2px `accent` 竖条 + `border.strong` 描边 |
| 拖拽源 | 透明度 40% + 1px dashed `accent` 描边 + 禁止 hover 位移 |
| 已静音 | 图标灰度化 + 应用名 `text.secondary` + `🔇` |
| 空闲（无音频流） | 收入折叠组「空闲 / 已静音」，默认收起 |
| 应用已退出但有路由 | 条目留在设备 chip 内，标记 `离线` 徽章（`state.warning`） |

### 3.3 排序与分组

- **默认排序**：正在播放优先 → 音量降序 → 应用名升序（稳定排序，避免跳动）。
- **可选排序**（栏头下拉，持久化）：名称 / 进程名 / 最近活跃保持。
- **分组**：`正在播放`（默认展开）/ `空闲 · 已静音 (N)`（可折叠，折叠状态持久化）。
- **重排抑制**：拖拽进行中冻结排序，避免源项从鼠标下跑掉；拖拽结束后延迟 400ms 再应用新顺序。

### 3.4 搜索与过滤

- 栏头搜索框（占位符「搜索应用…」），`Ctrl+F` 聚焦，`Esc` 清空。
- 匹配范围：应用名 / 进程名 / PID（前缀匹配优先）。
- 无结果：列表区显示「没有匹配「xxx」的应用」+「清除筛选」按钮。
- 搜索不改变排序，只做过滤。

### 3.5 空状态

| 场景 | 内容 |
| --- | --- |
| 无应用播放音频 | 居中 64px 线性图标（静音喇叭）+ 「当前没有应用在播放声音」+ 次行「播放音频后会自动出现在这里」+「刷新」文字按钮 |
| 引擎未就绪 | 图标换为警告色 + 「路由引擎未启动」+「以管理员身份重试」按钮 |
| 数据加载中 | 3 条骨架屏（shimmer 1.2s 循环），不使用转圈 |

### 3.6 数据与刷新策略

| 项 | 方案 |
| --- | --- |
| 会话来源 | `IAudioSessionManager2::GetSessionEnumerator` 枚举 → `IAudioSessionControl2` 拿 PID |
| 事件驱动 | `IAudioSessionNotification`（会话创建/销毁）+ `IAudioSessionEvents`（音量/静音变化）→ 立即更新 |
| 低频兜底 | 1s 轮询仅更新峰值与"是否有声音"判定（`IAudioMeterInformation`） |
| 图标获取 | `SHGetFileInfo` / `ExtractIconEx` + LRU 缓存 200 项，按 `exePath + 修改时间` 做键 |
| 退出清理 | 条目 160ms 淡出移除；若存在路由，chip 保留并标记「离线」 |
| 性能 | 列表虚拟化（`VirtualizingStackPanel` / `ItemsRepeater`），diff 更新绑定集合，禁止整表重建 |

### 3.7 数据模型

```csharp
record AppSession(
    int      Pid,
    string   ExePath,
    string   DisplayName,      // FileDescription → 窗口标题 → 进程名
    string   ProcessName,
    ImageSource? Icon,
    float    Volume,           // 0..1
    bool     IsMuted,          // 全局静音
    float    Peak,             // 0..1，实时
    IReadOnlyList<Guid> Routes // 已路由的设备 Id
);
```

---

## 4. 右侧：音频设备列表

### 4.1 设备卡片解剖

```
┌──────────────────────────────────────────────────────────────────┐
│ 🔊  扬声器 (Realtek Audio)      ★默认   · 扬声器 · 24bit/48kHz ⌄ │  ← 头部 56px
│ ──────────────────────────────────────────────────────────────── │  ← 1px 分隔
│ 🎵 Spotify    🔇  ✕      🎬 播放器   🔇  ✕                       │  ← 已路由 chip 区
│ ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐ │
│    拖入应用以路由到此处                                          │  ← 拖放区 ≥48px
│ └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘ │
└──────────────────────────────────────────────────────────────────┘
   圆角 12 · bg.surface · L1 阴影 · 内边距 16
```

| 元素 | 规格 |
| --- | --- |
| 设备类型图标 | 24px：扬声器 / 耳机 / HDMI / USB / 虚拟声卡（VB-Cable）/ 蓝牙 |
| 设备名 | 15 Semibold，单行省略，Tooltip 全名（`PKEY_Device_FriendlyName`） |
| 默认设备徽章 | `★ 默认`，`accent.weak` 底 + `accent` 文字，12px |
| 设备类型 · 格式 | 12 `text.tertiary`，如 `扬声器 · 24bit/48kHz` |
| 折叠箭头 | `⌄`，点击收起卡片正文（收起后只留头部，状态持久化） |
| 已路由 chip | 见 4.2 |
| 拖放区 | 见 4.3 |
| 设备主音量 | 展开区内滑块（可选增强，默认收起），`ISimpleAudioVolume`/`IAudioEndpointVolume` |

### 4.2 已路由应用 Chip（行高 40px）

| 元素 | 规格 |
| --- | --- |
| 图标 | 32×32 圆角 6 |
| 应用名 | 13，单行省略；离线时追加 `离线` 徽章（`state.warning`） |
| 设备级静音按钮 | 20px 图标按钮，**仅悬停/焦点时显示**；已静音时图标常驻且变色 |
| 移除按钮 `✕` | 20px 图标按钮，悬停显示，`state.danger` 悬停色 |
| Chip 底 | `bg.surface2` + 圆角 8；悬停 `bg.hover` |
| 拖拽 | chip 可拖出到其他设备卡（= 复制到该设备） |

> **Chip 是 per-route 实体**：同一应用路由到 3 个设备 = 3 张卡里各有一个独立 chip，可分别静音、分别移除。

### 4.3 拖放区规格

| 状态 | 视觉 |
| --- | --- |
| 空闲 | 高 48px（卡片无 chip 时 64px），`bg.sunken`，1px dashed `border.subtle`，圆角 8，居中文案「拖入应用以路由到此处」12 `text.tertiary` |
| 拖拽候选 | 边框变 1px solid `accent`，底色 `accent.weak`，文案变「**释放以路由**」(13, `accent`) |
| 拖拽命中 | 整卡：`accent` 1px 描边 + `accent.weak` 底 + 卡片缩放 1.012 + L2 阴影 + 拖放区文案变「释放以路由到 **扬声器 (Realtek)**」 |
| 已存在该应用 | 边框 `state.warning` + 文案「已在此设备上」+ 光标 `no-drop`，释放无效 |
| 设备不可用（断开/禁用） | 卡片整体 60% 透明度 + 顶部 `⚠ 已断开` 徽章，拖放区文案「设备不可用」，禁止投放 |

### 4.4 分组与排序

- **分组**：`可用输出设备`（默认展开）/ `已禁用或断开 (N)`（折叠，`text.disabled`）。
- **排序**：默认设备置顶 → 设备类型（扬声器 → 耳机 → HDMI → USB → 虚拟）→ 名称升序。
- **折叠状态、排序偏好**持久化到设置。

### 4.5 设备热插拔

| 事件 | 行为 |
| --- | --- |
| 新增设备 | 卡片 160ms 淡入 + 高度展开；Toast「检测到新设备：XXX」 |
| 移除设备 | 卡片淡出保留占位 3s；受影响的 N 条路由标记 `挂起`（`state.warning`），Toast「设备已断开，N 条路由已挂起，设备回来会自动恢复」 |
| 默认设备变化 | `★ 默认` 徽章 120ms 平移过渡 |
| 设备启用/禁用 | 卡片在分组间移动，保持 chip 不丢 |

### 4.6 数据模型

```csharp
record AudioDevice(
    string Id,                 // IMMDevice::GetId，路由的唯一键
    string FriendlyName,
    DeviceKind Kind,           // Speakers/Headphones/HDMI/USB/Virtual/Bluetooth
    bool   IsDefault,
    bool   IsActive,
    string FormatInfo,         // 24bit/48kHz
    float  MasterVolume,
    IReadOnlyList<Route> Routes
);

record Route(
    Guid      RouteId,         // 单条路由的唯一标识
    int       Pid,
    string    ExePath,
    RouteMode Mode,            // Route(1) | Duplicate(2)
    bool      IsMuted,         // 仅此设备静音
    RouteState State           // Active | Suspended | Offline
);
```

---

## 5. 拖拽交互流程

### 5.1 状态机

```
Idle
 │  鼠标按下（应用条目 / 设备 chip）
 ▼
Armed ──(位移 ≥ 4px)──► Dragging ──(进入设备卡)──► DraggingWithTarget
 │                          │                          │
 │(位移 <4px 抬起)          │(Esc / 拖出窗口)          ├─(合法) ─► DropValid
 ▼                          ▼                          └─(已存在/不可用) ─► DropInvalid
选中/右键                 取消，无副作用                        │
                                                    ┌───────────┴───────────┐
                                                    ▼                       ▼
                                              Commit(乐观更新)          ShowFeedback 后回 Idle
                                                    │
                                          ┌─────────┴─────────┐
                                          ▼                   ▼
                                       Success            Rollback + Toast
```

### 5.2 分步交互明细

| 阶段 | 触发 | 视觉反馈 | 数据动作 |
| --- | --- | --- | --- |
| 1 准备 | 鼠标按下 ≥4px 位移 | 源条目透明度 40% + 1px dashed `accent` 描边；光标 `SizeAll` | 冻结列表排序，快照选中项集合 |
| 2 拖拽中 | 进入拖拽态 | L4 拖拽预览窗口跟随光标（半透明卡片：图标 32 + 应用名 + 多选时右上角 `+2` 角标）；设备卡候选态 | 命中测试实时计算（整卡命中） |
| 3 悬停目标 | 光标进入设备卡 | 卡片缩放 1.012 + `accent` 描边 + `accent.weak` 底 + 拖放区文案替换 | — |
| 4 无效目标 | 已存在 / 设备不可用 / 左栏以外 | 卡片显示 `已在此设备上` 或 `设备不可用`，光标 `no-drop`，禁止缩放 | — |
| 5 释放 | 松开鼠标（合法目标） | chip 以 180ms 弹性动画插入目标卡；device 卡高亮回落；Toast「已路由 **Spotify** → 扬声器 (Realtek)」+「撤销」 | **乐观更新**：先插 chip → 异步下发路由命令 |
| 6 成功 | 核心返回成功 | Toast 3s 后自动淡出；状态栏路由数 +1 | 持久化 `saved_routings.dat` |
| 7 失败 | 核心返回失败 / 超时(3s) | chip 回滚移除（160ms 淡出）；Toast 错误态「路由失败：<原因>」+「重试」 | 回滚内存状态 |
| 8 取消 | `Esc` / 拖出窗口 | 预览消失，源条目恢复 | 无副作用 |

### 5.3 拖拽语义规则（重要）

| 起点 → 终点 | 语义 | 说明 |
| --- | --- | --- |
| 左侧应用 → 设备卡 | **新增一条路由**（`Mode = Route`） | 已存在则不重复添加 |
| 左侧应用 → 第二个设备卡 | **复制路由**（`Mode = Duplicate`） | 原路由保留，声音在多个设备同时播放 |
| 设备 chip → 另一设备卡 | **复制到该设备** | 原 chip 不动 |
| 设备 chip → 左侧应用列表 | **移除该路由** | 可选增强，需配垃圾桶视觉提示 |
| 设备 chip → 原设备卡 | 无操作（幂等） | 光标 `no-drop` |

> **不做** `Alt+拖拽 = 移动` 这类隐藏修饰键：学习成本高于收益（默认关闭，留给高级设置）。

### 5.4 幂等与去重

- 唯一键：`(Pid, DeviceId)`，已存在则 drop 直接拒绝，并在卡片显示「已在此设备上」。
- 同一应用拖到 N 个设备 = N 条独立路由，各自可静音、可移除。
- 应用退出后重启（PID 变化）：按 `ExePath` 视为同一应用，自动套用已保存路由（对应现有「saved routings」能力）。

### 5.5 边界情况清单

| 边界 | 处理 |
| --- | --- |
| 拖到窗口空白处 | 无操作，预览淡出 |
| 拖出窗口边界 | 视为取消 |
| 长列表边缘 | 距边缘 40px 触发自动滚动，速度随距离渐变（最大 600px/s） |
| 拖拽中会话刷新 | 冻结排序；源项若消失（应用退出）则保留幽灵占位并在释放时提示「应用已退出」 |
| 拖拽中设备断开 | 目标卡立即转为不可用态，释放被拒 |
| 多选拖拽 | 预览显示 `+N`，释放后批量路由；部分失败逐条回滚 + 汇总 Toast |
| 拖拽中右键 | 屏蔽右键菜单 |
| 触摸拖拽 | 长按 500ms 进入拖拽态，预览上移 24px 避免被手指遮挡 |
| 高 DPI | 预览窗口按目标显示器 DPI 缩放，跨屏拖拽实时重算 |

### 5.6 技术实现要点（拖拽视觉自由度）

| 方案 | 视觉自由度 | 代价 |
| --- | --- | --- |
| OLE 拖放（`DragDrop.DoDragDrop`） | 低——拖拽图由系统提供，`GiveFeedback` 只能改光标 | 需要自定义拖拽图时必须实现 `IDragSourceHelper` COM |
| **自绘拖拽（推荐）**：`Mouse.Capture` + 分层窗口(`WS_EX_LAYERED`)跟随光标 + `VisualTreeHelper.HitTest` 命中测试 | **高**——完全掌控预览、缩放、角标、缓动 | 不支持拖出到其他程序（本场景不需要） |
| WinUI 3 `DragUIOverride` + 自定义 `DragUI` | 中高 | 只能 Win11 优先 |

> 本项目拖拽**只在窗口内部**发生，因此**推荐自绘拖拽**：既能做出 PCL 级质感，又避免 OLE 拖放与自定义视觉的兼容摩擦。

---

## 6. 右键菜单交互流程

### 6.1 左侧应用列表 · 右键菜单

```
┌────────────────────────────┐
│ 🔇  静音                    │  ← 动态：已静音时显示「取消静音」并带 ✓
│ 📋  复制到多个设备…          │  ← 打开多选设备对话框
│ ⇢   路由到               ▸  │  ← 子菜单：设备列表
│ ────────────────────────── │
│ ⟳   刷新会话                │
└────────────────────────────┘
```

| 菜单项 | 行为 | 边界 |
| --- | --- | --- |
| 静音 / 取消静音 | 切换该应用的**全局静音**（所有路由 + 原始输出） | 菜单项文字与勾选态随当前状态变化 |
| 复制到多个设备… | 打开设备多选对话框：设备 checklist + 「应用到 N 个设备」 | 已路由设备默认勾选且置灰；未勾选任何设备时确认按钮禁用 |
| 路由到 ▸ | 子菜单列出全部可用设备（图标 + 名称 + `★默认`） | 已路由设备前显示 `✓`，点击 = **移除该路由**；含分隔 +「取消所有路由」（danger） |
| 刷新会话 | 立即重新枚举会话与设备 | — |

**「复制」与「路由到」的语义边界（基于现有核心事实）**：

现有核心已在 `session_guid_and_flag` 高 2 位编码了模式：`1 = Route`、`2 = Duplicate`（见 `audio-router-gui/app_inject.cpp:11-28`）。因此设计上明确切分：

| 入口 | 模式 | 适用场景 | UI 差异 |
| --- | --- | --- | --- |
| **路由到 ▸** | `Route (1)` | 快速把应用送到**某一个**设备 | 单选、二级子菜单、交互路径最短 |
| **复制到多个设备…** | `Duplicate (2)` | 一次把声音铺到**多个**设备 | 多选对话框、明确告知「将在 N 个设备同时播放」 |

> 这样两个入口不再是"同一件事的两种说法"，而是**单选快速路径**与**批量广播路径**，语义清晰、学习成本低。

### 6.2 设备卡内 Chip · 右键菜单

```
┌────────────────────────────┐
│ ✕   移除此路由              │  ← danger 色
│ 🔇  仅在此设备静音           │  ← 动态：已静音时显示「取消此设备静音」并带 ✓
│ 📋  复制到其他设备       ▸  │  ← 子菜单：排除当前设备
└────────────────────────────┘
```

| 菜单项 | 行为 | 边界 |
| --- | --- | --- |
| 移除此路由 | 只移除 `(Pid, DeviceId)` 这一条路由，其他设备的同应用路由不受影响 | danger 色，无二次确认（可撤销 Toast 兜底） |
| 仅在此设备静音 | 只改该 route 的 `IsMuted`，等价于把该设备上的那条流静音 | 与全局静音互不干扰，chip 上显示设备级静音图标 |
| 复制到其他设备 ▸ | 子菜单列出**除当前设备外**的可用设备，已存在的显示 `✓` 且置灰不可选 | 无其他可用设备时子菜单项禁用并提示「无其他可用设备」 |

### 6.3 菜单视觉与行为规格

| 项 | 规格 |
| --- | --- |
| 容器 | `bg.surface2` + 圆角 8 + L3 阴影 + 1px `border.subtle` 描边 |
| 内边距 | 上下 6px，左右 4px |
| 菜单项 | 高 32px，左右内边距 10px，图标 16px + 间距 10px + 文本 13 |
| 悬停 | `accent.weak` 底 + 圆角 6 |
| 复选态 | 项左侧 16px 勾选位（用图标位，不额外占宽） |
| 危险项 | 文本与图标 `state.danger`，悬停底色 `rgba(255,107,107,0.14)` |
| 分隔线 | 1px `border.subtle`，上下各 6px 外边距 |
| 子菜单 | 父项右侧箭头 `▸`，120ms 延迟展开（避免穿越误触），子菜单与父项顶部对齐 |
| 位置 | 光标右下 4px；越界自动翻转（右→左、下→上），始终距屏幕边 ≥8px |
| 动画 | 120ms 淡入 + Y 位移 4px；关闭 80ms 淡出（无位移） |
| 键盘 | 弹出后 `↑↓` 移动、`→` 进子菜单、`←` 返回、`Enter` 执行、`Esc` 关闭、输入字母跳转 |
| 触摸 | 长按 500ms 弹出，菜单项最小触摸目标 40px 高 |
| 多选 | 左栏多选后右键：静音/复制应用于全部选中项；菜单标题显示「已选 N 个应用」 |
| 关闭 | 点击外部、失焦、`Esc`、执行任意项后关闭 |

---

## 7. 状态、异常与提示体系

| 场景 | 呈现方式 | 文案示例 |
| --- | --- | --- |
| 路由成功 | 底部 Toast（3s，带撤销） | 已路由 **Spotify** → 扬声器 (Realtek) |
| 路由失败 | 错误 Toast（常驻至关闭，带重试） | 路由失败：目标进程无响应，请重试 |
| 设备断开 | 警告 Toast（5s）+ 卡片挂起态 | 设备已断开，2 条路由已挂起，设备回来会自动恢复 |
| 权限不足 | 顶部内联横幅（不遮内容） | Audio Router 需要管理员权限才能管理音频会话 · 重新启动 |
| 核心未运行 | 列表区全屏空态 + 状态栏红灯 | 路由引擎未启动 · 重新启动引擎 |
| 保存文件损坏 | 对话框（阻塞） | 路由配置文件已损坏，可删除后重新创建 · 重置 / 退出 |

Toast 规格：右下角堆叠，宽 320，圆角 10，`bg.surface2` + L3 阴影，最多同时 3 条，超出排队。

---

## 8. 技术实现建议

### 8.1 先看约束（已核实的现有工程事实）

| 事实 | 出处 | 对 GUI 选型的影响 |
| --- | --- | --- |
| 核心是注入式 C++ DLL，通过 patch `IAudioClient::Activate` 重定向音频流 | `audio-router/audio-router/main.cpp` | GUI 不需要碰音频底层，只需"枚举 + 下发 + 注入" |
| 现有通道 A：命名管道 `\\.\pipe\audio-router-pipe`（消息模式，请求 3×DWORD，回包 1×DWORD） | `audio-router-gui/delegation.h:3-11`、`delegation.cpp:18-27` | 协议极简，可直接复用或版本化扩展 |
| 现有通道 B：共享内存 `Local\audio-router-file-startup`，承载 `global_routing_params` 链表 | `audio-router/audio-router/bootstrapping.cpp:15`、`routing_params.h` | 路由参数**指针重定位**（`rebase`）——结构变更必须让两侧同步 |
| 注入接口 `app_inject::inject_dll(pid, x86, tid, flags)`，`do.exe` 按目标位数分工（内含 x86/x64 手写 shellcode） | `app_inject.h:20`、`do/main.cpp:7-50` | 新 GUI 必须与双架构 `do.exe` / `bootstrapper` 一起打包 |
| 路由模式已编码在 `session_guid_and_flag` 高 2 位（1=Route，2=Duplicate） | `app_inject.cpp:11-28`、`main.cpp:14-16` | **"复制"是一等公民**，UI 可直接映射，无需新协议 |
| 数据结构头文件被两侧互相 include（core `main.h` → gui `routing_params.h`） | `audio-router/main.h:3` | 强耦合点，重构时优先抽成共享契约层 |
| 程序需要管理员权限运行 | `README.md` 0.8.5 changelog | 部署与 UAC 提示要一并设计 |

### 8.2 框架选型对比

| 维度 | WPF (.NET 10) | WinUI 3 | Avalonia 11 | Tauri | Electron |
| --- | --- | --- | --- | --- | --- |
| 深色 + 圆角 + 半透明质感 | ★★★★★ 完全自绘可控 | ★★★★☆ Fluent 原生，自定义需绕 | ★★★★☆ 可自绘 | ★★★★★ CSS 最强 | ★★★★★ CSS 最强 |
| 真毛玻璃（窗口层） | ★★★★☆ DWM backdrop + P/Invoke | ★★★★★ Mica/Acrylic 原生 | ★★☆☆☆ 需大量 P/Invoke | ★★★☆☆ 平台相关 | ★★★☆☆ 需插件 |
| 拖拽视觉自由度 | ★★★★★ 可自绘分层窗口 | ★★★★☆ `DragUIOverride` | ★★★★☆ | ★★★★★ DOM 天然 | ★★★★★ DOM 天然 |
| 与 C++ 核心互操作 | ★★★★★ P/Invoke / C++/CLI / 管道 | ★★★★☆ C++/WinRT 或 P/Invoke | ★★★★☆ P/Invoke | ★★★☆☆ Rust FFI 中间层 | ★★☆☆☆ 需原生插件 |
| 包体 | ~60–90MB（自包含，可裁剪到 ~40MB） | ~40–70MB + 运行时 | ~50MB | ~5–10MB | 150MB+ |
| 内存占用 | 中（~80–120MB） | 中 | 中 | 低 | 高（~200MB+） |
| 开发效率 | ★★★★★ XAML + 成熟 MVVM 生态 | ★★★★☆ 新，坑多 | ★★★★☆ | ★★★☆☆ | ★★★★★ |
| Win10 兼容 | ★★★★★（含 Win7） | ★★☆☆☆（1809+，Win11 体验最佳） | ★★★★★ | ★★★★★ | ★★★★★ |
| 系统级工具契合度 | ★★★★★ | ★★★★★ | ★★★★☆ | ★★★☆☆ | ★★☆☆☆ |

### 8.3 推荐方案

> **首选：WPF (.NET 10) + 自绘控件 + 自有主题 Token 层**

> **目标框架修正（实机验证，非推测）**：原方案写 .NET 8，实机测试被推翻——本机 .NET 8 的
> WindowsDesktop 运行时（8.0.16）加载 `wpfgfx_cor3.dll` 失败（`0x8007007E`），WPF 渲染栈起不来，
> 连 `dotnet new wpf` 空白模板都建不出窗口；同一份代码改用 **net10.0-windows** 立刻正常，
> 窗口、渲染、数据绑定全部通过。因此落地 TFM 定为 `net10.0-windows`（本机 SDK 10.0.103、
> WindowsDesktop 10.0.3）。换机器部署时应先验证该机的 WPF 原生栈，不要盲信 .NET 8。

理由：

1. **与现有 C++ 核心零摩擦**：命名管道 + 共享内存都是 Win32 原生能力，WPF 直接 P/Invoke 即可；不需要引入 Rust/Node 中间层。
2. **视觉自由度足够复刻 PCL**：本项目要的不是 Fluent 原生观感，而是"扁平 + 圆角 + 半透明 + 分层阴影"这套自定义语言——WPF 的模板/样式/特效系统正是为此而生。
3. **拖拽可自绘**：分层窗口跟随光标，做到设计文档要求的 L4 拖拽预览与弹性动画，这是很多框架做不到的。
4. **部署简单**：自包含发布 + Inno Setup/MSIX，用户机器无需预装 Windows App SDK 运行时。
5. **风险最低**：技术栈成熟、社区资料充足、招人与维护成本低。

> **备选：WinUI 3** —— 若确定只支持 Windows 11 且愿意承担 Windows App SDK 部署与相对较新生态的风险，可换来更原生的 Mica/Acrylic 与更省事的窗口级材质。**不建议** Electron（150MB+ 包体与高内存，对系统级小工具不划算）；**不建议**继续用 WTL 手写界面对齐 PCL 观感（成本高、现代效果难做）。

### 8.4 架构建议

```
┌──────────────────────────────┐
│  AudioRouter.Gui (WPF)       │  MVVM + DI + 主题 Token
│  Views / ViewModels / Theme  │
└──────────────┬───────────────┘
               │  IAudioRouterClient（接口，便于 Mock 测试）
┌──────────────▼───────────────┐
│  AudioRouter.Core.Contracts  │  路由命令/事件的契约层（版本化）
│  Request{Version,Opcode,Len} │  Opcode: EnumSessions / Route / Unroute /
└──────────────┬───────────────┘          SetMute / SetVolume / Subscribe
               │  命名管道（消息模式，超时+重连+心跳）
┌──────────────▼───────────────┐
│  现有 C++ 核心（保持不动）     │  audio-router.dll / do.exe / bootstrapper
│  + 共享内存 saved_routings    │  路由参数链表（沿用 rebase 机制）
└──────────────────────────────┘
```

**协议演进策略（低风险路径）**：

1. 保留 `\\.\pipe\audio-router-pipe` 与现有 3×DWORD 注入请求 → **向后兼容**。
2. 新增一条并行管道（如 `audio-router-ctl`）承载控制命令：`{version, opcode, payloadLen, payload}`，`version` 字段先留好 → 未来可扩。
3. 复用 `global_routing_params.version`（结构里已有 version 字段，天然适合做迁移判断）→ 升级时先读版本、再决定解析方式。
4. **迁移点（必须处理）**：`saved_routings.dat` 目前写在**工作目录**；新 GUI 若安装在 `Program Files` 将无写权限 → 迁到 `%ProgramData%\AudioRouter\saved_routings.dat`，并在首次启动时从旧路径导入。

### 8.5 关键 Windows API 清单

| 用途 | API |
| --- | --- |
| 枚举音频会话 | `IAudioSessionManager2::GetSessionEnumerator` → `IAudioSessionControl2::GetProcessId` |
| 会话音量/静音 | `ISimpleAudioVolume`、`IAudioMeterInformation`（峰值） |
| 会话事件 | `IAudioSessionNotification`、`IAudioSessionEvents` |
| 枚举音频设备 | `IMMDeviceEnumerator::EnumAudioEndpoints`、`IMMDevice::GetId`、`IPropertyStore` 读 `PKEY_Device_FriendlyName` |
| 设备热插拔 | `IMMNotificationClient`（`OnDeviceAdded/Removed/StateChanged/DefaultDeviceChanged`） |
| 窗口材质 | `DwmSetWindowAttribute`（`DWMWA_SYSTEMBACKDROP_TYPE` / `DWMWA_USE_IMMERSIVE_DARK_MODE` / `DWMWA_WINDOW_CORNER_PREFERENCE`）、`SetWindowCompositionAttribute`（Win10 兜底） |
| 图标提取 | `SHGetFileInfo`、`ExtractIconEx` |
| 进程信息 | `OpenProcess`、`QueryFullProcessImageName`、`GetModuleFileNameEx` |

### 8.6 工程与交付

| 项 | 建议 |
| --- | --- |
| 工程结构 | 新增 `AudioRouter.Gui`（WPF）替换 `audio-router-gui`；原生工程（`audio-router`、`do`、`bootstrapper`）保留 |
| MVVM | `CommunityToolkit.Mvvm`（source generator，零样板） |
| DI | `Microsoft.Extensions.DependencyInjection` + `Hosting` |
| 单实例 | `Mutex` 全局单例，重复启动时激活已有窗口 |
| 权限 | `app.manifest` 声明 `requireAdministrator`，与核心一致 |
| 打包 | 自包含 `.NET 10` + Inno Setup（安装包）/ MSIX（商店）；一并打包 `do.exe`（x86+x64）与 `audio-router.dll` |
| 日志 | `%LOCALAPPDATA%\AudioRouter\logs\gui-*.log`，滚动保留 7 天 |
| 设置持久化 | `%APPDATA%\AudioRouter\settings.json`（主题色、排序、分组折叠、栏宽） |
| 性能预算 | 空闲 CPU < 1%，路由操作到 UI 反馈 < 100ms，100 个会话时滚动 60fps |

### 8.7 实施路线（建议分 3 个迭代）

| 迭代 | 内容 | 验收点 |
| --- | --- | --- |
| **M1 骨架** | 窗口 + 主题 Token + 双栏布局 + 会话/设备真实数据渲染 + 空状态 | 能如实显示系统里的应用与设备；深色主题观感达标 |
| **M2 核心交互** | 拖拽（含预览、高亮、幂等）+ 路由到核心并生效 + 右键菜单 + 状态栏 | 拖一下能真正改变声音去向；重复拖不进第二条 |
| **M3 打磨** | 复制/多选/批量、热插拔与挂起恢复、Toast/撤销、无障碍与键盘、浅色主题、设置持久化 | 边界用例（拔设备、杀进程、权限失败）不崩、不丢状态 |

---

## 9. 设计取舍与待确认项（主动暴露）

| # | 议题 | 现状 / 风险 | 建议 |
| --- | --- | --- | --- |
| 1 | 「复制」与「路由到」语义重叠 | 需求两项功能描述相近，容易做成"同一件事的两个入口" | 复制 = 多选批量广播（`Duplicate`），路由到 = 单选快速路径（`Route`）——已对齐核心 flag 编码 |
| 2 | 全局静音 vs 设备级静音 | 需求明确区分，但现有 `local_routing_params` 无独立静音字段 | 复用 flag 位或升级结构版本；`version` 字段已存在，可做兼容判断 |
| 3 | `saved_routings.dat` 写入位置 | 当前在工作目录；装到 `Program Files` 后无写权限 | 迁 `%ProgramData%`，首次启动导入旧文件 |
| 4 | 32 位进程注入 | 依赖双架构 `do.exe` / `bootstrapper` | 打包必须同时携带两套二进制，安装脚本需校验 |
| 5 | 毛玻璃观感与性能 | Win10 上 Acrylic 掉帧、Win11 Mica 观感不同 | 窗口层用系统材质，卡片刻意用"半透明 + 阴影"模拟；提供「高性能模式」关闭模糊 |
| 6 | 拖拽实现方式 | OLE 拖放视觉受限 | 采用自绘分层窗口拖拽（仅窗口内拖放，无跨程序需求） |
| 7 | 应用退出后的路由 | 需求未定义 | 保留路由并标记「离线」，应用重启后按 `ExePath` 自动恢复 |
| 8 | 一个应用多个同名进程 | 如多开的游戏/浏览器 | 会话按 PID 区分，UI 上以「应用名 (2)」聚合展开，路由按 PID 精确下发 |
| 9 | 浅色主题 | 需求只强调深色优先 | Token 层预留，M3 迭代补齐，不作为 M1/M2 验收项 |

---

## 10. 验收清单（供统一测试）

**布局**
- [ ] 窗口可拖拽、可缩放，最小 960×620 下左栏可折叠不破版
- [ ] 左栏宽度可拖拽并记住；高 DPI（125%/150%/200%）无模糊、无错位
- [ ] 深色主题下拉到底无白闪、无未主题化控件

**左栏**
- [ ] 正在发声的应用自动出现，停止发声后归入「空闲 / 已静音」
- [ ] 图标、应用名、进程名、PID、音量、静音态、路由徽章均正确
- [ ] 搜索、排序、分组折叠、键盘导航可用；应用退出条目优雅移除
- [ ] 无应用发声时显示空状态

**右栏**
- [ ] 设备卡片信息完整（名称/类型/默认/格式），默认设备带 `★`
- [ ] 已路由应用以 chip 形式出现在对应设备卡内；同应用可在多卡同时出现
- [ ] 设备热插拔：新增淡入、断开挂起并提示、恢复自动还原
- [ ] 禁用设备折叠分组

**拖拽**
- [ ] 拖拽有跟随光标的预览；经过设备卡高亮；释放后 chip 插入并真正改变音频去向
- [ ] 重复拖到同一设备被拒绝并提示「已在此设备上」
- [ ] 拖到第二个设备 = 复制（原路由保留）
- [ ] 核心返回失败时 chip 回滚 + 错误提示 + 重试
- [ ] `Esc` 取消拖拽无副作用；长列表边缘自动滚动

**右键菜单**
- [ ] 应用菜单：静音/取消静音（态正确）、复制到多个设备…、路由到 ▸（已路由打勾、可取消）
- [ ] chip 菜单：移除此路由（不影响其他设备）、仅在此设备静音、复制到其他设备 ▸（排除当前设备）
- [ ] 菜单越界自动翻转；键盘可完整操作；子菜单无穿越误触

**其他**
- [ ] Toast 成功/失败/挂起三类文案与行为正确，撤销可用
- [ ] 权限不足与核心未启动有明确引导
- [ ] 空闲 CPU < 1%，操作反馈 < 100ms，无内存持续增长

---

## 附录 A · M1 实现与验证记录

> 工程位置：`audio-router/AudioRouter.Gui`（WPF / `net10.0-windows` / 零 NuGet 依赖）
> 解决方案：`audio-router/AudioRouter.Managed.slnx`（与原有 C++ `audio-router.sln` 并存，未改动后者）

### A.1 M1 已交付（且已通过实机验证）

| 项 | 状态 | 验证方式 |
| --- | --- | --- |
| 工程骨架可编译 | ✅ | `dotnet build` → 0 错误 0 警告 |
| 主题 Token 层（颜色/圆角/字号/阴影/动效） | ✅ | 截图核对 |
| 深色控件样式（滚动条/按钮/搜索框/卡片/徽章/ToolTip） | ✅ | 截图核对 |
| 自定义标题栏 + 无边框窗口 + 最大化不遮任务栏 | ✅ | 截图 + WM_GETMINMAXINFO 处理 |
| 左栏：真实音频会话（图标/应用名/进程名·PID/音量/静音/峰值环） | ✅ | 实机枚举 6 个会话，图标提取成功 |
| 左栏：搜索 / 排序 / 分组（正在播放 · 空闲已静音）/ 空状态 | ✅ | 截图核对 |
| 右栏：真实输出设备卡片（名称/类型/默认徽章/禁用徽章） | ✅ | 实机枚举 30 个端点 |
| 右栏：可用设备置顶 + 已禁用或断开折叠（默认收起） | ✅ | 截图核对（4 可用 / 26 折叠） |
| 拖放区视觉（虚线凹陷 + 不可用态文案） | ✅ | 截图核对 |
| 状态栏（引擎状态 / 权限位 / 计数 / 演示数据标记） | ✅ | 截图核对 |
| 启动日志 + 异常兜底（不因枚举失败闪退） | ✅ | `%LOCALAPPDATA%\AudioRouter\logs\startup.log` |

**明确未交付（下一轮）**：拖拽落点与路由下发、右键菜单、真实 chip 数据、设备格式信息（24bit/48kHz）、
浅色主题、`IMMNotificationClient` 设备事件驱动（当前 3s 轮询）。

### A.2 M1 期间定位并修复的两个真实缺陷

| # | 缺陷 | 根因 | 证据 |
| --- | --- | --- | --- |
| 1 | 进程启动即 `AccessViolationException`，窗口建不出来 | `PROPVARIANT` 托管声明只有 16 字节，原生为 24 字节（union 含 `DECIMAL`），`IPropertyStore::GetValue` 写回时溢出 8 字节 | 修复前 stderr 完整栈指向 `WasapiDeviceService.Enumerate()`；用 `StructLayout(Size = 24)` 后枚举正常返回 30 个设备 |
| 2 | 窗口无法创建，`DllNotFoundException: wpfgfx_cor3.dll` | 本机 .NET 8 WindowsDesktop 运行时（8.0.16）原生渲染栈加载失败；**非本项目代码问题** | 空白 `dotnet new wpf` 模板报同一错误；改 `net10.0-windows` 后同一份代码窗口正常 |

> 经验沉淀：COM 互操作的结构体大小错误触发的是**不可捕获**的 `AccessViolationException`，
> `try/catch` 与 `DispatcherUnhandledException` 都拦不住，进程直接终止。
> 因此 WPF 程序在窗口建出来之前出错时，必须依赖文件日志（本项目的 `StartupLog`）才能定位。

### A.3 复现命令

```powershell
cd audio-router\AudioRouter.Gui
dotnet build -c Debug
.\bin\Debug\net10.0-windows\AudioRouter.Gui.exe
```

### A.4 M2 实现与验证记录（拖拽路由已打通）

| 项 | 状态 | 验证方式 |
| --- | --- | --- |
| 自绘拖拽（分层窗口预览 + 多选 `+N` 角标 + 4px 阈值） | ✅ | 真实鼠标拖拽（SendInput） |
| 整卡命中 + 悬停高亮（accent 描边/底色 + 提示文案替换） | ✅ | 截图 + 拖拽路径命中第一张卡 |
| 幂等：同 `(Pid, DeviceId)` 不重复添加 | ✅ | 二次拖拽日志 `drop: rejected (already routed)`，无新路由产生 |
| per-route chip（真实图标 + 复制标记 + 悬停移除 ✕） | ✅ | UIA 定位按钮并 Invoke，日志出现 `unroute#1` |
| `Route` / `Duplicate` 语义落地（对齐核心 flag 1/2） | ✅ | 同应用落到第二设备 → chip 显示「复制」，左栏徽章 `⇢ 2` |
| 边缘自动滚动 / `Esc` 取消 / 状态栏操作反馈 | ✅ | 代码就位并随拖拽路径触发 |
| 路由下发接缝 `IRoutingService`（可替换为命名管道实现） | ✅ | 状态栏如实显示「核心下发待接入」，不谎报已生效 |
| 栏头做减法：去掉「N 默认 / X/Y 在线」重复信息 | ✅ | 栏头仅显示可用设备数（4） |
| 可交互元素补 UIA Name / AutomationId / HelpText | ✅ | UIA 树中按钮名称可读（如「刷新会话与设备」「移除此路由」） |

**M2 期间定位并修复的 4 个真实缺陷**

| # | 缺陷 | 根因 |
| --- | --- | --- |
| 3 | chip 的 ✕ 按钮永远不显示，用户无法移除路由 | ①悬停触发器里漏了把按钮设为 Visible 的 Setter；②按钮上的 `Visibility="Collapsed"` 是**本地值**，WPF 里本地值优先级高于触发器，即使补了 Setter 也会被压住 → 默认值必须放进 `Style`（新增 `Btn.ChipRemove`） |
| 4 | 编译报 `Point` 类型不匹配 | 为 `MinMaxInfo` 互操作声明的嵌套 `struct Point` **遮蔽了 `System.Windows.Point`**，已改名 `NativePoint` |
| 5 | 纯图标按钮无 UIA 名称 | 设计文档 2.4 明确要求「全部可交互元素必须有 UIA Name」，实现时漏做 |
| 6 | 栏头「1 默认 · 4/30 在线」信息重复 | 与卡片 ★ 徽章、折叠分组计数三处重复展示同一信息 |

### A.5 M2 右键菜单（已验证）

| 项 | 状态 | 验证方式 |
| --- | --- | --- |
| **真实静音**（`ISimpleAudioVolume::SetMute`，公开 WASAPI，不需要注入核心） | ✅ | 日志 `mute(真实生效): pid=28416` + 界面出现 🔇 |
| 应用菜单：静音 / 取消静音（动态文案 + 勾选态） | ✅ | 真实右键 + 截图 |
| 应用菜单：复制到多个设备…（批量对话框） | ✅ | 截图；未勾选时「应用」按钮为禁用态 |
| 应用菜单：路由到 ▸（勾选式设备子菜单，支持多设备） | ✅ | 代码就位，与 chip 菜单同机制 |
| 应用菜单：刷新会话 | ✅ | — |
| chip 菜单：移除此路由（危险色）/ 仅在此设备静音 / 复制到其他设备 ▸ | ✅ | UIA + 截图 |
| 动态子菜单（排除当前设备；已路由项勾选且置灰） | ✅ | 日志 `menu: 展开「复制到其他设备」子菜单，候选设备 3 个` |
| 子菜单 → 复制到第二个设备 | ✅ | `route#2 … mode=Duplicate` +「复制」chip + 左栏 `⇢ 2` |
| 深色菜单视觉（圆角 / 悬停 / 勾选 / 子菜单箭头 / 分隔线） | ✅ | 截图 |

**M2 期间定位并修复的缺陷（续）**

| # | 缺陷 | 根因 |
| --- | --- | --- |
| 7 | **子菜单永不展开**（无箭头、悬停无效、点击反而关菜单） | 子项是在 `SubmenuOpened` 事件里才填充的 —— WPF 依据「当前有没有子项」判定 `MenuItem.Role`，零子项被当成**叶子项**（`SubmenuItem`），于是既不显示箭头也不触发展开，形成死循环。修法：**构造时先塞一个占位子项**，让 WPF 识别为 `SubmenuHeader`，实际内容仍在 `SubmenuOpened` 里替换 |

**M2 仍未做（见 A.7）**：`IMMNotificationClient` 设备事件驱动、设备格式信息（24bit/48kHz）、拖拽期间冻结会话排序。
**依赖原生核心才能完成**：把 `IRoutingService` 的本地实现替换为命名管道实现，让路由与 per-route 静音真正作用于音频流。

### A.6 Toast 浮层 + 撤销（已验证）

| 项 | 状态 | 验证方式 |
| --- | --- | --- |
| 右下角 Toast 浮层（成功/信息/警告/错误四色左侧色条） | ✅ | 截图：绿色成功条 + 消息换行 + 关闭按钮 |
| 进/出场动效（160ms 淡入 + 上滑） | ✅ | 模板 `Loaded` 触发器，与设计文档一致 |
| 最多同时 3 条，超出丢弃最旧 | ✅ | `MaxToasts` 约束 + 到点回收 |
| **撤销真实生效** | ✅ | 拖拽新增路由 → 点撤销 → ToDesk chip 消失、Realtek chip 保留、`⇢` 徽章回到 1、状态栏回到 1 条路由 |
| 撤销语义精确（只回滚本次新增，不误伤既有） | ✅ | 同上：既有路由完整保留 |
| 撤销覆盖的操作 | ✅ | 拖拽新增、移除路由（还原到原位置）、静音切换（再次调用真实 `SetMute` 反向操作）、批量复制、chip 复制 |

**对设计文档的一处主动修正**：原规格写「路由成功 Toast 3s」。实现时发现 —— **撤销提示 3 秒就消失等于没给用户反悔的机会**。
因此把带撤销的提示延长到 **12 秒**，纯告知型提示保持 3–4 秒。（即便过期也仍有补救路径：右键 chip → 移除此路由。）

### A.7 chip 拖拽路径 + 拖拽引擎统一（已验证）

| 项 | 状态 | 验证方式 |
| --- | --- | --- |
| ① 左侧应用 → 设备卡 = 新增路由（重构后回归） | ✅ | `drop: Applied` + 带撤销 Toast |
| ② chip → 另一设备卡 = 复制（Duplicate） | ✅ | `route#2 … mode=Duplicate` + 目标卡出现「复制」chip |
| ③ chip → 左侧应用区 = 移除该路由 | ✅ | `unroute#1` + chip 消失 + 左栏 `⇢` 回到 1 + 带撤销 Toast |
| 拖拽源项 40% 透明（补上设计文档 5.2 原未做项） | ✅ | 模板 `IsDragging` 触发器（应用条目与 chip 各自生效） |
| 移除落点提示（左栏危险色高亮） | ✅ | `IsRemoveTarget` + `LeftDropZone` 危险色描边/底色 |
| 引擎统一：窗口级事件 + 视觉树命中测试 | ✅ | 三个方向共用一套状态机 |

**实现要点（三个必须记住的坑）**

1. 鼠标捕获之后，输入事件只送往「捕获元素及其祖先」。因此把**捕获点设在窗口上**，两种拖拽源才能共用一套 Move/Up 处理；否则每种来源都要各写一份。
2. 判断「是否落在左栏」**必须用几何判定**，不能用 `IsMouseOver`：拖拽期间存在鼠标捕获，`IsMouseOver` 反映的是被捕获元素，而不是光标下方真实元素。
3. 左栏落点提示的默认 `BorderBrush` / `Background` **必须写在 Style 里**。写成元素上的本地值会压过 `DataTrigger`（本地值优先级更高）——与缺陷 #3 是同一个坑。

**M2 仍未做**：`IMMNotificationClient` 设备事件驱动（当前 3s 轮询）、设备格式信息（24bit/48kHz）、拖拽期间冻结会话排序。
**依赖原生核心才能完成**：把 `IRoutingService` 的本地实现替换为命名管道实现，让路由与 per-route 静音真正作用于音频流。

### A.8 多语言（i18n）+ 跨平台可行性评估

#### A.8.1 多语言实现（已验证）

| 项 | 状态 | 验证方式 |
| --- | --- | --- |
| 文案外置为语言文件（`Languages/*.json`，随程序发布） | ✅ | 输出目录含 `zh-CN.json` / `en-US.json` |
| 双层目录：内置只读 + `%APPDATA%\AudioRouter\Languages` 可写 | ✅ | 程序装在 Program Files 也能导入语言 |
| 内置简体中文 / English 两套完整语言包 | ✅ | 界面在两种语言下逐项核对 |
| 标题栏地球按钮：列出语言 + 勾选当前 | ✅ | 截图 |
| 切换语言**实时生效**（无需重启、无需重建界面） | ✅ | `切换为 en-US` + 全界面立即变英文 |
| 语言选择持久化 | ✅ | `%APPDATA%\AudioRouter\settings.json` → `{"Language":"en-US"}`；重启后恢复 |
| 首次启动跟随系统 UI 语言（`zh-CN` → 简中） | ✅ | 日志 `切换为 zh-CN（简体中文）` |
| **导入第三方语言包** | ✅ | 导入 8 条键的 `ja-JP` → 落盘到用户目录 + 立即切换 |
| **缺键回退**：当前语言 → en-US → 键名本身 | ✅ | 部分翻译的 ja-JP：已翻译键显示日语，其余显示英文 |
| 非 UTF-8 文件校验（记事本存成 ANSI 会乱码） | ✅ | 日志 `跳过 bad-gbk.json（file is not valid UTF-8）` |
| 不翻译系统数据（设备名 / 应用名来自 Windows） | ✅ | 英文界面下设备名仍为「扬声器 (Realtek(R) Audio)」——正确边界 |

**语言文件格式**（把这份丢给别人就能做翻译）：

```json
{
  "code": "en-US",
  "name": "English",
  "version": 1,
  "author": "whoever",
  "strings": {
    "section.apps": "Applications",
    "toast.routed": "Routed {0} → {1}"
  }
}
```

`{0}` `{1}` 是占位符；缺失的键自动回退，因此**允许只翻译一部分**。

#### A.8.2 跨平台可行性：修正后的结论

| 层 | 当前实现 | 能否跨平台 |
| --- | --- | --- |
| 视图层（XAML + 代码后置） | WPF（net10.0-windows） | ❌ **WPF 仅支持 Windows**。跨平台须换 Avalonia / Uno 等，视图层需重写 |
| 视图模型 / 模型 / 服务 | 纯 .NET，仅 3 处 WPF 类型依赖（Brush、ImageSource） | ✅ 剥离后可复用 |
| 语言包 / 设置存储 | 外置 JSON，与框架无关 | ✅ 已经是跨平台的 |
| 音频枚举 / 静音 | WASAPI（`ISimpleAudioVolume`） | ⚠️ 需按平台实现：macOS CoreAudio / Linux PipeWire |
| **路由能力** | 注入 DLL + patch WASAPI COM | ⚠️ **能力本身不是 Windows 专有，但「这套实现」无法复用** —— 见下表 |

**三平台的路由机制对比（本表修正了本文档早先版本的错误结论）**

| 平台 | 逐应用路由 | 多设备复制 | 机制 | 成本 |
| --- | --- | --- | --- | --- |
| Windows | ✅ | ✅ | 注入 DLL + patch `IAudioClient`（本项目现状）。Win11 已原生支持逐应用切换输出设备，但**不支持复制到多设备** | 已完成 |
| **Linux（PipeWire / PulseAudio）** | ✅ | ✅ | **纯 IPC 操作音频服务器图**：PipeWire 用 `pw-dump` 读图 + `pw-link` 连线（现成工具如 [route-audio](https://pkg.go.dev/git.sr.ht/~jcmuller/tools@v0.24.0/route-audio)）；PulseAudio 用 `pactl move-sink-input`（`pavucontrol` 的 Playback 页就是这个功能）；多设备复制用 `module-combine-sink` / PipeWire loopback。**零注入** | **低 —— 比 Windows 更省** |
| macOS | ⚠️ | ⚠️ | 需虚拟音频设备 / HAL 插件（AudioServerPlugin），SoundSource / Loopback 一类工具即此路线；macOS 14.2+ 提供 `AudioHardwareCreateProcessTap` 可辅助抓取进程音频。涉及驱动签名与用户授权 | 高 |

**结论（修正）**：*这套路由实现*是 Windows 专有的，但**逐应用路由这个能力不是**。
「支持跨平台」的准确含义是：**界面三平台可跑 + 各平台音频后端分别实现**；其中
**Linux 后端成本最低（音频服务器原生支持，只需 IPC），应作为跨平台第一站**；macOS 需要驱动级工作，成本最高、风险也最大。

> 诚实边界：以上为平台机制与现成工具的调研结论；本项目**只在 Windows 上做过实测**，Linux/macOS 两条路径落地前必须在真机验证。

**若要真做，推荐顺序**（先把代价压到最低，再动视图）：

1. **抽 `IAudioBackend` 接口**，把 WASAPI 实现收进 `WasapiAudioBackend` —— 这一步与框架无关，且当前就该做；
2. 让视图模型完全不引用 WPF 类型（3 处：`Brush` / `ImageSource` / `DispatcherTimer` → 换成平台无关抽象）；
3. **一次性迁移视图层到 Avalonia**（避免半迁移的最差状态），再逐平台补音频后端。

> 决策点：**是否现在做第 3 步**。视图层重写约 2.4k 行 XAML/CS，且本轮已通过真机验证的界面需要重新验证一遍；
> 收益是 Windows/macOS/Linux 都能跑起来。**第 1、2 步无论选哪条路都该先做**（不丢已有成果）。

### A.9 跨平台第一步：Core + CLI（已验证）

**结论先行**：*这套路由实现*是 Windows 专有的，但**逐应用路由这个能力不是**。
因此路线改为 **先做平台无关的核心 + 命令行，再谈 GUI**：

```
AudioRouter.Core   跨平台类库（net10.0，零 WPF 依赖、零 NuGet）★ 本轮交付
├── Models         平台无关模型（不含图标/颜色/峰值等表现层数据）
├── Backends       IAudioBackend + WasapiAudioBackend + NativeCoreProbe
├── Localization   语言包 + 设置（与 GUI 共用同一份语言文件）
├── Routing        RouteStore（落盘）+ RoutingService
└── Diagnostics    跨平台日志

AudioRouter.Cli    控制台入口（headless，无桌面环境可用）★ 本轮交付
AudioRouter.Gui    WPF（现状）—— 后续收敛到 Core，再考虑 Avalonia
```

#### A.9.1 已实现的 CLI（全部真机跑通，无 GUI 参与）

| 命令 | 作用 | 验证 |
| --- | --- | --- |
| `doctor` | 平台/后端/能力/语言/路径自检 | `Can redirect audio: NO (records only)` + 原因说明 |
| `devices [filter]` | 设备列表（默认置顶、`on`/`-` 标记、CJK 宽度对齐） | 30 个端点，默认设备置顶 |
| `apps` | 正在发声的应用（PID/播放中/静音/音量/进程名） | 6 个会话，Edge 显示 `mute` |
| `route <pid> <deviceId>` | 记录路由并按平台能力如实报告 | `recorded … — audio NOT redirected on this platform` |
| `unroute <pid> <deviceId>` | 删除路由 | 落盘后再查为空 |
| `mute <pid> [--off]` | **真实**静音（公开 WASAPI） | 生效，与 GUI 同一实现 |
| `lang [list\|set\|import]` | 语言管理（与 GUI 共享语言目录） | CLI 也能看到导入的 `ja-JP` |
| `--json` / `--lang` / `--verbose` | 脚本友好 | `devices --json` 取到的设备 id 直接用于 `route` |

退出码：0 成功 / 1 运行错误 / 2 用法错误（脚本可判）。

#### A.9.2 能力声明是显式的，不是假装的

`IAudioBackend` 把能力做成**运行时探测**而非写死：

```
SupportsRouting / SupportsDuplication / Limitation
```

- Windows：依据 `NativeCoreProbe`（`audio-router.dll` + `do.exe` 是否存在）判定 —— 找不到就如实说"仅记录"；
- Linux：规划中（PipeWire/PulseAudio 纯 IPC，无需注入）；
- macOS：`UnsupportedAudioBackend` 占位，程序照常启动并给出原因，而不是崩在启动路径。

> 这是全项目最容易被粉饰的地方：**记录一条路由看起来和"路由成功"一模一样**。
> 所以用不同枚举值把「已生效」与「仅记录」严格分开，CLI 与 GUI 都不许混为一谈。

#### A.9.3 两个技术要点

1. **跨平台库必须正面处理平台 API 边界**：`Type.GetTypeFromCLSID` / `Marshal.ReleaseComObject`
   在 `net10.0` 库里会触发 CA1416 警告。修法是给 Windows 专有类型加 `[SupportedOSPlatform("windows")]`
   让分析器守住边界（调用方先做 `OperatingSystem.IsWindows()` 判断），**不是屏蔽警告**。
2. **CLI 必须落盘**：每次调用都是独立进程，「路由」这类跨调用状态若只在内存里等于每次失忆，
   因此 `RouteStore` 持久化到 `routes.json`。

#### A.9.4 难度结论（修正后）

| 平台 | 枚举/静音 | 路由 | 成本 |
| --- | --- | --- | --- |
| Windows | ✅ 已完成 | ⚠️ 需注入式核心（C++ 编不出是**当前唯一真卡点**） | 低 |
| Linux | 中低 | ✅ 音频服务器原生支持，纯 IPC | **低 —— 建议作为跨平台第一站** |
| macOS | 中 | ❌ 需虚拟音频设备 / HAL 插件 | 高 |

**总体难度：中等。难点在 macOS 的路由，而不在"三个平台"本身。**
CLI 已经证明核心层确实可以跨平台运行（它就在无 GUI 的情况下跑通了枚举、真实静音与路由记录）。

#### A.9.5 待办（按优先级）

1. **Linux 后端**（`PipeWireAudioBackend`：`pw-dump`/`pw-link` 或 `pactl`）—— 成本最低、价值最直接；
2. **GUI 收敛到 Core**：目前 GUI 仍持有自己那份 WASAPI 互操作（过渡期重复，已知并跟踪），
   下一步把 GUI 切到 Core 并删除重复代码，再重新验证一遍界面；
3. 视图层迁移 Avalonia（在前面两步之后，避免半迁移状态）。

### A.10 Linux 后端 + 核心测试（本轮交付）

| 项 | 状态 | 说明 |
| --- | --- | --- |
| `LinuxAudioBackend`（PipeWire / PulseAudio，经 `pactl`） | ✅ 已实现 | **本项目第一个声明"真的能改道"的后端** |
| 逐应用路由 | ✅ | `pactl move-sink-input <index> <sink>` |
| 逐应用静音 | ✅ | `pactl set-sink-input-mute <index> 1` |
| 解除路由 | ✅ | `pactl move-sink-input <index> @DEFAULT_SINK@` |
| 设备/会话枚举 | ✅ | `pactl -f json list sinks` / `list sink-inputs` |
| 多设备复制 | ❌ 如实声明不支持 | pulse 层一个流只能属于一个 sink，需 `module-combine-sink` / 虚拟 sink（系统级改动） |
| `AudioRouter.Tests`（零依赖测试工程） | ✅ | **55 项断言全部通过** |

#### A.10.1 为什么用 JSON 而不是解析 `pactl list` 的文本

`pactl list sinks` 的**人类可读输出会随语言环境变化**（中文本地化下字段名都不同），
文本解析等于把程序绑死在某个 locale 上。`pactl -f json`（PulseAudio 15+ 与 PipeWire 的
pulse 兼容层都支持）是稳定的机器接口 —— 这是跨平台实现里少踩一个坑的关键决定。

#### A.10.2 测试工程存在的理由

Linux 后端在开发机上跑不起来，所以采用**"纯函数 + 录制样本 + 测试替身"**策略：

| 测试组 | 覆盖内容 | 为什么必须测 |
| --- | --- | --- |
| `pactl` 解析（录制样本） | sink/sink-input 字段、`application.process.id` **字符串与数字两种形态**、无 pid 的系统流必须跳过、多声道音量取平均（50%/100% → 0.75） | 字段类型在不同版本/后端下不稳定，靠"看起来对"必然翻车 |
| 进程调用 | PATH 查找、**捕获子进程 stdout** | Linux 后端的全部数据都来自这里 |
| 语言包 | 合法/缺字段/空 strings 拒绝、**非法 UTF-8 拒绝**、UTF-8 BOM 接受 | 用户直接编辑语言文件，容错必须明确 |
| 路由状态 | 幂等、跨进程持久化、删除 | CLI 每次是独立进程 |
| **路由语义（最关键）** | 能改道的后端 → `Applied`；不能改道的后端 → **`RecordedOnly`（绝不许报 `Applied`）**，且用户意图仍被记录 | 记录一条路由和真正改道，在界面上可以长得一模一样 |

> 这一条是全项目最容易被粉饰的地方，因此用两个**测试替身**（`AlwaysRoutingBackend` /
> `RecordingOnlyBackend`）把两条路径都钉死在断言里。

#### A.10.3 验证边界（诚实交代）

| 已验证 | 未验证 |
| --- | --- |
| 三个工程 0 错误 0 警告；55 项断言通过 | **没有 Linux 机器 —— 真实 PipeWire/PulseAudio 环境下的端到端未验证** |
| `pactl` JSON 解析（用真实形态样本） | `pactl` 各发行版/版本的**真实输出差异**（样本是录制的典型形态） |
| 子进程 stdout 捕获（本机实测通过） | 蓝牙/USB 设备在 Linux 上的实际分类准确度 |

#### A.10.4 跨平台进度

| 平台 | 枚举 | 静音 | 路由 | 状态 |
| --- | --- | --- | --- | --- |
| Windows | ✅ | ✅ 真生效 | ⚠️ 依赖注入式核心（本机编不出 C++） | 可用 |
| **Linux** | ✅ | ✅ | ✅ **`pactl` 直接改道** | 代码完成，待真机验证 |
| macOS | — | — | ❌ | 占位，待虚拟音频设备/HAL 插件 |

**下一站**：GUI 收敛到 Core（删掉过渡期重复的 WASAPI 代码并重验界面），
之后才是视图层迁移 Avalonia。

### A.11 GUI 收敛到 Core（本轮交付 · 过渡期重复代码已清零）

**目标**：GUI 不再持有任何音频/路由/语言/设置的实现，只留表现层。

| 从 GUI 删除（已由 Core 承担） | 保留在 GUI（确实是表现层） |
| --- | --- |
| `Services/Native/WasapiInterop.cs` | `Views/`、`Themes/`、`Converters/` |
| `Services/WasapiDeviceService.cs` / `WasapiSessionService.cs` / `WasapiProbe.cs` | `ViewModels/MainViewModel.cs`（`ICollectionView`/`DispatcherTimer` 属于 UI 层） |
| `Services/ProcessInfoHelper.cs` | `Services/Localization/LocExtension.cs`（WPF 标记扩展） |
| `Services/Localization/*`（语言包 + 服务） | `Services/AppIconService.cs`（`ExtractIconEx`） |
| `Services/SettingsStore.cs` / `StartupLog.cs` / `IRoutingService.cs` | `Services/AvatarPalette.cs`（WPF 画刷调色板） |
| `Models/ObservableObject.cs` / `AppSession.cs` / `AudioDevice.cs` | `Models/RelayCommand.cs` / `ToastModel.cs` / `DragPreviewModel.cs` |

#### A.11.1 关键手法：用 `object?` 承载表现层数据，而不是让 Core 依赖 UI 框架

Core 的 `AppSession` 有 `Icon` / `AvatarBrush`，`AudioDevice` 有拖放交互态 ——
但它们全部是 **`object?`**：WPF 今天放进 `ImageSource`/`Brush`，将来 Avalonia 放 `Bitmap`/`Brush`，
**Core 里不出现任何框架类型**，同时界面又能直接绑定核心模型（无需包装层）。
图标在 UI 层枚举后填充，Core 自己永远不创建它们。

#### A.11.2 重构大型 XAML 必须能抓到"静默失效"

XAML 绑定路径写错**既不报编译错误也不抛异常**，只显示空白 ——
所以本轮加了绑定追踪开关（`AUDIOROUTER_TRACE_BINDINGS=1` →
`%LOCALAPPDATA%\AudioRouter\logs\binding-trace.log`）。

**结果：整个收敛重构后，绑定错误日志始终为空。** 这才是这次大改能交付的依据。

#### A.11.3 本轮踩到并修掉的真实回归

| 缺陷 | 根因 | 修法与验证 |
| --- | --- | --- |
| 路由计数正确（`1 条路由`、`⇢1`），**但设备卡里的 chip 不显示** | `AudioDevice` 搬进 Core 时丢了 `Routes.CollectionChanged` 订阅 → `HasRoutes` 变了却不发通知 → chip 区域可见性绑定永远停在 `Collapsed` | 补回订阅，并加 **5 条回归测试**（增/删路由都必须触发 `HasRoutes` 通知） |
| 状态栏路由文案变成英文、且过长溢出 | Core 的 `Describe()` 写死了英文句子 | 改为走语言文件（`status.routing.active` / `status.routing.recordedOnly`），技术原因移到 `Detail` 供 Tooltip 显示 |

> 第一个缺陷值得记住：**它不会被绑定追踪抓到**（绑定是好的，是值从不更新），
> 也不会被编译或测试抓到（除非专门为"派生属性通知"写测试）。只有看界面才能发现。

#### A.11.4 验证结果

| 项 | 结果 |
| --- | --- |
| 四个工程构建 | **0 错误 0 警告** |
| 测试 | **61 项全部通过**（新增 5 条派生属性通知回归用例） |
| 绑定错误追踪 | 重构 + 交互全程**零错误** |
| 实机交互 | 拖拽路由 ✓ / 重复拖拽幂等 ✓ / chip 移除 ✓ / **撤销恢复** ✓ |
| 界面元素 | 图标、音量条、静音标记、默认徽章、折叠分组全部完好 |

### A.12 设备格式信息（24bit / 48kHz）—— 兑现设计文档的承诺

设备卡副标题与 CLI `devices` 的 Format 列现在显示混音格式，例如
**`32 bit float · 48 kHz · 2ch`**。

| 平台 | 取值方式 |
| --- | --- |
| Windows | `IMMDevice::Activate(IAudioClient)` → `GetMixFormat()`（vtable 第 8 槽）；只对**在用端点**查询，26 个失效端点不做无谓 Activate |
| Linux | `pactl -f json list sinks` 的 `sample_spec` → 位深由 `s16le`/`s24_3le`/`float32le` 等格式名折算 |

#### A.12.1 一个"看着对其实误导"的准确性问题

初版只根据 `WAVEFORMATEX.FormatTag` 判断浮点，结果是：
共享模式下端点普遍报 `WAVE_FORMAT_EXTENSIBLE(0xFFFE)`，
真实格式藏在 `WAVEFORMATEXTENSIBLE.SubFormat` GUID 里 ——
于是 **32 bit float 被显示成 32 bit**。

修法：`FormatTag == 0xFFFE` 时读取 `SubFormat`（偏移 = WAVEFORMATEX(18) + 2 + 4 = **24**），
与 `{00000003-...}`（IEEE_FLOAT）/ `{00000001-...}`（PCM）比较；非标准子格式**不做猜测**。
修正后真机输出确认：

```
*  on  扬声器    扬声器 (Realtek(R) Audio)           32 bit float · 48 kHz · 2ch
   on  虚拟声卡  扬声器 (ToDesk Virtual Audio)       32 bit float · 44.1 kHz · 2ch
   on  虚拟声卡  CABLE Input (VB-Audio Virtual Cable) 32 bit float · 48 kHz · 2ch
   -   耳机      耳机                                 (失效端点不显示格式)
```

#### A.12.2 可测性设计

格式拼接是**纯函数**（`AudioFormat.Describe` / `BitsFromSampleFormat` / `DescribeSampleRate`），
因此 GUI 与 CLI 共用同一份文案逻辑，且能在任何平台被测试：

- `48000 → "48 kHz"`、`44100 → "44.1 kHz"`
- `s24_3le → 24`、`float32le → 32 + float 标记`、认不出来 → `0`（**不猜数字**）
- 三个参数全部未知 → 返回空串，界面据此不显示该字段

#### A.12.3 验证

| 项 | 结果 |
| --- | --- |
| 构建 | 四工程 **0 错误 0 警告** |
| 测试 | **75 项全部通过**（新增格式拼接与 `sample_spec` 解析用例） |
| 真机 | CLI 与 GUI 均显示真实格式；失效端点正确留空 |

### A.13 路由按 exe 路径持久化 + 自动恢复（本轮交付）

**为什么改**：路由原先按 PID 存。PID 每次进程启动都会变（Windows 还会复用），
所以"重启后路由还在"从原理上就不成立。身份必须是**可执行文件路径**。

#### A.13.1 身份键

```
path:d:/文档/qq.exe      ← 可执行文件路径（归一化：统一小写 + 正斜杠）
name:firefox             ← 拿不到路径时的弱身份，必须被显式标注
```

- 归一化比较：大小写、`\` 与 `/` 不同也必须命中同一条（Windows 路径大小写不敏感）；
- 弱身份不被隐藏：`routes` 会标 `[weak: matched by process name only]`，`--json` 有 `weakIdentity` 字段；
- 旧格式（按 PID 存、没有身份键）记录**保留但标记 legacy**，不假装能匹配上。

#### A.13.2 自动恢复器（`RouteReconciler`）

把已保存的路由套用到活着的会话上。两条纪律都有测试钉死：

| 纪律 | 为什么 |
| --- | --- |
| 同一次运行内，同一 `(键, 设备, PID)` **只下发一次** | 否则在 Linux 上会每秒重复执行 `move-sink-input`，等于和用户手动调整打架 |
| 应用重启（新 PID）**必须重新下发** | 否则"重启后自动生效"根本不成立 |
| 同一 exe 的**所有实例**都要下发 | 真机上 QQ 就是两个进程；按路径存的意思就是"这个应用" |

#### A.13.3 命令行语义

| 命令 | 说明 |
| --- | --- |
| `route <pid> <deviceId>` | 输入仍是 PID，**存下来的是路径** |
| `unroute <pid\|exePath> <deviceId>` | **应用已退出也能解除**（按路径定位）—— PID 输入会退回用记录里的 `LastPID` |
| `apply` | 立刻把保存的路由套用到当前运行的应用上（无头场景的"恢复路由"） |
| `routes` | 显示身份键、名称、模式、设备、`LastPID` |

#### A.13.4 端到端验证（跨工具闭环）

用 CLI 建立路由，再启动 GUI：

```
$ audio-router route 8480 {5ad7c937-...}
recorded d:/文档/qq.exe -> 扬声器 (ToDesk Virtual Audio) (Route) — audio NOT redirected on this platform

$ audio-router routes              # 新进程读取 → 落盘证明
Application (identity key)  Name  Mode   Device                         LastPID
d:/文档/qq.exe              QQ    Route  扬声器 (ToDesk Virtual Audio)  8480

$ audio-router apply               # 多实例都覆盖
PID    Application  Device                         Mode   Result
8480   QQ           扬声器 (ToDesk Virtual Audio)  Route  recorded only
10388  QQ           扬声器 (ToDesk Virtual Audio)  Route  recorded only
```

随后启动 GUI：ToDesk 设备卡上**自动出现两个 QQ chip**，并提示「已自动恢复 2 条路由」，
状态栏同步为 `2 条路由`。日志确认每个 PID 只下发一次。

#### A.13.5 本轮修掉的两个真实缺陷

| 缺陷 | 根因 | 修法 |
| --- | --- | --- |
| 用不同大小写/斜杠的路径查不到已存路由 | `SameKey` 精确比较字符串，等于把"按路径存"退化成"按路径字符串相等" | 比较前做归一化（由我自己写的用例抓出） |
| **测试进程污染应用日志** | 测试跑同一份 Core，日志文件共享，测试的 reconcile/route 记录混进真实诊断 | `StartupLog.SuppressFileOutput()`，测试启动即关闭文件输出；已验证跑测试前后日志行数不变 |

#### A.13.6 本轮第 4 次"观测者自己出错"

日志里出现同一 PID **两轮** reconcile，看着像去重失效。
查下去发现那两条来自**测试进程**（共享日志文件），GUI 自身每个 PID 恰好一次 —— 去重是好的。

**四次都是同一模式**：GBK 解码 → toast 过期 → 显示代码空引用 → 共享日志文件。
**每次"失败"都出在观测者身上。** 因此现在核对异常的第一步固定为：先分辨是对象坏了，还是尺子坏了。

### A.14 Avalonia 前端（跨平台 GUI · 本轮交付）

**目标**：把视图层从 Windows 专有的 WPF 迁到 Avalonia，实现三平台同一套 GUI。
WPF 版在 Avalonia 达到功能对等前**保持可用**，之后退休。

#### A.14.1 先验工具链，再写代码

Avalonia 是 NuGet 包，而本项目此前零依赖 —— 所以**第一步不是写代码，是验证前提**：

| 检查 | 结果 |
| --- | --- |
| NuGet 源注册 | nuget.org ✓ |
| 本地缓存 | 无 Avalonia（必须真下载） |
| 直连 nuget.org | HTTP 200 ✓ |
| 最小切片 restore + build + 运行 | ✓ 版本 **12.1.3**（最新稳定） |

先跑通"空窗口 + 读一次核心数据"的切片，再动 1000 行视图代码 —— 通了才继续。

#### A.14.2 已移植（真机验证）

| 区块 | 状态 |
| --- | --- |
| 设计 Token / 控件样式 | ✓ 与 WPF **同源**（同一套颜色/圆角/字号/阴影语义） |
| 双栏布局 · 顶栏 · 状态栏 | ✓ |
| 应用列表（分组、搜索、字母头像、音量条、静音标记、⇢N 徽章） | ✓ 真实 WASAPI 数据 |
| 设备卡（类型图标、格式信息、★默认、落点区、chip） | ✓ |
| 已禁用或断开折叠分组 | ✓ |
| Toast 浮层（含撤销按钮） | ✓ |
| 本地化 | ✓ 与 CLI/WPF 共用同一份语言文件，切换即时生效 |
| 路由（按 exe 路径持久化 + 自动恢复） | ✓ **CLI 建立的路由被 Avalonia 前端自动恢复**（同一张表） |
| **拖拽路由交互** | ✓ 自绘引擎（指针捕获 + 命中测试），真机拖拽已验证：应用→设备＝路由；chip→左侧＝移除（含撤销） |

#### A.14.3 两个结构性改进

1. **编译期绑定校验**：开启 `AvaloniaUseCompiledBindingsByDefault`，
   绑定路径写错 = **编译失败**，而不是像 WPF 那样静默显示空白。
   这正是 WPF 迁移中最需要的能力（WPF 版专门做了运行时绑定追踪来补这个洞）。
2. **分组不再依赖 CollectionView**：Avalonia 侧用 `SessionGroup` 直接表达分组，
   更简单也更好测；构造函数里订阅 `Items.CollectionChanged` 触发派生属性通知
   —— 把 WPF 版踩过的"计数对了但条目不显示"从设计上堵住。
3. **消除重复**：`RelayCommand` / `ToastModel` 上移到 Core，两个前端共用一份。

#### A.14.4 本轮踩到的坑

| 缺陷 | 根因 | 修法 |
| --- | --- | --- |
| 界面大量显示**键名**而不是文案（`device.disabledGroup`、`status.routes`…），但少数键是日文 | Avalonia 工程**没把语言文件复制到输出目录** → 内置语言目录不存在，只加载到用户目录里那个 8 键的 ja-JP 包，其余键回退失败 | csproj 补 `None Include="..\Gui\Languages\*.json"`（与 GUI/CLI 共用同一份） |
| XAML 编译失败：`An XML comment cannot contain '--'` | 分节注释写了 `----------` | 改成 `=====`；**同一个坑第二次踩**（WPF 那轮犯过一次） |
| `TextBox.Watermark` 警告 | Avalonia 12 已废弃 | 改用 `PlaceholderText` |
| **命名空间冲突**：类内写 `Avalonia.Input.X` 报「命名空间 AudioRouter.Avalonia 中不存在 Input」 | 本工程命名空间 `AudioRouter.Avalonia` 与依赖的根命名空间 `Avalonia` **冲突** —— 编译器把 `Avalonia` 解析成 `AudioRouter.Avalonia`（外层命名空间里的同级子命名空间） | 命名空间改为 `AudioRouter.Desktop`（程序集名保持 `AudioRouter.Avalonia`，因为 `avares://` 资源 URI 依赖它）。**根因级修复**，不是到处打 `global::` 补丁 |
| `InputHitTest(point)` 报 CS0103 | Avalonia 12 里它是**普通静态辅助方法，不是扩展方法** | 显式写成 `Avalonia.Input.InputExtensions.InputHitTest(this, point)` |

#### A.14.5 Avalonia 12 与 11 的关键 API 差异（实测记录）

拖拽 API 在 12 里被重写，且**不能凭 11 的记忆写**：

| 11 的写法 | 12 的实际 API |
| --- | --- |
| `new DataObject()` + `.Set(format, value)` | `new DataTransfer()` + `Add(DataTransferItem.Create(DataFormat.CreateInProcessFormat<T>("fmt"), value))` |
| `DragDrop.DoDragDrop(pointerArgs, data, effects)` | `DragDrop.DoDragDropAsync(**PointerPressedEventArgs**, IDataTransfer, effects)` |
| `e.Data.Get(fmt)` | `e.DataTransfer` + `DataTransferExtensions.TryGetValues<T>(...)` |

**并且 12 的拖拽入口只接受「按下」事件参数**，而拖拽必须等移动超过阈值才算开始（事件参数还是池化的，不能留用）。
因此 Avalonia 侧的拖拽**没有套用平台 DnD**，改用「指针捕获 + 命中测试」自绘引擎：

- 不依赖平台 DnD 语义，三平台行为一致；
- 顺带实现设计稿要求的**拖拽预览**（跟随光标）；
- 业务判断仍在视图模型 + 核心层，视图层只做"谁拖到谁上面"的翻译。

> 这些 API 结论不是猜的：先用反射探针列出**已安装版本的真实公开成员**，再照着写。
> 探针一次跑清，胜过反复编译试错。

#### A.14.6 尚未移植（诚实清单）

| 项 | 说明 |
| --- | --- |
| 右键菜单 / 复制到多设备对话框 / 语言菜单 | 未移植 |
| 真实 exe 图标 | 当前统一用字母头像（需 HICON → 位图转换） |
| 自绘标题栏 | 当前用系统原生标题栏，应用头栏在其下方（视觉上多一条） |
| 滚动条 / 动画等细节打磨 | 沿用 Fluent 默认 |

#### A.14.6 验证

| 项 | 结果 |
| --- | --- |
| Avalonia 构建 | **0 错误 0 警告** |
| 其余四工程回归 | 0 错误 0 警告 |
| 测试 | **96 项全部通过** |
| 实机 | 截图确认：真实设备/会话枚举、分组、分栏、格式信息、chip、Toast、中文文案、自动恢复路由 |

### A.15 右键菜单 + 复制对话框（Avalonia · 本轮交付）

按 WPF 版的菜单结构**照源移植**（不自己发明交互）：

| 位置 | 菜单项 |
| --- | --- |
| 应用行右键 | 静音/取消静音（勾选态）· 复制到多个设备… · **路由到 ▸**（列出可用设备，勾选态＝已路由）· 分隔线 · 刷新会话 |
| chip 右键 | 移除路由（危险色）· 仅在此设备静音（勾选态）· **复制到其他设备 ▸**（排除当前设备，已路由项禁用） |

「复制到多个设备…」弹模态对话框：设备多选清单 + 取消/应用，**未选任何设备时「应用」禁用**。

#### A.15.1 与 WPF 版的一处差异（可以更简单）

WPF 版必须给子菜单**预先塞一个占位子项**，否则 WPF 会把零子项的 `MenuItem` 当成叶子项
（不显示箭头、不触发 SubmenuOpened）。
Avalonia 侧**不需要**这个技巧 —— 设备清单在右键那一刻就是已知的，
直接在构建菜单时填充子项即可，少一层间接。

#### A.15.2 顺手挖出一个潜伏缺陷（WPF 也在受影响）

移植时核对语言键，发现 **`menu.removeRoute` 这个键根本不存在**，
而 WPF 版 chip 菜单的代码一直在用它 —— 也就是说：

> **WPF 版的「移除路由」菜单项一直在显示键名 `menu.removeRoute`，而不是「移除路由」。**

（`action.removeRoute` 是给 ✕ 按钮的无障碍名称用的，名字相近但不是同一个键。）
本轮把 `menu.removeRoute` 与新增的 `dialog.duplicate.hint` 一起补进了 zh-CN / en-US，
**WPF 版同步修好**。

#### A.15.3 验证（真机，逐条）

```
menu: app context menu for 'Wallpaper Engine' (4 candidate devices)
route: key='path:.../wallpaper32.exe' device='{...8d5b2917...}' mode=Route          ← 菜单「路由到 ▸ CABLE In」
toast[Success]: 已路由 Wallpaper Engine → CABLE In 16ch (VB-Audio Virtual Cable) (可撤销)

dialog: duplicate targets opened for 'Wallpaper Engine' (4 devices)
dialog: duplicate targets closed (confirmed=True)                                    ← 对话框确认
route: ... device='{...e185ce00...}' mode=Duplicate                                  ← 批量复制 2 台
route: ... device='{...caa1b237...}' mode=Duplicate
toast[Success]: 已复制 Wallpaper Engine 到 2 个设备 (可撤销)
```

界面确认：应用行徽章 `⇢3`；Realtek / CABLE Input 两台设备卡上是带**「复制」徽章**的 chip，
CABLE In 是普通路由 chip；状态栏 `5 条路由`。

> 验证手法上也有收获：菜单项**不要用坐标点击**（子菜单边界只有几像素），
> 用 UIA 按元素 `Invoke` 才可靠 —— 本轮第一次坐标点击就落在两个菜单项之间失效了。

#### A.15.4 尚未移植

| 项 | 说明 |
| --- | --- |
| 语言切换菜单（右上角地球按钮） | 目前按钮在位但未接菜单 |
| 真实 exe 图标 | 当前统一字母头像（需 HICON → 位图转换） |
| 自绘标题栏 | 当前用系统原生标题栏 |
| 滚动条 / 动画细节 | 沿用 Fluent 默认 |

### A.16 真实图标 + 语言菜单（Avalonia · 本轮交付）

#### A.16.1 真实 exe 图标

Windows 侧链路：`ExtractIconEx` → `DrawIconEx` 画进 **32bpp DIB** → 拷成 `WriteableBitmap`（BGRA / 预乘）。

| 细节 | 处理 |
| --- | --- |
| 老式图标没有 alpha 通道 | 整幅 alpha 会是 0，直接画等于**全透明**。检测到就按不透明处理 —— 与其显示一个看不见的图标，不如显示一个实的 |
| 缓存 | 按 exe 路径缓存（`ConcurrentDictionary`），超过 256 条清空，避免无界增长 |
| 非 Windows / 取不到 | 返回 null，界面自动退回字母头像 —— **不编造图标** |
| 表现层归属 | 图标是 `object?` 槽位：Core 不引用任何 UI 类型，Avalonia 放 `Bitmap`，WPF 放 `ImageSource` |

同时把 Core 的 `Icon` / `AvatarBrush` 改成**带变更通知**的属性并加 `HasIcon`：
否则图标在枚举之后才填进来时，界面不会刷新，而"该显示图标还是字母"的判断也会停在旧值。

结果：应用行与设备卡上的 chip **都显示真实图标**（Wallpaper Engine / 哔哩哔哩 / Edge / QQ / Steam）。

#### A.16.2 语言菜单

右上角地球按钮 → 语言包列表（当前语言带勾选）+ 导入语言文件… + 打开语言文件夹。
导入走 Avalonia 的 `StorageProvider` 文件选择器；打开目录用 `Process.Start(UseShellExecute)`。

#### A.16.3 一类缺陷（本轮抓出 3 个，且**第一次修复不完整**）

切换语言时，**本地化的计算属性必须自己发通知**。本轮连续踩到三处，且全部只有**看界面**才能发现：

| # | 现象 | 根因 |
| --- | --- | --- |
| 1 | 栏头「应用程序 / 音频设备」、搜索框、空状态停在旧语言 | 视图模型自己持有的那组文案属性没在 `OnLanguageChanged` 里发通知（我只刷了分组标题与模型） |
| 2 | 分组标题 `Now playing` / `Idle · Muted` 停在旧语言 | `TitleWithCount` 由 `Title` 派生，但改 `Title` 时没有连带通知 `TitleWithCount` |
| 3 | chip 上的「复制」徽章停在旧语言 | chip 在**设备里的子集合**里，遍历设备覆盖不到它 |

> 第 1 条修完，截图里还剩 2、3 —— **第一次修复是"部分正确"**。
> 这类缺陷编译器、绑定校验、单元测试全都抓不到：绑定是好的，值只是没更新。
> 结论：**改完必须再看一次界面**，不能因为"逻辑上应该好了"就收工。

#### A.16.4 验证

| 项 | 结果 |
| --- | --- |
| 构建 | 五工程 **0 错误 0 警告** |
| 测试 | **96 项全部通过** |
| 图标 | 截图确认：应用行与 chip 都是真实图标 |
| 语言 | 双向切换实测：`zh-CN → en-US` 与 `en-US → zh-CN`，栏头/搜索框/分组标题/徽章/状态栏**整屏一致** |

#### A.16.5 与 WPF 版的差距（现在只剩视觉项）

| 项 | 状态 |
| --- | --- |
| 功能面（枚举/路由/拖拽/菜单/对话框/语言/图标/Toast/i18n） | ✅ **已对等** |
| 自绘标题栏 | Avalonia 用系统原生标题栏（应用头栏在其下方，视觉上多一条） |
| 滚动条 / 动画细节 | 沿用 Fluent 默认 |

**结论：WPF 版可以退休了**（保留作为参考实现即可）。

### A.17 Linux 后端验证：能验的都验了，验不了的说明白

#### A.17.1 容器前提（如实交代）

用户允许起容器，但本机：

| 检查 | 结果 |
| --- | --- |
| `docker` | **未安装**（命令不存在） |
| WSL 发行版 | **未安装任何发行版**；启用 WSL 需要管理员权限 + 重启 |

**我不会为了验证去擅自改你的系统**（启用 Windows 可选功能 + 重启）。
`linux-x64` 自包含 CLI 已经发布好，**一旦有容器/WSL，验证只差一条命令**（见 A.17.4）。

#### A.17.2 改用的办法：把"进程边界"做成可注入的

`LinuxAudioBackend` 的平台判定与进程调用改成可注入：

```csharp
public LinuxAudioBackend() : this(ProcessRunner.Run, () => OperatingSystem.IsLinux(), ProcessRunner.Exists) { }
internal LinuxAudioBackend(Func<...> run, Func<bool> isSupportedPlatform, Func<string,bool> toolExists) { ... }
```

于是**"实际发出的 pactl 命令行"**——也就是最容易错、之前完全没被覆盖的部分——
可以在没有 Linux 内核的机器上被钉死。新增 **14 项断言**：

| 验证点 | 断言 |
| --- | --- |
| 路由 | 发出 `move-sink-input 42 <sink 名>`（下标与目标都对）→ `Applied` |
| 复制 | pulse 层做不到 → **明确拒绝，且一条命令都不发** |
| 找不到流 | `NotFound` |
| 静音 | `set-sink-input-mute 42 1` |
| 按路由静音 | 把 **sink 名映射成 index** 后再 `set-sink-input-mute 43 1` |
| 解除 | `move-sink-input 42 @DEFAULT_SINK@` |
| 枚举 | sink/sink-input 解析、默认设备标记、`sample_spec → 格式摘要`、稳定 ID（用 sink 名而不是 index） |

#### A.17.3 仍未验证的（不能假装验过）

| 未验证 | 为什么重要 |
| --- | --- |
| 真实 PipeWire/PulseAudio **接受**这些命令 | 命令拼装对了 ≠ 服务端一定接受（版本/参数差异） |
| 音频**真的被改道**（`pactl list sink-inputs` 里 sink 字段变化） | 这是"路由"这个词的唯一硬证据 |
| 各发行版 `pactl` 输出差异 | 样本是录制的典型形态 |
| `/proc/<pid>/exe` 在受限权限下的可读性 | 影响 Linux 上是强身份（路径）还是弱身份（进程名） |

#### A.17.4 一有容器就能跑的验证脚本（已备好）

```bash
# 1) 本机已产出：audio-router/AudioRouter.Cli/bin/Release/net10.0/linux-x64/publish/
# 2) 容器里起一个无头音频服务器 + 空设备
docker run --rm -it -v "<publish目录>:/app" debian:stable-slim bash
apt-get update && apt-get install -y pulseaudio-utils pulseaudio
pulseaudio --start --exit-idle-time=-1 -n \
  --load="module-null-sink sink_name=alpha" \
  --load="module-null-sink sink_name=beta"
# 3) 造一个真实播放流
paplay -d alpha /usr/share/sounds/alsa/Front_Center.wav &
# 4) 验证
/app/audio-router doctor
/app/audio-router devices
/app/audio-router apps
/app/audio-router route <pid> beta      # ← 关键：音频应当真的被改道
pactl list sink-inputs | grep -A2 "Sink:"   # ← 硬证据：sink 变成 beta
```

---

### A.18 应用图标（本轮交付）

**背景**：之前只有应用列表里的"每个应用各自的图标"，**Audio Router 自己**没有图标
（窗口标题栏和 exe 都是系统默认图标）。

#### A.18.1 用代码生成，而不是塞一张图

`IconGenerator`（`--export-icon <dir>`）：用 Avalonia 自己的绘制 API 渲染，**零外部图形依赖**，
一份设计同时产出：

- `app.png`（256×256）→ 窗口图标（`Window.Icon`）
- `app.ico`（16 / 32 / 48 / 256 四尺寸，手写 ICO 容器，PNG 负载）→ exe 内嵌图标

图形语义：**扬声器（音频）+ 右向箭头（路由）**，白描于品牌蓝渐变圆角底。

| 技术点 | 说明 |
| --- | --- |
| ICO 容器手写 | `ICONDIR(6B)` + `ICONDIRENTRY(16B/项)` + 各尺寸负载；Vista 后允许 PNG 负载，不必再编码 BMP + 掩码 |
| 小尺寸可读性 | 内容画在 20×16 设计空间里整体缩放，**线宽随缩放一起变**，16px 下依然清楚 |
| `Bitmap.Save` | Avalonia 12 里 `Save(Stream, int?)` 已废弃 → 用 `Save(Stream, PngBitmapEncoderOptions)`（探针查出来的） |

#### A.18.2 两个前端共用一份图标

| 前端 | 接法 |
| --- | --- |
| Avalonia | `ApplicationIcon` 指向 `Assets\app.ico`；窗口图标读输出的 `Assets\app.png` |
| WPF | `ApplicationIcon` 与 `Resource` 均**链接**到同一份 `.ico`（不复制内容） |

#### A.18.3 验证

| 项 | 结果 |
| --- | --- |
| exe 内嵌图标 | `Icon.ExtractAssociatedIcon` 提取出 32×32 并目视确认是**本图标**（两个前端都是） |
| 窗口图标 | 标题栏左上角显示本图标（截图确认） |
| 图标本身 | 256×256 目视确认：蓝色渐变圆角底 + 白色扬声器与箭头 |
| 回归 | 四工程 0 错误 0 警告；**110 项测试全部通过** |

> 顺带记一笔：本会话 XML 注释里的 `--` 已经踩了**三次**（WPF XAML、Avalonia XAML、csproj）。
> 这是纯自伤型错误，规则很简单：**注释里别出现连续两个连字符**。

### A.19 自绘标题栏（Avalonia · 本轮交付）

消掉与 WPF 版的最后一处视觉差异：系统标题栏整个去掉，应用自己的头栏**就是**标题栏
（拖动 / 双击最大化 / 最小化 / 最大化 / 关闭全部自己来）。

#### A.19.1 Avalonia 12 的又一处 API 变更

| 11 的写法 | 12 的实际 API |
| --- | --- |
| `ExtendClientAreaChromeHints="NoChrome"` | **该属性已不存在** → 改用 `WindowDecorations` 枚举（`None` / `BorderOnly` / `Full`） |
| `this.GetObservable(WindowStateProperty).Subscribe(...)` | 该扩展不适用 → 用标准做法：**重写 `OnPropertyChanged`** 并比对 `WindowStateProperty` |

> 照例：**先反射探针列出真实成员，再动手**。`WindowDecorations` 的取值就是这么查出来的。

#### A.19.2 取舍：为什么最终用 `BorderOnly` 而不是 `None`

- `None`：确实没有系统标题栏，但**调整大小的边框也可能一并消失**（无法用手势确认）；
- `BorderOnly`：保留 OS 边框（可调整大小 + 阴影 + 贴边），标题栏由自绘头栏取代。

判定依据不是"看起来对不对"，而是**窗口样式位**：

```
style = 0x16870000
WS_THICKFRAME (0x40000, 可调整大小): True
WS_MAXIMIZEBOX / WS_MINIMIZEBOX     : True
```

#### A.19.3 诚实交代：什么验了、什么没验

| 项 | 状态 |
| --- | --- |
| 系统标题栏消失、头栏成为标题栏 | ✅ 截图确认 |
| 三个窗口按钮（最小化/最大化/关闭）+ 最大化图标随状态切换 | ✅ 代码路径 + `OnPropertyChanged` 绑定 |
| 双击头栏切换最大化 | ✅ 已实现（同一 `ToggleMaximize`） |
| **用鼠标拖边缘调整大小** | ⚠️ **未能验证**：合成输入触发不了 OS 的非客户区调整循环（多次尝试尺寸不变）。证据是样式位：`WS_THICKFRAME=True` 说明**能力存在** |

> 这条边界必须写清楚：**我没有验过"拖边缘真的能改尺寸"**。
> 用样式位证明能力，和用手势证明行为，不是一回事 —— 前者只能算"没有被配置剥夺"。

#### A.19.4 顺带修掉的实现细节

- 头栏拖动前要排除按钮：点在按钮上不能变成拖窗口（沿视觉树向上找 `Button`）；
- 最大化状态下不响应拖动：否则会出现"一拖先还原、再跟手"的跳动；
- 关闭按钮悬停变红（`#E81123`）、标题栏按钮 46×44 贴合头栏高度。

### A.20 WPF 前端归档（工程结构收敛）

Avalonia 版在**视觉与功能上均已对等**（含自绘标题栏），因此 WPF 前端退休：

| 动作 | 说明 |
| --- | --- |
| 从解决方案移除 | `AudioRouter.Managed.slnx` 现在只有 Core / Cli / Tests / Avalonia 四个工程 |
| 工程保留 | `AudioRouter.Gui/` 仍在仓库中作为**参考实现**（可单独构建），并加了 `ARCHIVED.md` 说明原因与共享资产去向 |
| 语言包搬家 | `AudioRouter.Gui/Languages/` → **`AudioRouter.Core/Languages/`**（语言包属于核心层，不是某个前端的私有资产）；Cli / Avalonia / Gui 都通过 `Link` 引用同一份 |

> 归档前必须处理的隐藏依赖：语言包当时住在 WPF 工程里，被 CLI 与 Avalonia **反向链接**。
> 直接删工程会连带删掉语言包 —— 所以先搬家、再归档。

**验证**（搬家的回归风险正是"界面显示键名"）：

| 项 | 结果 |
| --- | --- |
| 五个工程（含归档工程）构建 | 全部 0 错误 0 警告 |
| 测试 | **110 项全部通过** |
| 语言包到达各前端输出 | Cli / Avalonia / Gui 各 2 个文件 |
| **界面实测** | Avalonia 启动后**全中文**（栏头/搜索框/分组/提示/状态栏），未出现键名 |

顶层新增 `README.md`：工程结构、构建运行方式、平台支持矩阵、**未验证边界**、命令行速查与设计约定。

### A.21 Avalonia 收尾：动效 / 滚动条 / 工程名对齐

#### A.21.1 动效（把设计 Token 补全）

此前只搬了尺寸，**动效时长没搬**。现在 Token 里有 `Motion.Hover=120ms` / `Motion.Enter=160ms`，
并被样式**直接引用**（单一来源，不在样式里写死毫秒）：

| 位置 | 动效 |
| --- | --- |
| 应用行悬停 | `BrushTransition` 120ms |
| 设备卡落点高亮 | `Background` / `BorderBrush` 各 120ms（避免"突现"的割裂感） |
| Toast 入场 | 160ms 淡入（终值即默认不透明度，无残留状态） |

#### A.21.2 滚动条：Fluent 把几何写死在模板里

| 发现 | 证据 |
| --- | --- |
| 设 `ScrollBar.Width=10` **无效** | UIA 量到滚动条控件仍是 **16 逻辑像素宽**（24 物理） |
| 原因 | Fluent 的 ScrollBar 模板把**可见滑块宽度写在模板内部的 Border 上**，外层 `Width` 管不到 |

处理：**保留 Fluent 的轨道/拖拽逻辑**，只覆盖内层 Border 的宽度与圆角，外加颜色与悬停/按下态。
**没有**重写整个 ScrollBar ControlTheme —— 那要自己实现轨道与拖拽，一旦出错就是"滚动条本身坏了"，
代价明显大于"滑块比设计细 2 像素"。

> 如实记录：控件的 16 逻辑像素命中区仍然是 Fluent 的值，不是我设的 10。
> 视觉上滑块为深色圆角（`Brush.Border.Strong`，悬停转 `Text.Tertiary`，按下转 `Text.Secondary`）。

#### A.21.3 工程名对齐：`AudioRouter.Avalonia` → `AudioRouter.Desktop`

此前为了绕开命名空间冲突，出现了**程序集叫 `AudioRouter.Avalonia`、命名空间叫 `AudioRouter.Desktop`** 的错位。
现在全部对齐：目录、csproj、`AssemblyName`、`avares://` 资源 URI、解决方案入口、文档引用，
以及归档 WPF 工程里链接图标的路径（`..\AudioRouter.Desktop\Assets\app.ico`）。

**验证**：

| 项 | 结果 |
| --- | --- |
| 五个工程构建 | 0 错误 0 警告 |
| 测试 | **110 项全部通过** |
| **改名后启动** | ✅ 正常渲染（深色主题/自绘标题栏/图标/中文文案）——**avares 资源能加载，改名的关键风险已排除** |
| 解决方案 | 只剩 Core / Cli / Tests / Desktop 四个工程 |

#### A.21.4 本轮的两条教训（都是我的）

1. **脚本打印假成功**。目录改名因被占用而抛异常后，脚本**照样打印了"renamed"** —— 因为我没有逐步骤校验。
   连带查出一个隐藏原因：**MSBuild 常驻构建节点握着 `obj/` 句柄**，会让目录改名失败；
   `dotnet build-server shutdown` 之后才成功。
   → 改法：每步之后 `Test-Path` / 回读校验，末尾统计失败步骤数；不靠"命令没报错"当成功。
2. **`Join-String` 在 PowerShell 5.1 不存在**（7.0+ 才有），导致 slnx 改写失败、两条工程入口并存。
   用 `-join` 替代。

### A.22 收尾修正：最小化图标、音量条、路由落盘健壮性

#### A.22.1 最小化图标"消失"——零高度几何 + Uniform 缩放

用户反馈"窗口上的最小化图标丢失"。根因很干脆：

```
Geo.Win.Min = "M0,0 L10,0"    ← 一条水平线段，高度为 0
Path Stretch="Uniform"        ← 按几何包围盒等比缩放，包围盒高度 0 → 压成 0 → 不可见
```

**同一个几何在 WPF 版里也是 `M0,0 L10,0`** —— 也就是说这个图标**在两边都不显示**，
只是之前没人盯标题栏所以没被发现。

改法：几何改成**有高度的填充矩形** `M0,0 L10,0 L10,1 L0,1 Z`，渲染从 `Stroke` 改为 `Fill`。
（关闭/最大化/还原本来就是二维几何，所以不受影响。）

#### A.22.2 移除音量条与百分比（两个前端一起）

理由：它表达的信息用户并不需要，而"是否静音"已经由静音图标表达。

| 位置 | 处理 |
| --- | --- |
| Avalonia `MainWindow.axaml` | 删掉百分比文本 + 进度条，只留静音图标 |
| WPF `Themes/Templates.xaml` | 同上（归档工程也改，避免两边不一致） |
| `VolumePercent` / `Volume` 模型属性 | **保留** —— CLI 仍在用 |

#### A.22.3 路由落盘健壮性（由"5 条路由变 0 条"引出）

先说明事实：`routes.json` 变成了完整的 `[]`（不是截断），而代码里**没有任何自动清理**
（唯一删除路径是用户主动移除），所以**不能断定**那 5 条是被 bug 吃掉的 —— 最可能是被拖出移除。
但顺着查，确实找到两个**会造成同类丢失**的真漏洞：

| 漏洞 | 后果 | 修法 |
| --- | --- | --- |
| `File.WriteAllText` **非原子**：先截断再写 | 写到一半被强杀 → 半截 json → 下次解析失败 | 先写 `.tmp` 再 `File.Move(..., overwrite)` 整体替换 |
| 解析失败**静默当成"没有路由"** | 坏文件被后续保存直接覆盖，记录永久消失 | ① 现存的坏文件另存 `routes.corrupt-<时间>.json` 留现场；② **从 `.bak` 恢复** |
| —— 附带的坑：备份"当前文件"可能把坏内容覆盖到好备份上 | 好备份被毁 | 只备份**能解析**的那一代 |

于是形成三道防线：**原子替换**（不产生半截文件）→ **`.bak` 留一代有效版本**（可恢复）→
**`.corrupt-*` 留现场**（可人工抢救）。

**这里我犯过一个错，值得记下来**：我最初写的测试断言是"损坏时备份能救回数据"，
结果**测试直接失败** —— 因为备份到的是**那个损坏文件本身**（垃圾内容），当然救不回路由。
是这条失败的断言把设计从"备份现场"推到了"恢复上一代"。

#### A.22.4 验证

| 项 | 结果 |
| --- | --- |
| 五个工程构建 | 0 错误 0 警告 |
| 测试 | **118 项全部通过**（新增 8 项落盘健壮性断言） |
| 最小化图标 | 实测显示 ✓（`— □ ✕`） |
| 音量条/百分比 | 实测两个前端均已消失 ✓，静音图标保留 ✓ |

---

*文档结束 · 如需平面稿（Figma/视觉稿）或高保真原型，按本文档 Token 与规格直接产出即可。*
