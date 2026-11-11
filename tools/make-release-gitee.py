"""在 Gitee 上创建发行版并上传附件（网络层用 curl，避开 urllib 对 Gitee 的兼容问题）。

令牌来源（按优先级）：
  1. 环境变量 GITEE_TOKEN
  2. 同目录上层 .gitee-token 文件（本地临时用，已被 .gitignore 排除）
  3. Windows 凭据管理器里的 pi-client:gitee（tools/store-token.ps1 存进去的）

用法：
    python tools/make-release-gitee.py
    python tools/make-release-gitee.py --version v1.3
    python tools/make-release-gitee.py --no-assets
"""

import json
import os
import subprocess
import sys
import tempfile
import urllib.parse

OWNER = 'zhang-yj08'
REPO = 'pi-client'
API = 'https://gitee.com/api/v5'
DEFAULT_VERSION = 'v1.2'

DEFAULT_BODY = """### v1.2 — 青绿主题 · 统一界面

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

**直接下载**

- Gitee：https://gitee.com/zhang-yj08/pi-client/releases/download/v1.2/pi-client-1.2.zip
- GitHub：https://github.com/Moniao08/pi-client/releases/download/v1.2/pi-client-1.2.zip

**其它**
- 新增源文件：Native.cs、CaptionButton.cs、DragSurface.cs、DarkList.cs、ChatFlow.cs
- 完整变更见 CHANGELOG.md
"""


def read_token():
    tok = os.environ.get('GITEE_TOKEN', '').strip()
    if tok:
        return tok
    here = os.path.dirname(os.path.abspath(__file__))
    for name in ('.gitee-token', os.path.join('release', '.gitee-token')):
        path = os.path.join(here, '..', name)
        if os.path.exists(path):
            with open(path, 'r', encoding='utf-8') as f:
                tok = f.read().strip()
            if tok:
                return tok
    try:
        import ctypes
        from ctypes import wintypes

        class CREDENTIAL(ctypes.Structure):
            _fields_ = [
                ('Flags', wintypes.DWORD),
                ('Type', wintypes.DWORD),
                ('TargetName', wintypes.LPWSTR),
                ('Comment', wintypes.LPWSTR),
                ('LastWritten', wintypes.FILETIME),
                ('CredentialBlobSize', wintypes.DWORD),
                ('CredentialBlob', ctypes.POINTER(ctypes.c_byte)),
                ('Persist', wintypes.DWORD),
                ('AttributeCount', wintypes.DWORD),
                ('Attributes', ctypes.c_void_p),
                ('TargetAlias', wintypes.LPWSTR),
                ('UserName', wintypes.LPWSTR),
            ]

        advapi = ctypes.WinDLL('advapi32', use_last_error=True)
        pcred = ctypes.POINTER(CREDENTIAL)()
        if advapi.CredReadW('pi-client:gitee', 1, 0, ctypes.byref(pcred)):
            try:
                blob = pcred.contents.CredentialBlob
                size = pcred.contents.CredentialBlobSize
                return ctypes.string_at(blob, size).decode('utf-16-le', 'ignore').strip()
            finally:
                advapi.CredFree(pcred)
    except Exception:
        pass
    return ''


