# XNA 内容编译器

[English](README.md) | [简体中文](README.zh-CN.md)

无需安装 XNA Game Studio、Visual Studio 或 .NET 运行库，即可将贴图、精灵字体和音频编译为 XNA 4.0 的 `.xnb` 文件。

本项目是 [mattleibow/XNA-4.0-Content-Compiler](https://github.com/mattleibow/XNA-4.0-Content-Compiler) 的现代化重构版本。发布产物为单文件可执行程序，可直接在干净的 Windows 机器上运行。

## 功能特性

- 高级 WinForms 界面，支持运行时切换英文与简体中文。
- 支持添加单个文件或整个文件夹，也支持拖放。
- 可内联重命名输出资源；重名或非法名称会在生成前标记。
- 支持生成整个项目或仅生成选中项，并显示进度、可导出生成日志。
- 贴图选项：输出格式（颜色、DXT 压缩、不改变）、生成 Mipmap、预乘 Alpha、调整为 2 的幂、调整为正方形、颜色键及颜色键颜色。
- 精灵字体选项：图集格式（DXT 或颜色）与预乘 Alpha。
- WAV 转 SoundEffect，并保留 `smpl` 块中的循环点。
- MP3、WMA 转 Song 内容；媒体文件复制到 `.xnb` 同级目录并按文件名引用，与 XNA Song 语义一致。
- 支持 Reach 与 HiDef 图形配置文件，可在项目中保存、在界面切换或通过命令行指定。
- 输出使用 XNA 4.0 的类型读取器标识，可被 XNA 4.0、MonoGame 和 FNA 加载。
- 提供命令行模式，适合构建服务器与脚本使用。
- 支持最近项目、记忆上次输出目录；崩溃日志位于 `%APPDATA%\XnaContentCompiler`。

## 支持的内容

支持的文件类型与原版 XNA Content Compiler 一致：

| 内容 | 扩展名 | 导入器 / 处理器 |
| --- | --- | --- |
| 贴图 | `.bmp` `.jpg` `.png` `.tga` `.dds` | TextureImporter / TextureProcessor |
| 音效 | `.wav` | WavImporter / SoundEffectProcessor |
| 音乐 | `.mp3` `.wma` | Mp3 或 WmaImporter / SongProcessor |
| 精灵字体 | `.spritefont` | FontDescriptionImporter / FontDescriptionProcessor |

## 运行要求

运行已发布的工具：

- Windows 10 或 11，x64。
- 无需安装 XNA Game Studio、Visual Studio 或 .NET。

从源码构建：

- Windows，且安装 .NET 10 SDK。

## 使用方法

1. 启动 `XnaContentCompiler.exe`。
2. 使用工具栏添加图像、`.spritefont`、`.wav`、`.mp3` 或 `.wma` 文件，也可以直接拖放到列表中。
3. 在窗口顶部选择输出目录。
4. 选中项目后，在右侧属性面板调整设置。
5. 单击“生成”或按 `F6`，编译后的 `.xnb` 文件会写入输出目录。

常用快捷键与操作：

- `Ctrl+N` 新建项目，`Ctrl+O` 打开，`Ctrl+S` 保存，`Ctrl+Shift+S` 另存为。
- `F6` 生成全部，`Ctrl+Shift+B` 生成选中项。
- 双击名称单元格可重命名输出资源。
- 右键项目可生成选中项、在资源管理器中显示源文件、复制路径或移除项目。
- “生成 > 图形配置文件”可在 Reach（默认）与 HiDef 之间切换。
- “语言”菜单可在运行时切换英文与简体中文。

## 命令行

```text
XnaContentCompiler.exe [project.xnccproj]
XnaContentCompiler.exe --build <project.xnccproj> [--output <文件夹>] [--profile reach|hidef] [--lang en|zh]
XnaContentCompiler.exe --help | --version
```

`--build` 不打开界面直接生成，全部成功时返回 `0`。`--output` 覆盖项目中的输出目录，`--profile` 覆盖图形配置文件。

## 项目文件

项目保存为 `.xnccproj` JSON 文件。路径会尽可能相对于项目文件存储，因此整个项目文件夹可以直接移动。

```json
{
  "formatVersion": 1,
  "outputDirectory": "Content",
  "graphicsProfile": "Reach",
  "items": [
    {
      "sourcePath": "Textures/player.png",
      "outputName": "Textures/player",
      "kind": "Texture",
      "texture": {
        "outputFormat": "Color",
        "generateMipmaps": false,
        "premultiplyAlpha": true,
        "resizeToPowerOfTwo": false,
        "makeSquare": false,
        "colorKeyEnabled": true,
        "colorKeyColor": "#FF00FF"
      }
    },
    { "sourcePath": "Audio/beep.wav", "outputName": "Audio/beep", "kind": "SoundEffect" },
    { "sourcePath": "Music/theme.mp3", "outputName": "Music/theme", "kind": "Song" }
  ]
}
```

`graphicsProfile` 可选 `Reach` 或 `HiDef`。Song 项目无需额外设置：媒体文件会自动复制到编译后的 `.xnb` 旁边。

## 输出说明

- 输出为 XNA 4.0、Windows 平台的 XNB 版本 5。
- 内容以未压缩方式写出。XNA 的 LZX 压缩为私有格式，而 MonoGame 的 LZ4 输出无法被 XNA 4.0 加载。
- 在 Reach 配置文件下，DXT 压缩要求贴图宽高为 2 的幂；HiDef 会解除该限制。
- WAV 的 `smpl` 循环点会按 XNA 的字节制循环字段写入 SoundEffect 资源。
- 贴图格式默认值遵循 XNA TextureProcessor：默认启用颜色键且键色为洋红色，并对 Alpha 进行预乘。

## 从源码构建

```powershell
dotnet build XnaContentCompiler.slnx -c Release
dotnet test tests\XnaContentCompiler.Core.Tests\XnaContentCompiler.Core.Tests.csproj
```

日常开发可直接使用构建脚本：

```powershell
.\scripts\build.ps1                                    # Debug 普通构建
.\scripts\build.ps1 -Configuration Release             # Release 构建
.\scripts\build.ps1 -Test                              # 构建并运行单元测试
.\scripts\build.ps1 -Clean                             # 清理 bin/obj/dist 后再构建
.\scripts\build.ps1 -CleanOnly                         # 仅清理构建产物
.\scripts\build.ps1 -Run                               # 构建并启动界面
.\scripts\build.ps1 -Run --build path\to\project.xnccproj   # 构建并以命令行模式运行
```

批处理包装用法相同：`scripts\build.bat -Test`。

一步完成测试与单文件发布：

```powershell
.\scripts\publish.ps1
```

常用参数：

- `-SkipTests` 跳过单元测试，直接发布。
- `-Output <文件夹>` 修改输出目录（默认 `dist`）。
- `-Runtime <rid>` 修改目标运行时（默认 `win-x64`）。
- `-Configuration Debug` 发布 Debug 版本。

如果系统限制运行 PowerShell 脚本，可使用批处理包装：

```bat
scripts\publish.bat
```

等价的原始命令：

```powershell
dotnet publish src\XnaContentCompiler.App\XnaContentCompiler.App.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=none `
  -o dist
```

## 仓库结构

```text
scripts/                        命令行构建与发布脚本
src/XnaContentCompiler.Core/    项目模型、XNB 写入与重写、WAV/MP3/WMA 读取、构建服务
src/XnaContentCompiler.App/     WinForms 界面、语言表、命令行入口、图标与清单
tests/                          针对核心构建逻辑的 xUnit 测试
```

## 许可证

基于 MIT 许可证发布，详见 [LICENSE](LICENSE)。

## 致谢

- 基于 Matthew Leibowitz 的原始项目 [XNA Content Compiler](https://github.com/mattleibow/XNA-4.0-Content-Compiler)。
- 使用 [MonoGame](https://github.com/MonoGame/MonoGame) 内容管线与 [BCnEncoder.NET](https://github.com/Nominom/BCnEncoder.NET) 构建。
