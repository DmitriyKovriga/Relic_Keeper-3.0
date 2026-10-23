# Rogue v4: dispersed clusters and repeated small nodes

Each feeder group repeats one small template. Read one small node, then its notable(s).
72 specialized small nodes use 10 shared packages. All node geometry/types remain unchanged.

| Group | Location | Identical small IDs | Notable / keystone IDs | Repeated small effects |
|---|---|---|---|---|
| life_nw | Северо-запад, внутренний круг | n30, n31, n28, n74 | n37 | +25 — максимальное здоровье |
| evasion_ne | Северо-восток, внутренний круг | n33, n34, n35, n73 | n38 | +50 — рейтинг уклонения; 10% увеличение — рейтинг уклонения |
| recovery_w | Запад, малый круг | n44, n45, n170 | n171 | +0.5 — восстановление здоровья / с |
| speed_nw | Северо-запад, верхняя дуга | n69, n52, n53, n54 | n55 | 7% увеличение — скорость атаки |
| speed_se | Юго-восток, нижний малый круг | n174, n175, n176, n177 | n178 | 7% увеличение — скорость атаки |
| physical_poison_w | Запад, верхний внешний круг | n89, n92, n91, n94 | n87, n96 | 10% увеличение — физический урон |
| cold_n | Север, левый верхний круг | n154, n156, n158 | n160 | 10% увеличение — стихийный урон |
| range_n | Север, правый верхний круг | n155, n157, n159 | n161 | 10% увеличение — урон с тегом Range |
| resist_n | Север, центральный тупик | n72 | n71 | +5 п.п. — сопротивление огню; +5 п.п. — сопротивление холоду; +5 п.п. — сопротивление молнии |
| life_e | Восток, средний круг | n130, n131, n132, n133 | n129 | +25 — максимальное здоровье |
| evasion_e | Северо-восток, внешний треугольник | n151, n162 | n150, n152 | +50 — рейтинг уклонения; 10% увеличение — рейтинг уклонения |
| lightning_e | Восток, нижний внешний круг | n125, n126, n127, n135 | n128 | 10% увеличение — стихийный урон |
| evasion_sw | Юго-запад, внутренний треугольник | n116, n114, n113 |  | +50 — рейтинг уклонения; 10% увеличение — рейтинг уклонения |
| fire_sw | Юго-запад, нижний внешний круг | n165, n166, n167 | n168 | 10% увеличение — стихийный урон |
| life_sw | Юго-запад, внешняя орбита двойного круга | n140, n144, n143, n142 | n141 | +25 — максимальное здоровье |
| crit_sw | Юго-запад, внутренняя орбита двойного круга | n146, n145, n147, n148 | n149 | 15% увеличение — шанс критического удара |
| range_s | Юг, внешняя орбита двойного круга | n110, n109, n108, n107, n106 | n117 | 10% увеличение — урон с тегом Range |
| poison_s | Юг, внутренняя орбита двойного круга | n119, n120, n121 | n118 | +12 п.п. — увеличение урона яда |
| evasion_s | Юг, центральная развилка | n100, n102, n103, n104 | n75 | +50 — рейтинг уклонения; 10% увеличение — рейтинг уклонения |
| resist_se | Юго-восток, внутренний треугольник | n136, n138, n139 |  | +5 п.п. — сопротивление огню; +5 п.п. — сопротивление холоду; +5 п.п. — сопротивление молнии |
| physical_w_gate | Запад, подход к внешнему ключу | n82, n83, n84 | n85 | 10% увеличение — физический урон |

## Exact minimum allocation costs for all listed notable goals

| Direction | Separated goals | Minimum points, no keystones | Minimum points, all edges |
|---|---|---:|---:|
| speed | n55, n178 | 17 | 17 |
| crit | n60, n149 | 13 | 13 |
| poison | n87, n118 | 13 | 13 |
| range | n161, n117 | 18 | 17 |
| physical | n63, n96 | 14 | 14 |
| elemental | n160, n128, n168 | 23 | 18 |

Costs include free start and shared prefixes, not a finished build. The cheaper outer-ring route includes all keystone drawbacks. Full in-tree conversion requires all three 40% notables: n160 cold (north), n128 lightning (east), n168 fire (south-west). Skills and gear can reduce the required tree investment.
