# treegen: API и словарь кластеров

## Локальная рамка сектора

```
                 keyN (0,-560)
                   N (0,-500)
      Lv (-360,-360)          Rv (360,-360) — keyR (400,-400)
          L2 (-220,-220)   R2 (220,-220)
              L1 (-120,-120) R1 (120,-120)
                     start (0,0)
```

`world = start + rot90(mirror_x(local), k)`. Все аргументы-точки передаются в локальных координатах. Орбитальные углы — тоже локальные, пересчёт при повороте и зеркале делает `Sector`.

## API `Sector` (объект `q` в функции раскладки)

| Вызов | Что делает |
|---|---|
| `q.free(name, type, (x, y))` | Свободная нода. `type`: `SMALL`, `NOTABLE`, `KEY` |
| `q.cluster(name, center, (r0, r1, ...), color, shared=False)` | Кластер с орбитами. `shared=True` — один на всё дерево (центр у старта) |
| `q.orbit(name, type, cid, angle, oi=0)` | Нода на орбите `oi`. `existing=id` сажает на орбиту уже существующую ноду, если она не сдвигается |
| `q.ring(prefix, cid, angles, types, oi=0, closed=False)` | Цепочка нод по орбите, соседние соединены дугами |
| `q.link(a, b)` | Прямая связь |
| `q.link(a, b, c1, c2)` | Кубическая Безье с абсолютными контрольными точками (`c1` у `a`) |
| `q.curve_chain(prefix, a, b, c1, c2, ts, types)` | Новые ноды на одной кубике `a→b` в параметрах `ts`, гладко |
| `q.spline([ids...], t_start, t_end, tension=1)` | Гладкий путь через существующие ноды, касательные на концах |
| `q.arc_link(a, b, cid, oi=0)` | Безье точно по дуге орбиты между нодами, лежащими на этой окружности, но не состоящими в ней |
| `q.tangent(angle, direction=1)` | Касательная орбиты в локальном угле (+1 — по часовой на экране) |
| `q.toward(cid, node)` | Угол от центра кластера на ноду — для радиального входа |
| `q.at((x, y))` | ID существующей ноды в локальной точке |
| `q.pos(id)`, `q.center(cid)` | Локальная позиция ноды / центра кластера |
| `q.k`, `q.mirror`, `q.variant` | Номер четверти, зеркальная ли половина, `k % 2` |

`Tree.validate()` проверяет связность от старта, взаимность связей, целость скелета, перекрытие нод (радиусы: малая 14, средняя 20, кейстоун 26, плюс зазор 10), линии сквозь ноды, пересечения линий и хорды на орбитах. `main(...)` из `treegen` даёт стандартный CLI: превью, отчёт, `--write`, `--label`, `--force`.

Для смешанной симметрии передай в `main`/`build` список секторов, например `[(0, False), (0, True), (2, False), (2, True), (1, False), (3, True)]`. Внутри раскладки ветвись по `q.k` и `q.mirror`. В режиме `NONE` можно создавать секторы вручную: `Sector(q.t, k, mirror)`.

## Словарь кластеров

Размеры рассчитаны на сектор; радиус орбиты 40–100. Все примеры внутри функции раскладки.

**Развилка** (Blade Fork): один радиальный вход, две ветви, две средние рядом.

```python
c = q.cluster('Fork', (0, -300), (60,))
left = q.ring('fl', c, (90, 150, 198, 244), (SMALL, SMALL, SMALL, NOTABLE))
right = q.ring('fr', c, (30, 342, 296), (SMALL, SMALL, NOTABLE))
q.link(left[0], right[0]); q.link(hub, left[0])            # hub лежит на оси (0, y)
```

**Глаз**: замкнутое кольцо и средняя в центре.

```python
c = q.cluster('Eye', (0, -300), (58,))
ring = q.ring('eye', c, (90, 150, 210, 270, 330, 30), (SMALL,) * 6, closed=True)
q.link(ring[3], q.free('eyeCore', NOTABLE, (0, -300)))
```

**Улитка**: хвост входит по касательной, закручивается и кончается средней в центре.

```python
c = q.cluster('Snail', center, (46,))
s = q.ring('sn', c, (249, 320, 30, 100), (SMALL,) * 4)
q.link(s[-1], q.free('snCore', NOTABLE, center))
q.spline([corner, tail, s[0]], t_start=edge_dir, t_end=q.tangent(249))
```

