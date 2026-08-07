# COMICWEB — MASTER IMPLEMENTATION PLAN FOR ANTIGRAVITY

> Mục tiêu: dùng file này làm nguồn hướng dẫn triển khai tuần tự để hoàn thiện toàn bộ hệ thống ComicWeb.
>
> Nguyên tắc: **audit source hiện tại trước, triển khai theo phase, không tự ý mở rộng scope, không tuyên bố hoàn thành nếu chưa có evidence từ build/test/runtime.**

---

# 0. SYSTEM CONTEXT

## Frontend

- Framework: Next.js 16 App Router
- React 19
- TypeScript
- Styling: Vanilla CSS / custom design system
- HTTP client: native fetch
- Auth: HTTP-only cookies
- State: React hooks
- localStorage:
  - bookmarks
  - reading history
  - reader settings
- Next.js Route Handlers đang đóng vai trò proxy/BFF cho Admin/Auth.

FE path:

```text
d:\Hai\ComicWebFrontEnd
```

## Backend

- ASP.NET Core 9 Web API
- Clean Architecture:
  - Domain
  - Application
  - Persistence
  - Presentation
- CQRS + MediatR
- EF Core 9
- PostgreSQL
- Redis optional + MemoryCache fallback
- JWT Bearer
- Refresh Token Rotation
- BCrypt
- Rate Limiter
- Audit Logging
- Scheduled Publishing Background Worker
- Scraper
- Brotli/Gzip
- ETag / Cache-Control
- Docker / Render
- GitHub Actions

BE path:

```text
d:\Hai\ComicWebBackend
```

---

# 1. GLOBAL IMPLEMENTATION RULES

Các rule sau áp dụng cho mọi phase.

## 1.1 Source of truth

Source code hiện tại là source of truth.

Báo cáo audit chỉ dùng làm reference.

Trước khi sửa:

1. đọc source liên quan;
2. xác nhận implementation hiện tại;
3. xác nhận FE đang gọi endpoint nào;
4. xác nhận data model;
5. xác nhận authorization;
6. xác nhận test hiện có.

Không được triển khai chỉ dựa trên assumption.

---

## 1.2 Không rewrite kiến trúc

Không:

- rewrite project;
- chuyển sang microservices;
- thay Clean Architecture;
- thay MediatR/CQRS;
- thay PostgreSQL;
- thêm Kafka;
- thêm Elasticsearch;
- thêm Kubernetes;
- thêm dependency lớn không cần thiết.

Giữ kiến trúc modular monolith hiện tại.

---

## 1.3 Scope control

Mỗi phase chỉ xử lý scope được mô tả.

Nếu phát hiện vấn đề ngoài scope:

```text
OUT_OF_SCOPE_FINDINGS
```

Ghi:

- vấn đề;
- severity;
- file liên quan;
- đề xuất phase xử lý.

Không tự ý sửa trừ khi vấn đề trực tiếp block phase hiện tại.

---

## 1.4 Definition of Done chung

Sau mỗi phase phải chạy tối thiểu:

### Backend

```text
dotnet restore
dotnet build
dotnet test
```

Nếu có migration:

```text
dotnet ef migrations ...
```

phải xác nhận migration tạo hợp lệ.

### Frontend

Chạy theo package manager thực tế của repo:

```text
npm install
npm run lint
npm run build
```

hoặc equivalent nếu repo dùng pnpm/yarn.

Nếu có test:

```text
npm test
```

### Runtime validation

Kiểm tra các flow chính bị ảnh hưởng.

Không chỉ dựa vào compile success.

---

## 1.5 Completion report bắt buộc

Sau mỗi phase output:

```markdown
# PHASE COMPLETION REPORT

## Implemented
- ...

## Changed Files
- ...

## API Changes
- ...

## Database Changes
- ...

## Security Impact
- ...

## Tests Executed
- ...

## Runtime Evidence
- ...

## Remaining Issues
- ...

## Out of Scope Findings
- ...
```

Không ghi "100% completed" nếu test/runtime chưa xác nhận.

---

# PHASE 0 — CLEANUP & BASELINE

## Objective

Tạo baseline sạch trước khi thêm feature mới.

