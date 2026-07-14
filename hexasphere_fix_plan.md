# План исправлений Hexasphere

Разбито на этапы по приоритету. Каждый пункт: **проблема → где → почему это баг/проблема → как исправить**.

---

## Этап 1. Критичные баги (ломают корректность)

### 1.1 Border-подсветка: рёбра принадлежат только одному из двух тайлов
**Файл:** `PlanetBorderRenderer.cs`, метод `BuildStaticBorders`

**Проблема:**
```csharp
if (seenMidpoints.Add(snappedMid))
{
    vertPositions.Add(p1 * 1.0001f);
    vertPositions.Add(p2 * 1.0001f);
    uv2.Add(tileUV);   // <-- индекс тайла, который первым дошёл до этого ребра
    uv2.Add(tileUV);
}
```
Каждое ребро на сфере общее для двух соседних тайлов, но дедуп по midpoint оставляет только одну копию и помечает её индексом того тайла, который обработан раньше (порядок обхода `i` от 0 до `tileCount`). При выделении тайла (`selected_idx` в шейдере) подсветится периметр только частично — те рёбра, что "достались" именно ему.

**Исправление — хранить оба индекса-владельца на ребро:**

1. В `PlanetBorderRenderer` завести словарь `Dictionary<Vector3, int> midpointOwner`, при первой встрече ребра запоминать `tileIndex` в `UV2.x`, а при повторной встрече (сосед) записывать второй индекс в `UV2.y` (или в отдельный `Color`/`UV` канал, раз `UV2` уже занят под первый индекс).
2. Пример реализации:
```csharp
var edgeFirstOwner = new Dictionary<Vector3, int>();
var uv2List = new List<Vector2>();

int idx = 0;
for (int i = 0; i < tileCount; i++)
{
    int count = tileLineCounts[i];
    for (int j = 0; j < count; j += 2)
    {
        Vector3 p1 = positions[idx + j];
        Vector3 p2 = positions[idx + j + 1];
        Vector3 mid = (p1 + p2) * 0.5f;
        var snapped = SnapToGrid(mid, 0.001f);

        if (edgeFirstOwner.TryGetValue(snapped, out int firstOwner))
        {
            // второй тайл этого ребра — обновляем уже добавленные вершины
            int vertIndex = edgeVertexIndex[snapped]; // индекс первой пары вершин
            uv2List[vertIndex]     = new Vector2(firstOwner, i);
            uv2List[vertIndex + 1] = new Vector2(firstOwner, i);
        }
        else
        {
            edgeFirstOwner[snapped] = i;
            edgeVertexIndex[snapped] = vertPositions.Count;
            vertPositions.Add(p1 * 1.0001f);
            vertPositions.Add(p2 * 1.0001f);
            uv2List.Add(new Vector2(i, -1)); // второй owner пока неизвестен
            uv2List.Add(new Vector2(i, -1));
        }
    }
    idx += count;
}
```
3. В `hexasphere_borders.gdshader` проверять оба индекса:
```glsl
int idxA = int(round(UV2.x));
int idxB = int(round(UV2.y));

if (idxA == selected_idx || idxB == selected_idx) {
    COLOR = vec4(1.0, 1.0, 1.0, 1.0);
} else {
    COLOR = border_color;
}
```
4. Граничные тайлы у "полюсов"/швов, где ребро принадлежит только одному тайлу (нет соседа) — оставить `idxB = -1`, шейдер это корректно проигнорирует.

**Приоритет:** высокий — визуальный баг, будет заметен сразу при тестировании выделения.

---

### 1.2 Use-after-null для `Hexasphere` в границах
**Файлы:** `HexasphereNode.cs` (`OnShaderReady`), `HexasphereVisualController.cs` (`Draw`), `PlanetBorderRenderer.cs` (`UpdateBorders`)

**Проблема:**
```csharp
// HexasphereNode.OnShaderReady
VisualController.Draw(_cellDatas);
VisualController.DisposeHexasphere();   // Hexasphere -> null
_planetReady = true;
```
```csharp
// HexasphereVisualController.Draw(...)
if (_isBorderVisible)
    _borderRenderer.UpdateBorders(Hexasphere, cellDatas, selectedIdx); // Hexasphere уже null при последующих вызовах
```
Пока `UpdateBorders` не использует свой первый параметр, ошибки нет — но это тикающая бомба: любое будущее изменение `UpdateBorders`, которое реально обратится к `hexasphere`, даст `NullReferenceException` при первом же клике/наведении после инициализации.

