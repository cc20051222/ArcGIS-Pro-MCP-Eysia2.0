# 聊天阶段卡、持续观测面板与内部进度流

2026-10-19｜当前配套0.7.1：沿用0.7.0进度构件，新增真实综合实验的三维科学核验与原生界面边界，修正水文候选描述并补齐启动器D盘临时路径。独立运行高亮、需求数据输出确认、替代优先和双显示已实现并实机检查。交付PASS CANDIDATE；[当前更新与证据边界](SKILL_071_RELEASE_NOTES.md)，之前各版本记录仅作历史。

## 实现与可见效果

聊天关键节点直接呈现“当前步骤、已核验结果、下一步、受阻项、观测时间”，产生真实预览时直接内联；不以阶段 prose 或结束后的文件链接抵扣用户过程观看要求。

原生 Windows 面板每 **500 ms** 更新一次视图，展示当前请求、内部阶段、事件时间与来源、历史事件及业务任务记录。并发时优先展示未返回的请求。暂停/关闭面板仅停止观看，不取消 GIS；超过 15 秒没有新事件会明确提示待核查，而不编造完成率。更新后的 Add-in 在 MCP 页签新增“任务面板”，直接读当前进程的事件缓冲，无需附加端口。

独立窗口只读进度 JSONL 和任务记录，不执行 GIS，不建立监听器。实际文件校验夹具已经用于验证运行中窗口；截图中标有“开发验收夹具／非 GIS 生产任务”，不能作为真实 GP 运行证据。

## 开发文件

| 位置 | 职责 |
|---|---|
| `Source/Shared/ArcGISProMCP.Core/Progress/` | SDK 无关的请求作用域、有界事件缓冲、D 盘 JSONL、半行容错读取 |
| `Source/Shared/ArcGISProMCP.Server/McpServer.cs` | 接收、执行、返回及错误事件，请求相关通知与能力声明 |
| `Source/Shared/ArcGISProMCP.Server/Transport/HttpMcpTransport.cs` | 同一现有端点上的可选 SSE，缓冲、逐条 flush、保活及 JSON 兼容 |
| `Source/ArcGISProMCP.Compatibility/Services/GeoprocessingExecutor.cs` | ArcGIS GP 真实回调；捕获作用域后在 SDK 回调中报告 |
| `Source/Shared/ArcGISProMCP.Core/Jobs/WorkflowJobStore.cs` | 作业已处理／成功／失败／跳过计数，成果核验另计 |
| `Source/ArcGISProMCP.Compatibility/Controls/ProgressPanelWindow.cs` | 本地窗口；同一源文件供 Add-in 与独立应用构建 |
| `Source/ArcGISProMCP.ProgressMonitor/` | 不依赖 ArcGIS SDK 的独立面板入口 |
| `Tests/ProgressPanelChecks/` | 无第三方测试包的协议/隔离/容错集成检查；夹具不进生产注册表 |
| `tools/progress/` | 构建及启动脚本，不安装、不重启 Pro |
| `.runtime/progress-panel/` | 本轮隔离构建、测试、运行记录及截图 |

源代码注册仍为 239 件，错误码 33、GP 白名单 53 不因进度功能增加。Shared 没有引入 SDK 引用。本轮按用户明确例外更新本插件安装位与缓存，Release 未改；没有执行 `dotnet clean`。

## 构建和打开

使用现有 SDK、vendored refs 和已有 NuGet 缓存；脚本禁止联网获取依赖，全部新缓存/obj/bin/临时文件在本项目 D 盘目录。若缺少现有依赖则失败，不自动安装。

```powershell
& 'D:\ArcGIS-Pro-MCP 2.0\tools\progress\build-panel.ps1' -Verify -BuildNet8
& 'D:\ArcGIS-Pro-MCP 2.0\tools\progress\start-panel.ps1'
```

第一条编译面板与 net6/net8 候选并运行独立测试；不会生成安装包或启用 Add-in。第二条打开可见窗口，不改变客户端或用户工程。可用 `-EventDirectory` 与 `-TaskRecord` 指定其它已授权 D 盘目录。更新正在运行的面板时先正常关闭面板再构建，不能结束 Pro。

