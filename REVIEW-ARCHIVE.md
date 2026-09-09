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
> مورد ۳.۳ (`Database.Open()` دستی همه‌جا — نقض DI) در ۱۴۰۵/۰۶/۱۸ (2026-09-09)
> با `IDatabaseFactory` و حذف کامل متدهای استاتیک اتصال رفع و به همین فایل اضافه شد.
> مورد ۳.۲ (استفادهٔ بیش از حد `dynamic` و anonymous types) در ۱۴۰۵/۰۶/۱۸ (2026-09-09)
> با مدل‌ها و record های تایپ‌دار در کل مسیر داده→داشبورد رفع و به همین فایل اضافه شد.
> مورد ۱.۵ (مسیریابی پاسخ پس‌زمینه بر اساس `tab.index` به‌جای `tab.id`) در ۱۴۰۵/۰۶/۱۸
> (2026-09-09) با ارسال مستقیم `chrome.tabs.sendMessage(sender.tab.id, …)` رفع و به همین فایل اضافه شد.
> مورد ۱.۶ (زنجیرهٔ پیام‌رسانی بدون timeout/چک ok/catch) در ۱۴۰۵/۰۶/۱۸ (2026-09-09)
> با `FetchJson` مقاوم‌شده + retry محدود در content script رفع و به همین فایل اضافه شد.

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

### ۱.۵ (مرور ۱۴۰۵/۰۶/۱۸) مسیریابی پاسخ پس‌زمینه بر اساس `tab.index` به‌جای `tab.id`
(شماره‌گذاری این مدخل از مرور ۱۴۰۵/۰۶/۱۸ است؛ ۱.۵ آرشیوشدهٔ نگارش ۲۰۲۶-۰۸-۳۱ — `LastInsertRowId` — مدخل جداگانهٔ فوق است.)
`Respond` در `background.js` پاسخ سرور را با `chrome.tabs.query({ windowId, index })` دنبال
می‌کرد؛ `index` **موقعیت** تب است نه هویتش — جابه‌جایی یا بستن تب همسایه در طول رفت‌وبرگشت
سرور پاسخ را به تب اشتباه می‌رساند (id های per-tab از ۱ شروع می‌شوند و برخورد آنها رایج
است → اجرای فرمان خارجی در تب بی‌گناه) یا با `if (!tabs || !tabs[0]) return;` بی‌صدا گم
می‌شد. اکنون مستقیم `chrome.tabs.sendMessage(tab.id, …)` به هویت پایدار فرستنده
(`sender.tab.id` — همان کلیدی که نقشهٔ trend هم بر اساس آن است) ارسال می‌شود و callback
خطای runtime را مصرف و لاگ می‌کند («Receiving end does not exist» هنگام بستن/ناوبری تب
در میانهٔ درخواست — لاگ کافی است چون لود جدید صفحه خودش `Send` بعدی را صادر می‌کند).
بدون تغییر مانیفست؛ مورد ۱.۶ (چک ok/timeout/catch در زنجیرهٔ پیام) عمداً باز ماند.
**تأیید:** `agent-extension/controllers/background.js:38-41` — ارسال مستقیم به `tab.id` +
مصرف `chrome.runtime.lastError` در callback؛ تأیید نهایی با نخستین اجرای زنده.

