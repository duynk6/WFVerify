<#
.SYNOPSIS
    Cai dat nhanh WinForms Verifier MCP Server: publish binary va dang ky voi AI client.

.DESCRIPTION
    Thay vi phai tu tay publish roi copy-paste JSON voi duong dan tuyet doi vao tung file
    cau hinh client, script nay:
      1. Chay `dotnet publish` ra dist/ (bo qua bang -SkipPublish neu da publish roi).
      2. Tu suy ra duong dan .exe tu vi tri thuc te cua script (khong hardcode o dau).
      3. Voi -Client claude-code: goi thang `claude mcp add` (cach nhanh nhat, khong dong file).
      4. Voi -Client claude-desktop / cursor / antigravity: merge server vao file JSON cau hinh
         tuong ung, backup file cu truoc khi ghi de.
      5. Mac dinh (-Client print): chi in doan JSON san sang dan vao bat ky client nao khac
         (VS Code Cline/Roo, ...) - khong dong file nao ca.

.PARAMETER Client
    claude-code | claude-desktop | cursor | antigravity | print (mac dinh: print)

.PARAMETER AllowedRoots
    Danh sach thu muc duoc phep cho WFVERIFY_ALLOWED_ROOTS, phan tach bang ';'.
    Mac dinh: thu muc goc cua repo nay.

.PARAMETER SkipPublish
    Bo qua buoc `dotnet publish` (dung khi da publish o lan chay truoc).

.EXAMPLE
    .\install.ps1 -Client claude-code
    Publish server va dang ky ngay voi Claude Code CLI.

.EXAMPLE
    .\install.ps1 -Client claude-desktop
    Publish server va them vao %APPDATA%\Claude\claude_desktop_config.json (co backup).

.EXAMPLE
    .\install.ps1 -SkipPublish
    Chi in JSON cau hinh voi duong dan .exe hien tai, khong build lai.
#>
[CmdletBinding()]
param(
    [ValidateSet('claude-code', 'claude-desktop', 'cursor', 'antigravity', 'print')]
    [string]$Client = 'print',

    [string]$AllowedRoots,

    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$distDir = Join-Path $repoRoot 'dist'
$exePath = Join-Path $distDir 'WinFormsVerifier.McpServer.exe'

if (-not $SkipPublish) {
    Write-Host "==> Publish WinFormsVerifier.McpServer ra dist/ ..." -ForegroundColor Cyan
    dotnet publish (Join-Path $repoRoot 'src\WinFormsVerifier.McpServer') -c Release -r win-x64 --self-contained false -o $distDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish that bai (exit code $LASTEXITCODE). Sua loi build roi chay lai."
    }
}

if (-not (Test-Path $exePath)) {
    throw "Khong tim thay $exePath. Bo -SkipPublish di de script tu publish, hoac kiem tra loi build ben tren."
}

if (-not $AllowedRoots) {
    $AllowedRoots = $repoRoot
}

function New-ServerEntry {
    [pscustomobject]@{
        command = $exePath
        args    = @()
        env     = [pscustomobject]@{
            WFVERIFY_ALLOWED_ROOTS = $AllowedRoots
            WFVERIFY_LOG_LEVEL     = 'Information'
        }
    }
}

function Merge-McpConfig {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [pscustomobject]$ServerEntry
    )

    $dir = Split-Path -Parent $FilePath
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    if (Test-Path $FilePath) {
        $backup = "$FilePath.bak.$(Get-Date -Format 'yyyyMMddHHmmss')"
        Copy-Item -Path $FilePath -Destination $backup -Force
        Write-Host "    (da backup cau hinh cu: $backup)" -ForegroundColor DarkGray
        $raw = Get-Content -Path $FilePath -Raw
        $json = if ([string]::IsNullOrWhiteSpace($raw)) { [pscustomobject]@{} } else { $raw | ConvertFrom-Json }
    } else {
        $json = [pscustomobject]@{}
    }

    if (-not ($json.PSObject.Properties.Name -contains 'mcpServers')) {
        $json | Add-Member -MemberType NoteProperty -Name 'mcpServers' -Value ([pscustomobject]@{})
    }

    $json.mcpServers | Add-Member -MemberType NoteProperty -Name 'winforms-verifier' -Value $ServerEntry -Force

    ($json | ConvertTo-Json -Depth 10) | Set-Content -Path $FilePath -Encoding utf8
    Write-Host "    Da ghi: $FilePath" -ForegroundColor Green
}

$serverEntry = New-ServerEntry

switch ($Client) {
    'claude-code' {
        Write-Host "==> Dang ky voi Claude Code CLI ..." -ForegroundColor Cyan
        claude mcp add winforms-verifier -- $exePath
        Write-Host "    Go lai bang: claude mcp remove winforms-verifier" -ForegroundColor DarkGray
    }
    'claude-desktop' {
        $target = Join-Path $env:APPDATA 'Claude\claude_desktop_config.json'
        Write-Host "==> Cap nhat Claude Desktop: $target" -ForegroundColor Cyan
        Merge-McpConfig -FilePath $target -ServerEntry $serverEntry
        Write-Host "    Khoi dong lai Claude Desktop de nhan cau hinh moi." -ForegroundColor DarkGray
    }
    'cursor' {
        $target = Join-Path $repoRoot '.cursor\mcp.json'
        Write-Host "==> Cap nhat Cursor (workspace): $target" -ForegroundColor Cyan
        Merge-McpConfig -FilePath $target -ServerEntry $serverEntry
    }
    'antigravity' {
        $target = Join-Path $repoRoot '.mcp.json'
        Write-Host "==> Cap nhat Antigravity (workspace): $target" -ForegroundColor Cyan
        Merge-McpConfig -FilePath $target -ServerEntry $serverEntry
    }
    default {
        $wrapped = [pscustomobject]@{
            mcpServers = [pscustomobject]@{ 'winforms-verifier' = $serverEntry }
        }
        Write-Host "==> Dan doan JSON sau vao file cau hinh MCP cua client (VD: cline_mcp_settings.json):" -ForegroundColor Cyan
        $wrapped | ConvertTo-Json -Depth 10
        Write-Host "`nHoac neu dung Claude Code CLI, chay thang:" -ForegroundColor DarkGray
        Write-Host "  claude mcp add winforms-verifier -- $exePath" -ForegroundColor DarkGray
    }
}

Write-Host "`nDuong dan server: $exePath" -ForegroundColor Green
Write-Host "WFVERIFY_ALLOWED_ROOTS: $AllowedRoots" -ForegroundColor Green
