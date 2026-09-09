# آرشیو موارد رفع‌شدهٔ REVIEW.md

> مواردی از گزارش مرور که در تاریخ ۱۴۰۵/۰۶/۰۹ (2026-08-31) با کد فعلی تطبیق داده شدند
> و رفع آن‌ها **در کد تأیید شد**. شواهد (فایل:خط) مربوط به همین بررسی است.
> موارد باز و ناقص در [`REVIEW.md`](REVIEW.md) باقی مانده‌اند.
>
> مورد ۱.۴ (گذر plaintext از HTTP هنگام fill فرم لاگین) در ۱۴۰۵/۰۶/۱۳ (2026-09-04)
> با فعال‌سازی HTTPS روی سرور رفع و به همین فایل اضافه شد.
> مورد ۲.۱۰ (اثر جانبی DELETE در GET صفحه اصلی) در ۱۴۰۵/۰۶/۱۷ (2026-09-08)
> با انتقال پاک‌سازی trend‌های منقضی به سرویس پس‌زمینه‌ای (`TrendsCleanupService`)
> رفع و به همین فایل اضافه شد.
> مورد ۲.۱۳ (فیلدهای استاتیک چینش لاگ در `TrendsCheckpoint`) در ۱۴۰۵/۰۶/۱۷ (2026-09-08)
> با انتقال به سطح نمونه و محاسبهٔ یک‌بارهٔ عرض‌ها در شروع پاس رفع و به همین فایل اضافه شد.
> مورد ۲.۱۵ (منطق مرتب‌سازی جبری و مستندنشده) در ۱۴۰۵/۰۶/۱۷ (2026-09-08)
> با وزن ذوزنقه‌ای ضرب‌شوندهٔ `JobRanking` و تست xUnit رفع و به همین فایل اضافه شد.
> مورد ۲.۱۶ (الگوی `/rc/clk?jk=` در استخراج شغل این‌دیس) در ۱۴۰۵/۰۶/۱۷ (2026-09-08)
> با الگوی چندشکلی `IndeedSerp` رفع و به همین فایل اضافه شد.

---

## بخش ۱ — 🔴 بحرانی (همه رفع شد)

### ۱.۱ تزریق SQL در `JobBusiness.Fetch`
پارامترهای داینامیک جایگزین الحاق رشته شد؛ باگ `where =` نیز اصلاح شد.
**تأیید:** `core-decision-dotnet/Database/Business/JobBusiness.cs:12-30` — پارامترهای `$a0…` / `$c0…` با `parameters.AddRange(...)`.

### ۱.۲ اجرای SQL دلخواه در `JobController.Setting`
اندپوینت حفظ شد اما پشت میدل‌ور احراز هویت سراسری قرار گرفت.
**تأیید:** `core-decision-dotnet/Program.cs:100-111` — همهٔ مسیرها (به‌جز `/auth`) نیازمند X-API-Key یا کوکی امضاشده‌اند.

### ۱.۳ نبود احراز هویت/مجوزدهی
احراز هویت تک‌کاربره: هدر `X-API-Key` برای اکستنشن + کوکی امضاشده (DataProtection) از طریق `/auth/login` برای داشبورد. در Development بدون کلید فقط هشدار؛ در Production راه‌اندازی fail-fast است.
**تأیید:** `Program.cs:30-58` (کلیدها + fail-fast)، `Program.cs:120-143` (`Authorized`).

### ۱.۴ ذخیره اعتبارنامه به‌صورت Plain-text (at-rest)
رمزنگاری AES-GCM با پیشوند `enc:` + مهاجرت خودکار plaintext در startup.
**تأیید:** `Program.cs:43-58` — `SecretProtector.SetKey` + `AgencyBusiness.MigratePlaintextPasswords`.
**رفع باقی‌مانده (۱۴۰۵/۰۶/۱۳ — 2026-09-04):** گذر plaintext از HTTP هنگام fill فرم لاگین، با فعال‌سازی HTTPS روی سرور (سطح استقرار، خارج از کد اپ) رفع شد و از REVIEW.md آرشیو گردید.

