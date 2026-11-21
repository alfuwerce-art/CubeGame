---
name: cubegame-unity
description: Implement or verify a CubeGame runtime change, regression, Unity test or Windows Player build.
---

- Follow the existing runtime entry point, session lifecycle and save contract. Preserve scene GUIDs and asset .meta pairs.
- Use the editor version in ProjectSettings and one Unity slot. Keep test saves separate from player saves. See [engineering commands](../../../docs/ENGINEERING.md).
- Reproduce with the smallest useful Editor test, then verify the normal PlayMode or Player entry. For world changes include a reload and check stable identities and deltas.
- Report XML counts and exit codes, distinguish failed and skipped checks, and list anything requiring visual review.
