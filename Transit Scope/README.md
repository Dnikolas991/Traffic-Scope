# Transit Scope

## English

### Overview
Transit Scope is a Cities: Skylines II mod focused on visual selection and quick inspection of roads, railways, metro tracks, and buildings.

The mod provides:
- Unified selection for roads, railway lines, metro lines, and buildings
- Hover highlight and confirmed selection feedback
- Traffic flow analysis for selected transport networks
- A compact in-game UI with graphical statistics

### Current Features
- Hover roads, railways, and metro tracks with overlay feedback
- Confirm selection and keep the native selected marker behavior
- Hover buildings with a clean outline
- Display selected traffic composition as a pie chart
- Display selected building information as a graphical summary

### Tech Stack
- C# ECS systems for gameplay logic and overlay rendering
- React + TypeScript for the in-game UI
- Cities: Skylines II official modding toolchain

### Project Goal
The goal of Transit Scope is to make transport and object inspection easier and more intuitive inside the game, while keeping the visual style close to the original Cities: Skylines II interface.

### Future Plans
This is my first mod, so please forgive its shortcomings.

In the future, I will try to implement hover effects that are closer to the vanilla game and add more complete features.

## 中文

### 模组简介
Transit Scope 是一个面向《Cities: Skylines II》的信息查看类模组，主要用于在游戏中快速选择并查看道路、铁路线、地铁线路和建筑的相关信息。

这个模组目前提供：
- 道路、铁路、地铁、建筑的统一选择
- 悬停高亮与确认选中的视觉反馈
- 选中交通网络后的流量构成分析
- 紧凑的游戏内图形化统计界面

### 当前功能
- 对道路、铁路线、地铁线路提供悬停 overlay 高亮
- 点击确认后保留原版 selected marker 选中表现
- 对建筑提供简洁的悬停描边
- 用饼图展示所选道路或轨道的交通流量构成
- 用图形化方式展示所选建筑的基础信息

### 技术结构
- 后端使用 C# ECS System 处理工具逻辑、选择逻辑和 overlay 绘制
- 前端使用 React + TypeScript 构建游戏内 UI
- 基于 Cities: Skylines II 官方 Mod Toolchain 构建

### 项目目标
Transit Scope 的目标，是在尽量贴近原版 UI 与交互风格的前提下，为玩家提供更直观、更方便的对象选择与交通信息查看体验。

### 未来展望
这是我的第一个模组，有许多不足之处请见谅。

未来会尝试实现与原版相似的悬停效果以及更完善的功能。
