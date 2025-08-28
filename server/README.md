# Health-Guardian 服务端（Python 版本）架构说明

## 语言选择对比（Python / Go / C# 简表）
| 维度 | Python (选定) | Go | C#/.NET |
|------|---------------|----|---------|
| 开发速度 | ⭐⭐⭐⭐ 生态丰富，脚手架快 | ⭐⭐⭐ 简洁 | ⭐⭐⭐ 需要更多项目结构 | 
| 学习曲线（队员 C/C++ 背景） | 中（语法简单，需适应动态类型，可用类型标注缓解） | 低 | 中等 | 
| 大模型 / AI 库 | 最强（openai, transformers, langchain, vLLM, llama.cpp 绑定） | 一般（需 HTTP 调用） | 一般（需 HTTP 调用） |
| 数据/科学计算 | 强（pandas, numpy） | 较弱 | 中 | 
| 并发模型 | asyncio + 多进程；CPU 密集需额外优化 | goroutine 高并发 | async/await 线程池成熟 |
| 性能 | 原始性能中等；I/O 场景足够；可通过 uvloop/缓存/分层优化 | 高 | 高 | 
| 部署尺寸 | 中（基础镜像 50~100MB） | 小 | 中（裁剪后） |
| 类型安全 | 依赖规范 + mypy 静态检查 | 强类型 | 强类型 |
| 生态风险 | 版本碎片化，需锁定依赖 | 稳定 | 稳定 |
| 适合本项目原因 | AI 集成 & 迭代速度优势显著 | 性能/静态单文件但 AI 不如 | 桌面工具链好但 AI 生态弱 |

> 结论：以“规则 + LLM + 数据迭代”核心诉求下，Python 提供最高集成效率；通过工程规范弥补动态类型风险。

## 技术栈选型
| 领域 | 选型 | 说明 |
|------|------|------|
| Web 框架 | FastAPI | 高性能（Starlette + Pydantic），内置文档 UI |
| 运行模型 | uvicorn + uvloop (可选) | 异步 I/O 提升并发 |
| 任务调度 | APScheduler | Cron / 定时聚合 / 推送 |
| 数据库 | PostgreSQL (asyncpg) | JSONB + 扩展；async 驱动 |
| ORM / 查询 | SQLModel 或 Tortoise ORM / SQLAlchemy 2.0 | 类型友好；首阶段可直接 SQLAlchemy Core |
| 迁移 | Alembic | 数据库结构版本控制 |
| 配置 | Pydantic Settings | 环境变量 & .env |
| 日志 | structlog / loguru + JSON 输出 | 结构化日志 |
| 规则引擎 | 自研表驱动（condition json） | 简单、可控、可解释 |
| 缓存 / 去重 | Redis (aioredis) | 签名重放、限流、画像缓存 |
| LLM 接入 | OpenAI / 通义 / DeepSeek API + 可选本地 llama.cpp (python bindings / vLLM) | 统一抽象接口 |
| 流式输出 | Server-Sent Events / WebSocket | 后期增强实时体验 |
| 指标监控 | Prometheus FastAPI Instrumentator + OpenTelemetry | /metrics & trace |
| 容器 | 多阶段 Docker + slim 基础镜像 | 控制体积 |
| 测试 | pytest + httpx (async client) | 单元 / 集成测试 |
| 类型检查 | mypy + pyright (可选) | 减少动态错误 |
| 代码质量 | black + isort + ruff | 统一格式 & 静态分析 |

