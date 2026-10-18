# 舒适度设置（设置 → 舒适度调节）

本轮在 B 键菜单的“设置”页下新增一个**子页面**，用步进按钮 + 右手柄摇杆上下调节 5 个影响晕动的参数，并新增**快照转向**、**舒适度预设**和**恢复默认**。所有数值写入 `PlayerPrefs` 并跨 Play 保留。面板尺寸保持 `1100 × 740` 不变，因此没有引入滚动。

设计目标：**减弱平滑移动与连续转向带来的晕动症**，同时保留“按 A 瞬移”这条最舒适的路径。

## 参数

| 子页行 | 单位 | 标准（默认） | 舒适 | 灵敏 | 允许范围 | 步进 |
|---|---|---|---|---|---|---|
| 移动暗角 | % | 40 | 60 | 0 | 0 – 80 | 10 |
| 最大移动速度 | m/s | 1.2 | 0.8 | 2.0 | 0.4 – 2.5 | 0.1 |
| 移动平滑 · 起步 | s | 0.25 | 0.35 | 0.10 | 0 – 0.6 | 0.05 |
| 移动平滑 · 停止 | s | 0.00 | 0.20 | 0.00 | 0 – 0.6 | 0.05 |
| 转向速度 | deg/s | 60 | 45 | 120 | 15 – 120 | 15 |
| 转向平滑 · 起步 | s | 0.20 | 0.30 | 0.05 | 0 – 0.5 | 0.05 |
| 转向平滑 · 停止 | s | 0.00 | 0.15 | 0.00 | 0 – 0.5 | 0.05 |
| 转向方式 | — | 平滑 | 平滑 | 平滑 | 平滑 / 快照 | — |
| 快照角度 | 度 | 45 | 45 | 45 | 30 / 45 / 60 | 15 |

- **起步与停止是两个独立参数**，移动和视角各一对，共 7 个可调数值。它们在子页上两两并排：同一行左侧是"起步"，右侧是"停止"，各自有独立的 `-` / `+` 与摇杆命中区。
- **起步**：从静止缓入到满速/满角速所需的秒数；`0` 表示起步也不平滑。
- **停止**：从满速/满角速刹停所需的秒数；**`0` 表示当帧停止**（默认值，也是本功能最初的要求），松杆后位移为 0。
- 上限刻意小于 `ApartmentVRLocomotion.ConfigurationError()` 的硬边界（速度 3 m/s、转向 180 deg/s），所以**菜单不可能把移动组件写成无效配置**。
- “转向速度”“转向平滑（起步/停止）”只在**平滑转向**下可调；选快照转向后这些项变灰且不接受摇杆与扳机，快照角度才变为可调。

## 操作

- “设置”页新增一行入口“舒适度调节”，点击进入子页面；子页最后一行有“恢复默认”和“返回”。
- 每一行是一个整行射线目标：**右手指向该行，右摇杆向上 = 加一档，向下 = 减一档**。每拨动一次只走一档，必须让摇杆回到死区之内才能走下一档（沿用项目既有的“松开再触发”锁存风格），因此不会一拨到底。
- 每行右侧的 `-` / `+` 按钮仍可用右食指扳机点击，方便微调或摇杆不顺手时使用。
- 顶部的 3 个预设按钮（舒适 / 标准 / 灵敏）一次性写入上表 7 个数值；写完后每一项仍可单独微调，微调后预设高亮自动消失。
- 数值**立即生效**，不需要关闭菜单；每次改动都会立刻写入存档。
- 子页面不是第 7 个页签：`Page` 仍是 4，左侧页签栏和“退出游戏”位置都不变，切页签会自动退出子页。

## 持久化

