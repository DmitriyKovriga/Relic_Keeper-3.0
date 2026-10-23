"""Revision 4 content: homogeneous feeders and geographically separated goals.

Only review data. No Unity assets are created or changed by this module.
"""

def populate(put, m, inc, more, less, res, transport_ids):
    groups=[]
    def group(key, location, ids, ru, en, family, mods, notable_ids='', defense=()):
        put(ids,ru,en,family,mods,defense)
        groups.append({'id':key,'locationRu':location,'smallIds':ids.split(),
                       'notableIds':notable_ids.split(),'family':family})
    put('start','Путь охотника','Hunter Origin','start',[],note='Free automatic start; no modifiers.')
    put(' '.join(transport_ids),'Транспорт','Travel','transport',[m('MaxHealth',3),m('Evasion',5)],
        note='Reuse transportRogue. Exactly +3 HP Flat / +5 Evasion Flat. Not a dedicated defense option.')

    # Near-start defensive loops: all four small nodes in each loop are identical.
    group('life_nw','Северо-запад, внутренний круг','n30 n31 n28 n74','Запас жизни','Life Reserve','defense',[m('MaxHealth',25)],'n37',['life'])
    put('n37','Второе дыхание','Second Wind','defense',[inc('MaxHealth',15),m('HealthRegen',1)],['life','sustain'])
    group('evasion_ne','Северо-восток, внутренний круг','n33 n34 n35 n73','Уклонение','Evasion Training','defense',[m('Evasion',50),inc('Evasion',10)],'n38',['evasion'])
    put('n38','Призрачная поступь','Ghost Step','defense',[m('Evasion',100),inc('Evasion',30)],['evasion'])
    group('recovery_w','Запад, малый круг','n44 n45 n170','Восстановление','Recovery Training','defense',[m('HealthRegen',0.5)],'n171',['sustain'])
    put('n171','Крепкий организм','Hardy Constitution','defense',[inc('MaxHealth',15),m('HealthRegen',1)],['life','sustain'])

    # Speed is split between NW and SE. No speed-specific small exceptions.
    group('speed_nw','Северо-запад, верхняя дуга','n69 n52 n53 n54','Скорость атаки','Attack Tempo','speed',[inc('AttackSpeed',7)],'n55')
    put('n55','Шквал','Flurry','speed',[inc('AttackSpeed',18),m('MoveSpeed',4)])
    group('speed_se','Юго-восток, нижний малый круг','n174 n175 n176 n177','Скорость атаки','Attack Tempo','speed',[inc('AttackSpeed',7)],'n178')
    put('n178','Неослабевающий темп','Unbroken Tempo','speed',[inc('AttackSpeed',18),m('ManaOnHit',1)])

    # One repeated physical feeder supports poison and direct hits on the west arc.
    group('physical_poison_w','Запад, верхний внешний круг','n89 n92 n91 n94','Физический урон','Honed Edge','physical',[inc('DamagePhysical',10)],'n87 n96')
    put('n87','Отравленная сталь','Poisoned Steel','poison',[m('PoisonChance',20),m('PoisonDamage',25)])
    put('n96','Мастер клинка','Blade Master','physical',[inc('DamagePhysical',30),m('HealthOnHit',2)],['sustain'])
    put('n67','Живучий охотник','Hardy Hunter','defense',[inc('MaxHealth',15),m('HealthRegen',1)],['life','sustain'])
    put('n60','Безошибочный удар','Unerring Strike','crit',[inc('CritChance',35),m('CritMultiplier',20)])
    put('n63','Сокрушительная рана','Crushing Wound','physical',[inc('DamagePhysical',30),m('HealthOnHit',1)],['sustain'])
    put('n172','Закалка стихий','Elemental Conditioning','defense',res(10),['resist'])
    put('n173','Холодная выдержка','Cold Resolve','defense',[inc('MaxHealth',15)]+res(6),['life','resist'])

    # North: cold conversion and ranged damage, rather than two matching crit loops.
    group('cold_n','Север, левый верхний круг','n154 n156 n158','Стихийный урон','Elemental Training','elemental',[inc('ElementalDamage',10)],'n160')
    put('n160','Холодная сталь','Cold Steel','elemental',[m('PhysicalToCold',40),inc('DamageCold',25)])
    group('range_n','Север, правый верхний круг','n155 n157 n159','Дальний урон','Ranged Training','range',[inc('RangeDamage',10)],'n161')
    put('n161','Дальняя охота','Distant Hunt','range',[inc('RangeDamage',25),m('ProjectileCount',1)])
    group('resist_n','Север, центральный тупик','n72','Сопротивления','Elemental Guard','defense',res(5),'n71',['resist'])
    put('n71','Готовность к стихиям','Elemental Readiness','defense',res(12),['resist'])

    # East: lightning conversion; a life ring and physical mitigation fork nearby.
    put('n129','Боевой запас','Combat Reserve','defense',[m('MaxHealth',25),inc('MaxHealth',15)],['life'])
    group('life_e','Восток, средний круг','n130 n131 n132 n133','Запас жизни','Life Reserve','defense',[m('MaxHealth',25)],'n129',['life'])
    group('evasion_e','Северо-восток, внешний треугольник','n151 n162','Уклонение','Evasion Training','defense',[m('Evasion',50),inc('Evasion',10)],'n150 n152',['evasion'])
    put('n150','Защитный манёвр','Defensive Maneuver','defense',[m('Evasion',100),inc('Evasion',30)],['evasion'])
    put('n152','Скользящая защита','Glancing Guard','defense',[inc('Evasion',30),m('PhysicalResist',4),m('Armor',50)],['evasion','physical_resist','armor'])
    group('lightning_e','Восток, нижний внешний круг','n125 n126 n127 n135','Стихийный урон','Elemental Training','elemental',[inc('ElementalDamage',10)],'n128')
    put('n128','Заряженная сталь','Charged Steel','elemental',[m('PhysicalToLightning',40),inc('DamageLightning',25)])

    # South-west: fire conversion; the neighboring double ring is life + crit.
    group('evasion_sw','Юго-запад, внутренний треугольник','n116 n114 n113','Уклонение','Evasion Training','defense',[m('Evasion',50),inc('Evasion',10)],'', ['evasion'])
    group('fire_sw','Юго-запад, нижний внешний круг','n165 n166 n167','Стихийный урон','Elemental Training','elemental',[inc('ElementalDamage',10)],'n168')
    put('n168','Обожжённая сталь','Scorched Steel','elemental',[m('PhysicalToFire',40),inc('DamageFire',25)])
    group('life_sw','Юго-запад, внешняя орбита двойного круга','n140 n144 n143 n142','Запас жизни','Life Reserve','defense',[m('MaxHealth',25)],'n141',['life'])
    put('n141','Закалённое тело','Tempered Body','defense',[m('MaxHealth',50),inc('MaxHealth',20)],['life'])
    group('crit_sw','Юго-запад, внутренняя орбита двойного круга','n146 n145 n147 n148','Шанс крита','Critical Training','crit',[inc('CritChance',15)],'n149')
    put('n149','Смертельная точность','Deadly Precision','crit',[inc('CritChance',35),m('CritMultiplier',25)])

    # South: ranged outer orbit and poison inner orbit. No conversion remains here.
    group('range_s','Юг, внешняя орбита двойного круга','n110 n109 n108 n107 n106','Дальний урон','Ranged Training','range',[inc('RangeDamage',10)],'n117')
    put('n117','Возвратная кромка','Returning Edge','range',[inc('RangeDamage',25),m('ReturningProjectileDamage',20)])
    group('poison_s','Юг, внутренняя орбита двойного круга','n119 n120 n121','Урон яда','Venom Training','poison',[m('PoisonDamage',12)],'n118')
    put('n118','Мастер ядов','Venom Master','poison',[m('PoisonDamage',35),m('PoisonChance',15),inc('PoisonDuration',25)])
    group('evasion_s','Юг, центральная развилка','n100 n102 n103 n104','Уклонение','Evasion Training','defense',[m('Evasion',50),inc('Evasion',10)],'n75',['evasion'])
    put('n75','Право на ошибку','Room for Error','defense',[inc('Evasion',30),inc('MaxHealth',15)],['evasion','life'])
    group('resist_se','Юго-восток, внутренний треугольник','n136 n138 n139','Сопротивления','Elemental Guard','defense',res(5),'',['resist'])
    group('physical_w_gate','Запад, подход к внешнему ключу','n82 n83 n84','Физический урон','Honed Edge','physical',[inc('DamagePhysical',10)],'n85')

    # Existing keystone concepts relocated to break the previous mono-archetype sectors.
    put('n7','Дальняя охота: ставка','Distant Hunt: Wager','range',[more('RangeDamage',15),m('ProjectileCount',1),less('MeleeDamage',20)])
    put('n8','Глубокий разрез','Deep Cut','physical',[more('DamagePhysical',25),less('AttackSpeed',10)])
    put('n9','Летальный токсин','Lethal Toxin','poison',[m('PoisonDamageMult',5),inc('PoisonDuration',25),less('HealthOnHit',50)])
    put('n2','Перелом стихий','Elemental Turning Point','elemental',[more('ElementalDamage',25),less('DamagePhysical',15)])
    put('n3','Танец лезвий','Dance of Blades','hybrid',[inc('AttackSpeed',10),less('Armor',20)],
        scaling=[{'SourceStat':'Evasion','SourceAmountPerStep':1000,'UseWholeSteps':True,
          'TargetStat':'CritMultiplier','TargetValuePerStep':5,'TargetModifierType':'Flat','TargetModifierTypeValue':100}])
    put('n4','Безупречная точность','Perfect Precision','crit',[more('CritChance',40),m('CritMultiplier',30),less('AttackSpeed',10)])
    put('n5','Пограничный клинок','Borderland Blade','hybrid',[more('DamagePhysical',15),more('ElementalDamage',15),less('MaxMana',15)])
    put('n85','Безостановочная охота','Relentless Hunt','speed',[more('AttackSpeed',20),less('DamagePhysical',15)])
    return groups
