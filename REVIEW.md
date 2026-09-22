<div dir="rtl" lang="fa">

# گزارش مرور پروژه Job-Seeker — موارد باز

> موارد رفع‌شده‌ای که با کد تطبیق داده شدند به [`archive/review-2026-09.md`](archive/review-2026-09.md)
> منتقل شدند (بررسی: ۱۴۰۵/۰۶/۰۹ — 2026-08-31). این فایل فقط موارد باز/ناقص را دنبال می‌کند.
>
> رنگ‌بندی اولویت: 🔴 بحرانی · 🟠 مهم · 🟡 متوسط · 🔵 خفیف

---

## فهرست

- [بخش ۱ — بحرانی](#بخش-۱--بحرانی)
- [بخش ۲ — مهم (باگ‌های منطقی و درستی)](#بخش-۲--مهم-باگهای-منطقی-و-درستی)
- [بخش ۳ — متوسط (طراحی و قابلیت نگهداری)](#بخش-۳--متوسط-طراحی-و-قابلیت-نگهداری)
- [بخش ۴ — خفیف (سبک و املا)](#بخش-۴--خفیف-سبک-و-املا)
- [بخش ۵ — پیشنهادات بهبود](#بخش-۵--پیشنهادات-بهبود)

---

## بخش ۱ — 🔴 بحرانی

> ۱۴۰۵/۰۶/۱۸ (2026-09-09): پس از مرور کامل ارتباط سرور↔اکستنشن و الگوریتم تصمیم،
> موارد جدید **۱.۵–۱.۷**، **۲.۱۷–۲.۲۲** و **۳.۲۱–۳.۲۵** ثبت شد. سیستم تک‌کاربره است؛
> بنابراین موارد همزمانی ثبت‌شده از جنس همزمانی *داخلی*‌اند (چند تب آژانس موازی +
> poller داشبورد)، نه چندکاربره — اولویت آنها به‌تناسب تنظیم شده ولی ریشه‌ای باقی‌اند.
>
> موارد ۱.۵ (مسیریابی پاسخ پس‌زمینه بر اساس `tab.index`)، ۱.۶ (زنجیرهٔ پیام‌رسانی بدون
> timeout/چک ok/catch) و ۱.۷ (هویت تب فقط در حافظهٔ SW) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند — ۱.۷ با کامیت `d2861ff` (binding
> پایدار + adoption بر اساس آژانس)؛ تأیید نهایی با نخستین اجرای زنده.
> مورد باز در این بخش باقی نمانده است.

---

## بخش ۲ — 🟠 مهم (باگ‌های منطقی و درستی)

> مورد ۲.۲ (تأیید فیکس `JobFallow` این‌دیس با DOM زنده) در ۱۴۰۵/۰۶/۱۳ (2026-09-04) حذف شد:
> fav کردن شغل روی سایت لازم نیست (کاتالوگ شغل‌ها در خود سیستم موجود است)؛ ارسال رزومه در
> صورت نیاز به‌صورت دستی انجام می‌شود. (کد `JobFallow` دست‌نخورده و فعال مانده است.)
>
> آخرین مورد باز بخش ۲ (۲.۱۶ — الگوی `/rc/clk?jk=` در استخراج شغل این‌دیس) در ۱۴۰۵/۰۶/۱۷
> (2026-09-08) با الگوی چندشکلی `IndeedSerp` رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شد — تأیید نهایی با نخستین اجرای زنده.
>
> موارد ۲.۱۷ (TTL بدون heartbeat)، ۲.۱۸ (رزرو بدون lease) و ۲.۲۲ (اجرای open با
> `window.open`) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با کامیت `8503003` رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند — تأیید نهایی با نخستین اجرای زنده.
> موارد ۲.۱۹ (اتمیزم Orders×Take با تراکنش `BEGIN IMMEDIATE` + قفل ایستا دور کل پاس
> چک‌پوینت) و ۲.۲۱ (مرتب‌سازی/سقف عددی با ستون `Attempts` + مهاجرت backfill در
> `installation.sh`) در ۱۴۰۵/۰۶/۲۰ (2026-09-11) رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند — تأیید نهایی با نخستین اجرای زنده.
> مورد ۲.۲۰ (قفل per-agency روی `AnalyzeContent`) در ۱۴۰۵/۰۶/۲۰
> (2026-09-11) رفع و به [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شد —
> تأیید نهایی با نخستین اجرای زنده.
>
> ۱۴۰۵/۰۶/۲۱ (2026-09-12): با راه‌اندازی سوئیت تست JS اکستنشن
> (`agent-extension/tests/` — `npm test`)، مورد جدید **۲.۲۳** ثبت شد.
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): مرور پرفورمنسی `agent-extension` — موارد **۲.۲۴**
> (مسلح‌شدن تایمر بستن تب پیش از تطبیق دامنه — بستن تب‌های بی‌ربط بعد از ۹۰s)
> و **۲.۲۵** (نشت حافظهٔ ۴۵ ثانیه‌ای تایم‌اوت `BackgroundMessaging` با
> نگه‌داشتن HTML کامل صفحه در کلوژر) ثبت و در همان تاریخ رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند.
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): مرور پرفورمنسی `core-decision-dotnet` — موارد
> **۲.۲۶** (حلقهٔ Revaluation با انتقال دادهٔ درجه‌دو)، **۲.۲۷** (INSERT های
> autocommit پی‌درپی در SearchPage) و **۲.۲۸** (اتصال‌سازی پرتعداد مسیر داغ +
> نشت اتصال Dictionaries) در همان تاریخ رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند؛
> **۲.۲۹** (نبود ایندکس ثانویه) در همین بخش باز ثبت شد و نکات درجه‌دو/سهٔ
> همان مرور در بخش ۳ (۳.۳۴–۳.۳۹).
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): مرور پرفورمنسی `ai-worker` — موارد بحرانی همان مرور
> (**۲.۳۰**–**۲.۳۲**) در همان تاریخ رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند؛ نکات
> متوسط همان مرور در بخش ۳ (۳.۴۰–۳.۴۴).
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): مرور پرفورمنسی `assistant-extension` — موارد بحرانی
> همان مرور (**۲.۳۳**–**۲.۳۷**) در همان تاریخ رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند؛ نکات
> متوسط همان مرور در بخش ۳ (۳.۴۵–۳.۴۸). همراه همین فیکس، اسکریپت `test`
> پکیج (<code dir="ltr">node --test tests/</code>) که روی Windows/Node 22.9 با
> `MODULE_NOT_FOUND` می‌شکست به الگوی glob تغییر کرد (قرینهٔ fix مشابه
> `agent-extension`).

### ۲.۲۳ فرمان recheck بدون OnPageLoad منجر به TypeError می‌شود
**فایل:** `agent-extension/controllers/action-handler.js` (متد `Execute`، case "recheck")

وقتی `ActionHandler.OnPageLoad` تنظیم نشده باشد فقط هشدار داده می‌شود و بلافاصله
`ActionHandler.OnPageLoad()` صدا زده می‌شود → `TypeError: ... is not a function` و
رد شدن باقی‌ماندهٔ زنجیرهٔ فرمان‌ها. در عمل check-page.js این فیلد را قبل از استفاده ست
می‌کند، ولی هر مسیر دیگری (مثلاً فرمان recheck از مسیر سفارش‌ها) کرش می‌کند.
تأییدشده با `tests/action-handler.test.js` («recheck with OnPageLoad unset ...»).
**اقدام:** `if (ActionHandler.OnPageLoad) ActionHandler.OnPageLoad(); else console.warn(...)`.

### ۲.۲۹ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) نبود ایندکس ثانویه روی Job (و LastActivity روی Trend)
**فایل:** `database/structure/job.sql`، `database/structure/trend.sql`

روی جدول Job به‌جز PK و <code dir="ltr">unique(AgencyID, Code)</code> هیچ
ایندکسی وجود ندارد؛ این کوئری‌های مسیر داغ/نگهداشت full-scan می‌شوند و با
رشد جدول (هر ردیف با متن کامل `Html`/`Content`) خطی بدتر می‌شوند:

- `Q_FETCH_FIRST` (`State = 'Saved' AND AgencyID AND Attempts < 4`) — داخل قفل
  سراسری چک‌پوینت صدا زده می‌شود؛ مرتب‌سازی عبارتی‌اش (`ORDER BY Attempts = 0
  DESC, Attempts DESC, JobID`) نیز اساساً ایندکس‌پذیر نیست.
- `RevaluationScope` (`State IN (...) AND ModifiedOn <= @date`) — حلقهٔ Revaluation.
- `Q_CLEAN`/`Q_CLEAN_ATTENTION`/`Q_CLEAN_NOT_APPROVED` (شرط روی `RegTime`/`State`).
- برای Trend: شرط `DATETIME(LastActivity) <= @cutoff` در سه دستور `DeleteExpired`
  (جدول کوچک است ولی ایندکس ساده ارزان تمام می‌شود).

**اقدام:** افزودن به `database/structure/job.sql` + migration در
`installation.sh` (هم‌الگوی backfill مورد آرشیوشدهٔ ۲.۲۱):
<code dir="ltr">ix_job_state_modified ON Job(State, ModifiedOn)</code>،
<code dir="ltr">ix_job_agency_state ON Job(AgencyID, State, Attempts)</code> و
<code dir="ltr">ix_job_reg ON Job(RegTime)</code>؛ برای Trend ایندکس روی
`LastActivity`. سپس بازنگری `Q_FETCH_FIRST` برای مرتب‌سازی ایندکس‌پذیر (مثلاً
نگه‌داشتن اولین سطر با یک MAX ساده یا ستون کمکی).

---

## بخش ۳ — 🟡 متوسط (طراحی و قابلیت نگهداری)

> مورد ۳.۱ (ORM بازتابی شکننده) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با مهاجرت کامل لایهٔ داده به Dapper رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شد — به همراه فیکس باگ upsert ترند؛ رفتارهای حفظ‌شده
> (ترکیب UTC/local مهلت `ModifiedOn` و `Tries = NULL` در اسکرپ Stepstone) در همان مدخل آرشیو مستند شد.
>
> مورد ۳.۲ (استفادهٔ بیش از حد `dynamic` و anonymous types) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با مدل‌ها/record های
> تایپ‌دار (`JobOptionSettings`، `AgencyRate`، `TrendReportItem`، `JobListItem`، `AgencyInfo`،
> `AgencyDashboardItem`، `DashboardViewModel`) و view های `@model`دار رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شد — به همراه حذف کد مرده (`LoadSetting` و overload
> داینامیک `Query`)؛ گزینهٔ حقوقی بدشکل اکنون هشدار + امتیاز ۰ می‌دهد به‌جای کرش binder.
>
> مورد ۳.۳ (`Database.Open()` دستی همه‌جا — نقض DI) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با حذف کامل
> `Open`/`SetConfiguration` استاتیک و جایگزینی با `IDatabaseFactory` (Singleton) + `Database` اسکوپ‌شده
> در DI رفع و به [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شد — به‌همراه فیکس اتصال مرده در
> `Page.GetUserPass`؛ نیمهٔ باز ماندهٔ مورد ۲.۸ آرشیوشده نیز بسته شد.
>
> مورد ۳.۴ (تکرار کد `LoadJob` در Stepstone) در ۱۴۰۵/۰۶/۲۹ (2026-09-20) با یکسان‌سازی کامل
> Stepstone با الگوی استاندارد صفحات پایه (`LoginPage`/`AuthPage`/`SearchPage`/`JobPage`/
> `OtherPages` + `interface StepstonePage`) رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شد — به‌همراه پنج تغییر
> رفتاری مصوب (حذف ریست Tries/Attempts ری‌اسکرپ، حذف ریدایرکت پروفایل، اصلاح قطبیت
> صفحه‌بندی، seed متد DE، فعال‌شدن `GetMainHtml`).
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): در مرور پرفورمنسی `agent-extension`، موارد **۳.۲۹** (کش scopes
> فقط در static سرویس‌ورکر — fetch تکراری `decision/scopes` پس از هر ری‌استارت SW روی هر
> لود صفحهٔ دلخواه) و **۳.۳۰** (آلارم `trend-orders` همیشه‌فعال — بیدار کردن SW هر
> ۳۰ ثانیه حتی با ordering خاموش) رفع و به
> [`archive/review-2026-09.md`](archive/review-2026-09.md) منتقل شدند؛ موارد باز جدید
> **۳.۳۱–۳.۳۳** در ادامهٔ همین بخش ثبت شدند. ضمناً اسکریپت `test` پکیج
> (`node --test tests/`) که روی Windows/Node 22.9 با `MODULE_NOT_FOUND` می‌شکست
> به الگوی glob تغییر کرد تا دستور مستندشدهٔ `npm test` واقعاً پاس شود.
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): مرور پرفورمنسی `core-decision-dotnet` — موارد باز
> جدید **۳.۳۴–۳.۳۹** در ادامهٔ همین بخش ثبت شدند (موارد بحرانی همان مرور رفع
> و آرشیو شدند — رجوع کنید به یادداشت ابتدای بخش ۲).
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): مرور پرفورمنسی `ai-worker` — موارد باز جدید
> **۳.۴۰–۳.۴۴** در ادامهٔ همین بخش ثبت شدند (موارد بحرانی همان مرور رفع و
> آرشیو شدند — رجوع کنید به یادداشت ابتدای بخش ۲).
>
> ۱۴۰۵/۰۶/۳۱ (2026-09-22): مرور پرفورمنسی `assistant-extension` — موارد باز
> جدید **۳.۴۵–۳.۴۸** در ادامهٔ همین بخش ثبت شدند (موارد بحرانی همان مرور
> رفع و آرشیو شدند — رجوع کنید به یادداشت ابتدای بخش ۲).

