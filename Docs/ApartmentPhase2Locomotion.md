# 公寓 VR：走路、转向与 A 键传送

## M2-B3：A 键传送（2026-10-13）

已在个人分支 `fzj/teleport` 实现传送代码和场景配置，没有提交或推送。原本的包修改、场景修改和测试覆盖率设置均保留；原公寓灰盒及 Furnished 场景未改动。下面的 M2-B2 章节是历史实现记录，不代表最新安装状态。

玩法：右手 **按住 A** 显示弧线，绿色圆圈可以落脚，**松开 A** 传送；红色落点不传送。最大距离 5 米。保留左摇杆走路；右摇杆默认 **连续转向**，拨得越多转得越快，满幅 60°/秒。瞄准时暂停水平走路/转向，但保留重力。系统菜单、失焦、断连或追踪丢失会取消瞄准，恢复后需要先松开再重新按 A，摇杆回中后才能恢复转向。只有 VR 模式能提交传送，键盘 Debug Mode 不提供虚假的手柄测试。

### 跨房间传送 / 连续转向修复（2026-10-13）

用户实测发现其他房间不能传送。旧地图代理高度 2.10m，与所有房间门框净高 2.10m 相同，体素计算后门洞没有余量；修复前的门洞诊断中五个门洞均未得到完整路径。另外旧版直接对起点至落点扫掠身体，斜着瞄准时可能碰到门边，即使有可走路线也拒绝。

修复采用 1.80m 的候选导航高度，不改变公寓几何或玩家实际身高。每次传送仍对**实际追踪身体高度**检查落点和导航路线的每段，因此真正低矮门框、实时关闭的门和家具仍可阻挡。弧线仍在首次实体碰撞处停止，不能指向墙后的地板。通过开放门洞指向 5m 内的安全地板才能传送，不是随意穿墙到任意房间。

右摇杆 `Turn Mode = Smooth`、`Smooth Turn Speed = 60`、`Turn Dead Zone = 0.20`。连续转向围绕头部水平位置同时更新根位置与朝向，避免先旋转再碰撞补偿造成侧移。觉得太快可在退出 Play 后把速度设为 30–45，并保存场景；不适时停止测试，也可选择 `Snap` 回到 30° 分段转向。Q/E 键盘调试仍用分段转向，不能代替真实摇杆验收。

`Run Teleport Checks` 新增五个门洞双向传送、关闭门阻挡/重开恢复、实际高胶囊拒绝、斜向门洞路线，以及连续转向死区、持续输入、恢复回中、不同帧率和头部偏移检查。临时玩家和门碰撞体只在未保存的独立场景内使用，不修改公寓对象。

修复后已在 Unity Editor 编译并执行 **97 项检查，全部通过**（`Logs/TeleportChecks.json`，2026-10-13T02:31:21Z）。新地图为 `Assets/Scenes/ApartmentPhase2_VR_Teleport 1.asset`，旧地图保留，可恢复。VR 场景相比修复前仅更新地图引用并保存三项转向设置，没有改变建筑/家具尺寸。仍须用户通过 Air Link 戴 Quest 3 检查真实 A 键、弧线瞄准和连续转向舒适性；报告 `questLinkVerified=false`，不把 Editor 通过当作真机通过。

### 已配置的场景

- 场景：`Assets/Scenes/ApartmentPhase2_VR.unity`，原有 VRPlayer/OVRCameraRig/CharacterController 不重复添加。
- Debug Mode 已关闭，DesktopDebugCamera 保持 inactive，CenterEyeAnchor 为有效相机和音频监听器。
- 14 块 `Floor_…` 和 6 块 `Threshold_Door_…` 归类为 `TeleportFloor`；墙、门框和家具不是落点。
- ApartmentRoot 上的 NavMeshSurface 收集 Current Object Hierarchy，使用 Physics Colliders，包含 Default/TeleportFloor，Walkable，不生成 Links，Voxel Size 0.05。
- TeleportPlayer：Radius 0.30、候选导航 Height 1.80、Step Height 0.20、Max Slope 45。实际身体高度由头显决定（最大 2.10），落点和通路用实时胶囊检查。原 Humanoid 参数不改。
- 当前地图保存在 `Assets/Scenes/ApartmentPhase2_VR_Teleport 1.asset`，移动组件已引用此地图。旧版 `ApartmentPhase2_VR_Teleport.asset` 未删除。

