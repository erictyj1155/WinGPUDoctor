# Deterministic validation of synthetic reports only. Requires PowerShell 7.
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$schema=Join-Path $projectRoot 'schemas\report-0.1.0.schema.json'
$json=Get-Content -LiteralPath (Join-Path $projectRoot 'examples\report.example.json') -Raw
if(!(Test-Json -Json $json -SchemaFile $schema)){throw 'Synthetic example does not match report schema.'}
$invalid=$json | ConvertFrom-Json -AsHashtable
$invalid.facts.gpus.state='failed'
if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 30) -SchemaFile $schema -ErrorAction SilentlyContinue){throw 'Schema accepted a failed inventory with values.'}
$invalid=$json | ConvertFrom-Json -AsHashtable
$invalid['hostname']='SYNTHETIC-HOST'
if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 30) -SchemaFile $schema -ErrorAction SilentlyContinue){throw 'Schema accepted an undeclared identity field.'}
'Schema checks: PASS (valid synthetic report, contradictory state rejected, undeclared field rejected).'
