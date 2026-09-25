# 本地音乐播放器 — PC 端最小实现（设计文档）

- 日期：2026-09-25
- 状态：架构与界面已确认，等待最终审阅
- 范围：仅 PC 端（Windows）的最小可用版本；iOS 端不在本次实现范围内，但架构必须为它留好接口

## 1. 目标

做一个自己用的本地音乐播放器，只播放自己硬盘上的音乐文件。要求支持 MP3 和 FLAC 两种格式，先在 Windows PC 上做出最小可用版本，之后再补 iOS 端。

"最小可用"在本设计里的定义是：能选一个文件夹、把里面的 MP3/FLAC 列出来并显示正确元数据、点一首能出声、能控制播放。

## 2. 范围

### 2.1 本次要做的

- 选择一个本地文件夹，递归扫描其中的 `.mp3` 和 `.flac`
- 读取元数据：标题、歌手、专辑、时长、封面
- 歌曲列表展示，点击某首开始播放
- 播放控制：播放/暂停、上一首、下一首、可拖动的进度条、音量
- 没有音乐时的空状态引导

### 2.2 本次明确不做的

- 搜索框
- 记住上次播放位置
- 歌单的创建与编辑
- 歌词、均衡器、无缝播放、ReplayGain
- 在线音乐源、网络串流
- iOS 端实现
- 任何形式的音乐文件写回（本播放器只读，永不修改用户的音乐文件）

### 2.3 音乐库与只读约束

- **实际音乐库目录：`E:\LocalMusic\`**
- 播放器对该目录**只读**：不写入、不删除、不移动、不修改标签
- 该目录当前有 229 个文件（216 个 FLAC、12 个 MP3、1 个 `.exe`）。所以扩展名过滤是必需的，实现里不能假设"目录里只有音频"
- FLAC 文件体积偏大（单曲 77–125 MB，约为 CD 音质的 3 倍），推测是 24bit 高采样率音源。这正好落在 3.2 那条"依赖系统编解码器"的风险上，需要在实现早期用真实文件验证，而不是等到最后

## 3. 技术选型

**C# / .NET 10 + .NET MAUI**，一套代码同时覆盖 Windows 和后续的 iOS。

依据（均已核实，非经验判断）：

- Windows 10 及以后，系统的 Media Foundation 原生带 FLAC 解码。微软文档中 `MFAudioFormat_FLAC` 明确标注 "Free Lossless Audio Codec — Supported in Windows 10 and later"。MP3 同理。也就是说 PC 端播这两种格式**不需要引入任何第三方解码库**。
- .NET MAUI 官方支持 Windows、iOS 11+、macOS、Android，一个项目就能覆盖目标的两个平台。
- MAUI 的 `MediaElement`（CommunityToolkit）：Windows 走 Media Foundation，iOS/macOS 走 AVPlayer，Android 走 ExoPlayer，本地文件可直接传路径。

其余选型：

| 用途 | 选择 |
|---|---|
| 元数据读取 | TagLib# |
| MVVM | CommunityToolkit.Mvvm |
| 单元测试 | xUnit |

### 3.1 前置步骤

当前机器只有 .NET SDK 10.0.301，没有任何 workload。开工第一步需要执行 `dotnet workload install maui`（约 1GB 下载）。

### 3.2 已知风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| MAUI 的 Windows 播放依赖系统编解码器 | Windows N 版未装媒体功能包时 FLAC 播不了 | 播放能力抽在 `IAudioPlayer` 后面，失败时给出明确提示；必要时换 LibVLCSharp 只改一个实现类 |
| 实际音源可能是 24bit 高采样率 FLAC | 系统解码器对高位深/高采样率的支持不如普通 CD 音质 FLAC 稳妥 | 早期就用 `E:\LocalMusic\` 里的真实文件验证，别用网上随便下的测试文件。若有问题，切换 LibVLCSharp 即可绕开 |
| iOS 编译必须用 Mac（Xcode） | 做不到纯 Windows 出 iOS 包 | 属于后续阶段的问题，本次不影响；届时需要一台 Mac 或云 Mac |
| MediaElement 可定制性有限 | 做不了均衡器、无缝播放、自定义解码 | 同上，`IAudioPlayer` 抽层保留替换余地 |
| MAUI 桌面端细节需写平台代码 | 窗口按钮、托盘、全局快捷键要单独处理 | 已在界面设计中明确列出需要写平台代码的部分 |

## 4. 架构与项目结构

```
MusicPlayer.sln
├─ src/MusicPlayer.Core/          # net10.0 纯类库，零 UI 依赖
│   ├─ Models/Track.cs
│   ├─ Library/FolderLibraryScanner.cs
│   ├─ Metadata/TagLibMetadataReader.cs
│   └─ Playback/
│       ├─ IAudioPlayer.cs
│       ├─ PlaybackQueue.cs
│       └─ PlaybackController.cs
├─ src/MusicPlayer.App/           # MAUI，net10.0-windows（iOS 后续加 TFM）
│   ├─ Services/MediaElementAudioPlayer.cs
│   ├─ ViewModels/PlayerViewModel.cs
│   └─ Views/MainPage.xaml
└─ tests/MusicPlayer.Core.Tests/  # xUnit
```

### 4.1 三条关键边界

**播放队列逻辑完全不碰解码器。** "下一首是哪首""播到最后一首怎么办""播放结束自动前进"这些是纯逻辑，全部放在 `PlaybackQueue` 里。测试时塞一个假的 `IAudioPlayer` 就能跑，不需要真的出声。

**`IAudioPlayer` 是唯一接触平台播放器的地方。** 接口大致为 `Load(path)` / `Play` / `Pause` / `Seek` / `Position` / `Volume` / `Ended` 事件。PC 端 MVP 用 MediaElement 实现它；换 LibVLC 或做 iOS 都只是多一个实现类，上层一行不改。

**元数据读取必须能降级。** TagLib# 遇到没有标签的文件返回空值，遇到损坏文件抛异常，两种都不能让整次扫描失败。

### 4.2 数据流

```
用户选文件夹
  → FolderLibraryScanner.ScanAsync(folder)   递归枚举，过滤扩展名
  → 每个文件交给 TagLibMetadataReader         读标签，失败则回退文件名
  → IReadOnlyList<Track>                     绑定到列表
  → 用户点击某一行
  → PlaybackController.PlayAt(index)          定位队列索引
  → IAudioPlayer.Load(path) + Play()
  → 播放器回报 Position / Ended 事件
  → 进度条更新；Ended 时自动前进到下一首
