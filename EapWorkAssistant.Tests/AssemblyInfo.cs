using Xunit;

// 仓库测试依赖 DatabaseInitializer 的静态连接串覆盖，关闭并行化避免互相干扰。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
