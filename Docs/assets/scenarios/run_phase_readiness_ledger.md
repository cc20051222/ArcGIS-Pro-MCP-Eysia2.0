# 运行阶段资产预备 · 60 场景/36 模板就绪台账 — 资产侧交接件

> 版本：v0.1（2026-09-30，**用户口头授权生产，非指挥席批次派单**）
> 定位：M4 目录收官（G-223）后，为"定义态 → 运行/实现阶段"准备的**资产侧前置台账**。
> 纪律：**定义 ≠ 可运行**。本件不触发基线推进、不占 D 编号、不伪造工具实现/运行证据；阈值全引用 rules（73 集），不自定义。

---

## 一、总览（计数闭合）

| 面 | 场景 | 模板 | 输入资格（冻结面口径） |
|---|---|---|---|
| M1 | 6 | 6 | 常规制图 5 ＋ TS08 合格输入缺口 1 |
| M2 | 22 | 16 | NOT-REQUIRED 15 ＋ PENDING-DOMAIN 7 |
| M3 | 5 | 0 | 轻度 1＋数据/宿主 1＋组合 1＋域能力 1＋跨线 1 |
| M4 | 27 | 14 | 全 NOT-AVAILABLE-PENDING-USER |
| **合计** | **60** | **36** | — |

- rules：8＋17＋22＋26＝**73**；场景侧档案 SF：18＋48＋15＋81＝**162**；压力本地：12＋16＋14＋18＝**60 行**。
- 四锚字节不改：M1 `92ECD265…`/M2 `501A7E15…`/M3 `94577E76…`/M4 `F71BF6CD…`。

**前置性质标签（资产侧描述，非判据阈值）**：
- `制`＝输入无特殊域要求，运行仅差用户工程数据＋真机（工具实现以注册表为准）。
- `域`＝域能力/许可前置（PENDING-DOMAIN，工具未实现或白名单/许可未成就）。
- `数`＝特定合格输入缺口（NOT-AVAILABLE-PENDING-USER）。
- `跨`＝跨线（L4 PS）或未批准生成器（Open XML）。

---

## 二、60 场景全量就绪台账

### M1（6）
| 场景 | 名称 | 主模板 | 性质 | 运行前置 |
|---|---|---|---|---|
| S19 | 规划区位现状图 | TP01 | 制 | 用户工程/边界数据；制图工具链已验收 |
| S20 | 用地/约束专题图 | TP02 | 制 | 用地分类数据；配色/排序 |
| S07 | 设施直线覆盖 | TP03 | 制 | 设施点＋缓冲半径；buffer 工具 |
| S22 | 科研分级设色图 | TS02 | 制 | 值字段＋分类方法 |
| S18 | 多波段专题合成 | TS08 | 数 | 合格多波段影像（G-205，multi.tif 非合格） |
| S23 | 研究区域系列图册 | TS01 | 制 | 分区边界＋系列页配置 |

### M2（22）
| 场景 | 名称 | 主模板 | 冻结面输入资格 | 性质 |
|---|---|---|---|---|
| S01 | 文件夹资料体检入库 | TP08 | NOT-REQUIRED | 制 |
| S02 | 多源坐标统一 | TP05 | NOT-REQUIRED | 制 |
| S03 | 属性/几何重复审查 | TP06 | NOT-REQUIRED | 制 |
| S04 | 值域与子类型治理 | TP17 | NOT-REQUIRED | 制 |
| S05 | 数据版本变更审计 | TS03 | NOT-REQUIRED | 制 |
| S06 | 带依赖检查交接包 | TP10 | NOT-REQUIRED | 制 |
| S08 | 真实路网服务区 | TP04 | PENDING-DOMAIN | 域 |
| S10 | 规划叠加冲突 | TP06 | NOT-REQUIRED | 制 |
| S11 | 邻接与格网统计 | TS05 | PENDING-DOMAIN | 域 |
| S12 | 空间自相关与热点 | TS06 | PENDING-DOMAIN | 域 |
| S13 | DEM 坡度坡向地形 | TS07 | PENDING-DOMAIN | 域 |
| S15 | 分区土地类别统计 | TP09 | NOT-REQUIRED | 制 |
| S16 | 前后期栅格变化 | TS04 | NOT-REQUIRED | 制 |
| S21 | 方案 A/B 对照 | TP07 | NOT-REQUIRED | 制 |
| S26 | 规划汇报自动成稿 | TP10 | NOT-REQUIRED | 制 |
| S27 | 科研插图自动成稿 | TS09 | NOT-REQUIRED | 制 |
| S28 | 屏幕/打印双版本 | TP09 | NOT-REQUIRED | 制 |
| S29 | 换数据保留设计意图 | TS03 | NOT-REQUIRED | 制 |
| S30 | 外部修改保护与验收 | TP10 | NOT-REQUIRED | 制 |
| S46 | 独立分类精度评估 | TS15 | PENDING-DOMAIN | 域 |
| S52 | 量测误差传播 | TS10 | PENDING-DOMAIN | 域 |
| S60 | 同源报告/PPTX 自动交付 | TP10 | PENDING-DOMAIN | 跨 |

