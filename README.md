# pi 客户端（Windows 原生）

给 [pi](https://pi.dev) 编码助手做的 Windows 图形界面：**双击 exe 就能用，不用开终端敲 `pi`**。

- 单文件 exe，约 132 KB，**零依赖**（只用 Windows 自带的 .NET Framework 4.x，不需要浏览器 / Node / .NET SDK / 安装程序）
- 冷启动约 0.16 秒
- 调用你本机**已经装好的 pi**，所以扩展、技能、斜杠命令、模型配置、历史会话都和终端里的 pi 完全一致

```
┌──────────────┬──────────────────────────────────────────┐
│ 新对话        │  会话标题        [模型] [思考等级] [上下文] │
│ 工作目录      ├──────────────────────────────────────────┤
│ 打开的对话    │  你：……                                   │
│ 历史会话      │  π：流式回答 / 思考过程 / 工具调用卡片      │
│              │  ┌────────────────────────────────────┐  │
│              │  │ 输入框（可粘贴截图）      [停止][发送] │  │
└──────────────┴──────────────────────────────────────────┘
```

## 直接使用（不需要编译）

1. 下载 [`release/pi-client.exe`](release/pi-client.exe)（也可以下载 [`release/pi-client-1.0.zip`](release/pi-client-1.0.zip) 解压）
2. 双击运行 —— 客户端会自动找到本机的 pi
3. 如果没装 pi，启动时会弹出「绑定本机的 pi」窗口，点「安装 pi」执行官方命令
   `npm install -g --ignore-scripts @earendil-works/pi-coding-agent`，装完点「重新检测」
4. 点左侧「选择文件夹」选项目目录，输入需求，`Enter` 发送

> 客户端本身不含 AI 能力，它只是 pi 的图形外壳；依旧需要 `pi auth login` 或配好对应厂商的 API Key。

## 功能

| 区域 | 能做什么 |
| --- | --- |
| 左栏 | 新建对话、切换工作目录、多个对话并行切换、点历史会话直接续聊 |
| 顶栏 | 切换模型 / 思考等级、上下文用量与花费、压缩上下文、导出 HTML、重命名、重启会话进程、绑定 pi |
| 对话区 | 流式输出、思考过程折叠、工具调用卡片（点开看参数与完整输出）、Markdown（标题/列表/引用/表格/代码块） |
| 代码块 | 点一下就复制 |
| 输入框 | `Enter` 发送、`Shift+Enter` 换行、`Ctrl+V` 贴图、输入 `/` 弹出命令面板 |
| 排队 | 回答中继续输入 → 作为补充指令（steer）排队，可随时停止 |

客户端自带命令：`/new` `/compact` `/export` `/stop` `/dir` `/rename` `/setup`

命令行参数：`--setup`（强制打开绑定窗口）、`--session <会话.jsonl>`（直接打开某个会话）

## 从源码构建

只需要 Windows 自带的 C# 编译器，**不需要 Visual Studio / .NET SDK**：

```bat
build.cmd                     :: 编译出 dist\pi-client.exe
tools\create-shortcut.ps1     :: 安装到 %LOCALAPPDATA%\Programs\pi-client 并建快捷方式
```

自检（会真实调用一次模型并截图）：

```bat
dist\pi-client.exe --smoke "用 bash 执行 ls 并一句话总结"
:: 结果见同目录 smoke.png；异常会写 crash.log；加 --trace 输出 trace.log
```

## 目录结构

```
src/                 C# 源码（C# 5，WinForms）
├─ Program.cs        入口：单实例、DPI 比例、崩溃日志、--smoke 自检
├─ MainForm.cs       主窗口：布局、侧栏、顶栏、输入、会话管理
├─ Views.cs          消息视图（用户气泡 / 助手内容块）与滚动容器
├─ Controls.cs       自绘控件：按钮、下拉、列表、Markdown 视图、工具卡片、思考块
├─ Markdown.cs       极简 Markdown 解析
├─ Session.cs        一个会话 = 一个 pi --mode rpc 子进程 + 消息模型
├─ PiRpc.cs          JSONL 协议（进程管道、请求/响应、事件）
├─ PiLocator.cs      探测本机 pi / 生成安装命令
├─ SessionStore.cs   读取 ~/.pi/agent/sessions 历史
├─ SetupForm.cs      「绑定本机的 pi」窗口
├─ Dialogs.cs        输入/确认/选择对话框
├─ Theme.cs          配色与字体（含按需缩放）
├─ Dpi.cs            DPI 缩放判断
├─ Json.cs           JSON 小工具
└─ Config.cs         便携配置（写在 exe 同目录）
app.manifest         高 DPI 声明
build.cmd            编译脚本
icon.ico / icon.png  应用图标
tools/               辅助脚本（生成快捷方式、CRLF 转换）
```

工作原理：客户端用 pi 官方的 RPC 协议（`pi --mode rpc`）拉起子进程，通过 stdin/stdout 收发 JSONL；
会话文件、模型、密钥仍然由 pi 自己管理（`~/.pi/agent/`），客户端不改动它们。

## 常见问题

**双击没反应 / 一闪而过？**
看同目录是否生成 `crash.log`；确认是 Win10 及以上。

**提示「本机没有检测到 pi」？**
点「手动选择目录…」，选到包含 `node.exe` / `node_modules` 的那一层（例如
`C:\Users\你\AppData\Local\pi-node\current`），或者 `...\node_modules\@earendil-works\pi-coding-agent` 目录本身。

**想换绑定的 pi？**
右上角 ⚙，或改 exe 同目录 `config.json` 的 `"piHome"`，或 `pi-client.exe --setup`。

**能放 U 盘吗？**
可以，整个文件夹搬走即可，配置跟着 exe 走。