### ۱.۶ (مرور ۱۴۰۵/۰۶/۱۸) زنجیرهٔ پیام‌رسانی بدون timeout/retry و بدون چک `response.ok`
(شماره‌گذاری این مدخل از مرور ۱۴۰۵/۰۶/۱۸ است؛ ۱.۶ آرشیوشدهٔ نگارش ۲۰۲۶-۰۸-۳۱ — مدیریت استثناء — مدخل جداگانهٔ فوق است.)
`Send` در `core-messaging.js` برخلاف `Scopes`/`Orders` نه `response.ok` را چک می‌کرد نه خطا را
catch می‌کرد؛ `Respond` در `background.js` نیز بدون try/catch بود. با شکست fetch یا JSON
نامعتبر (خاموشی سرور، 413، پاسخ HTML به‌جای JSON) promise داخل content script هرگز resolve
نمی‌شد و تب بی‌صدا تا ناوبری بعدی از حلقه خارج می‌ماند؛ مدخل درخواست هم در
`CURRENT_REQUESTS` نشت می‌کرد. نکتهٔ ظریف تأییدشده: 401 با `Accept: application/json`
بدنهٔ JSON معتبر داشت و به‌جای هنگ، no-op بی‌صدا می‌داد (و پاسخ خطا در `SCOPES` کش می‌شد)؛
`BadRequest` های بدنهٔ تهی هم به هنگ منجر می‌شدند. اصلاحات:
- **`CoreMessaging.FetchJson` جدید** (`Send`/`Scopes`/`Orders` هر سه از آن استفاده می‌کنند):
  چک `response.ok` + پارس امن (`text()` → `JSON.parse` داخل try) + timeout ۳۰ثانیه‌ای با
  `AbortController` + بازگرداندن ساختار خطای `{error, status}` به‌جای throw. کد خطای بدنهٔ
  JSON سرور (مثل `unauthorized`) منتقل می‌شود و پاسخ خطادار دیگر در `SCOPES` کش نمی‌شود.
- **`Respond`:** try/catch و در خطا forward همان ساختار خطا به content script؛ نقشهٔ trend
  در خطا دست‌نخورده می‌ماند (تب اتصال خود را حفظ می‌کند).
- **`BackgroundMessaging.Message`:** تایمر ۴۵ثانیه‌ای `no-response` (پوشش مرگ/ری‌استارت
  سرویس‌ورکر در میانهٔ درخواست — هم‌افین با مورد باز ۱.۷) و resolve در catch سنکرون؛ دیگر
  هیچ promise ای معلق نمی‌ماند و مدخل درخواست نشت نمی‌کند.
- **`check-page.js`:** `SendingPageInfo` حداکثر ۳ تلاش با backoff (۵/۱۰ ثانیه) فقط برای
  خطاهای گذرا (`network`/`timeout`/`no-response`/5xx)؛ خطاهای پیکربندی (401/413/…) فقط
  لاگ می‌شوند. `OnPageLoad` و `CheckNewOrders` نتیجهٔ خطادار را لاگ و متوقف می‌شوند؛
  `ActionHandler.Handle` روی ورودی فاقد فرمان لاگ خطا می‌دهد.
- **سرور:** سقف `RequestSizeLimit` در `Decision.Take` از ۵ به **۲۰MB** (زیر سقف پیش‌فرض
  ~۲۸.۶MB کسترل)؛ `BadRequest` ها بدنهٔ JSON با کد خطا گرفتند (`missing-context`/
  `bad-job-request`)؛ بدنهٔ JSON خطای 500 به `internal-server-error` تغییر کرد.
رفتار مسیر موفق بدون تغییر: همان `{trend, commands}` قبلی. تأیید نهایی با نخستین اجرای زنده.
**تأیید:** `agent-extension/controllers/core-messaging.js:38-69` (`FetchJson`)،
`agent-extension/controllers/background.js:27-49` (`Respond`)،
`agent-extension/controllers/background-messaging.js:33-50` (`Message`)،
`agent-extension/controllers/check-page.js:5-22,56-100` (گارد اسکوپ/retry/Orders)؛
`agent-extension/controllers/action-handler.js:8-12`؛
`core-decision-dotnet/Controllers/Decision.cs:13-34`؛ `core-decision-dotnet/Program.cs:91`؛
`dotnet build` بدون هشدار جدید، ۸۰ تست سبز، `node --check` روی هر پنج فایل JS اکستنشن.

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
⚠️ بازسازی اتصال‌ها به DI (نیمهٔ دوم این مورد = مورد ۳.۳) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) بسته شد — بنگرید به مدخل ۳.۳ همین فایل.

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

