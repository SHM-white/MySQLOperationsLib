# Health-Guardian 服务端（Go 版本）

> 共享的业务背景 / 事件模型 / 规则 + LLM 流水线 / 风险缓解 / 指标 见仓库根目录 `README.md`。本文件聚焦 **Go 实现的目录结构、技术选型、并发模型与差异化亮点**。

## 🎯 版本目标
使用 Go 构建轻量高性能服务：钉钉回调 & 推送、LLM 适配、事件写入、规则聚合、定时任务、可观测与安全控制。

## 🧩 差异化亮点（相对 C# 版本）
| 领域 | Go 版本优势 |
|------|------------|
| 冷启动 & 体积 | 单静态二进制 + Distroless 镜像极小 |
| 并发模型 | goroutine+channel 开销低，适合 IO & 定时任务并发 |
| 部署弹性 | 适合边缘/Serverless（快速缩放）|
| 资源占用 | 内存占用可控，利于高密度容器 | 
| 生态 | 丰富云原生/运维工具：pprof, swag, prometheus client |

> 与“误区纠偏 / 统一事件模型”内容已在根 README 展开，此处省略。

### 并发与异步策略
| 场景 | 实现建议 |
|------|----------|
| 钉钉回调快速 ACK | Handler 内将耗时（LLM / DB）推入 buffered channel 由 worker 消费 |
| 定时任务 | robfig/cron v3 + context 取消；长任务 goroutine with timeout |
| 批写入 / 聚合 | 周期 goroutine 拉取事件缓存批量写库（控制事务开销） |
| LLM 限流 | 中央 `rate.Limiter` + 每用户 map[tokenBucket] |

> 统一事件 Schema 示例在根 README 中已给出。

> 建议流水线总体逻辑参阅根 README“规则 + LLM”。此处关注 Go 代码组织。

> Prompt 结构示例见根 README“LLM Prompt 设计”。

> 云上阶段化路线在根 README“华为云映射”章节统一说明。

> 隐私与合规策略已集中于根 README“安全 & 合规”。

> 竞争力要点与混合策略在根 README 已归纳。

> 指标目标在根 README“关键指标”统一维护。

> 风险与缓解列表已在根 README 中列出。

---

## 技术选型（Go 专属）
| 领域 | 选型 | 说明 |
|------|------|------|
| Web 框架 | `gin` | 轻量、高性能、生态丰富 |
| 配置 | `viper` | 支持多源（env / yaml）与热加载（可选） |
| 日志 | `zap` (prod) / `zap + lumberjack` | 结构化、可选滚动切割 |
| 数据库访问 | `sqlx` 或 `gorm` | 初期推荐 `sqlx`（更贴近 SQL），复杂模型时可换 gorm |
| 数据迁移 | `golang-migrate` | 版本化管理 schema |
| 调外部 HTTP | `resty` | 简洁的链式 API + 重试 | 
| LLM 客户端抽象 | 自定义 interface + 具体实现 | 统一 OpenAI/通义/本地 llama.cpp 等 |
| JSON 序列化 | 标准库 + `jsoniter`（可选性能优化） | 先用标准库 |
| 任务调度 | `robfig/cron/v3` | Cron 表达式 + 可并行控制 |
| DI（可选） | `google/wire` | 后期模块增多再引入 |
| API 文档 | `swaggo/swag` | 通过注释生成 OpenAPI/Swagger |
| 健康检查 | 自定义 `/healthz` + `/readyz` | K8s/云运维集成 |
| 指标 | `prometheus/client_golang` | `/metrics` 暴露 |
| 速率限制 | `golang.org/x/time/rate` 或中间件 | 保护外部 API 与内部处理 |
| 熔断/重试 | `resty` 内置 + 自定义封装 | LLM / 钉钉调用稳定性 |
| 容器 | 多阶段构建 + `distroless/static` | 极小镜像，减少攻击面 |
| 测试 | `testing` + `testify` | 单元/集成测试 |
| 本地向量/检索（后期） | 可独立微服务（Python/Rust） | 通过 HTTP/gRPC 调用 |

## 顶层目录结构（建议）
```
server/Go_Edition/
  cmd/
    server/
      main.go              # 程序入口（装配 root 依赖）
  internal/
    api/
      middleware/          # 日志 / 恢复 / 认证 / 限流
      handler/             # 具体路由处理（分文件：dingtalk.go / llm.go ...）
      router.go            # 路由注册
    app/                   # 业务用例 (Application Service)
    domain/                # 核心业务实体 / 领域服务（若复杂时逐步抽出）
    dingtalk/              # 钉钉客户端（签名/请求封装）
    llm/                   # LLM 接口与具体实现 (openai, tongyi, local)
    db/
      repo/                # 仓储实现（UserRepo, SessionRepo 等）
      migration/           # 内嵌或外置的迁移执行逻辑
      db.go                # 初始化（连接池、配置）
    scheduler/             # 定时任务定义与注册
    config/                # 配置结构体 & 加载
    logging/               # 日志初始化
    metrics/               # 指标注册
    errors/                # 统一错误包装（可选）
  pkg/
    model/                 # DTO / API 传输对象
    util/                  # 通用工具（签名、加密、重试等）
  configs/
    config.example.yaml
  migrations/              # SQL 迁移文件 (*.up.sql / *.down.sql)
  scripts/
    dev_run.sh             # 开发脚本（可选）
  build/
    Dockerfile
  tests/                   # 集成测试（可用 docker compose）
  Makefile (可选)
  README.md
```

