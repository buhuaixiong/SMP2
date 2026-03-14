# 服务层 Code Review 清单

> 适用于 Registry/ServiceManager/Service/Composable/测试/文档相关改动。  
> Reviewer 可按模块勾选，确保关键风险被覆盖。

## 1. 架构/目录
- [ ] 新增文件是否位于既定目录结构（`core/registry`, `core/services`, `services/`, `directives/` 等）？
- [ ] 是否更新了 `services/index.ts`、`composables/index.ts` 等集中导出？
- [ ] 依赖是否在 ServiceDefinition 中显式声明？

## 2. 服务实现
- [ ] `ServiceDefinition` 是否包含唯一 `name`、必要 `dependencies`、`setup`/`teardown`？
- [ ] 是否避免在 `setup` 内部再次调用 `useService`（应使用上下文 `get`）？
- [ ] 是否处理异常/降级逻辑，并记录关键日志？
- [ ] 是否存在潜在的循环依赖？

## 3. Composable / Hook
- [ ] 是否通过 `useService` 使用服务，避免重复封装？
- [ ] 是否返回明确的 state/方法，并提供类型注解？
- [ ] 是否遵循 `useXxx` 命名及目录规范？

## 4. 指令 / 组件集成
- [ ] 自定义指令是否安全地访问 ServiceManager（存在降级逻辑）？
- [ ] 组件中使用服务是否考虑未启动/权限不足等边界情况？

## 5. 测试
- [ ] 是否新增/更新对应的单元测试，使用 `tests/setup/mockServices.ts` 工具？
- [ ] 是否覆盖错误分支、依赖缺失等场景？
- [ ] 是否需要更新模板或快照？

## 6. 文档
- [ ] 是否更新 `docs/api/services/<name>.md` 或相关指南？
- [ ] 是否在 `CHANGELOG/实施计划` 中记录状态？
- [ ] 是否提供使用示例或教程链接？

## 7. 性能与安全
- [ ] 服务启动是否符合 <100ms，必要时是否添加性能测试？
- [ ] 是否存在敏感数据泄露（日志/通知）？
- [ ] 是否正确处理 Token/权限/缓存失效？

---
建议 Reviewer 在完成上述检查后，将勾选结果附在 PR 描述或 Review 评论中，便于后续追踪。*** End Patch
