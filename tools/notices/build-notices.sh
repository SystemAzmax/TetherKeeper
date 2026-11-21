#!/usr/bin/env bash
#
# ThirdPartyNotices.txt を、NuGet パッケージ付属のライセンスファイルなどから作成する。
# NuGet パッケージや .NET のランタイムを更新したら、下のバージョンを書き換えてから実行する
# （詳しくは docs/BuildGuide.md の「ライセンス表示の更新」を参照）。
#
# 使い方（Git Bash で、リポジトリのフォルダーから実行する）:
#   bash tools/notices/build-notices.sh [出力先] [ライセンスファイルのフォルダー]
# 出力先の既定は TetherKeeper/ThirdPartyNotices.txt。
# ライセンスファイルのフォルダーの既定は tools/notices/licenses
# （パッケージにライセンスファイルが無いソフトウェアの LICENSE を置く）。
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# アプリのプロジェクトのフォルダー（リポジトリのフォルダーからの相対パス）
APP_DIR="TetherKeeper"
OUT="${1:-$SCRIPT_DIR/../../$APP_DIR/ThirdPartyNotices.txt}"
DL="${2:-$SCRIPT_DIR/licenses}"
P="$(cygpath -u "$USERPROFILE")/.nuget/packages"

# 改行を LF にそろえ、末尾に改行を付けて出力する
emit_file() {
  tr -d '\r' < "$1"
  printf '\n'
}

section() {
  printf '\n\n================================================================================\n'
  printf '%s\n' "$1"
  printf '================================================================================\n\n'
}

{
  printf '\xEF\xBB\xBF'
  cat <<'EOF'
テザリングキーパー サードパーティ ソフトウェアのライセンス情報
================================================================================

テザリングキーパーは、次のソフトウェアを使用しています。
各ソフトウェアの著作権表示とライセンスを、原文のまま記載します。

  1. .NET MAUI (Microsoft.Maui.Controls 10.0.110) - MIT License
  2. .NET Community Toolkit (CommunityToolkit.Mvvm 8.4.2) - MIT License
  3. Microsoft.Extensions（DependencyInjection、Logging など 10.0）- MIT License
  4. Microsoft.IO.RecyclableMemoryStream 3.0.1 - MIT License
  5. .NET 10（.NET ランタイム 10.0.13）- MIT License
  6. Microsoft Edge WebView2 SDK 1.0.3179.45 - BSD 3-Clause 形式のライセンス
  7. Win2D (Microsoft.Graphics.Win2D 1.4.0) - Microsoft ソフトウェア ライセンス条項
  8. Windows App SDK 1.8（WinUI などの部品）- Microsoft ソフトウェア ライセンス条項
  9. Windows SDK の .NET 向けプロジェクション (Microsoft.Windows.SDK.NET.Ref 10.0.19041.57)
     - Microsoft ソフトウェア ライセンス条項

付録として、.NET MAUI、.NET Community Toolkit、Microsoft Edge WebView2 SDK、.NET、
Windows App SDK に含まれるサードパーティのソフトウェアの表示を、
各ソフトウェアに付属のファイルから原文のまま記載します。

  付録 A. .NET MAUI - THIRD-PARTY-NOTICES.TXT
  付録 B. .NET Community Toolkit - ThirdPartyNotices.txt
  付録 C. Microsoft Edge WebView2 SDK - NOTICE.txt
  付録 D. .NET - THIRD-PARTY-NOTICES.TXT（Microsoft.Extensions も同じ内容）
  付録 E. Windows App SDK - NOTICE.txt
EOF

  section "1. .NET MAUI (Microsoft.Maui.Controls 10.0.110) - MIT License"
  emit_file "$P/microsoft.maui.controls/10.0.110/LICENSE.TXT"

  section "2. .NET Community Toolkit (CommunityToolkit.Mvvm 8.4.2) - MIT License"
  emit_file "$P/communitytoolkit.mvvm/8.4.2/License.md"

  # Microsoft.Extensions のパッケージにはライセンスファイルが無い。
  # .NET ランタイムと同じリポジトリ (dotnet/dotnet) から作られているため、ランタイムの LICENSE.TXT を載せる
  section "3. Microsoft.Extensions (DependencyInjection, Logging, etc. 10.0) - MIT License"
  emit_file "$P/microsoft.netcore.app.runtime.win-x64/10.0.13/LICENSE.TXT"

  # パッケージにライセンスファイルが無いため、パッケージを作ったコミットのリポジトリの LICENSE を置いている
  section "4. Microsoft.IO.RecyclableMemoryStream 3.0.1 - MIT License"
  emit_file "$DL/recyclablememorystream-LICENSE.txt"

  section "5. .NET 10 (.NET Runtime 10.0.13) - MIT License"
  emit_file "$P/microsoft.netcore.app.runtime.win-x64/10.0.13/LICENSE.TXT"

  section "6. Microsoft Edge WebView2 SDK 1.0.3179.45 - BSD 3-Clause style license"
  emit_file "$P/microsoft.web.webview2/1.0.3179.45/LICENSE.txt"

  section "7. Win2D (Microsoft.Graphics.Win2D 1.4.0) - Microsoft Software License Terms"
  cat <<'EOF'
Copyright (c) Microsoft Corporation. All rights reserved.
Redistributed under the Microsoft Software License Terms for Win2D.
http://www.microsoft.com/web/webpi/eula/eula_win2d_10012014.htm
EOF

  section "8. Windows App SDK 1.8 - Microsoft Software License Terms"
  cat <<'EOF'
Copyright (c) Microsoft Corporation. All rights reserved.
Redistributed under the Microsoft Software License Terms for Microsoft Windows App SDK.
Third-party notices for the Windows App SDK are included in Appendix E.
EOF

  section "9. Windows SDK .NET Projection (Microsoft.Windows.SDK.NET.Ref 10.0.19041.57) - Microsoft Software License Terms"
  cat <<'EOF'
Copyright (c) Microsoft Corporation. All rights reserved.
Redistributed under the Microsoft Software License Terms for the Windows SDK.
https://aka.ms/WinSDKLicenseURL
EOF

  section "付録 A. .NET MAUI - THIRD-PARTY-NOTICES.TXT"
  emit_file "$P/microsoft.maui.controls/10.0.110/THIRD-PARTY-NOTICES.TXT"

  section "付録 B. .NET Community Toolkit - ThirdPartyNotices.txt"
  emit_file "$P/communitytoolkit.mvvm/8.4.2/ThirdPartyNotices.txt"

  section "付録 C. Microsoft Edge WebView2 SDK - NOTICE.txt"
  emit_file "$P/microsoft.web.webview2/1.0.3179.45/NOTICE.txt"

  section "付録 D. .NET - THIRD-PARTY-NOTICES.TXT"
  emit_file "$P/microsoft.netcore.app.runtime.win-x64/10.0.13/THIRD-PARTY-NOTICES.TXT"

  section "付録 E. Windows App SDK - NOTICE.txt"
  # 部品のうち Base の NOTICE.txt が SDK 全体の表示（WinUI の NOTICE.txt の内容も含む）
  emit_file "$P/microsoft.windowsappsdk.base/1.8.251216001/NOTICE.txt"
} | sed 's/$/\r/' > "$OUT"  # Windows で開くファイルのため、改行を CRLF にして書き出す