```

## 5. 界面设计（已确认）

- **布局**：左侧栏 + 歌曲列表 + 底部播放条
- **配色**：浅色，白底，强调色 `#2f6fed`，侧栏底色 `#f5f6f8`，次级文字 `#8b93a2`
- **歌曲行**：两行式。第一行歌名，第二行 `歌手 · 专辑 · 格式`（如 `陈粒 · 在蓬莱 · FLAC`），右侧只有时长。**单击即开始播放**，不做双击语义
- **侧边栏**：正在播放 / 资料库 / 文件夹 / 歌单 / 设置
- **底部播放条**：封面缩略图 + 当前曲名歌手 + 上一首/播放/下一首 + 进度条 + 音量。**封面在 MVP 里只用在这里**，列表行不显示封面
- **窄窗口**：侧边栏自动收成只剩图标，播放条自动折成两行。窗口最小宽度 480px
- **空状态**：居中显示"还没有音乐" + "选择文件夹"按钮
- **标题栏**：自定义标题栏，右上角放**最小化 / 最大化 / 关闭**三个按钮

### 5.1 MVP 阶段侧边栏的处理

侧边栏按最终形态摆放，但本次只有"资料库"是活的，默认选中它、内容区显示"全部歌曲"。"正在播放""文件夹""歌单""设置"显示为禁用态（灰色、不可点）。这样以后加功能时不用重排布局。

### 5.2 界面稿

线框和配色稿在 `.superpowers/brainstorm/` 下（本地文件，未纳入版本控制）。最终确认版是 `full-mockup-v3.html`。

## 6. 错误处理

设计原则：**单个坏文件不能毁掉整次扫描，单个播不了的文件不能卡住整个播放**。

| 情况 | 处理 |
|---|---|
| 文件夹里没有 MP3/FLAC | 显示空状态，并加一行提示"这个文件夹里没有 MP3 或 FLAC" |
| 元数据缺失或为空 | 标题回退为文件名（去掉扩展名），歌手/专辑显示为"—" |
| 元数据文件损坏 | 捕获异常，走上面同一条降级路径，不中断扫描 |
| 某个子目录无读取权限 | 跳过该目录继续，扫描照常完成 |
| 文件在扫描后被删除或移动 | 播放失败时提示一次并自动跳到下一首；若队列中全部失败则停止播放并提示 |
| 文件无法解码 | 同上一行，不做预检（扫描阶段不解码，避免拖慢） |
| 进度/音量超出范围 | 统一 clamp 到合法区间 |

## 7. 测试策略

### 7.1 Core 单元测试（xUnit）

`MusicPlayer.Core` 不依赖 UI，也不依赖真实声卡，因此绝大部分行为可以自动化验证：

- **FolderLibraryScanner**：在临时目录里真实建出 `.mp3` / `.flac` / `.txt` 文件和子目录，断言只收两种目标格式、能递归、扩展名大小写不敏感、空目录返回空列表
- **PlaybackQueue**：下一首/上一首的边界行为（列表头、列表尾、只有一首歌）、播放结束时的自动前进
- **元数据降级**：用一个没有标签的文件断言标题回退到文件名
- **PlaybackController**：用假的 `IAudioPlayer` 断言调用序列（先 `Load` 再 `Play`）和状态变化

### 7.2 必须手验的部分

解码能力是平台能力，单元测试覆盖不到，只能手工验证一次：

- **测试用的音乐一律从 `E:\LocalMusic\` 复制一两首到临时目录后再用**，绝不在原目录上做任何实验——包括调试、格式转换、标签读写。原目录是实际在用的音乐库，任何损坏都不可接受。
- 复制出来的样本必须包含一个 FLAC 和一个 MP3，各播一遍，确认有声音
- 拖进度条、调音量
- 播到最后一首时按"下一首"

### 7.3 不做的事

MVP 阶段不写 UI 自动化测试。对自用工具来说，投入产出比不划算。

## 8. 验收标准

1. `dotnet run` 能起窗口
2. 选一个同时含 MP3 和 FLAC 的文件夹（用从 `E:\LocalMusic\` 复制出来的样本），两种格式都能列出，元数据显示正确
3. 点一首能出声
4. 播放/暂停、上一首、下一首、进度拖动、音量调节都工作
5. 一首播完自动播放下一首
6. 标题栏右上角三个窗口按钮可用，关闭后进程正常退出

## 9. 后续（不在本次范围）

- 搜索框、记住播放位置
- "文件夹"和"歌单"两个入口做活
- iOS 端：复用 `MusicPlayer.Core`，新增 iOS 的 `IAudioPlayer` 实现与界面平台适配
- 若需要均衡器、无缝播放，把音频后端换成 LibVLCSharp
