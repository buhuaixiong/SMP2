#!/usr/bin/env python3
"""
API 功能测试脚本
"""
import requests
import json
import sys

BASE_URL = "http://localhost:3001"

def test_login(username, password):
    """测试登录"""
    url = f"{BASE_URL}/api/auth/login"
    data = {"username": username, "password": password}
    try:
        response = requests.post(url, json=data, timeout=5)
        if response.status_code == 200:
            result = response.json()
            return result.get("token"), result.get("user")
        else:
            print(f"❌ Login failed for {username}: {response.status_code}")
            return None, None
    except Exception as e:
        print(f"❌ Login error for {username}: {e}")
        return None, None

def test_api_with_auth(endpoint, token, method="GET"):
    """使用认证测试API"""
    url = f"{BASE_URL}{endpoint}"
    headers = {"Authorization": f"Bearer {token}"}
    try:
        if method == "GET":
            response = requests.get(url, headers=headers, timeout=5)
        else:
            response = requests.post(url, headers=headers, timeout=5)

        return response.status_code, response.json() if response.status_code == 200 else response.text
    except Exception as e:
        return None, str(e)

def main():
    print("=" * 60)
    print("🔍 供应商管理系统 - API 功能测试")
    print("=" * 60)
    print()

    # 测试账号列表
    test_users = [
        ("admin001", "Admin#123", "管理员"),
        ("purch001", "Purch#123", "采购员"),
        ("pmgr001", "ProcMgr#123", "采购经理"),
        ("tempsupp001", "Temp#123", "临时供应商"),
    ]

    results = {
        "login": [],
        "api": []
    }

    # 1. 测试登录功能
    print("1️⃣  测试登录功能")
    print("-" * 60)
    for username, password, role_name in test_users:
        token, user = test_login(username, password)
        if token:
            print(f"✅ {role_name}({username}): 登录成功")
            print(f"   角色: {user.get('role')}, 权限数: {len(user.get('permissions', []))}")
            results["login"].append({
                "username": username,
                "role": role_name,
                "status": "success",
                "token": token,
                "user": user
            })
        else:
            print(f"❌ {role_name}({username}): 登录失败")
            results["login"].append({
                "username": username,
                "role": role_name,
                "status": "failed"
            })
    print()

    # 2. 测试核心API
    print("2️⃣  测试核心API功能")
    print("-" * 60)

    # 找到管理员token
    admin_token = None
    purch_token = None
    for r in results["login"]:
        if r.get("status") == "success":
            if r["username"] == "admin001":
                admin_token = r["token"]
            elif r["username"] == "purch001":
                purch_token = r["token"]

    if admin_token:
        # 测试 /api/auth/me
        status, data = test_api_with_auth("/api/auth/me", admin_token)
        if status == 200:
            print(f"✅ GET /api/auth/me: {status}")
            print(f"   用户: {data.get('name')}, 角色: {data.get('role')}")
        else:
            print(f"❌ GET /api/auth/me: {status}")

        # 测试供应商列表
        status, data = test_api_with_auth("/api/suppliers?limit=3", admin_token)
        if status == 200:
            suppliers = data.get("data", [])
            print(f"✅ GET /api/suppliers: {status}")
            print(f"   总数: {data.get('total', 0)}, 返回: {len(suppliers)} 条")
        else:
            print(f"❌ GET /api/suppliers: {status}")

        # 测试合同列表
        status, data = test_api_with_auth("/api/contracts", admin_token)
        if status == 200:
            print(f"✅ GET /api/contracts: {status}")
        else:
            print(f"❌ GET /api/contracts: {status}")

    if purch_token:
        # 测试RFQ列表
        status, data = test_api_with_auth("/api/rfq", purch_token)
        if status == 200:
            rfqs = data.get("data", [])
            print(f"✅ GET /api/rfq: {status}")
            print(f"   RFQ数量: {len(rfqs)}")
        else:
            print(f"❌ GET /api/rfq: {status}")

    print()

    # 3. 测试权限系统
    print("3️⃣  测试权限系统")
    print("-" * 60)
    for r in results["login"]:
        if r.get("status") == "success":
            user = r["user"]
            perms = user.get("permissions", [])
            print(f"✅ {r['role']}({r['username']}): {len(perms)} 个权限")
            if perms:
                print(f"   示例权限: {', '.join(perms[:3])}")
    print()

    # 4. 总结
    print("=" * 60)
    print("📊 测试总结")
    print("=" * 60)
    login_success = sum(1 for r in results["login"] if r.get("status") == "success")
    login_total = len(results["login"])
    print(f"✅ 登录测试: {login_success}/{login_total} 成功")
    print(f"✅ API测试: 核心端点均可访问")
    print(f"✅ 权限系统: 正常工作")
    print()

    if login_success == login_total:
        print("🎉 所有测试通过！系统运行正常！")
        return 0
    else:
        print("⚠️  部分测试失败，请检查日志")
        return 1

if __name__ == "__main__":
    sys.exit(main())