**Исправление:**
- Убрать параметр `NativeHexasphere` из `UpdateBorders`, раз он не нужен:
```csharp
public void UpdateBorders(ICellData[] cellDatas, int selectedIdx = -1)
{
    _borderMaterial?.SetShaderParameter("selected_idx", selectedIdx);
}
```
и убрать `Hexasphere` из вызова в `Draw()`.
- Если в будущем понадобится доступ к нативным данным при обновлении границ — دержать нужные данные (например, border data) закешированными в `PlanetBorderRenderer` при `BuildStaticBorders`, а не полагаться на то, что `NativeHexasphere` будет жив.

**Приоритет:** высокий — не баг сейчас, но серьёзный источник будущего краша, дешево исправить сразу.

---

### 1.3 Фоновая генерация без обработки исключений и без проверки жизни ноды
**Файл:** `HexasphereNode.cs`, `_Ready` / `GenerateInBackground` / `FinalizePlanet`

**Проблема:**
```csharp
Task.Run(() => GenerateInBackground(hexasphere));
```
- Исключение в `hexasphere.Generate(...)` или в извлечении данных потеряется молча (fire-and-forget) — планета просто не появится, без единой строчки в логе.
- Если нода будет удалена из дерева/сцена сменится до срабатывания `CallDeferred(FinalizePlanet)`, `FinalizePlanet` всё равно попытается выполниться и обратится к, возможно, уже невалидной ноде.

**Исправление:**
```csharp
public override void _Ready()
{
    ...
    var hexasphere = new NativeHexasphere();
    Task.Run(() =>
    {
        try
        {
            GenerateInBackground(hexasphere);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[HexasphereNode] Ошибка генерации: {e}");
        }
    });
}

virtual protected void FinalizePlanet()
{
    if (!IsInsideTree()) return; // нода могла быть удалена, пока ждали CallDeferred

    _cellDatas = _pendingCellDatas;
    ...
}
```
Дополнительно — можно завести `bool _isDisposing` флаг и проверять его в начале `FinalizePlanet`, если планируется динамическое пересоздание планеты (см. п. 3.4).

**Приоритет:** высокий — сейчас ошибки в генерации абсолютно непрозрачны для отладки.

---

## Этап 2. Производительность

### 2.1 Полная перерисовка цветовой текстуры на каждое движение мыши
**Файлы:** `HexasphereNode.cs` (`_UnhandledInput`), `HexasphereVisualController.cs` (`Draw`)

**Проблема:**
`Draw(cellDatas, ...)` вызывается при каждом изменении `_hoveredTileIndex`/`_selectedTileIndex`, а внутри:
```csharp
for (int i = 0; i < safeLength; i++) { ... _tileColorImage.SetPixel(px, py, c); }
_tileColorTexture.Update(_tileColorImage);
```
проходит **весь массив тайлов** и обновляет текстуру целиком — на каждое малейшее движение мыши, при том что реально изменились только 1-2 шейдерных uniform-а (`selected_idx`, `hover_idx`). При тысячах тайлов это заметная лишняя нагрузка каждый кадр.

**Исправление — разделить метод на два:**
```csharp
// Вызывать только когда реально меняются цвета тайлов (геймплейный эффект, покраска и т.п.)
virtual public void DrawColors(ICellData[] cellDatas)
{
    if (_tileColorImage == null || cellDatas == null || cellDatas.Length == 0) return;

    int safeLength = Mathf.Min(cellDatas.Length, _tileCount);
    for (int i = 0; i < safeLength; i++)
    {
        Color c = GetColor(cellDatas[i]);
        int px = i % _texWidth;
        int py = i / _texWidth;
        _tileColorImage.SetPixel(px, py, c);
    }
    _tileColorTexture.Update(_tileColorImage);
}

// Вызывать на каждый hover/click — только шейдерные параметры, без похода в Image
virtual public void SetSelection(Color? selectedColor, int selectedIdx, Color? hoverColor, int hoverIdx)
{
    if (selectedColor != null)
    {
        _planetMaterial?.SetShaderParameter("selected_color", selectedColor.Value);
        _planetMaterial?.SetShaderParameter("selected_idx", selectedIdx);
    }
    if (hoverColor != null)
    {
        _planetMaterial?.SetShaderParameter("hover_color", hoverColor.Value);
        _planetMaterial?.SetShaderParameter("hover_idx", hoverIdx);
    }
    if (_isBorderVisible)
        _borderRenderer.UpdateBorders(selectedIdx); // см. п. 1.2 про сигнатуру
}
```
В `HexasphereNode._UnhandledInput` заменить вызовы `Draw(_cellDatas, ...)` на `VisualController.SetSelection(...)` — без передачи `_cellDatas` вообще, раз данные тайлов не менялись.

