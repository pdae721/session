param([switch]$NoLaunch)
$ErrorActionPreference='Stop'
$sourcePath=$PSScriptRoot
$installPath=Join-Path $env:LOCALAPPDATA 'Programs\DesktopTodo'
New-Item -ItemType Directory -Force -Path $installPath | Out-Null
$files=@('DesktopTodo.exe','NativeTheme.xaml','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll')
foreach($name in $files){if(-not(Test-Path -LiteralPath (Join-Path $sourcePath $name))){throw "Missing release file: $name"}}
$existing=Get-Process DesktopTodo -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $installPath 'DesktopTodo.exe')}
if($existing){
 Start-Process -FilePath (Join-Path $installPath 'DesktopTodo.exe') -ArgumentList '--quit' -WindowStyle Hidden -Wait
 for($attempt=0;$attempt -lt 60;$attempt++){
  if(-not(Get-Process DesktopTodo -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $installPath 'DesktopTodo.exe')})){break}
  Start-Sleep -Milliseconds 500
 }
 if(Get-Process DesktopTodo -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $installPath 'DesktopTodo.exe')}){throw 'Widget is still running. Save drafts and exit it before updating. No data or profiles were changed.'}
}
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $sourcePath $name) -Destination (Join-Path $installPath $name) -Force}
New-Item -ItemType Directory -Force (Join-Path $installPath 'assets') | Out-Null
$identity=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$service=New-Object -ComObject Schedule.Service
$service.Connect()
$definition=$service.NewTask(0)
$definition.RegistrationInfo.Description='Desktop Todo desktop widget - delayed logon startup with retries'
$definition.Principal.UserId=$identity
$definition.Principal.LogonType=3
$definition.Principal.RunLevel=0
$trigger=$definition.Triggers.Create(9)
$trigger.UserId=$identity
$trigger.Delay='PT15S'
$action=$definition.Actions.Create(0)
$action.Path=Join-Path $installPath 'DesktopTodo.exe'
$action.WorkingDirectory=$installPath
$action.Arguments='--startup'
$definition.Settings.Enabled=$true
$definition.Settings.AllowDemandStart=$true
$definition.Settings.StartWhenAvailable=$true
$definition.Settings.DisallowStartIfOnBatteries=$false
$definition.Settings.StopIfGoingOnBatteries=$false
$definition.Settings.ExecutionTimeLimit='PT0S'
$definition.Settings.RestartCount=3
$definition.Settings.RestartInterval='PT1M'
$definition.Settings.MultipleInstances=2
$task=$service.GetFolder('\').RegisterTaskDefinition('DesktopTodo',$definition,6,$identity,$null,3,$null)
Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name DesktopTodo -ErrorAction SilentlyContinue
[PSCustomObject]@{InstallPath=$installPath;StartupTask=$task.Name;Enabled=$task.Enabled;Delay=$trigger.Delay;RestartCount=$definition.Settings.RestartCount;TimeLimit=$definition.Settings.ExecutionTimeLimit}
if(-not $NoLaunch){$task.Run($null) | Out-Null}
