# 仓库清理执行计划

**生成时间**: 2025-10-31
**状态**: 待用户确认

---

## ✅ 验证结果总结

### 1. 脚本引用检查
- ✅ `package.json` 中**没有**引用任何临时测试脚本
- ✅ 临时脚本仅在中文文档中被引用（如`如何查看后端日志.md`）
- ✅ 没有 CI/CD 配置文件（无 `.github/workflows/` 或 `.gitlab-ci.yml`）

### 2. 发现的问题

#### A. extracted_backend/ 目录
- **路径**: `C:\supplier-system\extracted_backend/supplier-backend/`
- **状态**: 完整的 `supplier-backend` 副本（包含 `node_modules`）
- **风险**: 高 - 占用大量空间，完全冗余
- **建议**: **立即删除整个目录**

#### B. 运行期日志文件（已提交）
```
supplier-backend/backend.log          (5.5KB)
supplier-backend/backend-debug.log    (1.2KB)
supplier-backend/backend-new.log      (4.7KB)
```
- **问题**: 违反 `.gitignore` 规则但已提交
- **风险**: 中 - 可能包含敏感信息
- **建议**: 从仓库中删除，添加到 `.gitignore`

#### C. 数据库备份文件
```
supplier-backend/database.db.backup-20251030  (448KB)
```
- **问题**: 二进制备份文件被提交
- **风险**: 高 - 包含生产数据，违反最佳实践
- **建议**: 从 Git 历史中彻底清除

#### D. 审计日志归档
```
supplier-backend/audit-archive/2025-10-28/
supplier-backend/audit-archive/2025-10-29/
supplier-backend/audit-archive/2025-10-30/
```
- **问题**: 运行期生成的敏感审计日志
- **风险**: 高 - 包含用户操作记录
- **建议**: 从仓库删除，添加到 `.gitignore`

---

## 📋 分阶段清理方案

### 🔵 阶段 0: 安全准备（立即执行）

#### 步骤 0.1: 创建完整备份
```bash
# 创建 Git bundle 备份
git bundle create ../supplier-system-backup-$(date +%Y%m%d).bundle --all

# 或创建 zip 备份
cd ..
tar -czf supplier-system-backup-$(date +%Y%m%d).tar.gz supplier-system/
```

#### 步骤 0.2: 创建清理前的 Git tag
```bash
git tag -a pre-cleanup-2025-10-31 -m "仓库清理前的状态快照"
git push origin pre-cleanup-2025-10-31  # 如果有远程仓库
```

---

### 🟢 阶段 1: 更新 `.gitignore`（低风险）

创建增强的 `.gitignore` 规则：

```gitignore
# ============================================
# 运行期日志（新增）
# ============================================
supplier-backend/backend*.log
supplier-backend/*.log
logs/
*.log

# ============================================
# 数据库文件（增强）
# ============================================
*.db
*.db-shm
*.db-wal
*.sqlite
*.sqlite3
supplier-backend/supplier.db*
supplier.db*
# 数据库备份（新增）
*.backup-*
database.db.backup-*

# ============================================
# 审计日志归档（新增）
# ============================================
audit-archive/
supplier-backend/audit-archive/

# ============================================
# 临时开发文件（新增）
# ============================================
# Python 临时脚本
tmp_*.py
temp_*.py

# Vue 临时文件
tmp_*.vue
temp_*.vue

# 中文批处理脚本
诊断*.bat
重启*.bat
*诊断*.bat

# 临时文本文件
修复清单.md
如何查看*.md

# ============================================
# 冗余副本目录（新增）
# ============================================
extracted_backend/

# ============================================
# 运行期生成文件（新增）
# ============================================
migration-report.json
```

**执行命令**:
```bash
# 将上述内容追加到 .gitignore
# 然后提交
git add .gitignore
git commit -m "chore: 增强 .gitignore 规则防止临时文件入库"
```

---

### 🟡 阶段 2: 从工作区删除文件（中等风险）

#### 2.1 删除完全冗余的目录

```bash
# 删除 extracted_backend（完整副本）
rm -rf extracted_backend/

# 从 Git 索引中删除（如果已跟踪）
git rm -r --cached extracted_backend/ 2>/dev/null || true
```

**预期效果**: 节省 ~100-200MB 空间

#### 2.2 删除临时 Python/Vue 文件

