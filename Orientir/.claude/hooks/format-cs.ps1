# PostToolUse hook: format an edited .cs file with `dotnet format`.
# Reads the hook payload (JSON) from stdin, formats just the touched file
# within the solution, and never fails the tool call.
$ErrorActionPreference = 'SilentlyContinue'
try {
    $raw = [Console]::In.ReadToEnd()
    if (-not $raw) { exit 0 }
    $payload = $raw | ConvertFrom-Json
    $file = $payload.tool_input.file_path
    if (-not $file) { $file = $payload.tool_response.filePath }
    if ($file -and $file.ToLower().EndsWith('.cs')) {
        $sln = Join-Path $PSScriptRoot '..\..\Orientir.sln'
        & dotnet format $sln --include $file | Out-Null
    }
} catch { }
exit 0