- 键名：`ApartmentComfort.v1`（PlayerPrefs，JSON 文本）。
- 读取时会**重新夹紧到合法区间并对齐步进**；JSON 损坏、字段缺失或数值越界都会**丢弃整条记录并回到默认档**，不会把非法值带进本次游玩。非有限值（`NaN`、`±∞`）一律修到该档的**下界**——每一项的下界都是最不晕的一端，所以损坏数据只会更保守，不会更激进。写入前同样先夹紧，因此非有限值不会进入存档。
- 如果夹紧后的值仍被移动组件判为无效配置，会退回默认并删除该记录（避免出现“存档把 VR 输入锁死、连菜单都打不开”的情况）。
- 菜单：`Apartment Phase 2 → M9 Comfort → Forget Saved Comfort Settings` 删除存档；删除后下次进入 Play 使用“标准”档。
- `PlayerPrefs` 是本项目**唯一**的持久化存储；教学进度、门状态、参观统计仍然只在本次 Play 内有效。

## 暗角（防眩晕视觉隧道）

- 使用 Meta XR SDK 自带的 `OVRVignette` 与 `Oculus/OVRVignette` 着色器，未自写着色器。组件挂在 `OVRCameraRig/CenterEyeAnchor` 的相机上，驱动脚本挂在 `VRPlayer/ApartmentMenu/ComfortVignette`。
- 暗角**只由真实运动驱动**：按当前平面速度与偏航角速度取较大者，达到满速/满角速时暗角达到设定的强度；静止、瞬移、桌面调试模式都不触发。
- 菜单打开时移动被阻断 → 运动量为 0 → 暗角自动淡出（0.35 秒），不会压暗菜单文字；出现较快（0.15 秒）是因为它是舒适性提示。边缘过渡宽度 24 度。
- 强度 0% 时直接禁用 `OVRVignette` 组件，不产生任何绘制开销。
- **打包注意**：安装器把 `Oculus/OVRVignette` 着色器写进组件的序列化字段，靠场景引用把它带进包体；若只依赖运行时的 `Shader.Find`，打包后着色器可能被剥离，暗角会静默失效。检查工具会验证该引用非空。
- 立体模式为 Single Pass Instanced（`m_StereoRenderingPath: 2`）。该着色器按 `unity_StereoEyeIndex` 取值，理论上兼容，但**必须真机确认双眼效果**。

## 缓动实现（起步与停止分别为独立参数）

缓动由纯结构 `ApartmentMotionRamp`（`Assets/Scripts/ApartmentMotionRamp.cs`）实现，移动与转向共用，但**起步与停止各有独立参数**：

- **起步（`smoothTime`，分别为移动/转向）**：某个值从静止（0）起步时，按该时间缓入到目标速度。缓入在到达目标后**立即结束**，之后加速不再平滑——"已经走起来之后再加快"是即时响应的。
- **停止（`stopSmoothTime`，分别为移动/转向）**：目标变为 0（摇杆回中、松开）时按该时间线性刹停；**`0` 表示当帧停止**，此时松杆后位移严格为 0。
- **部分回拉与反向一律即时**：摇杆只是推得少一点（仍在推动）或反向，属于玩家主动改变速度，不做平滑；只有"归零停止"才走停止参数。
- **硬停止**：追踪/姿态无效、菜单打开、教学未开放该项操作、瞬移帧、应用失去焦点时，无论停止参数是多少都当帧清零，不会惯性穿过教学或菜单闸门。
- 两个参数互不影响：起步 0 可以配停止 0.2 s，反之亦然。
- 起步 0 且停止 0 时完全等同旧版本（既不平滑也不滑行）。
- 转向缓动只作用于**连续（平滑）转向**；快照转向与键盘调试转向不经此缓动。
- 与暗角的配合：暗角淡出固定保留 0.35 秒，它跟随运动出现、滞后于运动消失，所以"停止"设 0 还是 0.2 s 都不会让暗角闪烁。

这些行为都有断言覆盖：离线执行 `ApartmentMotionRamp` 的断言全部通过（含"停止 0 → 松杆位移为 0"、"停止 0.2 s → 刹停约 14 帧且滑行不超过 `满速 × 停止时间`"、"两参数互不影响"），`M9 Comfort → Run Comfort Checks` 内也会跑同一组检查。