## 顶层目录结构（建议）
```
server/Python_Edition/
  app/
    api/
      routes/
        dingtalk.py
        llm.py
        events.py
        health.py
      deps.py              # 依赖注入 / get_db 等
      errors.py            # 全局异常处理
      response_models.py
    core/
      config.py            # Pydantic Settings
      logging.py
      metrics.py
      security.py          # 签名/重放校验
    models/                # ORM / Pydantic 模型
      event.py
      recommendation.py
      rule.py
    repositories/
      event_repo.py
      rule_repo.py
      recommendation_repo.py
    services/
      llm/
        base.py
        openai_client.py
        deepseek_client.py
        provider_router.py
      rules/
        engine.py          # 规则匹配逻辑
      recommendations.py   # 组合事件->建议槽位->LLM
      feedback.py
      aggregation.py       # 日/周聚合
      dingtalk_client.py
    scheduler/
      jobs.py              # APScheduler 任务注册
    db/
      session.py           # Async engine / sessionmaker
      migrations/          # Alembic 迁移
    utils/
      time.py
      id.py
  tests/
    unit/
    integration/
  scripts/
    gen_fake_events.py
    run_dev.sh
  pyproject.toml
  README.md
  Dockerfile
```

## 模块职责
- api: FastAPI 路由、请求/响应模型校验、OpenAPI 文档。
- services: 核心应用逻辑（规则匹配、聚合、建议生成、LLM 调用、钉钉推送）。
- repositories: 数据访问抽象，隐藏 SQL/ORM 细节。
- models: ORM / Pydantic 数据结构（区分持久化模型与外部接口模型）。
- scheduler: 定时聚合与定时推送（如每日总结、超时久坐扫描）。
- core: 配置、日志、指标、签名校验、全局依赖。
- db: 连接与迁移管理。
- utils: 通用工具。 

## 关键数据流
1. 事件上报 `POST /events`（桌面 Agent / IoT / 手动指令） -> 验签 -> 存入 `events`。
2. APScheduler 定时任务 `aggregation_job`：
   - 聚合 `events` -> 写入/更新 `daily_metrics`。
3. 规则引擎 `rules.engine.match(user_id)`：
   - 读取最新 metrics + 生效规则表 -> 输出 slots JSON。
4. 建议服务 `recommendations.generate(user, slots)`：
   - 构建 Prompt -> 调 LLM 客户端 -> 得到文案 -> 持久化 `recommendations`。
5. 钉钉发送：`dingtalk_client.send_text()`（失败重试 + 回退队列）。
6. 用户反馈回调/命令 `/ok /skip /nothelpful` -> 写入 `recommendation_feedback`。
7. 指标记录：规则命中数、LLM 耗时、推送成功/失败率。

## 规则引擎（表驱动）
表 `configuration_rules`：
```json
condition = {"type":"POSTURE_IDLE","metric":"continuous_min","op":">=","value":60}
```
处理流程：
1. 解析 condition -> 构造表达式对象。
2. 聚合结果映射 metric 字典： `{continuous_min: 85, water_progress: 0.27}`。
3. 运算 -> 满足则生成 slot：
```json
{"type":"break","reason":"连续久坐85分钟","action":"起身活动5分钟","cooldown_min":30}
```
4. 冷却检查：查询最近 N 分钟是否已有同类型 slot。

## LLM 抽象接口
```python
# services/llm/base.py
from typing import Protocol, List
class ChatMessage(TypedDict):
    role: str
    content: str

class LLMClient(Protocol):
    async def chat(self, messages: List[ChatMessage], model: str, temperature: float = 0.7, max_tokens: int = 512) -> str: ...
```
多提供商实现统一 `chat` 接口，可做回退链：本地轻模型 -> DeepSeek API -> 通义 API。

## Prompt 结构（示例）
```
SYSTEM: 你是办公健康助手，只能使用提供的事实，禁止医学诊断。
FACTS: 久坐=230min; 最长连续=85min; 喝水=400/1500ml; 温度=28℃; 状态=困倦
SLOTS: break:连续久坐85min|hydration:喝水进度27%|environment:温度偏高
STYLE: 简洁中文，列点。
OUTPUT: JSON {"messages":["..."]}
```
输出解析校验：JSON 结构 / 禁止敏感词 / 长度限制 -> 失败降级模板。

## 性能与扩展
- I/O 为主：FastAPI + asyncpg 足够；热点数据（画像、最近 metrics）置入 Redis。
- 计算型（向量检索、长上下文）后期可拆成独立微服务（Python 或 Rust）。
- LLM 调用并发：令牌桶控制 + 单例 HTTP Session；失败回退链。

