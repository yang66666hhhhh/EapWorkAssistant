using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using Xunit;

namespace EapWorkAssistant.Tests;

public class KnowledgeRepositoryTests
{
    [Fact]
    public async Task SoftDelete_And_Paging_WithFilters()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new KnowledgeRepository();
            for (int i = 0; i < 15; i++)
                await repo.InsertAsync(new Knowledge { Title = $"K{i}", Content = $"body {i}", Tags = "t", Category = i < 5 ? "A" : "B", IsFavorite = i < 3 ? 1 : 0 });

            var (page, total) = await repo.GetFilteredPagedAsync(null, false, null, 0, 10);
            Assert.Equal(15, total);
            Assert.Equal(10, page.Count());

            // 收藏筛选
            var (favPage, favTotal) = await repo.GetFilteredPagedAsync(null, true, null, 0, 10);
            Assert.Equal(3, favTotal);

            // 分类筛选
            var (catPage, catTotal) = await repo.GetFilteredPagedAsync(null, false, "A", 0, 10);
            Assert.Equal(5, catTotal);

            // 软删除
            var id = (await repo.GetAllAsync()).First().Id;
            await repo.DeleteAsync(id);
            Assert.Equal(14, (await repo.GetAllAsync()).Count());
            Assert.Single(await repo.GetDeletedAsync());

            await repo.RestoreAsync(id);
            Assert.Equal(15, (await repo.GetAllAsync()).Count());
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }
}
