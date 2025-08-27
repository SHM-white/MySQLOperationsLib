# Health-Guardian 服务端（C# / .NET 8 版本）

> 通用业务背景 / 事件模型 / 规则+LLM 流水线 / 指标与风险 等共享内容已汇总至仓库根目录 `README.md`。此文档聚焦 **C#/.NET 特定的技术选型、目录结构、实现要点与差异化优势**。

## 🎯 版本定位
使用 .NET 8 Minimal APIs（可演进至分层）实现钉钉机器人 + LLM 智能应答 + 数据采集/分析/定时推送。强调：快速开发、类型安全、可测试、AOT 及可观测能力。

## 🧩 技术差异化亮点（相对 Go 版本）
| 领域 | .NET 版本优势 |
|------|---------------|
| 可观测 | EventSource, DiagnosticSource, dotnet-counters, OpenTelemetry 深度集成 |
| 性能调优 | JIT + R2R / AOT 组合；Span/MemoryMarshal 优化热点路径 |
| 类型系统 | 通过记录类型 / Source Generator 提前生成序列化代码，减少运行时反射 |
| 架构演进 | 原生良好分层模式（Domain/Application/Infrastructure）与 DI 容器 |
| 工具链 | Roslyn Analyzer / Source Generator 自动化（DTO 校验、OpenAPI、JSONContext） |

> 与共享层的通用“纠偏 / 数据治理 / 事件模型”已在根 README 描述，这里不再重复。

### 数据访问策略（C# 具体）
初期：Dapper + NpgsqlDataSource（复用连接池）→ 若复杂聚合或关系增多再引入 EF Core（混用可行）。

扩展：可选引入 EF Core `DbContext` + 迁移；使用编译时模型（AOT 场景）。

> 流水线与 Prompt 规范详见根 README“规则 + LLM 混合流水线”章节。

### LLM 接入（接口稳定性）
* 定义 `ILlmClient`（见下方示例）→ 多实现：OpenAI / 通义 / DeepSeek。
* 装饰器模式：日志、计费统计、超时与重试（Polly）、缓存（相同 prompt hash 短期命中）。

> 云上分阶段与成本控制策略见根 README“华为云 / 成本与性能”章节。

> 数据表整体定义与字段说明参见根 README“核心数据库表”。此处只补充 .NET 数据访问建议。

### 规则引擎 (.NET 具体实现建议)
* 数据加载：`IConfigurationRuleRepository` + 内存缓存 (IMemoryCache, TTL=1m, 失效主动刷新)。
* 表达式执行：可先手写 switch；后期可用 `System.Linq.Expressions` 动态编译或 Source Generator 生成策略委托。
* 冷却：`recommendations` 表查询最近窗口 + Redis Key (slotType:user:bucket) 双保险。

### 性能 & 成本（.NET 侧实现点）
| 项目 | 策略 |
|------|------|
| LLM Token | Prompt 分页 / 精简字段、slots 合并一次请求 |
| 序列化 | `JsonSerializerContext` Source Gen + `ReferenceHandler.IgnoreCycles`（若需要）|
| 对象池 | `ArrayPool<byte>` / `ObjectPool<T>`（自定义）用于高频短生命周期对象 |
| 限流 | `TokenBucket` 自定义中间件 + Redis 共享配额 |
| 指标 | prometheus-net 直出；或 OpenTelemetry Metric => Prometheus |

> 风险及缓解策略见根 README“风险与缓解”章节。

> 量化指标统一维护在根 README“关键指标”。

（差异化亮点已在本文“技术差异化亮点”集中列出。）

### 建议实施步骤（C# 视角）
1. `dotnet new sln` + 创建项目 (`Api`, `Application`, `Domain`, `Infrastructure`, `Tests`).
2. Api: Minimal APIs + 健康检查 + 全局错误处理中间件。
3. 集成钉钉发送（Webhook）→ 配置绑定 & Options Pattern。
4. 定义 `ILlmClient` 接口 + OpenAI 实现（可 Mock）。
5. Dapper 仓储：写入 messages / events；编写聚合查询。
6. 定时 HostedService / Quartz Job：计算 daily_metrics。
7. 规则匹配服务 + 冷却策略 + 生成 slots。
8. LLM 调用 + 文本校验 + 推送钉钉。
9. 反馈端点 `/feedback` 更新 recommendation_feedback。
10. prometheus-net + OpenTelemetry Tracing；Serilog 结构化日志。
11. Docker 多阶段构建 + 发布配置（Trim / SingleFile 可选）。

---
新增内容已针对初始设想的不足给出合规、可扩展及评分导向的修正与增强说明。

