<#
.SYNOPSIS
  Generates the feature files of Showcase.Large.MSTest: four price matrices, 2000 scenario rows in total.

.DESCRIPTION
  The expected values are computed here from the shop's specification, independently of Showcase.Domain, so the
  rows test the domain for real: where the domain has a bug (three-decimal currencies, the 50-unit bulk tier) the
  generated rows fail. The output is committed; rerun this script only to change the matrices.
#>
$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot '..\Showcase.Large.MSTest\Features'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$inv = [Globalization.CultureInfo]::InvariantCulture
function Fmt([decimal]$v, [int]$decimals) { $v.ToString('F' + $decimals, $inv) }
function Round([decimal]$v, [int]$decimals) { [Math]::Round($v, $decimals, [MidpointRounding]::AwayFromZero) }

function Write-Feature([string]$file, [string]$text) {
    [IO.File]::WriteAllText((Join-Path $out $file), $text.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
}

# 1. Shipping: 5 zones x 100 weights = 500 rows
$zones = [ordered]@{ LOCAL = @(2.90, 0.50); EU = @(6.90, 1.20); UK = @(9.90, 1.80); US = @(14.90, 2.40); ASIA = @(17.90, 3.10) }
$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("@matrix @shipping`nFeature: Shipping price matrix`n  Base price per zone plus a price per kilogram, for parcels of 0.1 to 10 kg.`n")
[void]$sb.AppendLine("  Scenario Outline: <zone> parcel of <kg> kg`n    Then shipping to zone <zone> for <kg> kg costs <cost> EUR")
foreach ($zone in $zones.Keys) {
    [void]$sb.AppendLine("`n    Examples: Zone $zone`n      | zone | kg | cost |")
    for ($i = 1; $i -le 100; $i++) {
        $kg = [decimal]$i / 10
        $cost = Round ([decimal]$zones[$zone][0] + [decimal]$zones[$zone][1] * $kg) 2
        [void]$sb.AppendLine("      | $zone | $(Fmt $kg 1) | $(Fmt $cost 2) |")
    }
}
Write-Feature 'ShippingMatrix.feature' $sb.ToString()

# 2. VAT: 25 countries x 20 amounts = 500 rows
$vat = [ordered]@{ DE = 0.19; FR = 0.20; PL = 0.23; UA = 0.20; NL = 0.21; ES = 0.21; IT = 0.22; SE = 0.25; DK = 0.25; HU = 0.27;
                   AT = 0.20; BE = 0.21; CZ = 0.21; FI = 0.255; IE = 0.23; PT = 0.23; RO = 0.19; SK = 0.23; SI = 0.22; LT = 0.21;
                   LV = 0.21; EE = 0.24; GR = 0.24; HR = 0.25; LU = 0.17 }
$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("@matrix @pricing`nFeature: VAT matrix`n  VAT for every supported EU country, rounded half away from zero to the cent.`n")
[void]$sb.AppendLine("  Scenario Outline: VAT on <net> EUR in <country>`n    Then the VAT on <net> EUR in <country> is <vat> EUR")
foreach ($country in $vat.Keys) {
    [void]$sb.AppendLine("`n    Examples: $country`n      | country | net | vat |")
    for ($i = 1; $i -le 20; $i++) {
        $net = [decimal](($i * 7.37) % 250) + 0.99
        $v = Round ($net * [decimal]$vat[$country]) 2
        [void]$sb.AppendLine("      | $country | $(Fmt $net 2) | $(Fmt $v 2) |")
    }
}
Write-Feature 'VatMatrix.feature' $sb.ToString()

# 3. Rounding: 5 currencies x 100 amounts = 500 rows (the dinar has three decimals)
$decimals = [ordered]@{ EUR = 2; USD = 2; GBP = 2; JPY = 0; KWD = 3 }
$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("@matrix @payments`nFeature: Currency rounding matrix`n  Every amount is rounded to the currency's own number of decimals, half away from zero.`n")
[void]$sb.AppendLine("  Scenario Outline: <amount> in <currency>`n    Then <amount> rounded in <currency> is <rounded>")
foreach ($currency in $decimals.Keys) {
    [void]$sb.AppendLine("`n    Examples: $currency`n      | currency | amount | rounded |")
    for ($i = 1; $i -le 100; $i++) {
        $amount = [decimal](($i * 13.7531) % 997) + [decimal]0.0005 * ($i % 3)
        $r = Round $amount $decimals[$currency]
        [void]$sb.AppendLine("      | $currency | $(Fmt $amount 4) | $(Fmt $r $decimals[$currency]) |")
    }
}
Write-Feature 'RoundingMatrix.feature' $sb.ToString()

# 4. Discounts: 2 customer kinds x 2 prices x 125 quantities = 500 rows
$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("@matrix @pricing`nFeature: Discount matrix`n  The better of the member discount (10%) and the bulk discount (5% from 10, 10% from 50, 15% from 100 units).`n")
[void]$sb.AppendLine("  Scenario Outline: <kind> buying <units> x <price> EUR`n    Then a <kind> buying <units> units of <price> EUR gets <discount> EUR off")
foreach ($kind in 'member', 'guest') {
    foreach ($price in [decimal]9.90, [decimal]34.00) {
        [void]$sb.AppendLine("`n    Examples: $kind, $(Fmt $price 2) EUR`n      | kind | units | price | discount |")
        for ($units = 1; $units -le 125; $units++) {
            $bulk = if ($units -ge 100) { 0.15 } elseif ($units -ge 50) { 0.10 } elseif ($units -ge 10) { 0.05 } else { 0 }
            $loyalty = if ($kind -eq 'member') { 0.10 } else { 0 }
            $d = Round ($price * $units * [decimal][Math]::Max($bulk, $loyalty)) 2
            [void]$sb.AppendLine("      | $kind | $units | $(Fmt $price 2) | $(Fmt $d 2) |")
        }
    }
}
Write-Feature 'DiscountMatrix.feature' $sb.ToString()
Write-Host "Generated 2000 rows in $out"
