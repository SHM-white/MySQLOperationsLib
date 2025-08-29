# HealthGuardian

一个“事件驱动 + 规则引擎 + LLM 润色” 的办公健康辅助服务：采集（久坐 / 喝水 / 环境 / 主观状态）→ 聚合分析 → 结构化建议 → 大模型生成自然语言提醒 → 钉钉推送 & 用户反馈闭环。

> 本根文档汇总通用架构与概念；各语言实现（C# / Go）中的具体技术栈、目录结构、代码示例请分别查看：
> - C# 版本: [`server/CSharp_Edition/README.md`](server/CSharp_Edition/README.md)
> - Go 版本: [`server/Go_Edition/README.md`](server/Go_Edition/README.md)

---

## 🎯 目标与价值
| 维度 | 说明 |
|------|------|
| 用户价值 | 低侵入、上下文相关的健康提醒（而非固定时间广播）|
| 技术路线 | 统一事件 Schema + 规则生成槽位 + LLM 语言润色 |
| 可解释性 | 规则产生结构化 slots，LLM 仅组织语言，减少幻觉 |
| 可扩展 | 分层/接口隔离，可替换数据源、模型、推送通道 |
| 降本 | 合并调用、缓存画像、模型降级（模板 / 轻模型）|

## 🤝 竞赛 / 场景对齐（AIoT & 华为云）
| 评分维度 | 落地策略 |
|----------|----------|
| AIoT 场景 | 桌面 Agent / IoT 设备（姿态、环境）上报事件，云端聚合与推送 |
| 华为云组件 | CCE/K8s、RDS(PostgreSQL)、OBS 归档原始日志、IoTDA 设备接入、ModelArts 训练轻量分类模型、CloudEye + APM 监控；可选 Ascend 部署开源模型 |
| 创新 | 规则可解释 + LLM 自然语言润色 + 反馈闭环（接受率统计）|
| 成本控制 | Token 计费监控、调用合并、缓存、降级模板 |
| 数据治理 | 统一事件表 + 聚合指标 + 用户画像 | 

## 🧱 数据采集与统一事件模型
事件类型（初期）：POSTURE_IDLE、WATER_INTAKE、MEETING、ENVIRONMENT、MANUAL_STATUS。

统一事件 Schema：
```json
{
	"id":"uuid",
	"user_id":"u123",
	"type":"POSTURE_IDLE",
	"value":{"duration_min":45},
	"ts":"2025-08-28T09:15:00Z",
	"source":"desktop_agent|iot_device|dingtalk|manual",
	"confidence":0.9
}
```

采集多源融合策略：桌面心跳/键鼠活动、指令输入(`/water`)、模拟或真实会议数据、IoT 传感器温湿度、用户自述状态。

## 🔄 规则 + LLM 混合建议流水线
1. 原始事件写入 `events`。
2. 定时聚合 `daily_metrics`（久坐总时长 / 最长连续 / 水摄入 / 环境异常次数等）。
3. 表驱动规则 (`configuration_rules`) 匹配 → 生成结构化槽位 JSON：
	 ```json
	 {"slots":[{"type":"break","reason":"连续久坐85分钟","action":"起身活动5分钟","cooldown_min":30}]}
	 ```
4. 构造 Prompt：画像 + metrics + slots → 调用 LLM。
5. 校验输出（JSON / 行数 / 违规词）→ 推送钉钉。
6. 用户反馈 `/ok|/skip|/nothelpful` 写入 `recommendation_feedback`；统计接受率反哺规则/排序。

## 🗄️ 核心数据库表（PostgreSQL）
```sql
events(id uuid pk, user_id text, type text, value jsonb, ts timestamptz, source text, confidence real);
daily_metrics(user_id text, day date, sit_total_min int, sit_longest_min int, water_ml int, env_hot_count int, updated_at timestamptz);
recommendations(id uuid pk, user_id text, day date, slots jsonb, generated_text text, created_at timestamptz);
recommendation_feedback(reco_id uuid, user_id text, action text, ts timestamptz);
user_profile(user_id text pk, baseline_sit_avg_min int, preferred_language text, created_at timestamptz, updated_at timestamptz);
configuration_rules(id serial pk, rule_type text, condition jsonb, cooldown_min int, enabled bool);
```
规则条件示例：`{"type":"POSTURE_IDLE","metric":"continuous_min","op":">=","value":60}`

## 🧪 LLM Prompt 设计原则
| 原则 | 说明 |
|------|------|
| 最小必要上下文 | 仅传画像摘要 + 当日关键指标 + slots |
| 结构化输出 | 要求 JSON/分条；解析再发送，防止模型添加多余信息 |
| 安全约束 | SYSTEM 中禁止医疗诊断 / 不臆造未给出的数据 |
| 降级策略 | Token 超阈值 → 仅模板化组合（不调用 LLM）|

