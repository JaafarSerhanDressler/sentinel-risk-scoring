# Generates a styled, elaborated .xlsx timesheet (no external modules).
$ErrorActionPreference = 'Stop'

# ---- Data: Date, Day, Category, Task (elaborated), Hours ----
$rows = @(
  @('2026-11-02','Mon','Analysis', 'Requirements review and architecture decisions: selected .NET 10 with Blazor WebAssembly as an installable PWA, fronted by an ASP.NET Core API that hosts it on a single origin (no CORS). Defined the solution layout, naming and run conventions.',1.0),
  @('2026-11-02','Mon','Setup',    'Scaffolded the solution end to end: RiskScoring.Client (Blazor WASM PWA), RiskScoring.Api (hosts the PWA + /api endpoints) and RiskScoring.Shared (models/DTOs). Wired project references so the whole app builds and runs with a single command.',1.5),
  @('2026-11-02','Mon','UI',       'Built the dark, clinical design system (CSS variables, cards, buttons, badges, pill stats) and the responsive app shell - desktop sidebar plus mobile bottom-nav with a central action button - including page-transition animations.',1.5),
  @('2026-11-03','Tue','UI',       'Authored the reusable animated component library: SVG risk gauge with count-up, donut (risk distribution), line (trend) and bar (throughput) charts, KPI cards, risk/alert badges and shimmer skeleton loaders.',2.0),
  @('2026-11-03','Tue','UI',       'Assembled the dashboard layout and the login / splash screen (brand hero, value props, demo sign-in and role hint).',1.0),
  @('2026-11-03','Tue','PWA',      'Configured the progressive web app: web manifest, service worker, icons and an in-app install prompt; verified installability and the offline app shell.',1.0),
  @('2026-11-04','Wed','UI',       'Built the participant list (search, risk + emergency filters, risk-sorted rows) and the participant detail screen (gauge, contributing factors, 12-week trend, recommended actions).',1.5),
  @('2026-11-04','Wed','Backend',  'Modelled the shared domain and DTOs used by both the client and the API, guaranteeing type-safe JSON serialization across the wire.',1.0),
  @('2026-11-04','Wed','UI',       'Implemented the Run Assessment journey: the stepped ML Fabric processing overlay/animation while scoring, followed by the animated result view.',1.5),
  @('2026-11-05','Thu','Backend',  'Built the ML Fabric mock scoring service (feature assembly from data sources, simulated inference latency, factor attribution) and the REST API endpoints for participants, dashboard aggregates and assessments.',2.0),
  @('2026-11-05','Thu','Backend',  'Created the admin / ML Fabric console (model metrics, uptime), the append-only audit log and the data-source health view with connected/degraded/offline status.',1.5),
  @('2026-11-05','Thu','Data',     'Seeded the initial in-memory synthetic dataset and wired the dashboards, lists and detail pages to read from it.',0.5),
  @('2026-11-06','Fri','Docs',     'Produced a standalone HTML guide documenting the architecture, personas, end-to-end user journeys, screen-by-screen flows and core use cases.',1.0),
  @('2026-11-06','Fri','UI',       'Polished the experience: micro-animations, chart reveal/draw effects, skeleton loaders and responsive QA across mobile and desktop breakpoints.',1.5),
  @('2026-11-06','Fri','QA',       'End-to-end browser testing and fixes: verified build and run, fixed inline-SVG icon sizing, and confirmed every screen and API endpoint live in the browser.',1.5),
  @('2026-11-09','Mon','Analysis', 'Read and analysed the three discovery documents (technical spec, Fabric-to-app data contract, and the app data dictionary) and mapped their requirements onto the app.',1.0),
  @('2026-11-09','Mon','Backend',  'Rebuilt the domain to match the spec: participants across the BH RES, ICMS, SH and SH SUBONLY programmes, each scored for three risk types (dangerous behaviour, medication refusal, medication non-compliance) with probability, severity and band.',1.5),
  @('2026-11-09','Mon','Data',     'Implemented alerts (emergency/warning plus the six states), behaviour and medication events, data-reliability flags, and synthetic data covering every required case: emergencies, no case manager, insufficient data, a 150+ day refusal streak, hospitalised (on hold), risk dropped to ready-to-close, and failed/late Fabric runs.',1.5)
)
$dayCount = ($rows | ForEach-Object { $_[0] } | Sort-Object -Unique).Count