```bash
# 删除临时脚本
rm -f tmp_*.py tmp_*.vue

# 从 Git 删除
git rm --cached tmp_*.py tmp_*.vue 2>/dev/null || true
```

#### 2.3 删除运行期日志文件

```bash
# 删除日志文件
rm -f supplier-backend/backend*.log

# 从 Git 删除
git rm --cached supplier-backend/backend*.log 2>/dev/null || true
```

#### 2.4 删除审计归档

```bash
# 删除审计归档目录
rm -rf supplier-backend/audit-archive/

# 从 Git 删除
git rm -r --cached supplier-backend/audit-archive/ 2>/dev/null || true
```

#### 2.5 删除数据库备份

```bash
# 删除备份文件
rm -f supplier-backend/database.db.backup-*
rm -f supplier-backend/*.backup-*

# 从 Git 删除
git rm --cached supplier-backend/*.backup-* 2>/dev/null || true
```

**提交更改**:
```bash
git add -A
git commit -m "chore: 删除运行期文件和冗余副本

- 删除 extracted_backend/ 完整副本
- 删除运行期日志文件 (backend*.log)
- 删除审计归档 (audit-archive/)
- 删除数据库备份文件 (*.backup-*)
- 删除临时脚本 (tmp_*.py, tmp_*.vue)
"
```

---

### 🟠 阶段 3: 整理临时测试脚本（需谨慎）

#### 3.1 创建归档目录

```bash
mkdir -p supplier-backend/scripts/archive/diagnostic
mkdir -p supplier-backend/scripts/archive/migration-runners
```

#### 3.2 保留有价值的脚本并移动

**保留并移动的脚本**（可能有复用价值）:
```bash
# 诊断工具
mv supplier-backend/diagnose-rfq-submission.js supplier-backend/scripts/archive/diagnostic/
mv supplier-backend/identify-backend-process.js supplier-backend/scripts/archive/diagnostic/
mv supplier-backend/check-active-db.js supplier-backend/scripts/archive/diagnostic/

# 迁移运行器（作为历史参考）
mv supplier-backend/run-migration-009.js supplier-backend/scripts/archive/migration-runners/
mv supplier-backend/run-migration-010.js supplier-backend/scripts/archive/migration-runners/
mv supplier-backend/migrate-supplier-invitations.js supplier-backend/scripts/archive/migration-runners/

# 数据修复工具
mv supplier-backend/fix-swiftcode.js supplier-backend/scripts/archive/
mv supplier-backend/fix-token-table-schema.js supplier-backend/scripts/archive/

# 密码重置工具（实用）
mv supplier-backend/reset-password.js supplier-backend/scripts/
```

#### 3.3 删除一次性测试脚本

**可安全删除的脚本**（纯调试/验证用途）:
```bash
cd supplier-backend

# 删除所有 test-* 脚本（不是正式测试套件）
rm -f test-*.js

# 删除所有 check-* 脚本（除了已移动的）
rm -f check-*.js

# 删除所有 debug-* 脚本
rm -f debug-*.js

# 删除 RFQ 测试脚本
rm -f create-test-rfq.js complete-test-rfq.js

# 删除临时脚本
rm -f temp_*.js tmp-*.js test.js

# 删除中文批处理脚本
rm -f *.bat
```

**创建 README 说明归档内容**:
```bash
cat > supplier-backend/scripts/archive/README.md << 'EOF'
# 归档脚本

本目录包含历史性或仅供参考的脚本，不应在生产环境中使用。

## diagnostic/
一次性诊断工具，用于解决特定历史问题。

## migration-runners/
特定迁移的运行器，迁移已应用，保留仅供参考。

## 其他
数据修复工具，解决特定数据问题后不再需要。

**警告**: 这些脚本可能依赖旧的数据结构或假设，直接运行可能导致问题。
EOF
```

**提交**:
```bash
git add supplier-backend/scripts/
git add -u  # 暂存删除
git commit -m "chore: 整理临时测试脚本

- 保留诊断和实用工具到 scripts/ 目录
- 删除一次性测试脚本 (test-*.js, check-*.js, debug-*.js)
- 删除中文批处理脚本
- 添加归档脚本说明文档
"
```

---

### 🟡 阶段 4: 整理文档（中等风险）

#### 4.1 创建文档目录结构

```bash
mkdir -p docs/implementation-reports
mkdir -p docs/zh
mkdir -p docs/guides
```

#### 4.2 移动根目录文档

