# 文档索引

欢迎查阅供应商管理系统文档。本目录包含项目的所有技术文档、指南和实施报告。

---

## 📚 核心文档

### 部署与运维
- [部署指南](DEPLOYMENT.md) - 生产环境部署步骤
- [发布前检查清单](PRE-LAUNCH-CHECKLIST.md) - 上线前必检事项
- [用户手册](USER_GUIDE.md) - 系统使用手册

### 架构与设计
- [可扩展性架构路线图](SCALABILITY-ARCHITECTURE-ROADMAP.md)
- [PostgreSQL 迁移计划](postgresql-migration/MIGRATION-PLAN.md)
- [存储抽象设计](storage-abstraction/DESIGN.md)

---

## 🔒 安全与修复

### 安全加固
- [P0 安全修复实施](P0-SECURITY-FIXES-IMPLEMENTED.md)
- [P0 安全修复改进](P0-SECURITY-FIXES-IMPROVEMENTS.md)
- [P0 安全修复测试报告](P0-SECURITY-FIXES-TEST-REPORT.md)
- [存储安全修复 V2](STORAGE-SECURITY-FIXES-V2.md)
- [存储安全修复](STORAGE-SECURITY-FIXES.md)
- [2025-01-22 安全修复](SECURITY-FIXES-2025-01-22.md)

### 问题修复记录
- [RFQ 提交问题修复](RFQ-SUBMISSION-FIX.md)
- [数据库架构修复](DATABASE-SCHEMA-FIX-2025-10-30.md)
- [修复完成报告](FIXES-COMPLETION-REPORT.md)

---

## 🛠️ 实施报告

详见 [implementation-reports/](implementation-reports/) 目录

### 功能实施报告
- 前端实施指南与完成报告
- 国际化 (i18n) 实施总结
- IDM 流程实施状态
- 采购组实施
- 对账模块实施完成
- RFQ 完整工作流设计与实施
- RFQ 邮件邀请实施
- RFQ 多项目实施进度
- 基于角色的实施总结
- 供应商变更请求实施
- UI 重新设计实施指南
- 安全改进总结
- 性能测试报告

### 测试报告 ([tests/](implementation-reports/tests/))
- 国际化测试报告
- 供应商资料测试指南
- 系统健康报告
- 模板验证报告
- 语言支持测试
- 供应商升级测试
- 升级模块测试总结

### 修复记录 ([fixes/](implementation-reports/fixes/))
- 国际化最终状态与待修复项
- RFQ 邮件与工作流后续步骤
- 供应商视图修复
- 标签管理修复总结
- 模板下载修复
- 翻译补丁
- 升级管理修复总结

---

## 📖 使用指南

详见 [guides/](guides/) 目录

### 浏览器兼容性
- [浏览器缓存清理指南](guides/BROWSER-CACHE-CLEAR-GUIDE.md)
- [浏览器兼容性指南](guides/BROWSER-COMPATIBILITY-GUIDE.md)
- [浏览器测试检查清单](guides/BROWSER-TEST-CHECKLIST.md)
- [浏览器测试总结](guides/BROWSER-TESTING-SUMMARY.md)

### 生产环境
- [生产就绪指南](guides/PRODUCTION-READINESS.md)
- [生产安全指南](guides/PRODUCTION-SECURITY-GUIDE.md)
- [故障排除](guides/TROUBLESHOOTING.md)

### 快速开始
- [采购组快速开始](guides/QUICK-START-PURCHASING-GROUPS.md)
- [UI 重新设计快速开始](guides/QUICK-START-UI-REDESIGN.md)
- [快速 UI 测试](guides/QUICK-UI-TEST.md)

---

## 📖 中文文档

详见 [zh/](zh/) 目录

- [如何查看后端日志](zh/如何查看后端日志.md)
- [修复清单](zh/修复清单.md)
- [问题已修复说明](zh/README-问题已修复.txt)

---

## 🔧 开发者资源

### 国际化 (i18n)
- [i18n 工作流程](i18n/workflow.md)
- [i18n 待办清单](i18n/backlog.md)

### 测试
- [测试指南](testing.md)

### 回滚
- [回滚指南](rollback.md)

---

## 📁 文档组织结构

```
docs/
├── README.md                          # 本文件 - 文档索引
├── DEPLOYMENT.md                      # 部署指南
├── USER_GUIDE.md                      # 用户手册
├── PRE-LAUNCH-CHECKLIST.md           # 发布前检查
├── implementation-reports/            # 实施报告
│   ├── ADVANCED-SECURITY-TEST-REPORT.md
│   ├── AUDIT-LOG-ENHANCEMENT-SUMMARY.md
│   ├── FILE-VALIDATION-IMPLEMENTATION.md
│   └── ...
├── guides/                            # 使用指南
│   ├── BROWSER-COMPATIBILITY-GUIDE.md
│   └── ...
├── zh/                                # 中文文档
│   ├── 如何查看后端日志.md
│   └── ...
├── postgresql-migration/              # PostgreSQL 迁移
│   ├── MIGRATION-PLAN.md
│   └── schema-conversion.sql
├── storage-abstraction/               # 存储抽象
│   ├── DESIGN.md
│   └── storage-service-interface.ts
└── i18n/                              # 国际化
    ├── workflow.md
    └── backlog.md
```

---

## 🔄 文档维护

### 添加新文档
1. 将文档放入相应目录
2. 更新本 README 的对应章节
3. 确保文档使用清晰的标题和结构

### 文档分类规则
- **核心文档**: 部署、架构、用户手册等长期维护的文档
- **实施报告**: 特定功能的实施过程和决策记录
- **指南**: 操作步骤和最佳实践
- **中文文档**: 面向中文用户的文档
- **临时文档**: 应整合到正式文档后删除

---

**最后更新**: 2025-10-31
**维护者**: 开发团队

有问题？请查看项目根目录的 `CLAUDE.md` 了解项目概况。