### 安全规则与接口

弧线检查所有非 Trigger 实体，在首次碰撞处截止，不穿过家具找后面的地板。落点必须有完整地板支撑、属于指定体型的导航地图、与当前区域连通，且通过最终胶囊净空检查。沿完整导航路线分段扫掠实际身体，不再要求直线冲过门洞；关闭的门或新障碍物仍会拒绝传送。松开 A 后重新计算弧线，真正移动前同步碰撞并再次检查落点；旧的绿色落点不构成承诺。传送移动整个 VRPlayer，保留朝向/眼高，补偿真实头部的水平偏移，并重置下落速度。

UI/交互后续可读取 `TeleportState`、`TeleportFailureReason`、`TeleportTarget`、`IsTeleportAiming`，订阅 `TeleportStateChanged` 和 `Teleported`。`TryTeleportTo(Vector3, out string)` 是唯一传送提交入口；外部调用也不能绕过头显追踪、输入焦点与碰撞检查。`ValidateTeleportDestination` 仅查询不移动。

运行时自动生成不带 Collider 的弧线和落点圈，停止运行后清理。当前使用 Built-in 的 Unlit/Color；Android 打包和 shader stripping 尚未验证，不要把本阶段的 Link 测试当作 APK 验收。

### 菜单与新手测试步骤

1. 仅打开 ApartmentPhase2_VR，先退出 Play 并保存有意保留的修改。
2. 场景已经配置，不必再次安装。以后调整固定家具/地板时可运行 `Apartment Phase 2 → M2-B3 → Configure A-Button Teleport` 重新烘焙；每次保留旧地图资产并创建新的独立资产，不覆盖同学的数据。
3. 用 `Validate Teleport (Read Only)` 检查结构，用 `Run Teleport Checks` 检查按钮状态机、偏移数学和实际 Physics/NavMesh 查询。检查的临时碰撞体只位于单独的未保存场景，完成后关闭，不写入公寓。结果写入 `Logs/TeleportChecks.json`。
4. 电脑打开 Quest Link，在头显进入 Link；如果使用过 Simulator，先停用模拟器。确认 Debug Mode 没有勾选，在 Unity 点击 Play。
5. 先确认头显转头、左摇杆走路和右摇杆转向，再按住右手 A 指向空地，看到绿色圆圈后松开。
6. 分别尝试墙、桌面、床、地板边缘、家具旁窄缝；这些无效落点不能移动。真实站位偏移后反复传送，检查眼高/落点。瞄准时打开系统菜单，再返回，不能自动传送。

Editor 检查不等于真机验收：真实 A 键、弧线显示、眼高、舒适性以及 Link 会话需用户戴头显确认。项目刷新时另有 OpenXR FeatureHelpers 的空引用错误，属于现有 XR 配置/SDK 路径；本阶段不修改 XR 配置来掩盖它，实际会话若不能启动需另外排查。

首次实现时的验证（修复前）：Unity Editor 编译完成，原 44 项自动检查通过；进入并退出 Play Mode，Inspector 显示 `Waiting for tracked HMD` / `Unavailable`。当时 OpenXR 返回 `XR_ERROR_FORM_FACTOR_UNAVAILABLE`，并出现已有 FeatureHelpers 空引用，未验证真实 VR 会话。用户随后通过 Air Link 实测并报告了门洞和转向问题；本次修复后的最新 97 项检查结果见上方“跨房间传送 / 连续转向修复”。历史 Play 检查没有保存运行时状态。

## M2-B2 历史记录：VR 移动与转向

适用项目：Unity **6000.6.4f1**，Meta XR SDK **205.0.0**，OpenXR **1.18.0**；Windows PC VR，Quest 3 + Quest Link。

本轮只新增运行时脚本、Editor 安装/验证工具和本文档。**尚未运行安装菜单，也没有修改或保存 VR 场景。** 不修改公寓几何、原场景、已有 Builder、包、XR 设置或其他项目配置。本文中的菜单行为仅在用户随后手动执行时发生。

