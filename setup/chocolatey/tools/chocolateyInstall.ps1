﻿$packageName = 'duck'
$installerType = 'EXE'
$url = 'https://dist.duck.sh/duck-${VERSION}.${REVISION}.exe'
$silentArgs = '/quiet'
$validExitCodes = @(0)

Install-ChocolateyPackage "$packageName" "$installerType" "$silentArgs" "$url" -validExitCodes $validExitCodes