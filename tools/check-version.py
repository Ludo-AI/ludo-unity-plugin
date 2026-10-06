#!/usr/bin/env python3
"""Fail when the plugin version was not kept in step with the code.

    tools/check-version.py             working tree (run.sh, by hand)
    tools/check-version.py --staged    what is staged (the pre-commit hook)
    tools/check-version.py --release   also require LudoAI_Plugin_<version>.unitypackage

The version lives in one place, LudoAIPlugin.PluginVersion, and is sent to Ludo on
every request (x-ludo-tool-version). Rules:
  - README "What's New in X" and its download link name that version;
  - the baseline is the newest release we can find: a vX.Y.Z tag, or a released
    LudoAI_Plugin_X.unitypackage tracked in git (compared from the commit that
    last touched it, so a forgotten or unfetched tag cannot hide a release).
    If Assets/ differs from the baseline the version must be higher; if Assets/
    is unchanged it must be equal. Bump once after a release; later commits pass;
  - --release: LudoAI_Plugin_X.unitypackage exists in the root.
"""
import os
import re
import subprocess
import sys

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
staged = '--staged' in sys.argv


def git(*args):
    return subprocess.run(['git', '-C', root, *args], capture_output=True, text=True)


def read(rel):
    if staged:
        r = git('show', ':' + rel)
        if r.returncode == 0:
            return r.stdout
    with open(os.path.join(root, rel), encoding='utf-8') as f:
        return f.read()


def parse(v):
    return tuple(int(x) for x in v.split('.'))


problems = []
m = re.search(r'PluginVersion\s*=\s*"(\d+\.\d+\.\d+)"', read('Assets/LudoAIPlugin/Editor/LudoAIPlugin.cs'))
if not m:
    sys.exit('check-version: PluginVersion constant not found in ' + ('the staged ' if staged else '') +
             'LudoAIPlugin.cs' + (' (stage the file that adds it)' if staged else ''))
version = m.group(1)
readme = read('README.md')

if f"What's New in {version}" not in readme:
    problems.append(f"README.md has no \"What's New in {version}\" section")
if f'LudoAI_Plugin_{version}.unitypackage' not in readme:
    problems.append(f'README.md download link does not name LudoAI_Plugin_{version}.unitypackage')

# (version, ref, label): releases we know about.
baselines = []
for t in git('tag', '--list', 'v*').stdout.split():
    if re.fullmatch(r'v\d+\.\d+\.\d+', t):
        baselines.append((parse(t[1:]), t, f'tag {t}'))
for pkg in git('ls-files', 'LudoAI_Plugin_*.unitypackage').stdout.split():
    pm = re.fullmatch(r'LudoAI_Plugin_(\d+\.\d+\.\d+)\.unitypackage', pkg)
    sha = git('log', '-1', '--format=%H', '--', pkg).stdout.strip()
    if pm and sha:
        baselines.append((parse(pm.group(1)), sha, f'{pkg} (commit {sha[:7]})'))

if not baselines:
    print('check-version: warning: no release tag or tracked package found (shallow clone? '
          'run `git fetch --tags`); the bump rule was skipped')
else:
    base_version, ref, label = max(baselines, key=lambda b: b[0])
    base = '.'.join(map(str, base_version))
    diff = ['diff', '--cached', '--quiet', ref, '--', 'Assets'] if staged else ['diff', '--quiet', ref, '--', 'Assets']
    changed = git(*diff).returncode != 0
    if changed and parse(version) <= base_version:
        problems.append(f'Assets/ changed since {label} but PluginVersion is still {version}: bump it '
                        f'(constant, README What\'s New + download link) so Ludo can tell the builds apart')
    if not changed and version != base:
        problems.append(f'Assets/ is unchanged since {label} but PluginVersion is {version}')

if '--release' in sys.argv and not os.path.exists(os.path.join(root, f'LudoAI_Plugin_{version}.unitypackage')):
    problems.append(f'LudoAI_Plugin_{version}.unitypackage is missing from the repo root '
                    f'(tools/pack.py LudoAI_Plugin_<previous>.unitypackage LudoAI_Plugin_{version}.unitypackage)')

if problems:
    print('check-version failed:')
    for p in problems:
        print('  -', p)
    sys.exit(1)
print(f'check-version: {version} ok')
