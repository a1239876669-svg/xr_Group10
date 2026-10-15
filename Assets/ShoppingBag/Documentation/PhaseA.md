# Shopping Bag Interaction — 阶段 A

## 交付范围与版本

本模块只负责白模、尺寸、Meta SDK 控制器近抓和抓取状态。没有菜单、家具变红、震动、远抓、手部捏合抓取或新的 XR Rig／移动系统。所有新内容位于 `Assets/ShoppingBag/`（及该目录的 `.meta`）。没有修改已有文件。

开发前工作区干净，分支为 `feature/shopping-bag-interaction`。执行 `git fetch --prune origin` 成功后，关注的远程引用与此前报告一致：

| 引用 | 提交 |
| --- | --- |
| origin/main | fc0444ccc6b782527907ed2762a493d036694494 |
| origin/fzj/interaction-ui | a976cbd16a5067c5a33da9fbff83aa340b7b5dde |
| origin/measure-mark-integration | 87f1f68（依赖修复） |

当前购物袋分支仍以 fc0444c 为基础。没有 Merge、Rebase、Reset、Checkout、Commit、Push 或 PR。正式详细场景尚未进入 main；较新的测距／标注整合分支也修改了菜单和家具脚本，阶段 A 不取回这些文件。

## 脚本与接口

| 脚本 | 职责 |
| --- | --- |
| BagSizeProfile.cs | Inspector 可编辑的尺寸配置：袋身宽／高／厚、提手高度与厚度；验证有限正数及提手参数 |
| ShoppingBag.cs | 根据配置同步白模与 BoxCollider；限制持袋时改尺寸；暴露持袋手、抓取点、状态事件和未来门控入口 |
| BagGripFilter.cs | 实现 SDK IGameObjectFilter，只接收有 Grip-only ControllerSelector 的近抓 GrabInteractor；拒绝远抓、射线和手部交互器 |
| BagSizeTestControls.cs | 测试用 Inspector Context Menu、公开尺寸方法、Game View 鼠标面板；不读取 A／B／Trigger／摇杆 |
| ShoppingBagTests.cs | Editor NUnit 测试：尺寸与碰撞体、持袋拒绝调整、无效尺寸／缩放、单手配置和过滤器 |
| asset_checks.py | 无依赖的静态资源检查；默认只读。`--generate` 显式重新生成资源，会覆盖模块资源，不用于日常验收；`--metadata` 只补缺失 meta |

运行时程序集：`XRGroup10.ShoppingBag`，仅显式引用 `Oculus.Interaction`。不依赖当前 Assembly-CSharp 内的组员类。

后续公共入口：

- `TrySetSize(BagSize)`：成功返回 true；持袋、非法配置、根／祖先缩放时返回 false，不改变尺寸。
- `CurrentSize`、`Profile(size)`、`BodySize`、`GrabPoint`、`BodyCollider`。
- `IsHeld`、`HeldInteractor`、`HeldController`、`HeldHand`。
- `SizeChanged` 和 `HoldStateChanged`：后续接尺寸 UI、接触状态清理和持袋手震动。
- `SetAcquisitionAllowed(bool)`：未来 Gate 阻止新增抓取，保持现有释放流程。该入口本身不是完整菜单门控，不包含松 Grip 后重新武装或失焦策略。

没有读取全局 OVRInput，没有修改已有 Selector。SDK 原有 Grip Selector 负责选择与释放。筛选器使用 SDK IController 的连接／姿态状态，并核对选择器只绑定 Grip。已有 Rig 若使用复合 Selector 或把 Selector 放在交互器子树外，将被保守拒绝，需要先核对接线。

## 尺寸定义

使用 Unity 1 单位 = 1 米，尺寸指**袋身**，提手是额外高度。根位于袋身底面中心，根和所有祖先 Scale 必须为 (1,1,1)。

| 配置 | 袋身宽 × 高 × 厚 | 额外提手高度 |
| --- | --- | --- |
| Small | 0.30 × 0.30 × 0.15 m | 0.10 m |
| Medium | 0.45 × 0.40 × 0.22 m | 0.12 m |
| Large | 0.60 × 0.48 × 0.30 m | 0.14 m |

提手厚度默认 0.02 m，横向跨度为袋宽的 55%。抓取点位于横提手中心，与横提手一起移动；Collider 尺寸直接取配置，没有预设通行成功／失败结果。默认 Medium。

