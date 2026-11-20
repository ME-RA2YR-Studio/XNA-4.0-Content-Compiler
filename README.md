# XNA Content Compiler

[English](README.md) | [简体中文](README.zh-CN.md)

Compile textures, sprite fonts and audio into XNA 4.0 `.xnb` files without installing XNA Game Studio, Visual Studio or the .NET runtime.

This is a modern, self-contained rebuild of [mattleibow/XNA-4.0-Content-Compiler](https://github.com/mattleibow/XNA-4.0-Content-Compiler). The published executable is a single file and runs on a clean Windows machine.

## Features

- Advanced WinForms UI with runtime switching between English and Simplified Chinese.
- Add individual files or whole folders; drag and drop is supported.
- Rename output assets inline; duplicate or invalid names are flagged before building.
- Build the whole project or only the selected items, with a progress display and build log export.
- Texture options: output format (Color, DXT compressed, No change), mipmaps, premultiply alpha, power-of-two resize, square resize, color key and color key color.
- Sprite font options: glyph atlas format (DXT or Color) and premultiply alpha.
- WAV to SoundEffect conversion with `smpl` loop points preserved.
- MP3 and WMA to Song content; the media file is copied next to the `.xnb` and referenced by name, matching XNA Song semantics.
- Reach and HiDef graphics profiles, selectable per project, in the UI, or from the command line.
- Output uses XNA 4.0 type reader identities and loads in XNA 4.0, MonoGame and FNA.
- Command line mode for build servers and scripts.
- Recent projects, remembered output directory and crash logs under `%APPDATA%\XnaContentCompiler`.

## Supported Content

The supported file types match the original XNA Content Compiler:

| Content | Extensions | Importer / Processor |
| --- | --- | --- |
| Texture | `.bmp` `.jpg` `.png` `.tga` `.dds` | TextureImporter / TextureProcessor |
| Sound effect | `.wav` | WavImporter / SoundEffectProcessor |
| Song | `.mp3` `.wma` | Mp3 or WmaImporter / SongProcessor |
| Sprite font | `.spritefont` | FontDescriptionImporter / FontDescriptionProcessor |

## Requirements

Running the published tool:

- Windows 10 or 11, x64.
- No XNA Game Studio, Visual Studio or .NET installation is required.

Building from source:

- Windows with the .NET 10 SDK installed.

## Usage

1. Start `XnaContentCompiler.exe`.
2. Add image, `.spritefont`, `.wav`, `.mp3` or `.wma` files with the toolbar, or drag them into the list.
3. Choose the output directory at the top of the window.
4. Select an item to adjust its settings in the properties panel.
5. Click **Build**, or press `F6`. Compiled `.xnb` files are written to the output directory.

Useful shortcuts and actions:

- `Ctrl+N` new project, `Ctrl+O` open, `Ctrl+S` save, `Ctrl+Shift+S` save as.
- `F6` build everything, `Ctrl+Shift+B` build the selected items.
- Double-click the name cell to rename an output asset.
- Right-click an item to build it, reveal the source, copy paths or remove it.
- **Build > Graphics Profile** switches between Reach (default) and HiDef.
- **Language** switches between English and Simplified Chinese at runtime.

## Command Line

```text
XnaContentCompiler.exe [project.xnccproj]
XnaContentCompiler.exe --build <project.xnccproj> [--output <folder>] [--profile reach|hidef] [--lang en|zh]
XnaContentCompiler.exe --help | --version
```

`--build` compiles without opening the UI and returns `0` when every item succeeded. `--output` overrides the output directory stored in the project, and `--profile` overrides the graphics profile.

## Project Files

Projects are saved as `.xnccproj` JSON files. Paths are stored relative to the project file whenever possible, so a project folder can be moved as a whole.

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

`graphicsProfile` accepts `Reach` or `HiDef`. Song items do not need extra settings: the media file is copied next to the compiled `.xnb`.

## Output Notes

- Content is written as XNA 4.0 XNB version 5 for Windows.
- Content is written uncompressed. XNA's LZX compression is proprietary, and MonoGame's LZ4 output would not load in XNA 4.0.
- In the Reach profile, DXT compression requires power-of-two dimensions. HiDef removes that restriction.
- WAV loop points from the `smpl` chunk are stored in the SoundEffect asset using XNA's byte based loop fields.
- Texture format defaults follow the XNA TextureProcessor: color keying is enabled with magenta as the key color, and alpha is premultiplied.

## Building From Source

```powershell
dotnet build XnaContentCompiler.slnx -c Release
dotnet test tests\XnaContentCompiler.Core.Tests\XnaContentCompiler.Core.Tests.csproj
```

Publish the self-contained single file executable:

```powershell
dotnet publish src\XnaContentCompiler.App\XnaContentCompiler.App.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=none `
  -o dist
```

## Repository Layout

```text
src/XnaContentCompiler.Core/    Project model, XNB writers and rewriters, WAV/MP3/WMA readers, build service
src/XnaContentCompiler.App/     WinForms UI, localization tables, CLI entry point, icon and manifest
tests/                          xUnit tests for the core build logic
```

## License

Released under the MIT License. See [LICENSE](LICENSE).

## Acknowledgements

- Based on the original [XNA Content Compiler](https://github.com/mattleibow/XNA-4.0-Content-Compiler) by Matthew Leibowitz.
- Built with the [MonoGame](https://github.com/MonoGame/MonoGame) content pipeline and [BCnEncoder.NET](https://github.com/Nominom/BCnEncoder.NET).
