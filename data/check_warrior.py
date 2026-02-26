import csv
with open('data/core_cards.tsv','r',encoding='utf-8') as f:
    rows = list(csv.DictReader(f, delimiter='\t'))
warriors = [r for r in rows if r['Class']=='Warrior' and r['Coll']=='Y']
with open('data/warrior_check.txt','w',encoding='utf-8') as out:
    out.write(f'Warrior collectible: {len(warriors)}\n\n')
    for w in sorted(warriors, key=lambda x: int(x['Cost'])):
        out.write(f'{w["CardId"]:25s}  {w["Cost"]:>2s}  {w["Atk"]:>2s}/{w["HP"]:>2s}  {w["Type"]:12s}  {w["NameEN"]:30s}  {w["NameZH"]}\n')
        out.write(f'    {w["Desc"][:80]}\n')
        out.write(f'    Mechanics: {w["Mechanics"]}\n\n')
print("Done")
