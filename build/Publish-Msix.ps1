<#
.SYNOPSIS
    TetherKeeper の MSIX パッケージを作成し、自己署名証明書で署名する。

.DESCRIPTION
    1. 署名用の自己署名証明書 (CN=TetherKeeper) が無ければ作成する
       (現在のユーザーの証明書ストアに作るため、管理者権限は不要)
       秘密鍵はエクスポート不可で作成する。盗まれると、この証明書を信頼した全 PC で
       任意の MSIX が警告なしにインストールできてしまうため、PC の外に持ち出せないようにする
    2. MSIX パッケージを作成して署名する。署名にはタイムスタンプを付ける
       (付けないと、証明書の有効期限後にインストール・更新できなくなる)
    3. インストール先 PC で信頼登録するための公開証明書 (TetherKeeper.cer) を出力する

    出力先: artifacts\msix\
    証明書の Subject は Package.appxmanifest の Identity Publisher と一致させること。

.PARAMETER TimestampUrl
    署名のタイムスタンプに使う RFC 3161 タイムスタンプサーバー。空文字を指定するとタイムスタンプを付けない。
    (タイムスタンプサーバーに送られるのは署名のハッシュ値だけで、パッケージの中身は送られない)

.PARAMETER NewCertificate
    既存の証明書を使わず、新しい証明書を作成する。
    エクスポート可能な秘密鍵の古い証明書から切り替えるときに使う。
    新しい証明書は、インストール先の各 PC で改めて信頼登録が必要になる。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\Publish-Msix.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\Publish-Msix.ps1 -NewCertificate
#>
param(
    [ValidateSet('x64', 'arm64')]
    [string] $Platform = 'x64',

    [string] $TimestampUrl = 'http://timestamp.digicert.com',

    [switch] $NewCertificate
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'TetherKeeper\TetherKeeper.csproj'
$outDir = Join-Path $root 'artifacts\msix'
$subject = 'CN=TetherKeeper'

# 秘密鍵がエクスポート可能か (判定できない場合は安全側に倒して true)
function Test-PrivateKeyExportable([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate) {
    try {
        $key = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
        if ($key -is [System.Security.Cryptography.RSACng]) {
            return $key.Key.ExportPolicy -ne [System.Security.Cryptography.CngExportPolicies]::None
        }
        if ($key -is [System.Security.Cryptography.RSACryptoServiceProvider]) {
            return $key.CspKeyContainerInfo.Exportable
        }
    }
    catch {
    }
    return $true
}

# --- 1. 署名用証明書 (有効期限内で秘密鍵を持つものを再利用。無ければ 5 年有効で作成) ---
# エクスポート不可の証明書を優先する
$cert = $null
if (-not $NewCertificate) {
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
        Sort-Object @{ Expression = { Test-PrivateKeyExportable $_ } }, @{ Expression = 'NotAfter'; Descending = $true } |
        Select-Object -First 1
}

if ($cert -and (Test-PrivateKeyExportable $cert)) {
    # 証明書を勝手に作り直すと、インストール先 PC での信頼登録がやり直しになるため警告に留める
    Write-Warning ("署名証明書の秘密鍵がエクスポート可能です (拇印 $($cert.Thumbprint))。" +
        "-NewCertificate を付けて実行するとエクスポート不可の証明書に切り替えられます " +
        "(インストール先 PC で新しい TetherKeeper.cer の信頼登録が必要です)。")
}

if (-not $cert) {
    Write-Host "署名用の自己署名証明書を作成します ($subject)"
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -KeyExportPolicy NonExportable `
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
    -p:AppxPackageSigningTimestampServerUrl=$TimestampUrl `
    -p:AppxPackageDir="$outDir\"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish が失敗しました (終了コード $LASTEXITCODE)"
}

# --- 3. 公開証明書の出力 (秘密鍵は含まない) ---
$cerPath = Join-Path $outDir 'TetherKeeper.cer'
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

$msix = Get-ChildItem $outDir -Recurse -Filter *.msix | Select-Object -First 1

# --- 4. 署名の確認 ---
$signature = Get-AuthenticodeSignature $msix.FullName
if ($signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
    throw "MSIX が想定した証明書で署名されていません ($($signature.StatusMessage))"
}
if ($TimestampUrl -and -not $signature.TimeStamperCertificate) {
    Write-Warning 'MSIX の署名にタイムスタンプが付いていません。証明書の有効期限後はインストール・更新できなくなります。'
}
Write-Host ''
Write-Host "MSIX : $($msix.FullName)"
Write-Host "証明書: $cerPath"
Write-Host 'インストール手順は README の「MSIX でのインストール」を参照してください。'
