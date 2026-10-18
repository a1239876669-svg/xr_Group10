# Shopping Bag 阶段 B：接触反馈与震动请求

状态：可复用模块；Pending Quest 3 Validation；Pending Detailed Apartment Integration。

阶段 A 原型先以 `470468d11d6d9ce96ca9e965ac2e124a41bad29e` 独立提交并推送。本阶段所有产品和测试改动仍局限于 Assets/ShoppingBag；OpenXR 定点修复、ProBuilder 与 Code Coverage 的本地修改不属于购物袋提交。未修改正式公寓、组员脚本、Packages 或 ProjectSettings。

## 组件和接线

```text
ShoppingBag
  原 Rigidbody / Grabbable / GrabInteractable / BagGripFilter / GrabFreeTransformer
  ShoppingBag             增加独立 Editor 持袋模拟；保护 SDK Awake 前的状态读取
  BagHaptics              Sensor → 子对象 BagContactSensor；默认 Editor dry run
  BagInteractionGate      Bag → 同根 ShoppingBag；菜单/教学/传送的接入接口
├─ 原 Body、三个 Handle、GrabPoint、GrabHandleVolume（不变）
└─ BagContactSensor       新增；本地位置/旋转为零、Scale=1
     BoxCollider         独立袋体 Trigger；不使用提手抓取 Trigger
     BagContactSensor    Bag → 根；ContactTrigger → 同对象 BoxCollider
```

BagContactSensor 使用实际有向袋身盒与当前 PhysicsScene 查询。仅持有且有明确左右手所有者时启用；尺寸来自原 BodyCollider，不硬编码尺寸结果。物理扫描同时解决初始重叠、Collider 被禁用/销毁而没有 Exit 回调的情况。按家具累计 Collider 数目，按家具首次进入/最终离开产生事件；支持同触多件家具。复用查询数组及两个计数字典，避免稳定扫描时每个物理帧创建集合。

家具通过 BagContactTarget 显式登记；没有登记的地面、Rig、控制器、UI 和传送辅助对象不会产生警告。触发器及袋子自身排除。不要将这些排除对象错误登记为家具。

BagContactTarget 的 FurnitureColliders 和 FurnitureRenderers 必须明确填写，且属于该目标 Transform 子树；WarningMaterial 使用 Materials/ContactWarning.mat。多个 Collider 对应一个目标；两个袋子的接触源独立计数。门扇或抽屉若要独立警告，应各自登记移动部件而不是整栋公寓。

## 红色覆盖与组员悬停反馈

不替换源 sharedMaterial，不读写源 MaterialPropertyBlock。每个登记的 MeshRenderer/SkinnedMeshRenderer 创建一个无 Collider 的 DontSave 子 Renderer，警告期间启用；解除警告仅关闭覆盖层。因此红色覆盖下方可持续更新青绿色悬停，离开后显示当时的正确悬停或默认状态。

自定义 Unlit Shader：Queue Transparent+100（3100）、不透明红色输出、ZWrite Off、ZTest LEqual、Offset -1/-1、Cull Back；带单通道立体实例化宏和 shader target 3.5。与当前分支的 Built-in 管线完成桌面像素渲染验证。深度偏移避免共面闪烁，深度测试保留遮挡；不是扩大网格或碰撞体。

限制：每个活动 Renderer/子网格增加一次绘制，首次接触懒创建覆盖对象；Quest 3 的立体、MSAA、Vulkan 深度偏移和帧时间仍待测试。透明/裁切材质、特殊高队列材质、LOD 切换、SkinnedMesh blendshape 权重同步需要具体模型验证，当前公寓静态家具不应据此宣称全兼容。不用额外膨胀网格“修复”Z-fighting。

## 震动

当前缓存的 Meta XR SDK 205 明确提供 OVRInput.SetControllerVibration(frequency, amplitude, controllerMask)，SDK PingPongPaddle 示例也用该接口。本组件按 Interaction SDK 的实际持袋 Handedness 选择 LTouch/RTouch，绝不使用 Controller.Active 或 Touch 双手掩码。

默认强度 .35、持续 .08s、频率 .5、冷却 .25s；Inspector 可调。首次家具进入发送请求，持续接触不重发；冷却分别按左右手计算。释放/失焦/暂停/禁用/销毁停止该组件已启动的持袋手脉冲。停止指令可能与将来其他震动系统竞争，正式整合时应统一仲裁震动输出。

Editor 默认 DryRunInEditor=true：记录可观察的 Requested 事件，真正 SDK 握持也不发送硬件指令；Quest Link 测试需明确取消该选项。桌面模拟始终不发硬件指令。设备输出还要求实际 SDK 持有、连接与输入焦点有效、OVR 后端已初始化为 Oculus（OVRManager 在 OVRPlugin 初始化后如此标记）。未满足条件时只是请求，不能视为震动成功。独立 Quest Player 不受 Editor dry-run 选项限制。真实强度、持续、左右路由和硬件停止均待 Quest 3 验证。

## 固定通道与桌面操作

仅编辑 ShoppingBag_Test.unity，保持内容场景：没有 Camera、XR Rig、EventSystem 或第二套移动系统。

原三尺寸配置保持逐字不变，袋身为 .30×.30×.15、.45×.40×.22、.60×.48×.30 米。沿 Z 方向有两段固定通道：A 中心 z=2.2、净宽 .38m；B 中心 z=3.3、净宽 .52m。两侧墙面均为已登记家具代理，边界高度 .7–1.4m。

