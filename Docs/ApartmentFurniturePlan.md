# Apartment Phase 2 — M1-B Furniture Plan

本阶段为 B1（布局数据与材质）+ B2（独立家具场景）+ B3（只读验证工具）。所有家具尺寸和定位均为已批准的灰盒近似，不是建筑事实、家具产品规格或无障碍合规结论。

## 来源、基线与完成状态

- 主参考图：`Docs/References/ApartmentFloorPlan.png`，已实际查看；不重命名、不覆盖。
- 基线：M1-A Commit `7e8c817`、`Assets/Scenes/ApartmentPhase2.unity`、`Assets/Apartment/ApartmentGrayboxLayout.json` 与 `Docs/ApartmentGeometryAssumptions.md`。
- 版本：Unity 6000.6.4f1，现有 Built-in Render Pipeline；本阶段不改变包、XR、ProjectSettings 或渲染管线。
- 新场景：`Assets/Scenes/ApartmentPhase2_Furnished.unity`，已实际生成原生场景文件。由原场景的序列化内容复制并追加家具及独立检查点；原场景文件没有修改。
- 本阶段未启动 Unity：新工具未经过本次 Unity Editor 编译、新场景未由助手在 Editor 打开、未执行 Physics 或 VR 测试，人工观感检查待完成。
- 已完成的离线检查见下表；离线检查不替代 Unity 场景反序列化、完整项目编译或运行时碰撞。
- 不实现购物袋、问题标记、反馈表、移动脚本或 OpenXR 修复；不新增 XR Rig、Rigidbody 或运行时 MonoBehaviour。

## 坐标与朝向

1 Unity Unit = 1m；入口门槛中心为原点；Y 向上，图右为 +X、图上为 +Z。图上方向不是经验证的地理北向。
标称 9.6 × 7.5m = 72㎡ 是外部参考框架乘积，不能当作净使用面积。本阶段不改变 M1-A 的面积口径或固定结构。
下表坐标是家具底部中心的世界坐标，尺寸为世界 X × Y × Z 的完整外观包围盒。
JSON 的 `nodes.position` 是父级局部坐标；家具根对象是底部中心，房间分组位置为零。
根对象 Rotation 为零、Scale 为一；朝向直接体现在靠背、床枕、柜门等子几何的位置中，不用根旋转偷偷交换尺寸。
front 表示家具正面或座椅朝向；head 表示床头，open 表示淋浴入口。圆茶几不要求固定朝向。

## 已批准家具参数

