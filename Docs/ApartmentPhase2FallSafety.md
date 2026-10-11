# M2-B3：入口外侧安全边界

适用 Unity：**6000.6.4f1**。仅用于 `Assets/Scenes/ApartmentPhase2_VR.unity` 的原型测试安全；**不是建筑墙体、真实入口结构或无障碍/疏散验证**。

## 当前交付状态

本轮仅新增独立 Editor Installer 与本文档。**没有执行菜单、修改场景 YAML、生成边界、启动/关闭 Unity或执行 Git 写操作。** 已有移动脚本、M1-A/M1-B/VR 场景、Builder、布局、材料和 XR 配置保持不变。脚本 `.meta` 由 Unity 后续正常导入时自动生成，不手工伪造。

Installer：`Assets/Scripts/Editor/ApartmentPhase2EntranceSafetyInstaller.cs`。不含运行时行为、自动重生、自动安装或自动打开场景。

## 原因与批准的参数

入口地板及门槛均已有启用、非 Trigger 的 Collider。门槛范围为 X `[-0.40,0.40]`、Z `[-0.15,0.15]`，顶面 Y=0；门外没有接续地面。玩家穿过入口后失去支撑，现有移动脚本继续施加重力且不提供重生。

仅新增两个对象，作为独立场景根分组，不插入 ApartmentRoot：

```text
SimulationSafety                     position=(0,0,0), rotation=0, scale=1
└── EntranceExteriorBoundary          world position=(0,1.40,-0.40), rotation=0, scale=1
    └── BoxCollider                   center=(0,0,0), size=(1.20,2.80,0.10)
```

边界仅包含 Transform + BoxCollider；无 MeshRenderer、MeshFilter、Rigidbody、Trigger、脚本或子对象。Collider 使用玩家的碰撞层，启用且没有 include/exclude 层覆盖。

世界范围：X `[-0.60,0.60]`、Y `[0,2.80]`、Z `[-0.45,-0.35]`。原门洞厚度范围 Z `[-0.15,0.15]`，边界与它相隔 0.20m，未移动或扩大门洞。默认半径0.30、Skin Width0.03时，停止位置的保守估算为胶囊中心 Z≈−0.08，距门槛地板外缘约0.07m。**这是静态估算，不是 PhysX 的通过结果**。

## 安装前提与保护

安装菜单会在以下情况停止：版本不匹配，处于 Play Mode/编译/导入中，活动场景不是 VR 场景或加载了多个场景，存在未保存修改，源场景/布局/移动脚本及其 metadata 缺失，移动依赖未成功导入，玩家组件异常，入口基线 Collider 有偏差，或任何位置存在同名安全分组/边界（包括 inactive 对象）。不复用或覆盖已有分组，也不自动保存/丢弃原有修改。

安装前后执行以下检查：

- 对 Assets、Docs、Packages、ProjectSettings 的文件集合及 SHA-256 进行比较，仅允许 VR 场景文件内容变化；其 `.meta`、源场景及其他资产仍受保护。直接读取文件，不运行 Git/LFS。
- 安装过程中持有两个源公寓场景及其 `.meta` 的只读文件锁；权限或锁失败时停止。
- 对所有原有场景 GameObject 与组件记录序列化内容，使用 Unity 6.6 的 EntityId 关联；新增对象之外的记录必须不变。因此原家具、地面、门框、玩家参数和路线锚点不会由工具调整。
- 若场景组件实现序列化回调接口，工具在读取序列化快照前停止，避免为检查而调用可能修改状态的未知回调。
- 检查新边界层级、坐标、尺寸、Collider、重复对象、与现有 Collider 包围盒及出生胶囊的重叠、碰撞层、地板边缘余量，以及对原有路线的新增阻挡。

工具只调用 `SaveScene` 保存 VR 场景，不调用 SaveAssets/Refresh/Fix，也不修改 SDK 或项目配置。文件与对象检查若失败，会停止并报告；不会通过恢复、删除或重写其他资产来“修复”检查。

保存前失败会尝试 Undo 本次新增对象/组件。开始保存后失败或保存后检查失败，保留现场供人工审查，不自动恢复磁盘文件；不要直接重复运行安装。Unity 的保存不是此工具能保证的文件事务。

## 手动运行（需批准后执行）

