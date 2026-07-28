# 可复现 Windows 构建

要求：.NET 8 SDK、Godot 4.7.1 及匹配的 Windows 导出模板。上游版本与 Godot 兼容补丁记录在 `UPSTREAM_SOURCES.md` 和 `patches/`。

开发验收构建：

```powershell
.\scripts\build_product.ps1 `
  -OutputDirectory "dist/product-open-source-win-x64" `
  -SmokeTestSource "C:\path\to\licensed-model.skel" `
  -BatchSmokeTestFolder "C:\path\to\licensed-model-folder"
```

正式官方构建必须由满足适用 Spine Editor License 的主体执行，并显式确认许可证门禁：

```powershell
.\scripts\build_product.ps1 `
  -OutputDirectory "dist/product-open-source-win-x64" `
  -DistributionBuild `
  -SpineLicenseAcknowledged `
  -SmokeTestSource "C:\path\to\licensed-model.skel" `
  -BatchSmokeTestFolder "C:\path\to\licensed-model-folder"
```

脚本依次执行转换器回归测试、自包含转换器发布、Godot Release 导出、Runtime 与许可证组装、单模型和批量流程冒烟测试，并写入 `SHA256SUMS.txt`。正式便携包输出为 `dist/SpineConverterPreview-OpenSource-win-x64-1.0.0.zip`。

安装程序：

```powershell
.\scripts\build_installer.ps1 -DistributionBuild
```

安装器使用 NSIS。输出为 `dist/SpineConverterPreview-OpenSource-Setup-win-x64-1.0.0.exe`。

## 赞助二维码

公共源码仓库不包含收款二维码。官方构建者可在构建前放入 `branding/support-qr.png`。缺少该文件时，软件不会显示“支持项目”按钮。

## 可复现性边界

构建过程固定依赖版本并生成哈希，但不承诺跨不同编译环境获得逐字节一致的 Godot 或 .NET 二进制。每次发布都应保存生成的哈希清单。