| 对象 | 区域 | 世界底部中心 X,Y,Z（m） | 尺寸 X×Y×Z（m） | 朝向 |
|---|---|---|---|---|
| EntryShoeCabinet | Entrance | (-0.22, 0, 1.3) | 0.3 × 1.1 × 0.6 | front +X |
| SofaWestModule | LivingRoom | (0.45, 0, 3.45) | 0.9 × 0.9 × 2.1 | front +X |
| SofaNorthReturn | LivingRoom | (1.425, 0, 4.075) | 1.05 × 0.9 × 0.85 | front -Z |
| CoffeeTable | LivingRoom | (1.5, 0, 3.05) | 0.7 × 0.4 × 0.7 | round |
| Armchair | LivingRoom | (1.8, 0, 2.1) | 0.75 × 0.9 × 0.75 | front +Z |
| DiningTable | DiningArea | (0.55, 0, 6) | 1.2 × 0.75 × 0.8 | long +X |
| DiningChairSouth01 | DiningArea | (0.27, 0, 5.45) | 0.5 × 0.85 × 0.5 | front +Z |
| DiningChairSouth02 | DiningArea | (0.83, 0, 5.45) | 0.5 × 0.85 × 0.5 | front +Z |
| DiningChairNorth01 | DiningArea | (0.27, 0, 6.55) | 0.5 × 0.85 × 0.5 | front -Z |
| DiningChairNorth02 | DiningArea | (0.83, 0, 6.55) | 0.5 × 0.85 × 0.5 | front -Z |
| CounterNorth | Kitchen | (-2.575, 0, 6.75) | 1.95 × 0.9 × 0.6 | front -Z |
| CounterWest | Kitchen | (-3.25, 0, 5.925) | 0.6 × 0.9 × 1.05 | front +X |
| CounterSouth | Kitchen | (-2.65, 0, 5.1) | 1.8 × 0.9 × 0.6 | front +Z |
| Fridge | Kitchen | (-1.275, 0, 6.725) | 0.65 × 1.8 × 0.65 | front -Z |
| Bed01 | Bedroom1 | (-2.85, 0, 1) | 2 × 0.6 × 1.2 | head -X |
| Wardrobe01 | Bedroom1 | (-2.55, 0, 2.575) | 1.2 × 1.9 × 0.55 | front -Z |
| Bed02 | Bedroom2 | (4.05, 0, 3.7) | 2 × 0.6 × 1.5 | head +X |
| Wardrobe02 | Bedroom2 | (3.8, 0, 0.6) | 2.3 × 1.9 × 0.6 | front +Z |
| ShowerArea | Bathroom | (-3.075, 0, 3.75) | 0.85 × 2 × 1.2 | open +X |
| Toilet | Bathroom | (-2.1, 0, 4.2) | 0.4 × 0.75 × 0.65 | front -Z |
| Vanity | Bathroom | (-1.35, 0, 4.325) | 0.65 × 0.85 × 0.45 | front -Z |
| BalconyChair | Balcony | (4.45, 0, 6.35) | 0.7 × 0.9 × 0.75 | front -Z |
| PlantPot | Balcony | (1.85, 0, 6.75) | 0.35 × 0.6 × 0.35 | none |

三段橱柜保持实际尺寸，不为了通行而缩小。水槽视觉占位位于北侧台面，炉灶占位位于西侧台面，冰箱在东北；均不实现电器功能。
水槽/炉灶用台面内侧薄片与圆形标记表示，最大高度仍为 0.90m；它们是台面识别细节，没有额外阻挡通路的 Collider。

## 修订与原图偏差

- Bedroom 1：床头保持图中朝左（-X）；床仍放在西南凹入区域。衣柜按批准修订北移至 Z=2.575m，并面向 -Z。原图衣柜在南侧，北移是针对当前固定灰盒净深的明确设计偏差，不能描述成忠实复刻。
- Bedroom 2：床头保持图中朝右（+X），衣柜仍在南侧、面向 +Z。
- Dining Area：餐桌采用已批准的长边沿 X 排布；原图长边沿图上/下。四把餐椅分布在南北侧，默认收拢，各有 0.10m 的平面范围进入桌面投影。座面顶 Y=0.45m、桌面底 Y=0.70m，座背与桌边、桌腿实际分块不穿插，不能把投影重叠误当成三维实体重叠。
- 客厅 L 型沙发采用两段互相接触的模块，内角没有整块阻挡 Collider。单椅按批准方案面向 +Z，没有复刻图片中的斜放角度。
- 茶几为 0.70m 圆形台面，Collider 用保守方形薄板和四腿；圆台边角的静态检查偏保守，人工检查需了解这一差异。
- 淋浴区只包含托盘、西侧和北侧边界，东侧开放；没有门片、透明玻璃或真实五金。
- 窗洞、门洞、隔墙、房间净尺寸仍沿用 M1-A 的近似。门扇和柜门开启包络尚未建模。

## 场景分组、材质与 Collider

```
ApartmentRoot
├─ Structure                       M1-A 原结构
├─ Rooms                           M1-A 八区域地面
├─ Furniture
│  └─ M1B
│     ├─ Entrance
│     ├─ LivingRoom / Sofa_L01
│     ├─ DiningArea
│     ├─ Kitchen
│     ├─ Bedroom1
│     ├─ Bedroom2
│     ├─ Bathroom
│     └─ Balcony
├─ Anchors
│  ├─ 原 M1-A 路线及出生点
│  └─ M1B                           独立核心路线、三个房间支路、阳台支路、出生点候选
└─ AssumptionMarkers
   ├─ 原 M1-A 门洞与尺寸假设
   └─ M1B                           狭窄处、拉椅限制标记，无 Collider
```

