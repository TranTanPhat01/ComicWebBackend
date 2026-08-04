# test-phase5-4.ps1
# Script to build, run and verify Public Cache & Response Compression (Phase 5.4) in Docker environment.

$ErrorActionPreference = "Stop"

# Helper assert function
function Assert-Equal($Condition, $Message) {
    if (-not $Condition) {
        Write-Error $Message
    }
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "PHASE 5.4: CACHING & PERFORMANCE SMOKE TEST" -ForegroundColor Cyan
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

Write-Host "API service is active! Proceeding with caching & performance smoke test..." -ForegroundColor Green

# Helper function to invoke JSON requests with basic parsing
function Invoke-Json([string]$Method, [string]$Uri, $Body, $Headers = @{}) {
    $parameters = @{ Method = $Method; Uri = $Uri; Headers = $Headers; ContentType = "application/json"; UseBasicParsing = $true }
    if ($null -ne $Body) { $parameters.Body = $Body | ConvertTo-Json }
    try {
        return Invoke-WebRequest @parameters
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

Write-Host "Logging in as admin..." -ForegroundColor Yellow
$loginRes = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/login" $loginBody
$loginData = $loginRes.Content | ConvertFrom-Json
$tempToken = $loginData.data.accessToken

# Change password as required by security policy
$changePasswordBody = @{
    currentPassword = "Admin@Comic2026Strong"
    newPassword = "Admin@Comic2026StrongNew-54"
    confirmPassword = "Admin@Comic2026StrongNew-54"
}

Write-Host "Changing forced administrator password..." -ForegroundColor Yellow
$changeResponse = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/change-password" $changePasswordBody @{ Authorization = "Bearer $tempToken" }

# Login again to get authorized token
$newLoginBody = @{
    usernameOrEmail = "admin"
    password = "Admin@Comic2026StrongNew-54"
}
$newLoginRes = Invoke-Json "Post" "http://localhost:8080/api/v1/auth/login" $newLoginBody
$newLoginData = $newLoginRes.Content | ConvertFrom-Json
$token = $newLoginData.data.accessToken

$headers = @{
    Authorization = "Bearer $token"
}

# 4. Create, publish a Story and Chapter to allow public readings
$storyBody = @{
    title = "Performance Cache Story"
    slug = "perf-cache-story"
    description = "A story to verify cache hits and invalidations"
}
Write-Host "Creating a story..." -ForegroundColor Yellow
$storyRes = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories" $storyBody $headers
$storyData = $storyRes.Content | ConvertFrom-Json
$storyId = $storyData.data.id
$storyVersion = $storyData.data.version

$chapterBody = @{
    chapterNumber = 1
    title = "Performance Cache Chapter 1"
    slug = "perf-cache-chapter-1"
    content = "<p>Super optimized HTML content.</p>"
}
Write-Host "Creating a chapter..." -ForegroundColor Yellow
$chapterRes = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories/$($storyId)/chapters" $chapterBody $headers
$chapterData = $chapterRes.Content | ConvertFrom-Json
$chapterId = $chapterData.data.id
$chapterVersion = $chapterData.data.version

# Publish Chapter & Story
Write-Host "Publishing chapter and story..." -ForegroundColor Yellow
$pubChapter = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/chapters/$($chapterId)/publish" @{ version = $chapterVersion } $headers
$pubChapterData = $pubChapter.Content | ConvertFrom-Json
$chapterVersion = $pubChapterData.data.version

$pubStory = Invoke-Json "Post" "http://localhost:8080/api/v1/admin/stories/$($storyId)/publish" @{ version = $storyVersion } $headers
$pubStoryData = $pubStory.Content | ConvertFrom-Json
$storyVersion = $pubStoryData.data.version

# 5. Measure Caching Cold vs Warm execution times
Write-Host "Verifying caching performance (Cold vs Warm)..." -ForegroundColor Yellow

$detailUrl = "http://localhost:8080/api/v1/stories/perf-cache-story"

# Cold request
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$coldRes = Invoke-WebRequest -Uri $detailUrl -Method Get -UseBasicParsing
$stopwatch.Stop()
$coldMs = $stopwatch.ElapsedMilliseconds
Write-Host "Cold Request time: $coldMs ms" -ForegroundColor Gray

# Warm request (should hit memory cache)
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$warmRes = Invoke-WebRequest -Uri $detailUrl -Method Get -UseBasicParsing
$stopwatch.Stop()
$warmMs = $stopwatch.ElapsedMilliseconds
Write-Host "Warm Request time: $warmMs ms" -ForegroundColor Gray

# ETag from Cold/Warm Response
$etag = $coldRes.Headers["ETag"]
Write-Host "Story Detail ETag: $etag" -ForegroundColor Cyan
if ($null -eq $etag) {
    Write-Error "ETag header is missing in response!"
}

# 6. Verify Conditional GET (304 Not Modified)
Write-Host "Testing Conditional GET (304 Not Modified)..." -ForegroundColor Yellow
$condHeaders = @{ "If-None-Match" = $etag }
try {
    $condRes = Invoke-WebRequest -Uri $detailUrl -Method Get -Headers $condHeaders -UseBasicParsing
    if ($condRes.StatusCode -eq 304) {
        Write-Host "Received expected 304 Not Modified!" -ForegroundColor Green
    } else {
        Write-Error "Expected 304 Not Modified, but received $($condRes.StatusCode)"
    }
} catch {
    if ($_.Exception.Response.StatusCode -eq 304) {
        Write-Host "Received expected 304 Not Modified (thrown as protocol exception)!" -ForegroundColor Green
    } else {
        throw
    }
}

# 7. Verify Cache Invalidation (Mutate story -> ETag must change and return 200 OK)
Write-Host "Mutating story to trigger invalidation..." -ForegroundColor Yellow
$updateBody = @{
    title = "Performance Cache Story Updated"
    slug = "perf-cache-story"
    description = "Updated description"
    version = $storyVersion
}
$updateRes = Invoke-Json "Put" "http://localhost:8080/api/v1/admin/stories/$($storyId)" $updateBody $headers

# Request with the old If-None-Match ETag: cache should have been evicted, returning 200 OK with new ETag!
Write-Host "Requesting detail with old If-None-Match..." -ForegroundColor Yellow
$afterInvalRes = Invoke-WebRequest -Uri $detailUrl -Method Get -Headers $condHeaders -UseBasicParsing
Assert-Equal ($afterInvalRes.StatusCode -eq 200) "Expected 200 OK after cache invalidation mutation, but got $($afterInvalRes.StatusCode)"

$newEtag = $afterInvalRes.Headers["ETag"]
Write-Host "New Story Detail ETag: $newEtag" -ForegroundColor Cyan
if ($etag -eq $newEtag) {
    Write-Error "ETag did not change after story modification! Invalidation failed."
}



# 8. Verify Response Compression (Brotli/Gzip)
Write-Host "Testing response compression headers..." -ForegroundColor Yellow
$compressHeaders = @{ "Accept-Encoding" = "gzip" }
$compressRes = Invoke-WebRequest -Uri "http://localhost:8080/api/v1/stories" -Method Get -Headers $compressHeaders -UseBasicParsing
$contentEncoding = $compressRes.Headers["Content-Encoding"]
Write-Host "Content-Encoding: $contentEncoding" -ForegroundColor Cyan
Assert-Equal ($contentEncoding -like "*gzip*") "Expected Gzip response compression, but Content-Encoding was '$contentEncoding'"

Write-Host "SUCCESS: Caching & Performance optimizations are fully operational in Docker!" -ForegroundColor Green

Write-Host "Stopping Docker containers..." -ForegroundColor Yellow
docker-compose down -v

Write-Host "=============================================" -ForegroundColor Green
Write-Host "PHASE 5.4 SMOKE TEST COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