### ۳.۵ تکرار منطق `Save` (BaseBusiness در برابر JobBusiness/TrendBusiness)
**فایل:** `BaseBusiness.cs` در برابر `JobBusiness.cs` — استخراج `id` یکسان
تکرار شده. DRY.

### ۳.۶ `ResumeContext` سریال‌سازی JSON سفارشی با Regex
**فایل:** `Analyze/Models/ResumeContext.cs` (`QouteSerializer`/`QouteDeserializer`)

حذف/افزودن کوتیشن کلیدها با Regex → فرمت غیراستاندارد که برای مقادیر حاوی `:` یا
کلیدهای غیرکلمه‌ای می‌شکند. پیشنهاد: JSON استاندارد.

### ۳.۱۰ وابستگی شکننده به ساختار HTML سایت‌ها

تمام scraper‌ها بر Regex/ساختار دقیق تکیه می‌کنند. هیچ سازوکار هشدار هنگام تغییر ساختار
وجود ندارد. پیشنهاد: لاگ/متریک تعداد شغل‌های استخراج‌شده و هشدار اگر صدا شد.

### ۳.۱۴ `job-seeker.sh` مسیر هاردکد

`cd "/home/rayan/Developing/Job Seeker/publish"` قابل‌حمل نیست.

### ۳.۱۵ `GetTextContent` فیلتر تگ ناقص
**فایل:** `JobEligibilityHelper.cs`

