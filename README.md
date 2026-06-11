# OSCC

OSCC 是一个面向 VRChat OSC 的 C#/.NET 桌面工具，包含键盘参数映射、语音参数控制和 AI 翻译模块。

## 内容

- source/voice-keyboard-src`：C# WinForms 源码。
- dist/OSCC-OSCC-Voice-Keyboard-Mapping已发布的 Windows x64 独立运行版。


## 构建
powershell
cd source/voice-keyboard-src
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

## 运行

直接运行 `dist/OSCC-OSCC-Voice-Keyboard-Mapping/OSCC.exe`。
=======
# vrc-oscc-voice-and-keyboard-mapping-management
一款可以通过键位映射，语音识别文字控制模型参数，以及支持ai翻译的vrchat的osc插件
License: Non-Commercial Personal Use License
This project is free for personal, non-commercial use only. Commercial use is prohibited without written permission.
本项目仅允许个人非商业使用。未经作者书面许可，禁止任何商业用途。

