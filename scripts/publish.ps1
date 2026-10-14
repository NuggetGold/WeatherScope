param(
    [ValidateSet("standalone", "portable", "all")]
    [string]$Mode = "all",
    [ValidateSet("net6.0", "net8.0", "net10.0", "all")]
    [string]$Framework = "all",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\WeatherScope\WeatherScope.csproj"
$output = Join-Path $root "dist"

$frameworks = if ($Framework -eq "all") { @("net6.0", "net8.0", "net10.0") } else { @($Framework) }
$modes = if ($Mode -eq "all") { @("standalone", "portable") } else { @($Mode) }

foreach ($tfm in $frameworks) {
    foreach ($m in $modes) {
        $target = "$tfm-windows"
        $dest = Join-Path $output "$tfm-$m"
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }

        $selfContained = if ($m -eq "standalone") { "true" } else { "false" }
        $arguments = @(
            "publish", $project,
            "-c", $Configuration,
            "-f", $target,
            "-r", $Runtime,
            "--self-contained", $selfContained,
            "-p:PublishSingleFile=true",
            "-p:DebugType=None",
            "-p:DebugSymbols=false",
            "-o", $dest
        )
        if ($m -eq "standalone") {
            $arguments += "-p:IncludeNativeLibrariesForSelfExtract=true"
        }

        Write-Host "Публикация: $target, режим $m, выход $dest"
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Ошибка публикации для $target ($m)"
        }
    }
}

Write-Host "Готово. Результаты в $output"
