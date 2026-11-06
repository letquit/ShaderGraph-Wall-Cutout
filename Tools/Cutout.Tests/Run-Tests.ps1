param(
    [Parameter(Mandatory = $true)][string]$UnityEditor,
    [string]$TestProject
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $TestProject) {
    $TestProject = Join-Path ([IO.Path]::GetTempPath()) ('CutoutRegression-' + [guid]::NewGuid().ToString('N'))
}
$TestProject = [IO.Path]::GetFullPath($TestProject)
$marker = Join-Path $TestProject '.cutout-regression'
if ((Test-Path -LiteralPath $TestProject) -and -not (Test-Path -LiteralPath $marker)) {
    throw 'Use a new isolated test directory, or reuse one created by this runner.'
}
New-Item -ItemType Directory -Force -Path $TestProject, "$TestProject/Assets/Tests", "$TestProject/Packages", "$TestProject/ProjectSettings" | Out-Null
Set-Content -LiteralPath $marker -Value $projectRoot
Copy-Item -LiteralPath "$projectRoot/Assets/Scripts/CutoutObject.cs" -Destination "$TestProject/Assets/Tests/CutoutObject.cs"
Copy-Item -LiteralPath "$PSScriptRoot/CutoutObjectTests.cs" -Destination "$TestProject/Assets/Tests/CutoutObjectTests.cs"
Copy-Item -LiteralPath "$projectRoot/ProjectSettings/ProjectVersion.txt" -Destination "$TestProject/ProjectSettings/ProjectVersion.txt"
$dependencies = [ordered]@{'com.unity.modules.physics'='1.0.0'; 'com.unity.modules.imgui'='1.0.0'; 'com.unity.modules.jsonserialize'='1.0.0'}
foreach ($name in @('com.unity.test-framework', 'com.unity.ext.nunit')) {
    $cachedPackage = Get-ChildItem -LiteralPath "$projectRoot/Library/PackageCache" -Directory -Filter "$name@*" | Select-Object -First 1
    if (-not $cachedPackage) { throw "Missing existing package cache: $name" }
    $dependencies[$name] = 'file:' + $cachedPackage.FullName.Replace([char]92, [char]47)
}
@{dependencies=$dependencies} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$TestProject/Packages/manifest.json" -Encoding utf8
@{name='Cutout.RegressionTests'; optionalUnityReferences=@('TestAssemblies')} | ConvertTo-Json | Set-Content -LiteralPath "$TestProject/Assets/Tests/Cutout.RegressionTests.asmdef" -Encoding utf8
@'
Shader "Hidden/CutoutRegression"
{
    Properties
    {
        _CutoutPos ("Position", Vector) = (0, 0, 0, 0)
        _CutoutSize ("Size", Float) = 0
        _FalloffSize ("Falloff", Float) = 0
        _MainTexture ("Texture", 2D) = "white" {}
        _Tiling ("Tiling", Vector) = (1, 1, 0, 0)
        _Offset ("Offset", Vector) = (0, 0, 0, 0)
    }
    SubShader { Pass {} }
}
'@ | Set-Content -LiteralPath "$TestProject/Assets/Tests/CutoutRegression.shader" -Encoding utf8
@'
Shader "Hidden/CutoutSource"
{
    Properties { _BaseMap ("Texture", 2D) = "white" {} }
    SubShader { Pass {} }
}
'@ | Set-Content -LiteralPath "$TestProject/Assets/Tests/CutoutSource.shader" -Encoding utf8
$resultPath = "$TestProject/results.xml"
if (Test-Path -LiteralPath $resultPath) {
    Move-Item -LiteralPath $resultPath -Destination ("$TestProject/results-" + [guid]::NewGuid().ToString('N') + '.xml')
}
Write-Output "Isolated test project: $TestProject"
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $TestProject + '"'), '-runTests', '-testPlatform', 'PlayMode', '-testResults', ('"' + $resultPath + '"'), '-logFile', ('"' + $TestProject + '/Editor.log"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if (Test-Path -LiteralPath $resultPath) {
    [xml]$results = Get-Content -LiteralPath $resultPath -Raw
    $results.'test-run' | Select-Object result,total,passed,failed,skipped | Format-List
    Select-Xml -Xml $results -XPath '//test-case[@result="Failed"]' | ForEach-Object {
        Write-Output $_.Node.fullname
        Write-Output $_.Node.failure.message.InnerText
    }
    if ($results.'test-run'.result -ne 'Passed') { exit 1 }
} else {
    Get-Content -LiteralPath "$TestProject/Editor.log" -Tail 60
    throw 'Unity did not produce test results.'
}
exit $process.ExitCode
