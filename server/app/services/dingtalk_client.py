import json
from typing import Dict, Any, Optional, List, Union
import hmac
import hashlib
import base64
import urllib.parse
import time

import aiohttp
import structlog
from prometheus_client import Counter, Histogram

from app.core.config import settings

logger = structlog.get_logger(__name__)

# 指标
dingtalk_calls_counter = Counter(
    "hg_dingtalk_calls_total", 
    "Total number of DingTalk API calls",
    ["type", "status"]
)
dingtalk_duration = Histogram(
    "hg_dingtalk_duration_seconds", 
    "Duration of DingTalk API calls in seconds",
    ["type"]
)


class DingTalkClient:
    """
    钉钉机器人客户端
    用于发送钉钉消息
    """
    
    def __init__(self):
        self.robot_token = settings.DINGTALK_ROBOT_TOKEN
        self.sign_secret = settings.DINGTALK_SIGN_SECRET
        self.base_url = "https://oapi.dingtalk.com/robot/send"
        
    async def send_text(
        self, 
        content: str, 
        at_mobiles: Optional[List[str]] = None, 
        at_all: bool = False
    ) -> Dict[str, Any]:
        """
        发送文本消息
        
        Args:
            content: 消息内容
            at_mobiles: 需要@的手机号列表
            at_all: 是否@所有人
            
        Returns:
            Dict[str, Any]: 钉钉接口响应
        """
        message = {
            "msgtype": "text",
            "text": {
                "content": content
            },
            "at": {
                "atMobiles": at_mobiles or [],
                "isAtAll": at_all
            }
        }
        
        return await self._send_message(message, message_type="text")
    
    async def send_markdown(
        self, 
        title: str, 
        text: str, 
        at_mobiles: Optional[List[str]] = None, 
        at_all: bool = False
    ) -> Dict[str, Any]:
        """
        发送 Markdown 消息
        
        Args:
            title: 消息标题
            text: Markdown 格式的消息内容
            at_mobiles: 需要@的手机号列表
            at_all: 是否@所有人
            
        Returns:
            Dict[str, Any]: 钉钉接口响应
        """
        message = {
            "msgtype": "markdown",
            "markdown": {
                "title": title,
                "text": text
            },
            "at": {
                "atMobiles": at_mobiles or [],
                "isAtAll": at_all
            }
        }
        
        return await self._send_message(message, message_type="markdown")
    
    async def send_action_card(
        self, 
        title: str, 
        text: str, 
        btns: List[Dict[str, str]], 
        btn_orientation: str = "0"
    ) -> Dict[str, Any]:
        """
        发送卡片消息
        
        Args:
            title: 卡片标题
            text: 卡片内容，支持 Markdown 格式
            btns: 按钮列表，格式为 [{"title": "按钮标题", "actionURL": "点击跳转链接"}]
            btn_orientation: 按钮排列方向，0: 按钮竖直排列，1: 按钮横向排列
            
        Returns:
            Dict[str, Any]: 钉钉接口响应
        """
        message = {
            "msgtype": "actionCard",
            "actionCard": {
                "title": title,
                "text": text,
                "btnOrientation": btn_orientation,
                "btns": btns
            }
        }
        
        return await self._send_message(message, message_type="actionCard")
    
    async def _send_message(
        self, 
        message: Dict[str, Any], 
        message_type: str
    ) -> Dict[str, Any]:
        """
        发送消息
        
        Args:
            message: 消息内容
            message_type: 消息类型
            
        Returns:
            Dict[str, Any]: 钉钉接口响应
        """
        if not self.robot_token:
            logger.error("DingTalk robot token not configured")
            return {"errcode": -1, "errmsg": "Robot token not configured"}
        
        # 生成签名
        timestamp = str(int(time.time() * 1000))
        sign = self._generate_signature(timestamp)
        
        # 构建请求 URL
        url = f"{self.base_url}?access_token={self.robot_token}"
        
        if sign:
            url += f"&timestamp={timestamp}&sign={sign}"
        
        # 发送请求
        try:
            with dingtalk_duration.labels(type=message_type).time():
                async with aiohttp.ClientSession() as session:
                    async with session.post(url, json=message) as response:
                        result = await response.json()
            
            # 记录指标
            if result.get("errcode") == 0:
                dingtalk_calls_counter.labels(
                    type=message_type, 
                    status="success"
                ).inc()
                logger.info(
                    "DingTalk message sent successfully", 
                    message_type=message_type
                )
            else:
                dingtalk_calls_counter.labels(
                    type=message_type, 
                    status="error"
                ).inc()
                logger.error(
                    "Failed to send DingTalk message", 
                    message_type=message_type, 
                    error=result
                )
            
            return result
            
        except Exception as e:
            dingtalk_calls_counter.labels(
                type=message_type, 
                status="exception"
            ).inc()
            logger.exception(
                "Exception when sending DingTalk message", 
                message_type=message_type, 
                error=str(e)
            )
            return {"errcode": -1, "errmsg": str(e)}
    
    def _generate_signature(self, timestamp: str) -> Optional[str]:
        """
        生成钉钉签名
        
        Args:
            timestamp: 时间戳
            
        Returns:
            Optional[str]: 生成的签名
        """
        if not self.sign_secret:
            return None
        
        string_to_sign = f"{timestamp}\n{self.sign_secret}"
        hmac_code = hmac.new(
            self.sign_secret.encode('utf-8'),
            string_to_sign.encode('utf-8'),
            digestmod=hashlib.sha256
        ).digest()
        
        sign = urllib.parse.quote_plus(base64.b64encode(hmac_code))
        return sign


# 创建全局实例
dingtalk_client = DingTalkClient()
