import json, os, subprocess, sys, urllib.request

TOKEN = os.environ.get('GH_TOKEN', '').strip()
if not TOKEN:
    sys.exit('缺少 GH_TOKEN 环境变量')
REPO = 'Moniao08/pi-client'
TAG = 'v1.2'
NAME = 'v1.2 — 青绿主题 · 统一界面'

BODY = """## v1.2 — 青绿主题 · 统一界面

### 界面
- 整体配色换成**青绿色系**：深青黑底 + 青绿强调色（`#2DD4BF` → `#34D399` 渐变），
  滚动条、选中高亮、行内代码、链接同步；应用图标重绘成青绿 π
- **去掉系统标题栏**：顶栏即标题栏，自绘最小化 / 最大化 / 关闭；
  支持拖动、双击最大化、四周边框缩放，最大化限位在工作区（不盖任务栏）
- **滚动条统一**：对话区自绘细滚动条（拖动 / 轨道翻页 / 悬停变亮）；
  侧栏两个列表改为自绘列表控件；工具卡片等系统文本框的滚动条转成深色
- 滚轮改走消息过滤器：鼠标停在对话区上即可滚动，无需先点一下

### 修复
- 无边框窗口下顶栏子控件吞掉命中测试导致拖不动 → 标题区控件改为 `HTTRANSPARENT`

### 下载使用
1. 下 `pi-client-1.2.zip`（或单独的 `pi-client.exe`）
2. 解压后双击 `pi-client.exe` —— 单文件、零依赖（只需 Windows 自带的 .NET Framework 4.x）
3. 客户端会自动找到本机已安装的 pi；没装的话启动时点「安装 pi」执行官方命令

> 客户端本身不含 AI 能力，它通过 pi 官方 RPC 协议驱动你本机的 pi；
> 会话、模型、密钥仍由 pi 自己管理（`~/.pi/agent/`）。

### 其它变更
- 新增源文件：`Native.cs`、`CaptionButton.cs`、`DragSurface.cs`、`DarkList.cs`、`ChatFlow.cs`
- 完整变更见 [CHANGELOG.md](https://github.com/Moniao08/pi-client/blob/main/CHANGELOG.md)
"""


def api(method, url, data=None, ctype='application/json'):
    req = urllib.request.Request(url, method=method)
    req.add_header('Authorization', 'Bearer ' + TOKEN)
    req.add_header('Accept', 'application/vnd.github+json')
    req.add_header('User-Agent', 'pi-client-release-script')
    body = None
    if data is not None:
        body = data if isinstance(data, bytes) else json.dumps(data).encode('utf-8')
        req.add_header('Content-Type', ctype)
    try:
        with urllib.request.urlopen(req, body, timeout=60) as r:
            return r.status, json.loads(r.read().decode('utf-8') or '{}')
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read().decode('utf-8') or '{}')


# 已有同名 release 就先删掉（方便重复运行）
st, rel = api('GET', f'https://api.github.com/repos/{REPO}/releases/tags/{TAG}')
if st == 200:
    print('已存在 release, 先删除:', rel['id'])
    api('DELETE', f"https://api.github.com/repos/{REPO}/releases/{rel['id']}")

payload = {
    'tag_name': TAG,
    'target_commitish': 'main',
    'name': NAME,
    'body': BODY,
    'draft': False,
    'prerelease': False,
}
st, rel = api('POST', f'https://api.github.com/repos/{REPO}/releases', payload)
print('create release:', st)
if st not in (200, 201):
    print(json.dumps(rel, ensure_ascii=False)[:500])
    sys.exit(1)

rel_id = rel['id']
print('release id:', rel_id, '| url:', rel['html_url'])

assets = [
    ('C:/Users/Administrator/pi-client/release/pi-client-1.2.zip', 'pi-client-1.2.zip', 'application/zip'),
    ('C:/Users/Administrator/pi-client/release/pi-client.exe', 'pi-client.exe', 'application/octet-stream'),
]
for path, name, ctype in assets:
    with open(path, 'rb') as f:
        data = f.read()
    st2, res = api('POST', f'https://uploads.github.com/repos/{REPO}/releases/{rel_id}/assets?name={name}', data, ctype)
    print(f'upload {name}: {st2} ' + (str(res.get('size', '')) + ' bytes' if st2 in (200, 201) else json.dumps(res, ensure_ascii=False)[:200]))

st3, final = api('GET', f'https://api.github.com/repos/{REPO}/releases/tags/{TAG}')
print('verify:', st3, '| assets:', [a['name'] + '(' + str(a['size']) + ')' for a in final.get('assets', [])])
print('tag:', final.get('tag_name'), '| target:', final.get('target_commitish'))
