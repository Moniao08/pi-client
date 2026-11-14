"""在 Gitee 上创建发行版并上传附件（网络层用 curl）。

版本号默认取 CHANGELOG.md 里最新的一条；说明正文也从 CHANGELOG 自动提取。

令牌来源（按优先级）：
  1. 环境变量 GITEE_TOKEN
  2. Windows 凭据管理器 pi-client:gitee（tools/store-token 存进去的）
  3. 仓库根的 .gitee-token 文件（已 gitignore）

用法：
    python tools/make-release-gitee.py
    python tools/make-release-gitee.py --version v1.4
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
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

FOOTER = """
**下载使用**

1. 下 pi-client-{ver}.zip（或单独的 pi-client.exe）
2. 解压后双击 pi-client.exe —— 单文件、零依赖（只需 Windows 自带的 .NET Framework 4.x）
3. 客户端会自动找到本机已安装的 pi；没装的话启动时点「安装 pi」执行官方命令

> 客户端本身不含 AI 能力，它通过 pi 官方 RPC 协议驱动你本机的 pi；会话、模型、密钥仍由 pi 自己管理（~/.pi/agent/）。

**直接下载**

- Gitee：https://gitee.com/zhang-yj08/pi-client/releases/download/{ver}/pi-client-{ver}.zip
- GitHub：https://github.com/Moniao08/pi-client/releases/download/{ver}/pi-client-{ver}.zip
"""


# ---------------------------------------------------------------- CHANGELOG

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
    section = changelog_section(version) or ('### ' + version)
    return section + '\n' + FOOTER.format(ver=version.lstrip('v'))


# ---------------------------------------------------------------- 令牌

def read_token():
    tok = os.environ.get('GITEE_TOKEN', '').strip()
    if tok:
        return tok
    path = os.path.join(ROOT, '.gitee-token')
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


# ---------------------------------------------------------------- HTTP（curl）

def call(method, path, params=None, form=None, files=None, timeout=240):
    """返回 (http_code, json_or_text)"""
    url = API + path
    if params:
        url += ('&' if '?' in url else '?') + '&'.join('%s=%s' % (k, v) for k, v in params.items())

    tmp = None
    cmd = ['curl', '-sS', '-X', method, url, '-w', '\n%{http_code}']
    if form is not None:
        # 表单体用 UTF-8 百分号编码写临时文件，避免命令行编码破坏中文
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
    body, _, code = out.rpartition('\n') if '\n' in out else (out, '', '0')
    code = int(code.strip()) if code.strip().isdigit() else 0
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
        sys.exit('缺少令牌：设置 GITEE_TOKEN，或先运行 tools/store-token（见 PROJECT.md）')

    version = arg('--version', None) or latest_version()
    if not version:
        sys.exit('无法确定版本号，请用 --version vX.Y 指定')
    name = arg('--name', version)
    auth = {'access_token': token}
    repo = '/repos/%s/%s' % (OWNER, REPO)

    code, me = call('GET', '/user', auth)
    if code != 200 or not isinstance(me, dict):
        sys.exit('令牌无效（HTTP %s）：%s' % (code, me))
    print('已认证: %s (%s)' % (me.get('login'), me.get('name')))

    code, rels = call('GET', repo + '/releases', dict(auth, page=1, per_page=100))
    if code == 200 and isinstance(rels, list):
        for r in rels:
            if r.get('tag_name') == version:
                print('删除旧发行版:', r.get('id'), r.get('name'))
                call('DELETE', '%s/releases/%s' % (repo, r.get('id')), auth)

    body_text = build_body(version)
    form = {
        'tag_name': version,
        'name': name,
        'body': body_text,
        'target_commitish': 'main',
        'prerelease': 'false',
    }
    code, rel = call('POST', repo + '/releases', params=auth, form=form)
    print('创建发行版: HTTP', code)
    if code not in (200, 201) or not isinstance(rel, dict):
        sys.exit('失败：%s' % (rel,))
    rel_id = rel.get('id')
    print('发行版 id:', rel_id)
    print('说明长度:', len(body_text))
    print('页面: https://gitee.com/%s/%s/releases' % (OWNER, REPO))

    if '--no-assets' not in sys.argv:
        ver = version.lstrip('v')
        for fname in ('pi-client-%s.zip' % ver, 'pi-client.exe'):
            path = os.path.join(ROOT, 'release', fname)
            if not os.path.exists(path):
                print('跳过（不存在）:', path)
                continue
            code, res = call('POST', '%s/releases/%s/attach_files' % (repo, rel_id),
                             params=auth, files=[('file', path)])
            ok = code in (200, 201)
            print('上传 %-24s HTTP %s  %d bytes %s' % (fname, code, os.path.getsize(path),
                                                      '' if ok else str(res)[:120]))

    code, final = call('GET', '%s/releases/tags/%s' % (repo, version), auth)
    if code == 200 and isinstance(final, dict):
        print('校验: 标题=%s' % final.get('name'))
        print('      附件=%s' % [a.get('name') for a in (final.get('assets') or [])])


if __name__ == '__main__':
    main()