### ۳.۱ ORM بازتابی (Reflection) شکننده
(۱۴۰۵/۰۶/۱۸ — 2026-09-09) مهاجرت کامل لایهٔ داده به **Dapper 2.1.35**: حذف `Database.Insert/Update` بازتابی،
`BaseBusiness.cs`، `DbType.cs` و enum‌های `JobFilter`/`TrendFilter` (خود منبع شکنندگی — تطابق اجباری نام
پراپرتی با عضو enum). جایشان متدهای صریح و تایپ‌دار در `JobBusiness`/`TrendBusiness`:
`InsertFromSearch`، `InsertJob`، `UpdateScrapedJob(codeChanged, linkFound, includeState)`،
`UpdateStepstoneJob`، `UpdateJobContent`، `UpdateEvaluation(clearContent)`، `UpdateTries`، `ChangeState`،
`RemoveHtmlContent`، `ChangeOptions`، `Delete`؛ و `CreateTrend`، `UpdateActivity`، `Block` در ترند؛
`SaveSettings` در آژانس. همهٔ خواندن‌ها هم `Query<T>`/`ExecuteScalar` شدند (حذف حلقه‌های دستی
`SQLiteDataReader`)؛ شکل خروجی dynamic ها (`Report`/`JobRateReport`/`LoadByName`/داشبورد `Fetch`)
دست‌نخورده ماند. enum ها همچنان با نام عضو به‌صورت متن ذخیره می‌شوند (TypeHandler در
`Database/SqliteTypeHandlers.cs` برای خواندن + `ToString()` صریح در پارامترهای SQL — پارامتر enum در
Dapper به‌صورت عدد bind می‌شود) و `ResumeContext` با همان `JsonConvert` قبلی round-trip می‌کند.
توکن‌های `$x` به `@x` تغییر کردند؛ متن سایر SQL ها (از جمله `Q_INDEX`) بیت‌به‌بیت حفظ شد. سطح HTTP،
امضای `Database.Open()` و accessors (`Trend/Job/Agency/JobOption`) بدون تغییر ماند (DI — مورد ۳.۳ —
در همان تاریخ ۱۴۰۵/۰۶/۱۸ رفع شد؛ رجوع کنید به مدخل ۳.۳).
**رفع همراه (تنها تغییر رفتاری):** باگ upsert ترند — در `ON CONFLICT(AgencyID, Type) DO NOTHING` وقتی
`changes()==0` است، `TrendID` از ردیف موجود خوانده می‌شود نه از `last_insert_rowid()` کهنه (آینهٔ الگوی
درست `InsertJob` از مورد آرشیوشدهٔ ۱.۵).
**رفتارهای عمداً حفظ‌شده (bug-for-bug):**
- `ModifiedOn`: INSERT آن را به default دیتابیس (`current_timestamp` — UTC) واگذار می‌کند ولی هر UPDATE
  مقدار `DateTime.Now` (local) می‌نویسد — ترکیب UTC/محلی دست‌نخورده ماند.
- اسکرپ شغل Stepstone هر بار `Tries = NULL` می‌نویسد (`UpdateStepstoneJob` — خواندن قدیمی هرگز `Tries`
  را روی model پر نمی‌کرد).
- غلط املایی ستون `Efective` حفظ شد (مورد ۴).
**تأیید:** تست‌های طلایی فاز A (`core-decision-dotnet.Tests/PhaseAGoldenTests.cs` — ۱۵ تست، قبل از مهاجرت
نوشته و سبز، بعد از آن بدون تغییر دوباره سبز)؛ تست‌های API جدید فاز B
(`core-decision-dotnet.Tests/PhaseBNewApiTests.cs` — ۱۲ تست: conflict-backfill شغل/ترند، ماتریس پرچم‌های
`UpdateScrapedJob`، `Tries` تهی Stepstone، `UpdateActivity` بدون دست‌زدن به AgencyID، round-trip
TypeHandler ها، پروجکشن Report)؛ `dotnet build` بدون هشدار جدید؛ کل مجموعه ۸۰ تست سبز.

