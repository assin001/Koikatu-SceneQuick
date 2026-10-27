<h1 align="center">场景快捷 · Scene Quick</h1>

<p align="center">
  <b>Koikatu / CharaStudio 工作室插件</b><br>
  <sub>读卡之后最常做的三件事——换人、开关效果、清理杂物，全部收进右侧一块面板</sub>
</p>

<p align="center">
  <img alt="Beta" src="https://img.shields.io/badge/release-v0.1beta-f59e0b?style=flat-square">
  <img alt="Game" src="https://img.shields.io/badge/game-Koikatu%20%2F%20CharaStudio-2ea96b?style=flat-square">
</p>

> **Beta**：主要流程已实测可用，细节仍在打磨。遇到问题请带上 `BepInEx\LogOutput.log` 反馈。

---

## 入口

- `F5` 开 / 关面板（快捷键可在配置里改）
- 屏幕右侧边缘的「快 捷」竖条，进工作室就常驻，点一下同样开关
- 面板贴右侧显示，高度默认屏幕的 72%，可配

## 面板做什么

| 栏位 | 功能 |
|---|---|
| **1 人物替换** | 列出场景角色（同名角色自动折叠成组）；单个或整组显隐、多选、删除；展开「角色卡」按名字 / 最近使用 / 修改 / 创建排序，点卡片即可换人（支持多选批量换）；下方列出该角色身上的挂载物并逐个开关 |
| **2 效果** | 自动识别效果类物件（后处理、雾、泛光、音频等），一键全开 / 全关 / 重置；勾选若干效果存成预设，随时读回 |
| **3 场景物品** | 按出现顺序列出独立物品，显隐 / 删除 / 分组展开，「重置」恢复读卡时的显隐 |

- 显隐、删除、换人之后面板自动刷新
- 物品行显示「中文名 + 原名 + 分类路径 + 所属 Mod」，右键任意物品行可以把它**改判**成效果或普通物品（也可恢复自动判断），改判结果会记住

## 效果预设

- 勾选效果行左边的「勾」，点「存预设」→ 写入当前显隐、位置、旋转、缩放以及组件上的公开参数
- 预设窗口（效果栏「预设管理」）支持：读入、覆盖、改名、删除、上移 / 下移
- 读入时场景里没有的物件会自动生成（`AddObjectItem`），已有的则按预设还原参数

## 配置（`BepInEx\config\kk.studio.scenequick.cfg`）

| 段 / 键 | 默认 | 说明 |
|---|---|---|
| 1-General / Enabled | true | 总开关 |
| 1-General / Panel hotkey | F5 | 面板快捷键 |
| 2-Cards / Sort order | name | 角色卡排序：name / recent / modified / created |
| 3-Sections / Panel height ratio | 0.72 | 面板高度占屏幕比例（0.4~1.0） |
| 3-Sections / Fold characters | false | 「人物替换」默认收起 |
| 3-Sections / Fold effects | false | 「效果」默认收起 |
| 3-Sections / Fold items | false | 「场景物品」默认收起 |

面板位置、折叠状态与排序会即时生效；改动配置后重启游戏即可重新读取默认值。

## 数据文件（`BepInEx\config\`）

| 文件 | 内容 |
|---|---|
| `kk.studio.scenequick.presets.txt` | 效果预设（可直接手改，格式 `[名字]\|时间` + `I/T/P` 行） |
| `kk.studio.scenequick.recent.txt` | 角色卡最近使用记录 |
| `kk.studio.scenequick.effectoverride.txt` | 手动「效果 / 普通」改判 |

## 安装

1. 需要 BepInEx 5（工作室为 `CharaStudio.exe`）
2. 把 `KK_SceneQuick.dll` 放进 `BepInEx\plugins\`
3. 启动工作室，右侧出现「快 捷」竖条即加载成功

## 构建

```powershell
# 需要 Roslyn 的 csc.exe；默认在 _tools\roslyn 或 SceneBrowser 工具目录里找
powershell -File tools\build.ps1 -Deploy
```

参数：`-GameRoot`（默认 `E:\game\GDAkk\Koikatu`）、`-OutDir`、`-Csc`、`-Deploy`（备份并覆盖到 `BepInEx\plugins`）。

## 说明

- 界面用 uGUI 重做，色板与圆角样式与 `KK_SceneBrowser` 保持一致
- 已挂 XUnity.AutoTranslator 忽略规则，面板文字不会被自动翻译
- 效果识别基于物件名关键词 + 组件类型，识别不到时用右键改判，一次设置长期有效
- 换人调用工作室自带的 `OCIChar.ChangeChara`，原角色的位置、表情、FK 状态由游戏自行处理

## 已知限制

- 只做了 Koikatsu（`CharaStudio.exe`），KKS 仍需用原 `KKS_SceneQuick`
- 角色卡列表只读 `UserData\chara\male|female`，不含子文件夹
- 预设参数快照只覆盖 float / int / bool / Color / Vector2 / Vector3 类型的公开字段