def curl(method, path, params=None, form=None, files=None, timeout=180):
    """返回 (http_code, parsed_json_or_text)"""
    url = API + path
    tmp = None
    if params:
        url += ('&' if '?' in url else '?') + '&'.join(
            '%s=%s' % (k, str(v)) for k, v in params.items())
    cmd = ['curl', '-sS', '-X', method, url, '-w', '\n%{http_code}']
    if form:
        # 表单体自己按 UTF-8 百分号编码后写入临时文件，避免命令行编码把中文搞坏
        body = urllib.parse.urlencode(form, quote_via=urllib.parse.quote).encode('utf-8')
        fd, tmp = tempfile.mkstemp(suffix='.form')
        with os.fdopen(fd, 'wb') as f:
            f.write(body)
        cmd += ['-H', 'Content-Type: application/x-www-form-urlencoded', '--data-binary', '@' + tmp]
    if files:
        for field, local in files:
            cmd += ['-F', '%s=@%s' % (field, local)]
    try:
        res = subprocess.run(cmd, capture_output=True, timeout=timeout)
    except subprocess.TimeoutExpired:
        return 0, 'timeout'
    finally:
        if tmp and os.path.exists(tmp):
            try:
                os.remove(tmp)
            except Exception:
                pass
    out = res.stdout.decode('utf-8', 'replace')
    if '\n' in out:
        body, _, code = out.rpartition('\n')
    else:
        body, code = out, '0'
    code = int(code.strip() or 0) if code.strip().isdigit() else 0
    if os.environ.get('GITEE_VERBOSE'):
        shown = url.replace(token, '***') if 'token' in dir() else url
        print('[%s %s] -> %s | %s' % (method, shown, code, body[:70].replace(chr(10), ' ')), file=sys.stderr)
    try:
        return code, json.loads(body)
    except Exception:
        return code, body[:200]


def arg(name, default=None):
    if name in sys.argv:
        i = sys.argv.index(name)
        if i + 1 < len(sys.argv):
            return sys.argv[i + 1]
    return default


def main():
    token = read_token()
    if not token:
        sys.exit('缺少令牌：设置 GITEE_TOKEN，或用 tools/store-token.ps1 存入凭据管理器')

    version = arg('--version', DEFAULT_VERSION)
    name = arg('--name', version + ' — 青绿主题 · 统一界面')
    auth = {'access_token': token}
    repo = '/repos/%s/%s' % (OWNER, REPO)

    code, me = curl('GET', '/user', auth)
    if code != 200 or not isinstance(me, dict):
        sys.exit('令牌无效（HTTP %s）：%s' % (code, me))
    print('已认证:', me.get('login'), me.get('name'))

    code, rels = curl('GET', repo + '/releases', dict(auth, page=1, per_page=100))
    if code == 200 and isinstance(rels, list):
        for r in rels:
            if r.get('tag_name') == version:
                print('删除旧发行版:', r.get('id'), r.get('name'))
                curl('DELETE', '%s/releases/%s' % (repo, r.get('id')), auth)

    form = {
        'tag_name': version,
        'name': name,
        'body': DEFAULT_BODY,
        'target_commitish': 'main',
        'prerelease': 'false',
    }
    code, rel = curl('POST', repo + '/releases', params=auth, form=form)
    print('创建发行版: HTTP', code)
    if code not in (200, 201) or not isinstance(rel, dict):
        sys.exit('失败：%s' % (rel,))
    rel_id = rel.get('id')
    print('发行版 id:', rel_id)
    print('页面:', rel.get('html_url') or ('https://gitee.com/%s/%s/releases/tag/%s' % (OWNER, REPO, version)))

    if '--no-assets' not in sys.argv:
        for fname in ('pi-client-%s.zip' % version.lstrip('v'), 'pi-client.exe'):
            path = 'C:/Users/Administrator/pi-client/release/' + fname
            if not os.path.exists(path):
                print('跳过（不存在）:', path)
                continue
            code, res = curl('POST', '%s/releases/%s/attach_files' % (repo, rel_id),
                             params=auth, files=[('file', path)])
            size = os.path.getsize(path)
            ok = code in (200, 201)
            print('上传 %-22s HTTP %s  %d bytes  %s' % (fname, code, size, '' if ok else str(res)[:120]))

    code, final = curl('GET', '%s/releases/tags/%s' % (repo, version), auth)
    if code == 200 and isinstance(final, dict):
        assets = [a.get('name') for a in (final.get('assets') or [])]
        print('校验: 标题=%s' % final.get('name'))
        print('      附件=%s' % assets)


if __name__ == '__main__':
    main()