### ۳.۲ استفادهٔ بیش از حد `dynamic` و anonymous types
(۱۴۰۵/۰۶/۱۸ — 2026-09-09) حذف کامل `dynamic` و anonymous type ها از کل مسیر داده→داشبورد.
`JobOption.Settings` از `dynamic` به مدل تایپ‌دار **`JobOptionSettings`** (+`ResumeSettings` با
`[JsonProperty]` برای `resume/money/period` و `key/include_matched/parent` — Newtonsoft باقی ماند؛
seed موجود در `database/structure/job-option.sql` بدون تغییر با مدل منطبق است) تغییر کرد و
`JobOptionBusiness.FetchAll` با `DeserializeObject<JobOptionSettings>` می‌خواند؛ `ResumeKeyword` بدون
try/catch های `RuntimeBinderException` بازنویسی شد با حفظ دقیق معنای قبلی (`"resume": null` →
return false؛ نبودِ `key` → Title؛ نبودِ `include_matched` → true). خروجی‌های گزارش‌دهی به record های
تایپ‌دار رسیدند: **`AgencyRate`** (ارتقای `RateRow` خصوصی + حذف projection ناشناس در `JobRateReport`)،
**`TrendReportItem`** (`TrendBusiness.Report` — همان `?? "None"/""/"-"`)، **`JobListItem`**
(`JobBusiness.Fetch`)، **`AgencyInfo`** (`AgencyBusiness.LoadByName`)، و **`AgencyDashboardItem`**/
**`DashboardViewModel`** در `ReportController` (شامل `RevaluationProcess.GetReportObject` با
`TrendID = -1`). view های `jobs/trends/agencies/index` هم `@model` تایپ‌دار گرفتند (حذف cast های
`(List<dynamic>)`/`(dynamic[])` در index). کد مرده حذف شد: `AgencyBusiness.LoadSetting` (بدون caller)
و overload بدون-نوع `Database.Query(string,…)` (بدون caller؛ `ReadAll` مستقیم از `connection.Query`
می‌خواند). **تنها تغییر رفتاری (مصوب):** گزینهٔ حقوقیِ دارای `Settings` غیرتهی ولی بدون `money` — که
قبلاً binder crash کنترل‌نشده داشت — اکنون `Log.Warning("Invalid salary options")` + امتیاز ۰ می‌دهد
(قرینهٔ شاخهٔ Settings تهی)؛ نبودِ `period` مثل قبل ۰ تلقی می‌شود. JSON ریشهٔ غیرشیء (مثلاً آرایه) حالا
در زمان بارگذاری گزینه fail-fast استثنا می‌دهد (پذیرفته‌شده؛ دادهٔ seed شیءهای سالم است). تست‌ها هم
تابع شدند (`EligibilityFixture.Option/SalaryOption` با `JobOptionSettings`؛ دسترسی تایپ‌دار در تست
پروجکشن داشبورد فاز A و تست projection گزارش فاز B). خارج از scope ماند: `Job.cs` →
`database.ReadAll` (دیکشنری-محور، dynamic نیست).
**تأیید:** `dotnet build core-decision.sln` بدون هشدار جدید (فقط ASP0014 پیش‌موجود — مورد ۳.۱۶)؛
کل مجموعه **۸۰ تست سبز**؛ اجرای زندهٔ سرور و بازکردن `/`، `/report/jobs`، `/report/trends`،
`/report/agencies` — ۵۶ سطر شغل، ۵ کارت آژانس با ۷ دکمهٔ method، ۵ سطر trend؛ رندر بدون تغییر.

