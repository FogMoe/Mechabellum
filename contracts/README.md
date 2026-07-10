# 数据契约

`match-snapshot/v1/schema.json` 定义采集、持久化、训练与推理共同使用的 `MatchSnapshot v1`。`battle-start.example.json` 是一条通过该 schema 校验的完整示例。

契约版本位于路径和文档字段 `schemaVersion` 中。同一主版本保持已有字段语义稳定；不兼容的结构或语义修改使用新的版本目录。

JSONL 数据集每行保存一个完整快照，编码为 UTF-8。读取和写出入口均使用本目录中的 schema 验证持久化表示，并构造强类型模型供包内模块使用。
