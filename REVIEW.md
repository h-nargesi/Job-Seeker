# گزارش مرور پروژه Job-Seeker

> مرور جامع کد، دیتابیس، اکستنشن مرورگر و معماری.
> هر مورد با اولویت، فایل/خط مربوطه و راه‌حل پیشنهادی ذکر شده است تا بتوان
> مورد به مورد بررسی و رفع شود.
>
> رنگ‌بندی اولویت: 🔴 بحرانی · 🟠 مهم · 🟡 متوسط · 🔵 خفیف

---

## فهرست

- [بخش ۱ — بحرانی (امنیت و از دست رفتن داده)](#بخش-۱--بحرانی-امنیت-و-از-دست-رفتن-داده)
- [بخش ۲ — مهم (باگ‌های منطقی و درستی)](#بخش-۲--مهم-باگهای-منطقی-و-درستی)
- [بخش ۳ — متوسط (طراحی و قابلیت نگهداری)](#بخش-۳--متوسط-طراحی-و-قابلیت-نگهداری)
- [بخش ۴ — خفیف (سبک و املا)](#بخش-۴--خفیف-سبک-و-املا)
- [بخش ۵ — پیشنهادات بهبود](#بخش-۵--پیشنهادات-بهبود)

---

## بخش ۱ — 🔴 بحرانی (امنیت و از دست رفتن داده)

### ۱.۱ تزریق SQL در `JobBusiness.Fetch` — ✅ رفع شد
پارامترهای داینامیک (`$a0, $c0, ...`) جایگزین الحاق رشته شد؛ باگ `where =` (حذف بی‌صدای فیلتر آژانس‌ها) نیز اصلاح شد.
**فایل:** `core-decision-dotnet/Database/Business/JobBusiness.cs` (خطوط ۱۴–۱۸)

ورودی کاربر (از Query String در `ReportController.Jobs`) مستقیماً در کوئری الحاق می‌شود:

```csharp
where += $" AND Agency.Title IN ('{string.Join("','", agency_titles)}')";
where =  $" AND Job.Country IN ('{string.Join("','", country_codes)}')";
```

**تأثیر:** تزریق SQL کامل از طریق URL `?agencies=...`.
**راه‌حل:** استفاده از پارامترهای داینامیک (`$a0, $a1, ...`) و `database.Parameter(...)`.

---

### ۱.۲ اجرای SQL دلخواه در `JobController.Setting` — ✅ حفظ شد زیر احراز هویت
**فایل:** `core-decision-dotnet/Controllers/Job.cs` (خطوط ۱۶۵–۲۰۰)

اندپوینت POST کوئری خام را از بدنه درخواست گرفته و اجرا می‌کند:

```csharp
database.Execute(options.Query);              // Type == "E"
var result = database.ReadAll(options.Query); // Type == "Q"
```

**تأثیر:** هر کسی که به سرور دسترسی داشته باشد می‌تواند DROP/DELETE/READ انجام دهد.
**راه‌حل:** حذف این اندپوینت از پروداکشن، یا پشت احراز هویت مدیر و IP-whitelist.

---

### ۱.۳ نبود احراز هویت/مجوزدهی — ✅ رفع شد
احراز هویت تک‌کاربره: هدر `X-API-Key` برای اکستنشن + کوکی امضاشده (DataProtection) از طریق `/auth/login` برای داشبورد.
**فایل:** `Program.cs` — هیچ `UseAuthentication`/`UseAuthorization`/فیلتری وجود ندارد.

تمام عملیات حساس (Clean، Reset، Setting، خواندن شغل‌ها و رزومه) بدون لاگین در دسترس است.
**راه‌حل:** اضافه کردن احراز هویت (حتی Basic Auth/IP filter برای ابزار داخلی).

---

### ۱.۴ ذخیره اعتبارنامه به‌صورت Plain-text — ✅ رفع شد (at-rest)
رمزنگاری AES-GCM با پیشوند `enc:` + مهاجرت خودکار در startup (`SecretProtector`). ⚠️ باقی‌مانده: گذر plaintext از HTTP هنگام fill فرم لاگین — نیازمند TLS.
**فایل:** `database/structure/agency.sql` (ستون‌های `UserName`, `Password`)

نام کاربری و رمز عبور سایت‌های کاریابی متن‌ساده است و با `AgencyBusiness.GetUserPass` خوانده می‌شود.
**راه‌حل:** رمزنگاری (DPAPI/AES) یا استفاده از Secret Manager.

---

### ۱.۵ `LastInsertRowId` نادرست هنگام Conflict — ✅ رفع شد
پس از insert، با `SELECT changes()` تشخیص conflict و در صورت تکراری بودن، ID رکورد موجود از طریق `Fetch(agency, code)` خوانده می‌شود.
**فایل:** `Database/Database.cs` (۶۷–۷۱) و `Database/Business/JobBusiness.cs` (۱۵۲–۱۵۷)

با `ON CONFLICT(...) DO NOTHING` اگر رکورد تکراری باشد، INSERT ای رخ نمی‌دهد ولی
`last_insert_rowid()` مقدار **قبلی** اتصال را برمی‌گرداند.

**تأثیر:** `job.JobID` پس از Conflict یک ID غلط/متعلق به رکورد دیگر می‌گیرد.
**راه‌حل:** پس از Save، با `Fetch(agency_id, code)` شناسه واقعی را خواند یا از
`ON CONFLICT DO UPDATE` استفاده کرد.

---

### ۱.۶ مدیریت استثناء ناقص → نشت Stack Trace — ✅ رفع شد
`UseExceptionHandler` سراسری اضافه شد: لاگ کامل + پاسخ 500 بدون جزئیات. DeveloperExceptionPage طبق پیش‌فرض فقط در Development فعال است.
**فایل:** همه Controller‌ها (مثلاً `DecisionController.Take` خط ۳۶ `throw;`)

هیچ `UseExceptionHandler` یا `UseDeveloperExceptionPage` در `Program.cs` پیکربندی نشده؛
خطاها به‌صورت ۵۰۰ خام برمی‌گردند.

---

### ۱.۷ نبود محدودیت حجم درخواست — ✅ رفع شد
`[RequestSizeLimit(5_000_000)]` روی `DecisionController.Take`.
`DecisionController.Take` کل HTML صفحه (`document.documentElement.outerHTML`) را
می‌پذیرد بدون محدودیت حجم. قابل سوءاستفاده برای اشغال منابع.

---

## بخش ۲ — 🟠 مهم (باگ‌های منطقی و درستی)

### ۲.۱ باگ تشخیص دوره حقوق (`EvaluateSalaryScore`) — ✅ رفع شد
**فایل:** `Analyze/JobEligibilityHelper.cs` (خطوط ۳۳۷–۳۴۱)

```csharp
if (period_index < 0 && ...)
```

`period_index` از تنظیمات = ۴ است و هرگز `< 0` نمی‌شود؛ پس **تشخیص month/year
هیچ‌وقت اجرا نمی‌شود** و همیشه شاخه default (تقسیم بر ۱۲ اگر > ۳۵۰۰۰) می‌رود.
**راه‌حل:** `if (period_index > 0 && matched.Groups[period_index].Success && ...)`.

---

### ۲.۲ `IndeedPageJob.JobFallow` سلکتور اشتباه — ⚠️ فیکس به‌نیت (کلیک روی `a[title*='Add to favourites']`)؛ نیازمند تأیید با DOM زندهٔ این‌دیس
**فایل:** `Analyze/Indeed/IndeedPageJob.cs` (۲۷)

این‌دیس روی `reg_job_adding` («Add to favourites») چک می‌شود ولی `button.jobs-save-button`
(کلاس مخصوص لینکدین) را کلیک می‌کند → کلیک روی عنصر اشتباه/غیرموجود.

---

### ۲.۳ `check-page.js` هندلر اشتباه — ✅ رفع شد
**فایل:** `agent-extension/controllers/check-page.js` (۲۰)

```js
.addEventListener("click", BackgroundMessaging.Scopes(true), false);
```

به‌جای تابع، **نتیجه‌ی فراخوانی** (یک Promise) به‌عنوان handler پاس داده شده. باید
`() => BackgroundMessaging.Scopes(true)` باشد.

---

### ۲.۴ `keyboard.js` باگ ارجاع پیش از تعریف و مرده بودن — ✅ فایل حذف شد
**فایل:** `agent-extension/controllers/keyboard.js` (۹)

```js
const modifier = (typeof (modifiers) === "object") ? modifier : {}; // self-reference
```

همچنین این فایل در `manifest.json` بارگذاری نمی‌شود → کد مرده.
APIهای `createEvent`/`initEvent` نیز منسوخ‌اند.

---

### ۲.۵ `Glassdoor` ناقص و مستعد کرش — ✅ رفع شد (`GetSubPages` آرایهٔ خالی برمی‌گرداند؛ کامنت‌های کپی‌پیست حذف شدند)
**فایل:** `Analyze/Glassdoor/Glassdoor.cs`

`GetSubPages()` مقدار `null` برمی‌گرداند و `LoadPages` با
`foreach (var type in GetSubPages())` روی null → **NullReferenceException**.
فعلاً چون Glassdoor در `agency.sql` INSERT نشده، `LoadByName` null می‌دهد و زود بازمی‌گردد؛
اما باگ بالقوه است. کدهای کامنت‌شده نیز به `BaytPage` ارجاع می‌دهند (کپی‌پیست).

---

### ۲.۶ `TrendsCheckpoint.LoadAndUpdateCurrentTrend` همیشه Rollback می‌زند — ✅ رفع شد (الگوی استاندارد try/Commit/catch-Rollback)
**فایل:** `Analyze/TrendsCheckpoint.cs` (۵۲–۸۴)

بلاک `try/finally` در `finally` همیشه `database.Rollback()` می‌زند. اگر `Commit()` موفق
شده باشد، transaction null است و Rollback بی‌اثر است، ولی منطق گیج‌کننده و شکننده است.
**راه‌حل:** استفاده از bool flag یا مهاجرت به یک helper تراکنشی.

---

### ۲.۷ SQLite و همزمانی (احتمال database is locked) — ✅ رفع شد (`PRAGMA journal_mode=WAL` + `busy_timeout=5000` در هر `Open`)

برای هر درخواست یک اتصال جدید باز می‌شود (`Database.Open()` در ده‌ها نقطه)، بدون WAL mode
و بدون `BusyTimeout`. تب‌های همزمان مرورگر که به `/decision/take` می‌زنند و می‌نویسند →
قفل تداخل.

**راه‌حل:** فعال کردن `PRAGMA journal_mode=WAL;` و `BusyTimeout=5000`، یا استفاده از یک
اتصال اشتراکی/مجموعه‌ای.

---

### ۲.۸ `JobEligibilityHelper` در هر شغل ۳ اتصال DB باز می‌کند — ✅ کش static برای `JobOption[]` (ابطال در `ReloadSettings` و بعد از Execute کنسول Setting)؛ بازسازی اتصال‌ها به DI خارج از دامنهٔ این مرحله باقی می‌ماند
**فایل:** `Analyze/Pages/JobPage.cs` (۱۸)

`new JobEligibilityHelper()` در سازنده `database`, `dictionaries`, `options` را باز/بارگذاری
می‌کند. برای هر صفحه شغلی تکرار می‌شود → پرهزینه.
**راه‌حل:** تبدیل به سرویس Scoped در DI.

---

### ۲.۹ اثر جانبی در متد شمارش (`FetchFromCount`) — ✅ رفع شد (UPDATE ریست Revaluationها به `RunRevaluateProcess` منتقل شد؛ رفتار حفظ شد)
**فایل:** `Database/Business/JobBusiness.cs` (۳۷–۴۴)

`FetchFromCount` ابتدا `Q_FETCH_UPDATE_REVAL` (یک UPDATE سنگین) اجرا می‌کند. شمارش
نباید بنویسد.

---

### ۲.۱۰ اثر جانبی DELETE در GET صفحه اصلی
**فایل:** `Controllers/Report.cs` (`GetTrends` خط ۹۴) که از `Index` (GET `/`) صدا زده
می‌شود و `DeleteExpired` را فرامی‌خواند → نوشتن در درخواست GET.

---

### ۲.۱۱ `VACUUM` در هر Clean — ✅ رفع شد (`?vacuum=true`؛ پیش‌فرض خاموش)
**فایل:** `JobBusiness.cs` (۱۸۱)

`Clean` همیشه `VACUUM` می‌زند که کل فایل را بازنویسی و دیتابیس را قفل می‌کند. باید
دوره‌ای/در زمان بیکاری اجرا شود.

---

### ۲.۱۲ `FillSpace` احتمال استثنای منفی — ✅ رفع شد (`Math.Max(0, ...)`)
**فایل:** `Analyze/TrendsCheckpoint.cs` (۳۱۰–۳۱۳)

`new string(' ', max - text.Length)` اگر `text.Length > max` شود →
`ArgumentOutOfRangeException`.

---

### ۲.۱۳ فیلدهای استاتیک برای چینش لاگ ناامن در برابر همزمانی
**فایل:** `TrendsCheckpoint.cs` (`AgencyNameLength`, `TrednTypeLength` خط ۳۰۷–۳۰۸)

در چند درخواست همزمان تغییر می‌کنند (هرچند ظاهری).

---

### ۲.۱۴ `DecisionController.Running` کلید نامعتبر → ۵۰۰ — ✅ رفع شد (`TryGetValue` + `NotFound()`)
**فایل:** `Controllers/Decision.cs` (۱۰۱)

`analyzer.Agencies[context.Agency]` با نام اشتباه `KeyNotFoundException` می‌دهد.

---

### ۲.۱۵ منطق مرتب‌سازی جبری و مستندنشده
**فایل:** `JobBusiness.cs` (`Q_INDEX`) — استفاده از `WHERE Ranking <= (12 / Category)`
با تقسیم صحیح و فرمول امتیاز نمایی (در کامنت LaTeX). بدون تست، درک/نگهداری سخت است.

---

### ۲.۱۶ `IndeedPageSearch` از `reg_job_url` (`/rc/clk?jk=`) استفاده می‌کند — ⚠️ فقط TODO ثبت شد؛ نیازمند تأیید با DOM زندهٔ این‌دیس (regex دست‌نخورده)

احتمالاً در DOM جدید این‌دیس وجود ندارد (URL بازنمایی `/viewjob?jk=` است) → استخراج
شغل این‌دیس ممکن است خالی بماند.

---

### ۲.۱۷ `IndeedPageJob.ChceckJob` استثنا برای کنترل جریان — ✅ رفع شد (`State = NotApproved` + Log؛ skip ارزیابی در `JobPage.IssueCommand`)
**فایل:** `Indeed/IndeedPageJob.cs` (۴۱–۴۵)

پرتاب `Exception` روی «region not supported» به‌جای تنظیم State مناسب.

---

### ۲.۱۸ `Q_CLEAN_ATTENTION` زیرکوئری مبهم — ✅ کامنت شفاف‌ساز اضافه شد (رفتار فعلی: top-100 سراسری)
**فایل:** `JobBusiness.cs` (۳۰۹–۳۱۳) — نگه‌داشتن HTML برای ۱۰۰ رکورد برتر بر اساس Score
بدون فیلتر RegTime یکسان در زیرکوئری.

---

### ۲.۱۹ `Analyze.Agencies` lazy-load الگوی قفل شکسته — ✅ رفع شد (بررسی داخل lock برای هر دو property؛ `ClearAgencies` زیر همان lock)
**فایل:** `Analyze/Analyzer.cs` (۱۱–۴۱)

بررسی `Count == 0` بیرون قفل سپس قفل؛ `Agencies` و `AgenciesByID` مستقل چک می‌کنند.
همراه با `ClearAgencies` می‌تواند data race ایجاد کند (هرچند قفل داخلی از فساد جلوگیری
می‌کند، بارگذاری مضاعف/مقادیر قدیمی محتمل است).

---

## بخش ۳ — 🟡 متوسط (طراحی و قابلیت نگهداری)

### ۳.۱ ORM بازتابی (Reflection) شکننده
**فایل:** `Database/Database.cs` (`Insert`/`Update`) + `JobFilter`/`TrendFilter`

وابستگی به تطابق دقیق نام ویژگی‌ها با اعضای Enum. تغییر نام → حذف خاموش ستون.
هیچ ایمنی زمان‌کامپایل نیست. پیشنهاد: مهاجرت به Dapper یا EF Core (با SQLite).

---

### ۳.۲ استفادهٔ بیش از حد `dynamic` و anonymous types

در سراسر لایه‌ها (`List<dynamic>`, `JobOption.Settings` dynamic, خروجی
`AgencyBusiness.LoadByName`). نوع‌ها گم می‌شوند، Intellisense/کامپایل ضعیف.

---

### ۳.۳ `Database.Open()` دستی همه‌جا — نقض DI

به‌جای تزریق، اتصال استاتیک/دستی باز می‌شود. تست‌پذیری سخت.
پیشنهاد: `IDatabaseFactory` به‌صورت Scoped در DI.

---

### ۳.۴ تکرار کد `LoadJob` در Stepstone
**فایل:** `Analyze/Stepstone/StepstonePageJob.cs`

به‌جای ارث‌بری از `JobPage`، تمام `IssueCommand`/`LoadJob` بازنویسی شده. الگوی پایه را
بی‌اثر می‌کند. Stepstone باید از `JobPage` مشتق شود و `StepstonePage` از `PageBase`.

---

### ۳.۵ تکرار منطق `Save` (BaseBusiness در برابر JobBusiness/TrendBusiness)
**فایل:** `BaseBusiness.cs` در برابر `JobBusiness.cs` (۱۳۶–۱۵۹) — استخراج `id` یکسان
تکرار شده. DRY.

---

### ۳.۶ `ResumeContext` سریال‌سازی JSON سفارشی با Regex
**فایل:** `Analyze/Models/ResumeContext.cs` (`QouteSerializer`/`QouteDeserializer`)

حذف/افزودن کوتیشن کلیدها با Regex → فرمت غیراستاندارد که برای مقادیر حاوی `:` یا
کلیدهای غیرکلمه‌ای می‌شکند. پیشنهاد: JSON استاندارد.

---

### ۳.۷ `Extensions.Shift` مرده و باگ‌دار — ✅ حذف شد
**فایل:** `Basics/Extensions.cs` (۳۳–۳۸) — `Insert(1, ...)` همیشه در اندیس ۱ درج می‌کند
(به‌جای shift واقعی). استفاده‌ای هم ندارد.

---

### ۳.۸ `Page.CompareTo` بیش‌حد پیچیده — ✅ ساده شد (`Order.CompareTo`)
**فایل:** `Analyze/Pages/Page.cs` (۱۸–۲۵) — می‌تواند `Order.CompareTo(other.Order)` باشد.

---

### ۳.۹ نبود تست واحد

منطق پیچیده امتیازدهی حقوق، الگوریتم مرتب‌سازی، تشخیص زبان، scoring → هیچ تستی وجود
ندارد. پروژه‌ی تست بسازید (xUnit/NUnit) و حداقل برای `EvaluateEligibility`,
`EvaluateSalaryScore`, `LanguageIsMatch`.

---

### ۳.۱۰ وابستگی شکننده به ساختار HTML سایت‌ها

تمام scraper‌ها بر Regex/ساختار دقیق تکیه می‌کنند. هیچ سازوکار هشدار هنگام تغییر ساختار
وجود ندارد. پیشنهاد: لاگ/متریک تعداد شغل‌های استخراج‌شده و هشدار اگر صدا شد.

---

### ۳.۱۱ Serilog با ASP.NET Logging ادغام نشده — ✅ رفع شد (`builder.Logging.AddSerilog()`)
**فایل:** `Program.cs` — `builder.Logging.AddSerilog()` فراموش شده؛ دو سیستم لاگ جدا.

---

### ۳.۱۲ مسیر لاگ ممکن است نباشد — ✅ رفع شد (`Directory.CreateDirectory` در startup)

`logs/E.log` — اگر پوشه موجود نباشد Serilog File sink استثنا می‌دهد. ایجاد پوشه در
startup لازم است.

---

### ۳.۱۳ `appsettings.json` در برابر Development فقط در casing (`Logs`/`logs`) — ✅ یکسان شد (`logs/E.log`)

روی فایل‌سیستم حساس به حروف (لینوکس) مشکل‌ساز.

---

### ۳.۱۴ `job-seeker.sh` مسیر هاردکد

`cd "/home/rayan/Developing/Job Seeker/publish"` قابل‌حمل نیست.

---

### ۳.۱۵ `GetTextContent` فیلتر تگ ناقص
**فایل:** `JobEligibilityHelper.cs` (۱۴۹–۱۶۷)

فقط والد مستقیم script/head/style چک می‌شود؛ `<noscript>`, `<svg>`, CSS/JS توکار
نشت می‌کنند. (مربوط به todo.txt: «Ignore javascript/json on content?»)

---

### ۳.۱۶ `Program.cs` APIهای منسوخ

`UseEndpoints` در NET 8 توصیه نمی‌شود؛ می‌توان مستقیم `MapControllers` زد.
`appsettings` value `.ToString()` با null → NRE.

---

### ۳.۱۷ توابع سراسری در view‌ها

`onclick="apply(...)"`, `ordering()`, `reset()` به توابع global در `wwwroot/scripts/*`
وابسته‌اند (Encapsulation ضعیف).

---

### ۳.۱۸ `Q_FETCH_FROM_COUNT` با `FetchFrom` ناسازگار

شمارش `State != Revaluation` را فیلتر نمی‌کند ولی خواندن می‌کند → ناهماهنگی در گزارش
پیشرفت Revaluation.

---

### ۳.۱۹ فایل‌های منتشرشده داخل ریپو

`jobs.photon-ai.ir/` و `jobs.photon-ai.ir.zip` (شامل database/runtimes/wwwroot) در فضای
کاری‌اند و باید از ریپو جدا شوند (deployment artifact).

---

### ۳.۲۰ پوشه `.github/workflows` خالی

CI/CD تعریف‌شده‌ای وجود ندارد.

---

## بخش ۴ — 🔵 خفیف (سبک و املا)

غلط‌های املایی که در کل پایه‌کد/اسکیما پخش‌اند (هنگام بازنویسی اصلاح شوند):

| فعلی | درست |
|------|------|
| `Efective` | **Effective** (ستون JobOption، در SQL و کوئری) |
| `reumse` | **resume** (دسته‌بندی در `job-option.sql`) |
| `mounths` | **months** |
| `DaysPriod` | **DaysPeriod** |
| `spliter` | **splitter** |
| `miliseconds` | **milliseconds** |
| `SimlpeSerialize`/`SimlpeDeserialize` | **Simple…** |
| `SerializeChecktSyntaxt`/`DeserializeChecktSyntaxt` | **CheckedSyntax** |
| `TrednTypeLength` | **TrendTypeLength** |
| `ChceckJob` | **CheckJob** |
| `portrate.png` | **portrait** |
| `fallow` (در `JobFallow`) | **follow** |
| `langu_count` | **lang_count** |
| `extnesion` | **extension** |
| `contaner` (در layout.cshtml کلاس) | **container** |
| `Counrty Filter` (placeholder در index.cshtml) | **Country** |

ناهماهنگی نام‌گذاری: فیلد `page_action` (snake_case) در کنار پراپرتی `Action` در `Command`.

سایر موارد خفیف:
- `PageContext`/`RunningMethodContext` ویژگی `[Serializable]` بلااستفاده (JSON سریالایز می‌شود).
- `ResumeContext.Version => 42` عدد جادویی بدون مهاجرت.
- `keyboard.js`/`Extensions.Shift` کد مرده — حذف شوند.
- نبود `.editorconfig`؛ ترکیب tab/space و استایل نامنظم.

---

## بخش ۵ — 💡 پیشنهادات بهبود

1. **معماری:** لایه‌بندی Clean (Controllers → Services → Repositories) با interface‌ها و تزریق وابستگی کامل.
2. **دسترسی داده:** مهاجرت به **Dapper** یا **EF Core** (پشتیبانی SQLite، ایمنی نوع‌دار، Migrations).
3. **امنیت:** احراز هویت + مجوزدهی + حذف اندپوینت SQL دلخواه + رمزنگاری اعتبارنامه + HTTPS/HSTS.
4. **همزمانی SQLite:** WAL mode، `BusyTimeout`، اتصال از طریق DI (Scoped).
5. **پایداری scraper:** متریک شمارش شغل + هشدار هنگام افت + تست snapshot برای هر آژانس.
6. **تست:** پروژه xUnit با پوشش برای امتیازدهی/حقوق/زبان/مرتب‌سازی.
7. **مشاهده‌پذیری:** داشبورد وضعیت آژانس‌ها، هشدار خطا (Serilog + Seq/Email).
8. **پیکربندی:** انتقال تنظیمات حساس به User-Secrets/Environment؛ رفع مسیرهای هاردکد.
9. **CI/CD:** اضافه کردن workflow برای build/test در `.github/workflows`.
10. **مستندسازی:** توضیح الگوریتم امتیازدهی (فرمول LaTeX در `JobBusiness.cs`) و حالت‌های ماشین Trend در یک سند (بخشی از آن در `TrendsCheckpoint.md` هست ولی ناقص).
11. **todo.txt** موارد باز: افزودن Qatar/Oman (بخشی در SQL هست)، bayt/qatarliving/omanjobs، popup هنگام باز کردن صفحه، حذف ستون HTML، نادیده‌گرفتن JS/JSON در محتوا.