Không triển khai:

- User Registration;
- Comments;
- Rating;
- Bookmark server sync;
- Reading History server sync;
- Notification.

---

## Backend Tasks

Audit toàn bộ controller.

Đặc biệt kiểm tra legacy routes:

```text
api/stories
api/chapters
```

so với API canonical:

```text
/api/v1/stories
/api/v1/admin/stories
/api/v1/admin/stories/{storyId}/chapters
/api/v1/admin/chapters/...
```

Xác định:

- route nào FE đang gọi;
- route nào duplicate;
- route nào legacy;
- controller obsolete;
- DTO duplicate;
- handler/repository chỉ phục vụ legacy flow.

Nếu legacy API không còn sử dụng:

- remove hoặc deprecate an toàn;
- không để hai API cùng làm một việc.

Chuẩn hóa:

- ApiEnvelope;
- PagedApiEnvelope;
- error format;
- pagination;
- HTTP status;
- route naming.

Audit:

- unused DTO;
- unused handler;
- dead repository method;
- duplicate mapper;
- obsolete configuration.

---

## Frontend Tasks

Audit:

```text
src/app
src/features
src/components
src/lib
src/constants
src/providers
```

Tìm:

- unused component;
- API helper duplicate;
- route constant duplicate;
- hardcoded URL;
- dead code;
- request bypass BFF không cần thiết.

Giữ admin/auth flow:

```text
Browser
  ↓
Next.js Route Handler
  ↓
HTTP-only cookies
  ↓
ASP.NET Core API
```

Client component không được trực tiếp đọc JWT.

---

## Demo fallback

Nếu public site có fallback demo data khi BE unavailable:

Giữ cho development/demo nếu thực sự cần.

Production phải OFF.

Tạo config/feature flag phù hợp convention hiện tại, ví dụ:

```env
NEXT_PUBLIC_ENABLE_DEMO_FALLBACK=false
```

Không bắt buộc dùng đúng tên trên nếu project đã có config system khác.

---

## Acceptance Criteria

- Legacy API duplicate không còn active nếu không cần.
- FE dùng canonical API.
- Swagger không còn obsolete endpoint.
- API response format nhất quán.
- Không còn obvious dead code trong scope.
- FE build pass.
- BE build pass.
- Existing core flows không bị break.

---

# PHASE 1 — SECURITY HARDENING

## Objective

Đóng các lỗ hổng security đã được audit.

Ưu tiên:

1. secrets;
2. SSRF;
3. validation;
4. error disclosure;
5. XSS/content sanitizer.

---

# 1.1 Secrets

Audit:

```text
appsettings.json
appsettings.*.json
.env*
docker config
render config
github actions
```

Không để trong git:

- real DB password;
- real JWT signing key;
- real bootstrap admin password;
- production credentials.

Production dùng environment variables.

Nếu secret đã từng commit:

- coi secret compromised;
- rotate;
- document tên secret cần rotate.

Không ghi secret mới vào source.

---

# 1.2 Validation

Thiết lập validation layer thống nhất.

Có thể dùng FluentValidation nếu phù hợp kiến trúc và chưa có solution tương đương.

Validate tối thiểu:

## Story

- Title required;
- max length;
- Description limits;
- AuthorName;
- Slug validity;
- ScheduledAt;
- Cover URL.

## Chapter

- Title;
- ChapterNumber;
- Content;
- Slug;
- ScheduledAt;
- AffiliateLink.

## Genre

- Name required;
- unique name;
- unique slug;
- max length.

## Auth

- username;
- email;
- password policy;
- change password.

## Scraper

- URL format;
- protocol;
- host.

Validation error phải trả API envelope consistent.

---

# 1.3 SSRF Protection

Scraper không được gọi private/internal network.

Reject:

```text
localhost
127.0.0.0/8
10.0.0.0/8
172.16.0.0/12
192.168.0.0/16
169.254.0.0/16
::1
link-local
private IPv6
cloud metadata IP
```

Flow mong muốn:

```text
Input URL
   ↓
Parse URL
   ↓
Allow HTTP/HTTPS policy
   ↓
Resolve DNS
   ↓
Reject private/internal IP
   ↓
Domain policy
   ↓
Timeout
   ↓
Response size limit
   ↓
Scrape
```

