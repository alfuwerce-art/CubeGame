---
name: cubegame-debug-regression
description: "Investigate a reproducible Unity or C# failure and validate a focused fix."
---
# cubegame-debug-regression
1. Record scene, build, SHA, error logs and reproduction steps.
2. Trace the failing code and affected state before changing it.
3. Make a falsifiable hypothesis and minimum patch; preserve save compatibility.
4. Run targeted test, broader regression and Windows Player where applicable.
5. Report symptom, cause evidence, code diff, test XML, SHA and remaining risks.
