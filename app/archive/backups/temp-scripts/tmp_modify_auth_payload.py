from pathlib import Path
path = Path("supplier-backend/utils/auth.js")
text = path.read_text(encoding="utf-8")

# Insert helper query before payload creation
group_query_snippet = "const functionalPermissions = getPermissionsByFunctions(Array.from(functions));\n\n"
if "const purchasingGroupMemberships" not in text:
    replacement = group_query_snippet + "const purchasingGroupMemberships = db.prepare(`\n    SELECT\n      pgm.groupId,\n      pgm.role as memberRole,\n      pg.name as groupName,\n      pg.code as groupCode\n    FROM purchasing_group_members pgm\n    JOIN purchasing_groups pg ON pgm.groupId = pg.id\n    WHERE pgm.buyerId = ? AND pg.deletedAt IS NULL\n  `).all(user.id);\n\n  const purchasingGroups = purchasingGroupMemberships.map((membership) => ({\n    id: membership.groupId,\n    code: membership.groupCode,\n    name: membership.groupName,\n    memberRole: membership.memberRole,\n  }));\n\n  const functionalPermissions = getPermissionsByFunctions(Array.from(functions));\n\n"
    text = text.replace(group_query_snippet, replacement, 1)

payload_marker = "    functions: Array.from(functions),\n  };"
if "purchasingGroups" not in text:
    text = text.replace(payload_marker, "    functions: Array.from(functions),\n    purchasingGroups,\n  };")

path.write_text(text, encoding='utf-8')
