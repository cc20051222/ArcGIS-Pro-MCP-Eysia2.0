# ArcGIS Pro MCP 2.1 安装升级教程

当前发布2.1，插件产品版本2.1.0，Skill版本0.7.7。面板功能已按用户授权完成本机验收，新发布载体验证另见[2.1摘要](RELEASE_2_1_VALIDATION.json)。本版安装器仅适用于ArcGIS Pro3.5/net8、Windows x64；未证明干净机或所有客户端安装通过。历史Word手册针对0.7.1，涉及本版路径与步骤时以本教程为准，旧r29原样保留。

## 安装包组成

| 目录或文件 | 用途 |
|---|---|
| 安装2.1.cmd | 启动交互安装，先显示计划，再明确确认安装 |
| 02_Skill | 0.7.7工作流Skill及引用、脚本、任务合同 |
| 03_Panel | 独立Windows观察面板，与Pro内面板共享任务文件 |
| 04_Plugin | 包含进度流、原生面板、明确质量检查的新插件构件 |
| 05_说明 | 当前安装和使用教程、2.1更新说明、验证摘要 |
| 安装配套.ps1 | 默认只读Plan；可分别安装或InstallAll，带备份回执 |
| 启动配套.ps1 | 从D盘运行根启动Pro或独立面板，统一事件/任务/日志路径 |
| companion-manifest.json | 逐件大小与SHA256，安装前会验证 |

从[GitHub Releases](https://github.com/cc20051222/ArcGIS-Pro-MCP-Eysia2.0/releases)或[国内仓库](https://gitcode.com/hhhi6/ArcGIS-Pro-MCP-Eysia2.0)获取实际已上传版本，核对对应SHA256，再完整解压到D盘。不能只从ZIP预览双击脚本。

## 首次安装与升级

1. 下载2.1安装ZIP和同名.sha256，核对后完整解压到D盘新目录。首次安装或从2.0升级均可直接双击`安装2.1.cmd`，按提示指定运行根及AI项目工作区，核对计划后确认；无需先安装r29。已有客户端连接可保留，首次连接手动添加 `http://127.0.0.1:6520/mcp`，安装器不改写客户端配置。
2. 保存并正常关闭ArcGIS Pro。配套安装器检查相关进程与监听；不会用强杀代替保存，不会删除未知锁。
3. 在配套根目录打开PowerShell，先运行Plan，检查工作区和运行根，再实际安装：

```powershell
& '.\安装配套.ps1' -Action Plan `
  -WorkRoot 'D:\MCP运行记录' -AgentWorkspace 'D:\你的AI工作区'

& '.\安装配套.ps1' -Action InstallAll `
  -WorkRoot 'D:\MCP运行记录' -AgentWorkspace 'D:\你的AI工作区' `
  -UpdateExisting -AllowOnlyThisAddinOnC
```

`InstallAll`安装插件、Skill与独立面板；也可选InstallSkill、InstallPanel、InstallPlugin。`-UpdateExisting`表示允许在先备份后更新不同文件；默认拒绝覆盖。新配套安装后不要再运行原版插件部署，否则会降回旧构件。

双击入口为本次PowerShell进程指定ExecutionPolicy，不更改CurrentUser/LocalMachine策略，组织组策略仍可能阻止未签名脚本。若受组织策略阻止请联系管理员审核，不永久关闭安全防护。发布包无可信发布者签名，先校验SHA256并审阅脚本。

4. 客户端打开同一个AI工作区并重新加载Skill。Codex项目发现位置是 `.agents/skills/arcgis-pro-2-workflows`；其它客户端要按自身技能机制导入，不能承诺复制后自动加载。
5. 用启动器启动已有D盘工程：

```powershell
& '.\启动配套.ps1' -Mode Pro -WorkRoot 'D:\MCP运行记录' `
  -Project 'D:\工程\你的工程.aprx'
```

在Pro的MCP页签启动服务器并打开任务面板；独立面板可选：

```powershell
& '.\启动配套.ps1' -Mode Panel -WorkRoot 'D:\MCP运行记录'
```

## 路径与依赖

插件只能更新当前用户Documents/ArcGIS/AddIns/ArcGISPro下本插件GUID `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` 与LOCALAPPDATA/ESRI/ArcGISPro/AssemblyCache对应GUID两处。显式开关只允许这两处C目录，不允许其它C盘项目文件。工程、数据、运行记录、备份和临时文件在D盘；用户指定业务根后，业务产物与中转归入该根。

独立面板需.NET 8 Windows Desktop Runtime，Skill本地脚本需Python 3.10+；Pro需相应许可。脚本不自动安装软件或依赖，不更改真实客户端配置，不下载任意算法。已有Pro进程不会继承新shell变量，必须保存正常重启后核实际日志和事件路径。

## 安装后怎么确认

按层验证：安装回执文件哈希→Pro加载实际构件→initialize进度能力→当前tools/list→已知安全的只读调用→本次任务记录发布→面板与聊天关键节点一致→真实业务输出检查。安装成功不等于整个GIS任务通过，239个工具被列出也不等于239个实现全部验收。

每次更新的回执与备份在 `WorkRoot/transactions/.../receipt.json`。有错误先对账，不盲目重试写入。需要回滚时保存并正常关闭Pro，指定原事务：

```powershell
& '.\安装配套.ps1' -Action Restore -WorkRoot 'D:\MCP运行记录' `
  -Receipt 'D:\MCP运行记录\transactions\实际事务\receipt.json' `
  -AllowOnlyThisAddinOnC
```

恢复仅针对登记文件，且当前哈希必须仍匹配该事务的安装后版本；用户后改或未知文件保留。部分失败的事务不承诺无条件自动恢复。没有签名或可信签名链时如实披露，不建议永久关闭组织执行策略或安全防护。