## 已核实的依赖与输入

- 现有 `ApartmentPhase2_VR.unity` 中，VRPlayer 是场景根对象，起点 `(0.45, 0, 0.70)`，Yaw=0、Scale=1；一个 CharacterController，一个子 OVRCameraRig，保留 SDK prefab 关联。
- Rig prefab：`Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab`。OVRManager 使用 FloorLevel；InspectionCamera 已禁用；OVRHeadsetEmulator 已禁用。
- SDK 205 的 `OVRInput.Get(Axis2D, Controller)`、`IsControllerConnected`、`OVRManager.instance/isHmdPresent/hasVrFocus` 和 Rig `centerEyeAnchor` 已对照本机包缓存源码。SDK 的 OVRManager 更新 OVRInput；本脚本不重复调用 `OVRInput.Update()`。
- 左输入：`OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch)`；右输入使用相同轴枚举和 `Controller.RTouch`。源码中 LTouch 的 PrimaryThumbstick 映射左摇杆，RTouch 的 PrimaryThumbstick 映射右摇杆，避免 Combined/Active 控制器的歧义。
- 通过 Unity XR 的 Head device `CommonUsages.isTracked`、`XRSettings.isDeviceActive`，以及 SDK HMD/焦点状态，限制 VR 输入。没有有效头显追踪时不读取手柄；仍执行重力。SDK 输入异常会暂停本组件的 VR 输入，重新启用组件才重试，避免持续异常。
- 项目 `activeInputHandler=1`（Input System）。电脑调试使用现有 Input System 的 Keyboard，不调用旧 `Input.GetAxis`，不安装新输入系统。
- Standalone 的 XRGeneralSettings 引用唯一 OpenXRLoader，`m_InitManagerOnStart=1`；没有为本轮另装加载器。实际启动后若 SDK 报告的跟踪原点不是 FloorLevel，脚本停止输入并报告，不会自行更改原点或项目设置。

以上证明 API/结构适合实现，**不证明 OpenXR + Quest Link 下这些输入已经可用**。仍需检查 Meta Quest Link 软件的运行时选择、实际会话、手柄和焦点；本轮不更改它们。

## 默认参数与控制

| 项目 | 默认值 / 行为 |
|---|---|
| 左摇杆 | 沿 HMD 水平 forward/right 连续移动；斜向速度不超过设定速度 |
| 速度 | 1.2 m/s |
| 额外移动死区 | 0.15；SDK 本身也可能已处理死区，实机需评价手感 |
| 右摇杆转向 | 水平超过 0.70，单次 30°；右正向右转 |
| 回中 | 整个右摇杆向量长度 ≤0.25 后才重新解锁；启动/失焦/重连/切模式后也先回中 |
| 胶囊半径 / Skin Width | 0.30 m / 0.03 m（默认占位直径 0.60 m） |
| 初始胶囊高度 / 中心 | 沿用场景 1.80 m / `(0, 0.90, 0)`；获得有效头部姿态后动态调整 |
| 动态高度 | `headLocalY + 0.15`，限制在 0.90–2.10 m；center.y=height/2，胶囊底部保持玩家局部地面 |
| 水平中心 | 头部在 VRPlayer 局部坐标的 x/z；仅接受可通行的胶囊调整 |
| Step Offset / Slope Limit | 0.20 m / 45° |
| 重力 / 地面下压速度 / 下落限速 | -9.81 m/s² / -1 m/s / -20 m/s |
| Min Move Distance | 0；静止也调用 Move 并检查 Below/isGrounded |
| 单帧时间上限 | 0.05 s；卡顿帧不执行过大的移动步长，卡顿时移动会变慢 |
| Debug Mode | 默认关闭；不自动根据头显缺失启用 |

正常运行只使用 `CharacterController.Move` 执行玩家位置变化；不使用 Transform.position、Translate、RotateAround 或 SimpleMove 位移。仅转向设置根对象的 rotation，再通过 Move 补偿 HMD 水平偏移。