| 尺寸 | A .38m | B .52m |
|---|---|---|
| Small | 不接触 | 不接触 |
| Medium | 两侧接触 | 不接触 |
| Large | 两侧接触 | 两侧接触 |

以上条件为袋底 y=.75、x=0、袋身直立且宽沿 X。旋转、抬高或横向偏移会真实改变结果。警告不是实体阻挡，持袋时原实体袋身 Collider 关闭，不能宣称大袋会阻止玩家行走。

Unity 手动步骤：

1. 打开购物袋测试场景；使用 Scene View 查看也可。若需要 Game View/Quest Link，Additive 加载既有 SampleScene 或后续已整合 Rig 场景，保留唯一 Rig；不保存正式场景的临时更改。测试内容不要与既有地面/出生点重叠。
2. 启用 Play 模式。原尺寸鼠标面板在左上方；下方 Phase B 面板提供 Simulate LEFT/RIGHT hold、沿 Z 的滑块、Release simulation 和 Reset position。没有新键盘或 VR 控制器绑定。
3. 放下后选择尺寸，再开始模拟，沿同一滑块路线比较。模拟期间禁止改变尺寸，真实 SDK Grip 也被独占过滤器拒绝；模拟结束恢复原重力和刚体状态。测试地面已延伸到第二段之后。
4. 选中 BagContactSensor，打开 Scene Gizmos 查看黄色/红色有向袋体边界。Sensor.ContactCount 和目标 WarningSourceCount 可用于检查多目标。
5. 真正 SDK Grip 抓取时不要同时使用模拟模式。A 传送、B 菜单、右 Trigger UI/家具、左右摇杆均未改写。

## 详细公寓整合边界

后续沿用 fzj/interaction-ui 的 Apartment_Detailed_VR.unity 与唯一现有 Rig。将现有“我的物品”按钮接 TrySetSize；持袋时按钮禁用。BagInteractionGate 的 SetMenuOpen、SetTutorialOpen、SetTeleportAiming 与焦点条件组合，只禁止新抓取，不强行释放正在握持的袋。

本阶段没有连接正式菜单、教学或传送状态，也未修改其输入脚本；Gate 接口测试通过不等于正式场景优先级已实现。菜单结束时 Grip 松开重新武装仍需整合时与现有 Selector 时序一起验证，防止按住 Grip 关闭菜单后意外选择。

家具登记、实际 Renderer/Collider 选择、Gate 状态事件接线和握持点位置需在整合阶段由场景所有者共同确认。不可自动把公寓内所有 Collider 登记为家具；不复制 Rig 或创建独立菜单。

## 已执行验证及证据位置

- C# 和新 Shader 导入编译，使用当前打开的 Unity 6000.6.4f1 会话。
- 最终 26/26 EditMode 测试通过（无跳过），scriptCompilationFailed=false、Prefab Missing Script=0、Shader 消息为空。
- GUID/fileID/.meta 静态检查，原三尺寸配置、近抓和单选配置检查。
- ShoppingBag EditorTests：原型与阶段 B 测试；包含多 Collider、多家具、双袋警告引用计数、多 Renderer、悬停块不被改写、左右震动请求、冷却、释放/失焦/禁用清理、Gate 组合状态和实际物理通道差异。
- 128×128 Camera.Render 像素检查：红色覆盖并在退出后恢复青色；截图在 Logs/ShoppingBagPhaseB-20261015/overlay-warning.png 与 overlay-restored-hover.png。
- 实际 ShoppingBag_Test.unity 通过隔离预览加载：8 个根、4 个家具目标、0 Camera、0 Missing Script；五个路线采样点分别为 Small [0,0,0,0,0]、Medium [0,2,0,0,0]、Large [0,2,0,2,0]。
- 结果 JSON、尝试历史与受保护文件基线保存在同一 Logs 目录，不提交日志、缓存、图片或备份。

EditMode 测试使用隔离预览场景并显式调用被测生命周期，不运行真实 SDK Grip 选择流程；测试退出销毁临时对象，不保存用户场景。通过这些测试不等于 VR 抓取或硬件震动验收。

## Quest 3 次日测试顺序与 PR

1. 确认 Unity 无 C# 错误，基础现有 Cube 和左右 Grip/追踪正常；先测试阶段 A 左右各十次抓放、再次拾取及单手独占，检查真实 HeldHand。
2. 不使用桌面模拟，三尺寸放下切换，靠近固定通道，在相同高度/姿态观察进入、持续、离开、释放后的恢复。
3. 指向家具令其青绿色悬停，再持袋接触变红，移开恢复正确悬停；验证多 Collider、多家具，以及实际 VR 立体中无闪烁。
4. 取消 Editor DryRun 后测试真实左右手短脉冲、持续接触不重复、冷却后重新进入、释放/失焦/禁用停止。日志请求不作为硬件成功依据。
5. 检查 A/B/Trigger/摇杆原有功能。正式菜单/传送优先级必须等详细公寓接线后单独验收。

当前可在需要组员审阅模块时创建明确标记 Pending Quest 3 Validation / Pending apartment integration 的 Draft PR；本次不创建。正式 Ready for review 应在真实左右 Grip、碰撞视觉与震动验收、详细公寓/Gate 接线验证、团队 XR 配置协调完成后提出。合并仍需团队审查。

依赖风险：manifest 的 Meta 包仍使用 E:/edge 本地 tgz 路径，新机器未必具备；当前通过编译依赖已有缓存。共享 OpenXR 修复仍仅在本地，不属于此分支提交。应通过独立团队配置变更处理，不将其混入购物袋功能提交。