```bash
# 实施报告 → docs/implementation-reports/
mv ADVANCED-SECURITY-TEST-REPORT.md docs/implementation-reports/
mv AUDIT-LOG-ENHANCEMENT-SUMMARY.md docs/implementation-reports/
mv BATCH-TAG-AND-BUYER-ASSIGNMENT-IMPLEMENTATION.md docs/implementation-reports/
mv BATCH-TAG-BUYER-FEATURES-GUIDE.md docs/implementation-reports/
mv BULK-DOCUMENT-IMPORT-IMPLEMENTATION.md docs/implementation-reports/
mv EMAIL-SETTINGS-GUIDE.md docs/implementation-reports/
mv EMAIL-SETTINGS-IMPLEMENTATION-SUMMARY.md docs/implementation-reports/
mv EMERGENCY-LOCKDOWN-IMPLEMENTATION.md docs/implementation-reports/
mv FILE-UPLOAD-APPROVAL-IMPLEMENTATION.md docs/implementation-reports/
mv FILE-VALIDATION-IMPLEMENTATION.md docs/implementation-reports/
mv FILE-VALIDITY-AND-REMINDER-IMPLEMENTATION.md docs/implementation-reports/
mv FINAL-PERFORMANCE-TEST-SUMMARY.md docs/implementation-reports/
mv FRONTEND-IMPLEMENTATION-COMPLETE.md docs/implementation-reports/

# 浏览器相关 → docs/guides/
mv BROWSER-CACHE-CLEAR-GUIDE.md docs/guides/
mv BROWSER-COMPATIBILITY-GUIDE.md docs/guides/
mv BROWSER-TEST-CHECKLIST.md docs/guides/
mv BROWSER-TESTING-SUMMARY.md docs/guides/

# 修复和解决方案文档 → docs/
mv fix-template-download.md docs/
# RFQ-提交问题-解决方案.md 已在 docs/RFQ-SUBMISSION-FIX.md 中重复，删除
rm -f RFQ-提交问题-解决方案.md

# 中文文档 → docs/zh/
mv 如何查看后端日志.md docs/zh/
mv 修复清单.md docs/zh/
mv README-问题已修复.txt docs/zh/

# 临时文档直接删除
rm -f SOLUTION-SUMMARY.md
rm -f QUICK-START-GUIDE.md  # 内容应整合到主 README
rm -f migration-report.json
rm -f PORT-CONFLICT-FIX.md  # 临时问题记录
```

#### 4.3 处理重复文档

```bash
# DEPLOYMENT.md 在根目录和 docs/ 都有，保留 docs/DEPLOYMENT.md
rm -f DEPLOYMENT.md
```

#### 4.4 更新 docs/index.md（文档导航）

创建 `docs/index.md`:
```markdown
# 文档索引

## 📚 核心文档
- [部署指南](DEPLOYMENT.md)
- [用户手册](USER_GUIDE.md)
- [发布前检查清单](PRE-LAUNCH-CHECKLIST.md)

## 🛠️ 实施报告
详见 [implementation-reports/](implementation-reports/) 目录

## 🌐 浏览器兼容性
详见 [guides/](guides/) 目录

## 🔒 安全修复
- [P0 安全修复实施](P0-SECURITY-FIXES-IMPLEMENTED.md)
- [存储安全修复](STORAGE-SECURITY-FIXES-V2.md)
- [2025-01-22 安全修复](SECURITY-FIXES-2025-01-22.md)

## 🐛 问题修复记录
- [RFQ 提交问题修复](RFQ-SUBMISSION-FIX.md)
- [数据库架构修复](DATABASE-SCHEMA-FIX-2025-10-30.md)

## 📖 中文文档
详见 [zh/](zh/) 目录
```

**提交**:
```bash
git add docs/
git add -u
git commit -m "docs: 重组文档结构

- 移动实施报告到 docs/implementation-reports/
- 移动指南到 docs/guides/
- 移动中文文档到 docs/zh/
- 删除重复和临时文档
- 添加文档索引页面
"
```

---

### 🔴 阶段 5: 从 Git 历史中清除敏感文件（高风险）

**⚠️ 警告**: 此操作会**重写 Git 历史**，需要团队协调！

#### 5.1 安装 git-filter-repo

```bash
# macOS
brew install git-filter-repo

# Ubuntu/Debian
apt install git-filter-repo

# 或使用 pip
pip install git-filter-repo
```