编辑 Config 中的资产会影响引用它的所有袋实例。运行中编辑资产不会每帧自动应用；在袋子放下后再次调用 TrySetSize／测试入口应用。不要在 Play 模式改 `initialSize` 期待立即切换。若袋子放下后已经倒转，调整尺寸可能与环境重叠；阶段 A 没有安全放置／净空检查。

## Prefab 层级与 Inspector

```text
ShoppingBag                       Scale 1; 底面中心为原点
  Rigidbody                       动态、重力、0.3 kg、Interpolate、Continuous Speculative
  Grabbable                       MaxGrabPoints=1; kinematic while selected; 不施加投掷速度
  GrabInteractable                 MaxSelectingInteractors=1; 只配置近抓
  BoxCollider                     袋身实体；持袋时关闭，释放时恢复
  ShoppingBag                     三尺寸、几何和 SDK 引用
  BagGripFilter                   只接受控制器 Grip 近抓
  GrabFreeTransformer             单手自由移动／转动
├─ Body                           Cube MeshRenderer；无独立 Collider
├─ HandleLeft                     Cube MeshRenderer
├─ HandleRight                    Cube MeshRenderer
├─ HandleTop                      Cube MeshRenderer
├─ GrabPoint                      固定抓取源；位于横提手中心
└─ GrabHandleVolume               BoxCollider，IsTrigger=true
```

资源已预先序列化接线。Unity 导入后必须检查以下引用没有丢失：

1. `ShoppingBag.prefab` 根的 ShoppingBag：Small／Medium／Large 分别指向三个 Config 资产；Grabbable、GrabInteractable、Rigidbody 指向同根组件。
2. BodyVisual、LeftHandle、RightHandle、TopHandle、GrabPoint 指向同名子对象；BodyCollider 指向根 BoxCollider；HandleTrigger 指向 GrabHandleVolume 的 BoxCollider。
3. Grabbable：One Grab Transformer = 同根 GrabFreeTransformer；Two Grab Transformer = None；TargetTransform = 根 Transform；Rigidbody = 根 Rigidbody；Max Grab Points = 1；Kinematic While Selected 开启；Throw When Unselected 关闭。
4. GrabInteractable：Pointable Element = 同根 Grabbable；Rigidbody = 根 Rigidbody；GrabSource = GrabPoint；Use Closest Point = false；Max Selecting Interactors = 1；Interactor Filters 列表只有同根 BagGripFilter。
5. BagGripFilter 的 Bag = 同根 ShoppingBag。不要添加 HandGrabInteractable、DistanceGrabInteractable、RayInteractable 或新的 Selector。
6. 根 BoxCollider 默认尺寸 (0.45,0.40,0.22)，中心 (0,0.20,0)，非 Trigger；提手 Trigger 默认中心 (0,0.52,0)，尺寸 (0.2675,0.04,0.04)。Start 前会由当前配置重算。
7. 白模使用内置 Cube 网格和 Standard 材质。若出现 Missing Script／粉色材质／引用丢失，先修复导入与环境，不能把静态检查视为 Unity 导入通过。

## 独立测试场景的使用

`ShoppingBag_Test.unity` 只含地面、支架、一个袋 Prefab 实例和测试控制器。它不含相机、Rig、移动脚本、EventSystem 或世界菜单，因此单独打开运行不会出现可操作的 VR 玩家。这是有意的内容场景。

建议在 **Unity Editor 手动**完成：

1. 首先解决当前项目已有的依赖问题并确认 Console 无阻塞编译错误。不要升级 Unity／SDK。
2. 打开现有 `Assets/Scenes/SampleScene.unity`，再从 Project 对 `Assets/ShoppingBag/ShoppingBag_Test.unity` 选择 **Open Scene Additive**。只使用 SampleScene 已有的 Rig。
3. 保持已有 SampleScene 为 Active Scene，以复用其光照设置。不要运行场景构建／迁移菜单。不要保存 SampleScene 的临时调整。
4. 检查左右 ControllerGrabInteractor 各有有效 Rigidbody，子树有 Trigger Collider；各自 `_selector` 指向该子树内唯一 GripButtonSelector，按钮只有 GripButton。保留原接线，不新增完整 Rig。
5. 保持所有测试内容单位缩放。袋底默认在 (0,0.75,1.2)，放在 0.75 m 高支架上。若现有玩家出生点不同，只在测试场景中调整测试内容整体位置；避免与原场景地板／Cube 重叠。
6. 选择 ShoppingBag_TestControls，确认 Bag 引用指向场景中的 ShoppingBag。Game View 中用鼠标选择尺寸；或在该组件菜单选择 Test size/Small、Medium、Large。
7. Quest Link 实测时，头显内只测试已有左右 Grip。尺寸切换可由 Editor 操作者在 Game View 点选；该桌面面板不是头显菜单。独立 APK 的头显尺寸 UI 留待菜单阶段，不宣称已完成。
8. 不把测试场景加入正式 Build Settings，不保存或修改正式公寓，不复制另一套相机或移动系统。

