# r5 使用指南独立验收

日期：2026-09-10

结论：DOCUMENT PASS。仅批准安装与使用指南 V1.0 作为 r5 用户实机试用的独立配套文档，不升级产品、真实安装、真实客户端或 clean-machine 的验收状态。

## 固定交付身份

- DOCX：`Docs/ArcGIS_Pro_MCP_r5_安装与使用指南_V1.0.docx`
- DOCX SHA256：`6BCF5A7775DA5AA4289123D7079F6F85B28191F1C9D644ECCBF14657A415897D`
- Markdown SHA256：`C683CD9917ED6681403709BE778B8C65A516C28F6204F54DD4AE5799E1FC4374`
- r5 ZIP SHA256：`3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652`，本轮复核未变化。

## 独立复核

最终渲染：`.codex-artifacts/document-build/render_word_content_gate_20260910_v3`，20 页。采用已记录的 Word 只读渲染 fallback。

前轮已检查全部20页。本轮逐张重新检查变化的1、3、13至20页；2、4至12页PNG SHA256与已检查v2完全一致。未发现文字裁切、重叠、缺字；原13页孤立标题及表头已与数据一起置于14页。封面内部QA说明和末页文档候选状态已删除。

此前内容修订已闭环：追加客户端不重复部署，采用独立Validate/ClientApply并明确写入边界；旧r4包内指南冲突提前警告；上下文错误不计成功只读调用；固定r5功能与后续测试harness修订分开。工具列表保留30个生产工具及已知限制，排除mcp_auth。

## 未升级的状态

本批准不证明新电脑安装、真实AI客户端新会话连接、重启后复测或恢复/卸载成功。上述仍需按指南实机清单提交fresh evidence。不得将NOT VERIFIED、BLOCKED或隔离测试结果改称实机PASS。

分享时将本指南作为r5 ZIP旁的独立文档发送；它不在已固定的ZIP中。包内旧r4指南不作为r5身份依据。本轮未执行部署、真实配置修改或GIS写入。
