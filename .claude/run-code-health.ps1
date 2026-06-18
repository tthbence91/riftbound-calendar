param([string]$RepoPath = "C:/Repos/riftbound-calendar")

function Emit-Result {
    param([string]$Summary, [string]$Detail, [int]$ExitCode)
    $output = @{
        systemMessage = "=== CodeScene Code Health ===`n$Summary"
        hookSpecificOutput = @{
            hookEventName = "PostToolUse"
            additionalContext = "=== CodeScene Code Health Review ===`n$Detail"
        }
    } | ConvertTo-Json -Depth 3 -Compress
    Write-Output $output
    exit $ExitCode
}

# Find all source .cs files, excluding obj/ dirs and placeholder UnitTest1 files
$sourceFiles = Get-ChildItem -Path $RepoPath -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\obj\\" } |
    Where-Object { $_.Name -ne "UnitTest1.cs" } |
    ForEach-Object { $_.FullName.Replace("\", "/") }

if ($sourceFiles.Count -eq 0) {
    Emit-Result "No source files found." "No .cs files found in $RepoPath" 0
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$tmpIn  = "$env:TEMP\mcp_ch_in.txt"
$tmpOut = "$env:TEMP\mcp_ch_out.txt"

# Build NDJSON: initialize + initialized notification + one tools/call per file
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"health-hook","version":"1.0"}}}')
$lines.Add('{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}')

$idMap = @{}  # id -> file path
$id = 2
foreach ($file in $sourceFiles) {
    $escaped = $file.Replace('"', '\"')
    $lines.Add("{`"jsonrpc`":`"2.0`",`"id`":$id,`"method`":`"tools/call`",`"params`":{`"name`":`"code_health_review`",`"arguments`":{`"file_path`":`"$escaped`"}}}")
    $idMap[$id] = $file
    $id++
}

$content = ($lines -join "`n") + "`n"
[System.IO.File]::WriteAllText($tmpIn, $content, $utf8NoBom)

if (Test-Path $tmpOut) { Remove-Item $tmpOut -Force }

$proc = Start-Process -FilePath "C:/Users/user/AppData/Local/Programs/cs-mcp/cs-mcp.exe" `
    -RedirectStandardInput $tmpIn `
    -RedirectStandardOutput $tmpOut `
    -NoNewWindow -Wait -PassThru `
    -WorkingDirectory $RepoPath

if (-not (Test-Path $tmpOut)) {
    Emit-Result "ERROR: cs-mcp produced no output." "cs-mcp exited with code $($proc.ExitCode) and no stdout." 0
}

$outputLines = Get-Content $tmpOut -Encoding UTF8 -ErrorAction SilentlyContinue
if (-not $outputLines) {
    Emit-Result "ERROR: Empty output from cs-mcp." "cs-mcp exit code: $($proc.ExitCode)" 0
}

# Parse responses, match by id
$results = [System.Collections.Generic.List[PSCustomObject]]::new()
foreach ($line in $outputLines) {
    if ($line -match '"id":(\d+)') {
        $respId = [int]$Matches[1]
        if ($idMap.ContainsKey($respId)) {
            try {
                $parsed = $line | ConvertFrom-Json
                $filePath = $idMap[$respId]
                $fileName = [System.IO.Path]::GetFileName($filePath)
                if ($parsed.error) {
                    $results.Add([PSCustomObject]@{ File = $fileName; Score = -1; Text = "ERROR: $($parsed.error.message)" })
                } else {
                    $textContent = $parsed.result.content |
                        Where-Object { $_.type -eq "text" } |
                        Select-Object -ExpandProperty text
                    $fullText = $textContent -join "`n"
                    # Response is JSON: {"score":9.68,"review":[...]}
                    $score = -1
                    try {
                        $scoreObj = $fullText | ConvertFrom-Json
                        if ($null -ne $scoreObj.score) { $score = [double]$scoreObj.score }
                    } catch {}
                    $results.Add([PSCustomObject]@{ File = $fileName; Score = $score; Text = $fullText })
                }
            } catch {
                $results.Add([PSCustomObject]@{ File = $idMap[$respId]; Score = -1; Text = "Parse error: $_" })
            }
        }
    }
}

if ($results.Count -eq 0) {
    Emit-Result "No results parsed." "Raw output lines: $($outputLines.Count). First line: $($outputLines[0])" 0
}

# Build summary
$summaryLines = [System.Collections.Generic.List[string]]::new()
$detailParts  = [System.Collections.Generic.List[string]]::new()
$allGreen = $true

foreach ($r in $results) {
    $scoreLabel = if ($r.Score -ge 0) { "$($r.Score)/10" } else { "N/A" }
    $summaryLines.Add("  $($r.File): $scoreLabel")
    if ($r.Score -lt 10.0 -and $r.Score -ge 0) { $allGreen = $false }
    # null score (N/A) means file too small to analyze — treat as OK
    $detailParts.Add("--- $($r.File) ($scoreLabel) ---`n$($r.Text)")
}

$statusEmoji = if ($allGreen) { "[OK]" } else { "[!]" }
$summary = "$statusEmoji $($results.Count) file(s) analyzed:`n" + ($summaryLines -join "`n")
$detail  = $detailParts -join "`n`n"
$exitCode = if ($allGreen) { 0 } else { 2 }

Emit-Result $summary $detail $exitCode
