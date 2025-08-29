from datetime import datetime, timedelta, date, time
import calendar
from typing import Optional, Tuple, Dict, Any, List, Union


def get_today() -> date:
    """获取今天的日期"""
    return date.today()


def get_yesterday() -> date:
    """获取昨天的日期"""
    return date.today() - timedelta(days=1)


def get_current_time() -> datetime:
    """获取当前时间"""
    return datetime.now()


def get_current_utc_time() -> datetime:
    """获取当前 UTC 时间"""
    return datetime.utcnow()


def format_date(d: date, fmt: str = "%Y-%m-%d") -> str:
    """格式化日期"""
    return d.strftime(fmt)


def format_datetime(dt: datetime, fmt: str = "%Y-%m-%d %H:%M:%S") -> str:
    """格式化日期时间"""
    return dt.strftime(fmt)


def parse_date(date_str: str, fmt: str = "%Y-%m-%d") -> date:
    """解析日期字符串"""
    return datetime.strptime(date_str, fmt).date()


def parse_datetime(dt_str: str, fmt: str = "%Y-%m-%d %H:%M:%S") -> datetime:
    """解析日期时间字符串"""
    return datetime.strptime(dt_str, fmt)


def get_day_start_end(day: date) -> Tuple[datetime, datetime]:
    """获取某一天的开始和结束时间"""
    start = datetime.combine(day, time.min)
    end = datetime.combine(day, time.max)
    return start, end


def get_week_start_end(day: date) -> Tuple[date, date]:
    """获取某一天所在周的开始和结束日期"""
    start = day - timedelta(days=day.weekday())
    end = start + timedelta(days=6)
    return start, end


def get_month_start_end(day: date) -> Tuple[date, date]:
    """获取某一天所在月的开始和结束日期"""
    start = date(day.year, day.month, 1)
    last_day = calendar.monthrange(day.year, day.month)[1]
    end = date(day.year, day.month, last_day)
    return start, end


def get_relative_day(days: int) -> date:
    """获取相对于今天的日期"""
    return date.today() + timedelta(days=days)


def time_ago(dt: datetime) -> str:
    """
    计算相对时间描述
    例如：刚刚、5分钟前、1小时前等
    """
    now = datetime.now()
    diff = now - dt
    
    seconds = diff.total_seconds()
    
    if seconds < 60:
        return "刚刚"
    elif seconds < 3600:
        minutes = int(seconds // 60)
        return f"{minutes}分钟前"
    elif seconds < 86400:
        hours = int(seconds // 3600)
        return f"{hours}小时前"
    elif seconds < 604800:
        days = int(seconds // 86400)
        return f"{days}天前"
    elif seconds < 2592000:
        weeks = int(seconds // 604800)
        return f"{weeks}周前"
    else:
        return format_date(dt.date())
