# FM27E0 汉化改版 v1.1.31 完整版

本版的一键属性优化按蓝、绿、白、其余四档分配。预览列出各档达标数、预计区间、被压低的属性及未达标蓝绿项。角色定向成长按累计推荐 CA 预算依次补蓝、绿、白，默认的仅加速成长只推进 CA。

v1.1.30 修复全宽英文和省略重音导致的俱乐部漏搜，连接报告新增俱乐部扫描各阶段计数。CA 成长默认只推进 CA，定向成长按实际位置校核估算推荐 CA。可对整份培养名单统一设置成长模式、倍率与未勾选属性上限，保留各球员原有自选项。

v1.1.29 修复俱乐部转会预算提交，并更新筛选结果、培养名单、报名页及辅助弹窗的布局与操作。

职员转会与职务调整使用同一个入口，覆盖 21 种职位。理疗师、首席理疗师、队医和首席队医分开显示，支持简式合同职员。可以保留原职务转会、转会时改职，也可以在原俱乐部改职。部门名单、专属岗位和合同成组应用与撤销。[职员转会说明](docs/staff-transfer.md)

自选属性成长新增可选的“限制未勾选属性上涨”。按开始成长时的数值为每名球员设上限，允许自然下滑与回升至该上限。培养设置只刷新对应球员，写入后的整组回读与回滚也已核查。

一键属性优化默认按当前 CA 核算整套属性，勾选“同时升到 PA”后可保留所有原有属性，并按两阶段角色安排增加的属性。“恢复默认／开档属性”可从本机 FM27 原版数据库或你选定的早期存档读取原值，确认前逐项预览。

完整解压后运行程序，保留 third-party 文件夹。CA 成长支持跨俱乐部勾选和统一查看已选球员，内置姓名库支持中英文搜索。

32,855 项离线断言通过，一键优化和定向成长已完成实机验收。[使用说明](使用说明-1.1.31.md) · [变更记录](CHANGELOG.md) · [验证范围](VALIDATION.md)

以下保留早期版本与原作者的项目介绍。

# FM27€0 - Open source (modified build with CA growth, Chinese interface)

A free, open-source live editor for **Football Manager 27**. Edit your players, staff, contracts and club finances while the game is running.

> This Chinese-language build extends Anze Alexander's FM27€0 v1.1.0 with CA growth and other editing features. The current executable is `FM27E0-CAgrowth v1.1.31.exe`. The original editor is available on [sortitoutsi.net](https://sortitoutsi.net/content/78059/fm27eur0-v100-live-editor-for-fm27). See [the changelog](CHANGELOG.md) for changes.

## What you can do

**Players**
- Change any attribute: technical, mental, physical, goalkeeping and the hidden ones (consistency, important matches, injury proneness and more)
- Set position ratings, so a player can play anywhere
- Change name, date of birth and nationality (rebuild a youngster into your own player)
- Change, add or remove second and third nationalities
- Change looks: ethnicity, skin tone, hair colour and hair length (shown in the 3D match)
- Change place of birth
- Add or remove player traits (shoots from distance, tries tricks, curls ball and 56 more)
- Set Current and Potential Ability (CA/PA) and turn any youngster into a future star
- Edit personality: professionalism, ambition, loyalty, temperament and more
- Change wages and contract expiry dates
- Transfer-list or loan-list any player and set an asking price
- Set squad status (Star Player, Regular Starter, Hot Prospect...)
- Change squad numbers
- Change height
- Set current, home and world reputation
- Change international caps and goals (senior and U21)
- Edit bonuses and release clauses: change amounts, add a release clause or remove one
- Move a player to another club while its squad list has a free place (common during the season)
- Release a player (he becomes a free agent)
- Swap two players between clubs: each joins the other's club straight away and keeps his contract. FM only lets them play in league matches once you register them in the next registration window

**Find players**
- Search every player in the game by age, position, CA, PA, nation, club, contract end, transfer list or free agent
- Sort by any column and double-click a player to edit him

