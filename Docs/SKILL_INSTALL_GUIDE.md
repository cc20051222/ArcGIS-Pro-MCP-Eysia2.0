# Skill 插件与面板安装教程

0.7.1是2.0的配套候选；原版r29原样保留。新增插件只在ArcGIS Pro 3.5/net8本机实测，其它版本继续采用原版的兼容矩阵，不强装此配套插件。尚未证明干净机、所有客户端或所有Pro版本安装通过。

## 安装包组成

| 目录或文件 | 用途 |
|---|---|
| 01_原版r29 | 原版一键ZIP原样副本，首次基础安装和客户端连接入口 |
| 02_Skill | 0.7.1工作流Skill及引用、脚本、任务合同 |
| 03_Panel | 独立Windows观察面板，与Pro内面板共享任务文件 |
| 04_Plugin | 包含进度流、原生面板、明确质量检查的新插件构件 |
| 05_说明 | 安装使用教程与功能特色原理两份Word |
| 安装配套.ps1 | 默认只读Plan；可分别安装或InstallAll，带备份回执 |
| 启动配套.ps1 | 从D盘运行根启动Pro或独立面板，统一事件/任务/日志路径 |
| companion-manifest.json | 逐件大小与SHA256，安装前会验证 |

从[GitHub Releases](https://github.com/cc20051222/ArcGIS-Pro-MCP-Eysia2.0/releases)或[国内仓库](https://gitcode.com/hhhi6/ArcGIS-Pro-MCP-Eysia2.0)获取实际已上传版本，核对对应SHA256，再完整解压到D盘。不能只从ZIP预览双击脚本。

## 首次安装与升级

1. 首次用户先解压01_原版r29的ZIP，按其中教程运行原版 `ONE-CLICK-SETUP.cmd`，选择真实客户端并完成本机回环连接。已有可用r29和连接的用户可跳过这一步。
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
