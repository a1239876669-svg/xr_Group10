# M2-B1：独立 VR 公寓场景构建与验证

## 当前交付状态

- 本轮新增 Editor Builder 和本说明；没有自动执行 Builder，没有自动创建 `ApartmentPhase2_VR.unity`。
- 脚本没有 `InitializeOnLoad`、自动构建回调或运行时移动组件。导入脚本不会主动创建场景。
- `.cs.meta` 交由 Unity AssetDatabase 自动生成；不手工编造 GUID。
- Unity 6000.6.4f1；现有渲染管线；Windows PC VR；Meta Quest 3 + Quest Link。
- 使用现有 OpenXR 1.18.0 和 Meta XR SDK 205.0.0，不安装额外 XR 系统。
- 当前没有 Quest 3：编译、场景生成、静态检查和实机验证必须分别记录。

## 路径与依赖

| 用途 | 路径 |
|---|---|
| Builder | `Assets/Scripts/Editor/ApartmentPhase2VRBuilder.cs` |
| 源场景，保持不变 | `Assets/Scenes/ApartmentPhase2_Furnished.unity` |
| 新场景，由菜单生成 | `Assets/Scenes/ApartmentPhase2_VR.unity` |
| SDK 原有预制体 | `Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab` |

预制体 GUID：`126d619cf4daa52469682f85c1378b4a`。通过包的逻辑路径加载，不把 `Library/PackageCache` 的本机缓存目录写入场景。

已对照本机 SDK 源码确认 `OVRCameraRig`、`usePerEyeCameras`、`OVRManager.trackingOriginType`、`TrackingOrigin.FloorLevel`、`OVRHeadsetEmulator` 和 `_trackingOriginType` 序列化字段；对照安装的 Unity 6000.6.4f1 API 文档确认 CopyAsset、OpenScene、SaveScene 和 InstantiatePrefab 的使用方式。最终编译仍需 Unity Editor 确认。

另已使用本机 Roslyn 和项目当前的 455 个引用程序集，对新脚本进行内存中的独立编译诊断：0 Errors、0 Warnings。该检查定义了 `UNITY_EDITOR`，没有输出 DLL 文件，也没有执行 Builder；不等同于 Unity 完整项目编译、Native API 执行或场景验证。

## 运行前与失败处理

本轮只供审查。请在批准运行之后才执行创建菜单。

1. 同一项目只保留一个 Unity Editor，退出 Play Mode，等待导入、编译完成。
2. 人工处理所有未保存场景。Builder 检查所有已加载场景的 dirty 状态；存在任何未保存修改就停止，不自动保存，也不静默丢弃。
3. 源场景及其 `.meta` 必须存在且可加载；源文件不能被排他锁定。
4. 目标 `.unity` **和** `.meta` 必须都不存在，孤立 `.meta` 也会阻止生成。
5. SDK prefab 必须可加载、GUID 符合预期，并含一个 Rig、一个 Manager、零个 CharacterController。
6. 源场景副本必须只有一个 `InspectionCamera`，且无已有 Rig、Manager、CharacterController、移动控制器或 Missing Script；否则停止。

工具用 `AssetDatabase.CopyAsset` 复制已保存的源场景，验证新 GUID 和复制内容，然后以 **Single 模式**打开新场景。其他已保存场景会从 Hierarchy 关闭，文件不会被保存或修改。Single 模式避免把旧玩家及 InspectionCamera 与新 Rig 同时加载。

创建后只调用新场景的 `SaveScene`，不调用全项目 `SaveAssets`，不更改 Build Settings。保持 prefab 关联，组件变化记录为场景实例覆盖。

任一步失败都输出 Console 错误并停止，不自动删除、恢复或覆盖资源。若复制后失败，目标可能是原场景副本或未完成草稿；不能把它视为完成的 VR 场景。保留现场审查，禁止直接重复运行覆盖该文件。成功创建一次后再运行也会停止。

## 生成后的层级与参数

```text
ApartmentRoot                       原结构、家具、路线和标记保持不变
InspectionCamera                    在新场景中禁用且取消 MainCamera 标签
VRPlayer                            根位置 (0.45, 0, 0.70)，Yaw=0，Scale=1
  CharacterController
  OVRCameraRig                      原包 prefab 实例，Local Position=0，Rotation=0，Scale=1
    OVRCameraRig                    usePerEyeCameras=false
    OVRManager                      FloorLevel
    OVRHeadsetEmulator               禁用
    TrackingSpace
      CenterEyeAnchor                唯一有效 Camera，MainCamera，唯一有效 AudioListener
      LeftEyeAnchor                  Camera 禁用，Untagged
      RightEyeAnchor                 Camera 禁用，Untagged
      LeftHandAnchor / RightHandAnchor
      LeftControllerAnchor / RightControllerAnchor
      TrackerAnchor
```