## 布局与排版核算

面板仍为 `1100 × 740`，子页面 8 行网格为 `190 + 7×62 + 54 = 678 < 690`（底部操作说明固定在 690）。7 个数值塞进 8 行靠的是**两两并排**：第 3 行是"移动平滑"（起步 / 停止），第 5 行是"转向平滑"（起步 / 停止），每行由行标题（`x=348, w=140`）加两个并列单元（`x=492` 与 `x=776`，各 `w=274`）组成，每个单元 = 短标签(60) + 右对齐数值 + `-`(62) + `+`(62)，各自是独立的摇杆命中区。

字体为 Noto Sans SC（pointSize 48，行高 69.504 → **行高系数 1.448**），据此对全部 30 个标签逐一核算过：

- 除"移动方式"的三个双行按钮外，其余标签在原始字号下宽度与高度均直接容纳，无需自动缩小。
- 三个模式按钮的双行高度为 81.1px，按钮文字框高 72px，靠 TMP 自动缩小到最小字号（82%）后为 66.5px 容纳，`isTextOverflowing` 保持为假——**这与改动前完全一致**，不是本轮新增的风险。
- "安全重置"的说明文案会显示 `TrySafetyReset` 的失败原因，最长一条 25 字（约 22 个全角字宽 ≈ 550px < 730px），不会折行；本轮把原来的"标题 + 说明"两行标签合并为一行，该处余量反而比改动前更大。

改版式前请同时看这两处：`ApartmentMenuController.Rebuild` / `RebuildComfort` 的坐标，以及 `Docs/ApartmentComfortSettings.md` 本节的行高系数。

## 修改的文件

- 新增 `Assets/Scripts/ApartmentComfortSettings.cs`：`ApartmentComfortProfile`（数值 + 夹紧 + 预设）与 `ApartmentComfortStore`（PlayerPrefs）。
- 新增 `Assets/Scripts/ApartmentComfortVignette.cs`：把真实运动映射成暗角淡入淡出，含可测试的纯函数 `TargetClearFov`。
- 改 `Assets/Scripts/ApartmentMenuController.cs`：舒适度子页面、步进行控件、预设、快照转向、持久化入口、`AllText` 文案。
- 改 `Assets/Scripts/ApartmentVRLocomotion.cs`：`moveSmoothTime` / `turnSmoothTime`、缓动状态机、`ApplyComfort` / `CaptureComfort`、范围校验。
- 改 `Assets/Scripts/ApartmentFurnitureInteractor.cs`：右摇杆上下采样（`ApartmentMenuStickStepLatch`）并传给菜单。
- 新增 `Assets/Scripts/Editor/ApartmentComfortUpgrade.cs`：M9 安装 / 校验 / 检查 / 字形补全，含独立入口 `Repair Menu Font Glyphs (Asset Only)`（只改字体资产）。
- 改 `Assets/Scripts/Editor/ApartmentMenuUpgrade.cs`：把 `RequireScene`、`Render`、`Protected`、`Hash` 改为可复用（纯可见性），运行时快照增加舒适度字段。

## 安装与检查