## 进度事件与兼容性

新构件初始化结果在 `capabilities.experimental["arcgispro-mcp/progress"]` 声明进度扩展。缺失时仍按旧插件处理，不把工具数量当能力检测。

请求正常走 `/mcp` 的 registry、invoker 和所有守卫。客户端接受 `text/event-stream` 且在 `params._meta.progressToken` 传入 string/number，服务端才产生关联进度通知；没有 token 时仍返回原有 JSON。不能把 `_meta` 塞进工具 `arguments`。保留现有协议版本，不宣称完整实现最新 MCP 传输规范。

通知的 `progress` 单调增加，单位明确为**观测事件数**，不提供虚构 total；真实阶段计数在 `params._meta.execution.Completed/Total`，GP 百分比仅指当前 GP 阶段。最终 JSON-RPC 响应结束流；迟到回调被忽略。每 10 秒的 SSE 注释仅用于连接保活，不生成工作进度。网络缓冲满时可合并中间进度，最终响应保留。

GP 来源包括 OnBeginExecute、OnProgressMessage、OnProgressPos、OnValidate、OnEndExecute。没有百分比的工具显示阶段与未知总量，不能保证 239 件都有内部计数。作业项计数与全业务核验分开。超时、取消后可能已经产生效果，显示待核查，不自动重发写入。

