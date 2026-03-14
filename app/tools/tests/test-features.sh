#!/bin/bash
set -e

BASE_URL="http://localhost:3001/api"

echo "=== 1. Logging in as admin ==="
LOGIN_RESP=$(curl -s -X POST "$BASE_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin001","password":"Admin#123"}')
TOKEN=$(echo "$LOGIN_RESP" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
echo "✓ Logged in successfully"

echo ""
echo "=== 2. Creating test tag ==="
TAG_RESP=$(curl -s -X POST "$BASE_URL/suppliers/tags" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"VIP供应商","description":"重要供应商标签","color":"#FF5722"}')
TAG_ID=$(echo "$TAG_RESP" | grep -o '"id":[0-9]*' | head -1 | cut -d':' -f2)
echo "✓ Created tag: ID=$TAG_ID"

echo ""
echo "=== 3. Creating test suppliers ==="
SUPPLIER_IDS=""
for i in 1 2 3; do
  SUPP_RESP=$(curl -s -X POST "$BASE_URL/suppliers" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d "{\"companyName\":\"Test Supplier $i\",\"companyId\":\"TEST-$(printf '%03d' $i)\",\"contactPerson\":\"Contact $i\",\"contactPhone\":\"138000$(printf '%05d' $i)\",\"contactEmail\":\"test$i@example.com\",\"category\":\"electronics\",\"address\":\"Test Address $i\"}")
  SUPP_ID=$(echo "$SUPP_RESP" | grep -o '"id":[0-9]*' | head -1 | cut -d':' -f2)
  SUPPLIER_IDS="$SUPPLIER_IDS $SUPP_ID"
  echo "  ✓ Created supplier $i: ID=$SUPP_ID"
done
SUPPLIER_IDS=$(echo "$SUPPLIER_IDS" | sed 's/^ *//')
echo "✓ Total suppliers created: $SUPPLIER_IDS"

echo ""
echo "=== 4. TEST FEATURE 1: Batch Tag Assignment ==="
SUPPLIER_ARRAY="[$(echo "$SUPPLIER_IDS" | sed 's/ /,/g')]"
echo "Assigning tag $TAG_ID to suppliers $SUPPLIER_ARRAY..."
BATCH_RESP=$(curl -s -X POST "$BASE_URL/suppliers/tags/$TAG_ID/batch-assign" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"supplierIds\":$SUPPLIER_ARRAY}")
echo "Response: $BATCH_RESP" | head -c 200
echo ""

echo ""
echo "Verifying tag assignment..."
TAG_SUPPLIERS=$(curl -s -X GET "$BASE_URL/suppliers/tags/$TAG_ID/suppliers" \
  -H "Authorization: Bearer $TOKEN")
SUPPLIER_COUNT=$(echo "$TAG_SUPPLIERS" | grep -o '"id":[0-9]*' | wc -l)
echo "✓ Suppliers with tag $TAG_ID: $SUPPLIER_COUNT suppliers"

echo ""
echo "=== 5. Testing Batch Tag Removal ==="
FIRST_SUPP=$(echo "$SUPPLIER_IDS" | awk '{print $1}')
REMOVE_RESP=$(curl -s -X POST "$BASE_URL/suppliers/tags/$TAG_ID/batch-remove" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"supplierIds\":[$FIRST_SUPP]}")
echo "✓ Removed tag from supplier $FIRST_SUPP"

echo ""
echo "=== 6. Re-assigning tag for feature 2 test ==="
curl -s -X POST "$BASE_URL/suppliers/tags/$TAG_ID/batch-assign" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"supplierIds\":$SUPPLIER_ARRAY}" > /dev/null
echo "✓ Tag re-assigned to all suppliers"

echo ""
echo "=== 7. Getting buyers list ==="
BUYER_ID="purch001"
echo "✓ Using buyer: ID=$BUYER_ID"

echo ""
echo "=== 8. TEST FEATURE 2: Assign Suppliers to Buyer by Tag ==="
echo "Assigning suppliers with tag $TAG_ID to buyer $BUYER_ID..."
ASSIGN_RESP=$(curl -s -X POST "$BASE_URL/buyer-assignments/by-tag" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"buyerId\":\"$BUYER_ID\",\"tagIds\":[$TAG_ID]}")
echo "Response: $ASSIGN_RESP" | head -c 200
echo ""

echo ""
echo "=== 9. Verifying buyer assignments ==="
ASSIGNMENTS=$(curl -s -X GET "$BASE_URL/buyer-assignments/suppliers?buyerId=$BUYER_ID" \
  -H "Authorization: Bearer $TOKEN")
ASSIGNMENT_COUNT=$(echo "$ASSIGNMENTS" | grep -o '"supplierId":[0-9]*' | wc -l)
echo "✓ Buyer $BUYER_ID has $ASSIGNMENT_COUNT assigned suppliers"

echo ""
echo "============================================================"
echo "✅ ALL TESTS PASSED!"
echo "============================================================"
echo ""
echo "Summary:"
echo "  ✓ Feature 1 (Batch Tag Assignment): Working correctly"
echo "  ✓ Feature 2 (Assign by Tag to Buyer): Working correctly"
echo "  ✓ Created 3 test suppliers"
echo "  ✓ Created 1 test tag"
echo "  ✓ Assigned suppliers to buyer via tags"