### ۱.۵ `LastInsertRowId` نادرست هنگام Conflict
**تأیید:** `JobBusiness.cs:166-168` — `database.Changes() == 0` → `Fetch(agency, code)`؛ در غیر این صورت `LastInsertRowId()`. متد `Changes()` در `Database.cs:80-85`.

### ۱.۶ مدیریت استثناء ناقص → نشت Stack Trace
`UseExceptionHandler` سراسری: لاگ کامل + پاسخ 500 بدون جزئیات (JSON یا متن).
**تأیید:** `Program.cs:71-95`.

### ۱.۷ نبود محدودیت حجم درخواست
**تأیید:** `Controllers/Decision.cs:12` — `[RequestSizeLimit(5_000_000)]` روی `Take`.

---

## بخش ۲ — 🟠 مهم (رفع‌شده‌ها)

### ۲.۱ باگ تشخیص دوره حقوق (`EvaluateSalaryScore`)
**تأیید:** `Analyze/JobEligibilityHelper.cs:359` — شرط به `period_index > 0 && matched.Groups[period_index].Success` اصلاح شد.

### ۲.۳ `check-page.js` هندلر اشتباه
**تأیید:** `agent-extension/controllers/check-page.js:20` — `function () { BackgroundMessaging.Scopes(true); }`.

### ۲.۴ `keyboard.js` باگ ارجاع پیش از تعریف و مرده بودن
**تأیید:** فایل `agent-extension/controllers/keyboard.js` حذف شده است (وجود ندارد).

### ۲.۵ `Glassdoor` ناقص و مستعد کرش
**تأیید:** `Analyze/Glassdoor/Glassdoor.cs:25-28` — `GetSubPages` آرایهٔ خالی برمی‌گرداند؛ کامنت‌های کپی‌پیست حذف شدند.

### ۲.۶ `TrendsCheckpoint.LoadAndUpdateCurrentTrend` همیشه Rollback
**تأیید:** `Analyze/TrendsCheckpoint.cs:71-82` — الگوی استاندارد `Commit` داخل try و `Rollback` فقط در catch.

### ۲.۷ SQLite و همزمانی
**تأیید:** `Database/Database.cs:44-47` — `PRAGMA journal_mode=WAL` + `PRAGMA busy_timeout=5000` در هر `Open`.

### ۲.۸ `JobEligibilityHelper` و اتصال‌های تکراری
**تأیید:** `JobEligibilityHelper.cs:22-50` — کش static برای `JobOption[]` (`cached_options` + `InvalidateOptionsCache`).
⚠️ بازسازی اتصال‌ها به DI (نیمهٔ دوم این مورد = مورد ۳.۳) در REVIEW.md باقی مانده است.

### ۲.۹ اثر جانبی در متد شمارش (`FetchFromCount`)
**تأیید:** `JobBusiness.cs:44-54` — `FetchFromCount` فقط می‌خواند؛ UPDATE ریست به `ResetRevaluations` منتقل شد.

### ۲.۱۰ اثر جانبی DELETE در GET صفحه اصلی
حذف فراخوانی `DeleteExpired` از `GetTrends` و انتقال پاک‌سازی trend‌های منقضی به سرویس پس‌زمینه‌ای دوره‌ای `TrendsCleanupService` (اجرای بلافاصله در startup و سپس هر ۱ دقیقه).
**تأیید:** `Controllers/Report.cs:92-100` — `GetTrends` فقط `Trend.Report()` را صدا می‌زند؛ `TrendsCleanupService.cs:5-36` و ثبت `AddHostedService<TrendsCleanupService>` در `Program.cs:65`.

### ۲.۱۱ `VACUUM` در هر Clean
**تأیید:** `JobBusiness.cs:188-194` — `Clean(int mounths, bool vacuum = false)`؛ `?vacuum=true` برای اجرا.

### ۲.۱۲ `FillSpace` احتمال استثنای منفی
**تأیید:** `TrendsCheckpoint.cs:318` — `Math.Max(0, max - text.Length)`.

