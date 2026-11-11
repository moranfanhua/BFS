# 黑流尖塔音乐 bank

`BlackFlowSpireMusic.bank`由本机FMOD Studio 2.03.06编译，包含7首音乐的14段前奏／循环音频，Vorbis质量80，流式播放。`BlackFlowSpireMusic.guids.txt`为同次构建产生的事件路径与GUID映射。

事件绑定尖塔0.111.0现有的`bus:/master/music`，不加载生成工程的Master bank，沿用游戏主音量、音乐音量和静音。仅在黑流模式中播放，曲目分配和生命周期见工作区根目录`BLACKFLOW-MUSIC.md`。

原始音频来自本地客户端提取结果`artifacts/arknights-blackflow/bgm/clips-flac`，来源与场景核对见`BLACKFLOW-BGM-ASSETS.md`。本次使用`m_sys_rouge6_theme1/2`、`m_sys_rglk1shop`、`m_sys_rouge6_shop`、`m_bat_rouge6_1/2/3`的分段FLAC，不使用拼接试听OGG。

重建：运行`tools/BlackFlowSpire.Audio/build_music.py`，同时更新bank与GUID映射。音乐配置写在C#，音频时间线与循环区域由`tools/BlackFlowSpire.Audio/BuildMusic.js`生成。
