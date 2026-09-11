# 自动化测试证据归档

> 归档日期：2026-09-11
> 归档人：AI 开发代理（本次会话）

## 文件

| 文件 | 内容 | 结果 |
|------|------|------|
| `editmode-results.xml` | EditMode 全量运行结果（NUnit XML） | `total=97 passed=97 failed=0` |
| `playmode-results.xml` | PlayMode 全量运行结果（NUnit XML） | `total=66 passed=66 failed=0` |

合计 163 项，0 失败。

## 产生方式

```powershell
$Unity = "E:\Unity\Unity\2022.3.62f3\Editor\Unity.exe"
& $Unity -batchmode -nographics -projectPath <工程路径> `
  -runTests -testPlatform editmode `
  -testResults Logs\editmode-results.xml -logFile Logs\editmode.log

& $Unity -batchmode -nographics -projectPath <工程路径> `
  -runTests -testPlatform playmode `
  -testResults Logs\playmode-verify.xml -logFile Logs\playmode-verify.log
```

## 运行环境（重要说明）

运行发生在**由真实工程 `Assets` 同步出的隔离副本** `E:\MergeWaterVerify` 中，而不是真工程本体。原因：

1. 真工程当时被运行中的 Unity 编辑器锁定（存在 `Temp/UnityLockfile`），Unity 批处理模式会以 `HandleProjectAlreadyOpenInAnotherInstance` 拒绝打开同一工程；
2. 本会话的 Unity MCP 桥接连接的是另一个工程（LittleGunfight），无法驱动本工程。

副本与真工程一致的部分：`Assets`（源码、资产、`.meta` 与其中的 GUID）、`Packages`、`ProjectSettings`。生成流程为「在副本中生成占位美术/配置/主场景 → 回同步到真工程 → 再从真工程同步出副本复跑」，因此可代表真工程。

## 真工程本体的补充证据（未跑测试，但已确认工程可用）

| 检查 | 结果 |
|------|------|
| 真工程编辑器是否成功编译全部程序集 | 是：`Library/ScriptAssemblies/` 下 8 个运行时/编辑器 DLL + 2 个测试 DLL 均生成（2026-09-11 13:38–13:44） |
| 是否存在编译错误 | 无：`Logs/AssetImportWorker*.log` 中无 `error CS` / `Scripts have compiler errors` |
| 是否有运行时异常 | 无：导入日志中无 MergeWater 相关异常的记录 |
| 场景引用是否可解析 | 是：`Main.unity` 中 140 处 `guid:` 引用，抽查 `GameBootstrapper`、`HudBinder`、`GameBalance.asset`、`MetaSettings.asset`、`fruit_circle.png` 全部命中 |
| 构建场景列表 | 一致：`EditorBuildSettings.asset` 中 Main.unity 的 GUID 与 `Main.unity.meta` 相同 |

## 尚未完成

- **在真工程本体中跑 Test Runner**（需要关闭锁定该工程的编辑器，或让 MCP 直连本工程）。完成后请用新的 XML 替换本目录文件。
- 手动验收项（手感、观感、真机 60fps、中文渲染）：清单见 `Docs/architecture/0X-*-test.md` 的「手动验收」表。
