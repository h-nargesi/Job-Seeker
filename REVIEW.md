# گزارش مرور پروژه Job-Seeker — موارد باز

> موارد رفع‌شده‌ای که با کد تطبیق داده شدند به [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md)
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
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شدند — ۱.۷ با کامیت `d2861ff` (binding
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
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد — تأیید نهایی با نخستین اجرای زنده.
>
> موارد ۲.۱۷ (TTL بدون heartbeat)، ۲.۱۸ (رزرو بدون lease) و ۲.۲۲ (اجرای open با
> `window.open`) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با کامیت `8503003` رفع و به
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شدند — تأیید نهایی با نخستین اجرای زنده.
> موارد ۲.۱۹ (اتمیزم Orders×Take با تراکنش `BEGIN IMMEDIATE` + قفل ایستا دور کل پاس
> چک‌پوینت) و ۲.۲۱ (مرتب‌سازی/سقف عددی با ستون `Attempts` + مهاجرت backfill در
> `installation.sh`) در ۱۴۰۵/۰۶/۲۰ (2026-09-11) رفع و به
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شدند — تأیید نهایی با نخستین اجرای زنده.
> مورد ۲.۲۰ (قفل per-agency روی `AnalyzeContent`) در ۱۴۰۵/۰۶/۲۰
> (2026-09-11) رفع و به [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد —
> تأیید نهایی با نخستین اجرای زنده. مورد باز این بخش باقی نمانده است.

---

## بخش ۳ — 🟡 متوسط (طراحی و قابلیت نگهداری)

> مورد ۳.۱ (ORM بازتابی شکننده) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با مهاجرت کامل لایهٔ داده به Dapper رفع و به
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد — به همراه فیکس باگ upsert ترند؛ رفتارهای حفظ‌شده
> (ترکیب UTC/local مهلت `ModifiedOn` و `Tries = NULL` در اسکرپ Stepstone) در همان مدخل آرشیو مستند شد.
>
> مورد ۳.۲ (استفادهٔ بیش از حد `dynamic` و anonymous types) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با مدل‌ها/record های
> تایپ‌دار (`JobOptionSettings`، `AgencyRate`، `TrendReportItem`، `JobListItem`، `AgencyInfo`،
> `AgencyDashboardItem`، `DashboardViewModel`) و view های `@model`دار رفع و به
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد — به همراه حذف کد مرده (`LoadSetting` و overload
> داینامیک `Query`)؛ گزینهٔ حقوقی بدشکل اکنون هشدار + امتیاز ۰ می‌دهد به‌جای کرش binder.
>
> مورد ۳.۳ (`Database.Open()` دستی همه‌جا — نقض DI) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با حذف کامل
> `Open`/`SetConfiguration` استاتیک و جایگزینی با `IDatabaseFactory` (Singleton) + `Database` اسکوپ‌شده
> در DI رفع و به [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد — به همراه فیکس اتصال مرده در
> `Page.GetUserPass`؛ نیمهٔ باز ماندهٔ مورد ۲.۸ آرشیوشده نیز بسته شد.

### ۳.۴ تکرار کد `LoadJob` در Stepstone
**فایل:** `Analyze/Stepstone/StepstonePageJob.cs`

به‌جای ارث‌بری از `JobPage`، تمام `IssueCommand`/`LoadJob` بازنویسی شده. الگوی پایه را
بی‌اثر می‌کند. Stepstone باید از `JobPage` مشتق شود و `StepstonePage` از `PageBase`.

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
نشت می‌کنند. (مربوط به todo.txt: «Ignore javascript/json on content?»)

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
11. **todo.txt** موارد باز: افزودن Qatar/Oman (بخشی در SQL هست)، bayt/qatarliving/omanjobs، popup هنگام باز کردن صفحه (ریشه: مورد ۲.۲۲)، حذف ستون HTML، نادیده‌گرفتن JS/JSON در محتوا.
12. **پروتکل پایدار تب↔trend:** ~~binding ماندگار در اکستنشن (`chrome.storage.session` + `onRemoved`)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۱.۷ آرشیو شد) + ~~heartbeat و TTL آگاه از حالت (۲.۱۷)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۲.۱۷ آرشیو شد) + ~~lease مهلت‌دار رزروها (۲.۱۸)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۲.۱۸ آرشیو شد) و ~~اجرای open از SW با `chrome.tabs.create` (۲.۲۲)~~ (انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۲.۲۲ آرشیو شد — close هم از SW با `chrome.tabs.remove` اجرا می‌شود).
13. **Idempotency در پروتکل درایو:** request-id/sequence در `PageContext` و پاسخ‌ها برای تشخیص درخواست تکراری/stale — ریشهٔ ۲.۱۹ با تراکنش اتمیک رفع شد (۱۴۰۵/۰۶/۲۰)؛ این مورد به‌عنوان لایهٔ دفاعی دوم باز می‌ماند.
