---
name: Bug report / 问题报告
about: 报告可复现的缺陷（Create a report to help us improve）
title: "[bug] "
labels: bug
---

<!-- 只在本机回环范围内复现：MCP 端点默认 127.0.0.1:6520/mcp。请勿粘贴 token、密码、内网主机名或本机用户目录绝对路径（用 %USERPROFILE% 形态代替）。 -->

## 环境 / Environment

- 操作系统（含内部版本）: **Windows 11 x64, build ______**
- ArcGIS Pro 版本: **3.5.x（本项目实测 3.5.0.______）**／其他版本请写明（★3.x 以外不受支持，3.6／3.7 未在本项目实机验证）
- 使用的 payload: **net6.0-windows（最小宿主 Pro 3.0）** ／ **net8.0-windows（最小宿主 Pro 3.5.0）**
- .NET Runtime: **8.x**（分享包运行端）
- ArcGIS Pro 内置 Python 环境: **arcgispro-py3**
- 安装件与代际: **ArcGIS-Pro-MCP-OneClick-1.0.2-r__-Windows-x64.zip**・SHA256 `________`
- MCP 客户端与优先级: **Codex（P0）／Cursor（P1）／DeepSeek Harness（P1）／Claude Desktop（P2・optional）**

## 复现步骤 / Steps to reproduce

1.
2.
3.

## 期望结果 / Expected

## 实际结果 / Actual

<!-- 请附：插件内 MCP 页的 Start/Stop 状态、端口占用情况、`certutil -hashfile <zip> SHA256` 输出、以及插件日志目录中与故障时间相邻的片段（可脱敏）。 -->

## 日志片段 / Log excerpt

```text

```

## 是否影响安全边界

- [ ] 服务只监听 `127.0.0.1:6520`（未出现 `0.0.0.0`／公网暴露）
- [ ] 未新增 Python 或 `6511` 旁路
- [ ] 日志已脱敏（无 token、无凭据、无本机用户目录明文）