四种新材质均使用现有 Built-in Standard Shader，颜色为不透明的木色、布面灰蓝、台面浅灰和设备白色，无新增贴图或付费依赖。
新家具包含 23 个家具根对象、75 个分块 BoxCollider；全部新节点 177 个，其中 136 个可见几何体包含家具细节和无 Collider 检查标记。
柜体、冰箱、床架、床垫采用实体块；沙发分座体、靠背及扶手；餐桌和餐椅分桌面/座面、靠背及四腿。
淋浴只对托盘和两片屏板设 Collider，不填满淋浴空间。桌下、沙发内角没有总包围盒 Collider。
咖啡圆台和灶圈使用 Unity 原生 Cylinder；其余使用 Cube。Cylinder 自动 Collider 被移除，茶几台面使用独立薄 BoxCollider。
装饰性门板、软垫细节、枕头、台面标识和路线标记不添加重复 Collider。静态家具不加 Rigidbody，不改变 Layer 或物理配置。
Collider 不带 Trigger；与墙面接触允许，跨家具或家具穿墙的正体积重叠是建模错误。
验证工具只在活动家具场景内计算变换后的 BoxCollider 几何，不调用 Physics 查询，不把其他 Additive 场景的 Collider 当作本场景障碍。

## 独立路线与出生点

原 M1-A JSON 和原 Anchors 对象均保留，供空场基线与家具加入后的对照。
M1-B 核心路线 11 段，沿入口、客厅西侧、餐区西侧进入厨房。没有移动墙体或修改门洞。
路线是审查候选，不是导航系统；没有强制用户沿线移动，也没有接入 XR Rig。

| 新核心检查点 | 世界地面坐标（m） |
|---|---|
| 00_Threshold | (0, 0, 0) |
| 01_EntryTurn01 | (0, 0, 0.6) |
| 02_EntryTurn02 | (0.45, 0, 0.6) |
| 03_SpawnCandidate | (0.45, 0, 0.7) |
| 04_EntranceReview | (0.65, 0, 1.15) |
| 05_LivingSouth | (0.65, 0, 2.05) |
| 06_LivingWest | (-0.4, 0, 2.05) |
| 07_DiningWest | (-0.4, 0, 5.25) |
| 08_KitchenOutside | (-0.45, 0, 5.2) |
| 09_KitchenInside | (-1.35, 0, 5.2) |
| 10_KitchenTurn | (-1.35, 0, 5.85) |
| 11_KitchenReview | (-2.1, 0, 5.85) |

- 出生点候选：`(0.45,0,0.70)`。绿色标记中心 Y=0.04m；未来 XR Rig 根位置应使用地面 Y=0，不使用标记中心高度。
- 阳台支路：`(-0.40,0,4.85) → (1.90,0,4.85) → (1.90,0,5.95)`。
- 次卧支路：`(-0.40,0,2.35) → (-1.40,0,2.35)`。
- 卫生间支路：`(-0.40,0,3.55) → (-1.40,0,3.55)`。
- 主卧支路：`(0.65,0,1.30) → (2.95,0,1.30)`。

## 保留的设计限制

