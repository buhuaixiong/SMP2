# 供应商管理系统 - 服务器上线部署指南

## 系统架构

```
浏览器 → .NET API (端口 5001) → SQL Server (端口 1433)
```

**技术栈**: Vue 3 前端 + .NET 9 API + SQL Server

---

## 环境要求

| 组件 | 要求 |
|------|------|
| 操作系统 | Windows Server 2019+ / Windows 10+ |
| .NET | .NET 9 Runtime |
| 数据库 | SQL Server 2019+ Express |
| 内存 | 最低 4GB，推荐 8GB |
| 硬盘 | 至少 10GB 可用空间 |

---

## 第一步：安装依赖

### 1. 安装 .NET 9 Runtime

下载链接: https://dotnet.microsoft.com/download/dotnet/9.0

选择 **ASP.NET Core Runtime 9.x** 进行安装。

### 2. 安装 SQL Server

下载链接: https://www.microsoft.com/sql-server/sql-server-downloads

安装 **Express** 版本，安装时注意：
- 启用 TCP/IP 协议（端口 1433）
- 设置混合模式认证
- 记录 SA 密码

### 3. 安装 SQL Server Management Studio (可选)

用于管理数据库：https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms

---

## 第二步：准备数据库

### 方法一：SSMS 创建

1. 打开 SSMS，连接到 local 或 localhost
2. 新建查询，执行：

```sql
CREATE DATABASE [SupplierSystemDev];
GO
```

### 方法二：命令行创建

```bash
sqlcmd -S localhost -U sa -P "YourSA_Password" -Q "CREATE DATABASE [SupplierSystemDev]"
```

---

## 第三步：配置环境

### 编辑配置文件

打开 `SupplierSystem/src/SupplierSystem.Api/appsettings.Production.json` 或 `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SupplierSystem": "Server=localhost;Database=SupplierSystemDev;User Id=sa;Password=你的SA密码;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "Secret": "改成你的密钥，至少32位字符",
    "Issuer": "supplier-system",
    "Audience": "supplier-system",
    "ExpiresIn": "8h"
  },
  "AllowedHosts": "*"
}
```

---

## 第四步：构建发布

### 1. 构建 Vue 前端

```bash
cd app
npm install
npm run build -w apps/web
```

### 2. 复制前端到 API 目录

构建完成后，前端文件会自动复制到 `SupplierSystem/src/SupplierSystem.Api/wwwroot/`

### 3. 发布 .NET API

```bash
cd SupplierSystem
dotnet restore
dotnet publish -c Release -o C:\app\publish
```

---

## 第五步：启动服务

### 方式一：直接运行

```bash
cd C:\app\publish
dotnet SupplierSystem.Api.dll
```

### 方式二：后台运行 (推荐)

创建启动脚本 `start.bat`:

```batch
@echo off
cd /d C:\app\publish
dotnet SupplierSystem.Api.dll
pause
```

双击运行或使用计划任务开机启动。

### 方式三：使用 PM2 (Windows)

```bash
npm install -g pm2
pm2 start "dotnet C:\app\publish\SupplierSystem.Api.dll" --name supplier-api
pm2 startup
pm2 save
```

---

## 访问系统

| 服务 | 地址 |
|------|------|
| 前端 | http://localhost:5001 |
| API | http://localhost:5001/api |

默认管理员账号：
- 用户名: `admin`
- 密码: `admin123`

**首次登录后请立即修改密码！**

---

## 常用命令

```bash
# 进入发布目录
cd C:\app\publish

# 启动服务
dotnet SupplierSystem.Api.dll

# 停止服务 (Ctrl+C)

# 查看日志 (实时)
dotnet SupplierSystem.Api.dll
```

---

## 文件上传配置

上传文件存储在 `C:\app\publish\uploads` 目录。

如需修改路径，编辑 `appsettings.json`:

```json
{
  "Uploads": {
    "Path": "D:\\uploads"
  }
}
```

---

## 数据库备份

### 手动备份

```bash
sqlcmd -S localhost -U sa -P "密码" ^
  -Q "BACKUP DATABASE [SupplierSystemDev] TO DISK='D:\\backups\\SupplierSystemDev_$(date +%Y%m%d).bak'"
```

---

## 故障排查

| 问题 | 解决方法 |
|------|----------|
| 无法连接数据库 | 检查 SQL Server 服务是否启动，检查连接字符串密码 |
| 端口被占用 | 修改 `appsettings.json` 中的端口，或停止占用程序 |
| 前端页面空白 | 检查 `wwwroot` 目录是否存在 index.html |
| 上传失败 | 检查 `uploads` 目录是否存在且有写入权限 |

---

## 开机自启 (Windows)

### 方法一：创建服务 (推荐)

使用 NSSM 创建 Windows 服务：

```bash
# 下载 NSSM: https://nssm.cc/download

nssm install SupplierAPI "C:\Program Files\dotnet\dotnet.exe" "C:\app\publish\SupplierSystem.Api.dll"
nssm set SupplierAPI AppDirectory "C:\app\publish"
nssm set SupplierAPI DisplayName "Supplier Management System API"
nssm set SupplierAPI Description "供应商管理系统 API 服务"
nssm set SupplierAPI Start SERVICE_AUTO_START
nssm set SupplierAPI AppStdout "C:\app\logs\stdout.log"
nssm set SupplierAPI AppStderr "C:\app\logs\stderr.log"

# 启动服务
net start SupplierAPI
```

### 方法二：启动文件夹

1. 按 `Win + R`
2. 输入 `shell:startup`
3. 创建快捷方式指向启动脚本

---

## 生产环境建议

1. **修改默认密钥**: JWT_SECRET 改为强随机字符串
2. **修改 SA 密码**: 使用强密码
3. **修改默认管理员密码**: 首次登录后立即修改
4. **配置防火墙**: 只开放 5001 端口
5. **定期备份**: 设置数据库自动备份计划
