# NOTICE — 来源、修改与第三方组件 / Attribution, Modifications, Third-Party

## 上游项目 / Upstream project

本仓库是下列项目的**修改版（fork / modified version）**：

- **Audio Router** — https://github.com/audiorouterdev/audio-router
- 上游作者：audiorouterdev（及贡献者 asasd）
- 上游许可：**GNU General Public License v3.0**（见 `LICENSE.md`，原样保留）

## 修改声明（GPLv3 §5(a) 要求）/ Statement of modification

- **修改开始日期**：2026-10-07
- **修改性质**：在保留上游 C++ 源码与许可的前提下，新增了跨平台核心库、无头命令行与
  Avalonia 图形界面；上游原有工程未改动。
- **新增内容**：`AudioRouter.Core/`、`AudioRouter.Cli/`、`AudioRouter.Desktop/`、
  `AudioRouter.Tests/`、`docs/`、`AudioRouter.Managed.slnx`、双语文档。
- **归档内容**：`AudioRouter.Gui/`（本仓库早期自研的 WPF 界面，已被 Avalonia 版取代，
  见该目录下 `ARCHIVED.md`）。它**不是**上游的 `audio-router-gui`。
- **未改动内容**：`audio-router/`、`audio-router-gui/`、`do/`、`bootstrapper/`、
  `third-party/`、`audio-router.sln` 等上游文件保持原样。

> 依 GPLv3 §5(b)：本作品仍然以 GPLv3 发布。依 §5(c)：**本作品整体（含新增部分）以 GPL-3.0 授权**，
> 不得改为 MIT / Apache 等宽松许可后再分发。

## 第三方组件 / Third-party components

### Windows Template Library 9.0（`third-party/WTL90_4140_Final/`）

- 许可：**Common Public License 1.0**（见该目录 `CPL.TXT`）
- 用途：**仅**被上游的遗留图形界面 `audio-router-gui/` 引用。
  路由核心（`audio-router/`）、注入器（`do/`）、引导程序（`bootstrapper/`）**都不依赖它**。
- ⚠️ **需要知道的一点**：自由软件基金会（FSF）将 CPL-1.0 归类为
  **"Free but GPL-Incompatible"** —— 与 GPLv3 不兼容
  （依据：它基于 Mozilla Public License 1，附加了 GPL 中没有的若干要求；
  见 https://directory.fsf.org/wiki/License:CPL-1.0 ）。
- 这一组合**沿用自上游仓库**，不是本次修改引入的。如需规避，可在分发时
  **不包含** `third-party/` 与依赖它的 `audio-router-gui/`，其余部分（含路由核心）不受影响。
- 本仓库按 CPL-1.0 要求保留其版权与许可声明，未作修改。

## 本仓库是否可以直接开源？

按照 GPLv3 的要求，**可以**公开发布，前提是做到：

1. 保留 `LICENSE.md`（GPLv3 全文）与版权声明；
2. 明确标注这是**修改版**以及修改日期（本文件，见上）；
3. 整体继续以 **GPL-3.0** 授权；
4. 分发二进制（Releases）时提供对应源码（本仓库即为对应源码；或在 Release 说明中给出仓库链接）；
5. 不添加额外限制（GPLv3 §10）。

上述内容是对许可证文本的整理，**不构成法律意见**；如涉及商业用途请咨询专业人士。
