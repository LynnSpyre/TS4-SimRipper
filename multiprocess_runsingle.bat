SETLOCAL EnableExtensions EnableDelayedExpansion
SET "arg1=%1"
set "param1=%1"
set "param2=%2"
set "param3=%3"
set "param4=%4"

echo %param1% - %param2% - %param3% - %param4%
start /w %param1% %param2% %param3% %param4%
