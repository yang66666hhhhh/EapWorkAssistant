using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using Xunit;

namespace EapWorkAssistant.Tests;

public class IssueRepositoryTests
{
    [Fact]
    public async Task SoftDelete_And_Paging_WithStatusFilter()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new IssueRepository();
            for (int i = 0; i < 12; i++)
                await repo.InsertAsync(new Issue
                {
                    ProjectName = "P",
                    Title = $"I{i}",
                    Description = $"desc {i}",
                    Status = i % 2 == 0 ? "Open" : "Resolved",
                    Priority = i < 4 ? "High" : "Low"
                });

            var (page, total) = await repo.GetFilteredPagedAsync(null, null, null, 0, 10);
            Assert.Equal(12, total);
            Assert.Equal(10, page.Count());

            // 状态筛选
            var (openPage, openTotal) = await repo.GetFilteredPagedAsync(null, "Open", null, 0, 10);
            Assert.Equal(6, openTotal);

            // 优先级筛选
            var (highPage, highTotal) = await repo.GetFilteredPagedAsync(null, null, "High", 0, 10);
            Assert.Equal(4, highTotal);

            // 软删除
            var id = (await repo.GetAllAsync()).First().Id;
            await repo.DeleteAsync(id);
            Assert.Equal(11, (await repo.GetAllAsync()).Count());
            Assert.Single(await repo.GetDeletedAsync());

            await repo.RestoreAsync(id);
            Assert.Equal(12, (await repo.GetAllAsync()).Count());
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }
}