## 指标示例
| 指标名 | 含义 |
|--------|------|
| hg_events_ingested_total | 累计写入事件数 |
| hg_rule_matches_total | 规则命中次数按 type 分拆 |
| hg_llm_calls_total / duration_seconds | LLM 调用次数/耗时 |
| hg_recommend_push_success_total | 推送成功数 |
| hg_feedback_ratio | 反馈/推送比率 |

## 安全
- 钉钉 HMAC+timestamp 验签；Redis 记录 `(timestamp, sign)` 1 分钟防重放。
- API Key/LLM Key 通过环境变量注入；不写入仓库。
- 输入校验：Pydantic 约束字段范围；正则过滤命令注入。
- 最小权限：数据库只开放必要 schema；分离读写账号（可选）。

## 配置示例 `.env`
```
APP_ENV=dev
API_PORT=8080
DATABASE_URL=postgresql+asyncpg://user:pass@localhost:5432/health
REDIS_URL=redis://localhost:6379/0
OPENAI_API_KEY=xxx
DINGTALK_SIGN_SECRET=xxx
DINGTALK_ROBOT_TOKEN=xxx
LLM_PROVIDER=deepseek
```

## Docker 多阶段示例
```
FROM python:3.12-slim AS base
WORKDIR /app
ENV PIP_NO_CACHE_DIR=1 PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
COPY pyproject.toml ./
RUN pip install --upgrade pip && pip install poetry && poetry config virtualenvs.create false && poetry install --no-dev
COPY app ./app
CMD ["uvicorn","app.api.main:app","--host","0.0.0.0","--port","8080"]
```
(生产可再加：只复制 lock 文件 + 分层缓存；使用 `--workers` 配合 gunicorn/uvicorn workers)

## 开发工作流
1. 建立虚拟环境 & 安装依赖（poetry/pip-tools）。
2. 编写最小 FastAPI app + 健康检查 `/healthz`。
3. 实现 DingTalk 发送封装（简单文本推送）。
4. 设计 Alembic 迁移：事件表、规则表、聚合表、建议表、反馈表、画像表。
5. 事件上报 & 合成数据脚本跑数据。
6. 规则引擎 + 建议生成 + LLM 封装。
7. 定时聚合 & 推送任务 APScheduler。
8. 指标 / 结构化日志 / 错误处理。
9. Docker 化 + 部署 (CCE / ECS)。
10. 压测与成本度量（记录 LLM token 使用）。

## 质量保障
- 测试层次：
  - 单元：规则匹配 / Prompt 生成 / LLM 适配 mock
  - 集成：事件→聚合→规则→建议→推送链路（httpx + 测试 DB）
  - 性能：locust / k6 模拟事件高并发写入
- 静态：mypy, ruff, black pre-commit
- CI（可选）：GitHub Actions / 华为云流水线 -> lint -> test -> build 镜像

## MVP 验收标准（建议）
| 项目 | 说明 | 验收方式 |
|------|------|---------|
| 事件入库 | POST /events 正常写入 | 返回 200 且 DB 有记录 |
| 聚合任务 | 定时/手动触发成功 | daily_metrics 更新 |
| 规则命中 | 人为构造久坐 > 阈值 | 生成 slot 记录 |
| 建议文本 | LLM 或模板输出 | recommendations 生成 & 推送成功 |
| 反馈闭环 | /ok /skip 指令 | feedback 表新增 |
| 监控 | /metrics 输出关键指标 | curl /metrics 查看 |

## 下一步
- 生成 `pyproject.toml` + 基础依赖列表
- 实现最小 app 与 1 条规则
- 添加合成数据脚本 & 演示 Notebook (可选)

---
本文件描述 Python 版本整体架构与实施路线，覆盖大赛对齐、数据与规则、LLM 调用、可观测、成本与安全控制。后续可据此逐步提交代码。
