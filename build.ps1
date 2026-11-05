$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$refs = @('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Web.Extensions.dll','System.Runtime.Serialization.dll','System.Xaml.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$argsList = @('/nologo','/target:winexe','/platform:x64','/optimize+','/out:DesktopTodo.exe')
foreach ($ref in $refs) { $argsList += '/reference:' + (Join-Path $framework $ref) }
$version = '1.0.2903.40'
$webSdk = Join-Path $PSScriptRoot ".build\webview2-$version"
$dependencies = @{
 'Microsoft.Web.WebView2.Core.dll' = 'lib\net462\Microsoft.Web.WebView2.Core.dll'
 'Microsoft.Web.WebView2.Wpf.dll' = 'lib\net462\Microsoft.Web.WebView2.Wpf.dll'
 'WebView2Loader.dll' = 'runtimes\win-x64\native\WebView2Loader.dll'
}
$missing = @($dependencies.Keys | Where-Object { -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $_)) })
if ($missing.Count) {
 $required = @($dependencies.Values | Where-Object { -not (Test-Path -LiteralPath (Join-Path $webSdk $_)) })
 if ($required.Count) {
  $package = "$webSdk.zip"
  New-Item -ItemType Directory -Force -Path (Split-Path $webSdk) | Out-Null
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $package
  Expand-Archive -LiteralPath $package -DestinationPath $webSdk -Force
 }
 foreach ($name in $missing) {
  Copy-Item -LiteralPath (Join-Path $webSdk $dependencies[$name]) -Destination (Join-Path $PSScriptRoot $name)
 }
}
foreach ($assembly in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll')) {
 $argsList += '/reference:' + (Join-Path $PSScriptRoot $assembly)
}
Push-Location $PSScriptRoot
try { & $compiler @argsList 'DesktopTodo.cs' 'LocalVaultStorage.cs' 'WorkspaceUI.cs' 'NativeFeatures.cs' 'Dashboard.cs' 'InlineTaskEditor.cs' 'UsageSessions.cs' 'CodexUsage.cs' 'StartupManager.cs' 'UniformButtons.cs' 'AppExit.cs' 'VaultTests.cs' 'InstanceGuard.cs' 'DateInput.cs' 'CategoryEditor.cs' 'ViewOptions.cs'; if ($LASTEXITCODE -ne 0) { throw 'Build failed' } } finally { Pop-Location }