#### 5.2 清除大文件和敏感文件

```bash
# 清除数据库备份
git filter-repo --path supplier-backend/database.db.backup-20251030 --invert-paths

# 清除审计归档
git filter-repo --path supplier-backend/audit-archive --invert-paths

# 清除日志文件
git filter-repo --path supplier-backend/backend.log --invert-paths
git filter-repo --path supplier-backend/backend-debug.log --invert-paths
git filter-repo --path supplier-backend/backend-new.log --invert-paths

# 清除 extracted_backend
git filter-repo --path extracted_backend --invert-paths
```

**或使用 paths-file（推荐）**:

创建 `cleanup-paths.txt`:
```
supplier-backend/database.db.backup-20251030
supplier-backend/audit-archive/
supplier-backend/backend.log
supplier-backend/backend-debug.log
supplier-backend/backend-new.log
extracted_backend/
```

执行:
```bash
git filter-repo --paths-from-file cleanup-paths.txt --invert-paths
```

#### 5.3 强制推送（如果有远程仓库）

```bash
# ⚠️ 警告: 通知所有团队成员！
git push origin --force --all
git push origin --force --tags
```

#### 5.4 团队成员同步

所有团队成员需要执行:
```bash
cd supplier-system
git fetch origin
git reset --hard origin/main  # 或你的主分支
git clean -fdx
```

---

## 🎯 推荐执行顺序

### 快速路径（仅工作区清理，不重写历史）
1. ✅ **阶段 0**: 创建备份
2. ✅ **阶段 1**: 更新 `.gitignore`
3. ✅ **阶段 2**: 删除运行期文件
4. ✅ **阶段 3**: 整理测试脚本
5. ✅ **阶段 4**: 整理文档
6. ⏭️ **跳过阶段 5**（敏感文件仍在历史中，但不在工作区）

**优点**: 安全、可逆、不破坏协作
**缺点**: 仓库历史仍包含敏感文件

### 完整路径（包括历史清理）
1. ✅ 阶段 0-4（同上）
2. 🔴 **阶段 5**: 清除 Git 历史
3. 🔴 **通知团队重新克隆**

**优点**: 彻底清理、减小仓库大小、移除敏感数据
**缺点**: 需要团队协调、有破坏性

---

## 📊 预期效果

### 空间节省
- **extracted_backend/**: ~150-200MB
- **数据库备份**: 448KB
- **审计归档**: ~50KB
- **日志文件**: ~12KB
- **临时脚本**: ~100KB
- **总计（工作区）**: ~150-200MB
- **总计（历史清理后）**: ~200-300MB

### 文件数量减少
- **删除**: ~70-80 个文件
- **移动/整理**: ~30-40 个文件

---

## ✅ 验证清单

清理完成后验证:

```bash
# 1. 检查系统功能
cd supplier-backend
node index.js  # 后端应正常启动

cd ..
npm run dev    # 前端应正常运行

# 2. 检查 .gitignore 生效
git status     # 不应显示 *.log, *.db, audit-archive/ 等

# 3. 检查文档链接
# 手动检查 README 和 docs/ 中的链接是否有效

# 4. 运行测试（如果有）
npm test
cd supplier-backend && npm test
```

---

## 🆘 回滚方案

### 如果只执行了阶段 0-4（未重写历史）

```bash
# 回滚到清理前
git reset --hard pre-cleanup-2025-10-31

# 或从备份恢复
git bundle unbundle ../supplier-system-backup-YYYYMMDD.bundle
```

### 如果执行了阶段 5（已重写历史）

```bash
# 从备份 tag 创建新分支
git checkout -b recovery pre-cleanup-2025-10-31

# 或从 bundle 恢复
cd ..
git clone supplier-system-backup-YYYYMMDD.bundle supplier-system-recovered
```

---

## 📝 下一步行动

**请确认您希望执行的方案**:

1. ⬜ **方案 A**: 仅阶段 1（更新 `.gitignore`），不删除任何现有文件
2. ⬜ **方案 B**: 阶段 1-4（快速路径），清理工作区但保留历史
3. ⬜ **方案 C**: 阶段 1-5（完整路径），包括历史清理
4. ⬜ **方案 D**: 自定义（指定要执行的阶段）

**我建议先执行方案 B（快速路径）**，验证系统功能正常后，再决定是否执行阶段 5。

---

**等待您的确认...**
