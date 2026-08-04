# test-phase5-2.ps1
# Script to build, run and verify Scheduled Publishing (Phase 5.2) in Docker environment.

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "PHASE 5.2: SCHEDULED PUBLISHING SMOKE TEST" -ForegroundColor Cyan
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

# 3. Log in as Administrator (Temporary login because password must be changed)
$loginBody = @{
    usernameOrEmail = "admin"
    password = "Admin@Comic2026Strong"
}

Write-Host "Logging in first-time as admin..." -ForegroundColor Yellow
$loginResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/login" $loginBody
$tempToken = $loginResponse.data.accessToken

# Change password as required by security policy
$changePasswordBody = @{
    currentPassword = "Admin@Comic2026Strong"
    newPassword = "Admin@Comic2026StrongNew-5"
    confirmPassword = "Admin@Comic2026StrongNew-5"
}

Write-Host "Changing forced administrator password..." -ForegroundColor Yellow
$changeResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/change-password" $changePasswordBody @{ Authorization = "Bearer $tempToken" }

# Login again with the new password to get the fully authorized token
$newLoginBody = @{
    usernameOrEmail = "admin"
    password = "Admin@Comic2026StrongNew-5"
}

Write-Host "Logging in again with the new password..." -ForegroundColor Yellow
$newLoginResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/login" $newLoginBody
$token = $newLoginResponse.data.accessToken

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Error "Login failed: No access token returned."
}

$headers = @{
    Authorization = "Bearer $token"
}

# 4. Create a Story
$storyBody = @{
    title = "Smoke Story"
    slug = "smoke-story"
    description = "A story to verify auto-publication"
}

Write-Host "Creating a story..." -ForegroundColor Yellow
$storyResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories" $storyBody $headers
$storyId = $storyResponse.data.id
$storyVersion = $storyResponse.data.version

Write-Host "Created Story ID: $storyId, Version: $storyVersion" -ForegroundColor Green

# 5. Create a Chapter
$chapterBody = @{
    chapterNumber = 1
    title = "Smoke Chapter 1"
    slug = "smoke-chapter-1"
    content = "<p>Valid HTML paragraph content for chapter.</p>"
}

Write-Host "Creating a chapter..." -ForegroundColor Yellow
$chapterResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories/$storyId/chapters" $chapterBody $headers
$chapterId = $chapterResponse.data.id
$chapterVersion = $chapterResponse.data.version

Write-Host "Created Chapter ID: $chapterId, Version: $chapterVersion" -ForegroundColor Green

# 6. Schedule both Story and Chapter in the past
# ScheduledPublishingWorker runs UTC time. Let's schedule it to 5 seconds ago in UTC.
$scheduledAt = [DateTime]::UtcNow.AddSeconds(-5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

$scheduleChapterBody = @{
    scheduledAt = $scheduledAt
    version = $chapterVersion
}

Write-Host "Scheduling Chapter $chapterId to $scheduledAt..." -ForegroundColor Yellow
$schedChapterResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/chapters/$chapterId/schedule" $scheduleChapterBody $headers

if ($schedChapterResponse.data.status -ne "Draft" -or [string]::IsNullOrEmpty($schedChapterResponse.data.scheduledAt)) {
    Write-Error "Scheduling Chapter failed."
}

$scheduleStoryBody = @{
    scheduledAt = $scheduledAt
    version = $storyVersion
}

Write-Host "Scheduling Story $storyId to $scheduledAt..." -ForegroundColor Yellow
$schedStoryResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories/$storyId/schedule" $scheduleStoryBody $headers

if ($schedStoryResponse.data.status -ne "Draft" -or [string]::IsNullOrEmpty($schedStoryResponse.data.scheduledAt)) {
    Write-Error "Scheduling Story failed."
}

Write-Host "Successfully scheduled both Story and Chapter!" -ForegroundColor Green

# 7. Wait for ScheduledPublishingWorker to publish them (interval is 10s)
Write-Host "Waiting 15 seconds for ScheduledPublishingWorker execution..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# 8. Verify statuses are updated to Published
Write-Host "Checking Chapter status..." -ForegroundColor Yellow
$checkChapter = Invoke-Json "Get" "http://localhost:8080/api/v1/admin/stories/$storyId/chapters/$chapterId" $null $headers
Write-Host "Chapter Status: $($checkChapter.data.status), ScheduledAt: $($checkChapter.data.scheduledAt)" -ForegroundColor Cyan

Write-Host "Checking Story status..." -ForegroundColor Yellow
$checkStory = Invoke-Json "Get" "http://localhost:8080/api/v1/admin/stories/$storyId" $null $headers
Write-Host "Story Status: $($checkStory.data.status), ScheduledAt: $($checkStory.data.scheduledAt)" -ForegroundColor Cyan

if ($checkChapter.data.status -eq "Published" -and $checkStory.data.status -eq "Published" -and [string]::IsNullOrEmpty($checkChapter.data.scheduledAt) -and [string]::IsNullOrEmpty($checkStory.data.scheduledAt)) {
    Write-Host "SUCCESS: Both Story and Chapter auto-published successfully!" -ForegroundColor Green
} else {
    Write-Error "FAIL: Statuses did not update correctly. Check docker-compose logs."
}

Write-Host "Stopping Docker containers..." -ForegroundColor Yellow
docker-compose down -v

Write-Host "=============================================" -ForegroundColor Green
Write-Host "PHASE 5.2 SMOKE TEST COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