### ۲.۱۳ فیلدهای استاتیک برای چینش لاگ ناامن در برابر همزمانی
فیلدها به سطح نمونه منتقل شدند؛ عرض ستون‌ها یک‌بار در شروع `CheckingSleptTrends`
محاسبه می‌شود و حین فرمت‌دهی لاگ دیگری چیزی تغییر نمی‌کند. غلط املایی `TrednTypeLength`
نیز به `TrendTypeLength` اصلاح شد (ردیف مربوط در بخش ۴ REVIEW.md حذف شد).
**تأیید:** `core-decision-dotnet/Analyze/TrendsCheckpoint.cs` — فیلدهای instance و
محاسبهٔ عرض‌ها در `CheckingSleptTrends`.

### ۲.۱۴ `DecisionController.Running` کلید نامعتبر → ۵۰۰
**تأیید:** `Controllers/Decision.cs:102` — `TryGetValue` + `NotFound()`.

### ۲.۱۵ منطق مرتب‌سازی جبری و مستندنشده
فرمول گاوسی/نمایی سرباز (`Score + A·e^YF − e^UF` با جریمهٔ بی‌سقف برای شغل‌های قدیمی) حذف و با
`EffectiveScore = Score × W(age)` جایگزین شد که `W` وزن ذوزنقه‌ای مقید است (۰.۸۵ در ۰–۲ روز، شیب تا ۱.۰
در روز ۴، فلات ۴–۱۰ روز، ۰.۷۵ در روز ۱۴، ۰.۲۵ در روز ۲۸ و کف ۰.۱۵). شغل‌های قدیمی دیگر نابود نمی‌شوند و
رتبهٔ هیچ شغلی به `MAX(Score)` سراسری وابسته نیست. `WHERE Ranking <= (12 / Category)` با سقف‌های CASE
صریح (Attention→12، NotApproved→6، Applied/Rejected→3، سایر→1) جایگزین شد؛ ثابت‌های `MaxScore`/`DaysPriod`
و کامنت LaTeX حذف شدند (ردیف املایی `DaysPriod` در بخش ۴ REVIEW.md نیز حذف شد) و سنِ اقدام در `Tries`
ثبت می‌شود (`«n: date (age Nd)»` — الگوی کپ `'%4: %'` دست‌نخورده مانده). منبع حقیقت منحنی
`Analyze/JobRanking.cs` است، SQL در `Q_INDEX` آینهٔ آن با کامنت همگام‌سازی، و مستندات در SCORING.md
بازنویسی شد.
**تأیید:** `core-decision-dotnet/Analyze/JobRanking.cs` — منحنی و ثابت‌ها؛ تست‌های نقطهٔ شکست/کران‌ها/یکنوایی
در `core-decision-dotnet.Tests/JobRankingTests.cs`؛ `core-decision-dotnet/Database/Business/JobBusiness.cs` —
`EffectiveScore` و CASE صریح سقف‌ها در `Q_INDEX`، افزودن `RegTime` به `Q_FETCH_FIRST`؛
`docs/SCORING.md` بخش Ranking.

### ۲.۱۶ `IndeedPageSearch` از `reg_job_url` (`/rc/clk?jk=`) استفاده می‌کند
**رفع:** الگوی چندشکلی در `IndeedSerp.reg_job_url` (rc/clk + viewjob + m/viewjob + data-jk)، هشدار استخراج صفر در `SearchPage`، تست snapshot (`IndeedSerpTests`). fixture سنتزی است؛ در نخستین اجرای زنده تأیید نهایی شود.
**تأیید:** `core-decision-dotnet/Analyze/Indeed/IndeedSerp.cs` — الگو و `ExtractJobCodes`؛
`core-decision-dotnet/Analyze/Indeed/IndeedPageSearch.cs` — `GetJobUrls` از `IndeedSerp`؛
`core-decision-dotnet/Analyze/Pages/SearchPage.cs` — هشدار `codes.Count == 0`؛
`core-decision-dotnet.Tests/IndeedSerpTests.cs` — سه تست استخراج/تکرار/خالی.

### ۲.۱۷ `IndeedPageJob.ChceckJob` استثنا برای کنترل جریان
**تأیید:** `Analyze/Indeed/IndeedPageJob.cs:45-48` — `State = JobState.NotApproved` + `Log.Warning`.

### ۲.۱۸ `Q_CLEAN_ATTENTION` زیرکوئری مبهم
**تأیید:** `JobBusiness.cs:321-322` — کامنت شفاف‌ساز (رفتار فعلی: top-100 سراسری) اضافه شد.

