param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$AdminPassword = $env:COMICWEB_TEST_ADMIN_PASSWORD
)

$ErrorActionPreference = "Stop"

function Assert-Status([int]$Actual, [int]$Expected, [string]$Step) {
    if ($Actual -ne $Expected) {
        throw "$Step expected HTTP $Expected but received $Actual."
    }
}

function Get-HttpStatusCode($errorRecord) {
    if ($null -eq $errorRecord) { return 0 }
    $e = $errorRecord.Exception
    while ($null -ne $e) {
        if ($null -ne $e.Response) {
            return [int]$e.Response.StatusCode
        }
        if ($e.GetType().Name -eq "ActionPreferenceStopException" -and $null -ne $errorRecord.ErrorRecord) {
            $e = $errorRecord.ErrorRecord.Exception
            continue
        }
        $e = $e.InnerException
    }
    return 0
}

function Invoke-Json([string]$Method, [string]$Uri, $Body, $Headers = @{}) {
    $parameters = @{ Method = $Method; Uri = $Uri; Headers = $Headers; ContentType = "application/json" }
    if ($null -ne $Body) { $parameters.Body = $Body | ConvertTo-Json -Depth 8 }
    try {
        return Invoke-RestMethod @parameters
    } catch {
        Write-Host "Invoke-Json failed for $Method $Uri" -ForegroundColor Red
        Write-Host $_ -ForegroundColor Red
        if ($null -ne $_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                Write-Host "Response body: $($reader.ReadToEnd())" -ForegroundColor Red
            }
        }
        throw
    }
}

if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
    throw "Set COMICWEB_TEST_ADMIN_PASSWORD before running this script."
}

Write-Host "Checking Swagger and public stories..."
Assert-Status (Invoke-WebRequest -UseBasicParsing "$BaseUrl/swagger/v1/swagger.json").StatusCode 200 "Swagger"
Assert-Status (Invoke-WebRequest -UseBasicParsing "$BaseUrl/api/v1/stories").StatusCode 200 "Public stories"

Write-Host "Logging in admin..."
$login = Invoke-Json "Post" "$BaseUrl/api/v1/auth/login" @{ usernameOrEmail = "admin"; password = $AdminPassword }
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }

if ($login.data.mustChangePassword) {
    $updatedPassword = "$AdminPassword-Phase4"
    Write-Host "Changing bootstrap admin password..."
    Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$BaseUrl/api/v1/auth/change-password" -Headers $headers -ContentType "application/json" -Body (@{ currentPassword = $AdminPassword; newPassword = $updatedPassword; confirmPassword = $updatedPassword } | ConvertTo-Json) | Out-Null
    $AdminPassword = $updatedPassword
    $login = Invoke-Json "Post" "$BaseUrl/api/v1/auth/login" @{ usernameOrEmail = "admin"; password = $AdminPassword }
    $headers = @{ Authorization = "Bearer $($login.data.accessToken)" }
}

$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

function Assert-NotFound([string]$Uri, [string]$Step) {
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -Uri $Uri -Method Get -ErrorAction Stop
        throw "$Step expected HTTP 404 but received $($resp.StatusCode)"
    } catch {
        $statusCode = Get-HttpStatusCode $_
        if ($statusCode -ne 404) {
            throw "$Step expected HTTP 404 but received $statusCode. Full Exception: $_"
        }
    }
}

Write-Host "Creating story and chapters..."
$story = Invoke-Json "Post" "$BaseUrl/api/v1/admin/stories" @{ title = "Phase4-$suffix"; slug = "phase4-$suffix"; description = "Acceptance"; authorName = "Acceptance" } $headers
$storyId = $story.data.id
$storySlug = $story.data.slug
$chapters = @(1, 3, 7 | ForEach-Object {
    Invoke-Json "Post" "$BaseUrl/api/v1/admin/stories/$storyId/chapters" @{ chapterNumber = $_; title = "Chapter $_"; slug = "chapter-$_-$suffix"; content = "Content $_" } $headers
})

Write-Host "Publishing chapters and story..."
$chapters | ForEach-Object { Invoke-Json "Post" "$BaseUrl/api/v1/admin/chapters/$($_.data.id)/publish" @{ version = $_.data.version } $headers | Out-Null }
$adminStory = Invoke-Json "Get" "$BaseUrl/api/v1/admin/stories/$storyId" $null $headers
Invoke-Json "Post" "$BaseUrl/api/v1/admin/stories/$storyId/publish" @{ version = $adminStory.data.version } $headers | Out-Null

Write-Host "Checking projection and navigation..."
$publicList = Invoke-Json "Get" "$BaseUrl/api/v1/stories" $null
$publicStory = @($publicList.data | Where-Object { $_.slug -eq $storySlug })[0]
if ($null -eq $publicStory) { throw "Story was not found in public list." }
if ($publicStory.chapterCount -ne 3) { throw "chapterCount assertion failed: expected 3 but got $($publicStory.chapterCount)." }
if ($publicStory.latestChapter.number -ne 7) { throw "latestChapter assertion failed: expected 7 but got $($publicStory.latestChapter.number)." }

