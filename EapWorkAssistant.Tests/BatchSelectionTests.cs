using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.Linq;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// P2 ⑧ 列表多选批量操作：模式切换 / 选择状态同步 / 批量软删除与撤销恢复。
/// 知识库与问题跟踪共用 <see cref="PagedCollectionViewModelBase{T}"/> 的通用实现，
/// 工作记录（不继承基类）有独立的多选实现，故分别覆盖。
/// </summary>
public class BatchSelectionTests
{
    // ===== 模式切换与选择状态（纯 VM 状态，不触库） =====

    [Fact]
    public void ToggleMultiSelect_切换模式并在退出时清空选择_Issue()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var vm = new IssueViewModel(new IssueRepository());
            Assert.False(vm.IsMultiSelectMode);
            Assert.False(vm.HasSelection);

            vm.ToggleMultiSelectCommand.Execute(null);
            Assert.True(vm.IsMultiSelectMode);

            vm.SelectedItems.Add(new Issue { Id = 1, Title = "a" });
            vm.SelectedItems.Add(new Issue { Id = 2, Title = "b" });
            Assert.True(vm.HasSelection);

            // 退出多选 → 清空已选，批量按钮失活
            vm.ToggleMultiSelectCommand.Execute(null);
            Assert.False(vm.IsMultiSelectMode);
            Assert.Empty(vm.SelectedItems);
            Assert.False(vm.HasSelection);
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public void ToggleMultiSelect_切换模式并在退出时清空选择_WorkRecord()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            var leaveRepo = new LeaveRecordRepository();
            var vm = new WorkRecordViewModel(repo, leaveRepo,
                new WorkRecordImportService(), new CompLeaveBalanceService(repo, leaveRepo));

            Assert.False(vm.IsMultiSelectMode);

            vm.ToggleMultiSelectCommand.Execute(null);
            Assert.True(vm.IsMultiSelectMode);

            vm.SelectedRecords.Add(new WorkRecord { Id = 1 });
            Assert.True(vm.HasSelection);

            vm.ToggleMultiSelectCommand.Execute(null);
            Assert.False(vm.IsMultiSelectMode);
            Assert.Empty(vm.SelectedRecords);
            Assert.False(vm.HasSelection);
        }
        finally { TestDb.Cleanup(db); }
    }

    // ===== 批量软删除 / 撤销恢复（基类通用实现，走真实 SQLite） =====

    [Fact]
    public async Task 批量删除_软删除选中项并刷新列表_Issue()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new IssueRepository();
            for (int i = 0; i < 5; i++)
                await repo.InsertAsync(new Issue
                {
                    ProjectName = "P",
                    Title = $"I{i}",
                    Description = $"desc {i}",
                    Status = "Open",
                    Priority = "Low",
                    CreateTime = $"2026-10-0{i + 1} 09:00:00"
                });

            var vm = new IssueViewModel(repo);
            await vm.RefreshAsync();
            Assert.Equal(5, vm.PagedItems.Count);

            var toDelete = vm.PagedItems.Take(2).ToList();
            foreach (var it in toDelete) vm.SelectedItems.Add(it);

            await vm.DeleteItemsAsync(toDelete);

            // 软删除生效：未删除 3 条、回收站 2 条（可从回收站恢复，非物理删除）
            Assert.Equal(3, (await repo.GetAllAsync()).Count());
            Assert.Equal(2, (await repo.GetDeletedAsync()).Count());
            // 列表已刷新、选择已清空
            Assert.Equal(3, vm.PagedItems.Count);
            Assert.Empty(vm.SelectedItems);
            Assert.False(vm.HasSelection);
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task 批量恢复_撤销批量删除_Issue()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new IssueRepository();
            for (int i = 0; i < 3; i++)
                await repo.InsertAsync(new Issue
                {
                    ProjectName = "P", Title = $"I{i}", Status = "Open", Priority = "Low",
                    CreateTime = $"2026-10-0{i + 1} 09:00:00"
                });

            var vm = new IssueViewModel(repo);
            await vm.RefreshAsync();

            var toDelete = vm.PagedItems.Take(2).ToList();
            await vm.DeleteItemsAsync(toDelete);
            Assert.Single(await repo.GetAllAsync());

            // 撤销：批量恢复
            await vm.RestoreItemsAsync(toDelete);
            Assert.Equal(3, (await repo.GetAllAsync()).Count());
            Assert.Empty(await repo.GetDeletedAsync());
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task 批量删除_知识库复用同一基类实现_Knowledge()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new KnowledgeRepository();
            for (int i = 0; i < 4; i++)
                await repo.InsertAsync(new Knowledge
                {
                    Title = $"K{i}",
                    Content = $"content {i}",
                    Category = "EAP",
                    CreateTime = $"2026-10-0{i + 1} 09:00:00"
                });

            var vm = new KnowledgeViewModel(repo);
            await vm.RefreshAsync();
            Assert.Equal(4, vm.PagedItems.Count);

            var toDelete = vm.PagedItems.Take(3).ToList();
            await vm.DeleteItemsAsync(toDelete);

            Assert.Single(await repo.GetAllAsync());
            Assert.Equal(3, (await repo.GetDeletedAsync()).Count());
            Assert.Single(vm.PagedItems);
        }
        finally { TestDb.Cleanup(db); }
    }
}
