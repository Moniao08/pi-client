"""在 GitHub 上创建发行版并上传附件。

版本号默认取 CHANGELOG.md 里最新的一条；说明正文也从 CHANGELOG 自动提取。
令牌来源：环境变量 GH_TOKEN，或（默认）从 Windows 凭据管理器里读 git 的 GitHub 凭据
（git credential fill），不需要手动粘贴。

用法：
    python tools/make-release-github.py
    python tools/make-release-github.py --version v1.4
"""

import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

OWNER = 'Moniao08'
REPO = 'pi-client'
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

FOOTER = """
### 下载使用

1. 下 `pi-client-{ver}.zip`（或单独的 `pi-client.exe`）
2. 解压后双击 `pi-client.exe` —— 单文件、零依赖（只需 Windows 自带的 .NET Framework 4.x）
3. 客户端会自动找到本机已安装的 pi；没装的话启动时点「安装 pi」执行官方命令

> 客户端本身不含 AI 能力，它通过 pi 官方 RPC 协议驱动你本机的 pi；会话、模型、密钥仍由 pi 自己管理（`~/.pi/agent/`）。

### 直接下载

- GitHub：https://github.com/Moniao08/pi-client/releases/download/{ver}/pi-client-{ver}.zip
- Gitee：https://gitee.com/zhang-yj08/pi-client/releases/download/{ver}/pi-client-{ver}.zip
"""


def _changelog_lines():
    path = os.path.join(ROOT, 'CHANGELOG.md')
    try:
        with open(path, 'r', encoding='utf-8') as f:
            return f.read().replace('\r\n', '\n').split('\n')
    except Exception:
        return []


def latest_version():
    for line in _changelog_lines():
        if line.startswith('## v'):
            return line[3:].split()[0].strip()
    return ''


def changelog_section(version):
    out = []
    inside = False
    for line in _changelog_lines():
        if line.startswith('## '):
            if inside:
                break
            inside = line.startswith('## ' + version)
            if inside:
                out.append(line)
                continue
        if inside:
            out.append(line)
    return '\n'.join(out).strip()


def build_body(version):
    section = changelog_section(version) or ('## ' + version)
    return section + '\n' + FOOTER.format(ver=version.lstrip('v'))


def read_token():
    tok = os.environ.get('GH_TOKEN', '').strip()
    if tok:
        return tok
    try:
        p = subprocess.run(['git', 'credential', 'fill'],
                           input='protocol=https\nhost=github.com\n\n',
                           capture_output=True, text=True, timeout=30)
        for line in (p.stdout or '').splitlines():
            if line.startswith('password='):
                return line[len('password='):].strip()
    except Exception:
        pass
    return ''


def api(method, url, token, data=None, ctype='application/json'):
    req = urllib.request.Request(url, method=method)
    req.add_header('Authorization', 'Bearer ' + token)
    req.add_header('Accept', 'application/vnd.github+json')
    req.add_header('User-Agent', 'pi-client-release-script')
    body = None
    if data is not None:
        body = data if isinstance(data, bytes) else json.dumps(data).encode('utf-8')
        req.add_header('Content-Type', ctype)
    try:
        with urllib.request.urlopen(req, body, timeout=120) as r:
            raw = r.read().decode('utf-8')
            return r.status, (json.loads(raw) if raw.strip() else {})
    except urllib.error.HTTPError as e:
        raw = e.read().decode('utf-8', 'replace')
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw[:200]


def arg(name, default=None):
    if name in sys.argv:
        i = sys.argv.index(name)
        if i + 1 < len(sys.argv):
            return sys.argv[i + 1]
    return default


def main():
    token = read_token()
    if not token:
        sys.exit('缺少 GitHub 令牌：设置 GH_TOKEN，或确保 git 能取到 github.com 的凭据')

    version = arg('--version', None) or latest_version()
    if not version:
        sys.exit('无法确定版本号，请用 --version vX.Y 指定')
    name = arg('--name', version)
    base = 'https://api.github.com/repos/%s/%s' % (OWNER, REPO)

    code, me = api('GET', 'https://api.github.com/user', token)
    if code != 200:
        sys.exit('令牌无效（HTTP %s）：%s' % (code, me))
    print('已认证:', me.get('login'))

    code, rel = api('GET', '%s/releases/tags/%s' % (base, version), token)
    if code == 200 and isinstance(rel, dict):
        print('删除同 tag 旧发行版:', rel.get('id'))
        api('DELETE', '%s/releases/%s' % (base, rel['id']), token)

    body_text = build_body(version)
    payload = {
        'tag_name': version,
        'target_commitish': 'main',
        'name': name,
        'body': body_text,
        'draft': False,
        'prerelease': False,
    }
    code, rel = api('POST', base + '/releases', token, payload)
    print('创建发行版: HTTP', code)
    if code not in (200, 201) or not isinstance(rel, dict):
        sys.exit('失败：%s' % (rel,))
    rel_id = rel['id']
    print('发行版 id:', rel_id)
    print('页面:', rel.get('html_url'))

    ver = version.lstrip('v')
    for fname, ctype in (('pi-client-%s.zip' % ver, 'application/zip'),
                         ('pi-client.exe', 'application/octet-stream')):
        path = os.path.join(ROOT, 'release', fname)
        if not os.path.exists(path):
            print('跳过（不存在）:', path)
            continue
        with open(path, 'rb') as f:
            data = f.read()
        code, res = api('POST', 'https://uploads.github.com/repos/%s/%s/releases/%s/assets?name=%s'
                        % (OWNER, REPO, rel_id, fname), token, data, ctype)
        ok = code in (200, 201)
        print('上传 %-24s HTTP %s  %d bytes %s' % (fname, code, len(data), '' if ok else str(res)[:120]))

    code, final = api('GET', '%s/releases/tags/%s' % (base, version), token)
    if code == 200:
        print('校验: 标题=%s' % final.get('name'))
        print('      附件=%s' % [(a['name'], a['size']) for a in (final.get('assets') or [])])


if __name__ == '__main__':
    main()
