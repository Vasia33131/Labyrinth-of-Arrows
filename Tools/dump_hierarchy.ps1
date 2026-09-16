# Печатает иерархию сцены Unity: имя объекта, размеры и якоря RectTransform.
# Нужен, чтобы читать вёрстку не открывая редактор.
param(
    [Parameter(Mandatory = $true)][string]$Scene,
    [int]$MaxDepth = 3
)

$enc = New-Object System.Text.UTF8Encoding($false)
$lines = [IO.File]::ReadAllLines($Scene, $enc)

$objects = @{}   # fileID -> @{ name; rect }
$rects = @{}     # fileID -> @{ owner; children; parent; anchorMin; anchorMax; sizeDelta; pos }

$curId = $null
$curType = $null
$block = $null

function Flush {
    if ($null -eq $curId) { return }
    if ($curType -eq "GameObject") { $objects[$curId] = $block }
    elseif ($curType -eq "RectTransform") { $rects[$curId] = $block }
}

for ($i = 0; $i -lt $lines.Count; $i++) {
    $l = $lines[$i]

    if ($l -match '^--- !u!(\d+) &(\d+)') {
        Flush
        $curId = $matches[2]
        $curType = $null
        $block = @{ children = New-Object System.Collections.ArrayList }
        continue
    }
    if ($null -eq $curId) { continue }

    if ($l -match '^(GameObject|RectTransform|Transform):') { $curType = $matches[1]; continue }
    if ($l -match '^\s+m_Name:\s*(.*)$') { $block.name = $matches[1].Trim() }
    elseif ($l -match '^\s+m_GameObject:\s*\{fileID:\s*(\d+)\}') { $block.owner = $matches[1] }
    elseif ($l -match '^\s+m_Father:\s*\{fileID:\s*(\d+)\}') { $block.parent = $matches[1] }
    elseif ($l -match '^\s+- \{fileID:\s*(\d+)\}' -and $curType -eq "RectTransform") { [void]$block.children.Add($matches[1]) }
    elseif ($l -match '^\s+m_AnchorMin:\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+)\}') { $block.aMin = "$($matches[1]),$($matches[2])" }
    elseif ($l -match '^\s+m_AnchorMax:\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+)\}') { $block.aMax = "$($matches[1]),$($matches[2])" }
    elseif ($l -match '^\s+m_SizeDelta:\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+)\}') { $block.size = "$($matches[1])x$($matches[2])" }
}
Flush

function Show([string]$rectId, [int]$depth) {
    if ($depth -gt $MaxDepth) { return }
    $r = $rects[$rectId]
    if ($null -eq $r) { return }
    $name = "?"
    if ($r.owner -and $objects[$r.owner]) { $name = $objects[$r.owner].name }
    $pad = " " * ($depth * 2)
    "{0}{1}  [anchors {2} .. {3}]  [size {4}]" -f $pad, $name, $r.aMin, $r.aMax, $r.size
    foreach ($c in $r.children) { Show $c ($depth + 1) }
}

# Корни: RectTransform без отца
foreach ($kv in $rects.GetEnumerator()) {
    if (-not $kv.Value.parent -or $kv.Value.parent -eq "0") { Show $kv.Key 0 }
}