Nếu GenericFallbackEngine làm tăng risk quá lớn và không cần requirement thực tế:

- document;
- có thể disable bằng config;
- không tự remove nếu FE/requirement vẫn đang cần.

---

# 1.4 Error Disclosure

Production API không trả:

- stack trace;
- SQL message;
- internal class;
- connection string;
- exception details.

Expected format ví dụ:

```json
{
  "success": false,
  "code": "INTERNAL_ERROR",
  "message": "An unexpected error occurred",
  "traceId": "..."
}
```

Logging server vẫn lưu exception detail.

---

# 1.5 HTML Sanitizer / XSS

Test sanitizer với payload:

```html
<script>alert(1)</script>
<img src=x onerror=alert(1)>
<iframe src="..."></iframe>
<a href="javascript:alert(1)">x</a>
<svg onload=alert(1)>
```

Xác định rõ whitelist tags/attributes.

Không phá các HTML cần thiết cho nội dung truyện.

---

## Acceptance Criteria Phase 1

- Không còn production/dev secret nhạy cảm trong tracked source.
- SSRF protection active.
- Critical requests có validation.
- Production error không leak internals.
- Sanitizer có tests.
- Auth flow cũ vẫn chạy.
- Scraper vẫn hoạt động với supported domains.

---

# PHASE 2 — COMPLETE EXISTING FEATURES

## Objective

Không thêm domain lớn mới.

Hoàn thiện những tính năng BE đã có nhưng FE thiếu hoặc implementation chưa khép kín.

---

# 2.1 Scheduled Publishing UI

BE hiện đã có schedule story/chapter.

Hoàn thiện FE.

Admin story UI:

```text
Save Draft
Publish Now
Schedule
Hide
Complete
```

Schedule modal/form:

```text
Date
Time
Timezone
Confirm
```

Display:

```text
Scheduled
08/08/2026 20:00
```

Cho phép nếu BE hỗ trợ:

- reschedule;
- cancel schedule.

Chapter UI tương tự.

Phải xử lý:

- past datetime;
- timezone;
- optimistic concurrency/version;
- conflict;
- loading;
- success toast;
- error toast.

---

# 2.2 Genre Hardening

Hoàn thiện BE validation.

Kiểm tra:

- duplicate name;
- duplicate slug;
- slug normalization;
- deleting genre đang referenced;
- inactive genre behavior.

Không làm thay đổi public behavior ngoài mong muốn.

---

# 2.3 Audit Log

Hoàn thiện viewer.

Nếu architecture hiện tại hỗ trợ hợp lý, thêm:

- date range;
- actor;
- action;
- entity type;
- result;
- search;
- pagination;
- detail view;
- CSV export.

Không export sensitive information.

---

# 2.4 SystemLog Legacy

Audit SystemLog.

Nếu chỉ còn legacy read:

- xác định còn UI nào sử dụng;
- deprecate nếu không cần;
- không để AuditLog và SystemLog gây confusion.

---

# 2.5 Scraper UX

Current scraper import chapter phải có:

- visible progress;
- success count;
- failed count;
- error per chapter;
- retry failed chapter;
- duplicate protection;
- cancel client-side operation nếu phù hợp.

Không bắt buộc build distributed queue ở phase này.

Nếu hiện flow đang chạy loop FE:

- giữ architecture nếu đủ;
- chỉ refactor nếu có lý do kỹ thuật rõ ràng.

---

## Acceptance Criteria Phase 2

- Schedule Story có UI end-to-end.
- Schedule Chapter có UI end-to-end.
- Genre validation complete.
- Audit logs usable.
- Scraper failure/retry UX rõ ràng.
- Existing public reader không regression.

---

# PHASE 3 — USER SYSTEM

## Objective

Biến ComicWeb từ public reader + admin CMS thành hệ thống có reader account thật.

---

# 3.1 Registration

Implement:

```http
POST /api/v1/auth/register
```

Fields dựa trên User model thực tế.

Tối thiểu:

```text
username
email
password
```

Validate:

