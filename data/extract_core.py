import json, sys

with open(r'd:\hs\fireplace\fireplace\Fireplace.CSharp\data\cards_dbfid.json', 'r', encoding='utf-8') as f:
    cards = json.load(f)

TYPE_MAP = {4:'Minion', 5:'Spell', 7:'Weapon', 3:'Hero', 10:'HeroPower', 6:'Enchantment'}
CLS_MAP = {2:'Druid', 3:'Hunter', 4:'Mage', 5:'Paladin', 6:'Priest', 7:'Rogue', 8:'Shaman', 9:'Warlock', 10:'Warrior', 11:'DemonHunter', 12:'Neutral', 14:'DemonHunter'}
RAR_MAP = {1:'Common', 2:'Free', 3:'Rare', 4:'Epic', 5:'Legendary'}

rows = []
for dbf, c in cards.items():
    tags = c.get('tags', {})
    if tags.get('183') != 1637:
        continue
    cid = c.get('cardId', '')
    ct = TYPE_MAP.get(tags.get('202', 0), 'Other')
    cls = CLS_MAP.get(tags.get('199', 0), '?')
    cost = tags.get('48', 0)
    atk = tags.get('47', 0)
    hp = tags.get('45', 0)
    rar = RAR_MAP.get(tags.get('203', 0), '')
    coll = 'Y' if tags.get('321') == 1 else ''
    n185 = tags.get('185', {})
    nzh = n185.get('zhCN', '') if isinstance(n185, dict) else ''
    nen = n185.get('enUS', '') if isinstance(n185, dict) else ''
    n184 = tags.get('184', {})
    desc = n184.get('zhCN', n184.get('enUS', '')) if isinstance(n184, dict) else ''
    # Strip HTML tags from desc
    import re
    desc = re.sub(r'<[^>]+>', '', desc)
    desc = re.sub(r'\$(\d+)', r'\1', desc)
    mechs = ','.join(c.get('mechanics', [])) if 'mechanics' in c else ''
    rows.append((cls, ct, cost, atk, hp, cid, dbf, rar, coll, nzh, nen, desc, mechs))

rows.sort()

# Write TSV
with open(r'd:\hs\fireplace\fireplace\Fireplace.CSharp\data\core_cards.tsv', 'w', encoding='utf-8') as out:
    out.write('CardId\tDbfId\tType\tClass\tCost\tAtk\tHP\tRarity\tColl\tNameZH\tNameEN\tDesc\tMechanics\n')
    for cls, ct, cost, atk, hp, cid, dbf, rar, coll, nzh, nen, desc, mechs in rows:
        out.write(f'{cid}\t{dbf}\t{ct}\t{cls}\t{cost}\t{atk}\t{hp}\t{rar}\t{coll}\t{nzh}\t{nen}\t{desc}\t{mechs}\n')

ccnt = sum(1 for r in rows if r[8] == 'Y')
print(f'Total: {len(rows)} cards, Collectible: {ccnt}')

# Also generate nice markdown
from collections import defaultdict

def write_table(md, items, show_rarity=True):
    """Write a markdown table for a list of card tuples."""
    if show_rarity:
        md.write('| Cost | Atk | HP | CardId | 中文名 | 英文名 | 稀有度 | 类型 | 效果 | 机制 |\n')
        md.write('|------|-----|----|--------|--------|--------|--------|------|------|------|\n')
    else:
        md.write('| CardId | 中文名 | 英文名 | 类型 | 效果 |\n')
        md.write('|--------|--------|--------|------|------|\n')
    for cls2, ct, cost, atk, hp, cid, dbf, rar, coll, nzh, nen, desc, mechs in items:
        desc_short = desc[:60] + '...' if len(desc) > 60 else desc
        desc_short = desc_short.replace('\n', ' ').replace('\r', '')
        if show_rarity:
            hp_str = str(hp) if ct not in ('Spell', 'Enchantment') else '-'
            atk_str = str(atk) if ct not in ('Spell', 'Enchantment') else '-'
            md.write(f'| {cost} | {atk_str} | {hp_str} | {cid} | {nzh} | {nen} | {rar} | {ct} | {desc_short} | {mechs} |\n')
        else:
            md.write(f'| {cid} | {nzh} | {nen} | {ct} | {desc_short} |\n')

# Categorize all cards
collectible = [r for r in rows if r[8] == 'Y']
enchantments = [r for r in rows if r[8] != 'Y' and r[1] == 'Enchantment']
hero_powers = [r for r in rows if r[8] != 'Y' and r[1] == 'HeroPower']
tokens = [r for r in rows if r[8] != 'Y' and r[1] not in ('Enchantment', 'HeroPower')]

with open(r'd:\hs\fireplace\fireplace\Fireplace.CSharp\data\core_cards.md', 'w', encoding='utf-8') as md:
    md.write('# 核心卡牌清单 (Core Set, CardSet=1637)\n\n')
    md.write(f'| 分类 | 数量 |\n')
    md.write(f'|------|------|\n')
    md.write(f'| 可收集卡牌 | {len(collectible)} |\n')
    md.write(f'| 衍生物 (Tokens) | {len(tokens)} |\n')
    md.write(f'| 附魔 (Enchantments) | {len(enchantments)} |\n')
    md.write(f'| 英雄技能 (Hero Powers) | {len(hero_powers)} |\n')
    md.write(f'| **总计** | **{len(rows)}** |\n\n')
    
    # ── Section 1: Collectible by class ──
    md.write('---\n\n')
    md.write('# 一、可收集卡牌\n\n')
    
    by_class = defaultdict(list)
    for r in collectible:
        by_class[r[0]].append(r)
    
    for cls in sorted(by_class.keys()):
        cards_list = by_class[cls]
        md.write(f'## {cls} ({len(cards_list)} 张)\n\n')
        write_table(md, cards_list)
        md.write('\n')
    
    # ── Section 2: Tokens ──
    md.write('---\n\n')
    md.write(f'# 二、衍生物 Tokens ({len(tokens)} 张)\n\n')
    md.write('> 由可收集卡牌的效果召唤/生成的随从、武器等\n\n')
    if tokens:
        by_class_t = defaultdict(list)
        for r in tokens:
            by_class_t[r[0]].append(r)
        for cls in sorted(by_class_t.keys()):
            items = by_class_t[cls]
            md.write(f'### {cls} ({len(items)} 张)\n\n')
            write_table(md, items)
            md.write('\n')
    
    # ── Section 3: Enchantments ──
    md.write('---\n\n')
    md.write(f'# 三、附魔 Enchantments ({len(enchantments)} 张)\n\n')
    md.write('> Buff/Debuff 效果，附加在实体上\n\n')
    if enchantments:
        by_class_e = defaultdict(list)
        for r in enchantments:
            by_class_e[r[0]].append(r)
        for cls in sorted(by_class_e.keys()):
            items = by_class_e[cls]
            md.write(f'### {cls} ({len(items)} 张)\n\n')
            write_table(md, items, show_rarity=False)
            md.write('\n')
    
    # ── Section 4: Hero Powers ──
    md.write('---\n\n')
    md.write(f'# 四、英雄技能 Hero Powers ({len(hero_powers)} 张)\n\n')
    if hero_powers:
        write_table(md, hero_powers)
        md.write('\n')

print(f'Collectible: {len(collectible)}, Tokens: {len(tokens)}, Enchantments: {len(enchantments)}, HeroPowers: {len(hero_powers)}')
print('Files written: core_cards.tsv, core_cards.md')