## 模块职责简述
- `api`：仅做输入输出（HTTP -> DTO -> 调用 app/service -> DTO -> HTTP）
- `app`：编排业务流程（事务、调用仓储、外部服务）
- `domain`：业务规则（如对健康数据的阈值判定、告警策略）
- `db/repo`：集中数据持久化逻辑，不向外暴露 SQL 字符串
- `dingtalk`：钉钉 Webhook / 回调验签 / 发送消息 / AccessToken 缓存
- `llm`：统一接口 `Client.Complete(ctx, req) (resp, error)`；策略注入
- `scheduler`：注册 cron 任务（例如：每日健康摘要推送）
- `config`：结构体 + 默认值 + 环境覆盖
- `metrics`：Prometheus 指标（请求耗时、外部调用失败率、任务执行次数）

## 关键数据流 & 时序
1. 用户在钉钉群 @ 机器人 -> 钉钉服务器 -> (POST 回调) `/dingtalk/callback`
2. 中间件：验签 + 限流 -> 解析消息 DTO -> 调用 app 层 UseCase
3. UseCase：
   - 记录消息入库（异步可通过 channel / goroutine + buffer 队列）
   - 构造 LLM Prompt -> 调用 `llm.Client`
   - 处理响应（截断 / 敏感过滤）
   - 调用 `dingtalk.Client.SendMessage`
4. 返回 200 给钉钉（要求快速响应，可将长耗时交给异步 worker）
5. 定时任务：在 scheduler 启动时注册（例如每小时统计用户交互写入一条报表并推送）

## LLM 抽象 Interface 示例（概念）
```go
// internal/llm/types.go
type Client interface {
    ChatCompletion(ctx context.Context, req ChatRequest) (ChatResponse, error)
}

type ChatRequest struct {
    Model       string
    Messages    []Message
    Temperature float32
    MaxTokens   int
    Stream      bool // 未来扩展
}

type Message struct { Role, Content string }

type ChatResponse struct {
    Content string
    Usage   TokenUsage
}
```

## 钉钉签名校验流程
- 读取 header：`timestamp`, `sign`
- 计算：`stringToSign := timestamp + "\n" + secret`
- `hmacSHA256(stringToSign, secret)` -> Base64 -> URL Encode -> 对比 `sign`
- 超时窗口（例如 1 分钟）内才接受

## 事务 & 并发
- 简单写请求：直接使用 `db.BeginTx` + defer rollback/commit
- 高频外部调用：加 `context.WithTimeout`
- 限流：令牌桶，中间件内实现；或对 LLM & 钉钉客户端装饰器模式

## 错误处理约定
- 统一返回结构：`{"code": <int>, "message": "...", "data": <any>}`
- 自定义错误类型携带：`ErrCode`, `HTTPStatus`
- 中间件拦截 panic -> 500 + 日志

## 配置示例（config.example.yaml）
```yaml
server:
  addr: ":8080"
  read_timeout: 5s
  write_timeout: 10s

database:
  dsn: "postgres://user:pass@localhost:5432/health?sslmode=disable"
  max_open: 20
  max_idle: 5

llm:
  provider: "openai"
  api_key: "YOUR_KEY"
  base_url: "https://api.openai.com/v1"

dingtalk:
  app_key: ""
  app_secret: ""
  sign_secret: ""    # 回调签名用
  robot_access_token: ""

scheduler:
  jobs:
    - name: daily_summary
      spec: "0 0 9 * * *"   # 每天09:00
```

## 开发流程建议
1. 定义接口 & DTO -> 生成 Swagger -> Mock 调试
2. 写基础中间件（日志 / 恢复 / 请求 ID）
3. 接入钉钉发送（最小闭环：API -> 钉钉群）
4. 回调处理 + LLM 调用
5. 加入数据库（会话上下文 / 问答历史）
6. 定时任务 & 指标 & 监控
7. 容器化 + 部署脚本

## Docker 多阶段示例（思路）
```
FROM golang:1.23-alpine AS build
WORKDIR /app
COPY . .
RUN go build -o server ./cmd/server

FROM gcr.io/distroless/static
WORKDIR /app
COPY --from=build /app/server .
ENTRYPOINT ["./server"]
```

## 测试层次
- 单元：llm mock / dingtalk mock
- 集成：docker compose 启 Postgres -> 运行 migration -> 回调模拟脚本
- 负载（可选）：`vegeta` / `k6`

## 安全 & 可靠性
- 所有外部调用设置超时 + 指数退避重试（最大次数）
- 机密通过环境变量 / 密钥管理（不要写入 git）
- 最小权限 DB 账号 + 迁移独立执行
- 访问日志与业务日志分级

## 后期扩展
- WebSocket / SSE 流式回复
- 向量检索（独立 embedding 服务）
- RAG：文档分块 -> 向量库（pgvector / qdrant）
- 多租户：在 domain 增加 TenantID 维度

（差异化亮点已整合到前文“差异化亮点”表格。）

## 下一步建议（Go 实现侧）
1. 初始化 `cmd/server/main.go` + `internal/api` 路由 & 健康检查。
2. 钉钉 Webhook 发送封装 + 回调签名校验中间件。
3. 事件写入仓储 (sqlx) + migration（golang-migrate）。
4. 规则匹配 Service（内存加载 + 条件判定）。
5. LLM Client (OpenAI / DeepSeek) + 限流 + 重试装饰。
6. cron 任务：聚合 daily_metrics + 推送建议。
7. Prometheus `/metrics` + zap 日志 + pprof。
8. Distroless Docker 镜像构建。

---
跨语言共享内容请参阅根 README；本文件仅保留 Go 特有实现视角。
