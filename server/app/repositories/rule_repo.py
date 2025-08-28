from typing import List, Optional, Dict, Any
from uuid import UUID

from sqlalchemy import select, and_, or_, desc
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.event import Rule


class RuleRepository:
    """
    规则仓库
    提供规则数据的访问抽象
    """
    def __init__(self, db: AsyncSession):
        self.db = db
    
    async def get_all_active(self) -> List[Rule]:
        """获取所有激活的规则"""
        query = select(Rule).where(Rule.is_active == True).order_by(desc(Rule.priority))
        result = await self.db.execute(query)
        return result.scalars().all()
    
    async def get_by_id(self, rule_id: UUID) -> Optional[Rule]:
        """根据 ID 获取规则"""
        result = await self.db.execute(select(Rule).where(Rule.id == rule_id))
        return result.scalars().first()
    
    async def create(self, rule_data: Dict[str, Any]) -> Rule:
        """创建新规则"""
        rule = Rule(**rule_data)
        self.db.add(rule)
        await self.db.flush()
        return rule
    
    async def update(self, rule_id: UUID, rule_data: Dict[str, Any]) -> Optional[Rule]:
        """更新规则"""
        rule = await self.get_by_id(rule_id)
        if not rule:
            return None
            
        for key, value in rule_data.items():
            setattr(rule, key, value)
            
        await self.db.flush()
        return rule
    
    async def delete(self, rule_id: UUID) -> bool:
        """删除规则"""
        rule = await self.get_by_id(rule_id)
        if not rule:
            return False
            
        await self.db.delete(rule)
        await self.db.flush()
        return True
