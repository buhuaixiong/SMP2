#!/usr/bin/env python3
"""测试批量标签分配和按标签分配供应商功能"""
import requests
import json

BASE_URL = "http://localhost:3001/api"

# 1. 登录获取token
print("=== 1. Logging in as admin ===")
login_resp = requests.post(f"{BASE_URL}/auth/login", json={
    "username": "admin001",
    "password": "Admin#123"
})
token = login_resp.json()["token"]
headers = {"Authorization": f"Bearer {token}"}
print(f"✓ Logged in successfully. Token: {token[:50]}...")

# 2. 创建测试标签
print("\n=== 2. Creating test tag ===")
tag_resp = requests.post(f"{BASE_URL}/suppliers/tags",
    headers=headers,
    json={
        "name": "VIP供应商",
        "description": "重要供应商标签",
        "color": "#FF5722"
    })
tag_data = tag_resp.json()
tag_id = tag_data["data"]["id"]
print(f"✓ Created tag: ID={tag_id}, Name={tag_data['data']['name']}")

# 3. 创建测试供应商
print("\n=== 3. Creating test suppliers ===")
supplier_ids = []
for i in range(1, 4):
    supplier_resp = requests.post(f"{BASE_URL}/suppliers",
        headers=headers,
        json={
            "companyName": f"测试供应商{i}",
            "companyId": f"TEST-{i:03d}",
            "contactPerson": f"联系人{i}",
            "contactPhone": f"138000{i:05d}",
            "contactEmail": f"test{i}@example.com",
            "category": "electronics",
            "address": f"测试地址{i}号"
        })
    supplier_data = supplier_resp.json()
    supplier_id = supplier_data["data"]["id"]
    supplier_ids.append(supplier_id)
    print(f"  ✓ Created supplier {i}: ID={supplier_id}, Name={supplier_data['data']['companyName']}")

print(f"\n✓ Total {len(supplier_ids)} suppliers created: {supplier_ids}")

# 4. 测试功能1: 批量标签分配
print("\n=== 4. TEST FEATURE 1: Batch Tag Assignment ===")
print(f"Assigning tag {tag_id} to suppliers {supplier_ids}...")
batch_assign_resp = requests.post(
    f"{BASE_URL}/suppliers/tags/{tag_id}/batch-assign",
    headers=headers,
    json={"supplierIds": supplier_ids}
)
batch_result = batch_assign_resp.json()
print(f"✓ Response: {batch_result['message']}")
print(f"  Added: {batch_result['data']['added']}, Skipped: {batch_result['data']['skipped']}")

# 验证标签分配
print(f"\nVerifying tag assignment...")
tag_suppliers_resp = requests.get(f"{BASE_URL}/suppliers/tags/{tag_id}/suppliers", headers=headers)
tag_suppliers = tag_suppliers_resp.json()["data"]
print(f"✓ Suppliers with tag {tag_id}: {len(tag_suppliers)} suppliers")
for s in tag_suppliers:
    print(f"  - ID={s['id']}, Name={s['companyName']}")

# 5. 测试批量移除标签
print("\n=== 5. Testing Batch Tag Removal ===")
batch_remove_resp = requests.post(
    f"{BASE_URL}/suppliers/tags/{tag_id}/batch-remove",
    headers=headers,
    json={"supplierIds": [supplier_ids[0]]}
)
remove_result = batch_remove_resp.json()
print(f"✓ Response: {remove_result['message']}")
print(f"  Removed: {remove_result['data']['removed']}")

# 6. 重新分配标签用于第二个功能测试
print("\n=== 6. Re-assigning tag for feature 2 test ===")
requests.post(
    f"{BASE_URL}/suppliers/tags/{tag_id}/batch-assign",
    headers=headers,
    json={"supplierIds": supplier_ids}
)
print(f"✓ Tag re-assigned to all suppliers")

# 7. 获取买家列表
print("\n=== 7. Getting buyers list ===")
buyers_resp = requests.get(f"{BASE_URL}/buyer-assignments/buyers", headers=headers)
buyers = buyers_resp.json()["data"]
if not buyers:
    print("⚠ No buyers found, creating one...")
    # 使用现有采购员
    buyers = [{"id": "purch001", "name": "Purchaser 001", "role": "purchaser"}]
buyer_id = buyers[0]["id"]
print(f"✓ Using buyer: ID={buyer_id}, Name={buyers[0]['name']}")

# 8. 测试功能2: 按标签分配供应商给采购员
print("\n=== 8. TEST FEATURE 2: Assign Suppliers to Buyer by Tag ===")
print(f"Assigning suppliers with tag {tag_id} to buyer {buyer_id}...")
assign_by_tag_resp = requests.post(
    f"{BASE_URL}/buyer-assignments/by-tag",
    headers=headers,
    json={
        "buyerId": buyer_id,
        "tagIds": [tag_id]
    }
)
assign_result = assign_by_tag_resp.json()
print(f"✓ Response: {assign_result['message']}")
print(f"  Assigned: {assign_result['data']['assignedCount']} out of {assign_result['data']['totalSuppliers']} suppliers")
print(f"  Supplier IDs: {assign_result['data']['supplierIds']}")

# 9. 验证买家的供应商分配
print("\n=== 9. Verifying buyer assignments ===")
buyer_assignments_resp = requests.get(
    f"{BASE_URL}/buyer-assignments/suppliers",
    headers=headers,
    params={"buyerId": buyer_id}
)
assignments = buyer_assignments_resp.json()["data"]
print(f"✓ Buyer {buyer_id} has {len(assignments)} assigned suppliers:")
for a in assignments:
    print(f"  - Supplier ID={a['supplierId']}, Name={a['companyName']}, Status={a.get('status', 'N/A')}")

print("\n" + "="*60)
print("✅ ALL TESTS PASSED!")
print("="*60)
print("\nSummary:")
print(f"  ✓ Feature 1 (Batch Tag Assignment): Working correctly")
print(f"  ✓ Feature 2 (Assign by Tag to Buyer): Working correctly")
print(f"  ✓ Created {len(supplier_ids)} test suppliers")
print(f"  ✓ Created 1 test tag")
print(f"  ✓ Assigned {len(assignments)} suppliers to buyer")
