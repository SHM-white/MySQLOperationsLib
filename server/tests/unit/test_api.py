import pytest
from httpx import AsyncClient
from fastapi.testclient import TestClient

from app.main import app


@pytest.fixture
def client():
    """创建测试客户端"""
    return TestClient(app)


@pytest.fixture
async def async_client():
    """创建异步测试客户端"""
    async with AsyncClient(app=app, base_url="http://127.0.0.1") as client:
        yield client


class TestHealthAPI:
    """测试健康检查 API"""

    def test_health_check(self, client):
        """测试健康检查端点"""
        response = client.get("/healthz")

        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "ok"
        assert "version" in data
        assert "environment" in data
        assert "details" in data

    async def test_database_health_check(self, async_client):
        """测试数据库健康检查端点"""
        response = await async_client.get("/healthz/db")

        assert response.status_code == 200
        data = response.json()
        assert data["status"] in ["ok", "error"]
        assert "version" in data
        assert "environment" in data
        assert "database" in data
        assert "details" in data


class TestEventsAPI:
    """测试事件 API"""

    async def test_create_event_success(self, async_client):
        """测试成功创建事件"""
        event_data = {
            "user_id": "test_user_123",
            "event_type": "WORK",
            "source": "desktop",
            "data": {
                "duration": 30,
                "activity": "coding"
            }
        }

        response = await async_client.post("/events/", json=event_data)

        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "success"
        assert data["message"] == "Event created successfully"
        assert "data" in data
        assert data["data"]["user_id"] == event_data["user_id"]
        assert data["data"]["event_type"] == event_data["event_type"]
        assert data["data"]["source"] == event_data["source"]

    async def test_create_event_invalid_type(self, async_client):
        """测试创建无效事件类型"""
        event_data = {
            "user_id": "test_user_123",
            "event_type": "INVALID_TYPE",
            "source": "desktop",
            "data": {}
        }

        response = await async_client.post("/events/", json=event_data)

        assert response.status_code == 422  # Validation error

    async def test_create_event_invalid_source(self, async_client):
        """测试创建无效事件来源"""
        event_data = {
            "user_id": "test_user_123",
            "event_type": "WORK",
            "source": "invalid_source",
            "data": {}
        }

        response = await async_client.post("/events/", json=event_data)

        assert response.status_code == 422  # Validation error

    async def test_get_event_not_found(self, async_client):
        """测试获取不存在的事件"""
        response = await async_client.get("/events/nonexistent-id")

        assert response.status_code == 404
        data = response.json()
        assert data["status"] == "error"
        assert "message" in data

    async def test_get_user_events(self, async_client):
        """测试获取用户事件"""
        # 首先创建一些测试事件
        event_data = {
            "user_id": "test_user_123",
            "event_type": "WORK",
            "source": "desktop",
            "data": {"duration": 30}
        }

        # 创建事件
        await async_client.post("/events/", json=event_data)

        # 获取用户事件
        response = await async_client.get("/events/user/test_user_123")

        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "success"
        assert "data" in data
        assert "meta" in data
        assert isinstance(data["data"], list)
        assert data["meta"]["page"] == 1
        assert data["meta"]["size"] == 20