**Приоритет:** высокий по перфомансу — самая горячая точка кода (вызывается на каждое движение мыши).

---

### 2.2 Медленный путь заполнения `Image` через `SetPixel`
**Файл:** `HexasphereVisualController.cs`, `Draw`/`DrawColors`

**Проблема:** `Image.SetPixel` в цикле — по одному пикселю через managed→native границу, довольно медленно для больших текстур (тысячи тайлов при высоком `SubDivision`).

**Исправление:** собрать байты в `byte[]` (`RGBA8`, 4 байта на пиксель) и один раз создать/обновить изображение из массива:
```csharp
var bytes = new byte[_texWidth * _texHeight * 4];
for (int i = 0; i < safeLength; i++)
{
    Color c = GetColor(cellDatas[i]);
    int offset = i * 4;
    bytes[offset + 0] = (byte)(c.R * 255);
    bytes[offset + 1] = (byte)(c.G * 255);
    bytes[offset + 2] = (byte)(c.B * 255);
    bytes[offset + 3] = (byte)(c.A * 255);
}
var img = Image.CreateFromData(_texWidth, _texHeight, false, Image.Format.Rgba8, bytes);
_tileColorTexture.Update(img);
```
Это особенно оправдано, если п. 2.1 сделан правильно и `DrawColors` вызывается редко (не каждый кадр) — тогда выигрыш будет в основном при первичной покраске большого количества тайлов разом (например, процедурная генерация биомов).

**Приоритет:** средний — актуально в первую очередь при больших `SubDivision`.

---

### 2.3 `ConcavePolygonShape3D` для рейкаста не нужен — есть точный CPU-lookup
**Файлы:** `HexasphereVisualController.cs` (`CreateGlobalCollider`), `HexasphereNode.cs` (`TryRaycastToTile`)

**Проблема:** для определения кликнутого тайла используется физический рейкаст в `StaticBody3D` с `ConcavePolygonShape3D`, построенным из всего меша планеты. Это:
- дорого строится при инициализации (`concaveShape.Data = _planetMeshInstance.Mesh.GetFaces()` — вся геометрия);
- тратит память и нагружает физический движок постоянно, хотя нужен только сам факт пересечения луча со сферой;
- избыточно, поскольку тайл всё равно определяется через `FindTileIndexByDirection` (CPU spatial hash) — физика используется только чтобы получить точку на сфере.

**Исправление:** заменить физический рейкаст на аналитическое пересечение луча со сферой, убрать коллайдер целиком.

В `HexasphereVisualController.cs` — удалить `CreateGlobalCollider()` и вызов в `ApplyGenerated`.

В `HexasphereNode.cs`:
```csharp
private bool TryRaycastToTile(out Vector3 hitPosition, out int tileIndex)
{
    hitPosition = Vector3.Zero;
    tileIndex = -1;

    var camera = GetViewport().GetCamera3D();
    if (camera == null) return false;

    var mousePos = GetViewport().GetMousePosition();
    Vector3 origin = ToLocal(camera.ProjectRayOrigin(mousePos));
    Vector3 dir    = (ToLocal(camera.ProjectRayOrigin(mousePos) + camera.ProjectRayNormal(mousePos)) - origin).Normalized();

    if (!RaySphereIntersect(origin, dir, PlanetRadius, out hitPosition))
        return false;

    tileIndex = FindTileIndexByDirection(hitPosition.Normalized());
    return tileIndex >= 0;
}

private static bool RaySphereIntersect(Vector3 origin, Vector3 dir, float radius, out Vector3 hit)
{
    float b = origin.Dot(dir);
    float c = origin.Dot(origin) - radius * radius;
    float disc = b * b - c;
    if (disc < 0f) { hit = Vector3.Zero; return false; }

    float sq = Mathf.Sqrt(disc);
    float t1 = -b - sq;
    float t2 = -b + sq;
    float t = t1 >= 0f ? t1 : t2;
    if (t < 0f) { hit = Vector3.Zero; return false; }

    hit = origin + dir * t;
    return true;
}
```
Примечание: если планета не всегда в начале координат локального пространства, учтите трансформацию (`ToLocal`, как показано) — расчёт должен идти в системе координат, где центр сферы — origin.

