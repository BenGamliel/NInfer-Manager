$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$problems = [System.Collections.Generic.List[string]]::new()
$excluded = '\\(build|dist|bin|obj|TestWorkspace|runtime)\\'

Get-ChildItem -LiteralPath $projectRoot -File -Recurse -Force |
    Where-Object { $_.FullName -notmatch $excluded } |
    ForEach-Object {
        if ($_.Extension -in '.ninfer', '.part', '.log', '.jsonl') { $problems.Add("Forbidden artifact: $($_.FullName)") }
        if ($_.Length -gt 100MB) { $problems.Add("Unexpected large file: $($_.FullName)") }
    }

$patterns = @(
    '(?i)bearer\s+[a-z0-9_-]{20,}',
    '(?i)api[_-]?key\s*[:=]\s*["''][^"'']{8,}',
    '(?i)[a-z]:\\users\\[^\\]+',
    '(?i)[a-z]:\\space\\'
)
$textFiles = Get-ChildItem -LiteralPath $projectRoot -File -Recurse -Force |
    Where-Object { $_.FullName -notmatch $excluded -and $_.Extension -in '.cs', '.csproj', '.md', '.txt', '.json', '.ps1', '.iss', '.gitignore' }
foreach ($file in $textFiles) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($pattern in $patterns) {
        if ($content -match $pattern) { $problems.Add("Sensitive/local pattern in $($file.FullName): $pattern") }
    }
}

if ($problems.Count) {
    $problems | ForEach-Object { Write-Error $_ }
    throw "Repository audit failed with $($problems.Count) issue(s)."
}
Write-Host 'Repository audit passed: no models, partials, logs, local paths or obvious secrets were found.' -ForegroundColor Green
