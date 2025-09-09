# HealthGuardian

一个“事件驱动 + 规则引擎 + LLM 润色” 的办公健康辅助服务：采集（久坐 / 喝水 / 环境 / 主观状态）→ 聚合分析 → 结构化建议 → 大模型生成自然语言提醒 → 钉钉推送 & 用户反馈闭环。

## 开发：
请新建自己的`dev_{username}`分支进行开发，完成后发起 PR 合并到 `dev`分支，`dev`分支测试通过后再合并到`master`分支（当然现在暂时没有完整的全功能代码，`master`分支会不定期镜像`dev`分支）。

项目架构：

- server
  - app                  FastAPI 主应用
	- api                FastAPI 后端接口
	- scheduler          定时任务
	- services           各种服务（钉钉、LLM、规则引擎等，调用大语言模型在此处调用）
	- repositories       数据仓库(数据库接口，使用`C#`子模块通过`pythonnet`调用)
	- utils              工具类
	- core               核心配置、日志等
  - tests                测试代码
  - scripts              生成假数据脚本等其它工具脚本