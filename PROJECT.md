# PROJECT —— 长期维护说明（给未来的自己 / 给下一次 pi 会话看）

> 这份文档是项目的「交接说明」。**新开一个 pi 会话、或者 `pi -r` 续接旧会话后，先读这一份**，
> 就能立刻知道项目在哪、怎么编译、发布流程是什么、还有哪些坑没填。

- 仓库（双远程，一条命令推两边）：
  - Gitee：https://gitee.com/zhang-yj08/pi-client
  - GitHub：https://github.com/Moniao08/pi-client
  - main 分支；两个仓库内容保持一致
- 当前版本：**v1.2**（2026-11-11，青绿主题 + 自绘标题栏/滚动条）
- 本机源码目录：`C:\Users\Administrator\pi-client`
- 本机安装目录：`%LOCALAPPDATA%\Programs\pi-client`（桌面快捷方式「pi 客户端」指向这里）
- 运行产物：`release/pi-client.exe`（147 KB）、`release/pi-client-1.2.zip`

---

## 0. 远程仓库与网络

`origin` 的 fetch 指向 Gitee，**push 同时推 Gitee + GitHub**（同一个 origin 上挂了两个推送地址）：

```bat
git remote -v
:: origin  https://gitee.com/zhang-yj08/pi-client.git (fetch)
:: origin  https://gitee.com/zhang-yj08/pi-client.git (push)
:: origin  https://github.com/Moniao08/pi-client.git (push)
git push          :: gitee 和 github 一起推
```

- **GitHub 必须走代理**：本仓库已配 `http.https://github.com.proxy = http://127.0.0.1:7897`
  （Clash Verge 的混合端口）。代理没开时 GitHub 会推失败，而 Gitee 通常已推完，
  此时 `git push` 返回非 0 属正常现象；补推只需 `git push github main`。
  端口变了就改：`git config --local "http.https://github.com.proxy" "http://127.0.0.1:新端口"`。
- **凭据**：`git:https://github.com` 和 `git:https://gitee.com` 都已存在 Windows 凭据管理器，
  本仓库 `credential.helper=wincred`，正常不用输密码。换机器才需要重新登录
  （`git credential-manager github login`，或直接在推送时按提示输入用户名 + PAT）。
- 只推一侧：GitHub → `git push github main`；Gitee → 直接把 Gitee 地址当远程用
  `git push https://gitee.com/zhang-yj08/pi-client.git main`。

---

## 1. 项目是什么