转向前对偏离玩家根点的胶囊临时运动路径做碰撞预检；无法安全补偿时拒绝此次转向，不强制穿墙。右摇杆仍需回中才可重试。实际补偿被碰撞限制、视点漂移超过 1 cm 时输出警告，需人工检查。此方案较保守，靠墙且头部偏移较大时可能无法转向；不要通过缩小家具或挪墙消除该限制。

胶囊追随头部前检查目标空间和水平扫掠；阻挡时暂停摇杆移动/转向，保留原胶囊，提示回到原先可通行位置。查询忽略 Trigger 和玩家自己的 Collider，使用去掉 Skin Width 的查询半径避免地板接触被误判；查询缓冲区满时保守判阻挡。实际 Move 仍由完整 CharacterController 处理碰撞。

**物理走动不等于摇杆移动。** 系统不会强制冻结、移动或遮挡真实 HMD；暂停胶囊调整也不能阻止用户把真实头部探入虚拟墙体。房间尺度越界反馈、头部独立碰撞及安全舒适性仍未实现/验证。动态追踪、屈身、低顶、身高上限、重定位都必须在实机测试。

## 手动安装（当前尚未执行）

1. 等待 Unity 自动导入这两个新 `.cs` 并生成 `.meta`；确认 Console 没有编译错误。不要覆盖已有 `.meta` 或修改 SDK 来解决编译问题。
2. 手动打开 `Assets/Scenes/ApartmentPhase2_VR.unity`；只加载这一个场景，不采用 Additive。
3. 检查并自行保存有意保留的修改。安装器遇到未保存修改直接停止，不代为保存/丢弃。退出 Play Mode，并等待导入完成。
4. 执行 **Apartment Phase 2 → M2-B2 → Install Locomotion (No Overwrite)**。确认对话框说明后，点击 **Install and save VR scene**。
5. 预期：VRPlayer 新增唯一 ApartmentVRLocomotion，Rig/Manager/desktopCamera 引用完整；新增子对象 DesktopDebugCamera，局部位置 `(0, 1.65, 0)`，Camera 和 AudioListener 均禁用，GameObject inactive；原 Rig 仍为唯一有效主摄像机和音频监听器。
6. 安装器仅保存 `ApartmentPhase2_VR.unity`，不会创建/修改其他资产；原公寓场景及其 `.meta` 和目标场景 `.meta` 的哈希会核对。已有安装组件或同名调试对象时停止，不重复添加、不升级安装。
7. 再执行 **Apartment Phase 2 → M2-B2 → Validate Active VR Scene (Read Only)**，检查 Console 汇总及每条错误/限制。验证不会保存场景或改变对象。

安装前还会检查导入后的脚本类型、唯一 Rig/Manager/CharacterController、预制体关联、FloorLevel、有效主 Camera/AudioListener、缺失脚本、旧 JoystickLocomotion/OVRPlayerController 等冲突、胶囊和初始几何重叠。没有安装时验证菜单报告缺少组件属于预期。

失败处理：保存前失败会 Undo 本次安装登记的新增对象/组件；不恢复、删除或覆盖磁盘资产。保存失败的磁盘状态无法保证原子性，应检查 Console、场景和磁盘文件后再决定处理，不直接重跑。保存后检查失败会报告已保存状态，停止恢复操作。任何已有安装均禁止自动覆盖。

## 无头显的电脑端调试

1. 安装完成后先运行只读验证，保持 Edit Mode 的 DesktopDebugCamera inactive；进入 Play Mode。
2. 在 VRPlayer 的 ApartmentVRLocomotion 中，**仅在 Play Mode 勾选 Debug Mode**。点击 Game 窗口获得输入焦点。
3. 用 W/A/S/D 移动，Q/E 分别向左/右分段转向。持续按住 Q/E 只转一次；释放后再次按下才转。同时按 Q+E 不视为回中。
4. 调试相机高度固定 1.65 m、非立体；此模式禁用 Rig 的锚点更新组件和 Rig Camera/AudioListener，VR 手柄输入分支完全不执行。OVRManager 保留运行，脚本不改 XR 初始化。相机不需要鼠标捕获或鼠标转向。
5. 取消 Debug Mode 时恢复原 Rig 状态，禁用调试相机，转向锁重新要求回中。停止 Play Mode 后确认 Debug Mode 没有被保存为开启状态。