### ۲.۱۹ `Analyze.Agencies` lazy-load الگوی قفل شکسته
**تأیید:** `Analyze/Analyzer.cs:11-43,53-60` — بررسی داخل lock (double-check) برای هر دو property؛ `ClearAgencies` و `ReloadSettings` زیر همان lock.

---

## بخش ۳ — 🟡 متوسط (رفع‌شده‌ها)

### ۳.۷ `Extensions.Shift` مرده و باگ‌دار
**تأیید:** `Basics/Extensions.cs` — متد `Shift` حذف شده است.

### ۳.۸ `Page.CompareTo` بیش‌حد پیچیده
**تأیید:** `Analyze/Pages/Page.cs:18-21` — `Order.CompareTo(other.Order)`.

### ۳.۱۱ Serilog با ASP.NET Logging ادغام نشده
**تأیید:** `Program.cs:22-23` — `ClearProviders()` + `builder.Logging.AddSerilog()`.

### ۳.۱۲ مسیر لاگ ممکن است نباشد
**تأیید:** `Program.cs:12-14` — `Directory.CreateDirectory(log_directory)` در startup.

### ۳.۱۳ ناهماهنگی casing مسیر لاگ
**تأیید:** `Program.cs:12` — پیش‌فرض یکسان `logs/E.log`.

### ۳.۹ نبود تست واحد
(۱۴۰۵/۰۶/۱۸ — 2026-09-09) پنج متد از منطق اصلی امتیازدهی/تحلیل زیر تست xUnit قرار گرفت:
`EvaluateEligibility`, `EvaluateSalaryScore`, `LanguageIsMatch`, `CheckOptionIn`, `GetTextContent`.
مسیر تست: seam حداقلی — تغییر visibility چهار متد به `internal` + سازندهٔ internal برای تزریق
`Dictionaries`/`Database`/`JobOption[]` روی SQLite درون‌حافظه‌ای؛ بدون تغییر رفتار و بدون
DI کامل (مورد ۳.۳ باز ماند). نکتهٔ فنی: `Settings` حقوق باید مثل تولید از طریق
`JsonConvert.DeserializeObject<dynamic>` ساخته شود — anonymous type در اسمبلی تست از اسمبلی
اصلی با dynamic bind نمی‌شود (RuntimeBinderException). رفتار فعلی `GetTextContent` برای
شکاف ۳.۱۵ (نشت متن `<noscript>` تو در تو) عمداً pin شد
(`Includes_nested_noscript_text_today_3_15_pinned`) تا اصلاح آتی ۳.۱۵ آن را آگاهانه
به‌روز کند. تست‌های JS اکستنشن (`agent-extension/`) عمداً به تسک جداگانهٔ فاز ۲ موکول شد.
**تأیید:** `core-decision-dotnet.Tests/JobEligibilityHelperTests.cs:6-75` (زبان: distinct/مرز ۵۰٪/batching)،
`:80-175` (eligibility: گیت field/reject/نمرهٔ دقیق/کلید تکراری/نیم‌شدن دومین کلید)؛
`core-decision-dotnet.Tests/SalaryScoreTests.cs:17-87` (دوره‌ها/k/حد ۳۵۰۰۰/پیشوند >۲۴ کاراکتر/
غیرقابل‌پارس/Settings تهی + الگوی واقعی production)؛
`core-decision-dotnet.Tests/CheckOptionInTests.cs:6-73` (dedupe/حساس به حروف/فاصلهٔ خالی/
برندهٔ اولین match حقوق/نمرهٔ تخت/محتوای تهی)؛
`core-decision-dotnet.Tests/GetTextContentTests.cs:6-40` (استخراج متن/فروپاشی خطوط خالی/
جدا کردن script-head-style + pin مورد ۳.۱۵)؛
fixture مشترک `core-decision-dotnet.Tests/EligibilityFixture.cs:7-112`؛
seam: `core-decision-dotnet/Analyze/JobEligibilityHelper.cs:34-39` (سازندهٔ internal) و
`:198,225,317,341` (internal شدن)، `core-decision-dotnet/core-decision.csproj:11-13` (`InternalsVisibleTo`).