**Staff**
- Find staff by name or by club, then edit any coach, scout, physio or analyst: coaching (attacking, defending, fitness, goalkeeping, possession, set pieces, tactical, technical, working with youngsters), mental (authority, determination, motivating, people management), knowledge, data analysis and medical attributes
- Edit their personality and reputation too
- Change their wage and contract expiry
- Change jobs at the current club or during a transfer. Includes physios, doctors, analysts and verified head coach or director-of-football posts.
- Transfer contracted staff to clubs with verified team records and a writable department slot or vacancy. Includes basic contracts.
- Find staff by job and attributes (for example every assistant coach with Tactical 15+), age, club, or only the unemployed

**You (the manager)**
- Type "manager" on the Staff tab to edit your own coaching, mental and knowledge attributes, personality, reputation, wage and contract length

**Squad**
- Max out a whole squad's attributes

**CA growth** (added in this modified build)
- Pick a club, tick the players you want to develop (or **Select visible** for the filtered roster), and their CA climbs along a natural curve until it reaches PA
- A player's time depends only on his gap, not his age: `span = max(21, round(gap x K))`, with a 21-day floor and no upper cap. A small gap is closed in weeks; a large gap can take longer than two seasons on the low gears
- The speed is one of nine gears in a dropdown (**2x to 10x**, default **4x**): K = 40 / gear in-game days per CA point, so 4x is K = 10. The curve f(t) = t*(2-t) runs over each player's own span, roughly three quarters of the gap in the first half, then slower and slower towards the ceiling
- The **Progress** column shows `elapsed/span days` or `at PA`; changing the gear does not re-stretch a run that has already started
- Growth stops by itself at PA. With a negative PA (a random range) it stops at the top of the range shown in the PA column
- **Fill to PA now** closes the whole gap in one go (it asks first)
- **Start growth** keeps paying once per in-game day while the editor stays open; a long skip (a holiday) pays for up to a whole season in one write. It stops by itself when the game date stops being readable, when the watched players move after a new scan, or when every watched player is at his PA
- Nothing is ever written past PA, and a player FM already pushed further than the curve is left where he is instead of being pulled back
- Every write is listed in the Change log, so it can be undone like any other edit
- In CA-only mode, CA is not the attributes: FM itself spends the new CA on training, so the attributes follow over the next few days. Hand-editing CA works the same way
- **Automatic roster refresh**: once per new in-game month, update the roster and rebind only explicitly watched UIDs, keeping each player’s options and progress. Previously unchecked players and new signings remain unchecked. Departed, missing or ambiguous identities are removed. This replaces the old v1.1.3 auto-select-whole-squad behaviour.

**Chinese interface** (added in this modified build)
- The main window, all 8 sub-dialogs, the columns, buttons, hints and the players/staff rows are Chinese, using FM's own Chinese terms for attributes, positions, squad statuses and staff jobs
- The most common error messages are Chinese too; a few rarer message boxes and the Change log list stay English. This README and CHANGELOG stay English

**Club**
- Set your balance, transfer budget and wage budget
- Freeze your money so it never drops (income still counts)
- Upgrade facilities: training, youth, junior coaching, youth recruitment and corporate
- Raise your club's reputation, all the way to Worldwide
- Change stadium capacity and how much of it is used
- Rename a club (the new name can be as long as the old one or shorter)
- Set season ticket holders and the average ticket price
- Change sponsorship deals (main kit, kit manufacturer, shirt sleeve and general sponsors)

**Competitions**
- Change league prize money for every finishing place, in the leagues that have prize money (for example Premier League, Serie A, Spanish First Division, League One and League Two). After a save is loaded FM keeps most leagues' tables for this season only, so that is what you change; a place that pays nothing this season cannot be given money

**Safety**
- Every change is logged and can be undone, one by one or all at once
- Works in your own currency (€, $, £...)
- Checks the game's data after every scan. Any part that does not look right becomes read-only instead of writing to the wrong place
- Recognises which FM version is running and finds its way again after most game updates