function XmlEsc($s) { ($s -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;' -replace '"','&quot;') }
function TextCell($ref,$val,$style) {
  $st = if ($style) { " s=`"$style`"" } else { "" }
  "<c r=`"$ref`"$st t=`"inlineStr`"><is><t xml:space=`"preserve`">$(XmlEsc $val)</t></is></c>"
}
function NumCell($ref,$val,$style) {
  $st = if ($style) { " s=`"$style`"" } else { "" }
  "<c r=`"$ref`"$st><v>$val</v></c>"
}

$cols = 'A','B','C','D','E'
$sb = [System.Text.StringBuilder]::new()

# Row 1: title (style 4), overflows across empty cells
[void]$sb.Append('<row r="1">')
[void]$sb.Append((TextCell 'A1' 'Sentinel - Participant Risk Response | Weekly Timesheet (Mon 2 Nov - Mon 9 Nov 2026)' 4))
[void]$sb.Append('</row>')

# Row 2: header (style 1)
[void]$sb.Append('<row r="2">')
$h = 'Date','Day','Category','Task','Hours'
for ($i=0;$i -lt 5;$i++) { [void]$sb.Append((TextCell "$($cols[$i])2" $h[$i] 1)) }
[void]$sb.Append('</row>')

# Detail rows (Task column uses wrap style 3)
$r = 3
foreach ($row in $rows) {
  [void]$sb.Append("<row r=`"$r`">")
  [void]$sb.Append((TextCell "A$r" $row[0] 0))
  [void]$sb.Append((TextCell "B$r" $row[1] 0))
  [void]$sb.Append((TextCell "C$r" $row[2] 0))
  [void]$sb.Append((TextCell "D$r" $row[3] 3))
  [void]$sb.Append((NumCell  "E$r" $row[4] 0))
  [void]$sb.Append('</row>')
  $r++
}

# Total
$total = ($rows | ForEach-Object { $_[4] } | Measure-Object -Sum).Sum
[void]$sb.Append("<row r=`"$r`">")
[void]$sb.Append((TextCell "D$r" "TOTAL  ($dayCount working days x 4h)" 2))
[void]$sb.Append((NumCell  "E$r" $total 2))
[void]$sb.Append('</row>')
$r += 2

# Summary by category
[void]$sb.Append("<row r=`"$r`">")
[void]$sb.Append((TextCell "A$r" 'Summary by category' 1))
[void]$sb.Append((TextCell "B$r" 'Hours' 1))
[void]$sb.Append('</row>')
$r++
$cats = $rows | Group-Object { $_[2] } | ForEach-Object {
  [pscustomobject]@{ Cat = $_.Name; Hrs = ($_.Group | ForEach-Object { $_[4] } | Measure-Object -Sum).Sum }
} | Sort-Object -Property Hrs -Descending
foreach ($c in $cats) {
  [void]$sb.Append("<row r=`"$r`">")
  [void]$sb.Append((TextCell "A$r" $c.Cat 0))
  [void]$sb.Append((NumCell  "B$r" $c.Hrs 0))
  [void]$sb.Append('</row>')
  $r++
}

$sheet = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<cols>
<col min="1" max="1" width="12" customWidth="1"/>
<col min="2" max="2" width="6" customWidth="1"/>
<col min="3" max="3" width="12" customWidth="1"/>
<col min="4" max="4" width="95" customWidth="1"/>
<col min="5" max="5" width="8" customWidth="1"/>
</cols>
<sheetData>$($sb.ToString())</sheetData>
<mergeCells count="1"><mergeCell ref="A1:E1"/></mergeCells>
</worksheet>
"@

$contentTypes = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>
"@
$relsRoot = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
"@
$workbook = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheets><sheet name="Timesheet" sheetId="1" r:id="rId1"/></sheets>
</workbook>
"@
$workbookRels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>
"@
$styles = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<fonts count="4">
<font><sz val="11"/><name val="Calibri"/></font>
<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
<font><b/><sz val="11"/><name val="Calibri"/></font>
<font><b/><sz val="14"/><color rgb="FF4F46E5"/><name val="Calibri"/></font>
</fonts>
<fills count="3">
<fill><patternFill patternType="none"/></fill>
<fill><patternFill patternType="gray125"/></fill>
<fill><patternFill patternType="solid"><fgColor rgb="FF4F46E5"/><bgColor indexed="64"/></patternFill></fill>
</fills>
<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="5">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
<xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment wrapText="1" vertical="top"/></xf>
<xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/>
</cellXfs>
</styleSheet>
"@

$tmp = Join-Path $env:TEMP ("xlsx_" + [guid]::NewGuid().ToString('N'))
foreach ($p in @('', '_rels', 'xl', 'xl/_rels', 'xl/worksheets')) { New-Item -ItemType Directory -Path (Join-Path $tmp $p) | Out-Null }
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $tmp '[Content_Types].xml'), $contentTypes, $utf8)
[System.IO.File]::WriteAllText((Join-Path $tmp '_rels/.rels'), $relsRoot, $utf8)
[System.IO.File]::WriteAllText((Join-Path $tmp 'xl/workbook.xml'), $workbook, $utf8)
[System.IO.File]::WriteAllText((Join-Path $tmp 'xl/_rels/workbook.xml.rels'), $workbookRels, $utf8)
[System.IO.File]::WriteAllText((Join-Path $tmp 'xl/styles.xml'), $styles, $utf8)
[System.IO.File]::WriteAllText((Join-Path $tmp 'xl/worksheets/sheet1.xml'), $sheet, $utf8)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$targets = @()
$d1 = Join-Path $env:USERPROFILE 'Desktop'; if (Test-Path $d1) { $targets += (Join-Path $d1 'Sentinel-Weekly-Timesheet.xlsx') }
$d2 = "C:\Users\JaafarSerhan\OneDrive - Alcumus Holdings Limited\Desktop"; if (Test-Path $d2) { $targets += (Join-Path $d2 'Sentinel-Weekly-Timesheet.xlsx') }
if ($targets.Count -eq 0) { $targets += (Join-Path $env:USERPROFILE 'Sentinel-Weekly-Timesheet.xlsx') }
$first = $targets[0]; if (Test-Path $first) { Remove-Item $first -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($tmp, $first)
foreach ($t in $targets | Select-Object -Skip 1) { Copy-Item $first $t -Force }
Remove-Item $tmp -Recurse -Force
"Days: $dayCount   Total hours: $total"
$targets | ForEach-Object { "Saved: $_" }
