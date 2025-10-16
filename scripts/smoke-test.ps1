param(
    [string]$BaseUrl = "https://localhost:5001",
    [string]$UserName = "admin@localhost",
    [string]$Password = "ChangeMe!123!"
)

$ErrorActionPreference = 'Stop'

Write-Host "[SmokeTest] Target API: $BaseUrl" -ForegroundColor Cyan

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

$loginBody = @{ userNameOrEmail = $UserName; password = $Password; rememberMe = $false } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" -Body $loginBody -ContentType 'application/json' -WebSession $session | Out-Null
Write-Host "[SmokeTest] Authenticated as $UserName" -ForegroundColor Green

function Get-FirstId {
    param([string]$Type)
    $items = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/dictionaries?type=$Type" -WebSession $session
    if (-not $items -or $items.Count -eq 0) {
        throw "Dictionary '$Type' does not contain any entries"
    }
    return $items[0].id
}

$environmentId = Get-FirstId -Type 'Environment'
$wsusId = Get-FirstId -Type 'WsusPriority'
$osId = Get-FirstId -Type 'OperatingSystem'
$roleId = Get-FirstId -Type 'ServerRole'
$workstationTypeId = Get-FirstId -Type 'WorkstationType'
$locationId = Get-FirstId -Type 'Location'

$deviceTypes = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/dictionaries?type=DeviceType" -WebSession $session
if (-not $deviceTypes -or $deviceTypes.Count -eq 0) {
    $newDeviceType = @{ dictType = 'DeviceType'; key = 'SWITCH'; value = 'Switch'; description = 'Smoke seed' } | ConvertTo-Json
    $createdType = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/dictionaries" -Body $newDeviceType -ContentType 'application/json' -WebSession $session
    $deviceTypeId = $createdType.id
    Write-Host "[SmokeTest] Created dictionary entry for DeviceType." -ForegroundColor Yellow
} else {
    $deviceTypeId = $deviceTypes[0].id
}

$serverRequest = @{ 
    name = 'Smoke Server';
    inventoryNumber = 'SRV-SMOKE';
    manufacturer = 'Contoso';
    model = 'Tower 1';
    environmentId = $environmentId;
    wsusPriorityId = $wsusId;
    operatingSystemId = $osId;
    serverRoleId = $roleId;
    primaryAdministratorId = $null;
    secondaryAdministratorId = $null;
    locationId = $locationId;
    rackPosition = 'R1';
    purchasedAt = [DateTime]::UtcNow.ToString('o');
    supportUntil = $null;
    notes = 'Smoke test server';
    networkAssignments = @(@{ label = 'Primary'; vlanId = $null; ipAddress = '10.0.0.10' })
} | ConvertTo-Json

$server = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/servers" -Body $serverRequest -ContentType 'application/json' -WebSession $session
Write-Host "[SmokeTest] Created server $($server.id)" -ForegroundColor Green

$networkDeviceRequest = @{
    name = 'Smoke Switch';
    inventoryNumber = 'NET-SMOKE';
    deviceTypeId = $deviceTypeId;
    manufacturer = 'Contoso';
    model = 'Switch 24';
    locationId = $locationId;
    rackPosition = 'R1';
    primaryAdministratorId = $null;
    secondaryAdministratorId = $null;
    supportUntil = $null;
    notes = 'Smoke test network device';
    networkAssignments = @(@{ label = 'Uplink'; vlanId = $null; ipAddress = '10.0.0.1' })
} | ConvertTo-Json

$networkDevice = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/network-devices" -Body $networkDeviceRequest -ContentType 'application/json' -WebSession $session
Write-Host "[SmokeTest] Created network device $($networkDevice.id)" -ForegroundColor Green

$workstationRequest = @{
    name = 'Smoke Workstation';
    inventoryNumber = 'WRK-SMOKE';
    operatingSystemId = $osId;
    workstationTypeId = $workstationTypeId;
    ownerId = $null;
    ownerDisplayName = 'Smoke Tester';
    ownerDepartment = 'QA';
    locationId = $locationId;
    locationNote = 'Desk 5';
    cpu = 'Intel i5';
    ram = '16 GB';
    storage = '512 GB SSD';
    macAddress = 'AA-BB-CC-00-11-22';
    purchasedAt = [DateTime]::UtcNow.AddMonths(-1).ToString('o');
    supportUntil = $null;
    primaryAdministratorId = $null;
    secondaryAdministratorId = $null;
    notes = 'Smoke test workstation';
    networkAssignments = @(@{ label = 'LAN'; vlanId = $null; ipAddress = '10.0.0.50' })
} | ConvertTo-Json

$workstation = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/workstations" -Body $workstationRequest -ContentType 'application/json' -WebSession $session
Write-Host "[SmokeTest] Created workstation $($workstation.id)" -ForegroundColor Green

$serversList = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/servers" -WebSession $session
$networkList = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/network-devices" -WebSession $session
$workstationList = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/workstations" -WebSession $session

Write-Host "[SmokeTest] Inventory counts => Servers: $($serversList.Items.Count), Network: $($networkList.Items.Count), Workstations: $($workstationList.Items.Count)" -ForegroundColor Cyan

$audit = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/audit?entityType=Server&size=5" -WebSession $session
if ($audit.Items.Count -gt 0) {
    Write-Host "[SmokeTest] Audit log captured last action at $($audit.Items[0].PerformedAtUtc)." -ForegroundColor Green
} else {
    Write-Warning "[SmokeTest] Audit log did not return entries."
}

Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/logout" -WebSession $session | Out-Null
Write-Host "[SmokeTest] Completed. Logout successful." -ForegroundColor Green