- username unique;
- email unique;
- valid email;
- password policy;
- normalized username/email.

Không trả PasswordHash.

---

# 3.2 Reader Login

Reuse auth architecture hiện tại nếu phù hợp.

Admin và User phải dùng cùng auth infrastructure, nhưng authorization khác nhau.

Không tạo auth system thứ hai.

Expected roles:

```text
User
Admin
```

Không thêm role mới trừ khi requirement thực sự cần.

---

# 3.3 Reader Login/Register FE

Tạo UI:

```text
/login
/register
```

Nếu route `/login` hiện thuộc Admin, refactor route naming rõ ràng.

Possible structure:

```text
/login
/register

/admin/login
```

hoặc giữ route hiện tại nếu project convention yêu cầu.

Điều quan trọng:

- reader login UX rõ;
- admin auth không bị break;
- HTTP-only cookie vẫn được ưu tiên.

---

# 3.4 Profile

Implement:

```text
/profile
```

Display:

- username;
- email;
- role;
- created information nếu phù hợp;
- last login nếu cần.

Actions:

- change password;
- logout.

Không cho user sửa role.

---

# 3.5 Admin User Management

Backend API canonical ví dụ:

```http
GET    /api/v1/admin/users
GET    /api/v1/admin/users/{id}
PATCH  /api/v1/admin/users/{id}/status
PATCH  /api/v1/admin/users/{id}/role
```

Không bắt buộc đúng route nếu project convention khác.

Admin UI:

```text
/admin/users
```

Features:

- pagination;
- search username/email;
- filter role;
- filter active;
- view detail;
- disable;
- enable;
- change role nếu requirement cho phép.

Không cho admin:

- đọc password;
- đọc PasswordHash;
- set arbitrary password trực tiếp.

Mọi admin action phải audit log.

---

## Acceptance Criteria Phase 3

Reader flow:

```text
Register
  ↓
Login
  ↓
GET /me
  ↓
Profile
  ↓
Change password
  ↓
Logout
```

Admin flow:

```text
Admin Login
  ↓
Users
  ↓
Disable Reader
  ↓
Reader access blocked
  ↓
Audit Log created
```

---

# PHASE 4 — SERVER-SIDE BOOKMARK & READING HISTORY

## Objective

Đồng bộ dữ liệu reader trên nhiều thiết bị.

Guest vẫn hoạt động được bằng localStorage.

---

# 4.1 FollowedStory

Tạo entity tương đương:

```text
FollowedStory
-------------
UserId
StoryId
CreatedAt
```

Unique constraint:

```text
(UserId, StoryId)
```

API:

```http
POST   /api/v1/me/follows/{storyId}
DELETE /api/v1/me/follows/{storyId}
GET    /api/v1/me/follows
```

Route có thể đổi theo API convention hiện tại.

---

# 4.2 ReadingHistory

Entity:

```text
ReadingHistory
--------------
UserId
StoryId
ChapterId
Progress
LastReadAt
```

Unique logic cần xác định rõ.

Expected behavior:

- update last read chapter;
- update progress;
- sort newest first.

API cần:

```text
GET history
UPSERT progress/history
DELETE history item
CLEAR history (optional)
```

---

# 4.3 Guest → Account Merge

Guest vẫn lưu localStorage.

Khi login:

```text
Local bookmarks/history
        ↓
Authentication success
        ↓
Merge with server
        ↓
Deduplicate
        ↓
Server becomes canonical
```

Không mất dữ liệu local.

Merge phải idempotent.

---

# 4.4 Reader Settings

Nếu phù hợp, tạo server-side ReaderPreference:

```text
Theme
FontSize
LineHeight
ContentWidth/Fit
```

Không bắt buộc nếu scope quá lớn.

Ưu tiên Bookmark và History trước.

---

## Acceptance Criteria Phase 4

- Guest bookmark vẫn chạy.
- Logged-in bookmark sync server.
- History sync server.
- Login merge không duplicate.
- Logout không xóa dữ liệu server.
- Account sử dụng trên browser khác vẫn thấy bookmark/history.

---

# PHASE 5 — ENGAGEMENT

## Order

Triển khai theo thứ tự:

```text
Rating
→ Comments
→ ViewCount
```

