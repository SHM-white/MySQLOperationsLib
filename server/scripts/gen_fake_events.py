#!/usr/bin/env python
"""
生成合成测试数据
用于开发和测试环境
"""

import argparse
import asyncio
import json
import random
import uuid
from datetime import datetime, timedelta

import httpx


def generate_user_id(count=1):
    """生成用户ID"""
    users = []
    for i in range(count):
        users.append(f"user_{i+1:03d}")
    return users


def generate_work_event(user_id, timestamp):
    """生成工作事件"""
    duration = random.randint(5, 25)  # 5-25分钟
    return {
        "user_id": user_id,
        "timestamp": timestamp.isoformat(),
        "event_type": "WORK",
        "source": "desktop",
        "data": {
            "duration_min": duration,
            "activity_level": random.choice(["low", "medium", "high"]),
            "app": random.choice(["vscode", "browser", "office", "terminal"]),
            "keyboard_count": random.randint(10, 500),
            "mouse_distance": random.randint(100, 1000)
        }
    }


def generate_break_event(user_id, timestamp):
    """生成休息事件"""
    duration = random.randint(3, 15)  # 3-15分钟
    return {
        "user_id": user_id,
        "timestamp": timestamp.isoformat(),
        "event_type": "BREAK",
        "source": "desktop",
        "data": {
            "duration_min": duration,
            "type": random.choice(["active", "passive", "away"]),
            "reason": random.choice(["timer", "manual", "system"])
        }
    }


def generate_water_event(user_id, timestamp):
    """生成饮水事件"""
    amount = random.choice([100, 150, 200, 250, 300])
    return {
        "user_id": user_id,
        "timestamp": timestamp.isoformat(),
        "event_type": "WATER",
        "source": "manual",
        "data": {
            "amount_ml": amount,
            "container": random.choice(["bottle", "cup", "glass"]),
            "recorded_by": random.choice(["manual", "reminder"])
        }
    }


def generate_posture_event(user_id, timestamp):
    """生成姿势事件"""
    return {
        "user_id": user_id,
        "timestamp": timestamp.isoformat(),
        "event_type": "POSTURE",
        "source": "desktop",
        "data": {
            "issue_type": random.choice(["slouching", "leaning", "too_close"]),
            "confidence": random.uniform(0.7, 0.98),
            "duration_sec": random.randint(30, 300)
        }
    }


def generate_environment_event(user_id, timestamp):
    """生成环境事件"""
    return {
        "user_id": user_id,
        "timestamp": timestamp.isoformat(),
        "event_type": "ENVIRONMENT",
        "source": "iot",
        "data": {
            "temperature": round(random.uniform(18, 30), 1),
            "humidity": round(random.uniform(30, 70), 1),
            "light": random.randint(200, 1000),
            "noise": random.randint(30, 70)
        }
    }


def generate_event(user_id, timestamp):
    """随机生成事件"""
    event_type = random.choices(
        ["WORK", "BREAK", "WATER", "POSTURE", "ENVIRONMENT"],
        weights=[5, 1, 1, 1, 1]
    )[0]
    
    if event_type == "WORK":
        return generate_work_event(user_id, timestamp)
    elif event_type == "BREAK":
        return generate_break_event(user_id, timestamp)
    elif event_type == "WATER":
        return generate_water_event(user_id, timestamp)
    elif event_type == "POSTURE":
        return generate_posture_event(user_id, timestamp)
    else:
        return generate_environment_event(user_id, timestamp)


async def post_event(client, url, event):
    """发送事件到服务器"""
    try:
        response = await client.post(f"{url}/events/", json=event)
        if response.status_code != 200:
            print(f"Error posting event: {response.status_code} - {response.text}")
            return False
        return True
    except Exception as e:
        print(f"Exception posting event: {str(e)}")
        return False


async def generate_user_data(user_id, base_url, days, events_per_day, client):
    """为用户生成指定天数的数据"""
    total = 0
    successful = 0
    
    for day in range(days):
        # 今天减去指定天数
        date = datetime.now().replace(hour=9, minute=0, second=0) - timedelta(days=day)
        
        # 工作时间分布（9:00 - 18:00）
        for i in range(events_per_day):
            # 随机时间
            hour_offset = random.randint(0, 9)  # 9小时工作日
            minute_offset = random.randint(0, 59)
            time_offset = timedelta(hours=hour_offset, minutes=minute_offset)
            timestamp = date + time_offset
            
            # 生成事件
            event = generate_event(user_id, timestamp)
            total += 1
            
            # 发送事件
            if await post_event(client, base_url, event):
                successful += 1
                
            # 随机延迟，避免请求过快
            await asyncio.sleep(random.uniform(0.05, 0.2))
    
    return total, successful


async def main(args):
    """主函数"""
    base_url = args.url
    days = args.days
    events_per_day = args.events
    user_count = args.users
    
    print(f"Generating data for {user_count} users, {days} days, {events_per_day} events per day")
    print(f"Target API: {base_url}")
    
    # 生成用户
    users = generate_user_id(user_count)
    
    # 创建 HTTP 客户端
    async with httpx.AsyncClient(timeout=30.0) as client:
        # 检查服务可用性
        try:
            response = await client.get(f"{base_url}/healthz")
            if response.status_code != 200:
                print(f"Error: API not available, status code: {response.status_code}")
                return
        except Exception as e:
            print(f"Error: API not available - {str(e)}")
            return
        
        # 为每个用户生成数据
        total_events = 0
        successful_events = 0
        
        for user_id in users:
            print(f"Generating data for user: {user_id}")
            total, successful = await generate_user_data(
                user_id, base_url, days, events_per_day, client
            )
            total_events += total
            successful_events += successful
        
        # 输出统计信息
        print("\nData generation completed")
        print(f"Total events: {total_events}")
        print(f"Successful events: {successful_events}")
        print(f"Failed events: {total_events - successful_events}")
        print(f"Success rate: {successful_events / total_events * 100:.2f}%")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Generate synthetic test data")
    parser.add_argument("--url", type=str, default="http://localhost:8080", help="API base URL")
    parser.add_argument("--days", type=int, default=7, help="Number of days to generate")
    parser.add_argument("--events", type=int, default=30, help="Events per day per user")
    parser.add_argument("--users", type=int, default=3, help="Number of users to generate")
    
    args = parser.parse_args()
    
    asyncio.run(main(args))
