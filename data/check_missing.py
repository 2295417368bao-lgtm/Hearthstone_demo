import csv, glob, re, sys

# Read TSV
with open('data/core_cards.tsv', 'r', encoding='utf-8') as f:
    reader = csv.DictReader(f, delimiter='\t')
    rows = list(reader)

cols = list(rows[0].keys()) if rows else []
# Detect column names (case-insensitive)
def col(name):
    for c in cols:
        if c.lower() == name.lower():
            return c
    return name

cid_col = col('CardId')
coll_col = col('Coll')
name_col = col('NameEN')
type_col = col('Type')
cls_col = col('Class')

collectible = [r for r in rows if r.get(coll_col, '') == 'Y']
coll_ids = set(r[cid_col] for r in collectible)

# Read implemented
impl = set()
for fn in glob.glob('src/Fireplace.Core/Cards/Core/*.cs'):
    with open(fn, 'r', encoding='utf-8') as f2:
        for line in f2:
            for m in re.findall(r'b\.Card\("([^"]+)"', line):
                impl.add(m)

missing = sorted(coll_ids - impl)

with open('data/missing_cards.txt', 'w', encoding='utf-8') as out:
    out.write(f"Columns: {cols}\n")
    out.write(f"Total rows: {len(rows)}\n")
    out.write(f"Collectible: {len(collectible)}\n")
    out.write(f"Implemented b.Card: {len(impl)}\n")
    out.write(f"Missing collectible: {len(missing)}\n\n")
    for mid in missing:
        card = next((r for r in rows if r[cid_col] == mid), None)
        if card:
            out.write(f"{mid}  |  {card.get(name_col,'?')}  |  {card.get(type_col,'?')}  |  {card.get(cls_col,'?')}\n")
        else:
            out.write(f"{mid}  |  ??  |  ??  |  ??\n")

print("Done. See data/missing_cards.txt")
