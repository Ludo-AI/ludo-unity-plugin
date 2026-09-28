# Plugin test harness

Runs `Assets/LudoAIPlugin/Editor/LudoAIPlugin.cs` without Unity: it is compiled
unmodified against stand-ins for the Unity API (`UnityStubs/`, held to Unity
2019.4's netstandard2.0 + C# 7.3), together with the bundled Editor Coroutines
package, and driven by clicking the plugin's own buttons.

    harness/run.sh                         # working tree vs the fake API
    harness/run.sh --ref v1.0.2-source     # any git revision
    harness/run.sh --filter audio          # matching scenarios only
    LUDO_API_KEY=... harness/run.sh --live # real API (LUDO_API_URL, default dev); spends credits

Needs the .NET SDK (8+). Saved files land in `$TMPDIR/ludo-unity-harness/`.

## What it checks

- **Contract**: the fake API (`Runner/FakeLudoApi.cs`) behaves like the public API's
  job queue (202 + job, `GET /assets/jobs/{id}?wait=`, `poll_after_ms`, 429
  `Retry-After`) and validates every request body against
  `contract/openapi-public.json`: wrong types/enums/required fields are 400s like
  the real server; fields the real API would silently drop, and deprecated
  fields, fail the scenario.
- **Every generator** end to end, including preview loading and the Save /
  Download buttons (file extension must match the bytes).
- **Failure paths**: failed job, full queue, bad key, validation error, poll
  429, dropped connection, a job that never finishes.
- **UI**: every screen renders; every dropdown value is accepted by the API;
  every animation model x duration; margin modes; image options.

## Keeping the plugin current

When the public API changes, refresh the contract from the backend and rerun:

    harness/contract/sync.sh ../prometheus-server
    harness/run.sh

`contract/pending.json` lists fields the plugin already sends that are merged in
the backend but not yet published in the spec; drop an entry once `sync.sh`
brings it in (the runner prints each pending field it applies).

What it cannot see: IMGUI layout, Unity's asset import, and the native WebP
decoder (the stubs only recognise the WebP header). Those keep the patterns
1.0.2 already used in Unity.

## Packaging

    python3 tools/pack.py LudoAI_Plugin_1.0.3.unitypackage LudoAI_Plugin_1.0.4.unitypackage

Starts from the current package in the repo root (keeping every GUID), swaps in
what changed under `Assets/`, and verifies the result byte for byte. Then
`git rm` the old package, tag `vX.Y.Z` and attach the new one to a GitHub Release.
Older packages live in git history and in the Releases.