### M3（5）
| 场景 | 名称 | 主模板 | 性质 | 运行前置 |
|---|---|---|---|---|
| S09 | 设施最短路线/服务范围 | TP04 | 域 | P5 求解器＋Network Analyst＋网络数据集 |
| S14 | 流域与汇流分析 | TS07 | 制/域 | 水文 GP 已在白名单；差合格 DEM（Z 基准）＋Spatial Analyst |
| S17 | 点位像元值采样 | TS01 | 制 | 门槛最低；点/栅 CRS 一致 |
| S24 | 固定分类时间对比 | TS04 | 制 | 共网格配准＋固定分类体系（组合前提） |
| S25 | GIS 语义素材导入 PS | TP10 | 跨 | GIS 半边可闭合；PS 组稿归 L4 |

### M4（27，全 NOT-AVAILABLE-PENDING-USER）
| 场景 | 主模板（次） | G-DOMAIN 域 | 性质 |
|---|---|---|---|
| S31 | TS09（TS12） | space-time-trends | 数 |
| S32 | TS12 | space-time-trends | 数 |
| S33 | TS11 | space-time-trends | 数 |
| S34 | TS06 | space-time-trends | 数 |
| S35 | TS09 | space-time-trends | 数 |
| S36 | TP14（TS13） | three-dimensional | 数 |
| S37 | TP15 | three-dimensional | 数 |
| S38 | TP14 | three-dimensional | 数 |
| S39 | TP16 | three-dimensional | 数 |
| S40 | TS13 | point-cloud-terrain | 数 |
| S41 | TS07 | point-cloud-terrain | 数 |
| S42 | TS14 | point-cloud-terrain | 数 |
| S43 | TS03 | point-cloud-terrain | 数 |
| S44 | TS08 | remote-sensing-models | 数 |
| S45 | TS15 | remote-sensing-models | 数 |
| S47 | TS08 | remote-sensing-models | 数 |
| S48 | TS08（TS15） | remote-sensing-models | 数 |
| S49 | TS09（TS12） | remote-sensing-models | 数 |
| S50 | TS18 | science-decision-uncertainty | 数 |
| S51 | TS16 | science-decision-uncertainty | 数 |
| S53 | TP18 | science-decision-uncertainty | 数 |
| S54 | TS17 | science-decision-uncertainty | 数 |
| S55 | TP04 | network-facility | 数/域 |
| S56 | TP12（TP17） | network-facility | 数/域 |
| S57 | TP13 | network-facility | 数/域 |
| S58 | TP11 | network-facility | 数/域 |
| S59 | TP14 | local-interchange | 数/跨 |

> 跳号 S46/S52 已在 M2 定义（非 M4）；编号 S01–S60 中未列号即不存在，不冒名补写。

---

## 三、36 模板运行就绪条件（按面）