若 SampleScene 在本机不能正常抓取现有 Cube，应先解决 SDK／Rig 的基础问题，不要用购物袋的成功或失败代替基础 Rig 验证。后续详细公寓已正确接线的左右 ControllerGrabInteractor 可直接复用；本阶段没有将正式场景拉入当前分支。

## Quest 3 手动验收流程（尚未执行）

1. 先验证现有 Cube 与追踪，再靠近袋把，左 Grip 按住拾取、松开放下；重复 10 次。换右手重复。
2. 左手持袋时右 Grip 尝试抓取，袋仍由左手持有；反向测试，不得抢占、双持或缩放。
3. 只扣 Trigger、按 A／B／推动摇杆不能抓起袋子；距离较远按 Grip 不能吸取。
4. 放下后依次切换 Small／Medium／Large，检查袋身尺寸、Collider 与提手位置；持袋时调用同一尺寸入口，应拒绝。
5. 确认释放后恢复动态重力并落在支架／地面，之后能再次抓取；袋不会一直保持运动学。
6. 观察 HoldStateChanged 与 HeldHand：左右手识别对应；释放清空。禁用／重新启用整个袋对象后可再次拾取。
7. 在测试场景摆放有实体 Collider 的相同通道，固定路线与姿态比较三尺寸。阶段 A 不含碰撞反馈、震动和阻止玩家通行逻辑，不把它们列为已通过。

## 后续兼容约定

- 菜单：接现有“我的物品”页面调用 TrySetSize；持袋时显示不可修改。
- Gate：驱动 SetAcquisitionAllowed；实现菜单／教学／传送优先级和每只手 Grip 松开重新武装。禁止把新脚本挂到 VRPlayer 根组件列表，避免触发现有移动的重复控制器检查。
- ContactSensor：新增独立的袋身 Trigger，尺寸从 BodySize 获取；**不改变现有 handle trigger 的职责**。持袋时袋身实体 Collider 关闭，避免阻挡现有家具／传送射线；接触检测不得依赖它继续触发。
- Haptics：读 HeldController／HeldHand；在放下、失焦和禁用时停止震动。
- AcquisitionAllowed 默认 true，仅适用于隔离测试。正式菜单尚未接入，不能宣称菜单期间抓取已被阻止。
- 单手独占针对本袋；阶段 A 不实现多个袋实例之间的全局手部占用策略。

## 验证结果与限制

已执行：资源 GUID／fileID 引用静态检查、三尺寸默认数据检查、近抓与单选配置检查、无相机测试场景检查；使用现有 Unity 引用和缓存 SDK 程序集独立编译运行时脚本及 Editor 测试脚本。

未执行：启动 Unity、完整项目编译／导入、Test Runner 测试执行、Play 模式物理验证、Quest 3 测试。独立编译不能证明 YAML 被 Unity 正确导入，也不能证明真实手柄抓取成功。

当前 manifest 中三项 Meta XR 包仍指向不存在的 E:/edge 本地 tgz；本轮按约定没有修改。已有 Library 缓存用于源码核实与编译，但新机器／重新导入仍可能失败。独立运行时编译会出现 CS0649 Inspector 序列化字段未显式赋值警告，字段已在 Prefab 中接线；没有据此宣称 Unity Console 无错误。

测试场景、Prefab 均为文本序列化资源，尚未由 Editor 重新保存。Unity Inspector 检查是交付验收必需步骤。没有升级依赖，没有修改 SDK 缓存。

下一阶段：先整合基础公寓与依赖，再运行上述验收；随后接菜单和 Gate，最后开发接触反馈与震动。若要求大袋实际限制玩家通行，需要额外与唯一移动所有者协作，不在阶段 A 伪造结果。
