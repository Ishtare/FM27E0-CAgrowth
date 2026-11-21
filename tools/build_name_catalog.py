"""Rebuild the embedded names and coverage reports from the included public facts.

No network access or game process is required. CCTV IDs are provenance keys only,
never FM person UIDs. No unsourced Chinese transliteration is generated here.
"""
from pathlib import Path
from urllib.parse import urlparse
import csv
import datetime
import json
import re

ROOT = Path(__file__).resolve().parent.parent
CHINESE = re.compile(r'[\u3400-\u9fff]')

def unique(values):
    return list(dict.fromkeys(v.strip() for v in values if v and v.strip()))

def english_forms(values, both_orders=False):
    forms = []
    for value in values:
        # The public directory uses "surname, given names"; FM also uses given-name first.
        if value.count(',') == 1:
            surname, given = (part.strip() for part in value.split(',', 1))
            if surname and given:
                forms.append(given + ' ' + surname)
                # The supplementary directory mixes family/given order for Chinese names.
                # Both complete published token orders still require the same birthday.
                if both_orders:
                    forms.append(surname + ' ' + given)
        forms.append(value)
    return unique(forms)

def chinese_values(values):
    # Single-character placeholders such as "德" cannot establish a usable translated name.
    return unique(v for v in values if len(CHINESE.findall(v)) >= 2)

def validate_source(url):
    parsed = urlparse(url)
    host = parsed.hostname or ''
    assert parsed.scheme in ('http', 'https') and any(host == d or host.endswith('.' + d) for d in ('cctv.com', 'cctv.cn', 'news.cn', 'xinhuanet.com', 'acmilan.com')), url

