Write-Host "Cleaning up publish folder" -f DarkGreen
Remove-Item .publish/* -Recurse -Force

Write-Host "Publishing for Windows" -f DarkGreen
dotnet publish PgReorder.App/PgReorder.App.csproj --output .publish/win --artifacts-path .publish/win-artifacts --self-contained true --framework net9.0 --runtime win-x64 --configuration Release

Write-Host "Publishing for Linux" -f DarkGreen
dotnet publish PgReorder.App/PgReorder.App.csproj --output .publish/linux --artifacts-path .publish/linux-artifacts --self-contained true --framework net9.0 --runtime linux-x64 --configuration Release

Write-Host "Publishing for Mac" -f DarkGreen
dotnet publish PgReorder.App/PgReorder.App.csproj --output .publish/osx --artifacts-path .publish/osx-artifacts --self-contained true --framework net9.0 --runtime osx-x64 --configuration Release

Write-Host "Cleaning up artifacts" -f DarkGreen
Remove-Item .publish/**/*.pdb -Recurse -Force
Remove-Item .publish/win-artifacts -Recurse -Force
Remove-Item .publish/linux-artifacts -Recurse -Force
Remove-Item .publish/osx-artifacts -Recurse -Force

Write-Host "Renaming final binaries" -f DarkGreen
Rename-Item -Path ".publish/win/PgReorder.App.exe" -NewName "pgreorder.exe"
Rename-Item -Path ".publish/linux/PgReorder.App" -NewName "pgreorder"
Rename-Item -Path ".publish/osx/PgReorder.App" -NewName "pgreorder"

Write-Host "Compressing" -f DarkGreen
Compress-Archive -Path ".publish/win/pgreorder.exe" -DestinationPath ".publish/win/pgreorder.zip"
cd .publish/linux
tar -czf pgreorder-linux.tar.gz pgreorder
cd ../..
cd .publish/osx
tar -czf pgreorder-osx.tar.gz pgreorder
cd ../..
Write-Host "Done" -f Green