- **M1 六模板（TP01/02/03、TS01/02/08）**：前 5 个为布局/制图，工具链（create_map/layout、add_layer/legend/north/scale、export_pdf）已验收，运行待用户工程；TS08 待合格多波段输入。
- **M2 十六模板（TP04-10、TP17、TS03-07、TS09、TS10、TS15）**：
  - 数据治理/制图类（TP05/06/08/09/10/17、TS03/04/09）输入无特殊域要求，运行待真机与对应工具实现核对；
  - 域类（TP04 网络、TS05 邻接、TS06 热点、TS07 地形、TS10 误差、TS15 精度）依赖域许可/求解器/统计能力。
- **M4 十四模板（TP11-16、TP18、TS11-14、TS16-18）**：全部依赖八域合格输入与宿主扩展，当前零合格输入。

> 工具"已实现/未实现"以 L1 注册表（Composition）为唯一依据；资产侧不据模板定义推断工具已存在（KDA/OSV 纪律）。

---

## 四、G-DOMAIN 八域运行前置（每域须集齐 GDOM-01…04 四件）

| 域 | 合格输入 | 许可/能力前置 | 今日 |
|---|---|---|---|
| space-time-trends | 带日历时间序列、已知变化/预测答案 | 时空立方体/预测 typed 操作（BRG-001） | 缺 |
| three-dimensional | 控制高程＋垂直基准面 | 3D Analyst；视域/LoS/剖面/土方 | 缺 |
| point-cloud-terrain | LAS/LAZ/COPC＋参考地面 | 点云读取/分类/派生（无字面） | 缺 |
| remote-sensing-models | 公布 QA 多波段＋参考掩膜/模型登记 | Image Analyst；MRC-001 模型门 | 缺 |
| science-decision-uncertainty | 观测＋公布设计/折面 | Geostatistical；采样/插值/集合 | 缺 |
| network-facility | 网络数据集（阻抗/方向） | Network Analyst；白名单 0/53 求解器 | 缺 |
| local-interchange | 交换标准版本＋容器适配器 | 离线包/往返/符合性 | 缺 |
| same-source-report | — | 已批准 Open XML 生成器（SRP-001） | 缺 |

---

## 五、全局缺口清单（运行前必须成就，按解锁权归属）

| 缺口 | 归属 | 关联 |
|---|---|---|
| 六类宿主扩展许可探测（3D/SA/Image/Geostat/Network/模式挖掘） | 真机＋用户 | 全域 |
| 八域合格输入 | 用户提供 | M4 全部、S18 |
| GP 白名单扩项（缺口 12 名，0/53 网络求解器） | 用户裁定 | 网络/3D |
| Python Bridge typed 审查 | 指挥席＋用户（BRG-001） | S33/S48 |
| X07/X08 合并裁定 | 指挥席（SCN-001） | TP14 |
| Open XML 生成器批准 | 用户＋指挥席（SRP-001） | S60/同源报告 |
| L4 PS 组稿 | 他线（不代行） | S25 |
| 在线发布三件 | 用户红线（G-190） | 发布 |

---

## 六、建议的运行准入顺序（资产侧建议，非裁定）

1. **先跑门槛最低、纯制图/治理的 `制` 类**（M1 五场景、M2 NOT-REQUIRED、M3 的 S17/S24）：积累真机渲染/导出证据，验证布局工具链。
2. **再成就数据/宿主前置**（S14 合格 DEM＋Spatial Analyst）：水文/地形族。
3. **域能力随实现批解锁**（网络 P5/3D/点云/遥感/决策）：每域须 GDOM 四件齐全方可记完成。
4. **跨线与发布最后**（L4 PS、Open XML、G-190）：依赖他线/用户红线。

> 是否按此顺序、何时由定义转入运行，由指挥席与用户裁定；本件仅提供资产侧就绪视图。

## 七、[VERIFY] 与免责

- 本件为**定义/台账**：无任何工具调用、渲染、导出、评测；不声称任一场景已可运行。
- 生产系用户口头授权，**未改动** L3_INBOX 的 WAITING 状态、未伪造 D 编号派单/验收、未触碰四锚与 M1–M4 冻结资产、未写入 Source/Tests/Config/Benchmarks。
- 所有"性质/前置"标签为资产侧描述；判据与阈值一律以 rules 73 集及后续 L2 冻结面为准。
