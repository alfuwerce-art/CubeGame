# Инструменты и прототипы персонажей CubeGame

[Манифест](provenance.json) фиксирует исходный коммит Factoria и SHA-256 каждого файла. В Git-истории видны авторы и ревизии. [Лицензионная матрица](docs/LICENSE_MATRIX.md) сохраняет прежнюю экспертизу инструментов. Разрешение на выпуск весов, референсов и изображений проверяется для каждого материала отдельно.

## Blender

Скопируйте каталог `scripts` в отдельный рабочий каталог `$run`. Скрипты определяют корень относительно своего расположения и создают `outputs/prototypes` в нём. Перед GEO-04 скопируйте файл `outputs/prototypes/mannequin_geo_03/run1/module_sockets.json` из этого пакета в тот же относительный путь под `$run`. Укажите путь к Blender в `$blender`.

```powershell
& $blender --background --factory-startup --threads 8 --python-exit-code 1 --python "$run/scripts/blender/mannequin_geo_04.py"
& $blender --background --factory-startup --threads 8 --python-exit-code 1 --python "$run/scripts/blender/mannequin_geo_04_fit.py"
```

GEO-02R/03/04 сохраняют отдельные результаты. Шаг `validate` для GEO-03 использует GEO-02R в `outputs/prototypes/mannequin_geo_03/baseline_geo_02r`. Для сравнительных листов нужны Pillow/NumPy и локальный эталон. Результаты проверок и известные дефекты описаны в [техническом отчёте](../../docs/reports/FACTORIA_MIGRATION.md).

Графы ComfyUI требуют собственного окружения, разрешённых моделей и нод. Некоторые графы зависят от локального референса. Проверенный выход конкретной задачи G3 импортируется в Unity отдельно, с сохранением происхождения и визуальным ревью.
