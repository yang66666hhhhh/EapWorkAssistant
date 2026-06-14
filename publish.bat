@echo off
chcp 65001 >nul
echo ========================================
echo   EAP Work Assistant - 发布脚本
echo ========================================
echo.

:: 清理旧的发布输出
if exist "publish" (
    echo 清理旧的发布目录...
    rd /s /q publish
)

:: 自包含发布（无需目标机器安装 .NET Runtime）
echo 正在发布（自包含，Windows x64）...
echo 这可能需要几分钟，请耐心等待...
echo.

dotnet publish -c Release ^
    --self-contained true ^
    -r win-x64 ^
    -o publish ^
    -p:PublishReadyToRun=true ^
    -p:PublishSingleFile=false ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    --nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo 发布失败！请检查上方的错误信息。
    pause
    exit /b 1
)

echo.
echo ========================================
echo   发布成功！
echo   输出目录: publish\
echo   主程序:   publish\EapWorkAssistant.exe
echo ========================================
echo.
echo 你可以将整个 publish 文件夹打包为 zip 分发给用户，
echo 用户无需安装 .NET Runtime 即可直接运行。
echo.
pause