未连接头显且 Debug Mode 关闭时，预期无输入移动、无重复脚本空引用错误，玩家仍受重力；没有普通桌面视角不代表 VR 相机故障。SDK 自身可能报告没有头显或会话，调试模式不负责关闭/修复这些已有初始化行为。

电脑调试只能验证键盘分支、基本碰撞/路线和转向逻辑；**不能标记 Quest Link、真实控制器、追踪高度、真实转向舒适性为通过**。

## 分层验证与记录

| 层级 | 要检查的内容 | 当前状态 |
|---|---|---|
| 文件/离线编译 | 本机已安装 Unity/SDK 程序集能否解析两份源码；回中锁状态机 | 由本轮报告列出实际结果，不等同 Editor 编译 |
| Editor 静态检查 | Rig/胶囊/移动脚本唯一，引用完整，唯一有效主 Camera/AudioListener，FloorLevel，出生重叠 | 安装后由用户执行；不会保存 |
| Desktop Play Mode | WASD/斜向速度、Q/E 回中、墙体碰撞、闲置重力、门洞/路线、模式切换 | 未验证 |
| Quest Link Play Mode | 左手移动、右手转向、失焦/重连、HMD 偏移/身高、转向漂移、真实碰撞/地面、舒适性 | 无 Quest 3，未验证 |

本轮实际离线结果：使用本机已安装的 Unity/SDK 程序集，仅在内存中编译 Editor、Player 和未启用 Input System 的保护分支，均为 0 错误、0 警告。提取实际源码中的转向回中锁，在内存中执行 16 项状态机检查，全部通过。未执行 Unity Editor、安装菜单、Physics 或 VR 测试；这些结果不能替代上表后三层验证。

只读验证将结果区分为 MODEL/CONFIG ERROR、DESIGN LIMIT / CHECK COVERAGE 与 UNVERIFIED。初始几何检查使用垂直胶囊与轴对齐 BoxCollider 的解析距离；地板相切属于正常接触。任意旋转/非 Box 的 Collider 会记录覆盖限制，不冒充 Physics 验证。运行时独立使用 Physics 查询和 Move；这两部分都仍需要 Play Mode 验证。

建议逐项记录：

1. 初始站位：地面 Y=0、脚部贴地，未重叠入口柜/墙；不自动调整已批准的起点或家具。
2. 无头显：Debug Mode 关闭，运行至少 30 秒；脚本不重复报错、重力不停止。
3. 桌面碰撞：分别正面/斜向撞墙、家具、床与柜；松开键盘后不继续水平滑行；不穿过固定 Collider。
4. 转向：初次按住不自动连续转；释放后再触发；在宽敞处、HMD 偏离根点、靠墙处分别检查。拒绝转向与超过 1 cm 的漂移单独记录。
5. 路线：Entrance → Living Room → Dining Area → Kitchen；检查厨房门洞转身、卫生间/Bedroom 2 门外，以及阳台支路。保留 M1-A/M1-B 原路线和已记录的狭窄限制，不为零失败改几何。
6. 先以默认 0.60 m 胶囊检查；0.80 m 占位体只作为另一项人为评价。退出 Play Mode，分别将移动脚本的 Capsule Radius 和 CharacterController 的 Radius 设为 0.40，再验证和重新运行；Skin Width 保持小于 Radius，最小高度不小于两倍半径。半径/Skin/Step/Slope 在组件启用时初始化，不采用只改 Play Mode Inspector 就已生效的假设。单独记录设计限制，并恢复默认值后再保存有意保留的 VR 场景设置；不要将该评价视为购物袋或无障碍测试。
7. 实机检查 FloorLevel、真实站立/屈身/头部偏移、手柄死区、失焦后停止、重连时需回中；持续检查相机/音频唯一。观察蹲下后站起是否被低顶限制。
8. 若出现持续异常、穿透、地面漂移或显著视点跳动，停止测试并记录复现条件，不继续缩小占位体或修改墙体“修复”。

本阶段未实现购物袋、Marker、反馈 UI、教程或导出；未修复 OpenXR duplicate settings 警告，未修改任何 XR 配置。