---

# 5.1 Rating

Entity:

```text
StoryRating
-----------
UserId
StoryId
Score
CreatedAt
UpdatedAt
```

Constraint:

```text
Score 1..5
Unique(UserId, StoryId)
```

Story public response cần có:

```text
averageRating
ratingCount
myRating
```

Nếu anonymous:

```text
myRating = null
```

API phải support create/update rating.

---

# 5.2 Comments

Entity:

```text
Comment
-------
Id
StoryId
ChapterId?
UserId
ParentCommentId?
Content
Status
CreatedAt
UpdatedAt
DeletedAt?
```

V1 chỉ cần:

- create;
- edit own;
- delete own;
- admin moderate/delete;
- pagination.

Nested comments:

- tối đa 1–2 levels;
- không làm infinite tree.

Security:

- sanitize/encode;
- authorization ownership;
- rate limit;
- reasonable content length.

---

# 5.3 ViewCount

Không update PostgreSQL trực tiếp mỗi request nếu tránh được.

Preferred:

```text
Page View
  ↓
Redis INCR
  ↓
Periodic flush
  ↓
PostgreSQL aggregate
```

Nếu Redis disabled:

- define fallback strategy;
- không được crash.

Sau đó support ranking:

```text
Hot today
Hot week
Hot month
```

Không fake view count.

---

## Acceptance Criteria Phase 5

- User chỉ có 1 rating/story.
- Average rating chính xác.
- Comment ownership protected.
- Admin moderation audited.
- ViewCount không tạo write storm vào DB.

---

# PHASE 6 — SEARCH & SEO

# 6.1 PostgreSQL Search Optimization

Audit query hiện tại trước.

Nếu search dùng LIKE/ILIKE:

enable/use:

```text
pg_trgm
```

Index phù hợp cho:

- Story.Title;
- AuthorName;
- normalized title nếu tồn tại;
- slug lookup.

Dùng EXPLAIN ANALYZE để evidence index được dùng.

Không thêm Elasticsearch.

---

# 6.2 Search UX

Public search:

- query;
- genre;
- status;
- sort;
- pagination.

Handle:

- empty result;
- invalid page;
- slow query;
- Vietnamese text.

---

# 6.3 SEO

Implement:

```text
sitemap.xml
robots.txt
canonical URL
OpenGraph
Twitter metadata
JSON-LD nếu phù hợp
```

Dynamic metadata cho:

## Story

- title;
- author;
- description;
- cover;
- canonical.

## Chapter

- story title;
- chapter title;
- canonical.

Không index admin/auth pages.

---

## Acceptance Criteria Phase 6

- Search query performance được đo.
- Search indexes có evidence.
- sitemap hoạt động.
- robots hợp lệ.
- story/chapter metadata render server-side.
- canonical URL đúng.

---

# PHASE 7 — MEDIA UPLOAD

## Objective

Không phụ thuộc hoàn toàn external cover URL.

---

## Architecture

```text
Admin
  ↓
Upload API
  ↓
Object Storage
  ↓
CDN/Public URL
  ↓
PostgreSQL stores URL
```

Không lưu image binary trực tiếp vào PostgreSQL.

Storage provider phải abstract qua interface.

Có thể support:

- Cloudflare R2;
- S3;
- Cloudinary;
- provider khác phù hợp.

Không hardcode provider vào Domain/Application.

---

## Validation

- allowed MIME;
- actual file signature;
- max file size;
- generated filename;
- sanitized metadata;
- image dimension limit.

Nếu phù hợp:

- convert WebP/AVIF;
- cover resize.

---

## Cleanup

Xử lý orphan images.

Không delete file đang referenced.

---

## Acceptance Criteria Phase 7

- Admin upload cover.
- Story save URL.
- Public cover render.
- Invalid file rejected.
- Oversize file rejected.
- Storage failure handled.

---

# PHASE 8 — NOTIFICATIONS & NEWSLETTER

# 8.1 UserNotification

Existing model phải được audit trước.

Nếu usable, reuse thay vì tạo duplicate.

Trigger ban đầu:

```text
Followed Story
    +
New Chapter Published
    ↓
Notification
```

