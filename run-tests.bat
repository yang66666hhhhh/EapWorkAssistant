@echo off
cd /d %~dp0
echo 运行 EAP Work Assistant 单元测试（隔离输出，绕过 bin/Debug 锁）...
dotnet test EapWorkAssistant.Tests/EapWorkAssistant.Tests.csproj -c Debug -p:OutDir=bin/_verify_test/ --logger "console;verbosity=normal"
echo.
echo 测试结束。按任意键关闭窗口。
pause >nul