فقط والد مستقیم script/head/style چک می‌شود؛ `<noscript>`, `<svg>`, CSS/JS توکار
نشت می‌کنند. (مربوط به TODO.md: «Ignore javascript/json on content?»)

### ۳.۱۶ `Program.cs` APIهای منسوخ

`UseEndpoints` در NET 8 توصیه نمی‌شود؛ می‌توان مستقیم `MapControllers` زد.

### ۳.۱۷ توابع سراسری در view‌ها

`onclick="apply(...)"`, `ordering()`, `reset()` به توابع global در `wwwroot/scripts/*`
وابسته‌اند (Encapsulation ضعیف).

### ۳.۱۸ `Q_FETCH_FROM_COUNT` با `FetchFrom` ناسازگار

شمارش `State != Revaluation` را فیلتر نمی‌کند ولی خواندن می‌کند → ناهماهنگی در گزارش
پیشرفت Revaluation.

### ۳.۱۹ فایل‌های منتشرشده داخل ریپو

`jobs.photon-ai.ir/` و `jobs.photon-ai.ir.zip` (شامل database/runtimes/wwwroot) در فضای
کاری‌اند و باید از ریپو جدا شوند (deployment artifact).

### ۳.۲۰ پوشه `.github/workflows` خالی

CI/CD تعریف‌شده‌ای وجود ندارد.

### ۳.۲۱ طبقه‌بندی صفحه first-match-wins بدون اطمینان؛ Other→Search ترند جستجو را زنده نگه می‌دارد

**فایل:** `Agency.AnalyzeContent`، `TrendState.GetTrendType`

misroute بی‌صدا (صفحه‌ای که چند الگوی نقش را همزمان دارد، یا تغییر طراحی سایت) →
`OtherPages` بدون فرمان → خواب حلقه تا انقضای trend. جالب‌تر: `TrendState.Other`
به `TrendType.Search` مپ می‌شود، پس صفحات ناشناخته activity جستجو را هم تازه نگه
می‌دارند. مرتبط: ۳.۱۰.
**اقدام:** هشدار/متریک وقتی هیچ صفحه‌ای مچ نمی‌شود یا `OtherPages` برنده است؛
بازنگری مپ `Other` (نوع جدا، یا حداقل بدون بروزرسانی LastActivity جستجو).