### ۳.۳ `Database.Open()` دستی همه‌جا — نقض DI
(۱۴۰۵/۰۶/۱۸ — 2026-09-09) حذف کامل الگوی service-locator: متدهای استاتیک `Database.Open()`/
`SetConfiguration` و فیلد استاتیک `connection_string` از `Database.cs` برداشته شدند تا کامپایلر
تضمین کند هیچ فراخوانی جامانده نیست (`rg "Database\.Open|Database\.SetConfiguration"` → صفر).
جایگزین: `IDatabaseFactory` با پیاده‌سازی `DatabaseFactory(IConfiguration)` — ثبت **Singleton**
(مصرف‌کننده‌های singleton: `Analyzer`، `TrendsCleanupService`) که همان connection string قبلی
(`Data Source={path};Foreign Keys=True`) را می‌سازد، PRAGMA های `journal_mode=WAL` و
`busy_timeout=5000` را مثل قبل اجرا می‌کند و هنگام نبودن `Database:Path` (به‌جای سکوت با
path تهی) fail-fast می‌شود؛ `Database` از طریق فکتوری به‌صورت **Scoped** ثبت و توسط کانتینر
به‌ازای هر درخواست dispose می‌شود. `Analyzer` فکتوری را در سازنده تزریق می‌کند و
`Agency.DatabaseFactory` بلافاصله پس از `Activator.CreateInstance` و پیش از `LoadFromDatabase`
(ساخت صفحات در `LoadPages`) ست می‌شود؛ صفحات از `Parent.DatabaseFactory.Open()` می‌خوانند.
`TrendsCheckpoint` اتصال را به‌صورت پارامتر می‌گیرد — سازنده‌های `(Analyzer, Database)` و
`(Analyzer, Database, Result)` — و دیگر `IDisposable` نیست (مالک اتصال نیست)؛ ثبتِ
`AddScoped<TrendsCheckpoint>` که تا امروز مرده بود فعال شد و `DecisionController.Orders` به‌جای
ساخت دستی از همان تزریق استفاده می‌کند. کنترلرهای `Decision`/`Job`/`Report` `Database`
اسکوپ‌شده را در سازنده تزریق می‌کنند (حذف همهٔ `using var database = Database.Open()` در اکشن‌ها)؛
`JobEligibilityHelper` با `IDatabaseFactory` ساخته می‌شود، `GetOptions(factory)` از همان فکتوری
می‌خواند و `RunRevaluateProcess(Analyzer, IDatabaseFactory)` هر دو پارامتر را صریح می‌گیرد
(سازندهٔ internal تست دست‌نخورده ماند).
**فیکس همراه:** اتصال مرده در `Page.GetUserPass` حذف شد — `AgencyBusiness.GetUserPass` متد نمونه
شد و از همان اتصالی که `Page` باز کرده می‌خواند (قبلاً `Page` یک اتصال بی‌استفاده باز می‌کرد و
متد استاتیک اتصال دومی باز می‌کرد)؛ نیمهٔ باز ماندهٔ مورد ۲.۸ نیز بسته شد.
**عمداً خارج از scope:** `Dictionaries.Open()/SetConfiguration` (لیست واژهٔ فقط‌خواندنی) با الگوی
استاتیک قبلی ماند. تغییر رفتاری مشاهده‌پذیر: اتصال کنترلرها به‌جای هر اکشن، یک‌بار در هر درخواست
باز و بسته می‌شود (هر درخواست یک اکشن؛ PRAGMA ها و رفتار همزمانی یکسان).
**تأیید:** `core-decision-dotnet/Database/IDatabaseFactory.cs` و
`core-decision-dotnet/Database/DatabaseFactory.cs:9-19` (فکتوری + PRAGMA ها + fail-fast)؛
`core-decision-dotnet/Database/Database.cs` — بدون هیچ عضو استاتیکِ اتصال؛
`core-decision-dotnet/Program.cs:27` (نمونهٔ واحد فکتوری)، `:54` (مهاجرت plaintext با همان
نمونه)، `:61-62` (ثبت Singleton/Scoped)؛ `core-decision-dotnet/Analyze/Analyzer.cs:5,11` و
`Analyze/Agency.cs:23`؛ `core-decision-dotnet/Analyze/TrendsCheckpoint.cs:5-27` (حذف IDisposable)؛
`core-decision-dotnet/Controllers/Decision.cs:7-11,86`؛ `Controllers/Job.cs:8-11`؛
`Controllers/Report.cs:7-9`؛ `core-decision-dotnet/Analyze/JobEligibilityHelper.cs:27-33,60-82`؛
`core-decision-dotnet/Analyze/Pages/Page.cs:29-33`؛ `Analyze/Pages/SearchPage.cs:18`؛
`Analyze/Pages/JobPage.cs:20,51`؛ `Analyze/Stepstone/StepstonePageSearch.cs:26`؛
`Analyze/Stepstone/StepstonePageJob.cs:21,42`؛
`core-decision-dotnet/Database/Business/AgencyBusiness.cs:67-77` (متد نمونه)؛
`core-decision-dotnet/TrendsCleanupService.cs:5,24-34`؛ `dotnet build` بدون هشدار جدید؛
کل مجموعه ۸۰ تست سبز (سازندهٔ عمومی `Database(SQLiteConnection)` و سازندهٔ internal تست دست‌نخورده).

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
