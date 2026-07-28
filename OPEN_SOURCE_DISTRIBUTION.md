# 开源与发行边界

> 说明：组合程序限制商业用途，严格来说属于源码可用（source-available），并非 OSI 定义的开源软件。本文件名为历史沿用。

## 自研代码

仓库根目录 `LICENSE` 覆盖自研转换核心、Godot 界面、构建脚本和项目文档，采用 Apache License 2.0。

## 多版本转换器与非商业限制

`third_party/SpineSkeletonDataConverter/` 来自 `wang606/SpineSkeletonDataConverter`，固定到提交 `5ecb2139b0a1af266974f95abeec6bb8562d1249`，采用 PolyForm Noncommercial License 1.0.0。

该组件允许非商业目的的使用、修改和分享，但禁止商业使用。由于正式程序包含该组件，整个组合程序只能免费用于非商业用途，不得销售、收费授权或作为商业服务的一部分。分发时必须保留 `POLYFORM-NONCOMMERCIAL-LICENSE.txt`、原作者署名和本说明。

## Spine Runtime 例外

`addons/spine_godot/` 及其构建产物包含或依赖 Spine Runtime，不属于 Apache-2.0。适用 `SPINE-RUNTIMES-LICENSE.txt` 以及 Esoteric Software 的 Spine Editor License Agreement。

根据 2025-04-05 官方协议第 2.1 至 2.4 节：集成 Runtime 的产品需要具有显著主要功能；集成时构建主体必须持有有效适用的 Spine Editor License；分发时必须附带 Runtime License；修改或创建包含 Runtime 的衍生产品也可能要求修改者持有自己的 Spine Editor License。

官方协议：<https://esotericsoftware.com/spine-editor-license>

因此：获得本仓库自研代码的 Apache-2.0 权利，不代表自动获得 Spine Runtime 的集成、修改或分发权。每个构建者和分发者必须自行核对其适用资格。

## 官方二进制

官方二进制由持有适用 Spine Professional 许可证的发行主体构建，并附带 Runtime License、Godot License、PolyForm Noncommercial License 和第三方声明。第三方不得删除这些文件。

## 用户素材

仓库与发行包不授予任何示例、客户或用户 Spine 模型的再分发权。测试时只使用自行有权使用的素材，且不要提交到公共仓库。