### ۳.۲۲ امتیازدهی regex تمام‌محتوایی بدون مکان‌محوری

**فایل:** `JobEligibilityHelper` + `database/structure/job-option.sql`

برای `reject` یک mention در هرجای صفحه (فوتر، «استفاده نمی‌کنیم») کافی است؛ برای
`field` هم ذکر گذرا در tech-stack مچ می‌شود. پس از رفع ۳.۱۵ (پاک‌سازی محتوا)
منطقی‌تر هم می‌شود.
**اقدام:** محدودسازی تطبیق به ناحیهٔ شرح شغل یا وزن‌دهی ناحیه‌ای؛ حداقل گزارش محل
مچ برای بازبینی قواعد.

### ۳.۲۳ پارس حقوقی locale-فرضکن و غلبهٔ score حقوق بر رتبه

**فایل:** `JobEligibilityHelper.EvaluateSalaryScore`

Stepstone آلمانی است؛ فرمت `50.000,00` با فرض آمریکایی غلط parse می‌شود → score
حقوقی غلط. و چون امتیاز نهایی `(salary/1000)×Score` است، یک آگهی پردرآمد بر
آستانه و رتبهٔ نهایی غالب می‌شود فارغ از تطابق فیلد.
**اقدام:** نرمال‌سازی ارقام/اعشار بر اساس آژانس + سقف‌گذاری سهم حقوق در امتیاز کل.

### ۳.۲۴ دروازهٔ زبان با degrade بی‌صدا

**فایل:** `Dictionaries.cs` / `appsettings.json`

غیبت `dictionaries.sqlite3` (یک سطح بالاتر، خارج از `installation.sh`) زبان‌سنجی
را بی‌صدا خراب می‌کند (AGENTS.md هم هشدار داده، ولی در زمان اجرا هیچ سیگنالی نیست).
**اقدام:** health-check در startup + بنر هشدار در داشبورد وقتی دیکشنری در دسترس نیست.

### ۳.۲۵ نبود backoff/jitter و سقف نرخ

**فایل:** حلقهٔ `Command.Recheck` + الگوی خودترمیمیِ بستن‌تب/لاگین‌مجدد

حلقهٔ recheck می‌تواند بی‌مکث تند شود (فقط `Waiting` ثابت per-agency وجود دارد)؛
و الگوی «بستن تب → شروع دوباره از لاگین» دقیقاً الگوی anti-bot سایت‌ها را تحریک می‌کند.
**اقدام:** تأخیر تصادفی کوتاه بین recheckها + سقف نرخ per-agency (فاصلهٔ حداقلی
بین دو `Take` یک آژانس).

### ۳.۲۶ نقض تصمیم استقلال پیشخان از حلقهٔ جستجو

**فایل:** `Controllers/Decision.cs` (Running/Reset)، `Controllers/Job.cs` (Setting)، `agent-extension/controllers/check-page.js`

تصمیم کاربر: کل فرایند جستجو (افزونهٔ جستجوگر + هستهٔ تصمیم) باید کاملاً از
پیشخان جدا و مستقل باشد؛ اجرا نشده و در هر دو جهت نقض شده است:

۱. **پیشخان به وضعیت زندهٔ حلقه می‌نویسد:** `POST /decision/running` تنها از
   `wwwroot/scripts/server-operations.js` صدا می‌خورَد و مستقیم روی شیء سینگلتون
   آژانس جهش می‌کند (`CurrentMethodIndex`، بیت `ActiveSeeking`، `ClearSearching`،
   `SaveState`)؛ `POST /decision/reset` روندها را می‌کشد و با `ClearAgencies`
   شیءهای زنده را زیر تحلیل‌های در جریان تعویض می‌کند؛ `POST /job/setting` با
   `reload` همان‌جا سینگلتون‌ها را بازبارگذاری می‌کند و کنسول SQL همان endpoint
   نوشتن خام روی سطر تنظیمات را ممکن می‌کند.
۲. **حلقه به پیشخان وابسته است:** پمپ سفارش‌ها (`CheckNewOrders` — فعال‌ساز
   روندهای بی‌کار مثل «تب جستجو را باز کن») فقط روی تب پیشخان اجرا می‌شود
   (`check-page.js:25-52`، دکمهٔ `stop-start-ordering`، تایمر ۲۰ ثانیه)؛ بدون تب
   پیشخان جستجو می‌ایستد.

**اقدام:** اصل نویسندهٔ واحد (single-writer): مالکیت وضعیت آژانس فقط با حلقهٔ
تصمیم (take/orders/checkpoint)؛ نیت پیشخان از طریق DB در نقطهٔ امن اعمال شود
(چک‌پوینتِ تراکنشیِ موجود جای طبیعی است؛ جهت لانه‌شدن قفل‌ها:
`checkpoint_lock` → قفل آژانس)؛ انتقال پمپ سفارش‌ها به service worker
پس‌زمینهٔ افزونه (`chrome.alarms` به‌دلیل خواب MV3، هم‌نوا با ۲.۱۷). پس از
استقرار، پوشش قفلی ۲.۲۰ برای endpoint `Running` حذف‌شدنی است.

> ۱۴۰۵/۰۶/۲۱ (2026-09-12): با سوئیت تست JS اکستنشن (`agent-extension/tests/`)
> موارد جدید **۳.۲۷** و **۳.۲۸** ثبت شدند.

### ۳.۲۷ هشدار مردهٔ «Not found» در OnFill/OnClick
**فایل:** `agent-extension/controllers/action-handler.js`