| 位置 | 当前近似净空/限制 | 解释 |
|---|---|---|
| 沙发西侧 | 0.75m | 0.60m 占位体可采样通过，0.80m 会受阻；不扩大通道 |
| 餐桌西侧 | 0.70m | 核心路线选择此侧，但没有 0.80m 余量 |
| 次卧床与衣柜 | 0.70m | 保留床柜间隙，不宣称正常换衣或开柜可用 |
| 主卧床脚至西墙 | 0.55m | 无法供 0.60m 占位体正常绕床；南侧主要入口区域仍保留 |
| 客厅沙发至茶几 | 两侧各约 0.25m | 这是坐席间局部间隙，不作为核心路线 |
| 单椅至茶几 | 约 0.225m | 使用体验值得评价；不改小家具 |
| 主卧门外单椅 | 0.60m 直线占位体边缘至椅子约 0.125m；0.80m 约 0.025m | 门洞本身仍 0.80m、宽占位体零余量；转身/门片尚未验证 |
| 厨房中部 | 南北台面之间约 1.05m | 门内南柜端与东墙形成约 0.80m 入口颈部；宽占位体接触不是安全通行 |
| 北侧餐椅收拢 | 椅背至北墙只有 0.25m | 正常拉出使用空间不足，不能宣称四人正常就餐已经满足 |
| 北侧餐椅虚拟拉出 0.20m | 只剩 0.05m 墙前余量 | 没有证明坐下/起立可用 |
| 北侧餐椅虚拟拉出 0.30m | 穿北墙约 0.05m | 无效测试姿态；不将该姿态保存进场景 |
| 南侧餐椅虚拟拉出 0.30m | 沙发与椅子之间约 0.40m | 会阻挡 0.60m 阳台支路；默认场景保持收拢，限制需评价 |

以上为批准布局的推导值，不是实测或规范要求。卧室门扇/衣柜开启、冰箱开门、厨房转身和餐椅实际人体使用仍待核实。
不以零失败为目标修改墙体、洞口或家具尺寸。

## B3 验证方法与离线结果

验证分为：MODELLING ERROR（丢失对象/引用、尺寸变更、穿墙/穿家具、堵门、无地面支撑）、DESIGN LIMIT（空间狭窄、零余量、旧路线受家具阻挡、拉椅阻挡阳台）、UNVERIFIED（尚未进行的实际测试）。
占位体分别宽 0.60m、0.80m，X/Z 等宽、Y=[0.10,1.80]，沿路线每段采样步长不超过 0.04m。与障碍接触记录为 zeroMargin，不能当作充分净空；正体积穿插记录 blocked。
检测采用已生成的分块 Collider 轴对齐包围盒，不在场景添加占位体 Collider；家具完整占地仅用于检查地面支撑。
虚拟拉椅只计算复制的 Bounds 偏移，不修改 Transform、不实例化物体、不保存场景。
采样不是连续扫掠，未验证人体旋转、真实 CharacterController 或购物袋；浮点运算可能导致 Unity 采样点数量略有变化。
路线地面支撑只检查采样中心，不代表占位体完整脚底受到支撑；入口门槛外尚未建立走廊。卫生间与卧室支路仅检查门口进入区域，不证明全部洁具、床侧或衣柜均可正常使用。

| 离线几何检查（非 Unity/Physics） | 0.60m | 0.80m |
|---|---|---|
| 原 M1-A 路线，空场结构 | 201 样本，阻挡 0、接触 0 | 阻挡 1、接触 28 |
| 原 M1-A 路线，家具场景对照 | 阻挡 119、接触 3 | 阻挡 140、接触 51 |
| 新 M1-B 核心路线 | 245 样本，阻挡 0、接触 0 | 阻挡 113、接触 152 |
| 阳台支路，餐椅收拢 | 88 样本，阻挡 0、接触 0 | 阻挡 61、接触 11 |
| 阳台支路，北椅拉 0.20m/南椅拉 0.30m | 阻挡 41 | 仍受限 |
| 次卧/卫生间/主卧门口支路 | 阻挡 0、接触 0 | 没有正体积阻挡，但门框零余量接触，不能宣称通过 |

离线检查所有路线采样点均有地面支撑，23 件家具完整占地均有支撑；75 个家具 BoxCollider 未发现与固定结构、其他家具的非预期正体积穿插。
相同家具内部的拼接和局部重叠允许；例如淋浴屏板与托盘交接，不会被误报为跨家具穿插。
原路线加入家具后的阻挡是对照结果，不修改原路线来掩盖问题。0.80m 已失败/零余量，不声称整体通行验证通过。

## Unity 中的操作与验收

