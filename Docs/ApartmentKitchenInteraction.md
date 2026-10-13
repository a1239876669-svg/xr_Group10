# 厨房柜门交互：Quest 3 新手验证与学习说明

现在的精细公寓副本为 `Apartment_Detailed_VR`，操作和迁移说明见 `Docs/ApartmentDetailedVR.md`。
下文保留原 `ApartmentPhase2_VR` 的实现记录；精细副本的六扇活动门位于西侧四扇和北侧两扇，
不是下文灰盒场景的北、西、南三段布局。阳台玻璃门按本次选择暂不开发开关功能。

## 本次验证结果（2026-10-13）

已在 Unity 6000.6.4f1 中编译、安装并保存 VR 场景。
柜门检查 140 项通过；原有传送/连续转向回归检查 97 项通过，共 237 项。
包含六扇门分别打开到 90 度并关闭、玩家身体阻挡、头部保护、暂停后重新按键、相邻门干涉、
射线遮挡/距离/手柄位于实体内部、共享材质保护、中文提示网格及实际普通渲染。
重复运行安装前后 VR 场景文件哈希一致，没有重复对象或再次烘焙。
原建筑场景、原家具场景、布局数据、既有移动脚本和 Packages 内容均与本轮开始时一致。
详细报告在 Logs；报告中 questLinkVerified=false，仍须下文的 Air Link 真机验收。

脚本重新编译时仍会出现项目原有的 OpenXR FeatureHelpers.RefreshAllFeatureInfo 空引用异常。
异常堆栈来自现有 OpenXR 包，本轮没有改该包或 XR 配置；这不等于柜门脚本有编译错误，也未作为已修复项。

## 操作

在 `ApartmentPhase2_VR` 场景，通过 Air Link 进入 Play 后，走到厨房，用右手柄指向柜门。
高亮和“按右扳机打开”出现后，扣一下右手**食指扳机**，门开始打开。打开后再扣一下关闭。
不是 A 键，也不是握把侧面的抓握键。A 键仍然用于传送，左摇杆走路、右摇杆连续转向不变。
有效距离默认 2 米。按住扳机不会重复操作；按住 A 时家具射线暂时隐藏。

本版只有六扇门可操作：北侧 `Door_1`、`Door_2`，西侧 `Door_S`、`Door_N`，南侧 `Door_1`、`Door_2`。
北侧、南侧的 `Door_0` 正对西侧柜体，按确认的方案保留为固定门板。衣柜、冰箱和其他家具暂未安装交互。

门被玩家或其他门挡住时停下。先让开（或关闭相邻门），然后松开并重新扣扳机，继续上次方向。
打开系统菜单、追踪失效或手柄断连时，运动中的门暂停。恢复后也需要重新扣扳机。
重新进入 Play，所有六扇活动门回到关闭状态；本版不保存开关进度。

## 在 Inspector 看实现

1. 退出 Play，展开 `VRPlayer/FurnitureInteractor`。
2. `Apartment Furniture Interactor` 的 Locomotion 引用现有移动组件，Hint Font 是中文字体，Max Distance 是 2 米。
3. 展开 `ApartmentRoot/Furniture/M1B/Kitchen/CounterNorth`。
4. 选中 `Door_1_Hinge`：`Apartment Cabinet Door` 的 Open Angle 是正负 90 度，Degrees Per Second 默认 90。
   只改速度可调整开门快慢；符号决定方向，不建议随意改变。速度支持 15–180 度/秒。
5. 其下的 `Door_1` 保留原门板模型、材质和关闭位置，新增 Box Collider。

Pivot 是门的旋转支点，类似现实中的铰链。若直接旋转门板自身，门会绕中心转，看起来像旋转牌子。
门在无缩放的支点下旋转；模型尺寸放在门板子物体上，避免非均匀缩放把旋转扭曲。

Rigidbody 开启 Is Kinematic、关闭 Use Gravity：门不会掉落或自由晃动，角度由脚本决定。
运动学刚体不会自动保证“撞到墙就停止”，所以脚本按每步最多 1.5 毫米边缘位移预先检查碰撞，再改变角度。
身体碰撞使用现有 CharacterController，另检查真实头部附近 12 厘米球形空间。
这不能替代 Quest 的现实安全边界：真实身体仍可强行走穿虚拟物体，请保持周围安全。