`document.querySelectorAll` هرگز null برنمی‌گرداند ( NodeList خالی)، پس
`if (!elements) console.warn("Not found", object)` هرگز اجرا نمی‌شود و سلکتور
اشتباه بی‌صدا نادیده گرفته می‌شود. تأییدشده با `tests/action-handler.test.js`
(«fill/click on an empty selector ... never warns»).
**اقدام:** شرط را به `if (!elements.length)` تغییر دهید.

### ۳.۲۸ کش استاتیک SERVER_URL/API_KEY در Service Worker
**فایل:** `agent-extension/controllers/core-messaging.js` (`CheckServerUrl`/`CheckApiKey`)

آدرس سرور و کلید API یک‌بار خوانده و در فیلد استاتیک کش می‌شوند؛ تغییر آنها از
popup تا ری‌استارت بعدی Service Worker بی‌اثر می‌ماند. MV3 SW ها مکرر ری‌استارت
می‌شوند ولی در بازهٔ عمرشان با تنظیمات کهنه کار می‌کنند. تأییدشده با
`tests/core-messaging.test.js` («caches the URL after the first storage read»).
**اقدام:** TTL کوتاه برای کش یا پاکسازی با `chrome.storage.onChanged`.

> ۱۴۰۵/۰۶/۳۱ (2026-09-22): موارد ۳.۲۹–۳.۳۰ (fetch تکراری scopes و آلارم
> همیشه‌فعال orders) رفع و آرشیو شدند — رجوع کنید به یادداشت ابتدای همین بخش.

### ۳.۳۱ تأخیرهای ثابت در حلقهٔ scrape
**فایل:** `agent-extension/controllers/check-page.js` (`setTimeout(..., 1000)` در `OnPageLoad`)،
`agent-extension/controllers/action-handler.js` (`OnWait({ miliseconds: 300 })` بعد از fill/click)

هر لود صفحه ۱ ثانیه تأخیر خالص می‌گیرد (پیش از هر بررسی‌ای) و بعد از هر fill/click
مهم نیست عمل چقدر سریع بوده ۳۰۰ms صبر می‌شود؛ `recheck` هم کل چرخه را با تأخیر
۱ ثانیه‌ای تکرار می‌کند. در مقیاس حلقهٔ scrape (صدها لود صفحه) دقیقه‌ها تأخیر
تجمعی صرفاً از این دو ثابت حاصل می‌شود. تأخیر ۱s به‌ظاهر برای جاافتادن SPA است
ولی برای همهٔ صفحات حتی استاتیک پرداخت می‌شود.
**اقدام:** نگه‌داشتن تأخیر فقط برای حالت challenge/SPA؛ ارسال صفحات عادی بلافاصله
بعد از `load`؛ رویدادمحور کردن انتظار بعد از fill/click (MutationObserver/event
به‌جای timer ثابت). نیازمند سنجش خطر anti-bot است — هم‌خانوادهٔ ۳.۲۵.

### ۳.۳۲ حجم payload صفحه: `outerHTML` کامل + ۳–۴ کپی رشته
**فایل:** `agent-extension/controllers/check-page.js` (`content: document.documentElement.outerHTML`)
تا `agent-extension/controllers/core-messaging.js` (`JSON.stringify(params)`)

صفحه‌های LinkedIn/Indeed به‌راحتی ۱–۳MB HTML دارند و در مسیر ارسال چند بار
کپی می‌شود: serialize `outerHTML` (مسدودکنندهٔ main thread صفحهٔ سایت — خطر jank
و تشخیص bot)، structured clone به SW، `JSON.stringify` و serialization بدنهٔ
fetch. هر کپی CPU واقعی مصرف می‌کند.
**اقدام:** اگر سرور فقط body را scrape می‌کند ارسال `document.body.outerHTML`؛
گزینهٔ بعدی فشرده‌سازی با `CompressionStream('gzip')` در SW + پشتیبانی سمت سرور.
مرز سمت سرور همان `[RequestSizeLimit(5_000_000)]` (مورد آرشیوشدهٔ ۱.۷) است.

### ۳.۳۳ ریزمصرف‌های مسیر داغ content script
**فایل:** `agent-extension/controllers/check-page.js`، `core-messaging.js`، `background-messaging.js`

- `new RegExp(scopes[s].domain, 'i')` داخل حلقهٔ تطبیق در هر لود صفحه کامپایل
  می‌شود → regex های کامپایل‌شده همراه کش scopes نگه داشته شوند.
- لاگ‌های سنگین روی مسیر داغ: `CoreMessaging.Send` کل response و `SendingPageInfo`
  کل scope را لاگ می‌کنند؛ هر ۵ اسکریپت هم در include-time روی همهٔ سایت‌ها
  `console.log` می‌زنند → پشت فلگ DEBUG برود.
- پارامتر مردهٔ `reset` در `BackgroundMessaging.Scopes` که به `CoreMessaging.Scopes`
  نمی‌رسد (پاک‌سازی شود یا به invalidation کش وصل شود).
- heartbeat هر ۳۰s به‌ازای هر تبِ match (برای زنده‌نگه‌داشتن SW طراحی‌شده؛
  با تعداد تب زیاد بازبینی شود).

### ۳.۳۴ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) کوئری رتبه‌بندی داشبورد سنگین است
**فایل:** `Database/Business/JobBusiness.Sql.cs` (`Q_INDEX`)

دو لایهٔ تودرتو از `ROW_NUMBER` روی **کل** جدول Job در هر بارگذاری صفحهٔ jobs
داشبورد محاسبه می‌شود + <code dir="ltr">Log LIKE '%) Relocation**%'</code>
غیرقابل ایندکس برای هر ردیف + `JulianDay(latest) - JulianDay(job.RegTime)`
سطر به سطر. با رشد جدول هزینهٔ این کوئری خطی بدتر می‌شود (خودِ CTE ستون‌های
`Html`/`Content` را نمی‌خواند، پس بار اصلی CPU/sort است نه I/O متن).
**اقدام:** ستون‌های precomputed برای EffectiveScore و flag های Relocation/Remote
که هنگام write (همان `UpdateEvaluation`) پر شوند؛ یا محدودکردن CTE به subset
پیش از window function (فیلتر State/date پیش از رتبه‌بندی). هماهنگ با آینهٔ
`JobRanking.SqlRankScore` نگه داشته شود.

