using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using Xunit;

namespace EapWorkAssistant.Tests;

public class WorkRecordRepositoryTests
{
    [Fact]
    public async Task SoftDelete_HidesFromGetAll_AndIsRecoverable()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            var id = await repo.InsertAsync(new WorkRecord { WorkDate = "2026-08-12", ProjectName = "P", WorkType = "T", Content = "c" });
            Assert.Single(await repo.GetAllAsync());

            await repo.DeleteAsync(id); // 软删除
            Assert.Empty(await repo.GetAllAsync());

            var deleted = (await repo.GetDeletedAsync()).ToList();
            Assert.Single(deleted);
            Assert.Equal(id, deleted[0].Id);

            await repo.RestoreAsync(id);
            Assert.Single(await repo.GetAllAsync());

            await repo.HardDeleteAsync(id);
            Assert.Empty(await repo.GetDeletedAsync());
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }

    [Fact]
    public async Task GetFilteredPagedAsync_Paginates_And_Searches()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            // 每 3 条写入一条带 "special" 标记的内容，便于搜索断言精确计数
            for (int i = 0; i < 25; i++)
                await repo.InsertAsync(new WorkRecord
                {
                    WorkDate = $"2026-08-{i + 1:00}",
                    ProjectName = "P",
                    WorkType = "T",
                    Content = i % 3 == 0 ? $"special {i}" : $"normal {i}"
                });

            var (page, total, _, _) = await repo.GetFilteredPagedAsync(null, null, null, null, null, 0, 10);
            Assert.Equal(25, total);
            Assert.Equal(10, page.Count());

            // 关键词搜索：带 "special" 的记录为 0,3,6,9,12,15,18,21,24 共 9 条
            var (page2, total2, _, _) = await repo.GetFilteredPagedAsync("special", null, null, null, null, 0, 10);
            Assert.Equal(9, total2);
            Assert.All(page2, r => Assert.Contains("special", r.Content));
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }

    [Fact]
    public async Task Stats_ExcludeSoftDeleted()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            var id = await repo.InsertAsync(new WorkRecord { WorkDate = "2026-08-01", ProjectName = "P", WorkType = "T", Content = "c", Hours = 8 });
            Assert.Equal(1, await repo.GetTotalCountAsync());

            await repo.DeleteAsync(id);
            Assert.Equal(0, await repo.GetTotalCountAsync());
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }
}
