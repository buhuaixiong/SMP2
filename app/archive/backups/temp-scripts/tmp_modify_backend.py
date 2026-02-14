from pathlib import Path
path = Path("supplier-backend/utils/auth.js")
text = path.read_text(encoding="utf-8")
marker = "  const functionalPermissions = getPermissionsByFunctions(Array.from(functions));\n\n"
if "purchasingGroupMemberships" not in text:
    replacement = (
        "  const functionalPermissions = getPermissionsByFunctions(Array.from(functions));\n\n"
        "  const purchasingGroupMemberships = db.prepare(`\n"
        "    SELECT\n"
        "      pgm.groupId,\n"
        "      pgm.role as memberRole,\n"
        "      pg.name as groupName,\n"
        "      pg.code as groupCode\n"
        "    FROM purchasing_group_members pgm\n"
        "    JOIN purchasing_groups pg ON pgm.groupId = pg.id\n"
        "    WHERE pgm.buyerId = ? AND pg.deletedAt IS NULL\n"
        "  `).all(user.id);\n\n"
        "  const purchasingGroups = purchasingGroupMemberships.map((membership) => ({\n"
        "    id: membership.groupId,\n"
        "    code: membership.groupCode,\n"
        "    name: membership.groupName,\n"
        "    memberRole: membership.memberRole,\n"
        "  }));\n\n"
    )
    text = text.replace(marker, replacement, 1)

payload_marker = "    functions: Array.from(functions),\n  };"
if "purchasingGroups" not in text:
    text = text.replace(payload_marker, "    functions: Array.from(functions),\n    purchasingGroups,\n  };")

path.write_text(text, encoding='utf-8')