### ۳.۳۵ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) تشخیص زبان با رفت‌وبرگشت DB به‌ازای هر شغل
**فایل:** `Analyze/JobEligibilityHelper.cs` (`LanguageIsMatch`)، `Database/Dictionaries.cs`

واژه‌های محتوا با `OrderBy` (برای مجموعه بی‌نیاز از ترتیب) استخراج، به
دسته‌های ۱۰۰تایی شکسته و برای هر دسته یک `IN ('a','b',…)` با literal می‌سازد —
چند کوئری per job و متن SQL متفاوت هر بار (بی‌اثر با statement cache).
دیکشنری <code dir="ltr">en_US</code> چند صد هزار واژه است و به‌راحتی در RAM جا
می‌شود.
**اقدام:** یک <code dir="ltr">HashSet&lt;string&gt;</code> سینگلتون از دیکشنری در
startup (پیش‌نیاز: health-check مورد ۳.۲۴ تا غیبت فایل بی‌صدا نماند)؛ حذف
`OrderBy` و مجموعه‌های میانی LINQ (چانک کردن فقط برای کوئری بود، با HashSet
کل آن حذف می‌شود). این هم ضربهٔ CPU و هم رفت‌وبرگشت DB را از مسیر ارزیابی هر
شغل برمی‌دارد.

### ۳.۳۶ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) Regex های interpreted روی مسیر HTML های بزرگ
**فایل:** `Analyze/LinkedIn/LinkedInPage.cs` و همتاهایش در هر پوشهٔ پلتفرم،
`Database/Business/JobOptionBusiness.cs`، `Analyze/JobEligibilityHelper.cs`

هیچ‌یک از الگوها با `RegexOptions.Compiled` ساخته نشده‌اند (و static ها از
<code dir="ltr">[GeneratedRegex]</code> که در NET 8 در دسترس است استفاده
نمی‌کنند) در حالی که روی HTML های چند‌مگابایتی و متن کامل آگهی‌ها اجرا
می‌شوند — در هر بار تحلیل صفحه.
**اقدام:** مهاجرت الگوهای static صفحات به <code dir="ltr">[GeneratedRegex]</code>
(نیازمند partial type؛ فیلد static با متد تولیدشده جایگزین شود) و `Compiled`
برای الگوهای خوانده‌شده از DB در `JobOptionBusiness.FetchAll`. سنجش قبل/بعد
با یک snapshot واقعی (همان fixture های `LinkedInMarkupTests`).

### ۳.۳۷ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) `agency_lock` در طول کل تحلیل HTML نگه داشته می‌شود
**فایل:** `Analyze/Agency.cs` (`AnalyzeContent`)

قفل per-agency کل حلقهٔ `page.IssueCommand` را پوشش می‌دهد — یعنی تطبیق regex
روی HTML کامل، پارس HtmlAgilityPack و نوشتن DB همه زیر قفل؛ دو تب همزمانِ یک
آژانس پشت هم صف می‌شوند. قفل برای جهش وضعیت (`CurrentMethodIndex`/`Status`) و
پیشروی متد جستجو لازم است (مورد ۲.۲۰ آرشیوشده)، نه برای کل تحلیل.
**اقدام:** کوچک‌کردن ناحیهٔ بحرانی به mutation وضعیت و تصمیم پیشروی متد؛
تحلیل صفحه بیرون قفل. هم‌راستا با بازآرایی تک‌نویسندهٔ مورد ۳.۲۶ انجام شود
(جهت لانه‌شدن قفل‌ها: `checkpoint_lock` → قفل آژانس).

### ۳.۳۸ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) console sink با سطح Debug در production
**فایل:** `Program.cs` (ساخت LoggerConfiguration)

سطح فایل به environment وابسته است ولی `WriteTo.Console` همیشه `Debug` است —
در production همهٔ رخدادهای Debug فرمت و نوشته می‌شوند (I/O بی‌دلیل روی
مسیر داغ؛ هر `Take` چند رخداد Debug دارد).
**اقدام:** سطح کنسول همان `file_event_level` شود (یا در Production حذف کامل
console sink).

### ۳.۳۹ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) ریزمصرف‌های پایدار سمت سرور

- `PRAGMA synchronous=NORMAL` در کنار WAL: پیش‌فرض FULL است؛ برای این workload
  (نوشتار زیاد، تحمل از‌دست‌رفتن آخرین لحظه‌ها) throughput نوشتن را محسوس
  بالا می‌برد — یک خط در `DatabaseFactory` پس از WAL.
- `Analyzer.FindAgency` با `FirstOrDefault` خطی روی مقادیر دیکشنری جستجو
  می‌کند در حالی که `agencies_by_name` برای همین هست (فقط آژانس‌های غیرفعال
  باید از `by_id` جستجو شوند تا endpoint های داشبورد آنها را پیدا کنند).
- `Q_FETCH_ID` و `Fetch` با <code dir="ltr">SELECT *</code> در مسیرهایی که متن کامل `Html`/`Content`
  لازم نیست (بخش عمدهٔ آن در ۲.۲۶ آرشیوشده رفع شد؛ بازبینی موارد باقی‌مانده
  مثل `JobController.Get` که به کل ردیف نیاز دارد واقعاً دارد، پس فقط موارد
  Log-only هدف باشند).
