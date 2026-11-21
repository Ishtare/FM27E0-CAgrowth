"""Offline provenance/coverage audit for the published name catalog."""
from pathlib import Path
from urllib.parse import urlparse
import datetime
import json
import re

ROOT=Path(__file__).resolve().parent.parent
snapshot=json.loads((ROOT/'name-data/cctv-rosters.json').read_text(encoding='utf-8'))
supplement=json.loads((ROOT/'name-data/supplementary-rosters.json').read_text(encoding='utf-8'))
report=json.loads((ROOT/'name-data/coverage.json').read_text(encoding='utf-8'))
preferred=json.loads((ROOT/'name-data/preferred-translations.json').read_text(encoding='utf-8'))
rows=[line.split('\t') for line in (ROOT/'official-names.tsv').read_text(encoding='utf-8').splitlines() if line and not line.startswith('#') and not line.startswith('uid\t')]
assert len(rows)==report['total_name_records']==5270
assert len(snapshot['people'])==3415
people={p['catalogue_id']:p for p in snapshot['people']}
assert len(people)==len(snapshot['people'])
assert snapshot['catalogue_ids_are_fm_uids'] is False
assert supplement['catalogue_ids_are_fm_uids'] is False
extra_people={p['catalogue_id']:p for p in supplement['people']}
assert len(extra_people)==len(supplement['people'])==1923
assert not people.keys() & extra_people.keys()
assert len(people)+len(extra_people)==report['directory_people']==5338
teams=[team for league in snapshot['leagues'] for team in league['teams']]
assert len(teams)==len({team['catalogue_id'] for team in teams})==96
assert sorted(len(league['teams']) for league in snapshot['leagues'])==[18,18,20,20,20]
assert all(team['player_ids'] and set(team['player_ids'])<=people.keys() for team in teams)
for entry in preferred:
    person=people[entry['catalogue_id']]
    assert person['birthday']==entry['birth_date']
    assert re.search('[\u3400-\u9fff]',entry['chinese'])
for row in rows:
    assert len(row)==6 and re.search('[\u3400-\u9fff]',row[3])
    assert row[0].isdigit()
    url=urlparse(row[5]);assert url.scheme in ('https','http') and url.hostname
    if row[2]:datetime.date.fromisoformat(row[2])
new_rows=rows[106:3491]
assert len(new_rows)==3385
assert all(row[0]=='0' for row in new_rows)
assert len({(row[1],row[2]) for row in new_rows})==len(new_rows)
available={(row[1].split('|')[0],row[2]) for row in new_rows}
mapped=set()
for person in snapshot['people']:
    forms=[]
    for original in person['english']:
        if original.count(',')==1:
            last,first=original.split(',');forms.append(first.strip()+' '+last.strip())
        forms.append(original)
    if any((name,person['birthday']) in available for name in forms):mapped.add(person['catalogue_id'])
assert len(mapped)==3385
assert len(people.keys()-mapped)==30
for league,stats in zip(snapshot['leagues'],report['leagues']):
    ids=set(pid for team in league['teams'] for pid in team['player_ids'])
    assert stats['people']==len(ids) and stats['translated']==len(ids & mapped)
for team,stats in zip(teams,report['team_coverage']):
    assert stats['people']==len(set(team['player_ids']))
    assert stats['translated']==len(set(team['player_ids']) & mapped)
extra_rows=rows[3491:5268]
assert len(extra_rows)==report['supplementary_records']==1777
assert all(row[0]=='0' and row[2] for row in extra_rows)
assert len({(row[1],row[2]) for row in extra_rows})==len(extra_rows)
available_extra={(name,row[2]) for row in extra_rows for name in row[1].split('|')}
extra_mapped={pid for pid,p in extra_people.items() if any((name,p['birthday']) in available_extra for name in p['english'])}
assert len(extra_mapped)==1777
assert len(extra_people.keys()-extra_mapped)==146
people.update(extra_people);mapped.update(extra_mapped)
assert len(mapped)==report['translated_people']==5162
assert len(people.keys()-mapped)==len(report['pending'])==report['pending_people']==176
all_groups=snapshot['leagues']+supplement['leagues']
all_teams=[t for l in all_groups for t in l['teams']]
assert len(all_teams)==len({t['catalogue_id'] for t in all_teams})==report['teams']==176
assert [len(l['teams']) for l in supplement['leagues']]==[16,48,16]
assert all(t['player_ids'] and set(t['player_ids'])<=people.keys() for t in all_teams)
for league,stats in zip(all_groups,report['leagues']):
    ids={pid for t in league['teams'] for pid in t['player_ids']}
    assert stats['people']==len(ids) and stats['translated']==len(ids & mapped)
for team,stats in zip(all_teams,report['team_coverage']):
    ids=set(team['player_ids'])
    assert stats['people']==len(ids) and stats['translated']==len(ids & mapped)
assert len(rows[5268:])==report['verified_records']==2
benzema_uid,benzema_fallback=rows[5268:]
assert benzema_uid[:5]==['8435089','Karim Benzema|Benzema','1987-12-19','本泽马','卡里姆·本泽马']
assert benzema_fallback[:5]==['0','Karim Benzema','1987-12-19','本泽马','卡里姆·本泽马']
print(json.dumps({'provenance_and_coverage':'PASS','teams':176,'directory_people':5338,'translated_people':5162,'pending':176,'name_rows':5270,'preferred_spellings':len(preferred),'directory_fm_uids_all_zero':True,'verified_benzema_uid':8435089},ensure_ascii=False))