$chapterOne = $chapters | Where-Object { $_.data.chapterNumber -eq 1 }
$chapterThree = $chapters | Where-Object { $_.data.chapterNumber -eq 3 }
$chapterSeven = $chapters | Where-Object { $_.data.chapterNumber -eq 7 }

$detail3 = Invoke-Json "Get" "$BaseUrl/api/v1/stories/$storySlug/chapters/$($chapterThree.data.slug)" $null
if ($detail3.data.previousChapter.number -ne 1) { throw "Chapter 3 previousChapter expected 1 but got $($detail3.data.previousChapter.number)." }
if ($detail3.data.nextChapter.number -ne 7) { throw "Chapter 3 nextChapter expected 7 but got $($detail3.data.nextChapter.number)." }

Write-Host "Hiding Chapter 3..."
$adminC3 = Invoke-Json "Get" "$BaseUrl/api/v1/admin/stories/$storyId/chapters/$($chapterThree.data.id)" $null $headers
Invoke-Json "Post" "$BaseUrl/api/v1/admin/chapters/$($chapterThree.data.id)/hide" @{ version = $adminC3.data.version } $headers | Out-Null

Write-Host "Verifying Chapter 3 public returns 404..."
Assert-NotFound "$BaseUrl/api/v1/stories/$storySlug/chapters/$($chapterThree.data.slug)" "Public chapter 3 after hiding"

Write-Host "Verifying Chapter 1 navigation updates: next = 7..."
$detail1 = Invoke-Json "Get" "$BaseUrl/api/v1/stories/$storySlug/chapters/$($chapterOne.data.slug)" $null
if ($detail1.data.nextChapter.number -ne 7) { throw "Chapter 1 nextChapter expected 7 but got $($detail1.data.nextChapter.number)." }

Write-Host "Verifying Chapter 7 navigation updates: previous = 1..."
$detail7 = Invoke-Json "Get" "$BaseUrl/api/v1/stories/$storySlug/chapters/$($chapterSeven.data.slug)" $null
if ($detail7.data.previousChapter.number -ne 1) { throw "Chapter 7 previousChapter expected 1 but got $($detail7.data.previousChapter.number)." }

Write-Host "Hiding/unpublishing Story..."
$adminStory = Invoke-Json "Get" "$BaseUrl/api/v1/admin/stories/${storyId}" $null $headers
Invoke-Json "Post" "$BaseUrl/api/v1/admin/stories/${storyId}/unpublish" @{ version = $adminStory.data.version } $headers | Out-Null

Write-Host "Verifying Public Story returns 404..."
Assert-NotFound "$BaseUrl/api/v1/stories/${storySlug}" "Public story detail after unpublishing"

Write-Host "Verifying Public Chapters return 404..."
Assert-NotFound "$BaseUrl/api/v1/stories/${storySlug}/chapters/$($chapterOne.data.slug)" "Public chapter 1 after unpublishing story"

Write-Host "Verifying delete and restore workflow..."
$adminStory = Invoke-Json "Get" "$BaseUrl/api/v1/admin/stories/${storyId}" $null $headers
Invoke-Json "Delete" "$BaseUrl/api/v1/admin/stories/${storyId}?version=$($adminStory.data.version)" $null $headers | Out-Null

Write-Host "Verifying soft-deleted story is not found on normal admin GET..."
try {
    Invoke-Json "Get" "$BaseUrl/api/v1/admin/stories/${storyId}" $null $headers | Out-Null
    throw "Admin GET on deleted story without includeDeleted expected 404 but got success."
} catch {
    $statusCode = Get-HttpStatusCode $_
    if ($statusCode -ne 404) { throw "Admin GET expected 404 but got $statusCode." }
}

Write-Host "Restoring the story..."
$adminStoryDeleted = Invoke-Json "Get" "$BaseUrl/api/v1/admin/stories/${storyId}?includeDeleted=true" $null $headers
Invoke-Json "Post" "$BaseUrl/api/v1/admin/stories/${storyId}/restore?version=$($adminStoryDeleted.data.version)" $null $headers | Out-Null

Write-Host "Publishing the story again..."
$adminStoryRestored = Invoke-Json "Get" "$BaseUrl/api/v1/admin/stories/${storyId}" $null $headers
Invoke-Json "Post" "$BaseUrl/api/v1/admin/stories/${storyId}/publish" @{ version = $adminStoryRestored.data.version } $headers | Out-Null

Write-Host "Verifying Public Story is active again..."
$publicStoryActive = Invoke-Json "Get" "$BaseUrl/api/v1/stories/${storySlug}" $null
if ($publicStoryActive.data.slug -ne $storySlug) { throw "Public story slug after restore/publish does not match." }

Write-Host "Phase 4 acceptance passed."
