import hashlib
import hmac
import time
from typing import Dict, Optional, Tuple, Union

import redis.asyncio as redis
from fastapi import Request, HTTPException, Depends

from app.core.config import settings


class SecurityVerifier:
    """
    安全验证器
    负责钉钉签名验证和防重放攻击
    """
    def __init__(self, redis_client: Optional[redis.Redis] = None):
        self.sign_secret = settings.DINGTALK_SIGN_SECRET
        self.redis_client = redis_client
        self.replay_window_seconds = 60  # 防重放窗口：1分钟
    
    async def verify_dingtalk_signature(
        self, 
        request: Request,
        timestamp: str, 
        sign: str
    ) -> bool:
        """
        验证钉钉签名
        使用 HMAC-SHA256 验证请求签名
        """
        if not self.sign_secret:
            # 开发环境可能不启用验签
            if settings.is_development:
                return True
            raise HTTPException(status_code=500, detail="Signature secret not configured")
        
        # 检查时间戳是否在合理范围
        try:
            ts = int(timestamp)
            now = int(time.time())
            if abs(now - ts) > 60:  # 时间戳有效期为1分钟
                raise HTTPException(status_code=401, detail="Timestamp expired")
        except ValueError:
            raise HTTPException(status_code=400, detail="Invalid timestamp")
        
        # 检查重放攻击
        if self.redis_client:
            replay_key = f"replay:{timestamp}:{sign}"
            exists = await self.redis_client.exists(replay_key)
            if exists:
                raise HTTPException(status_code=401, detail="Replay attack detected")
            
            # 记录已处理的签名，有效期与时间窗口一致
            await self.redis_client.set(replay_key, "1", ex=self.replay_window_seconds)
        
        # 验证签名
        string_to_sign = f"{timestamp}\n{self.sign_secret}"
        signature = hmac.new(
            self.sign_secret.encode('utf-8'),
            string_to_sign.encode('utf-8'),
            digestmod=hashlib.sha256
        ).hexdigest()
        
        if not hmac.compare_digest(signature, sign):
            raise HTTPException(status_code=401, detail="Invalid signature")
        
        return True
    
    
async def get_security_verifier() -> SecurityVerifier:
    """
    依赖注入：获取安全验证器实例
    """
    # 在实际应用中，这里会从依赖注入中获取 Redis 客户端
    redis_client = None
    if settings.REDIS_URL:
        # 实际项目中应从依赖中获取连接池
        pass
    
    return SecurityVerifier(redis_client)
