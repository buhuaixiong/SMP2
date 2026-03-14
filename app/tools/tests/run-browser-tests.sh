#!/bin/bash
# 浏览器兼容性自动化测试运行脚本

echo "=========================================="
echo "  浏览器兼容性自动化测试"
echo "=========================================="
echo ""

# 颜色定义
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 检查依赖
echo "1. 检查依赖..."
if ! command -v npx &> /dev/null; then
    echo "❌ npx 未安装，请先安装 Node.js"
    exit 1
fi

# 检查 Playwright 是否安装
if [ ! -d "node_modules/@playwright" ]; then
    echo -e "${YELLOW}⚠️  Playwright 未安装${NC}"
    echo "正在安装 Playwright..."
    npm install -D @playwright/test

    echo "正在安装浏览器..."
    npx playwright install
fi

echo -e "${GREEN}✅ 依赖检查完成${NC}"
echo ""

# 选择测试模式
echo "选择测试模式:"
echo "1) 所有浏览器 (Chrome + Edge + Firefox + Safari)"
echo "2) 仅Chrome"
echo "3) 仅Edge"
echo "4) 仅Firefox"
echo "5) 仅Safari (WebKit)"
echo ""
read -p "请选择 (1-5): " choice

case $choice in
    1)
        echo "运行所有浏览器测试..."
        npx playwright test
        ;;
    2)
        echo "运行 Chrome 测试..."
        npx playwright test --project=chromium
        ;;
    3)
        echo "运行 Edge 测试..."
        npx playwright test --project=edge
        ;;
    4)
        echo "运行 Firefox 测试..."
        npx playwright test --project=firefox
        ;;
    5)
        echo "运行 Safari (WebKit) 测试..."
        npx playwright test --project=webkit
        ;;
    *)
        echo "无效选择，运行所有浏览器测试..."
        npx playwright test
        ;;
esac

echo ""
echo "=========================================="
echo "测试完成！"
echo ""
echo "查看测试报告:"
echo "  npx playwright show-report"
echo ""
echo "测试结果位置:"
echo "  - HTML报告: playwright-report/index.html"
echo "  - JSON结果: test-results/results.json"
echo "  - 截图: test-results/ 目录"
echo "=========================================="