**Not in this version (yet)**
- For the verified Steam 27.0.1+2503222 build, injuries, condition, match sharpness, morale and fatigue have separate guarded editors. Other builds and failed object checks remain read-only; see the current Chinese documentation.
- Cup prize money (round appearance money)

## How to use

1. Back up your save (`Documents\Sports Interactive\Football Manager 27\games`).
2. Download `FM27€0 vX.Y.Z.exe` (latest version) from [sortitoutsi.net](https://sortitoutsi.net/content/78059/fm27eur0-v100-live-editor-for-fm27).
3. Start Football Manager 27 and load your save.
4. Run it and click **Attach & scan**.
5. Search for a player or club, make your changes and click **Apply changes**.
6. Continue one day in FM to see the changes. They are kept when you save.

If money amounts look different from FM, click **Currency...** and follow the three steps.

Works on Windows 10/11 with the Steam and Xbox / PC Game Pass versions of FM27. For offline, single-player saves only.

A few antivirus engines flag the exe because it edits another program's memory. These are false positives, see [ANTIVIRUS.md](ANTIVIRUS.md).

## Making players happy

FM works out a player's happiness again every day from his situation, so there is no happiness value to set. Change the cause and it improves after the next day:
- **Considering his options:** take him off the transfer list (Players tab, transfer status).
- **Unhappy with playing time:** lower his squad status to match how often he plays.
- **Unhappy with his contract:** raise his wage.

Reasons that come from his standing in the squad (hierarchy, social group) change slowly as he settles in.

## Game versions and patches

The editor shows the FM version it attached to, for example `FM 27.0.0 (FM27 built-in profile)`. It was tested on the FM27 Steam demo and the Xbox / PC Game Pass version, both 27.0.0. The two are different builds with the same version number, and the editor picks the right profile by itself.

After a game patch, things inside the game can move. If the editor cannot find the players, player-coaches, staff, clubs, contracts, finances, your manager, loan deals, stadiums, competitions and prize money tables where it expects them, it searches the game's memory and finds them again by itself, and it finds the game date again from the game's code. This was tested by running the Steam profile against the Xbox build, where every one of them sits somewhere else: all were found. After every scan the editor runs safety checks (attributes in range, real birth dates, contracts that point back to their player and so on). Each part of the editor that fails its check becomes read-only and the others keep working. If the player data itself has changed, the whole editor becomes read-only until there is an update. The **Change log** tab lists every check.

All game-specific numbers live in a profile. FM27 is built in. A text file in a `profiles` folder next to the exe can supply the numbers for another version: run the exe with `--dump-profile` for a starting file.

## Support my work

If you want to support my work (Anze Alexander, the author of the original FM27€0): **[ko-fi.com/anzealexander](https://ko-fi.com/anzealexander)**
The Ko-fi button is not in the window of this modified build; the author's credit and the link to the original download page are.

## Thanks

Thanks to **tdg6661** for the *Football Manager 2024 Cheat Table* ([patreon.com/tdg6661](https://www.patreon.com/tdg6661)).
It was used as a reference for the player trait names and to cross-check field locations.
FM27€0 contains no code from it. If you use Cheat Engine, their table is well worth a look.

## Build it yourself

Run `build.bat`. It uses the C# compiler that ships with Windows, so nothing else needs to be installed.

## License

Copyright (C) 2026 Anze Alexander. Licensed under the [GNU GPL v3](LICENSE) (or any later version), with the [additional terms](ADDITIONAL-TERMS.txt) below.

In plain words:
- You can use, change and share this code for anything.
- If you share it or anything made from it, it has to stay free and open source under the same license, with the full source code included.
- Keep the copyright notice and give credit to **Anze Alexander** (with a link to the original download page).
- Nobody can take this code and sell it as a closed, paid product.

Additional terms (GPL section 7):
- Keep "Original by Anze Alexander" and the link to the original download page visible in the program's main window.
- A modified version must not be called FM27€0 or pass itself off as the original. Give it another name, or say clearly in its name and window that it is modified.
