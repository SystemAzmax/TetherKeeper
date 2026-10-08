<#
.SYNOPSIS
    TetherKeeper の MSIX パッケージを作成し、自己署名証明書で署名する。

.DESCRIPTION
    1. 署名用の自己署名証明書 (CN=TetherKeeper) が無ければ作成する
       (現在のユーザーの証明書ストアに作るため、管理者権限は不要)
    2. MSIX パッケージを作成して署名する
    3. インストール先 PC で信頼登録するための公開証明書 (TetherKeeper.cer) を出力する

    出力先: artifacts\msix\
    証明書の Subject は Package.appxmanifest の Identity Publisher と一致させること。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\Publish-Msix.ps1
#>
param(
    [ValidateSet('x64', 'arm64')]
    [string] $Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'TetherKeeper\TetherKeeper.csproj'
$outDir = Join-Path $root 'artifacts\msix'
$subject = 'CN=TetherKeeper'

# --- 1. 署名用証明書 (有効期限内で秘密鍵を持つものを再利用。無ければ 5 年有効で作成) ---
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "署名用の自己署名証明書を作成します ($subject)"
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -FriendlyName 'TetherKeeper MSIX signing (self-signed)' `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @(
            '2.5.29.37={text}1.3.6.1.5.5.7.3.3',  # 拡張キー使用法: コード署名
            '2.5.29.19={text}'                    # 基本制約: CA ではない
        )
}
Write-Host "署名証明書: $($cert.Subject) (拇印 $($cert.Thumbprint), 有効期限 $($cert.NotAfter.ToString('yyyy-MM-dd')))"

# --- 2. MSIX の作成と署名 ---
if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

dotnet publish $project `
    -f net10.0-windows10.0.19041.0 `
    -c Release `
    -p:RuntimeIdentifierOverride=win-$Platform `
    -p:WindowsPackageType=MSIX `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateThumbprint=$($cert.Thumbprint) `
    -p:AppxPackageDir="$outDir\"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish が失敗しました (終了コード $LASTEXITCODE)"
}

# --- 3. 公開証明書の出力 (秘密鍵は含まない) ---
$cerPath = Join-Path $outDir 'TetherKeeper.cer'
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

$msix = Get-ChildItem $outDir -Recurse -Filter *.msix | Select-Object -First 1
Write-Host ''
Write-Host "MSIX : $($msix.FullName)"
Write-Host "証明書: $cerPath"
Write-Host 'インストール手順は README の「MSIX でのインストール」を参照してください。'
