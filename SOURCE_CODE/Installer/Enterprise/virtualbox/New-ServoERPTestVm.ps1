param(
    [string]$VmName = 'ServoERP-QA-Win11',
    [string]$IsoPath,
    [string]$OvaPath,
    [string]$BaseFolder = "$env:USERPROFILE\VirtualBox VMs",
    [int]$MemoryMb = 4096,
    [int]$CpuCount = 2,
    [int64]$DiskSizeMb = 81920,
    [string]$Username = 'servoqa',
    [string]$Password = 'ServoQA!2026',
    [switch]$Start,
    [switch]$Headless
)

$ErrorActionPreference = 'Stop'

$VBoxManage = @(
    'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe',
    'C:\Program Files\VirtualBox\VBoxManage.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $VBoxManage) {
    throw 'VBoxManage.exe was not found. Install Oracle VirtualBox first.'
}

if ($IsoPath -and $OvaPath) {
    throw 'Pass either -IsoPath or -OvaPath, not both.'
}

if (-not $IsoPath -and -not $OvaPath) {
    throw 'Pass a Windows installation ISO with -IsoPath or a Microsoft/Windows OVA with -OvaPath.'
}

function Invoke-VBox {
    & $VBoxManage @args
    if ($LASTEXITCODE -ne 0) {
        throw "VBoxManage failed: $($args -join ' ')"
    }
}

$existing = & $VBoxManage list vms
if ($existing -match [regex]::Escape("`"$VmName`"")) {
    throw "A VirtualBox VM named '$VmName' already exists. Delete it or choose another -VmName."
}

New-Item -ItemType Directory -Force -Path $BaseFolder | Out-Null

if ($OvaPath) {
    if (-not (Test-Path -LiteralPath $OvaPath)) {
        throw "OVA not found: $OvaPath"
    }

    Invoke-VBox import $OvaPath --vmname $VmName --basefolder $BaseFolder --memory $MemoryMb --cpus $CpuCount --options importtovdi
    Invoke-VBox modifyvm $VmName --clipboard-mode bidirectional --drag-and-drop hosttoguest --nic1 nat --audio-driver none
}
else {
    if (-not (Test-Path -LiteralPath $IsoPath)) {
        throw "ISO not found: $IsoPath"
    }

    $vmFolder = Join-Path $BaseFolder $VmName
    $diskPath = Join-Path $vmFolder "$VmName.vdi"

    Invoke-VBox createvm --name $VmName --ostype Windows11_64 --basefolder $BaseFolder --register
    Invoke-VBox modifyvm $VmName --memory $MemoryMb --cpus $CpuCount --firmware efi --tpm-type 2.0 --ioapic on --x86-pae on --nested-paging on --graphicscontroller vboxsvga --vram 128 --clipboard-mode bidirectional --drag-and-drop hosttoguest --nic1 nat --audio-driver none --boot1 dvd --boot2 disk
    Invoke-VBox storagectl $VmName --name SATA --add sata --controller IntelAhci --hostiocache on
    Invoke-VBox createmedium disk --filename $diskPath --size $DiskSizeMb --format VDI
    Invoke-VBox storageattach $VmName --storagectl SATA --port 0 --device 0 --type hdd --medium $diskPath
    Invoke-VBox storageattach $VmName --storagectl SATA --port 1 --device 0 --type dvddrive --medium $IsoPath

    $hostName = ($VmName -replace '[^A-Za-z0-9-]', '-')
    if ($hostName.Length -gt 15) {
        $hostName = $hostName.Substring(0, 15)
    }
    $unattendedHostName = "$hostName.local"

    $postInstallScript = Join-Path $vmFolder 'ServoERP-QA-PostInstall.cmd'
    @(
        '@echo off',
        'reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System /v EnableLUA /t REG_DWORD /d 0 /f',
        'shutdown /r /t 10 /c "ServoERP QA VM enabling noninteractive installer testing"'
    ) | Set-Content -LiteralPath $postInstallScript -Encoding ASCII

    Invoke-VBox unattended install $VmName --iso=$IsoPath --user=$Username --user-password=$Password --admin-password=$Password --full-user-name='ServoERP QA' --install-additions --additions-iso='C:\Program Files\Oracle\VirtualBox\VBoxGuestAdditions.iso' --locale=en_US --country=US --time-zone=UTC --hostname=$unattendedHostName --post-install-template=$postInstallScript --start-vm=none
}

$sharePath = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\installer_output\enterprise')
Invoke-VBox sharedfolder add $VmName --name ServoERPInstaller --hostpath $sharePath --automount --auto-mount-point S

if ($Start) {
    $type = if ($Headless) { 'headless' } else { 'gui' }
    Invoke-VBox startvm $VmName --type $type
}

Write-Host "Created VirtualBox test VM: $VmName"
Write-Host "Guest user: $Username"
Write-Host "Installer share: $sharePath"
