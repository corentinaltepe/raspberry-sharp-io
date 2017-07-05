param([String]$Config="Debug") 

$gitCommitHash = (git rev-parse HEAD)
$buildNumber = ([System.DateTime]::UtcNow - [System.DateTime]::Parse("2010-01-01T00:00:00.0000000Z").ToUniversalTime()).TotalDays.ToString("F0")
$buildNumber += ([System.DateTime]::UtcNow.TimeOfDay.TotalSeconds/10).ToString("0000")
$VersionSuffix = "build" + $buildNumber

(dotnet msbuild "/t:Restore;Pack" /p:VersionSuffix=$VersionSuffix /p:Configuration=$Config)