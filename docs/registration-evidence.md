# 注册资格功能：来源、实现与验证边界

该文件公开注册功能的实现依据与验证边界，不包含私人存档或原始游戏内存资料。

## 为什么仅修改国籍可能无效

Sports Interactive 社区团队在 FM26 的同类报告中解释：意大利非欧名额在转入时判定，没有获得名额的球员会保留当季认定，随后更改国籍也不重算。该答复说明了机制，**属于 FM26 的同类说明**，不能代替 FM27 的实际存档测试。[SI 原始答复](https://community.sports-interactive.com/bugtracker/previous-versions/1644_football-manager-26-bugs-tracker/1890_pre-game-in-game-editors/in-game-editor/adding-second-nationality-doesnt-remove-non-eu-status-r39748/)

FMRTE 27 的官方说明将意大利非欧认定、赛事注册、工作许可及本土培养分开处理；意甲示例是在转会前处理。因此这里只用于区分资料的含义，不把它作为转会后修复已成功的证明。[个人资料](https://docs.fmrte.com/players/personal-details/)，[注册与国籍](https://docs.fmrte.com/players/registration-and-permits/)

## 公开源码参考与致谢

参考项目为 [Jackhamerp24/Football-Manager-27-Editor](https://github.com/Jackhamerp24/Football-Manager-27-Editor)，核对固定提交 `cfc80fdd336e553a18fe08738321079274e66836`，其 [GPL-3.0 许可证](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/cfc80fdd336e553a18fe08738321079274e66836/LICENSE) 与本改版采用的 GPL 许可兼容。感谢该项目公开以下格式与映射：

- [27.0.1+2503222 Steam 映射](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/cfc80fdd336e553a18fe08738321079274e66836/offsets/27.0.1%2B2503222-steam.json)
- [球员状态](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/cfc80fdd336e553a18fe08738321079274e66836/src/Fm27Editor.Core/Players/PlayerStatuses.cs)、[关系记录](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/cfc80fdd336e553a18fe08738321079274e66836/src/Fm27Editor.Core/Players/RelationEntries.cs)
- [赛事规则表示](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/cfc80fdd336e553a18fe08738321079274e66836/src/Fm27Editor.Core/Competitions/CompetitionSquadRules.cs)、[规则名称](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/cfc80fdd336e553a18fe08738321079274e66836/src/Fm27Editor.Core/Competitions/CompetitionRuleNames.cs)

本版根据公开格式独立编写读取、预览、写入与测试，没有引入该项目的注册解锁注入／跳板功能，也不调用原生游戏函数。原 FM27€0 作者 Anze Alexander 的署名、原版链接、GPL 与附加条款继续保留。

## 本版采用的字段

仅适配 Steam `27.0.1+2503222`，模块映像大小 `0x185FA000`，并核对 RVA `0x162C88B` 的 `F6 81 78 02 00 00 08` 指令签名。该原生检查读取普通球员对象 `+0x278` 的 `0x08` 位；本版只清除此位，其余七位保留，不推断相邻状态位的含义。

注册关系按已核实的 16 字节记录处理，当前球队目标、类别 4、登记类型 `0x19`、取消登记类型 `0x1B`，国内类型 8、UEFA 类型 0。只在现有已核实容量内更新，不分配游戏内存。已有登记不会重复添加；其他赛事、球队及未知记录按原字节保留。

v1.1.10 最初支持的通用最大人数规则：

| 编码 | 含义 |
| --- | --- |
| `0x04` | 外援比赛名单上限 |
| `0x07` | 非欧比赛名单上限 |
| `0x12` | 非欧注册名单上限 |
| `0x15` | 外援注册名单上限 |
| `0x1E` | 每赛季从国外引进非欧球员上限 |

规则从已扫描球队的实际关联赛事读取，不使用失配的全局赛事根指针。主值位于规则 `+0x18`，类型在 `+0x1C`；最大值方向仅检查 `+0x1E` 的单字节 `0xFF`，`+0x1F` 是独立资料，不能错误地当作 16 位方向的一部分。只接受唯一且无附加条件的规则，必须同时找到运行值及两份保存表示且数值一致，才生成三处修改计划。共享赛事会尽量按当前加载资料标明，作用范围始终是整项赛事。

## v1.1.11 扩展

按同一格式增加最高／最低方向检查，支持 18 类人数规则，包含总报名容量、本国籍及不同培养类型。五大联赛、实际读取结果、来源与新验证范围见 [五大联赛报名规则](league-registration.md)。此前意甲个人修复逻辑保留，新增规则不改写国籍或培养经历。

## 验证与限制

注册功能的本地检查覆盖现有登记保留、非欧状态位、登记容量、三份赛事规则值、身份与归属检查、失败回滚及成组撤销。生产代码进行过只读结构和默认预览核对，没有由开发工具应用或操作游戏窗口。

个人非欧修复已有可以注册的使用反馈，下一比赛日出场及保存重载仍需验证，其他联赛规则不能由单项反馈推定生效。部分规则类型仅通过离线结构检查；界面只在实际出现且完整匹配时提供写入。意甲年度转会配额不直接等于通用名单人数上限。

未知版本、球员兼教练、外租／临时球队、不完整或带条件的规则保持只读。本功能不承诺覆盖工作许可、本土培养、杯赛代表资格、MLS 或全部特殊条款，也不会用国籍变化代替独立规则。

## v1.1.17 统一入口

球员资格与赛事规则现集中管理。各国共用登记关系按实际结构检查；独立历史非欧位仍仅限意大利，不推断其他国家偏移。国家支持与验证边界见 [统一报名管理](registration-workspace.md)。
