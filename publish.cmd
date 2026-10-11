@echo off
setlocal

rem Publishes LinkDash and copies it to the IIS site folder on the web server.
rem App_Data (links, admin account, cookie keys) and appsettings.Production.json on the server are never touched.

set "TARGET=\\web.bergdahl.local\LinkDashRoot"
set "OUTPUT=%~dp0publish"

if not exist "%TARGET%\" (
    echo Cannot reach %TARGET%.
    exit /b 1
)

if exist "%OUTPUT%" rd /s /q "%OUTPUT%"
dotnet publish "%~dp0src\Bergdahl.LinkDash.Web" -c Release -o "%OUTPUT%"
if errorlevel 1 (
    echo Publish failed.
    exit /b 1
)

rem app_offline.htm makes IIS stop the app and release its files while they are replaced.
echo ^<!DOCTYPE html^>^<title^>LinkDash^</title^>^<p^>LinkDash is being updated. Try again in a minute.^</p^> > "%TARGET%\app_offline.htm"

robocopy "%OUTPUT%" "%TARGET%" /E /R:10 /W:2 /NP /NFL /NDL /XD App_Data /XF appsettings.Production.json app_offline.htm
set "COPY_RESULT=%ERRORLEVEL%"

del "%TARGET%\app_offline.htm"

rem Robocopy exit codes below 8 mean success.
if %COPY_RESULT% GEQ 8 (
    echo Copy to %TARGET% failed with robocopy exit code %COPY_RESULT%.
    exit /b 1
)

echo Published to %TARGET%.