## 技术选型（C# 专属细化）
| 领域 | 选型 | 说明 |
|------|------|------|
| .NET 运行时 | .NET 8 (LTS) | 跨平台，性能佳，原生容器支持 |
| Web 模型 | Minimal APIs + Endpoint 分组 | 低样板，可逐步抽象 Controller/分层 |
| 序列化 | System.Text.Json | 默认高性能，可配置 Source Generation |
| 依赖注入 | 内置 DI 容器 | 通过扩展方法注册模块 |
| 配置 | appsettings*.json + 环境变量 + Options Pattern | 强类型配置绑定 |
| 日志 | `Microsoft.Extensions.Logging` + Serilog | 结构化日志输出 JSON |
| 数据库 | PostgreSQL 或 MySQL | 选 PG（支持 JSONB/扩展） |
| ORM / 数据访问 | Dapper（早期）→ EF Core（若需复杂关系） | 先快后抽象 |
| 迁移 | `dotnet-ef` 或 `DbUp` | 团队喜好二选一 |
| HTTP 外部调用 | HttpClientFactory + Polly | 统一重试/熔断/超时策略 |
| LLM 调用 | 封装接口 + 实现 (OpenAI / 通义 / 其它) | 接口隔离可替换 |
| 定时任务 | Quartz.NET 或 CronBackgroundService | 简单任务可自写 HostedService |
| 缓存 & 分布式锁 | Redis (StackExchange.Redis) | 限流 / Token 缓存 |
| 文档 | Swagger / Swashbuckle | OpenAPI 3 UI |
| 验证 | FluentValidation | DTO 输入验证 |
| 显示指标 | prometheus-net | /metrics 暴露 |
| 追踪 | OpenTelemetry (.NET SDK) | 输出到 OTLP / Jaeger |
| 消息队列 (可选) | RabbitMQ / Kafka | 若需异步扩展再引入 |
| 容器 | 多阶段 Docker + trim self-contained | 降低镜像体积 |

## 解决方案分层结构（建议）
```
server/CSharp_Edition/
  src/
    HealthGuardian.Api/            # Web 层 (Minimal APIs / Endpoints)
      Program.cs
      Endpoints/
        DingTalkEndpoints.cs
        LlmEndpoints.cs
        HealthEndpoints.cs
        InternalAdminEndpoints.cs
      Middleware/
        RequestLoggingMiddleware.cs
        ErrorHandlingMiddleware.cs
      Filters/ (可选)
      Contracts/                   # Request/Response DTO
    HealthGuardian.Application/    # 应用层（UseCases / Services）
      Interfaces/                  # 抽象 (IDingTalkService, ILlmClient, IRepository<T>)
      UseCases/
        ProcessIncomingMessage.cs
        GenerateDailySummary.cs
      Services/
        MessageWorkflowService.cs
    HealthGuardian.Domain/         # 领域模型 & 领域服务
      Entities/
        User.cs
        Conversation.cs
        Message.cs
        HealthMetric.cs
      ValueObjects/
      Events/
    HealthGuardian.Infrastructure/ # 基础设施实现
      Persistence/
        DbContext.cs (若 EF Core)
        DapperConnectionFactory.cs
        Repositories/
          MessageRepository.cs
          ConversationRepository.cs
      Llm/
        OpenAiClient.cs
        TongyiClient.cs
        ClientFactory.cs
      DingTalk/
        DingTalkClient.cs
        SignatureValidator.cs
      Scheduling/
        QuartzJobDefinitions.cs
      Configuration/
      Logging/
    HealthGuardian.Background/     # 可选：长耗时 / 队列消费者
    HealthGuardian.Tests/          # 单元测试 (xUnit + FluentAssertions)
  build/
    Dockerfile
  scripts/
    migrate.ps1
  README.md
```

## 组件职责
- Api: 只做 HTTP 层绑定与输入输出映射；不写业务逻辑。
- Application: 编排业务用例（事务控制、调用多个服务/仓储）。
- Domain: 纯业务规则（无技术依赖），可单元测试驱动。
- Infrastructure: 外部资源的实现（DB/HTTP/队列/钉钉/LLM）。
- Background: HostedService/Quartz Job 执行定时或异步任务。

## LLM 抽象示例
```csharp
public interface ILlmClient {
    Task<ChatResult> ChatAsync(ChatRequest request, CancellationToken ct = default);
}

public record ChatRequest(string Model, List<ChatMessage> Messages, float Temperature = 0.7f, int MaxTokens = 1024, bool Stream = false);
public record ChatMessage(string Role, string Content);
public record ChatResult(string Content, Usage Usage);
public record Usage(int PromptTokens, int CompletionTokens, int TotalTokens);
```

通过 `ILlmClient` + DI 工厂：
```csharp
services.AddSingleton<ILlmClient>(sp => new OpenAiClient(settings));
```
后期增加 `TongyiClient` 只需实现接口与 DI 切换。

## 钉钉签名校验
```csharp
public static class DingTalkSignature {
    public static bool Validate(string timestamp, string signature, string secret) {
        var toSign = timestamp + "\n" + secret;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));
        var base64 = WebUtility.UrlEncode(Convert.ToBase64String(hash));
        return base64 == signature; // 需注意大小写/编码
    }
}
```
添加时间窗口判断防重放。

## 中间件管线（顺序建议）
1. Request ID 注入（若无自带）
2. 全局错误处理 -> 统一返回结构 `{code,message,data}`
3. 访问日志（Serilog Enricher）
4. 限流（可选 TokenBucket / Redis）
5. 认证/鉴权（若后期增加管理端）

