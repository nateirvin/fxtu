@echo OFF

rem thanks to http://stackoverflow.com/questions/15567809/batch-extract-path-and-filename-from-a-variable
rem thanks to http://stackoverflow.com/questions/5553040/batch-file-for-loop-with-spaces-in-dir-name
SETLOCAL
set file=%1
FOR /f "delims=" %%i IN ("%file%") DO (
set filepath=%%~di%%~pi
set filename=%%~ni%%~xi
)

pushd %filepath%
rename %filename% web.config
aspnet_regiis -pef %2 "%filepath:~0,-1%"
rename web.config %filename%
popd