`CabinetBody` 保留原对象但禁用 Renderer 和 Collider。`InteractionShell` 是空心柜体：18 毫米的底、顶、背和侧板，
侧板前缘留 3 厘米铰链活动间隙。柜体位置、完整占地、台面和原门板大小不变。
没有保留实心碰撞体，否则门虽然打开了，里面仍会被整块方盒挡住。

活动门的 NavMesh Modifier 设置 Ignore From Build；柜体下的 CabinetNoTeleport 标记占地不可传送。
固定地图不记录门的运动位置；现有传送脚本会用当前实体碰撞体检查路线与落点。
原有导航地图保留，本次另外保存 FurnitureNav 地图。不要手动把门板设为 TeleportFloor。

交互射线会寻找最近的非玩家实体，而不是只寻找“可交互层”，所以不能穿墙点击后面的门。
按键使用 0.75 按下 / 0.25 松开的两个阈值，避免扳机轻微抖动导致重复触发。
脚本放在玩家子物体上，不竞争玩家位置控制，也不再增加第二个 Camera Rig。

中文提示用 Noto Sans SC，源字体和许可证在 Assets/Apartment/Fonts。提示图集仅预生成本版所需字形及 ASCII。
以后增加新提示文字，要更新 RequiredCharacters 并生成新字体图集，不能仅添加未包含的汉字。
高亮通过 MaterialPropertyBlock 临时覆盖颜色，移开后恢复，不修改其他门共用的材质。

## 菜单与恢复

- 安装：`Apartment Phase 2 → M3 Furniture → Install Kitchen Cabinet Interaction`（Ctrl+Alt+Shift+I）。
  仅作用于已保存、非 Play、单独打开的 VR 场景；已安装时只验证，不重复创建或重新烘焙。
- 只读配置检查：`Validate Kitchen Interaction (Read Only)`。
- 自动检查：`Run Kitchen Interaction Checks`（Ctrl+Alt+Shift+K）。临时几何在未保存的独立测试场景中运行，结束后关闭。
- 与旧功能一起验证：`M2-B3 → Run Teleport Checks`。
- 输出：`Logs/FurnitureChecks.json`、`Logs/FurnitureHintPreview.png`、`Logs/TeleportChecks.json`。
- 仅菜单安装的场景改动支持 Unity Undo；保存前后都保留原实心对象。外部备份位于
  `C:/Users/fzj/Documents/Codex/Backups/quest-furniture-20261013/ApartmentPhase2_VR.unity`。
  恢复整个备份会同时撤销此后的场景修改，须先备份新的工作。

## 真机验收

先停用 Meta XR Simulator，Quest 3 通过 Air Link 连接电脑。场景 Debug Mode 必须关闭。
戴上头显，确认头部追踪、左摇杆走路、右摇杆转向和 A 键传送正常，再逐扇验证柜门。

1. 六扇门分别开到 90 度并关闭；开门后可见空柜内部，两扇转角门保持固定。
2. 靠近、远离、遮挡、指向固定门板，检查是否只在有效目标上显示提示。
3. 按住扳机、轻扣未达到阈值、按着扳机换门，都不能连续触发。
4. 开多个相邻门，发生真实空间干涉时应停住，不要求所有门同时打开。
5. 站在门的开关路径上，门应停住；让开再按时继续原方向，不推动玩家。
6. 按 A 瞄准时再扣扳机，柜门不能开始新的开关；传送后保持扣住扳机也不能误操作。
7. 打开系统菜单再返回，门保持暂停，重新松开并按扳机才能继续。
8. 开门后尝试走路与传送：不能穿过门，也不能站进柜体。关门后原有空地可恢复使用。
9. 不再指向门时，高亮恢复原色。中文清晰，不显示方框，提示不遮满视野。

自动检查只覆盖代码状态、几何碰撞和普通中文渲染，不代表已经验证 Quest 按键、双眼显示或舒适性。
本次不修改源建筑/家具场景、布局 JSON、已有移动脚本、Packages 或 GitHub。
