from pathlib import Path
path = Path("supplier-backend/utils/auth.js")
text = path.read_text(encoding="utf-8")

if "const isPurchasingGroupLeader" not in text:
    insert_point = "const purchasingGroups = purchasingGroupMemberships.map((membership) => ({\n    id: membership.groupId,\n    code: membership.groupCode,\n    name: membership.groupName,\n    memberRole: membership.memberRole,\n  }));\n\n"
    if insert_point not in text:
        raise SystemExit('purchasingGroups mapping not found')
    text = text.replace(insert_point, insert_point + "  const isPurchasingGroupLeader = purchasingGroups.some((group) => group.memberRole === 'lead');\n\n")

payload_marker = "    functions: Array.from(functions),\n    purchasingGroups,\n  };"
if "purchasingGroups" not in payload_marker:
    pass
if "purchasingGroups" not in text:
    text = text.replace("    functions: Array.from(functions),\n  };", "    functions: Array.from(functions),\n    purchasingGroups,\n    isPurchasingGroupLeader,\n  };")
else:
    text = text.replace("    functions: Array.from(functions),\n  };", "    functions: Array.from(functions),\n    isPurchasingGroupLeader,\n  };")

path.write_text(text, encoding='utf-8')
