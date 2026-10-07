# 环境能力、跨版本资格与普通用户工作台

日期：2026-10-01。未来实现建议与静态审查，未启动 Pro/PS/MCP、未改兼容脚本或客户端。配套[模型合同](MODEL_PROVIDER_AND_FALLBACK_CONTRACTS.md)、[资源包合同](FORMAT_AND_RESOURCE_PACK_CONTRACTS.md)。

## 1. 五层证据不能混为一个 PASS

| 层 | 能证明的事 | 不能代替 |
|---|---|---|
| 声明/编译 | manifest、TFM、API签名和编译条件匹配 | 真宿主加载 |
| 加载/连接 | 插件/peer加载、端点响应、工具发现 | 操作业务成功、多版本协议合规 |
| 当前能力探测 | 当前版本、对象、许可/资源可用观测 | 持续预约或逐操作内容正确 |
| 具体操作资格 | 某版本/参数域/输入下真实操作和 oracle | 全目录、全格式或所有环境 |
| 完整任务资格 | 同版输入/方法/PS/Office/文件与科学门完整通过 | 未测试环境、不同任务或新资源版本 |

成熟度和环境可用性使用 V2 的统一枚举：DECLARED / CONTRACT_DEFINED / IMPLEMENTED / RUNTIME_VERIFIED / E2E_VERIFIED / INDEPENDENTLY_ACCEPTED；AVAILABLE / CONDITIONAL / UNAVAILABLE / UNKNOWN。源码审查另存 sourceEvidence，缺依赖、不支持、暂不可用等记 availabilityReason，不另造轴内状态。IMPLEMENTED 不自动转 RUNTIME_VERIFIED；AVAILABLE 不自动转 E2E_VERIFIED；某项缺少证据保持 UNKNOWN，不能写 0 或 false 假装确定。

## 2. 本轮静态审查落点

| 位置 | 当前事实 | 拟议实现工作 |
|---|---|---|
| Compatibility.csproj:3、Configuration.csproj:3 | 默认 net6.0-windows / net6.0 | 分别记录源码、包、加载程序集 TFM 和宿主 CLR |
| Config/compatibility-policy.json:70–141 | 两 TFM 变体，verifiedSeries 仅3.5 | 每宿主/包组合保留实际能力范围与证据 |
| Compatibility/Services/VersionService.cs:18–20 | 从插件 TargetFrameworkAttribute取framework | 新能力探针增加活动 Host/实际 CLR/SDK身份，改变公开字段需SC |
| Core/Versioning/VersionIdentity.cs:17–24 | 未识别net8时兜底net6 | 新内部快照保留UNKNOWN，不用旧兜底证明适配 |
| Server/Mcp/McpProtocolHandler.cs:63–96 | initialize固定版本、完整tools/list | 多版协商、分页或新通知独立设计/合同和客户端验收 |
| scripts/check-compatibility.ps1:138–145 | 变体解析已有；部分未验版本标PASS但文字未验；Python/ArcPy固定口径 | 分结构适配/运行资格；按Host变体探测，不靠一个PASS推广 |
| Server/Internal/PsChannelHandler.cs:403–516 | 允许可选版本/能力字段，未见实质核验/存储 | 真实peer/profile/digest协议，缺字段或差异保持未知/不相容 |
| Server/Internal/PsSessionStore.cs:19–33 | pairing/persistent/session为Pro内存字典 | 重启恢复/撤销/token保管另实现和故障验收 |
| PsChannelPolicy + Composition.cs:767–770 | 默认策略集中在Server，Composition构造 | 由Configuration生成typed策略，保留现缺省与固定回环路由 |
| Composition.cs:228–236、RuntimePathResolver.cs:194–224 | LocalAppData派生runtime/managed-logs | 实施明确D盘拥有根/可写探针；不声称现路径已满足 |
| Compatibility/Services/LicenseService.cs:12–97 | 基础级别、单扩展探测和可选checkout | 只读探测不checkout；实际动作前预约/复核和释放策略 |

以上只是当前代码边界，不修改已有指挥裁定。G-240验证的连接/只读范围继续成立；本稿不因静态差距撤销其范围内PASS，也不据PASS推导未验功能。

