@echo off

echo %time%

(
start "" multiprocess_runsingle.bat TS4SimRipper.exe 1 0 "E:\Dropbox\Machine Learning\Sims 4 Saves\5k YA Sims Set 2.save"
start "" multiprocess_runsingle.bat TS4SimRipper.exe 1 0 "E:\Dropbox\Machine Learning\Sims 4 Saves\5k YA Sims Set 3.save"
start "" multiprocess_runsingle.bat TS4SimRipper.exe 1 0 "E:\Dropbox\Machine Learning\Sims 4 Saves\5k YA Sims Set 4.save"

) | set /P "="

echo %time%