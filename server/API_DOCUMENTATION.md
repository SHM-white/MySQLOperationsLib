# API 文档

## 健康检查 API

### 健康检查端点
- **URL**: `/healthz`
- **方法**: `GET`
- **描述**: 返回服务状态和环境信息，用于监控和检查部署。
- **响应**:
  ```json
  {
      "status": "ok",
      "version": "0.1.0",
      "environment": "development",
      "details": {
          "debug_mode": true
      }
  }
  ```

### 数据库健康检查端点
- **URL**: `/healthz/db`
- **方法**: `GET`
- **描述**: 检查数据库连接是否正常。
- **响应**:
  ```json
  {
      "status": "ok",
      "version": "0.1.0",
      "environment": "development",
      "database": "ok",
      "details": {
          "debug_mode": true
      }
  }
  ```

## 事件 API

### 创建事件
- **URL**: `/events/`
- **方法**: `POST`
- **描述**: 上报新的事件，如工作状态、休息、饮水、姿势、环境等。
- **请求体**:
  ```json
  {
      "user_id": "test_user_123",
      "event_type": "WORK",
      "source": "desktop",
      "data": {
          "duration": 30,
          "activity": "coding"
      }
  }
  ```
- **响应**:
  ```json
  {
      "status": "success",
      "message": "Event created successfully",
      "data": {
          "user_id": "test_user_123",
          "event_type": "WORK",
          "source": "desktop",
          "data": {
              "duration": 30,
              "activity": "coding"
          }
      }
  }
  ```

### 获取事件详情
- **URL**: `/events/{event_id}`
- **方法**: `GET`
- **描述**: 通过事件 ID 获取事件详情。
- **响应**:
  ```json
  {
      "status": "success",
      "data": {
          "id": "event_id",
          "user_id": "test_user_123",
          "event_type": "WORK",
          "source": "desktop",
          "data": {
              "duration": 30,
              "activity": "coding"
          }
      }
  }
  ```

### 获取用户事件列表
- **URL**: `/events/user/{user_id}`
- **方法**: `GET`
- **描述**: 获取用户在指定时间范围内的事件列表。
- **响应**:
  ```json
  {
      "status": "success",
      "data": [
          {
              "id": "event_id",
              "user_id": "test_user_123",
              "event_type": "WORK",
              "source": "desktop",
              "data": {
                  "duration": 30,
                  "activity": "coding"
              }
          }
      ],
      "meta": {
          "page": 1,
          "size": 20,
          "total": 1,
          "pages": 1
      }
  }
  ```
