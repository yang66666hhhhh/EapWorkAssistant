using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.Linq;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 编辑记录时表单能否正确回填工时（双口径）——排查「编辑时工时显示 0」用。
/// </summary>
public class WorkRecordEditLoadTests
{
    [Fact]
    public async Task 编辑记录_应回填报工时与实际工时()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            var leaveRepo = new LeaveRecordRepository();

            await repo.InsertAsync(new WorkRecord
            {
                WorkDate = "2026-10-10",
                ProjectName = "测试项目",
                WorkType = "调试",
                Content = "编辑加载测试内容",
                Hours = 8,
                ActualHours = 4,
                Progress = 100
            });

            // 1) 仓储往返：SELECT * 能否映射 Hours / ActualHours
            var loaded = (await repo.GetByDateAsync("2026-10-10")).Single();
            Assert.Equal(8, loaded.Hours, 3);
            Assert.Equal(4, loaded.ActualHours, 3);
            Assert.Equal(100, loaded.Progress);

            // 2) EditRecord 复制逻辑：抽屉表单读的就是 CurrentRecord
            var vm = new WorkRecordViewModel(
                repo, leaveRepo, new WorkRecordImportService(),
                new CompLeaveBalanceService(repo, leaveRepo));

            vm.EditRecordCommand.Execute(loaded);

            Assert.Equal(8, vm.CurrentRecord.Hours, 3);
            Assert.Equal(4, vm.CurrentRecord.ActualHours, 3);
            Assert.Equal(100, vm.CurrentRecord.Progress);
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }
}