Rig、Manager 和 Emulator 是 OVRCameraRig 对象上的组件，不是额外子 GameObject。不复制 TwoRooms 的 JoystickLocomotion，不新增 OVRPlayerController、XR Origin 或 XR Interaction Toolkit。手部和控制器 Anchor 是跟踪锚点，本轮不添加交互物体或可视控制器模型。

| CharacterController 参数 | 初始值 |
|---|---:|
| Height | 1.80m |
| Radius | 0.30m |
| Center | (0, 0.90, 0)m |
| Skin Width | 0.03m |
| Step Offset | 0.20m |
| Slope Limit | 45° |
| Min Move Distance | 0m |

1 Unity Unit = 1m；公寓地面 Y=0。出生点来自 M1-B 的 `spawnCandidate`，是待验证候选。Rig 根使用地面 Y=0，不使用绿色标记的 Y=0.04，也不人为把眼睛抬高 1.8m。

## Unity 操作与 Console 验证

获批运行后：

1. 等待脚本导入完成。确认没有新的编译错误，并确认 Unity 已生成脚本 `.meta`。菜单不出现时先检查 Console，不复制第二份脚本。
2. 执行 `Apartment Phase 2 → M2-B1 → Create VR Scene (No Overwrite)`。
3. 成功保存时 Console 显示 `M2-B1 scene saved: Assets/Scenes/ApartmentPhase2_VR.unity`，随后输出 `M2-B1 static check`。
4. 确认 Project 中目标 `.unity` 和其 `.meta` 已出现。查看新场景 Hierarchy，确认只有一个 VRPlayer 和一个 Rig。
5. 在 Inspector 检查上述参数、prefab 关联、FloorLevel、禁用的 Emulator 和 InspectionCamera，以及唯一有效 Camera/AudioListener。
6. 打开 Collider Gizmos，检查入口处胶囊和鞋柜、墙体、门洞、地面的关系。保持所有结构和家具尺寸；遇到重叠先记录，不自动调小胶囊或移动固定几何。
7. 可重复执行 `Apartment Phase 2 → M2-B1 → Validate Active VR Scene (Read Only)`。该菜单不保存、不修改对象、不创建探针物体。
8. 验证菜单只检查活动的目标场景；若另有 Additive 场景会提示，Play Mode 前只保留目标场景。保持 duplicate settings 警告，不点击 Fix/Fix All。

静态检查区分：

- **建模/配置错误**：Rig、Manager、Camera、AudioListener、胶囊、参数、prefab 关联不符合契约；Missing Script；存在移动控制器；出生胶囊穿入 BoxCollider。
- **需要审查的限制**：SkinWidth 附近的小净空、其他 Additive 场景、未保存的场景状态。不会调整几何来消除提示。
- **未经验证**：不支持的 Collider、PhysX 行为、实际 Floor Level、Quest Link 初始化、头部与手柄跟踪、真实走动及全部后续交互。

初始重叠使用垂直胶囊轴线到轴对齐 BoxCollider 的最短距离，半径为 0.30m，容差 0.001m；支持盒体的父级缩放和 90°轴向旋转。地面相切计为 `contacts`，不当作穿透；`overlaps` 才表示超过容差的几何重叠。非轴对齐或其他 Collider 计入 `unsupported` 并逐项提示，不能在此情况下宣称全部碰撞通过。它不调用 Physics 查询，不能替代 PhysX 验证，也不模拟 SkinWidth、重力或移动碰撞。

## 后续 HMD 偏移与实机验证

本轮胶囊固定在玩家根，不实现胶囊跟随、重力或 Move/SimpleMove。真实走动的头部跟踪不会自动受到该固定胶囊约束，不能据此声称已经防止穿墙。

以后可用 `VRPlayer.InverseTransformPoint(CenterEyeAnchor.position)` 得到 HMD 在玩家根中的位置，再让胶囊中心 X/Z 跟随该水平偏移、Y 使用 `height / 2`。高度需结合佩戴者和校准策略另行设计。胶囊更新与移动、碰撞处理应统一实现，避免多个脚本同时修改玩家根；本轮不添加这段运行时逻辑。

有 Quest 3 后再验证：Quest Link/OpenXR 会话、Meta XR Feature 初始化、头显画面与眼高、Floor Level、头部/控制器姿态、重置跟踪后的出生位置、碰撞以及帧率。没有设备时的 Game View 画面不能代替这些检查；不为改善无设备预览而改动 FloorLevel 或加入模拟移动。

## 状态记录

| 项目 | 本轮状态 |
|---|---|
| Builder 与说明 | 已新增，可审查 |
| 新脚本独立编译诊断 | 455 个引用程序集，0 Errors / 0 Warnings，仅内存检查 |
| Unity 项目编译 | 待用户在 Editor 确认 |
| VR 场景生成与保存 | 待批准后执行菜单 |
| Editor 静态验证 | 待生成场景后执行 |
| Unity Physics、Quest Link 和实机体验 | 尚未验证 |

不进行 Git 提交/推送，不进入 M2-B2，不开发移动、购物袋、Marker 或反馈 UI。
