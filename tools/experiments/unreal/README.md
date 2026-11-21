# UE 5.8 — read-only editor script example

**Not installed, not executed, no test project created.** This directory is a source-controlled **research sample** under D96 and [Unreal spike](../../../docs/research/UNREAL_SCRIPTING_SPIKE.md). It does not authorize using Unreal or migrating CubeGame from Unity.

- `audit_assets.py` calls Unreal Editor Python's `EditorAssetLibrary.list_assets` for `/Game`, prints the count and 25 example paths; no modification or generation.
- Unreal Editor must be installed independently and Python Editor Script Plugin enabled **in the separately authorized test project**. Do not run with the existing CubeGame Unity checkout.
- Consult `UnrealEditor-Cmd.exe -help` and [Epic Editor Python docs](https://dev.epicgames.com/documentation/en-us/unreal-engine/scripting-the-unreal-editor-using-python) to verify exact invocation syntax in the installed UE 5.8 environment.
- For an eventual comparison, run the same task through Unity/CLI and UE/Python, measure duration, failure modes and logs; a console message is not proof of gameplay.

No paid services, MCP, assets, licenses or global machine settings are changed by this repository sample.