def main():
    snapshot = json.loads((ROOT / 'name-data/cctv-rosters.json').read_text(encoding='utf-8'))
    assert snapshot['catalogue_ids_are_fm_uids'] is False
    supplement = json.loads((ROOT / 'name-data/supplementary-rosters.json').read_text(encoding='utf-8'))
    assert supplement['catalogue_ids_are_fm_uids'] is False
    original_ids = {p['catalogue_id'] for p in snapshot['people']}
    assert not original_ids.intersection(p['catalogue_id'] for p in supplement['people'])
    original_retrieved = snapshot['retrieved']
    snapshot = dict(snapshot, retrieved=supplement['retrieved'],
                    people=snapshot['people'] + supplement['people'],
                    leagues=snapshot['leagues'] + supplement['leagues'])
    preferred_names = json.loads((ROOT / 'name-data/preferred-translations.json').read_text(encoding='utf-8'))
    preferred_by_id = {}
    for entry in preferred_names:
        validate_source(entry['source'])
        assert CHINESE.search(entry['chinese'])
        preferred_by_id.setdefault(entry['catalogue_id'], []).append(entry)
    base_lines = (ROOT / 'name-data/base-names.tsv').read_text(encoding='utf-8-sig').splitlines()
    base_rows = [line.split('\t') for line in base_lines if line and not line.startswith('#') and not line.startswith('uid\t')]
    verified_lines = (ROOT / 'name-data/verified-additions.tsv').read_text(encoding='utf-8-sig').splitlines()
    verified_rows = [line.split('\t') for line in verified_lines if line and not line.startswith('#') and not line.startswith('uid\t')]
    translated = {}
    pending = {}
    for person in snapshot['people']:
        extra = person['catalogue_id'] not in original_ids
        english = english_forms(person['english'], both_orders=extra)
        short = chinese_values(person['chinese_short'])
        full = chinese_values(person['chinese_full'])
        birthday = person['birthday']
        if birthday:
            assert datetime.date.fromisoformat(birthday).year >= 1901
        if not english or not (short or full) or (extra and not birthday):
            pending[person['catalogue_id']] = english[0] if english else '(未提供英文名)'
            continue
        preferred = (short or full)[0]
        if len(preferred) < 2 and full:
            preferred = full[0]
        source = person['source']
        published = preferred_by_id.get(person['catalogue_id'], [])
        for entry in published:
            assert entry['birth_date'] == birthday
        if published and len(published[0]['chinese']) >= 2:
            preferred = published[0]['chinese']
            source = published[0]['source']
        aliases = unique([entry['chinese'] for entry in published] + full + short)
        validate_source(source)
        translated[person['catalogue_id']] = ['0', '|'.join(english), birthday, preferred,
                                               '|'.join(aliases), source]
    rows = base_rows + list(translated.values()) + verified_rows
    for row in rows:
        assert len(row) == 6 and CHINESE.search(row[3]), row
        assert all('\t' not in cell and '\n' not in cell and '\r' not in cell for cell in row)
        validate_source(row[5])
    header = [
        '# 国内官方发布的球员中文译名；每条保留来源。不是自动音译。',
        '# 目录核对：' + snapshot['retrieved'] + '；五大联赛96队、中超16队、国家队48队、其他欧洲16队。',
        '# ' + str(len(translated)) + ' 名目录球员有可用中文对照；另保留原有及单独核实的来源记录。',
        '# ' + str(len(pending)) + ' 人缺少可确认中文译名或生日，另列待补，不生成音译。',
        '# CCTV 目录 ID 不等于 FM UID；目录记录 UID 均为 0，按英文姓名和出生日期确认人物。',
        '# 原有 106 条对照保留在前，重复人物补充别名及来源，不改变原有优先译名。',
        'uid\tenglish\tbirth_date\tchinese\taliases\tsource',
    ]
    (ROOT / 'official-names.tsv').write_text('\n'.join(header + ['\t'.join(row) for row in rows]) + '\n', encoding='utf-8')
    person_by_id = {p['catalogue_id']: p for p in snapshot['people']}
    stats = []
    details = []
    team_count = sum(len(league['teams']) for league in snapshot['leagues'])
    extra_count = sum(pid not in original_ids for pid in translated)
    coverage = ['已核对人物姓名对照库收录范围', 'v1.1.12 同时内置完整游戏词库（506,169 条组成记录、91,482 条完整／条件记录）及自动音译。', '本文件只统计下列来源人物对照；目录待补项目不等于修改器会继续只显示英文。', '资料核对日期：' + snapshot['retrieved'], '',
                '按央视赛事中心各球队球员目录整理，含目录列出的青年及外租球员。',
                '这份名单用于中英文姓名对照，不改变 FM 中的所属球队，也不代表某份存档的实时阵容。',
                '五大联赛 96 队（3,385 人有对照）继续保留，新增中超 16 队、48 支国家队及其他欧洲 16 队。',
                f'{team_count} 个球队目录共 {len(person_by_id):,} 条按编号去重的人物资料；{len(translated):,} 条有可用中文对照，{len(pending)} 条缺少确认译名或生日，暂列待补。',
                '目录统计按央视编号去重；同一人物偶有多个目录编号，例如李可的两份资料，不能当作 FM 中两个人。',
                f'本次新增 {extra_count:,} 条目录对照及 {len(verified_rows)} 条单独核实记录（本泽马）；保留原有 {len(base_rows)} 条基础对照，合计 {len(rows):,} 条姓名记录。',
                '本泽马支持“本泽马 / 卡里姆·本泽马 / Karim Benzema / Benzema”；已核对的 FM UID 与原名可在年龄修改后继续匹配。',
                '以下人数为姓名资料覆盖数量；存档中的实际匹配数量请在“姓名对照…”查看。',
                '各联赛内去重；联赛之间可能因转会或外租有重复。', '']
    markdown = ['# 已核对人物姓名对照库覆盖说明', '', 'v1.1.12 另有完整游戏姓名词库与自动音译，覆盖各球队、青年球员及未来新组合。本文件只统计有来源的人物对照；目录待补不等于修改器继续只显示英文。完整处理规则见 [姓名处理说明](docs/name-translations.md)。', '', '核对日期：**' + snapshot['retrieved'] + '**。', '',
                f'覆盖央视赛事中心 **{team_count} 个球队目录**，共 {len(person_by_id):,} 条按目录编号去重的人物资料，其中 **{len(translated):,} 条**有国内官方中文译名与可用身份资料，**{len(pending)} 条**缺少确认译名或生日，暂列待补。保留原有 {len(base_rows)} 条基础对照及 {len(verified_rows)} 条单独核实记录，合计 **{len(rows):,} 条**姓名来源记录。', '',
                f'五大联赛原有 96 队快照（{original_retrieved}）与 3,385 条目录对照完整保留。本次新增中超 16 队、48 支国家队及五大联赛外 16 支欧洲球队的资料，增加 {extra_count:,} 条目录对照。', '',
                '另补入本泽马：常用名“本泽马”、全名“卡里姆·本泽马”、英文“Karim Benzema / Benzema”。以已核对的 FM UID 加原名匹配时，修改年龄后仍可使用中文；无该 UID 时，只用完整英文名和生日确认身份。', '',
                '目录包含部分青年及外租球员，可能出现同一球员列在多个球队的情况。这里核对的是姓名资料；不会改变 FM 内的球队归属，也不声称覆盖任意存档中的全部人物。', '',
                '目录统计按央视编号去重；同一人物偶有不同编号，例如李可（1993-05-24）的两份资料。多个来源可以合并到同一个 FM 人物，不能把资料记录数当作存档中的人数。', '',
                '## 各联赛范围', '', '| 联赛 | 球队数 | 目录内去重球员 | 有中文译名 | 待补 |', '|---|---:|---:|---:|---:|']
    for league in snapshot['leagues']:
        all_ids = set(pid for team in league['teams'] for pid in team['player_ids'])
        ready = all_ids & translated.keys()
        stats.append({'league': league['chinese'], 'teams': len(league['teams']), 'people': len(all_ids), 'translated': len(ready), 'pending': len(all_ids - translated.keys())})
        markdown.append(f'| {league["chinese"]} | {len(league["teams"])} | {len(all_ids)} | {len(ready)} | {len(all_ids - translated.keys())} |')
        coverage.append(f'{league["chinese"]}：{len(league["teams"])} 队；{len(ready)} / {len(all_ids)} 人有中文译名')
        for team in league['teams']:
            ids = set(team['player_ids'])
            good = ids & translated.keys()
            details.append({'league': league['chinese'], 'team': team['chinese'], 'people': len(ids), 'translated': len(good), 'source': team['source']})
            coverage.append(f'  {team["chinese"]}：{len(good)} / {len(ids)}')
        coverage.append('')
    markdown += ['', '各联赛分别去重后相加会大于全库去重人数，原因是转会或外租带来的重复。', '', '## 各球队', '', '| 联赛 | 球队 | 有中文译名／目录人数 | 官方球队页面 |', '|---|---|---:|---|']
    for team in details:
        markdown.append(f'| {team["league"]} | {team["team"]} | {team["translated"]} / {team["people"]} | [央视资料]({team["source"]}) |')
    markdown += ['', '## 待补资料', '', '以下球员缺少可确认的中文译名或出生日期。中文字段为英文占位、过短，或生日缺失时，不据此绑定人物，继续支持游戏原名搜索。未自行生成音译。', '', '| 英文姓名 | 出生日期 | 目录所属球队 | 官方资料 |', '|---|---|---|---|']
    coverage += ['待补资料（继续支持游戏原名搜索）：']
    for pid, english in pending.items():
        person = person_by_id[pid]
        clubs = [team['chinese'] for league in snapshot['leagues'] for team in league['teams'] if pid in team['player_ids']]
        markdown.append(f'| {english} | {person["birthday"]} | {"、".join(clubs)} | [央视资料]({person["source"]}) |')
        coverage.append('  ' + english + '（' + '、'.join(clubs) + '）')
    markdown += ['', '## 来源与生成规则', '',
                 '- 球队及球员身份、英文姓名、出生日期：[央视赛事中心](https://cbs.sports.cctv.cn/)。每条对照保留球员资料或新闻来源，全部球队也列有来源。',
                 '- 瓦伦蒂尼的中文译名由[新华网《意甲：罗马胜维罗纳》](https://www.xinhuanet.com/sports/20260525/8483e9e60e4748508db80892b4e12964/c.html)补充，英文身份与生日继续由央视资料核对。',
                 '- 原有对照保留央视网、新华网的发布来源；官方中文短名、全名都作为可搜索名。',
                 f'- 另逐条核对 {len(preferred_names)} 条新闻及官方中文资料中的常用译名，覆盖 {len(preferred_by_id)} 名球员；例如巴斯托尼、邓弗里斯、格瓦尔迪奥尔。相关姓名及来源保存在源码 `name-data/preferred-translations.json`。',
                 '- 央视目录编号与 FM UID 分开处理。目录编号不会被当作 FM UID 使用。',
                 '- 本泽马常用名及出生日期由[央视生日当日报道](https://worldcup.cctv.com/2022/12/19/ARTIla96WkxuClUigz4cs4e1221219.shtml)核对，全名由[新华社年度运动员资料](https://www.news.cn/photo/2022-12/26/c_1129232660_6.htm)核对。已验证的 FM UID 仅用于修改器人物对应；保留完整原名核对，UID 本身不足以套用译名。',
                 '- 新增目录记录均须有生日。逗号前后姓名顺序同时保留，兼容中超目录混用的拼音顺序；仍要求生日一致及候选人物唯一。',
                 '- 用英文完整姓名和可用生日核对；姓名包含额外中间名时，还须生日相同、至少两个完整英文词、首尾一致、顺序一致且人物唯一。',
                 '- 扫描到的匹配数量可在程序的“姓名对照…”查看。未能确认的人物仍保留游戏原名。',
                 '- 源码内附精简公开资料快照与 `tools/build_name_catalog.py`，可离线重新生成。', '']
    (ROOT / 'name-coverage.txt').write_text('\r\n'.join(coverage) + '\r\n', encoding='utf-8')
    (ROOT / '姓名库覆盖说明.md').write_text('\n'.join(markdown), encoding='utf-8')
    report = {'retrieved': snapshot['retrieved'], 'teams': len(details), 'directory_people': len(person_by_id), 'translated_people': len(translated), 'pending_people': len(pending), 'base_records': len(base_rows), 'supplementary_records':extra_count,'verified_records':len(verified_rows), 'total_name_records': len(rows), 'preferred_spellings': len(preferred_names), 'leagues': stats, 'team_coverage': details, 'pending': [{'english': english, 'source': person_by_id[pid]['source']} for pid, english in pending.items()]}
    (ROOT / 'name-data/coverage.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key not in ('team_coverage', 'pending')}, ensure_ascii=False, indent=2))

if __name__ == '__main__':
    main()