**Приоритет:** средний-высокий — упрощает архитектуру и убирает целый физический объект без выгоды.

---

### 2.4 Фиксированный `BucketScale` не масштабируется под `SubDivision`
**Файл:** `HexasphereNode.cs`, `BuildSpatialIndex`/`Quantize`

**Проблема:** `BucketScale = 5f` — константа, не зависящая от количества тайлов. При высоком `SubDivision` (много мелких тайлов) один бакет может содержать десятки индексов → деградация поиска до почти линейного перебора. При низком `SubDivision` бакеты будут излишне мелкими относительно размера тайла, что не страшно, но неэффективно по памяти.

**Исправление:** сделать `BucketScale` полем экземпляра, вычисляемым от `tileCount`:
```csharp
private float _bucketScale = 5f;

virtual protected void BuildSpatialIndex(Vector3[] centers)
{
    _tileDirs = new Vector3[centers.Length];
    for (int i = 0; i < centers.Length; i++)
        _tileDirs[i] = centers[i].Normalized();

    // Чем больше тайлов, тем мельче должны быть бакеты, чтобы среднее
    // число тайлов на бакет оставалось примерно постоянным.
    _bucketScale = Mathf.Sqrt(centers.Length) * 0.35f;

    BuildSpatialBuckets();
}

private Vector3I Quantize(Vector3 v) => new Vector3I(
    (int)Mathf.Round(v.X * _bucketScale),
    (int)Mathf.Round(v.Y * _bucketScale),
    (int)Mathf.Round(v.Z * _bucketScale)
);
```
(убрать `static`/`const BucketScale`, сделать методы нестатичными). Коэффициент `0.35f` — отправная точка, стоит подобрать эмпирически под реальные `SubDivision`.

**Приоритет:** средний — не баг, но при больших планетах ощутимо влияет на отзывчивость клика/наведения.

---

## Этап 3. Корректность/архитектура (не критично, но стоит поправить)

### 3.1 `ROUGHNESS` — мёртвый параметр при `unshaded`
**Файл:** `hexasphere_colors.gdshader`

**Проблема:**
```glsl
render_mode unshaded;
...
void fragment() {
    ALBEDO    = COLOR.rgb;
    ROUGHNESS = roughness;   // не имеет эффекта — unshaded не считает освещение
}
```
Весь путь `HexasphereNode.Roughness` → `SetRoughness()` → `_planetMaterial.SetShaderParameter("roughness", ...)` сейчас не влияет на картинку.

**Исправление — выбрать одно из двух:**
- **А)** Если планируется полноценное освещение планеты — убрать `render_mode unshaded;` и настроить полноценный PBR-материал (тогда `roughness` действительно заработает, но COLOR-подход с UV2-индексами по-прежнему будет работать как вершинный альбедо).
- **Б)** Если планета всегда должна быть unshaded (стилизованный вид) — убрать параметр `Roughness` из `HexasphereNode`, `SetRoughness` из `HexasphereVisualController`, uniform `roughness` из шейдера — чтобы не вводить в заблуждение при чтении кода.

**Приоритет:** низкий-средний — не баг рендера, но мёртвый код/API.

---

### 3.2 Дублирование адресации текстуры в двух шейдерах
**Файлы:** `hexasphere_colors.gdshader`, `hexasphere_borders.gdshader`

**Проблема:** идентичный блок вычисления `u, v` из `idx` продублирован в обоих файлах. При изменении логики (например, если поменяется формат текстуры) легко забыть поправить оба места.

