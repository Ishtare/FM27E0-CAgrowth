# Xbox PC 适配与本队名单刷新

从 v1.1.21 起适用。Xbox 测试配置对应 Windows Xbox 应用或 PC Game Pass 安装、在本机运行的 FM27 27.1.0+2505117。

## 识别引擎

Steam 和 Xbox PC 会报告相同的引擎版本号。网友截图中，旧版的通用扫描找到了球员和职员，但使用的俱乐部和完整财务类型与 Xbox PC 布局不同，因此俱乐部为 0，依赖这些数据的功能进入只读。新版按实际引擎布局区分安装渠道。

| Windows 发行渠道 | 引擎版本 | 引擎映像大小 | 核对依据 |
| --- | --- | --- | --- |
| Xbox PC / Game Pass | 27.1.0+2505117 | 0x179AD000 | PE 时间戳 0x6AF5C8C6、内嵌构建标记及对象逐项检查 |
| Steam | 27.1.0+2505117 | 0x18878000 | 五处原生代码锚点及对象逐项检查 |

Xbox 测试配置使用经对应构建发布的球员、职员、球队、完整财务、日期、退役和赛事资料。退役副对象额外核对其本机类型信息与运行时归属，功能按检查结果启用。Steam 27.1 商业设施字段已通过安装文件核对。旧构建匹配方式见 [Steam 27.1 适配](engine-27.1.md)。

Xbox 资料取自 Jackhamerp24 的 GPL-3.0 项目 Football-Manager-27-Editor，锁定公开提交 ac7ed73b9743e51c6d7ec61cfb85dcd85f4574bb。 [Game Pass 构建配置](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/ac7ed73b9743e51c6d7ec61cfb85dcd85f4574bb/offsets/27.1.0%2B2505117-gamepass.json) · [Steam 构建配置](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/ac7ed73b9743e51c6d7ec61cfb85dcd85f4574bb/offsets/27.1.0%2B2505117-steam.json) · [存档载入状态](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/ac7ed73b9743e51c6d7ec61cfb85dcd85f4574bb/src/Fm27Editor.Core/Game/GameLoadState.cs) · [退役资料读取](https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/ac7ed73b9743e51c6d7ec61cfb85dcd85f4574bb/src/Fm27Editor.Core/Players/PlannedRetirements.cs)

Xbox 实际运行结果仍待核实。使用相应 Windows 版的存档挂接后，底部“连接详情…”显示识别到的构建、球员／职员／俱乐部数量及可用功能。

## 刷新球队名单和球员资料

1. 挂接已载入存档。在“球队名单”或“CA 成长”中选中俱乐部。
2. 转会、租借或青年队调动完成后，点击“刷新本队名单”。结果同步到两个页面及球员搜索。
3. 在资料页已打开旧预览时，重新打开球员资料并核对新值后再应用。

刷新先读取俱乐部当前分队，再核对已知的离队和外租记录。按 UID 合并已确认的球员身份，已有培养方案与进度随仍符合条件的球员保留，新出现的球员默认未勾选。同步前校验当前连接，识别到已切换存档、读取不完整或身份冲突时保留上次名单并说明原因。此时可在新存档载入后重新挂接。

普通球员和球员兼教练现在分别按对应人物类型核对，避免球员兼教练反复出现身份变化提示。球员资料或合同已发生变化时刷新预览，并由使用者重新确认修改。

## 培养与定位球

CA 成长默认“保留特点（按角色成长）”，使用新增能力额度提高属性。一键属性优化默认“保留特点”，也可手动选择“重新分配”并预览。点球、角球、任意球和界外球在自动成长、低值补强和两种一键优化模式下保持原值。需要专门培养定位球时，使用“自选属性成长”手动勾选。游戏训练根据 FM 自身规则结算。