## 3. SP04 EnvironmentCapabilitySnapshot

快照包含 snapshotId/digest、采集时间与证据方法，六组观测：

1. Host身份：活动Pro/build、实际加载SDK、包/程序集hash及TFM、宿主CLR/架构；安装注册表声明独立保存。
2. Bridge身份：实际Python/ArcPy、固定typed操作与版本、健康/能力范围；不能增加任意脚本入口。
3. Adobe身份：实际PS/UXP/peer/plugin版本、协议/能力摘要、pairing/file-grant/文档归属和会话周期。
4. MCP身份：客户端/服务实现、实际固定或协商协议、transport行为/限制；工具发现与业务证据分开。
5. 许可与资源：当前基础/扩展观察、实际占用/预约、CPU/可用RAM、GPU/后端/VRAM、D盘容量/拥有目录/探针。
6. 支持引用：每operation/参数域/格式动作/模板/模型/规则的成熟度、可用性、限制、证据与失效条件。

进程、GPU/WMI或访问探测失败记录UNKNOWN+原因+方法，不填0GB，不依安装目录推测已启动。只读探测不创建GIS/PS产物或领取许可。Host写、持久凭据或文件探针按已授权作用域执行；本轮只规划这些行为。

TTL按不同观测配置，Host重启、peer断连、目录/资源版本改变、许可变化、对象切换或能力摘要差异立即使对应观测失效。只在时间上新鲜不代表任务期间一致；派发仍须最后复核。

## 4. Pro/PS/MCP的跨版本策略

