# Spine 一键转换预览工具

一个源码公开、免费且仅限非商业使用的 Windows 桌面工具，用于批量转换和预览 Spine 骨骼动画。

> 本组合项目包含 PolyForm Noncommercial 1.0.0 组件，禁止商业使用、销售或用于其他商业目的。

## 功能

- Spine 3.5–4.2 `.skel` / `.json` 自动识别和双向转换
- 目标版本：4.2.11、4.1.24、4.0.64、3.8.99
- 单文件、多文件和文件夹递归导入
- 自动识别源版本和同名模型
- 模型切换、动画选择、播放和自动适配预览
- 批量导出和默认保存目录
- 不覆盖源文件；遇到未知版本、附件或时间线时明确拒绝

预览要求骨骼文件旁存在对应 `.atlas` 及纹理。转换本身不要求 atlas。

## 下载与使用

官方发布提供免安装 ZIP。解压完整目录后运行 `SpineConverterPreview.exe`，不要单独移动 EXE。

1. 选择单个或多个 `.skel` / `.json`，也可以导入整个素材文件夹。
2. 选择输出格式和保存目录。
3. 点击“转换并预览”或“批量导出”。

## 从源码构建

要求 Windows、.NET 8 SDK、CMake、Godot 4.7.1 及匹配的 Windows 导出模板。克隆后先初始化子模块：

```powershell
git submodule update --init --recursive
```

依赖版本和兼容补丁记录在 `UPSTREAM_SOURCES.md`、`third_party/` 与 `patches/`。

```powershell
.\scripts\build_product.ps1 `
  -DistributionBuild `
  -SpineLicenseAcknowledged `
  -SmokeTestSource "C:\path\to\licensed-model.skel" `
  -BatchSmokeTestFolder "C:\path\to\licensed-model-folder"
```

`-SpineLicenseAcknowledged` 不是形式选项。只有在构建和分发主体满足适用 Spine Editor License 条件时才可使用。

## 许可说明

自研代码采用 [Apache License 2.0](LICENSE)。多版本转换器采用 [PolyForm Noncommercial 1.0.0](third_party/SpineSkeletonDataConverter/LICENSE)，因此整个组合程序仅限非商业使用。这不包括 Spine Runtime、Godot 或其他第三方组件。

由于组合程序限制商业用途，严格来说它属于“source-available（源码可用）”，而不是 OSI 定义的开源软件。

预览功能依赖 `spine-godot` / Spine Runtime。它不属于 Apache-2.0，适用条款见 `SPINE-RUNTIMES-LICENSE.txt` 和 `THIRD_PARTY_NOTICES.md`。修改、构建或分发包含 Spine Runtime 的版本可能要求你持有自己的有效 Spine Editor License。详见官方协议：<https://esotericsoftware.com/spine-editor-license>。

项目名称、官方发行者名称和未来的官方图标不因源码许可而自动授权第三方用于冒充官方发行版，详见 `TRADEMARKS.md`。

## 支持项目

软件完整功能免费开放。愿意支持维护工作的用户，可在程序“关于 / 支持项目”入口或项目发布页自愿赞助。赞助不影响任何功能，也不构成购买或投资。

真实收款二维码不会提交到公共源码仓库。官方构建时将经营主体收款码放入 `branding/support-qr.png`；缺少该文件时软件不显示二维码。

## 安全与贡献

- 贡献说明：`CONTRIBUTING.md`
- 安全问题：`SECURITY.md`
- 发行与许可证边界：`OPEN_SOURCE_DISTRIBUTION.md`
