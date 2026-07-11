# Memory

## 边界
- 读取 Terraria 进程、MemoryProbe 布局、托管对象、世界生成状态和创图种子。
- 输出外部状态快照或诊断，不决定自动化流程。

## 约束
- 读取失败、形状错误或版本不匹配时返回 Unknown/空结果/诊断，不使应用崩溃。
- 缓存地址必须处理页面切换、进程重启和对象失效，并允许重新扫描。
- 不在此处点击、等待、读取设置或做筛选决策。
- 除 UI scale patch 外不新增版本专用字节签名；修改布局时运行 memory resolver 与 world-generation memory 聚焦测试。