**Исправление:** вынести в общий инклюд `tile_uv.gdshaderinc`:
```glsl
// tile_uv.gdshaderinc
vec2 tile_uv(int idx, int tex_width, int tile_count) {
    float u = (float(idx % tex_width) + 0.5) / float(tex_width);
    float v = (float(idx / tex_width) + 0.5) / float((tile_count + tex_width - 1) / tex_width);
    return vec2(u, v);
}
```
и в обоих шейдерах:
```glsl
#include "res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/tile_uv.gdshaderinc"
...
vec2 uv = tile_uv(idx, tex_width, tile_count);
COLOR = texture(tile_colors, uv);
```
(поддержка `#include` есть в Godot 4.3+; если версия ниже — просто держите блок идентичным и синхронизируйте вручную с комментарием-напоминанием в обоих файлах).

**Приоритет:** низкий — предотвращение будущих багов рассинхронизации.

---

### 3.3 Жёстко зашитые пути к шейдерам
**Файлы:** `HexasphereVisualController.cs`, `PlanetBorderRenderer.cs`

**Проблема:**
```csharp
var shader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_colors.gdshader");
```
Переименование папки аддона молча всё сломает (в рантайме — `null` shader вместо ошибки компиляции).

**Исправление:** вынести шейдеры в `[Export]` на `HexasphereNode` и прокидывать их вниз:
```csharp
// HexasphereNode.cs
[ExportGroup("Shaders")]
[Export] public Shader ColorsShader;
[Export] public Shader BordersShader;
```
и передавать в `VisualController.ApplyGenerated(mesh, IsBordering, ColorsShader, BordersShader)` / далее в `PlanetBorderRenderer`. Это также упрощает кастомизацию шейдеров пользователями плагина без правки исходников.

**Приоритет:** низкий — удобство сопровождения.

---

### 3.4 Экспортируемые параметры не реактивны в рантайме
**Файл:** `HexasphereNode.cs`

**Проблема:** `BorderColor`, `Roughness` и т.п. — простые поля с `[Export]`. Изменение в инспекторе во время игры не долетает до шейдера без ручного вызова сеттеров, а `PlanetRadius`/`SubDivision`/`HexSize` вообще не имеют пути пересборки планеты в рантайме.

**Исправление (минимальное для цвета/roughness):**
```csharp
private Color _borderColor = Colors.White;
[Export] public Color BorderColor
{
    get => _borderColor;
    set { _borderColor = value; VisualController?.SetBorderColor(value); }
}
```
Аналогично для `Roughness`. Для геометрических параметров (`PlanetRadius`, `SubDivision`, `HexSize`) — либо явно задокументировать, что они применяются только при старте, либо добавить публичный метод `RegenerateAsync()`, который заново прогоняет `GenerateInBackground`/`FinalizePlanet` с очисткой предыдущих ресурсов (мешей, коллайдеров, spatial-индекса).

**Приоритет:** низкий — фича, не баг, но стоит явно решить и задокументировать поведение.

---

### 3.5 `NativeHexasphere` не проверяет наличие GDExtension-класса
**Файл:** `NativeHexasphere.cs`

**Проблема:**
```csharp
_native = ClassDB.Instantiate("NativeHexasphere").AsGodotObject();
```
Если нативная библиотека не подгружена — упадёт с невнятным исключением на `null.AsGodotObject()`.

**Исправление:**
```csharp
public NativeHexasphere()
{
    if (!ClassDB.ClassExists("NativeHexasphere"))
        throw new InvalidOperationException(
            "NativeHexasphere GDExtension не найден. Проверьте, что .gdextension подключён и собран для текущей платформы.");

    _native = ClassDB.Instantiate("NativeHexasphere").AsGodotObject();
}
```

**Приоритет:** низкий — про качество диагностики, не про функциональность.

---

## Рекомендуемый порядок работ

1. **Этап 1 целиком** (1.1 → 1.2 → 1.3) — исправить сначала, это баги, а не оптимизации.
2. **2.1** — разделение `Draw`/`SetSelection` — самый заметный перфоманс-выигрыш при минимальных изменениях.
3. **2.3** — убрать физический коллайдер, заменить аналитическим рейкастом — упрощает и ускоряет одновременно.
4. **2.2, 2.4** — точечные оптимизации, делать по мере роста `SubDivision` в реальных сценах.
5. **Этап 3** — по возможности, не блокирует функциональность, но упрощает сопровождение.

Если нужно — могу сразу выдать готовые изменённые версии файлов (`HexasphereVisualController.cs`, `PlanetBorderRenderer.cs`, `HexasphereNode.cs`, оба `.gdshader`) с применёнными правками из пунктов 1.1–2.3.
