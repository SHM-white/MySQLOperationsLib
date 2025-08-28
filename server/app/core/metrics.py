from fastapi import FastAPI
from prometheus_fastapi_instrumentator import Instrumentator, metrics


def setup_metrics(app: FastAPI) -> None:
    """
    设置 Prometheus 指标监控
    为 FastAPI 应用添加 /metrics 路由
    """
    instrumentator = Instrumentator(
        should_group_status_codes=True,
        excluded_handlers=[".*healthz", "/metrics"],
    )
    
    # 添加基础指标
    instrumentator.instrument(app)
    
    # 添加自定义业务指标
    instrumentator.add(
        metrics.request_size(
            metric_name="hg_request_size_bytes",
            should_include_handler=True,
            should_include_method=True,
            should_include_status=True,
        )
    )
    
    instrumentator.add(
        metrics.response_size(
            metric_name="hg_response_size_bytes",
            should_include_handler=True,
            should_include_method=True,
            should_include_status=True,
        )
    )
    
    # 注册自定义业务指标（根据架构文档）
    # 这些将在应用中通过 Prometheus 客户端库直接使用
    # - hg_events_ingested_total
    # - hg_rule_matches_total
    # - hg_llm_calls_total
    # - hg_llm_duration_seconds
    # - hg_recommend_push_success_total
    # - hg_feedback_ratio
    
    # 暴露 /metrics 端点
    instrumentator.expose(app)
