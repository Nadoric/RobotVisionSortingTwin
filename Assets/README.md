# 沙盘应用图标

图案为机械臂抓取青绿色工件与下方输送线，使用沙盘界面的深蓝、工业橙和青绿色。

- `TwinIcon.xaml`：可编辑的 WPF 矢量源。
- `TwinIcon.png`：256×256 透明背景图，主界面标题区使用。
- `TwinIcon.ico`：16、24、32、48、64、128、256 像素七种尺寸，程序文件及窗口图标使用。

在项目根目录重新生成：

```powershell
dotnet run --project tests/Verify.csproj -c Release -- --generate-icon Assets
```

全部资源由项目代码绘制，不需要外部图像工具或网络服务。
