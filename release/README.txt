pi 客户端  v1.0
================

一个给 pi（pi.dev 的编码助手）用的 Windows 图形界面。
不用开终端敲 pi，双击 exe 就能聊天、看工具调用、接着以前的会话干。

特点
----
* 单文件 exe，约 130 KB，零依赖、不需要 .NET SDK / 浏览器 / 安装程序
  （只要 Windows 自带的 .NET Framework 4.x，Win10/11 默认都有）
* 冷启动通常 0.2 秒左右
* 复用你本机已经装好的 pi —— 扩展、技能、斜杠命令、模型配置、
  历史会话全部和终端里的 pi 完全一致（会话存在 ~\.pi\agent\sessions）


使用前：确认本机有 pi
----------------------
客户端本身不含 AI 能力，它调用你电脑上的 pi。三种情况：

1) 已经装过 pi（终端里能跑 pi）：直接双击 exe，客户端会自动找到它。
2) 装过但没检测到：启动时会弹出「绑定本机的 pi」窗口，
   点「手动选择目录…」，选到包含 node.exe / node_modules 的那一层
   （例如 C:\Users\你\AppData\Local\pi-node\current），或者选
   ...\node_modules\@earendil-works\pi-coding-agent 目录本身。
3) 完全没装：在同一个窗口点「安装 pi」，它会在新命令行窗口里执行官方命令
     npm install -g --ignore-scripts @earendil-works/pi-coding-agent
   装完回到窗口点「重新检测」即可。
   （没有 npm 的话，先装 Node.js；或用官方脚本：
     Git Bash 里执行  curl -fsSL https://pi.dev/install.sh | sh ）

之后想换绑定的 pi：点右上角 ⚙，或直接改同目录下的 config.json 里的 "piHome"。
也可以命令行指定：pi-client.exe --setup


日常使用
--------
* 左侧「选择文件夹」= 换项目目录（pi 就在那个目录里干活）
* 左侧「打开的历史会话」点一下 = 接着以前的对话聊（会新开一个标签，不动原会话）
* 输入框：
    Enter 发送
    Shift+Enter 换行
    Ctrl+V 直接贴截图（会作为图片随消息发送）
    输入 / 弹出命令面板（扩展命令、技能、提示模板 + 客户端自带的
    /new /compact /export /stop /dir /rename /setup）
* 回答生成中继续输入 = 作为补充指令排队（steer），可点「停止」中断
* 工具卡片：点标题展开命令/参数/完整输出；代码块点击即可复制
* 顶栏：切模型、切思考等级、上下文用量、压缩上下文（⤵）、
  导出 HTML（⭳）、重命名（✎）、重启会话进程（↻）、绑定 pi（⚙）
* 底部状态栏：pi 版本、状态、上下文 token、本次会话花费


文件说明
--------
pi-client.exe    主程序（单文件，可随便挪位置；配置写在同目录）
config.json      首次运行后自动生成：绑定的 pi、上次目录、窗口位置等
crash.log        只有在程序异常时才会生成，可据此反馈问题


常见问题
--------
Q: 双击没反应 / 一闪而过？
A: 看同目录是否生成了 crash.log。另外确认系统是 Win10 及以上。

Q: 提示「本机没有检测到 pi」？
A: 按上面第 2/3 步绑定或安装。

Q: 回答报鉴权错误？
A: 和终端里一样，先 `pi auth login` 或配好对应厂商的 API Key。
   客户端不另存密钥。

Q: 想看某个工具到底执行了什么？
A: 点工具卡片标题展开，参数和完整输出都在里面。

Q: 能放到 U 盘/别的盘吗？
A: 可以，整个文件夹搬走即可；配置跟着 exe 走（同目录 config.json）。

Q: 想从命令行直接打开某个会话？
A: pi-client.exe --session "C:\Users\你\.pi\agent\sessions\...\xxx.jsonl"

命令行参数
----------
--setup                     启动时先弹出「绑定 pi」窗口
--session <会话文件.jsonl>   打开指定会话