1. 让 Unity 导入新脚本并等待编译完成，**不要**在 Play 模式或场景有未保存修改时执行安装。
2. 先运行 `Apartment Phase 2 → M9 Comfort → Install Comfort Settings and Vignette`（快捷键 `Ctrl+Shift+6`）。它会：补全 `VRStandaloneUI-SC` 字体图集中新增文案的字形 → 在立体相机上添加 `OVRVignette` → 在 `ApartmentMenu` 下创建 `ComfortVignette` 驱动并接线 → 写入移动组件的平滑默认值 → 校验后保存场景，安装前场景备份到 `Logs/M9Backup-<时间戳>/`。
3. **必须先 M9 再 M8**：新增文案让菜单字形需求变大，而 M8 的字体检查是“缺字即报错”而不是自动补字。M9 完成补字后，M8 的 `Apply Standalone UI Font` 以及 M6/M7 的校验才会通过。
4. 运行 `Apartment Phase 2 → M9 Comfort → Run Comfort Checks`（`Ctrl+Shift+7`）。报告写入 `Logs/M9ComfortChecks.json`，界面预览写入 `Logs/M9-Comfort.png`、`Logs/M9-Comfort-Snap.png`、`Logs/M9-Settings.png`。检查覆盖：非法存档夹紧、预设本身合法、存档往返与损坏回退、暗角映射单调性、每一行双向各 200 档后仍落在移动组件支持区间内、快照转向下两行确实失效、切页签/返回退出子页、以及“设置”页仍保留三种移动方式与安全重置。检查还会逐个标签核对**渲染用到的字符都有字形**。检查会**临时备份并在结束时还原**你已有的舒适度存档。
5. 之后按需重跑 `M6 Menu → Run Menu Checks`、`M7 Visit → Run Visit Checks`、`M5 Upgrade → Run Upgrade Checks` 确认没有回归。

## 排错：界面上部分中文显示为空白方块

**原因**：菜单字体 `Assets/Apartment/Fonts/VRStandaloneUI-SC.asset` 是 **Static（静态图集）**，图集里只包含生成它那一刻所要求的字符。新增文案带来新字符后，图集里没有它们，TMP 就画不出字形。更关键的是：**TMP 明确拒绝对 Static 资产追加字符**（`TMP_FontAsset.TryAddCharacters` 在 `AtlasPopulationMode.Static` 时直接返回 false，只打一条 "because its AtlasPopulationMode is set to Static" 警告），所以"缺字即报错"的 M8 无法自愈，必须显式补字。

本功能新增文案实际缺这 18 个字符（`U+9ED8` 是"恢复默认"的"默"）：

```
上值大快敏数暗最灵照舒节调速预默（）
```

其中全角括号 `（）` 来自既有的"（当前未启用）"标注，属于**改动前就存在的隐性缺口**（选"平滑移动"或"A 键瞬移"时会显示为方块），本轮一并修掉。

**修复（任选其一）**

1. **脚本修复（推荐）**：退出 Play，运行 `Apartment Phase 2 → M9 Comfort → Repair Menu Font Glyphs (Asset Only)`。它只改字体资产、不打开也不保存场景；做法是把资产**临时切成 Dynamic 写入字形再切回 Static**，因此**资产 GUID 不变**，场景里那 3 处字体引用（`menu.font`、`interactor.hintFont`、`tutorial.font`）不会断。完成后会逐字复核并打印结果；若图集已满或字模缺失会明确报错。
2. **GUI 保底路径**：`Window → TextMeshPro → Font Asset Creator`，`Source Font File` 选 `Assets/Apartment/Fonts/NotoSansSC-Regular.otf`，`Character Set` 选 `Custom Characters` 并粘贴上面的字符（或直接粘贴全部所需字符），`Atlas Resolution` 保持 `2048 × 2048`、`Sampling Point Size` 保持 `48`、`Padding` 保持 `5`、`Render Mode` 保持 `SDFAA`，然后选 **`Update Atlas Texture`** 并指定现有的 `VRStandaloneUI-SC.asset`。该模式下 GUID 与场景引用同样保持不变。

**预防**：以后凡改动菜单/教学文案，改完就运行一次 `Repair Menu Font Glyphs (Asset Only)`；`ApartmentMenuUpgrade.RequiredCharacters()`（= 教学文案 + 门提示 + ASCII + 菜单 `AllText`）是所需字符的唯一来源，`M9 检查` 与 `M6 检查` 都会按它核对。

## 能否把完整 GBK 字符集写进资产

**源字体够用，图集不够用。**

实测源字体 `Assets/Apartment/Fonts/NotoSansSC-Regular.otf`：31,036 个字形、**30,890 个已映射码位**，其中 BMP 汉字区 `U+4E00–U+9FFF` 覆盖 **20,976 / 20,992**，另有 CJK 扩展 A 6,582 字。GBK 的 21,886 个字基本落在这些范围内，所以"字符从哪来"不是问题。