简化示例：
```
SYSTEM: 你是办公健康助手，只能使用提供的数据，禁止医学诊断。
FACTS: 久坐=230min; 最长连续=85min; 喝水=400/1500ml; 温度=28℃; 状态=困倦
SLOTS: break(...); hydration(...); environment(...)
STYLE: 简洁中文，列点。
OUTPUT: JSON {"messages":["..."]}
```

## 🚀 MVP 迭代路线
| 阶段 | 里程碑 |
|------|--------|
| 1 | 最小 HTTP 服务 + 健康检查 + 钉钉主动发送 |
| 2 | 回调验签 & LLM 接口封装（单轮对话）|
| 3 | 事件写入 / 聚合 / 规则生成槽位 |
| 4 | 建议推送 & 反馈命令闭环 |
| 5 | 指标 (Prometheus) + 结构化日志 + 错误中间件 |
| 6 | Docker 化 & K8s 部署脚本（CCE）|
| 7 | 合成数据脚本 & 接入 IoT 模拟 |

## 📊 关键指标 (MVP 验收)
| 指标 | 目标 |
|------|------|
| 事件写入延迟 | <100ms (本地) |
| 建议端到端延迟 | <2.5s |
| 同类型建议冷却 | ≥30 分钟 |
| 用户反馈记录率 | >60% |
| LLM 日成本 | < 1 元（示例目标）|

## ⚠️ 风险与缓解
| 风险 | 影响 | 缓解 |
|------|------|------|
| 数据稀疏 | 建议不稳定 | 合成 + 标记来源 + 逐步替换真实 |
| LLM 幻觉 | 错误/夸大描述 | 结构化 slots + 正则校验 + 违规降级模板 |
| 成本超预算 | 难以持续 | Token 统计 + 限流 + 缓存 + 模型选择策略 |
| 权限不足 (会议等) | 指标缺失 | 手动指令/模拟数据替代并标识限制 |
| 重放攻击 | 安全风险 | 签名时间窗口 + Redis 去重 (timestamp+sign) |

## 💰 成本与性能控制
* 合并多 slot 一次 LLM 调用
* 缓存用户画像 & 最近 metrics (内存/Redis, TTL 5m)
* Token 超阈值 → 模板降级
* 并发限制：全局 Semaphore + per-user 令牌桶

## 🔐 安全 & 合规
* 钉钉 HMAC 签名 + 时间窗口校验 + 重放缓存
* 最小化数据：不存私人内容，只存结构化行为统计
* 用户 ID 可脱敏导出 (hash)
* 环境变量/Secret Manager 管理机密
* 输入长度与字符白名单（防 prompt 注入）

## 🔍 可观测性
| 维度 | 内容 |
|------|------|
| Metrics | 请求耗时、外部 API 状态码、规则匹配次数、LLM 调用 token 用量 |
| Logging | 结构化 JSON + TraceId/SpanId |
| Tracing | OpenTelemetry → Jaeger / Tempo |
| Profiling(可选) | .NET: dotnet-counters / Go: pprof |

## 🔭 未来扩展路线
* SSE/WebSocket 流式输出
* RAG（pgvector/Qdrant）+ 企业知识文档
* 多模型路由（本地轻模型→商业 API Fallback）
* AB 测试不同提示策略
* 积分/激励体系（领域扩展）
* 多租户 (TenantId) & 权限模型

## 📁 仓库结构（顶层）
```
server/
	CSharp_Edition/   # .NET 8 实现文档与代码（待补充）
	Go_Edition/       # Go 实现文档与代码（待补充）
README.md           # 通用架构（当前文件）
README.en.md        # 英文版（可同步更新）
```

## 🛠️ 语言实现文档
| 语言 | 入口 |
|------|------|
| C# / .NET 8 | `server/CSharp_Edition/README.md` |
| Go | `server/Go_Edition/README.md` |

## 🤔 如何选择实现语言
| 侧重点 | C# 版本优势 | Go 版本优势 |
|--------|------------|------------|
| 可观测/诊断 | 强大的事件与分析工具 (.NET Diagnostics) | 原生 pprof / 低内存开销 |
| 冷启动 / 镜像体积 | AOT/Trim 后亦可控 | Distroless 极小静态二进制 |
| 并发模型 | async/await, 线程池成熟 | goroutine 大规模并发简单 |
| 生态库 | 企业级组件丰富 | 云原生 & 轻量工具链 |

## 🚩 快速开始（通用占位）
当前仓库尚未提交具体实现代码。建议流程：
1. 选择语言目录，初始化最小 Web 项目与健康检查端点。
2. 添加钉钉发送能力（Webhook 发送一条测试消息）。
3. 设计 `events` 表迁移 / DDL。
4. 编写一个合成事件脚本（每日模拟久坐 + 喝水）。
5. 引入规则匹配与 LLM 封装（留接口，先用假实现）。

> 提示：提交初版代码后，可在本 README 增加运行脚本 / 架构图（Mermaid）等。

---
本文件聚焦跨语言共享知识。请在各自实现 README 中补充具体工程细节与演进记录。