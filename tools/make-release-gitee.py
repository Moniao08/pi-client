"""在 Gitee 上创建 v1.2 发行版并上传附件。

用法（需要一个 Gitee 私人令牌，勾选 projects 权限）：

    set GITEE_TOKEN=你的令牌
    python tools/make-release-gitee.py

令牌获取：Gitee → 右上角头像 → 设置 → 私人令牌 → 生成新令牌（勾选 projects）

也可以只建发行版、不传附件：
    python tools/make-release-gitee.py --no-assets
"""

import json
import mimetypes
import os
import sys
import urllib.parse
import urllib.request
import uuid

TOKEN = os.environ.get('GITEE_TOKEN', '').strip()
OWNER = 'zhang-yj08'
REPO = 'pi-client'
TAG = 'v1.2'
NAME = 'v1.2 — 青绿主题 · 统一界面'
API = 'https://gitee.com/api/v5'

BODY = """### v1.2 — 青绿主题 · 统一界面

**界面**
- 整体配色换成青绿色系：深青黑底 + 青绿强调色（#2DD4BF → #34D399 渐变），滚动条、选中高亮、行内代码、链接同步；应用图标重绘成青绿 π
- 去掉系统标题栏：顶栏即标题栏，自绘最小化 / 最大化 / 关闭；支持拖动、双击最大化、四周边框缩放，最大化限位在工作区（不盖任务栏）
- 滚动条统一：对话区自绘细滚动条（拖动 / 轨道翻页 / 悬停变亮）；侧栏两个列表改为自绘列表控件；工具卡片等系统文本框的滚动条转成深色
- 滚轮改走消息过滤器：鼠标停在对话区上即可滚动，无需先点一下

**修复**
- 无边框窗口下顶栏子控件吞掉命中测试导致拖不动 → 标题区控件改为 HTTRANSPARENT

**下载使用**
1. 下 pi-client-1.2.zip（或单独的 pi-client.exe）
2. 解压后双击 pi-client.exe —— 单文件、零依赖（只需 Windows 自带的 .NET Framework 4.x）
3. 客户端会自动找到本机已安装的 pi；没装的话启动时点「安装 pi」执行官方命令

> 客户端本身不含 AI 能力，它通过 pi 官方 RPC 协议驱动你本机的 pi；会话、模型、密钥仍由 pi 自己管理（~/.pi/agent/）。

**其它**
- 新增源文件：Native.cs、CaptionButton.cs、DragSurface.cs、DarkList.cs、ChatFlow.cs
- 完整变更见 CHANGELOG.md

GitHub 同步发布：https://github.com/Moniao08/pi-client/releases/tag/v1.2
"""


def call(method, path, params=None, body=None, content_type='application/json'):
    url = API + path
    if params:
        url += ('&' if '?' in url else '?') + urllib.parse.urlencode(params)
    data = None
    if body is not None:
        data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode('utf-8')
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header('Content-Type', content_type)
    req.add_header('User-Agent', 'pi-client-release-script')
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            raw = r.read().decode('utf-8')
            return r.status, (json.loads(raw) if raw.strip().startswith(('{', '[')) else raw)
    except urllib.error.HTTPError as e:
        raw = e.read().decode('utf-8', 'replace')
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw


def main():
    if not TOKEN:
        sys.exit('缺少环境变量 GITEE_TOKEN（Gitee → 设置 → 私人令牌）')
    auth = {'access_token': TOKEN}

    st, me = call('GET', '/user', auth)
    if st != 200:
        sys.exit(f'令牌无效（HTTP {st}）：{me}')
    print('已认证:', me.get('login'), me.get('name'))

    repo = f'/{OWNER}/{REPO}'

    # 已有同名发行版 → 先删（方便重复执行）
    st, rels = call('GET', repo + '/releases', dict(auth, page=1, per_page=100))
    if st == 200 and isinstance(rels, list):
        for r in rels:
            if r.get('tag_name') == TAG:
                print('删除同 tag 的旧发行版:', r.get('id'))
                call('DELETE', f"{repo}/releases/{r.get('id')}", auth)

    body = dict(auth)
    body.update({
        'tag_name': TAG,
        'name': NAME,
        'body': BODY,
        'target_commitish': 'main',
        'prerelease': False,
    })
    st, rel = call('POST', repo + '/releases', body=body)
    print('创建发行版:', st)
    if st not in (200, 201):
        sys.exit(f'失败：{rel}')
    rel_id = rel.get('id')
    print('发行版 id:', rel_id)
    print('页面:', rel.get('html_url') or f'https://gitee.com/{OWNER}/{REPO}/releases/tag/{TAG}')

    if '--no-assets' in sys.argv:
        return

    files = [
        'C:/Users/Administrator/pi-client/release/pi-client-1.2.zip',
        'C:/Users/Administrator/pi-client/release/pi-client.exe',
    ]
    for path in files:
        if not os.path.exists(path):
            print('跳过（不存在）:', path)
            continue
        name = os.path.basename(path)
        ctype = mimetypes.guess_type(name)[0] or 'application/octet-stream'
        with open(path, 'rb') as f:
            content = f.read()
        boundary = '----pi' + uuid.uuid4().hex
        parts = []
        for key, value in auth.items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode('utf-8'))
        head = (
            f'--{boundary}\r\n'
            f'Content-Disposition: form-data; name="file"; filename="{name}"\r\n'
            f'Content-Type: {ctype}\r\n\r\n'
        ).encode('utf-8')
        payload = b''.join(parts) + head + content + f'\r\n--{boundary}--\r\n'.encode('utf-8')
        st, res = call('POST', f'{repo}/releases/{rel_id}/attach_files', params=auth, body=payload,
                       content_type=f'multipart/form-data; boundary={boundary}')
        print(f'上传 {name}: {st} ' + (str(res)[:120] if st not in (200, 201) else 'ok'))

    st, final = call('GET', f'{repo}/releases/tags/{TAG}', auth)
    if st == 200:
        assets = final.get('assets') or []
        print('校验: 附件', [a.get('name') for a in assets])


if __name__ == '__main__':
    main()