- payload سمت سرور: <code dir="ltr">[RequestSizeLimit(20_000_000)]</code>
  روی `Take` — قرینهٔ مورد ۳.۳۲ سمت اکستنشن؛ رشتهٔ JSON پس از deserialize
  دوبرابر (UTF-16) و روی LOH می‌نشیند. اگر فشار memory دیدیم، همان راهکار
  ۳.۳۲ (body-only/فشرده‌سازی) بار سرور را هم کم می‌کند.

### ۳.۴۰ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) بهره‌گیری صریح از prefix caching در `ai-worker`

System prompt هر دو call در سراسر یک run بایت‌به‌بایت ثابت است (تست
`SystemPromptIsStableAcrossJobs` پین کرده) و بخش ثابت **قبل از** JD متغیر
می‌آید — شکل درست برای KV-prefix reuse در llama-server که prefill چند-KB-توکنی
را برای هر job تقریباً رایگان می‌کند؛ ولی قطعات ثابت per-run کش نمی‌شوند:
`rubricTemplate.Replace` و serialize ردیف‌های `MemoryBlock` به‌ازای هر job
تکرار می‌شود و `Estimate(system)` دو بار محاسبه می‌شود (نتایج الان یکسان‌اند،
پس فقط CPU جزئی).
**اقدام:** کش per-run قطعات ثابت؛ فعال‌سازی/سنجش cache-reuse اسلات llama-server
و اندازه‌گیری اثرش روی زمان prefill؛ قاعدهٔ صریح که هیچ فیلد per-job (مثل
job id) به system prompt اضافه نشود — چنین چیزی reuse را کامل می‌کشد.

### ۳.۴۱ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) console sink با سطح Debug در production در `ai-worker`

قرینهٔ ۳.۳۸ برای worker: سطح فایل تابع environment است ولی
<code dir="ltr">WriteTo.Console(LogEventLevel.Debug)</code> همیشه Debug است و
`LogPrompt` کل بدنهٔ system/user prompt (تا ~۱۶k کاراکتر × ۲ به‌ازای هر call،
دو call در هر job) را در Debug لاگ می‌کند → در production تمام آن به stdout
می‌رود.
**اقدام:** سطح کنسول مثل `FileLevel` تابع environment شود (یا در Production
حذف کامل).

### ۳.۴۲ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) parse مجدد `response_format` در هر call

<code dir="ltr">JsonDocument.Parse(responseFormat).RootElement.Clone()</code>
به‌ازای هر call LLM روی دو ثابت رشته‌ای اجرا می‌شود؛ `JsonDocument` اصلی هم
dispose نمی‌شود (حافظهٔ pooled دیر برمی‌گردد). هزینهٔ CPU جزئی ولی رایگان
رفع‌شدنی.
**اقدام:** کش static (دو کلید شناخته‌شده)؛ بهبود بعدی: ساخت بدنهٔ درخواست با
`JsonNode` به‌جای `Dictionary&lt;string, object?&gt;` + serialize دومرحله‌ای.

### ۳.۴۳ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) ریزمصرف‌های `ai-worker`

- `RunStats.CallMs` جمع می‌شود ولی هرگز در Summary گزارش نمی‌شود (متریک مرده
  — یا به Summary اضافه شود یا حذف).
- تخصیص‌های LOH (رشته‌های ~64KB ای prompt به‌ازای هر job) — در نرخ فعلی
  (~۱ job در چند ده ثانیه) ناچیز؛ فقط اگر روزی pipelining/هم‌پوشانی اضافه شد
  بازبینی شود.

### ۳.۴۴ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) نکات سنجش `ai-worker`

- آمار tok/s در `LogCall` زیر json-schema constrained decode معیار خوانایی
  ندارد (grammar در llama.cpp سرعت decode را می‌کاهد) — اعداد را با احتیاط
  مقایسه کنید.
- worker مقدار واقعی <code dir="ltr">n_ctx</code> اسلات خود را نمی‌داند (با
  <code dir="ltr">--parallel 2</code> نصف `-c` کل است): در startup یک‌بار query/لاگ شود و
  <code dir="ltr">usage.prompt_tokens</code>ی که همین حالا پارس می‌شود به‌عنوان
  فیدبک تطبیقی بودجه (مکمل fix آرشیوشدهٔ ۲.۳۱) به کار رود تا سرریز واقعی
  از برآورد chars/4 قابل تشخیص باشد.

### ۳.۴۵ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) `OwnLabel` با querySelector سراسری به‌ازای هر فیلد
**فایل:** `assistant-extension/controllers/form-inventory.js` (`OwnLabel`)

وقتی <code dir="ltr">element.labels</code> خالی باشد و فیلد `id` داشته باشد، برای هر فیلد
یک <code dir="ltr">document.querySelector('label[for="…"]')</code> روی کل سند اجرا می‌شود؛
فرم با صدها فیلدِ بدون label association (در سایت‌های اپلای رایج) یعنی صدها
جستجوی کامل DOM در یک `Extract`.
**اقدام:** یک پاس <code dir="ltr">document.querySelectorAll("label[for]")</code> در ابتدای
`Extract` و ساخت Map از id به label؛ `OwnLabel` فقط از Map بخواند.

### ۳.۴۶ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) بدون retry در `LlmClient.Chat`
**فایل:** `assistant-extension/controllers/llm-client.js` (`Chat`)

یک خطای گذرا (اتصال لحظه‌ای به llama-server) کل `FillLoop.Run` را با error
می‌کشد؛ fillهای قبلاً اعمال‌شده در صفحه می‌مانند ولی حلقه abort می‌شود و
کاربر از نو شروع می‌کند.
**اقدام:** retry ساده با backoff کوتاه فقط برای `llm-unreachable`/`5xx` (نه
`llm-timeout` — انتظار دوبارهٔ ۱۸۰ ثانیه‌ای بی‌معناست).