## 端点分组示例
```csharp
app.MapGroup("/dingtalk")
   .MapPost("/callback", DingTalkEndpoints.Callback);

app.MapGroup("/chat")
   .MapPost("/completion", LlmEndpoints.ChatCompletion);

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
```

## 配置绑定（Options Pattern）
```csharp
builder.Services.Configure<DingTalkOptions>(builder.Configuration.GetSection("DingTalk"));
```
`appsettings.json`:
```json
{
  "DingTalk": {
    "AppKey": "",
    "AppSecret": "",
    "SignSecret": "",
    "RobotAccessToken": ""
  },
  "Llm": {
    "Provider": "OpenAI",
    "ApiKey": "YOUR_KEY",
    "BaseUrl": "https://api.openai.com/v1"
  },
  "ConnectionStrings": {
    "Main": "Host=localhost;Port=5432;Database=health;Username=user;Password=pass"
  }
}
```

## 定时任务策略
选型 1：Quartz.NET
- 在 Infrastructure 注册 `IJob` 实现（例如 `DailySummaryJob`）
- Cron 表达式配置化

选型 2：自定义 HostedService
- `while (!stoppingToken.IsCancellationRequested)`+ `Delay` + 计算下一次时间
- 适合少量简单任务

## 数据访问建议
阶段 | 策略
-----|------
MVP | Dapper + 手写 SQL（可放置在 Repository）
复杂关系 / 迁移 | 引入 EF Core（仍可混合 Dapper 做高性能查询）

Repository 示例（Dapper）：
```csharp
public class MessageRepository : IMessageRepository {
    private readonly NpgsqlDataSource _ds;
    public MessageRepository(NpgsqlDataSource ds) => _ds = ds;
    public async Task InsertAsync(Message m) {
        const string sql = "INSERT INTO messages(id,user_id,role,content,created_at) VALUES(@Id,@UserId,@Role,@Content,@CreatedAt)";
        await using var conn = await _ds.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, m);
    }
}
```

## 事务与一致性
- 使用 `NpgsqlConnection.BeginTransactionAsync()` 包裹应用层 UseCase
- 封装一个 `IUnitOfWork` 接口供 Application 调用
- 若转向 EF Core：使用 `DbContext` 自带事务（`SaveChanges`）

## 异步工作流 / 解耦
- 简单：在 UseCase 内 `Task.Run`（不推荐大量使用）
- 更好：`Channel<T>` + BackgroundService 消费
- 更进一步：引入消息队列（扩展阶段）

## 可观测性
- Metrics：请求计数/耗时、外部 API 状态码、任务执行次数
- Logging：结构化 + TraceId/SpanId
- Tracing：OpenTelemetry => Jaeger / Tempo

## Docker 多阶段构建（示例）
```
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./src ./src
RUN dotnet publish ./src/HealthGuardian.Api/HealthGuardian.Api.csproj -c Release -o /app/publish /p:PublishTrimmed=true /p:PublishSingleFile=true /p:TrimMode=link

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["./HealthGuardian.Api"]
```

可选自包含（无 runtime 镜像）：`/p:SelfContained=true -r linux-x64`，但需注意修剪风险。

## 测试策略
层次 | 工具 | 内容
-----|------|------
单元 | xUnit + FluentAssertions | 纯领域/服务逻辑（Mock 仓储）
集成 | WebApplicationFactory | 启动内存 TestServer，测试端点序列
迁移 | 启动临时容器 PostgreSQL + 运行迁移 + 断言表结构
负载（可选） | `bombardier` / `k6` | 端到端吞吐/延迟

## 最小可行流程（MVP 顺序）
1. Program.cs 建立基础端点 + 健康检查
2. 集成钉钉主动发送（Webhook Token + Secret）
3. 实现回调验签 & Echo 机制
4. LLM OpenAI 实现 + ChatCompletion 端点
5. 消息入库 + 历史上下文拼接
6. 定时任务每日推送
7. 引入指标、结构化日志、错误处理中间件
8. Docker 化 & 部署脚本

## 错误处理约定
统一响应：
```json
{"code":0,"message":"ok","data":{}}
```
- 业务错误：4xx + `code` 自定义
- 系统错误：5xx + `code` 固定（如 10001）
- 全局中间件捕获未处理异常 -> 记录日志 -> 500

## 安全
- 钉钉签名时间窗口 + 重放缓存（Redis Key: timestamp+sign, TTL 1min）
- 外部 API 限次调用（Polly 速率限制 + 重试）
- 机密使用环境变量/Cloud Secret Manager
- 输入校验 + 最大消息长度裁剪

## 未来扩展
- SSE / WebSocket 流式输出
- RAG（向量库：pgvector / Qdrant）
- 多模型路由（根据 prompt 分类）
- AB 实验（不同提示策略对比）
- 多租户支持（TenantId 贯穿 Domain & 数据层）

---
如需了解跨语言通用概念（事件模型、规则流水线、指标、风险），请返回根目录 `README.md`。
