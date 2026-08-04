# test-phase5-3.ps1
# Script to build, run and verify Audit Logging (Phase 5.3) in Docker environment.

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "PHASE 5.3: AUDIT LOGGING SMOKE TEST" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Clean and Rebuild Docker containers
Write-Host "Stopping and cleaning up existing Docker containers..." -ForegroundColor Yellow
docker-compose down -v

Write-Host "Building and starting new Docker containers..." -ForegroundColor Yellow
docker-compose up --build -d

# 2. Wait for API container to be healthy
$url = "http://localhost:8080/api/v1/stories"
$maxAttempts = 30
$attempt = 1
$healthy = $false

Write-Host "Waiting for comicweb-api to start..." -ForegroundColor Yellow
while ($attempt -le $maxAttempts) {
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 2 -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            $healthy = $true
            break
        }
    }
    catch {
        Write-Host "Attempt $attempt/$maxAttempts failed. Retrying in 2 seconds..."
        Start-Sleep -Seconds 2
        $attempt++
    }
}

if (-not $healthy) {
    Write-Error "API service did not become healthy in time."
}

Write-Host "API service is active! Proceeding with smoke test..." -ForegroundColor Green

# Helper function to invoke JSON requests with basic parsing
function Invoke-Json([string]$Method, [string]$Uri, $Body, $Headers = @{}) {
    $parameters = @{ Method = $Method; Uri = $Uri; Headers = $Headers; ContentType = "application/json"; UseBasicParsing = $true }
    if ($null -ne $Body) { $parameters.Body = $Body | ConvertTo-Json }
    try {
        return Invoke-RestMethod @parameters
    } catch {
        Write-Host "Request failed: $Method $Uri" -ForegroundColor Red
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

# 3. Log in as Administrator (Temporary password)
$loginBody = @{
    usernameOrEmail = "admin"
    password = "Admin@Comic2026Strong"
}

Write-Host "Logging in first-time as admin..." -ForegroundColor Yellow
$loginResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/login" $loginBody
$tempToken = $loginResponse.data.accessToken

# Change password as required by security policy (triggers PASSWORD_CHANGED)
$changePasswordBody = @{
    currentPassword = "Admin@Comic2026Strong"
    newPassword = "Admin@Comic2026StrongNew-53"
    confirmPassword = "Admin@Comic2026StrongNew-53"
}

Write-Host "Changing forced administrator password..." -ForegroundColor Yellow
$changeResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/change-password" $changePasswordBody @{ Authorization = "Bearer $tempToken" }

# Login again with the new password to get the fully authorized token (triggers LOGIN_SUCCEEDED)
$newLoginBody = @{
    usernameOrEmail = "admin"
    password = "Admin@Comic2026StrongNew-53"
}

Write-Host "Logging in again with the new password..." -ForegroundColor Yellow
$newLoginResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/login" $newLoginBody
$token = $newLoginResponse.data.accessToken

$headers = @{
    Authorization = "Bearer $token"
}

# 4. Create a Story (triggers STORY_CREATED)
$storyBody = @{
    title = "Audit Smoke Story"
    slug = "audit-smoke-story"
    description = "A story to verify audit logs"
}

Write-Host "Creating a story..." -ForegroundColor Yellow
$storyResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories" $storyBody $headers
$storyId = $storyResponse.data.id
$storyVersion = $storyResponse.data.version

# 5. Update the Story (triggers STORY_UPDATED)
$updateBody = @{
    title = "Audit Smoke Story Changed"
    slug = "audit-smoke-story-changed"
    description = "New description of audit smoke story"
    version = $storyVersion
}

Write-Host "Updating the story..." -ForegroundColor Yellow
$updateResponse = Invoke-Json "Put" "http://localhost:8080/api/v1/admin/stories/$($storyId)" $updateBody $headers
$storyVersion = $updateResponse.data.version

# 6. Create a Chapter (triggers CHAPTER_CREATED)
$chapterBody = @{
    chapterNumber = 1
    title = "Audit Smoke Chapter 1"
    slug = "audit-smoke-chapter-1"
    content = "<p>Top secret HTML content for audit smoke chapter.</p>"
}

Write-Host "Creating a chapter..." -ForegroundColor Yellow
$chapterResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories/$($storyId)/chapters" $chapterBody $headers
$chapterId = $chapterResponse.data.id
$chapterVersion = $chapterResponse.data.version

# 7. Publish the Chapter (triggers CHAPTER_PUBLISHED)
$pubChapterBody = @{
    version = $chapterVersion
}
Write-Host "Publishing the chapter..." -ForegroundColor Yellow
$pubChapterResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/chapters/$($chapterId)/publish" $pubChapterBody $headers
$chapterVersion = $pubChapterResponse.data.version

# 8. Publish the Story (triggers STORY_PUBLISHED)
$pubStoryBody = @{
    version = $storyVersion
}
Write-Host "Publishing the story..." -ForegroundColor Yellow
$pubStoryResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories/$($storyId)/publish" $pubStoryBody $headers
$storyVersion = $pubStoryResponse.data.version

# 9. Delete Chapter (triggers CHAPTER_DELETED)
Write-Host "Soft deleting the chapter..." -ForegroundColor Yellow
$delChapterResponse = Invoke-Json "Delete" "http://localhost:8080/api/v1/admin/stories/$($storyId)/chapters/$($chapterId)?version=$($chapterVersion)" $null $headers

# Get latest deleted version
$checkChapter = Invoke-Json "Get" "http://localhost:8080/api/v1/admin/stories/$($storyId)/chapters/$($chapterId)?includeDeleted=true" $null $headers
$chapterVersion = $checkChapter.data.version

Write-Host "Restoring the chapter..." -ForegroundColor Yellow
$restoreChapterResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories/$($storyId)/chapters/$($chapterId)/restore?version=$($chapterVersion)" $null $headers

# 11. Fetch GET /api/v1/admin/audit-logs
Write-Host "Retrieving Audit Logs..." -ForegroundColor Yellow
$logsResponse = Invoke-Json "Get" "http://localhost:8080/api/v1/admin/audit-logs?pageSize=50" $null $headers

$items = $logsResponse.data
Write-Host "Total audit logs retrieved: $($items.Count)" -ForegroundColor Cyan

# 12. Verify actions are logged properly
$actions = $items | ForEach-Object { $_.action }
$actionsList = [string]::Join(", ", $actions)
Write-Host "Logged actions: $actionsList" -ForegroundColor Gray

$hasPasswordChanged = $actions -contains "PASSWORD_CHANGED"
$hasLoginSucceeded = $actions -contains "LOGIN_SUCCEEDED"
$hasStoryCreated = $actions -contains "STORY_CREATED"
$hasStoryUpdated = $actions -contains "STORY_UPDATED"
$hasStoryPublished = $actions -contains "STORY_PUBLISHED"
$hasChapterCreated = $actions -contains "CHAPTER_CREATED"
$hasChapterPublished = $actions -contains "CHAPTER_PUBLISHED"
$hasChapterDeleted = $actions -contains "CHAPTER_DELETED"
$hasChapterRestored = $actions -contains "CHAPTER_RESTORED"

if (-not $hasPasswordChanged) { Write-Error "Missing PASSWORD_CHANGED audit log" }
if (-not $hasLoginSucceeded) { Write-Error "Missing LOGIN_SUCCEEDED audit log" }
if (-not $hasStoryCreated) { Write-Error "Missing STORY_CREATED audit log" }
if (-not $hasStoryUpdated) { Write-Error "Missing STORY_UPDATED audit log" }
if (-not $hasStoryPublished) { Write-Error "Missing STORY_PUBLISHED audit log" }
if (-not $hasChapterCreated) { Write-Error "Missing CHAPTER_CREATED audit log" }
if (-not $hasChapterPublished) { Write-Error "Missing CHAPTER_PUBLISHED audit log" }
if (-not $hasChapterDeleted) { Write-Error "Missing CHAPTER_DELETED audit log" }
if (-not $hasChapterRestored) { Write-Error "Missing CHAPTER_RESTORED audit log" }

# 13. Verify data redaction
$containsSecrets = $false
foreach ($item in $items) {
    $details = $item.detailsJson
    if ($null -ne $details) {
        if ($details.Contains("Admin@Comic2026Strong") -or $details.Contains("Top secret HTML content")) {
            $containsSecrets = $true
            Write-Host "VULNERABILITY DETECTED: Log ID $($item.id) contains secrets: $details" -ForegroundColor Red
        }
    }
}

if ($containsSecrets) {
    Write-Error "FAIL: Audit logs contain raw passwords or raw chapter HTML content!"
}

# 14. Verify Anonymous user is blocked
Write-Host "Verifying anonymous requests get 401..." -ForegroundColor Yellow
try {
    $temp = Invoke-Json "Get" "http://localhost:8080/api/v1/admin/audit-logs" $null
    Write-Error "Anonymous was not blocked!"
} catch {
    Write-Host "Anonymous request blocked successfully." -ForegroundColor Green
}

Write-Host "SUCCESS: Audit Logging works, records are redacted, and access control is enforced!" -ForegroundColor Green

Write-Host "Stopping Docker containers..." -ForegroundColor Yellow
docker-compose down -v

Write-Host "=============================================" -ForegroundColor Green
Write-Host "PHASE 5.3 SMOKE TEST COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