依据：[Esri GP 回调接口](https://pro.arcgis.com/en/pro-app/3.3/sdk/api-reference/topic9391.html)、[MCP 2025-06-18 进度字段](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-06-18/schema.mdx)。这里采用请求级通知的字段合同，不暗中迁移原有取消或生命周期协议。

## 业务流程、聊天和质量状态

Agent 在关键节点更新同一份任务记录，并在聊天直接发送对应阶段卡。面板监听该记录的文件变化；内部事件并不自动将质量或验收置为通过。先检查→过程检查→首次结果→复检→最终交付检查→外部验收分别显示。任务记录是 Agent 录入的业务状态，工具事件是实际执行观测，两者来源分开。

当前开发记录是 `.runtime/progress-panel/task-record.json`。它不表示考试任务正在重跑。独立面板可同时观察开发事件与该记录；历史成图始终标历史版本。后台窗口不能自行改写旧聊天消息，也不代表所有客户端已渲染通知。

## 新构件启用与真实 GP 验收

独立面板可以直接运行；Skill 更新本身不启用二进制回调。初始旧进程的 C 盘模块证据在 live-loaded-assemblies.json；本轮随后获得仅两处本插件目录的 C 写入例外及工程保存/重启授权，已正常更新并实测。未扩大例外到其它项目路径，不覆盖现役种子或发布包。net8 包使用 3.5 引用集和匹配的包元数据，不能使用外层 3.0 元数据替代变体判断。

启用后在启动宿主前设置进程级环境（不写用户全局配置）：

```powershell
$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY = 'D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel\events'
$env:ARCGIS_PRO_MCP_PROGRESS_TASK_RECORD = 'D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel\task-record.json'
```

原生面板不依赖 JSONL；独立窗口依赖可读的事件目录。JSONL 会话上限 16 MiB，超限不删除历史，停止落盘并暴露记录错误；内存缓冲保留最近 2000 条。独立读取每次只取最近 5 份日志尾部，不是无限审计数据库。

真实验收应使用已获授权的新输出：开始前直接显示流程；运行中至少观察一次真实 GP 阶段/计数事件；产物返回后检查实际数据；展示真实图；复检及最终交付分别留证。保留原件。没有 OnProgressPos 的 GP 仍可显示真实阶段，但不得制造百分比。Pro/插件升级的许可来自根 AGENTS.md §6，不能由 build 成功替代。

## 测试结果与范围

最新证据见 `.runtime/progress-panel/DELIVERY.md`。协议夹具、文件校验和截图证明请求流和面板可以工作，不证明真实 ArcGIS GP 已验收。首次 Server 回归因生产端口占用失败，后将测试固定端口集中隔离为 16525，并为历史 fixture 改用本次测试根，最终复跑结果单独保存。开发检查临时端口 16524 与 16525 均在测试结束后释放；生产仍仅使用原来的 6520。

独立测试不关用户 Pro、不安装、不写真实客户端配置、不增加任意 Python/6511 旁路、不把工具成功自动升级为成果合格。提交的是开发候选，最终由有权验收方裁定。

缓存行为的补充依据：[Esri SDK Advanced Topics](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/conceptdocs/docs/ProConcepts-Advanced-Topics.html)说明插件 Install 内容会由 Pro 在启动时复制到 AssemblyCache；本机现值另有实际模块路径证据。本轮未找到并验证符合当前 C 盘禁写约束的重定向方案，不把最新文档等同于本机 3.5 的已验证配置能力。

## 本机部署预检补充（2026-10-18 17:32）

以下为例外授权前的调查与首轮候选，保留当时 BLOCKED 事实；实际启用状态和最终候选以末节为准。

本轮通过只读 PE 元数据/IL 检查本机 **Pro 3.5** 的 Framework DLL，不加载或调用厂商内部代码：`GetLocalAssemblyCachePath` 使用 `Environment.GetFolderPath(LocalApplicationData)`，拼接 `ESRI/ArcGISPro/AssemblyCache`；解压函数直接使用该路径。记录在 `.runtime/progress-panel/cache-path-il.txt`。这是一项针对当前二进制的方法检查，不宣称已证明所有版本、所有隐藏设置都不存在。

子进程仅修改自身 `LOCALAPPDATA` 后再次查询 Windows Known Folder，实际返回仍为 `C:/Users/Administrator/AppData/Local`；未创建探测目标目录。记录在 `known-folder-probe.json`。因此不能用简单环境变量方案启动 Pro 来冒险验证。文档中的 D 盘 Add-in 搜索目录与 AssemblyCache 是两层目录；前者不能替代后者。`UserConfigLocationLocal/Roaming` 对应 user.config 路径，也不构成缓存重定向证据。

`tools/progress/cache-inspect/` 为只读检查程序；`tools/progress/prepare-activation.py` 准备测试候选、原包回退副本和阻断预检，不执行安装、注册、缓存同步、重启或 GIS。全部输出在 `.runtime/progress-panel/activation/`；禁止输出路径经 junction/symlink 转向其它位置。

已实际生成：`activation/20261018-173157-81a7740d/ArcGISProMCP.Compatibility.esriAddInX`，1,096,555 字节，SHA256 `FEEF70F7EE4F14AEA7339D173831298972629D056CED53862A7BA8FBAE511F15`。20 个条目逐件 CRC/SHA-256 与输入一致；8 个项目 DLL、匹配的 deps、当前 Bridge 脚本、图标和含任务面板按钮的 DAML。DAML desktopVersion=3.5，Compatibility TFM=net8，7 个 SDK 引用均为 13.5.0.0。它是本机测试包，未包含分发教程/安装器，不是新正式发布。原安装包只读复制为同目录 `original-installed.esriAddInX`，未改原件。

`preflight.json` 的 BLOCKED 是真实结果：安装位在 `C:/Users/Administrator/Documents/ArcGIS/AddIns/ArcGISPro/{BAA5628C-3C08-4AD5-A6C0-915ADA475709}/`，运行缓存在 `C:/Users/Administrator/AppData/Local/ESRI/ArcGISPro/AssemblyCache/{BAA5628C-3C08-4AD5-A6C0-915ADA475709}/`。在目前已核实的部署机制下，启用新版需要更新这两处；严格禁写继续生效时停止启用，不自动建 C 盘 junction、改 Known Folder 注册表或注入运行进程。

现场 `get_project_info` 另外返回 `isDirty=true`（`activation-get_project_info.json`），因此不能强杀 Pro 或丢弃工程。启用之前须由用户保存工程，或明确允许保存当前工程，再正常关闭/重启；取消保存则停止，不绕过对话框。

后续实测顺序已冻结：确认路径例外（如用户选择允许）→核原包及回退副本→保留工程编辑→正常关闭 Pro、复核进程退出→更新本插件、正常启动并核实际加载哈希→initialize 真实发现进度能力、tools/list 仍为 239→在 `D:/shiyanwenjaijia/自测记录/进度面板实测/<新 run>/` 下先告知具体新输出、探针及输入/CRS/范围检查→通过正常 MCP 发单次受控 GP 请求→记录 SDK 来源事件及终端结果→数据读回/真实预览→以新观察复检→提交 PASS CANDIDATE。SDK 没有报告百分比时如实显示阶段；不增加伪计数、不把测试包构建当实机验收。

## 上轮实际启用与检查（历史，现值见末节）

用户明确允许仅更新本插件 `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` 的 C 盘安装目录/缓存，并允许保存工程后正常重启。安装已完成；当前 Pro PID=32996。工程在保存前、保存后各留 D 盘副本；正常退出复核 remaining=0 后再换包，不强杀。期间项目读取哈希遇到共享锁，已核已保存状态后使用只读共享文件流对账，没有重复保存或清除锁。

最终安装测试包：`.runtime/progress-panel/activation/20261018-173734-fd93ca32/ArcGISProMCP.Compatibility.esriAddInX`，**1,096,596 B / 20 条目 / SHA256 90ACBBACE1AE803BD8B49CAA9BCB45B2F0F42D6CF8D5F5C28252F92E13856EF2**。补齐既有/新增菜单的 8 处 keytip 后，使用本机 Pro 3.5 Framework.xsd 校验通过。DAML desktopVersion=3.5、TFM=net8、SDK refs=13.5.0.0。原包和全部缓存文件已在同目录保留回退副本/哈希清单；这不是新正式发行包，未修改 Release。

首轮直接启动出现原生 Fortran `CONOUT$` 写入错误，正常关闭错误进程后改用 Windows ShellExecute GUI 启动，工程与只读调用恢复正常；未改厂商 DLL 或数据。此为本机观察到的启动相关故障及修复，未宣称已完整证明所有原生错误根因。安装/启动历史见 activation-report.json、restart-after-console-error.json。后续可使用：

```powershell
& 'D:\ArcGIS-Pro-MCP 2.0\tools\progress\start-pro-progress.ps1' -StartMcp
```

启动器仅在没有 Pro 运行时启动既有工程；不替换安装、不改注册表/客户端配置。进度事件、managed log 留项目 D 目录，TEMP/TMP/ARCTEMPDIR、GP 审计和作业留用户 D 数据根。其它工程可显式传 `-Project`、`-DataRoot`，均限 D。直接使用普通系统快捷方式可能没有这些进程级覆盖；原生内存面板仍可用，外部日志配置不能靠 Skill 自动补上。

实机 `initialize` 已声明新进度能力，最新工具清单仍为 239；实际加载模块逐件哈希与候选一致。两次 Buffer 均走原 `/mcp`、原 registry/invoker/守卫，未增加接口或旁路。每次 **16 条通知 / 13 条 ArcGIS GP 来源事件**，token 匹配、观测事件计数单调，全部通知在最终响应前到达；真实 SDK 报告 1%–7%，没有补造 100%。

实测数据和报告：`D:/shiyanwenjaijia/自测记录/进度面板实测/20261018-173734-fd93ca32/`。20 m/25 m 两份缓冲各 6 个 Polygon，扫描 6/6、空几何 0、米制 CGCS2000，BUFF_DIST 分别全为 20/25，ORIG_FID 各 6 个唯一。SDK 重读与独立 DBF/SHP 读取一致；边界按本样例预期向四向扩展，1e-5 m 仅为文件数值比较容差，不代表测绘精度承诺。原输入 7 成员哈希不变。实际 PNG 预览已生成并查看，属于测试地图视图，不是带完整制图要素的正式考试成果。会话 GP 环境已 reset 并重读核对；测试图层留在当前地图，新增图层尚未保存回原 aprx。

最终证据是数据目录 `final-recheck.json`：**7/7 项限定范围检查通过，PASS CANDIDATE，外部验收待裁**。配套质量工具 `validate_geometries` 实机仍返回 NOT_IMPLEMENTED；更新前 DLL 亦有该占位。完整自相交/拓扑质检未通过，不将基本几何读回替代它，也不把 239 个注册工具升格为 239 个实机可用能力。

原生面板实际显示 SDK 历史百分比，已视觉查看 `native-gp-history-visible.png`。这张图是历史事件查看，不能称为运行态截图；同步截帧尝试因客户端参数错误及短任务错过窗口，没有取得有效运行态 GP 截帧。黑帧和未覆盖 GP 的帧保留为未采用记录，不列入通过证据。SSE 客户端逐事件接收时间证明内部事件在结果前送达；用户是否亲眼持续观看仍不由该证明代替。外部验收状态保持 PENDING。


## 几何、拓扑与运行态取证整改（0.5.1，上批历史）

当前本机已启用测试构件：SHA256 **07D06BE4826D265143A74C4546F76F6B4CD5779E031BA22C0D1CBFCF5C69A69F**，**1,115,343 B / 20 条目**；Pro 3.5 / net8 / SDK13.5，观测 PID52164。安装与回退证据：`D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel\activation\20261018-212728-26b45bd5`。用户授权保存工程、正常关闭，先复核 remaining=0，再仅更新本插件 C 盘安装/缓存；没有修改 Release 或生成正式发行包。独立 fresh-loaded-modules.json 重枚举的 7 个已加载项目模块与候选一致，未拿旧记录替代新身份。

已实现既有 `validate_geometries` 的空几何、自相交、短边、重复顶点，以及 `check_topology_rules` 的无重叠、无内部孔洞、自相交、单部件、线自重叠、线悬挂六类明确规则。实现入口为 `Services/D088GeometryQuality.cs`；所有 SDK 访问在 MCT，只读源数据，复用既有注册和路径守卫。没有新增工具、错误码、GP 白名单或旁路：注册239、错误码33、白名单53。其余质量/溯源方法仍有占位，不因本批两项修复而宣布全部可用。

适用性与完整性：线合法自接触允许；内交叉、顶点X穿越、零长度邻边包围的X均能检出。面包含和重复是重叠，边界接触不是重叠；面孔不误算外部件；无缝隙仅检查并集内孔，未指定业务边界不认证外围覆盖。选择、定义查询不参与检查，范围是底层完整要素类。全量上限10,000要素/成对工作预算2,000,000，证据前500条另保留总数；超限、样本、未知规则、被禁阈值均不能伪报全检通过。曲线保留真实SDK段，非简单单曲线或未分类曲线节点保守未完成；本次未实际取证真实曲线、旧SDK API缺失和预算耗尽。具体参数与判据见 Skill 质量参考。

| 检查 | 本次结果 | 实际范围 |
|---|---:|---|
| net6/net8编译 | 0警告/0错误 | 两套SDK编译，不等同两版本实机验收 |
| 相关契约/取消测试 | 39/39 | 原超时定位为缺少显式作业/审计目录，D盘配置后重测；旧失败TRX保留 |
| 真实MCP黑盒反例 | 36/36 | 24组夹具，含正确/缺陷/拒绝/抽样/N/A/报告覆盖保护；坏数据应检出缺陷 |
| 最终数据与截帧复检 | 14/14 | 源件不变、SSE成功/顺序、六帧窗口、三份成果全量质量与文件计数、环境恢复 |
| 独立真实只读复核 | 17/17 | 11项语义抽检＋原20m/25m与新3m成果六次重新读回；未重派GP |
| 真实GP运行截图 | 6/6 时间窗口有效 | 目视00/02/05；并非6张不同进度动画 |

实测与成果目录：`D:\shiyanwenjaijia\自测记录\几何拓扑整改\20261018-212728-7efb7fec`。黑盒记录：`quality-checks-212752641786/results.json`；最后复检：`final-recheck-213008637499/summary.json`；独立记录：`independent-review-212903427812/`。原20m/25m各6/6、新3m输出10,000/10,000完成四项几何检查，短边阈值用各自XYTolerance=0.001m，明确仅为数值健康检查，不是测绘精度承诺。道路独立缓冲的相互重叠是合法业务效果，适用拓扑仅选无自交；新压力输出另检查无重叠。SDK全量重读、独立SHP/DBF逐记录计数一致，所有120夹具文件哈希不变，原成果独立调用前后哈希不变，GP会话环境reset后与执行前一致。

本次唯一新 Buffer 请求 `quality-fix-live-buffer` 历时约3.4s，**17条通知/14条SDK事件**。截图 `running-gp-00.png` 至 `05.png` 显示真实请求和执行态；严格区间是SDK ObservedAt开始 **21:28:38.457795** 至结束 **21:28:41.873336**（本地+08），最后一帧仍早于结束约492ms。多次相同画面源于该阶段没有新消息，不补造递增百分比。原生截图右侧有 Pro 坐标变换提示，不遮挡关键状态；这不是正式地图排版验收。

观察器末尾发生一次 UIAutomation ElementNotAvailableException，原观察记录和成功终端响应均保留；六帧在异常前已落盘，随后独立按SDK时间核验，不重放写请求。截帧辅助脚本已修为捕捉UI观察异常并保留候选摘要；该异常后的捕捉恢复路径未另行重跑GP验证，不宣称已消除所有UI供应器异常。暂停/关闭面板不取消GIS，聊天旧气泡不会被面板自动改写；其它客户端渲染及用户是否从头持续观看不由截图代证。

本批最终独立技术验收记录：`D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel\quality-fix\independent-review.md`。验收仅覆盖以上两项质量工具、限定成果和运行态取证，不签署整个2.0项目、所有Esri规则、考试整套业务或特定行业标准。三份Skill同步0.5.1，任务面板读取更新后的现役任务记录；原历史失败与旧包证据保持留存。


最初考试实际成果追加复检：NoiseStrong/NoiseMedium/NoiseWeak各1/1全量四项几何与无自交/单外壳检查通过，15个核心成员前后哈希不变，追加 **9/9** 技术检查；证据 `D:\shiyanwenjaijia\自测记录\几何拓扑整改\20261018-212728-7efb7fec\exam-quality-213641556277\summary.json`。这与上述14/14进度/缓冲复检分别记账；不证明路灯、阴影、三维或完整考试已完成。原Skill自测报告已同步当前与历史边界。


最终签记：独立复核方 `/root/quality_independent_review` 已签 **SCOPED INDEPENDENT TECHNICAL ACCEPTANCE — PASS**，原件 `D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel\quality-fix\independent-review.md`。任务记录按该原件更新本批 S6 为 VERIFIED；不是执行端自批项目PASS，不覆盖G系裁定。


## 0.6.0 业务流程优先面板（当前现值，2026-10-18）

本次按用户要求将主视图改为完整业务处理步骤和质量闭环。Agent依据真实数据、用户目标、当前工具/许可、方法与适用标准先制定完整计划；面板是该计划和实际观测的持续展示，不以最近工具调用替代业务流程。原0.5.1独立验收只属于上批，本次交付为 **PASS CANDIDATE**，不借用旧裁定自批新版。

- 首屏：任务目标、方法选择理由、计划版本、全部步骤清单与依赖、已核验/执行中/异常计数和最近实际观测时间；不伪造整单百分比。
- 主区域：完整步骤列表；点击可看方法、输入/计划输出、检查重点、判据与来源、检查覆盖、结果和证据。自动跟随当前步骤，手动查看时停止跟随，可再次开启。选中项采用深色高对比边框，避免白底遮盖当前步骤。
- 右侧“检查与修复”：首检/复检/最终结果、全量/抽样及数量、失败原因、正在处置、受影响下游、新证据复检和历史检查；技术候选和外部验收分别展示。
- 右侧“内部进度”：SDK阶段、观测时间/来源、请求关联和原始事件辅助排查。仅实际绑定的requestId关联步骤，不能按同名工具乱配任务；较新执行不能沿用旧VERIFIED/质量通过。
- 面板500ms读取变化；Agent在实际开始、返回、检查、发现问题、修复、复检、计划变更和验收时发布。没有Agent新记录或SDK回调时显示最后观测/未知，不承诺旧聊天气泡自行流式改写。

运行与发布合同：Skill **0.6.0**、schemaVersion1.2兼容原记录，新增可选kind/method/inputs/outputs/checkFocus/requirementIds/requestIds；`publish_workflow.py`复用既有质量校验器派生workflowView，在已配置D盘任务记录原子更新。检查依据/版本/覆盖不足不能通过；RUNNING须有实际派发观测；步骤须满足依赖，修复分支允许对失败/失效依赖处置。单写锁、显式任务切换与预期taskId、防旧版本倒退、保留旧观测/失败检查、D盘内容寻址历史均已加入。发布器不执行GIS或修复，不给新写入授予权限。

目前发布方式要求Agent具备本机D盘文件访问与Python能力，并知道启动时已配置的任务记录路径；仅有远程MCP工具而无文件能力的客户端，尚无专用业务计划发布MCP入口，不宣称所有客户端已打通。普通用户无需编辑JSON，当前本机由Agent维护记录。跨客户端推送/显示仍须各自实证。

水文方法参考已新增到Skill：16步范例含资料与标准预检、DEM预处理、洼地诊断/填洼Fill、流向、汇流累计、阈值敏感性、河网提取分级、矢量化、出口点、流域/子流域、指标、首检、修复、复检、最终检查、外部验收。区分指定出口D8/Watershed与全域Basin，保留真实洼地、上游范围、单位/格网和河网阈值依据。Esri方法文档不代替行业标准；没有适用标准条款不宣称行业合格。当前239工具和53白名单未覆盖传统水文核心入口，完整流程可规划，缺口在真实任务中必须BLOCKED，不代表本批实现了水文算法。

### 本次验证与实机现值

| 项目 | 结果 | 证据范围 |
|---|---:|---|
| net6/net8插件与独立面板 | 0警告/0错误 | 本机隔离编译，不等同跨版本实机 |
| 进度协议与业务步骤关联 | 16/16 | 精确请求关联、不能自批、较新执行使旧检查失效及既有进度回归 |
| 质量契约/取消回归 | 39/39 | 原两项质量实现未退回占位 |
| Server回归 | 68/68 | 16525隔离端口；测试监听结束remaining=0 |
| Skill离线 | 88/88辅助测试、8项检查 | 新发布器的依赖/锁/路径/任务切换等；非82个真实模型行为评测 |
| 水文显示闭环 | 4个实际窗口状态已截帧 | 计划→失败/修复中→复检中→问题关闭；全部醒目标SYNTHETIC，不是实际水文执行 |
| 真实Pro完整质检流程 | 两轮各12次只读调用 | 9个业务步骤，首检与新证据复检，15个源成员SHA不变；未检出问题则有据跳过修复 |

最终本机测试包：**1F827D71F97AB363552510C4FBD49FB311EA605FFF36DCFA8C41745B4F2E29E5 / 1,124,211 B / 20条目**。安装与回退：`D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel\activation\20261018-224614-e762b5ce`。进程观测PID **56076**；7个实际加载模块与候选逐件哈希匹配，初始化进度能力存在、tools/list239。DAML与此前Pro3.5 XSD校验过的载荷逐字节一致。未修改Release、不制作正式发行包、不扩53白名单。

继续沿用用户已明确的例外：仅本插件C盘安装/AssemblyCache更新，其余开发、备份、数据、日志、临时均在D。正常退出曾遇Process.CloseMainWindow拒绝与UIA关闭未完成，未强杀或覆盖加载中的DLL；状态变化时保留现场，重新备份/保存当前工程，再通过身份核验的Pro标题栏正常关闭，复核remaining=0后启用。新原生面板明确归属Pro主窗口；截帧工具同时识别owned窗口与独立窗口，避免把找不到顶层窗口误判为面板没打开。

最终实际流程数据/请求/前后SHA：`D:\shiyanwenjaijia\自测记录\业务流程面板\20261018-224654-final`。当前面板记录仍是实际噪声三区复检，不是开发验收S1/S2或水文假作业。已保留历史任务快照。开发验证与图片：`D:\ArcGIS-Pro-MCP 2.0\.runtime\progress-panel\workflow-panel`；`native-final-workflow.png`为实际业务流程，`native-final-quality.png`为实际质量结果；`actual-workflow-01.png`保留第一轮真实执行中步骤，`final-workflow-01.png`是最终构件上的实际处理中快照（已核验中区，尚未执行弱区），不能混称都为运行中。

水文演示的 `hydrology-final-plan.png`、`hydrology-final-repair.png`、`hydrology-final-rechecking.png`、`hydrology-final-closed.png`均保留演示标签；没有拿它们证明实际水文/实际GIS修复/行业验收。当前真实数据没有缺陷，因此真实修复执行本轮未触发；问题修复和闭环显示以明确标识的夹具验证。全部记录只是可复核观测，制作端不自签新版独立验收。

## 0.7.0 · 2026-10-19：资料问询、能力规划与独立执行高亮

本批由用户直接授权，交付 PASS CANDIDATE；不沿用旧版独立验收结论。新模板 schema1.3 必须有 intake；旧schema1.2只保留兼容展示。需求、数据路径、输出根引用用户观测，未知项集中问询；已经明确的值复述沿用。资料缺口逐项列用途/规格建议/影响步骤，缺必需资料阻断依赖操作。处理、检查、复检和修复均需可执行工具方案；缺工具先查科学等效替代，写等效条件和限制，确需开发先获用户明确同意，同意不表示新工具已可执行。

面板顶部显示当前执行步骤，所有RUNNING行使用独立底色、左条和文字标记，不随选中行消失；没有运行观测时不把最后完成项冒充执行中。新增资料与交付页，和聊天状态板显示同一份需求/资料/输出位置/缺口/工具选择/实际成果。发布器仍不执行GIS；聊天更新由Agent发送，不冒称后台会自动修改聊天。

本机net6/net8/独立面板编译均零警告零错误；97项Skill辅助检查、16项进度检查、7项配套安装检查通过。真实Pro当前239工具，最终三区12次只读质检及复检通过，15源成员哈希未变；另保留早先同版12次回归，不计为独立验收。压力观测两次90请求及一次300请求共480次，均是重复只读界面压力测试，不是480种能力覆盖。合格运行截帧采用 native-running-live300.png，观测11:27:42.814+08，早于实际完成11:27:47.514+08；早先截图与真实结束时间不一致的材料保留为非合格运行截帧，不抵扣证据。

候选插件SHA256 EFF400148D6E9BA95FCC5C887108983514A150156212F85429EB208355A316A9，1,126,260B，20条目；仅Pro3.5/net8本机实测。原版r29保持099A4F3A575A65A1A8F9A7F424414465E676E605B12D8C1B9A7BA36E6F36B3CE，2,278,077B，未覆盖。

完整配套目录：D:/ArcGIS-Pro-MCP 2.0/配套交付/ArcGIS-Pro-MCP-2.0-Skill与面板-0.7.0候选。01_原版r29为原包副本；02_Skill为19文件Skill；03_Panel为独立窗口；04_Plugin为本次Pro3.5候选构件；05_说明含两份Word（安装使用教程、功能特色与实现原理，各6页，已逐页检查）。安装配套.ps1提供Plan/分项安装/InstallAll/按事务Restore，默认拒绝不同文件覆盖、保留备份；不修改真实客户端配置或安装依赖。原包先装、配套后装，避免降回旧构件。

安装只在用户已允许的本插件GUID AddIns/AssemblyCache两处C目录更新，其它产物、临时文件与事务记录在D；原Release和旧仓库不改。几何/拓扑仍为明确实现范围，不声称全部Esri拓扑、完整考试或传统Fill/D8/Watershed全链已经完成。该配套没有给仅远程MCP客户端新增任务发布工具。

最终业务检查报告：D:/shiyanwenjaijia/自测记录/业务流程面板/20261019-070-final/actual-result.json；原始任务与检查记录：同目录actual-task.json。工程数据未改写。最终证据：.runtime/progress-panel/intake-070；现役清单：.runtime/progress-panel/delivery-manifest.json；新版独立验收待完成。