官方Esri资料说明Pro3.3转向.NET8，并描述3.0–3.2加载项在3.3的向前兼容；Pro3.7升级指南描述.NET10，并列出复制/粘贴、拖放序列化的迁移注意。它们只说明具体平台的演进，**不证明本项目所有功能已在这些版本验收**。[Esri .NET8](https://github.com/Esri/arcgis-pro-sdk/wiki/ProGuide-NET-8-Upgrade)、[Esri .NET10](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/conceptdocs/docs/ProGuide-NET-10-Upgrade.html)

本产品按 net6/net8现变体和未来平台候选分别建HostProfile、API/GP参数差异和任务资格；若新增net10变体，需要独立依赖/公开合同/构建与真实业务验证，不因官网存在就改包。旧Pro版本使用真实最低API面和匹配Bridge，不靠使用相同命名空间证明全签名兼容。

工作台资料拖放、对象复制/粘贴也进入每个 HostProfile 的操作资格；若涉及旧序列化方式，采用明确版本的 JSON/XML/文本载荷并检验类型、长度、来源与对象身份，再进入相同资料绑定。不能只让窗口成功打开就略过这些用户操作，也不能反序列化资源文本为任意可执行对象。这是拟议兼容实现切片，不是当前功能证明。

PS manifest声明的minVersion/API权限、真实UXP能力和业务测试分别核对；不是提高或降低minVersion数字就完成支持。[Adobe manifest](https://developer.adobe.com/photoshop/uxp/2022/guides/uxp-guide/uxp-misc/manifest-v5/)

MCP当前固定2024-11-05路径保留，不静默加新版协议/分页。未来每版本与客户端建立合同测试，未知版本请求按正式规范和已批准策略处理；不能返回一个固定版本后宣称通用协商。模型API endpoint不等于MCP端点，远端模型请求不要求公开本机MCP。

验证矩阵先覆盖实际支持组合和关键差异，再用组合测试发现交互风险；组合/抽样测试不代替每个发布承诺组合的任务门。身份不符、未知Host或新能力摘要能停止和说明，不自动选择未验HostAdapter或安装依赖。

## 5. SP08 TaskReadinessReport：从目标成果反查条件

输入为 OutputContract、typed plan、环境快照、格式/包/模型支持条目。每步骤列 requiredCapabilityRef、最低证据域、当前观察、满足/缺失/未知/不相容、受影响产物、已验等价路径与重新检查点。

根结果区分 READY_FOR_ORIGINAL_CONTRACT、READY_WITHIN_ADOPTED_EQUIVALENCE、OPTIONAL_GAPS_ONLY、WAITING_REQUIRED_CAPABILITY。可选文件缺失不伪装已生成；必要PSD/方法/内容检查缺失就不能完整交付。改变必要成果属于新的OutputContract，不是技术性静默降级。

图像模型不是所有任务的必需项；已验确定性布局/检查可以满足既定要求。反之，若某任务明确要求某项不可替代生成或检查能力，缺它必须等待。等价路径要有同问题/同输出/同科学门的资格，网络改直线距离不等价。

## 6. 工作台视图和操作

现阶段建议在Pro DockPane按任务组织“资料与关键条件、计划与真实状态、预览与成果”三个视图；地图对象选择保留在地图。高级参数按影响展开，普通用户无需看内部协议/token/工具类名。

| UI状态/动作 | 必须展示 | 后端依据 |
|---|---|---|
| 资料绑定 | 实际对象、时间/单位、有效覆盖、歧义 | DatasetContract/InputSnapshot |
| 缺条件 | 一个合并问题集、为什么影响结果、可选已验口径 | DataAdequacyReport/DecisionRecord |
| 执行前任务卡 | 精确D盘输出目录/必要文件/方法/范围/预算 | OutputContract/PreparedInvocation |
| 等待 | 等资源、模型、Host或用户决定，区分原因 | 已提交事件+CapabilitySnapshot |
| 执行中 | 可确认的已完成步骤与当前步骤，不虚构百分比 | Jobs实际事件；未知总量用阶段进度 |
| 验证中 | 文件/科学/视觉/编辑性检查分层 | CheckReport及覆盖 |
| 待对账 | 已知效果、未知项、将做的最小核查 | ReconciliationStrategy/receipt |
| 取消 | 已请求、已停止、已有产物、未解决效果分别显示 | 控制意图和效果状态 |
| 预览改稿 | 最多三张首轮预览，样式/分析修改影响 | typed Patch/根预算/更新计划 |
| 领取/更新 | 必需/可选/部分文件及版本，复用/重算原因 | DeliveryRevision/UpdateImpactPlan |

UI订阅已提交事件并以sequence恢复/去重；断线重连补状态而不重发业务。Presenter只更新视图，不能写任务事实。按钮携带task/revision与当前能力，旧窗口不能采用新任务提案；命令进入同一应用服务与Invoker。

输出目录在写前显示；已有授权不重复要求批准。超范围的方法、数据发送、文件或预算变化才形成新的决定。键盘焦点、取消/状态查询和高DPI布局不能被长渲染阻塞；长期任务显示真实等待与可领取部分，不把估计时间当完成事实。

## 7. 可访问、离线和资源不足

中文/英文/符号字形按真实字体fallback测量；fallback后重新检查字号/换行/图例，不以字体名存在证明内容完整。灰度/色觉模式在定量颜色规则允许的合同内，类别可区分与图例绑定实测；辅助纹理/标记不能改数值。

离线模式分确定性表单、已部署本地模型、已缓存合法资料、必须联网服务。任务准备逐项检查，不能用“有本地插件”证明全任务离线。缺PS时保留GIS能力和已验部分成果；需要PSD的原任务完整交付仍受阻。

资源不足先降并发、排队、选择已验等价执行路径或减少可选预览，不降低空间精度/分辨率/方法和必要成果规格。切换模型量化、上下文或视觉处理路径可能影响任务资格，需匹配实际Profile；探测不到可用RAM/VRAM就不承诺某大模型能稳定共跑。

## 8. 关键验收

未知TFM不兜底成功；安装声明与活动身份不符可停止；未实测宿主不靠脚本PASS升格；MCP不同版本能力逐合同验；PS重启/能力摘要变化/file token失效明确再核验；拒绝访问的资源探测保持UNKNOWN。

工作台测试事件重复/乱序/缺页、重连、旧窗口、执行/验证/对账/取消区分、必要文件缺失、中文/灰度/高DPI/键盘及无模型/离线/无PS。新手完成标准链、PS手工设计=0，首次配置/业务决定/异常另计。模拟UI事件不代替真实Pro/PS任务验收。