问题在图集容量。当前资产是 `2048 × 2048`、采样点号 48、padding 5，每个汉字字形约占 `(48 + 2×5) = 58 px` 的方格：

| 图集尺寸 | 单图容纳字形 | Alpha8 显存 | 预生成 GBK 全集（21,886 字）需要 |
|---|---|---|---|
| 2048 × 2048 | 约 1,225 | 4 MB | **约 18 个图集 ≈ 71 MB** |
| 4096 × 4096 | 约 4,900 | 16 MB | 约 5 个图集 ≈ 71 MB |

作为对照，当前全部界面文案只需 **349 字**，仅占 1 张 2048² 图集的 28.5%。而 GB2312 一级字 3,755 字要 3 张 2048²（约 11 MB），GB2312 全集 6,763 字要 6 张（约 22 MB）。预生成 GBK 全集还要打开 TMP 多图集，对一个 Quest 项目来说 71 MB 纹理（并相应增大 APK）并不划算。

**推荐做法：把该字体资产改为 Dynamic（按需生成）**

- 菜单：`Apartment Phase 2 → M9 Comfort → Use Dynamic Menu Font (Any Chinese, Recommended)`。
- 效果：凡是源字体支持的字符（含 GBK 全集）都会在**首次渲染时自动栅格化**，不需要预生成，也不会再出现空白方块；运行时图集只装实际渲染过的字符，本项目约 349 字，占用与现在相同。
- 代价：`NotoSansSC-Regular.otf`（7.95 MB）必须随包体发布（该字体导入设置 `includeFontData: 1`，已满足），运行时多约 8 MB 字体数据。
- 影响：`HasCharacter()` 在字符首次渲染前返回 `false`，因此 **M5/M6/M7/M8/M9 的"预生成字形"校验会自动跳过**（`ApartmentExperienceUpgrade.RequiresPreloadedGlyphs`）。这是切 Dynamic 的必要配套，已一并实现。
- 回退：`M9 Comfort → Use Static Menu Font (Curated Atlas)` 切回静态；若切回后提示缺字，接着运行 `Repair Menu Font Glyphs (Asset Only)`。

**若坚持静态预生成**：建议只做一个有界集合（例如 GB2312 一级字 3,755 字），用 4096 × 4096 图集一次装下（约 11 MB），并通过 `Font Asset Creator → Update Atlas Texture` 指定现有资产以保持 GUID 不变；它覆盖现代中文文本绝大部分用字，但仍不是 GBK 全集。

## 真机验收清单（编辑器与离线编译都不能替代）

1. 三种预设各选一次，确认明显可感：舒适最稳、灵敏最激进。
2. 平滑移动起步与停止是否平顺；把“移动速度平滑”调到 0.6 秒确认会滑行、调到 0 确认立即停。
3. 暗角只在真正移动/连续转向时出现，静止、瞬移、打开菜单时消失；**确认菜单与教学文字没有被暗角压暗**。
4. 快照转向角度是否正确、转头后身体碰撞是否正常（沿用既有的头显枢轴补偿）。
5. 关闭并重新进入 Play，确认设置被保留；用 `Forget Saved Comfort Settings` 后再进 Play，确认回到标准档。
6. 长时间游玩，重点确认晕动感是否比旧版本下降；若仍不适，先加大暗角与平滑时间，或改用“A 键瞬移”与快照转向。

## 与“减弱眩晕”的对应关系（按实测优先级）

1. **改用 A 键瞬移**：本版最有效的单项措施，不产生连续视觉流。
2. **快照转向**：消除连续偏航，效果仅次于瞬移。
3. **移动暗角**：直接抑制周边视觉冲突，但过强会影响观察房间细节。
4. **降低最大速度、加大平滑时间**：减少加减速冲击，但过慢会削弱控制感。
5. **降低转向速度**：连续转向较慢时更舒适。

这 5 项现在都可以在头显里即时调整，不需要重新打包。
