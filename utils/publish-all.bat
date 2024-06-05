@echo off
setlocal enableDelayedExpansion

set apps=CameraCapture CaptivClient DataManager EmpaticaClient EquivitalClient ErgoPak KeMoStreamer MoticonClient

echo Apps to be published:
for %%a in (!apps!) do (
    echo %%a
)
echo *** NOTE: if this list is not complete, modify the this batch file and add your app to the list. ***
set /p publishConfirm="Do you want to publish these apps? (y/n): "
echo publishConfirm: !publishConfirm!
if /i !publishConfirm!==y (
    
    set /p apath="Press enter to publish apps at default location (C:\Apps) or enter a custom path: "

    if NOT defined apath (
        set apath=C:\Apps
    ) 
    if not exist "!apath!\" (
        echo The directory !apath! does not exist. Please create it first.
        goto :End
    )    
    set apath=!apath!\AugmentX
    echo Publishing at !apath!
    cd ..
	cd apps
    for %%a in (!apps!) do (
    	cd %%a
    	dotnet publish -r win-x64 -f net6.0-windows /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained true --output !apath!\%%a
    	echo !apath!\%%a
    	DEL !apath!\%%a\*.pdb
		cd ..
    )

) else (
    echo Publishing canceled.
)
Endlocal

:End

pause