### ۳.۴۷ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) حلقه‌های سریال await در `ApplyAcceptedDrafts`/`FlushDiffs`
**فایل:** `assistant-extension/controllers/background.js`

هر draft پذیره‌شده یک `TabSend` سریال و هر diff یک POST جدای سریال است. در
مقیاس فعلی (≤۵ draft، diffهای کم) مشکلی نیست؛ فقط اگر تعداد رشد کرد:
`Promise.all` روی فیلدهای متمایز (drafts) و/یا endpoint batch سمت سرور
(diffs).

### ۳.۴۸ (مرور پرفورمنسی ۱۴۰۵/۰۶/۳۱) نکات خفیف `assistant-extension`

- لاگ‌های <code dir="ltr">console.log("ASSISTANT", …)</code> در هر لود صفحهٔ content script —
  در production حذف یا شرطی شوند.
- `FillHandler.ApplyRadio`/`Current` هر بار <code dir="ltr">querySelectorAll</code> تازه برای radio
  group می‌زنند — قابل کش از `FormInventory.Registry` (المان اول گروه همان‌جاست).
- `RenderMemory` با ۵۰۰+ ردیف کل DOM را rebuild می‌کند (~۲۰۰۰ نود) — در صورت
  بزرگ‌شدن جدول، صفحه‌بندی یا رندر تدریجی.
- `StorageHandler.Set` بدون چک callback است — ریسک از‌دست‌رفتن write هنگام
  بسته‌شدن سریع popup (بیشتر درست‌کاری تا پرفورمنس).

---

## بخش ۴ — 🔵 خفیف (سبک و املا)

غلط‌های املایی که در کل پایه‌کد/اسکیما پخش‌اند (هنگام بازنویسی اصلاح شوند):

| فعلی | درست |
|------|------|
| `Efective` | **Effective** (ستون JobOption، در SQL و کوئری) |
| `reumse` | **resume** (دسته‌بندی در `job-option.sql`) |
| `mounths` | **months** |
| `spliter` | **splitter** |
| `miliseconds` | **milliseconds** |
| `SimlpeSerialize`/`SimlpeDeserialize` | **Simple…** |
| `SerializeChecktSyntaxt`/`DeserializeChecktSyntaxt` | **CheckedSyntax** |
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
- استفاده از `DateTime.Now` (زمان محلی) در `Trend.LastActivity`/`DeleteExpired` — در تغییر DST می‌تواند TTL را جابه‌جا کند؛ UTC بهتر است.
- `ResumeContext.Version => 42` عدد جادویی بدون مهاجرت.
- نبود `.editorconfig`؛ ترکیب tab/space و استایل نامنظم.
- در `agent-extension` (کشف با سوئیت تست، ۱۴۰۵/۰۶/۲۱ — 2026-09-12): `event.keyCode` منسوخ در
  `application/menu.js` (به‌جای آن `event.key === 'Enter'`) و غلط املایی `Unkown action` در
  لاگ `controllers/action-handler.js` (`Execute`).

---

## بخش ۵ — 💡 پیشنهادات بهبود

1. **معماری:** لایه‌بندی Clean (Controllers → Services → Repositories) با interface‌ها و تزریق وابستگی کامل.
2. **دسترسی داده:** ~~مهاجرت به **Dapper** یا EF Core~~ (Dapper انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۳.۱ آرشیو شد).
3. **امنیت:** فعال‌سازی HSTS برای تحکیم HTTPS (گذر plaintext اعتبارنامه با HTTPS روی سرور رفع شد — مورد ۱.۴ آرشیو شد).
4. **همزمانی SQLite:** ~~اتصال از طریق DI (Scoped)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۳.۳ آرشیو شد) — WAL و BusyTimeout انجام شد.
5. **پایداری scraper:** متریک شمارش شغل + هشدار هنگام افت + تست snapshot برای هر آژانس.
6. **تست:** پروژه xUnit با پوشش برای امتیازدهی/حقوق/زبان/مرتب‌سازی.
7. **مشاهده‌پذیری:** داشبورد وضعیت آژانس‌ها، هشدار خطا (Serilog + Seq/Email).
8. **پیکربندی:** رفع مسیرهای هاردکد (`job-seeker.sh`).
9. **CI/CD:** اضافه کردن workflow برای build/test در `.github/workflows`.
10. **مستندسازی:** توضیح الگوریتم امتیازدهی (فرمول LaTeX در `JobBusiness.cs`) و حالت‌های ماشین Trend در یک سند (بخشی از آن در `TrendsCheckpoint.md` هست ولی ناقص).
11. **TODO.md** موارد باز: افزودن Qatar/Oman (بخشی در SQL هست)، bayt/qatarliving/omanjobs، popup هنگام باز کردن صفحه (ریشه: مورد ۲.۲۲)، حذف ستون HTML، نادیده‌گرفتن JS/JSON در محتوا.
12. **پروتکل پایدار تب↔trend:** ~~binding ماندگار در اکستنشن (`chrome.storage.session` + `onRemoved`)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۱.۷ آرشیو شد) + ~~heartbeat و TTL آگاه از حالت (۲.۱۷)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۲.۱۷ آرشیو شد) + ~~lease مهلت‌دار رزروها (۲.۱۸)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۲.۱۸ آرشیو شد) و ~~اجرای open از SW با `chrome.tabs.create` (۲.۲۲)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۲.۲۲ آرشیو شد — close هم از SW با `chrome.tabs.remove` اجرا می‌شود).
13. **Idempotency در پروتکل درایو:** request-id/sequence در `PageContext` و پاسخ‌ها برای تشخیص درخواست تکراری/stale — ریشهٔ ۲.۱۹ با تراکنش اتمیک رفع شد (۱۴۰۵/۰۶/۲۰)؛ این مورد به‌عنوان لایهٔ دفاعی دوم باز می‌ماند.

</div>
