#!/usr/bin/env python3
"""Build a .unitypackage from Assets/, starting from a released package.

    tools/pack.py "LudoAI_Plugin 1.0.2.unitypackage" "LudoAI_Plugin 1.0.3.unitypackage"

A .unitypackage is a gzipped tar of <guid>/{asset, asset.meta, pathname}. The base
package supplies every entry and its GUID (including files inside .xcframework
bundles, which carry no .meta of their own); an asset or .meta that differs in
Assets/ replaces the base copy. Files new in Assets/ are added under the GUID in
their .meta. Files gone from Assets/ are dropped. The result is re-read and
checked byte-for-byte against Assets/.
"""
import io
import os
import re
import sys
import tarfile

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
base_path, out_path = sys.argv[1], sys.argv[2]


def local(p):
    return os.path.join(root, p)


def read(p):
    with open(p, 'rb') as f:
        return f.read()


entries = {}  # guid -> {name: bytes}
with tarfile.open(base_path, 'r:gz') as tar:
    for m in tar.getmembers():
        if not m.isfile():
            continue
        guid, _, name = m.name.lstrip('./').partition('/')
        entries.setdefault(guid, {})[name] = tar.extractfile(m).read()

by_path = {e['pathname'].decode().splitlines()[0]: g for g, e in entries.items() if 'pathname' in e}
changed, added, dropped = [], [], []

for path, guid in list(by_path.items()):
    e = entries[guid]
    if not os.path.exists(local(path)):
        dropped.append(path)
        del entries[guid]
        continue
    if 'asset' in e and os.path.isfile(local(path)) and read(local(path)) != e['asset']:
        e['asset'] = read(local(path))
        changed.append(path)
    if os.path.exists(local(path) + '.meta') and read(local(path) + '.meta') != e.get('asset.meta'):
        e['asset.meta'] = read(local(path) + '.meta')
        changed.append(path + '.meta')

for dirpath, dirs, files in os.walk(local('Assets')):
    for n in dirs + files:
        full = os.path.join(dirpath, n)
        path = os.path.relpath(full, root)
        if n.endswith('.meta') or path in by_path or n == '.DS_Store':
            continue
        meta = full + '.meta'
        if not os.path.exists(meta):
            if not re.search(r'\.xcframework/', path):
                sys.exit(f'new file without .meta (let Unity create one): {path}')
            continue
        guid = re.search(rb'guid: ([0-9a-f]{32})', read(meta)).group(1).decode()
        e = {'pathname': path.encode(), 'asset.meta': read(meta)}
        if os.path.isfile(full):
            e['asset'] = read(full)
        entries[guid] = e
        added.append(path)

with tarfile.open(out_path, 'w:gz') as tar:
    for guid in sorted(entries):
        for name in ('asset', 'asset.meta', 'pathname', 'preview.png'):
            if name in entries[guid]:
                data = entries[guid][name]
                info = tarfile.TarInfo(f'{guid}/{name}')
                info.size = len(data)
                info.mode = 0o644
                tar.addfile(info, io.BytesIO(data))

# verify: every packaged asset equals Assets/, every Assets/ file is packaged
problems = []
packaged = set()
with tarfile.open(out_path, 'r:gz') as tar:
    files = {}
    for m in tar.getmembers():
        guid, _, name = m.name.partition('/')
        files.setdefault(guid, {})[name] = tar.extractfile(m).read()
for guid, e in files.items():
    path = e['pathname'].decode().splitlines()[0]
    packaged.add(path)
    if 'asset' in e and read(local(path)) != e['asset']:
        problems.append('differs: ' + path)
    if 'asset.meta' in e and read(local(path) + '.meta') != e['asset.meta']:
        problems.append('meta differs: ' + path)
for dirpath, dirs, fs in os.walk(local('Assets')):
    for n in fs:
        path = os.path.relpath(os.path.join(dirpath, n), root)
        if not n.endswith('.meta') and n != '.DS_Store' and path not in packaged:
            problems.append('not packaged: ' + path)

print(f'{out_path}: {len(files)} entries; changed {len(changed)}, added {len(added)}, dropped {len(dropped)}')
for p in changed:
    print('  ~', p)
for p in added:
    print('  +', p)
for p in dropped:
    print('  -', p)
if problems:
    sys.exit('VERIFY FAILED:\n  ' + '\n  '.join(problems))
print('verified: package matches Assets/ byte for byte')