1. 等待 Unity 导入新脚本并生成 `.meta`，确认没有编译错误。保持当前正确的 XR 配置，不点击 Fix/Fix All。
2. 手动打开且只加载 `ApartmentPhase2_VR.unity`。退出 Play Mode，检查并自行处理有意保留的未保存修改。
3. 执行 **Apartment Phase 2 → M2-B3 → Install Entrance Safety Boundary (No Overwrite)**。
4. 阅读人工确认对话框，接受仅保存 VR 场景及入口向外通行限制后，点击 **Install and save VR scene**；Cancel 不新增对象或保存资产。
5. 检查 Hierarchy/Inspector：一个 SimulationSafety 根对象，一个 EntranceExteriorBoundary 子对象；Transform Scale=1，Collider Size 使用上表尺寸；无 Renderer/Rigidbody。
6. 执行 **Apartment Phase 2 → M2-B3 → Validate Entrance Safety Boundary (Read Only)**。验证不保存场景，不修改对象、脏标记或资产；允许检查当前未保存的内存状态，但会明确提示。

预期静态汇总：`safetyRoots=1, boundaries=1, existingColliderOverlaps=0, routes=6, routeHits0.60=0, routeHits0.80=1, errors=0`；默认参数的 `estimatedFootMargin≈0.070m`。允许的设计限制警告见下节。实际结果仍须由 Unity 执行菜单确认，以上不是本轮已取得的 Editor 结果。

安装是一次性操作。重复执行必须停止，不能重新创建或覆盖。原 M2-B2 的安装菜单也不用于更新这个边界。

## 路线影响与已知限制

路线检查只评价**新边界增加的阻挡**，不重新判定现有家具、拉椅空间等限制。读取原 M1-A route 与五条 M1-B routes（含阳台支路），不写入它们。按0.04m间隔采样，使用高度1.70m、中心Y=0.95m的垂直胶囊，与候选边界进行解析距离检查，不执行 Physics 模拟。

本轮独立离线核算结果：

- 六条记录路线的0.60m占位体均不与新边界相交。
- 0.80m占位体在 M1B_Core 的门槛中心 Z=0 和相邻 Z=0.04 样本处相交。其他记录路线不与新边界相交。
- 这是入口边缘/向外退出的新增限制，必须报告为 DESIGN LIMIT；不修改原路线点、门洞或边界坐标来消除它。不能声称全部0.80m路线已通过。

边界限制离开公寓原型，不能用来评价真实出口能否使用。真实头部追踪不会被 CharacterController 强制锁住，房间尺度越界仍需单独验证。边界只保护这一处，其他异常跌落没有恢复机制。

## Play Mode 验证与通过条件

1. 初始位置 `(0.45,0,0.70)`、Yaw=0时，直线后退应先遇到右门框；另行移动到门洞中央，才能测试外侧边界。不要误把门框碰撞当成边界验证。
2. 使用现有 Debug Mode：门洞中央持续按 S 后退至少10秒。观察 VRPlayer/胶囊底部高度与 Grounded，边界应在失去支撑前阻挡，不能持续下降。正常 Skin Width 接触不要求根Y每帧严格等于0。
3. 停在边界后按 W，必须能重新离开、返回入口；不能因胶囊阻挡检测被锁住。另测试侧移、斜向后退、门框附近贴边及转向。
4. 重新走通 Entrance → Living Room → Dining Area → Kitchen；检查卧室/卫生间门外、阳台支路，确保新增保护不改变这些区域的通行。
5. 保持0.60m默认胶囊作为本轮主要验证条件。若另测0.80m，单独记录门槛限制，不通过缩小胶囊、扩大门洞或移家具追求零警告。
6. 退出 Play Mode，重新打开 VR 场景，执行只读验证；确认只有两个新增安全对象、没有重复安装，原结构/家具/路线未变。
7. Quest Link 的真实追踪、头部偏移、边缘/斜向碰撞、卡顿帧和舒适性尚未验证。桌面通过不能替代实机通过。

若仍跌落、无法从边界走开或出现穿透，停止测试并记录位置、方向、胶囊参数及 Console；不自动移动已批准的边界或改动原几何。

## 本轮助手检查记录

脚本已使用本机安装的455个 Unity/项目程序集引用进行内存中的离线编译，结果为 **0错误、0警告**。Unity 6.6 已弃用的 GetInstanceID 未使用，采用已核实的 GetEntityId。

**尚未执行 Unity Editor 编译确认、Installer、只读验证菜单、Physics/Play Mode 或 Quest Link 测试。** 当前 VR 场景文件没有新增此边界；须由用户获批后执行上述菜单生成并保存。