API:

```text
GET notifications
MARK one read
MARK all read
```

FE:

```text
notification bell
unread count
notification dropdown/page
```

Không cần WebSocket V1.

Polling/refetch là đủ.

---

# 8.2 Newsletter

Nếu frontend đã có subscribe form:

Implement backend.

Entity tối thiểu:

```text
NewsletterSubscriber
--------------------
Email
Status
SubscribedAt
UnsubscribedAt
```

Requirements:

- valid email;
- deduplicate;
- unsubscribe;
- rate limit;
- không expose subscriber list public.

Không cần build email campaign engine lớn trong phase này.

---

## Acceptance Criteria Phase 8

- Following user nhận notification khi chapter publish.
- Duplicate notification được kiểm soát.
- Read/unread hoạt động.
- Newsletter form thật sự persist.
- Duplicate subscription handled.

---

# PHASE 9 — TEST HARDENING

## Objective

Đưa hệ thống từ “build được” thành “có regression protection”.

Testing phải được thêm từ các phase trước, nhưng phase này là hardening toàn hệ thống.

---

# 9.1 Backend Unit Tests

Priority:

1. Authentication
2. Authorization
3. Refresh rotation
4. Refresh reuse detection
5. Story handlers
6. Chapter handlers
7. Publishing
8. Scheduled publishing
9. Validation
10. Scraper URL security
11. Cache invalidation
12. Follow
13. Reading history
14. Rating
15. Comment authorization

---

# 9.2 Integration Tests

Preferred:

```text
WebApplicationFactory
+
Testcontainers PostgreSQL
+
Redis test container if necessary
```

Test actual HTTP flows.

Không mock database cho integration tests.

---

# 9.3 Frontend Tests

Sử dụng tool phù hợp repo.

Suggested:

```text
Vitest
React Testing Library
Playwright
```

Không bắt buộc thêm toàn bộ nếu repo đã có alternative.

---

# 9.4 Critical E2E

## Admin publishing

```text
Admin login
→ create story
→ create chapter
→ schedule/publish
→ public site sees content
```

## Reader

```text
Register
→ login
→ follow
→ read chapter
→ history updated
→ logout
→ login
→ data persists
```

## Security

```text
invalid login
lockout
refresh rotation
disabled user
AdminOnly rejected for User
```

---

## Coverage

Không chạy theo coverage % một cách máy móc.

Ưu tiên critical business paths.

Nhưng phải tạo baseline coverage report để theo dõi regression.

---

## Acceptance Criteria Phase 9

- Critical auth tests pass.
- Publishing E2E pass.
- Reader E2E pass.
- Integration DB tests chạy isolated.
- CI chạy tests tự động.

---

# PHASE 10 — PRODUCTION READINESS

## Objective

Đưa hệ thống đến mức có thể deploy và vận hành có kiểm soát.

---

# 10.1 Health Checks

Implement:

```text
/health/live
/health/ready
```

Liveness:

- app process healthy.

Readiness:

- database available;
- critical dependencies available.

Redis optional không được làm toàn app unhealthy nếu architecture cho phép fallback.

---

# 10.2 Structured Logging

Log structured.

Fields nên có:

```text
timestamp
level
traceId
requestId
userId if authenticated
route
statusCode
duration
exception
```

Không log:

- password;
- JWT;
- refresh token;
- connection string;
- sensitive cookie.

---

# 10.3 Observability

Chọn solution vừa đủ.

Có thể:

```text
Serilog
Sentry
OpenTelemetry
```

Không bắt buộc Grafana stack nếu project không cần.

Minimum:

- exceptions searchable;
- request correlation;
- performance visibility.

---

# 10.4 Database Backup

Document:

```text
backup strategy
backup frequency
retention
restore procedure
```

Phải test restore ít nhất trên staging/dev copy.

Backup mà chưa restore-test không được coi là hoàn chỉnh.

---

# 10.5 Database Migration Strategy

Production deployment phải xác định rõ:

```text
Who applies migrations?
When?
Rollback strategy?
```

Không để multiple instance race apply migration nếu deployment architecture có risk.

---

# 10.6 CI/CD

Pipeline minimum:

