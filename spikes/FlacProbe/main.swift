// 一次性探针：验证 Apple 的音频栈能不能解码我们的 24bit / 96kHz FLAC。
//
// 为什么用 Swift 而不是 .NET：
//   .NET 的 iOS 工具链在 CI 镜像上调不到 Apple 的命令行工具
//   （actool 和 install_name_tool 是同一个 xcrun 失败）。
//   而 swiftc / xcrun 在普通 shell 里是正常的，所以绕开 .NET 工具链、
//   直接用 AVFoundation，才能干净地回答这个问题。
//
// 同一个程序在两处跑：
//   macOS 原生  —— 用的是和 iOS 同一套 CoreAudio/AVFoundation 解码器
//   iOS 模拟器  —— 真 iOS 运行时

import AVFoundation
import Foundation

func fail(_ message: String) -> Never {
    print(message)
    print("结论: FAIL")
    exit(2)
}

let path: String
if CommandLine.arguments.count > 1 {
    path = CommandLine.arguments[1]
} else if let bundled = Bundle.main.path(forResource: "sample-24bit96k", ofType: "flac") {
    path = bundled
} else {
    fail("找不到测试样本")
}

let url = URL(fileURLWithPath: path)
let sizeMB = (try? FileManager.default.attributesOfItem(atPath: path)[.size] as? Int)
    .flatMap { $0 }.map { Double($0) / 1024 / 1024 }

print("=== FLAC 解码探针（Swift + AVFoundation）===")
print("运行平台: \(ProcessInfo.processInfo.operatingSystemVersionString)")
print("样本: \(url.lastPathComponent)  \(sizeMB.map { String(format: "%.2f MB", $0) } ?? "?")")

// 1. 容器与轨道信息
let asset = AVURLAsset(url: url)
let tracks = asset.tracks(withMediaType: .audio)
guard let track = tracks.first else {
    fail("[1] 失败：AVAsset 里没有音频轨（连容器都不认）")
}
print("[1] AVAsset 解析成功：时长 \(String(format: "%.2f", asset.duration.seconds)) 秒，音频轨 \(tracks.count) 条")

for desc in track.formatDescriptions {
    guard let audioDesc = desc as? CMAudioFormatDescription,
          let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(audioDesc) else { continue }
    let d = asbd.pointee
    print("      轨道格式：\(d.mSampleRate) Hz / \(d.mChannelsPerFrame) 声道 / \(d.mBitsPerChannel) bit")
}

// 2. 真正解码 —— 这一步才说明系统解码器吃不吃得下。
//    打不开 = 格式不支持；读不出帧 = 解不出数据。
do {
    let file = try AVAudioFile(forReading: url)
    let sd = file.fileFormat.streamDescription.pointee
    print("[2] AVAudioFile 打开成功：文件格式 \(sd.mSampleRate) Hz / \(sd.mChannelsPerFrame) ch / \(sd.mBitsPerChannel) bit")

    let wantFrames: AVAudioFrameCount = 96000  // 约 1 秒
    guard let buffer = AVAudioPCMBuffer(pcmFormat: file.processingFormat, frameCapacity: wantFrames) else {
        fail("[2] 失败：无法分配 PCM 缓冲区")
    }
    try file.read(into: buffer, frameCount: wantFrames)
    guard buffer.frameLength > 0 else {
        fail("[2] 失败：打开成功但一帧都没解出来")
    }
    print("      实际解码 \(buffer.frameLength) 帧 —— 解码器确实在工作")
} catch {
    fail("[2] 失败：解码抛异常 \(error)")
}

print("结论: PASS")
