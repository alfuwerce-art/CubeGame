# Проверенная библиотека опыта AI harness

Пока нет ни одной принятой записи. Это **не** база вымышленных улучшений и не источник игровых механик.

После испытаний по [HARNESS_EVALUATION](../HARNESS_EVALUATION.md) создаётся короткая запись `<domain>-<YYYYMMDD>-<slug>.md` со ссылкой на PR и логи:

```text
Problem:
Baseline: task / model / SHA / measurable failure
Variant: exact skill, script or hook change
Probe: identical task + isolated branch + metrics
Independent validation: gameplay/test oracle
Decision: ADOPT | REJECT | INCONCLUSIVE
Applies to / does not apply to:
Rollback:
Owner review:
```

Загружать опыт по релевантному домену. Никогда не пересматривать `AGENTS.md`, права DSH или принятые художественные решения автоматически на основании одного прогона.
