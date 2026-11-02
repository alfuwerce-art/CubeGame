# CubeGame — карты замены Plasma -> Unity 6.3 LTS

**Версия Editor:** `6000.3.27f2`; для CubeGame не открывать проект в Unity 6.6 без ADR/тестов.  
**Архитектура:** модульный монолит ADR-0001. `Presentation` отделён от игровой логики.  
**Цель:** максимально использовать уже существующие Unity API и пакеты; писать собственные системы **только** там, где Unity не даёт правил игры.

| Что обнаруживаем в оригинале | Сначала проверить в Unity | Самостоятельный код CubeGame |
|---|---|---|
| D3D9/Plasma graphics, окна и вывод | Unity Rendering + URP, MeshRenderer, Light, материалы/шейдеры | Особая генерация voxel-мешей/LOD, визуальные настройки по прототипам |
| Сетки `Chunk/Region` и ландшафт | `Mesh`, `MeshData`, `MeshCollider`, `LODGroup`, Jobs/Burst для горячих циклов | Генератор по seed, высоты/биомы/декорации, стриминг, стабильные ID |
| Контроллер персонажа и перемещение | `CharacterController` либо `Rigidbody/Collider` после реального сравнения | Игровые режимы движения и правила, переходы, управление навыками |
| Камера третьего лица | `Camera`; при необходимости `Cinemachine` | Специфическое поведение, дистанция, столкновения и параметры Alpha |
| Клавиатура/мышь/геймпад | Unity `Input System` | Действия игры, биндинги, реакция |
| `Creature` и combat/abilities | `Animator`, `Physics`, `Animation Events`, VFX | Урон, броня, Power, MP, cooldown, combo, hit windows и навыки |
| Скелеты/анимации Plasma | `Animator Controller`, `Blend Trees`, `Avatar/Generic rig`, `Animator Override Controller` | Риги и анимационные клипы в S05A/S05B, параметры и реакции |
| AI Behavior, пути NPC | `NavMesh` / `AI Navigation` (после проверки работы со streaming chunks) | Логика решений/потребности/фракции/память, дальняя симуляция |
| Панели UI/диалоги/меню Plasma | `UI Toolkit` или `uGUI`, TMP (по проверке) | Раскладка интерфейса, правила отображения и UX CubeGame |
| Воспроизведение звуков/XAudio | Unity AudioSource/AudioMixer | События аудио и собственные композиции/эффекты |
| SQLite сохранения Alpha | `JsonUtility` для простых структур, `System.IO`, сериализатор/SQLite-пакет если оправдан | Версии/миграции, события, persistence мира и персонажей; **не** считать JsonUtility полным универсальным решением |
| Загрузка моделей/пакетов | Unity AssetDatabase (Editor), Addressables/AssetBundles (если нужно runtime) | Организация контента и версии, не чужой `.cub` runtime loader без надобности |
| Сценические/физические тесты | Unity Test Framework (EditMode, PlayMode), Unity CLI | Golden cases из Alpha, perf benchmarks, сценарии загрузки/сохранения |

## 1. Принцип BeforeBuild: сначала проверить, умеет ли Unity это сама

Перед добавлением собственной инфраструктуры агент обязан указать:
1. Что требуется по поведению Alpha?
2. Какой **встроенный компонент / официальный пакет** Unity решает техническую часть задачи?
3. Почему встроенного недостаточно (функциональность, потери FPS/RAM, streaming, детерминированность)?
4. Какая минимальная обёртка/адаптер нужна?
5. Как измерена стоимость/результат альтернатив? Если нет измерения — ПОКА НЕ писать «своё быстрее».
6. Разрешение на новую зависимость, точная версия пакета/лок-файл, результаты после установки.

**Не вводить весь ECS/DOTS/Netcode/Addressables/Cinemachine заранее.** Для начала сохранить понятный модульный C# Core. Jobs/Burst и MeshData применять по профилированию на больших областях, не потому что они модны.

## 2. Ссылки на официальную документацию именно Unity 6.3

- Mesh API / MeshData: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh
- Job System: https://docs.unity.com/en-us/engine/6000.3/manual/scripting/programming-distribute-work-threads/job-system/overview
- Cinemachine: https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-cinemachine
- Input System: https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-inputsystem
- AI Navigation: https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-ai-navigation
- Animation state machine: https://docs.unity.com/en-us/engine/6000.3/manual/animation-section/animation-mecanim/animation-animator-controller/animation-state-machines
- Animator Controller: https://docs.unity.com/en-us/engine/6000.3/manual/animation-section/animation-mecanim/animation-animator-controller
- Addressables: https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-addressables
- Test Framework: https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/test-framework-introduction/getting-started/edit-mode-vs-play-mode-tests
- URP setup & shader migration: https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/introduction/installing-and-configuring-urp/creating-a-urp-project/install-urpinto-aproject
- Render pipeline asset/quality overrides: https://docs.unity3d.com/6000.1/Documentation/Manual/srp-setting-render-pipeline-asset.html
- Unity CLI/Pipeline (экспериментальный): https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package

## 3. URP migration trial — разрешён после фиксации пилота

- Отдельная ветка, сохранить исходную сцену и тестовый профиль FPS/CPU.
- Добавить совместимую для 6000.3 ветку URP, создать URP Asset (Universal Renderer) и привязать Graphics/Quality.
- Переписать тестовый Built-in shader так, чтобы он отображал прежний vertex-color результат; не оставлять розовые материалы.
- Повторить EditMode/PlayMode, player build, screenshot, измерение FPS и RAM; сравнить baseline до/после.
- Если не проходит — вернуть ветку в рабочее состояние; не принимать миграцию.
- Это **техническое** утверждение URP, не утверждение художественного качества окружения.

## 4. Unity Pipeline: NOT REQUIRED

CLI 1.0.0-beta.13 уже способен запускать batchmode/test/build; `com.unity.pipeline` нужен для интерактивных команд запущенному Editor. Экспериментальный пакет можно рассмотреть после проверки конкретной потребности, в отдельной ветке и с согласия продюсера. Не включать в основной проект автоматически.

## 5. Запрет на повторение движка Plasma

Не переносить Direct3D9 renderer, собственный оконный слой, XAudio2, Plasma UI и управление потоками MSVC как самостоятельные подсистемы CubeGame. Подменять **механические принципы**, а не повторять устаревшую платформенную реализацию. Исключение только по обоснованной, измеренной необходимости.
