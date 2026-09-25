# 本地音乐播放器（PC 端）

自己用的本地音乐播放器。扫描本地文件夹里的 MP3 / FLAC，显示元数据，播放并控制。
**对音乐文件只读**：不写入、不删除、不移动、不修改标签。

## 直接运行

`dist/win-x64/MusicPlayer.App.exe` —— 双击即可，不需要预先安装 .NET。
首次运行会把运行库解压到临时目录（约 3 秒），之后启动约 0.7 秒。

打开后把音乐文件夹的路径粘进顶部输入框（例如 `E:\LocalMusic`），回车或点「打开」。

## 从源码构建

```powershell
dotnet test                                                                # Core 单元测试
dotnet run --project src/MusicPlayer.App                                   # 调试运行

# 打包单文件 exe（项目用的是复数 TargetFrameworks，所以必须带 -f）
dotnet publish src/MusicPlayer.App -c Release -f net10.0-windows10.0.19041.0 -o dist/win-x64
```

需要 .NET 10 SDK 与 `maui-windows` 工作负载（`dotnet workload install maui-windows`）。

## 操作

| 操作 | 方式 |
|---|---|
| 播放某首 | 在列表里单击（点当前正在播的那首不会重播） |
| 播放 / 暂停 | 播放键，或**空格键** |
| 上一首 / 下一首 | 底部播放条两侧按钮 |
| 切换播放方式 | 播放条上的图标按钮：`➡️` 顺序 → `🔀` 随机 → `🔂` 单曲循环 |
| 拖动进度 / 调音量 | 播放条上的滑块 |

播放方式的含义：

- **顺序播放**：一首接一首，放完最后一首回到第一首继续
- **随机播放**：整张列表打乱后逐首放完，每首歌都放过一次才会出现重复
- **单曲循环**：一首放完从头再来

## 设计文档

- 设计说明：`docs/superpowers/specs/2026-09-25-local-music-player-pc-mvp-design.md`
- 实现计划：`docs/superpowers/plans/2026-09-25-local-music-player-pc-mvp.md`

出问题时看日志：`%TEMP%\musicplayer-diag.log`
