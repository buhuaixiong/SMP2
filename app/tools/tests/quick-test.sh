#!/bin/bash
# 快速系统健康检查脚本

echo "=========================================="
echo "  供应商管理系统 - 快速健康检查"
echo "=========================================="
echo ""

# 颜色定义
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 检查后端
echo "1. 检查后端服务..."
if curl -s http://localhost:3001/healthz > /dev/null; then
    echo -e "${GREEN}✅ 后端服务运行正常 (http://localhost:3001)${NC}"
else
    echo -e "${RED}❌ 后端服务未运行${NC}"
    exit 1
fi

# 检查前端
echo "2. 检查前端服务..."
if curl -s http://localhost:5173 > /dev/null; then
    echo -e "${GREEN}✅ 前端服务运行正常 (http://localhost:5173)${NC}"
else
    echo -e "${RED}❌ 前端服务未运行${NC}"
    exit 1
fi

# 测试登录
echo "3. 测试登录功能..."
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:3001/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"username":"admin001","password":"Admin#123"}')

if echo "$LOGIN_RESPONSE" | grep -q "token"; then
    echo -e "${GREEN}✅ 登录功能正常${NC}"
else
    echo -e "${RED}❌ 登录功能异常${NC}"
    exit 1
fi

# 检查数据库
echo "4. 检查数据库..."
if [ -f "supplier-backend/supplier.db" ]; then
    SIZE=$(du -h supplier-backend/supplier.db | cut -f1)
    echo -e "${GREEN}✅ 数据库文件存在 (${SIZE})${NC}"
else
    echo -e "${RED}❌ 数据库文件不存在${NC}"
    exit 1
fi

# 系统状态
echo "5. 获取系统状态..."
STATUS=$(curl -s http://localhost:3001/status)
USERS=$(echo "$STATUS" | grep -o '"users":[0-9]*' | grep -o '[0-9]*')
SUPPLIERS=$(echo "$STATUS" | grep -o '"suppliers":[0-9]*' | grep -o '[0-9]*')

if [ ! -z "$USERS" ] && [ ! -z "$SUPPLIERS" ]; then
    echo -e "${GREEN}✅ 数据统计: ${USERS} 用户, ${SUPPLIERS} 供应商${NC}"
else
    echo -e "${YELLOW}⚠️ 无法获取数据统计${NC}"
fi

echo ""
echo "=========================================="
echo -e "${GREEN}✅ 所有检查通过！系统运行正常！${NC}"
echo "=========================================="
echo ""
echo "快速访问:"
echo "  前端: http://localhost:5173"
echo "  后端: http://localhost:3001"
echo "  健康检查: http://localhost:3001/healthz"
echo "  系统状态: http://localhost:3001/status"
echo ""
echo "测试账号:"
echo "  管理员: admin001 / Admin#123"
echo "  采购员: purch001 / Purch#123"
echo "  供应商: formsupp001 / Formal#123"
echo ""
echo "详细报告: SYSTEM-HEALTH-REPORT.md"
echo "故障排查: TROUBLESHOOTING.md"
echo ""
