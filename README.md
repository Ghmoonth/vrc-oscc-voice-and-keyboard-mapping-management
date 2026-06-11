# OSCC

OSCC 是一个面向 VRChat OSC 的 C#/.NET 桌面工具，包含键盘参数映射、语音参数控制和 AI 翻译模块。

## 内容

- `source/voice-keyboard-src`：C# WinForms 源码。
- `dist/OSCC-键盘语音映射版-独立新版-翻译修复版`：已发布的 Windows x64 独立运行版。

## 隐私说明

本包已清理 API Key。首次运行后，请在程序 UI 中自行填写 ASR/LLM API Key。

## 构建

```powershell
cd source/voice-keyboard-src
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

## 运行

直接运行 `dist/OSCC-键盘语音映射版-独立新版-翻译修复版/OSCC.exe`。