```text
Frontend lint
Frontend build
Backend build
Backend tests
Integration tests where possible
Docker build
```

Production deploy chỉ chạy khi required checks pass.

---

# 10.7 Production Configuration

Verify:

```text
ASPNETCORE_ENVIRONMENT=Production
Secure cookies
HTTPS
CORS allowlist
No Swagger public unless intentionally enabled
No dev secrets
Demo fallback OFF
Correct FE/BE URLs
```

---

# 10.8 Performance Smoke Test

Test:

```text
story list
story detail
chapter detail
search
login
```

Check:

- average latency;
- DB query count;
- cache hits;
- no obvious N+1.

Không cần enterprise-scale load test nếu requirement không yêu cầu.

---

## Acceptance Criteria Phase 10

- Health endpoints work.
- Structured logs work.
- Exceptions observable.
- Backup + restore documented/tested.
- CI protects build.
- Production env hardened.
- Core smoke tests pass.

---

# FINAL SYSTEM ACCEPTANCE

Chỉ coi hệ thống "hoàn thiện" khi các vertical flows sau chạy end-to-end.

---

## ADMIN FLOW

```text
Login
→ Dashboard
→ Story CRUD
→ Genre
→ Chapter CRUD
→ Scrape
→ Schedule
→ Publish
→ Audit
→ User Management
```

---

## READER FLOW

```text
Browse
→ Search
→ Story Detail
→ Read
→ Register/Login
→ Bookmark
→ History
→ Rating
→ Comment
→ Notification
```

---

## CONTENT FLOW

```text
Draft
→ Validate
→ Schedule
→ Background Publish
→ Cache Invalidation
→ Public Reader
→ SEO
→ Notification
```

---

## SECURITY FLOW

```text
Login
→ JWT
→ Refresh Rotation
→ Reuse Detection
→ Role Authorization
→ Active User
→ Validation
→ Rate Limit
→ Sanitization
→ Audit
```

---

# PRIORITY SUMMARY

| Order | Phase | Priority |
|---|---|---|
| 0 | Cleanup & Baseline | Critical |
| 1 | Security Hardening | Critical |
| 2 | Complete Existing Features | Critical |
| 3 | User System | Critical |
| 4 | Bookmark + Reading History | High |
| 5 | Rating + Comments + ViewCount | High |
| 6 | Search + SEO | High |
| 7 | Media Upload | Medium |
| 8 | Notifications + Newsletter | Medium |
| 9 | Test Hardening | Critical / Continuous |
| 10 | Production Readiness | Critical before release |

---

# FEATURES NOT TO PRIORITIZE YET

Không triển khai trừ khi có requirement mới:

```text
Microservices
Kafka
Elasticsearch
Kubernetes
Complex RBAC
WebSockets
PWA
OAuth providers
AI recommendation engine
distributed event architecture
```

Lý do:

ComicWeb hiện chưa bị giới hạn bởi architecture.

Các gap quan trọng hơn là:

```text
Security
User System
Data Sync
Testing
SEO
Production Reliability
```

---

# ANTIGRAVITY EXECUTION INSTRUCTION

Khi nhận file này:

1. Không triển khai tất cả phase cùng lúc.
2. Bắt đầu Phase 0.
3. Audit code thực tế.
4. Tạo implementation plan riêng cho Phase 0.
5. Thực hiện Phase 0.
6. Build/test.
7. Output Phase Completion Report.
8. Chỉ sau khi Phase 0 đạt acceptance criteria mới chuyển Phase 1.
9. Lặp lại cho đến Phase 10.

Nếu gặp conflict giữa file này và source:

```text
SOURCE CODE WINS
```

Nhưng phải ghi rõ conflict trong report.

Nếu một requirement trong phase đã được source hiện tại implement đầy đủ:

- verify;
- test;
- ghi evidence;
- không rewrite.

Nếu một task không cần thiết sau khi audit:

- explain;
- mark NOT REQUIRED;
- provide evidence.

Mục tiêu cuối cùng không phải "thêm thật nhiều code".

Mục tiêu là:

```text
A coherent, secure, tested, production-ready ComicWeb
with complete FE ↔ BE vertical flows.
```