1. 由用户在 Unity 6000.6.4f1 打开项目，等待导入与编译。不要用旧 TwoRoomSceneBuilder；若出现新脚本编译错误，记录完整 Console 文本并停止。OpenXR Editor 既有 NullReferenceException 本阶段不处理。
2. Project 中双击 `Assets/Scenes/ApartmentPhase2_Furnished.unity`，单独打开。若当前场景有未保存修改，先取消切换并保留改动；不要丢弃他人场景工作。
3. Hierarchy 展开 `ApartmentRoot/Furniture/M1B`。选择家具根对象按 F 聚焦，查看底部中心坐标；展开子物体查看 Cube Scale，完整尺寸查本表和 JSON。
4. 俯视检查八区关系和固定门洞；切换眼高视角检查柜、床、沙发等是否容易辨认。比较原 M1-A 场景时不要把两份场景重叠着当一份使用。
5. 使用 Physics Debugger 或 Collider 的 Edit Collider 视图查看桌面/桌腿、座面/椅背/椅腿、沙发两模块和淋浴边界。桌下、L 型内角、淋浴东侧入口应保持空隙；无隐藏全包围 Collider。
6. 在活动家具场景执行：`Apartment Phase 2 → M1-B → Validate Active Furnished (Read Only)`。
   - 预期：`furniture=23; openings=6; modellingErrors=0`；同时出现已知设计限制的 Warning。
   - 新核心路线 0.60m 应无正体积阻挡、无零余量接触、无缺失地面；0.80m 的阻挡/零余量应保留。
   - 原路线家具对照受阻、南椅拉出阻挡阳台、北椅拉 0.30m 穿墙属于报告中的限制/无效虚拟姿态，不代表脚本偷偷调整布局。
   - 验证不会保存资产、移动物体、修改 Collider 或使场景从干净变为脏状态。若场景本来已脏，不自动清除。
7. 沿 `Anchors/M1B/M1B_Core` 和 `M1B_Balcony` 逐点检查。人工重点观察卫生间门外、主卧门外、厨房门内柜端，以及椅子拉出后的阳台支路。
8. 主卧床脚、次卧床柜、餐椅墙前净空应记录为体验问题。当前没有门片或真实人体；不对这些区域给出舒适/无障碍结论。
9. 不需要 Play Mode，也不要添加 XR Rig。实际移动与 VR 体验另行批准后开展。

独立安全生成菜单为 `Apartment Phase 2 → M1-B → Create Furnished Copy (Never Overwrite)`。
本次场景已生成，因此执行该菜单应提示目标已存在并停止；请直接打开现有新场景。该菜单用于目标尚不存在时从磁盘原场景复制并添加同一 JSON 家具。
菜单先检查源图、源布局、源场景及 meta、材料导入状态、目标路径；目标 scene 或 meta 已存在一律停止，不自动改名或覆盖。
生成仅保存指定新场景，并恢复原活动场景；不保存其他已打开的场景，不更改 Build Settings，不调用 SaveAssets 或批量刷新。
若生成中发生异常，保留新生成的不完整目标并报告，工具不删除、不恢复或覆盖它；由用户决定后续处理。

## 新增文件与后续边界

本阶段只新增 16 个约定文件（含 Unity meta），不修改任何既有项目文件：

- `Assets/Apartment/Furniture.meta`
- `Assets/Apartment/Furniture/ApartmentFurnitureLayout.json` 及 `.meta`
- `Assets/Apartment/Materials/FurnitureWood.mat` 及 `.meta`
- `Assets/Apartment/Materials/FurnitureFabric.mat` 及 `.meta`
- `Assets/Apartment/Materials/FurnitureWorktop.mat` 及 `.meta`
- `Assets/Apartment/Materials/FurnitureAppliance.mat` 及 `.meta`
- `Assets/Scripts/Editor/ApartmentPhase2FurnitureBuilder.cs` 及 `.meta`
- `Assets/Scenes/ApartmentPhase2_Furnished.unity` 及 `.meta`
- `Docs/ApartmentFurniturePlan.md`

验收后再决定是否调整家具方案、增加门扇开启范围或进入后续 VR 集成。本次不执行 Git add/commit/push、不创建 PR，不实现购物袋、标记、反馈表或 OpenXR 修复。
