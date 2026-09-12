# 奥尔伦多 0.2（Archon Engine 初版）

开局：AO 纪元 2136 年。单人离线，横屏，全奥尔伦多世界地图。

## 运行

打开 `Assets/Game/Scenes/Aoerlunduo-Archon.unity`，点击 Unity Play。
在地图上选择一个有主省份，再点击国家选择面板中的“开始游戏”。

旧的独立原型仍保存在 `Assets/Game/Scenes/Aoerlunduo.unity`，只作为备份和规则参考。

## Archon Engine 接入

- 主场景从 Archon Engine 的 `StarterKit.unity` 复制并单独保存。
- `AoerlunduoSettings.asset` 将引擎数据目录指向 `Assets/Game/Aoerlunduo-Data`。
- 世界边界图转换为 96 个 Archon 原生省份，其中 28 个国家可玩，海洋和南极保持无主。
- 国家、首都、颜色、历史归属、地形、高程和本地化均使用引擎数据文件加载。
- 时间使用 `TimeManager`，开局为 `AO 2136-01-01`。
- 经济、月度收入、农场建设使用 StarterKit 的 `EconomySystem` 和 `BuildingSystem`。
- 步兵创建、单位驻扎和移动使用 StarterKit/Core 的单位与命令系统。
- 地图高亮、国家着色、国界、地图模式、镜头和省份选择使用 Archon Engine 地图系统。
- AI 使用 StarterKit 的 `AISystem`。
- `AoerlunduoArchonRuntime` 负责显示国家选择、游戏标题，以及接入月度时间与经济系统的少量随机事件。

## 数据位置

- `Assets/Game/Aoerlunduo-Data/map`：省份色彩图、高程、地形和定义表。
- `Assets/Game/Aoerlunduo-Data/common/countries`：28 个国家定义。
- `Assets/Game/Aoerlunduo-Data/history/provinces`：省份开局归属。
- `Assets/Game/Aoerlunduo-Data/localisation/english`：当前中文显示文本；目录名沿用引擎默认语言槽。
- `Assets/Game/Aoerlunduo-Data/units`：步兵定义。
- `Assets/Game/Aoerlunduo-Data/common/buildings`：农场定义。
- `Assets/Game/Aoerlunduo-Data/world_manifest.json`：国家、首都、省份数和坐标的生成清单。

## 当前范围

这是可迭代的战略沙盒初版。国家名和首都为暂定设定；省份名称、经济数值、军事平衡、外交、科技、完整事件链、音画资源和移动端 UI 仍可继续细化。

Android 构建仍需要为 Unity 6000.6.0f1 安装 Android Build Support、SDK、NDK 与 JDK，然后把数据同步到 StreamingAssets 并完成设备性能与触控测试。

## 上游

Archon Engine：https://github.com/ForgottenHistory/Archon-Engine

参考版本：`27d3ec2f46ad8362f59719850cf549f74a583c82`。奥尔伦多新增内容位于 `Assets/Game`；没有改写 `Assets/Archon-Engine` 中的上游源码和素材。