给 [pi](https://pi.dev) 编码助手做的 **Windows 原生图形客户端**：
双击 exe 就能聊天，不用开终端敲 `pi`；界面通过 pi 官方 RPC 协议（`pi --mode rpc`）
驱动用户**本机已安装的 pi**，所以扩展、技能、斜杠命令、模型配置、会话历史与终端里的 pi 完全一致。

设计约束（不要轻易推翻）：

1. **零依赖**：只用 Windows 自带的 .NET Framework 4.x + `csc.exe` 编译，不引入 NuGet/Node/浏览器运行时。
2. **单文件、秒开**：exe ≤ 200 KB，冷启动 < 0.5 s。
3. **不重复实现 agent**：AI 能力全部来自用户的 pi，客户端只是外壳；不碰 `~/.pi/agent/` 里的密钥与会话内容。
4. **便携**：配置写在 exe 同目录 `config.json`，整个文件夹可以拷到 U 盘。

---

## 2. 代码地图

| 文件 | 职责 | 改动频率 |
| --- | --- | --- |
| `src/Program.cs` | 入口：单实例互斥、DPI 感知、崩溃/跟踪日志、`--smoke` 自检 | 低 |
| `src/MainForm.cs` | 主窗口：手动布局（`DoLayout`）、侧栏/顶栏/输入区、会话管理、60 ms 刷新循环 `Pump` | **高** |
| `src/Views.cs` | `MessageView`（用户气泡/助手内容块堆叠） | 中 |
| `src/ChatFlow.cs` | 对话滚动容器：自绘细滚动条、贴底跟随、滚轮/拖动/翻页 | 中 |
| `src/DarkList.cs` | 侧栏自绘列表（细滚动条、悬停/选中态） | 低 |
| `src/CaptionButton.cs` | 自绘最小化/最大化/关闭按钮 | 低 |
| `src/DragSurface.cs` | 命中测试透明的顶栏/标题控件（让无边框窗口能拖动） | 低 |
| `src/Native.cs` | Win32 封装：命中常量、工作区限位、阴影、暗色滚动条、ForceDark | 低 |
| `src/Controls.cs` | 自绘控件：`FlatButton`、`DarkSelect`、`DarkList`、**`MdView`（Markdown+选取复制）**、`ToolCardView`、`ThinkingView` | **高** |
| `src/Session.cs` | 一个会话 = 一个 pi 子进程 + 消息模型；事件→模型→UI 增量的数据中心 | **高** |
| `src/PiRpc.cs` | JSONL 协议：进程管道、请求/响应配对、超时、杀进程树 | 低 |
| `src/PiLocator.cs` | 探测本机 pi、生成安装命令 | 低 |
| `src/SessionStore.cs` | 读 `~/.pi/agent/sessions` 历史清单（只读文件头尾） | 低 |
| `src/SetupForm.cs` | 「绑定本机的 pi」窗口 | 低 |
| `src/Dialogs.cs` | 输入/确认/选择小对话框 | 低 |
| `src/Markdown.cs` | 极简 Markdown 解析（块 + 行内） | 中 |
| `src/Theme.cs` | 配色、字体（按需缩放） | 低 |
| `src/Dpi.cs` | 布局/字号两级缩放判定 | 低 |
| `src/Config.cs` | 便携配置读写 | 低 |

渲染管线（重要，改 UI 前必读）：

```
pi 子进程 stdout(JSONL) → PiRpc.Request/Evt
        → Session.OnEvent：更新 Messages/Blocks（模型）
        → MainForm._dirty = true
        → 60ms Timer → Pump → RefreshUi → SyncChat
              ├─ _forceRebuild → RebuildChat（整段重建 MessageView）
              └─ 增量：MessageView.Sync() 对比 block.Text 与控件内容，只更新变化的块
```

几条踩过的坑（**别再踩**）：

- **布局重入会静默崩溃**（栈溢出，无 crash.log）。`ChatFlow.Relayout`、`MessageView.LayoutAssistant/User`、
  `MdView.Relayout` 都有 `_layingOut/_inLayout/_relaying` 防重入；子控件高度变化只置
  `ChatFlow.PendingLayout = true`，由 60 ms 循环统一重排。
- **字体是 GDI+ 的**：`TextMeter` 用 `TextRenderer.MeasureText(NoPadding)` 缓存宽度；
  CJK 按字符切块（`SplitChunks`），同一样式的相邻块必须合并成一个 `Seg` 再绘制，否则字间会出现缝隙。
- **DPI**：本机系统 125% 缩放，但进程内 DPI 可能报 96 或 120。判定在 `OnHandleCreated` 里做：
  `layoutScale = AppliedDPI/96`（放大布局），`fontScale = AppliedDPI/renderDpi`（渲染已是 120 则不再放大字号）。
- **打包后 asar 只读**：本项目已不用 Electron，但 `Config` 依然写成「exe 同目录」，注意目录可写性。
- **pi 子进程要整棵树杀**：`PiRpc.Dispose()` 用 `taskkill /T /F`。
- **无边框窗口的拖动/缩放**：顶栏（含标题 Label）必须对命中测试返回 `HTTRANSPARENT`，
  否则鼠标落在子控件上，命中测试根本到不了主窗口，拖动无效；缩放则依赖保留
  `WS_THICKFRAME`（系统在客户区外的边框上做命中测试，子控件盖不到），
  并用 `WM_GETMINMAXINFO` 把最大化限制在工作区。
- **系统控件滚动条**：`SetPreferredAppMode(ForceDark)` + `SetWindowTheme(hwnd,"DarkMode_Explorer",null)`
  可让 ListBox/TextBox 的滚动条变深；对话区和侧栏列表则完全自绘。

---

## 3. 常用命令

```bat
:: 编译（产物 dist\pi-client.exe）
cd /d C:\Users\Administrator\pi-client
build.cmd

:: 自检：真实调一次模型 + 截图（结果 dist\smoke.png，异常写 dist\crash.log）
dist\pi-client.exe --smoke "用 bash 执行 ls 并一句话总结"
dist\pi-client.exe --trace        :: 额外输出 trace.log（启动/DPI/事件）

:: 安装到本机 + 重建快捷方式
tools\create-shortcut.ps1

:: 打开某个会话
dist\pi-client.exe --session "C:\Users\Administrator\.pi\agent\sessions\...\xxx.jsonl"
```

一次性的 UI 自动化验证脚本（用真实鼠标事件，改交互后值得重跑）：

```bat
:: 拖选 → Ctrl+C → 读剪贴板
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-selection.ps1
:: 占位提示：失焦/聚焦各截一张图
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-placeholder.ps1
```

> 注意：这些 .ps1 必须是 **UTF-8 with BOM**，否则 PowerShell 5.1 会把中文读成乱码而解析失败；
> `tools/mkcrlf.js` 用于把 .cmd 转成 CRLF（cmd.exe 的 `^` 续行要求）。

---

## 4. 发布流程（做「版本更新」就照这个走）

1. 改代码 → `build.cmd` 编译通过。
2. 跑一遍自检：`--smoke` 至少一次；改了交互就跑上面两个 .ps1。
3. 升版本号：
   - 更新 `release/pi-client.exe`（`copy /Y dist\pi-client.exe release\`）
   - 重新生成 zip（见下）
   - `README.md` 顶部版本说明 + 新增 `CHANGELOG.md` 条目
4. 打 zip（桌面也放一份）：

```powershell
$stage = "C:\Users\Administrator\pi-client\stage"
Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item C:\Users\Administrator\pi-client\dist\pi-client.exe $stage
Copy-Item C:\Users\Administrator\pi-client\README.txt $stage
Compress-Archive -Path "$stage\*" -DestinationPath C:\Users\Administrator\pi-client\release\pi-client-1.2.zip -CompressionLevel Optimal
```

5. 本机更新：`tools\create-shortcut.ps1`（会覆盖 `%LOCALAPPDATA%\Programs\pi-client` 里的 exe）。
6. 提交并推送：

```bat
git add -A
git commit -m "fix(v1.2): 改了什么"
git push
```

   Gitee 凭据存在 Windows 凭据管理器里（`git:https://gitee.com`），无需每次输密码。
   若换机器：`git config credential.helper manager` 或 `wincred`。

7. 可选：发一个 Release（给使用者稳定下载页）。仓库里有两个脚本：

```bat
:: GitHub（用 GCM 存在凭据管理器里的 OAuth token，读 GH_TOKEN）
set GH_TOKEN=...
python tools\make-release-github.py

:: Gitee（需要私人令牌：Gitee → 设置 → 私人令牌 → 勾选 projects）
set GITEE_TOKEN=...
python tools\make-release-gitee.py
```

   两个脚本都会先删掉同 tag 的旧发行版再重建，并上传 `pi-client-<版本>.zip` 与 `pi-client.exe`。
   发之前先 `git tag v<版本> && git push origin v<版本>`（origin 会同时推到 Gitee 和 GitHub）。

---

## 5. 现状与待办

**已具备**：绑定/探测/安装 pi、会话列表与续聊、模型与思考等级、上下文与花费、工具卡片、
思考折叠、Markdown（标题/列表/引用/表格/代码块）、图片粘贴、`/` 命令面板、补充指令排队、
停止、压缩、导出 HTML、重命名、重启会话、单实例、记住窗口、高 DPI、文字拖选复制。

**还可以做（按价值排序）**

1. **跨消息连续选择**：目前每段文本块独立可选；整段对话一次性复制需要选中多块（可加「复制整段对话」）。
2. **双击选词 / 三击选行**、`Ctrl+Shift+C` 等习惯键位。
3. **代码块语法高亮**（现在只有等宽字体 + 单色）。
4. **多标签页 + 会话搜索**（现在靠左侧列表，会话多了要滚）。
5. **托盘 / 开机自启 / 全局快捷键**。
6. **表情/文件拖入**（现在只支持图片粘贴）。
7. **LICENSE**：仓库还没有许可证（如果要给别人自由使用/分发，建议加 MIT）。
8. 安装包（Inno Setup 之类）——目前是「解压即用」，够用就行。

**已知限制**

- 只支持 Windows（.NET Framework + WinForms）。
- 会话内的图片只在历史里显示缩略图，不能点开大图。
- 表格渲染是等宽列，复杂表格（合并单元格）不支持。
- 工具卡片展开后的长输出是 TextBox，滚动条是系统样式（浅色），与暗色主题不完全统一。

---

## 6. 下次怎么接着干

**在终端（cmd）里：**

```bat
cd /d E:\homework
pi -r
```

`pi -r` 会列出本项目目录下的历史会话，选「pi 客户端开发」那条即可继续（会话名称可用
`/session-name pi 客户端开发` 设置，设完 `pi -r` 里更好认）。
也可以直接打开指定会话文件：

```bat
pi --session "C:\Users\Administrator\.pi\agent\sessions\--E--homework--\2026-10-28T00-35-17-279Z_01a17fee-c69e-711a-bca2-129d2a486569.jsonl"
```

**接上后第一句话建议：**

> 读一下 C:\Users\Administrator\pi-client\PROJECT.md 和 CHANGELOG.md，然后我们继续做 v1.3。

**长期备份**

`E:\pi-client-长期项目\` 里存了两份快照（每次大版本更新后补一份）：

- `pi-client-src-<日期>.zip` —— 源码 + release 的工作区快照
- `pi-client-history-<日期>.bundle` —— 完整 git 历史（`git clone pi-client-history-<日期>.bundle pi-client` 可还原）

---

## 7. 关键事实备忘

- pi 安装位置：`%LOCALAPPDATA%\pi-node\current`（官方安装器布局：`node.exe` + `node_modules\@earendil-works\pi-coding-agent`）
- RPC 入口：`...\pi-coding-agent\dist\bundle\rpc-entry.js`（用 `node.exe` 直接跑，避开 .cmd 引号问题）
- 会话文件：`C:\Users\Administrator\.pi\agent\sessions\--E--homework--\*.jsonl`（每行一条 JSON，append-only 树）
- 本机显示：1920×1080，系统缩放 125%（`AppliedDPI=120`）
- 编译器：`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（**C# 5**：不能用 `?.`、字符串插值、
  表达式体成员、`out var`、模式匹配、元组、局部函数）