**Проход с тупиком** (Wedge): вход и выход радиальные, остаток дуги — к средней.

```python
c = q.cluster('Wedge', (225, -335), (50,))
a_in, a_out = q.toward(c, q.R2), q.toward(c, other)
w = q.ring('w', c, (a_in, a_out, a_out + 72, a_out + 144), (SMALL,) * 3 + (NOTABLE,))
q.link(q.R2, w[0]); q.link(other, w[1])
```

**Центр-дыра**: орбита через первые ноды лучей и внутренняя орбита со средними.

```python
hole = q.cluster('Hole', (0, 0), (95, 170), shared=True)
q.orbit(None, None, hole, 225, oi=1, existing=q.L1)
q.orbit(None, None, hole, 315, oi=1, existing=q.R1)
hub = q.orbit('hub', SMALL, hole, 270, oi=1)
q.link(q.L1, hub); q.link(hub, q.R1)
q.link(hub, q.orbit('inner', NOTABLE, hole, 270, oi=0))
```

**Концентрический**: две орбиты одного центра, переход по радиусу, внешняя — частичная дуга.

```python
c = q.cluster('Rings', center, (40, 80))
outer = q.ring('ro', c, (200, 250, 300, 350), (SMALL,) * 4, oi=1)
inner = q.ring('ri', c, (350, 60, 130), (SMALL, SMALL, NOTABLE), oi=0)
q.link(outer[-1], inner[0])                                  # один угол, линия по радиусу
```

**Восьмёрка**: два касающихся круга. Нода в точке касания сидит на орбите A; к кругу B её ведёт `arc_link` — Безье точно по дуге B.

```python
a = q.cluster('Eight A', (cx - 50, cy), (50,)); b = q.cluster('Eight B', (cx + 50, cy), (50,))
ra = q.ring('ea', a, (0, 290, 220, 150, 80), (SMALL,) * 4 + (NOTABLE,))   # ra[0] — точка касания
rb = q.ring('eb', b, (250, 320, 30, 100), (SMALL,) * 3 + (NOTABLE,))
q.arc_link(ra[0], rb[0], b)
```

**Двойной мост** (как DoubleBridge у воина): две дуги между общими концами и прямая перемычка через центр. `ring` всегда создаёт новые ноды, поэтому общие концы собирай через `q.orbit` и `q.link`.

```python
c = q.cluster('Bridge', center, (60,))
a, b = q.orbit('brA', SMALL, c, 180), q.orbit('brB', SMALL, c, 0)
top, bottom = q.orbit('brTop', SMALL, c, 270), q.orbit('brBot', NOTABLE, c, 90)
for x, y in ((a, top), (top, b), (a, bottom), (bottom, b)): q.link(x, y)
q.link(a, b)   # 180° на одной орбите игра рисует прямой хордой — валидатор пишет это как note
```

**Вписанный треугольник** (как у мага): свободные ноды внутри круга, соединены прямыми, круг — только рамка кластера.

```python
c = q.cluster('Tri', center, (70,))
v = [q.free('tri%d' % i, t, polar(center, 55, a)) for i, (a, t) in enumerate(((270, SMALL), (30, SMALL), (150, NOTABLE)))]
q.link(v[0], v[1]); q.link(v[1], v[2])
```

**Хвост вдоль ребра**: угол восьмиугольника, 1–2 малые, вход в кластер по касательной.

```python
edge_dir = norm((360, -140)); edge_in = (0.363, 0.933)       # ребро Lv→N и нормаль внутрь
t = q.free('tail', SMALL, add(add((-360, -360), mul(edge_dir, 95)), mul(edge_in, 20)))
q.spline([q.Lv, t, entry_node], t_start=edge_dir, t_end=q.tangent(entry_angle))
```

**Ребро фигуры** (лезвие, крыло, стрелка): одна кубика, ноды точно на ней.

```python
lead = q.curve_chain('lead', q.L2, q.N, (-150, -260), (-50, -380), (0.3, 0.55, 0.8), (SMALL,) * 3)
```

## Подбор контрольных точек кубики

Середина кубики: `(P0 + 3·c1 + 3·c2 + P3) / 8`. Выгиб относительно хорды считай по ней. Касательная на старте — `c1 − P0`, на конце — `P3 − c2`. Чтобы две фигуры сходились остриём (кончик лезвия, клык), подведи обе кубики к общему углу с касательными под 30–60° друг к другу.
