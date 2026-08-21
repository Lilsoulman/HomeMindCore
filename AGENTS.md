# 计划驱动开发（HomeMind Core / .NET 8 + MySQL）

`docs/development-plan.md` 是本仓库功能开发的执行队列，`docs/product.md` 是计划任务的产品输入。根级指令见 `../AGENTS.md`。

## 文档链（开发前必读）

| 文档 | 路径 | 作用 |
| --- | --- | --- |
| 产品总设计 | `docs/product.md` | 唯一产品事实源：功能卡、方向共识、产品原则 |
| 开发计划 | `docs/development-plan.md` | 执行队列：P0-P4 阶段 + 切片表 + 下一步 |
| API 契约 | `docs/api-integration.md` | 跨端契约：路由索引、认证刷新、错误码、确认流 |
| 功能卡 | `docs/family-finance.md` 等 | 领域边界快速确认 |
| 开发环境 | `DEVELOPMENT.md` | 环境版本、启动命令、迁移应用 |
| 本文件 | `AGENTS.md` | 计划驱动规则 + 开发流程 |

## 代码结构

```
HomeMind.Api/Controllers/<Domain>/      # 控制器（路由+权限）
HomeMind.Business.Services/             # 业务服务
HomeMind.Business.IServices/            # 服务接口
HomeMind.Common.Model/Entities/         # EF 实体
HomeMind.Common.Model/ViewModel/Data/   # DTO（PascalCase 响应）
HomeMind.Common.Repository/             # EF DbContext
HomeMind.Business.Services.Tests/       # xUnit 测试
database/NNN_*.mysql.sql                # 迁移（顺序编号，中文 COMMENT 强制）
```

## 任务执行流程

**用户说「按照下一步计划进行开发」（或「继续开发」）时，自动执行：**

1. 读取 `docs/development-plan.md` 的「下一步」区，选依赖已满足的首个 `待做` 任务（如 B44 快递管家）。
2. 若该切片在文档中**尚未展开详细任务表**：先在 `docs/development-plan.md` 按「P3-F 家庭财务执行计划」表格格式生成该切片的编码任务表（ID/状态/依赖/AI 编码任务/改动位置/完成标准与验证），写进文档后再开发。**禁止跳过计划直接写代码。**
3. 读取任务链接的产品卡（`docs/product.md`），先检查现有代码和测试；只实现最小改动。
4. 编码前将任务状态改为 `进行中`；完成定义验证后改为 `完成`；验证未通过保留 `进行中` 并报告阻塞。
5. 写迁移（如有）：`database/NNN_*.mysql.sql`，编号查 `ls database/ | tail` 取末号+1；中文 COMMENT 强制。
6. 写代码顺序：IServices 接口 → Services 实现 → Controller（`/api/v1`、camelCase 请求、权限特性）→ 测试（状态机/幂等/409/422/404/隔离）。
7. 验证：
   ```bash
   dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore   # 0 errors / 0 CS1591
   dotnet test --filter "FullyQualifiedName~<领域>"              # 定向全绿
   ```
8. **回写文档**（与代码同一变更）：`development-plan.md` 状态、`api-integration.md` 接口索引、`product.md` 受影响功能卡/能力边界。
9. 不以补写设计文档代替任务实现；仅在接口、产品边界或计划状态实际变化时更新对应文档。
10. 若任务依赖的客户端目录或运行环境不在当前工作区，停止并说明缺少的仓库/环境；不得虚构客户端实现。

## 验收定义（切片完成标准）

- `dotnet build` 0 errors / 0 CS1591；`dotnet test` 定向全绿
- 真实 MySQL 顺序迁移可应用；新表字段中文备注
- 接口经 Swagger 可查（路由/认证/请求模型）
- 文档已回写（计划状态 + API 索引 + 产品卡）
- 敏感字段不出现在响应或日志

## Pitfalls（Windows / git-bash）

- `read_file` 对超长行 UTF-8 文档误报 "Binary file" → 用 `sed -n 'X,Yp'` 或 `head -c` 读取
- Windows 路径写入会转义反斜杠 → 写路径后检查落盘字节
- CRLF 文件 diff 显示 `\r` 属正常
- 文档中文引号用 “ ”（弯引号），注意配对
- 迁移编号避让已占用号；四引擎/外部依赖默认关闭，失败返回明确失败，禁止伪造成功

用户明确指定的需求优先于计划；仅要求审阅、说明或文档修改时，不自动启动计划中的编码任务。
