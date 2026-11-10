# Runs the console checks headless against every page variant. Exit 0 only if
# every variant passes except missing-state, which must fail.
$root = Split-Path $PSScriptRoot
dotnet build "$root/src/FormAutomation" --nologo -v q | Out-Null
if ($LASTEXITCODE) { Write-Error "build failed"; exit 1 }

$ok = 0; $total = 0; $bad = @()
foreach ($f in Get-ChildItem "$root/variants/*.html") {
    $name = $f.BaseName
    $expectFail = $name -eq 'missing-state'
    Write-Host "=== $name ===$(if ($expectFail) { ' (expected to fail)' })"
    dotnet run --project "$root/src/FormAutomation" --no-build -- --headless true --timeout 5 --url ([Uri]$f.FullName).AbsoluteUri
    $total++
    if (($LASTEXITCODE -ne 0) -eq $expectFail) { if (-not $expectFail) { $ok++ } } else { $bad += $name }
}
if ($bad) { Write-Host "$ok of $total variants passed, unexpected result: $($bad -join ', ')"; exit 1 }
Write-Host "$ok of $total variants passed, missing-state failed as expected"
