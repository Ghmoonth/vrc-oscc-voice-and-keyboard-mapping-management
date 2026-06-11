# OSCC 妯″潡寮€鍙戣鏄?
OSCC 鐜板湪鐢变富绋嬪簭鍜屾ā鍧楃粍鎴愩€?
涓荤▼搴忚礋璐ｏ細

- OSC Host/Port
- 閰嶇疆璇诲啓
- 鏃ュ織
- 鎵樼洏
- 妯″潡鍚姩/鍋滄
- Windows 娑堟伅鍒嗗彂

妯″潡璐熻矗鑷繁鐨?UI 鍜屽姛鑳介€昏緫銆?
## 鏂板妯″潡

鏂板涓€涓被骞跺疄鐜?`IOsccModule`锛?
```csharp
public sealed class MyModule : UserControl, IOsccModule
{
    public string Id => "my-module";
    public string DisplayName => "鎴戠殑妯″潡";
    public Control View => this;

    public void Initialize(ModuleContext context) { }
    public void Start() { }
    public void Stop() { }
    public bool HandleMessage(ref Message message) => false;
}
```

鐒跺悗鍦?`MainForm.RegisterModules()` 閲屾敞鍐岋細

```csharp
modules.Register(new MyModule(), context);
```

## 鍙鐢ㄨ兘鍔?
妯″潡鍙互閫氳繃 `ModuleContext` 浣跨敤锛?
- `context.Config`
- `context.Osc.Send(parameter, value, type)`
- `context.Log("message")`
- `context.SaveConfig()`
- `context.Owner`

浠ュ悗闈㈡崟銆丮IDI銆佹墜鏌勩€佽闊虫帶鍒躲€丠TTP/WebSocket 杈撳叆閮藉彲浠ュ仛鎴愬崟鐙ā鍧椼€?