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

آخرین مورد باز قبلی (۱.۴ — گذر plaintext از HTTP هنگام fill فرم لاگین) در ۱۴۰۵/۰۶/۱۳
(2026-09-04) با فعال‌سازی HTTPS روی سرور رفع و به
[`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد.

> ۱۴۰۵/۰۶/۱۸ (2026-09-09): پس از مرور کامل ارتباط سرور↔اکستنشن و الگوریتم تصمیم،
> موارد جدید **۱.۵–۱.۷**، **۲.۱۷–۲.۲۲** و **۳.۲۱–۳.۲۵** ثبت شد. سیستم تک‌کاربره است؛
> بنابراین موارد همزمانی ثبت‌شده از جنس همزمانی *داخلی*‌اند (چند تب آژانس موازی +
> poller داشبورد)، نه چندکاربره — اولویت آنها به‌تناسب تنظیم شده ولی ریشه‌ای باقی‌اند.

### ۱.۵ مسیریابی پاسخ پس‌زمینه بر اساس `tab.index` به‌جای `tab.id`

**فایل:** `agent-extension/controllers/background.js` (تابع `Respond`)

پاسخ با `chrome.tabs.query({ windowId, index })` دنبال می‌شود در حالی که `index`
ناپایدار است؛ اگر بین درخواست و پاسخ تبی جابه‌جا شود یا تبِ کناری بسته شود، پاسخ به
**تب اشتباه** می‌رسد (تب بی‌گناه به URL شغل دیگری هدایت می‌شود) یا گم می‌شود و تب
مقصد تا انقضای trend بی‌فرمان می‌ماند. `sender.tab.id` پایدار است و همان لحظه موجود.
**اقدام:** حذف `query` و ارسال مستقیم `chrome.tabs.sendMessage(sender.tab.id, ...)`؛
در خطای «Receiving end does not exist» (ناوژی پیش از پاسخ) فقط لاگ — ناوبری جدید
خودش حلقه را ادامه می‌دهد.

### ۱.۶ زنجیرهٔ پیام‌رسانی بدون timeout/retry و بدون چک `response.ok` → توقف دائمیِ بی‌صدا

**فایل:** `core-messaging.js` (`Send`)، `background.js` (`Respond`)، `background-messaging.js` (`Message`)

`Send` برخلاف `Scopes`/`Orders` نه `response.ok` را چک می‌کند نه خطا را catch
می‌کند؛ `Respond` هم بدون try/catch است. اگر fetch شکست بخورد یا JSON نامعتبر باشد،
promise داخل content script **هرگز resolve نمی‌شود** و آن تب تا ناوبری بعدی از حلقه
خارج می‌ماند — بدون هیچ لاگ/هشداری. محرک‌ها: خاموشی سرور، 401 (کلید API نامعتبر)،
413 (`RequestSizeLimit(5_000_000)` در `Decision.cs` در برابر outerHTML سنگین
LinkedIn/Glassdoor)، پاسخ HTML صفحهٔ لاگین به‌جای JSON.
**اقدام:** چک `ok` + پارس امن + timeout (مثلاً 30s) + catch در هر دو سطح؛ بازگرداندن
ساختار خطا به content script و لاگ/شمارندهٔ خطا؛ تعیین تکلیف سقف 5MB (افزایش یا فشرده‌سازی).

### ۱.۷ هویت تب فقط در حافظهٔ سرویس‌ورکر MV3 → با ری‌استارت SW درخواست‌ها بی‌trend می‌روند و تبِ سالم Blocked می‌شود

**فایل:** `agent-extension/controllers/trend-collection.js`؛ سمت سرور: `TrendsCheckpoint.LoadAndUpdateCurrentTrend` (شرط adoption فقط برای Reserved)

MV3 سرویس‌ورکر را بعد از ~۳۰ ثانیه بی‌تحرکی terminate می‌کند؛ نگاشت
`windowId→tabId→trendId` از بین می‌رود و درخواست بعدی **بدون trend id** ارسال
می‌شود. سرور فقط trend های `Reserved` را بدون id قبول می‌کند؛ پس تبِ در جریان (که
trend آن قبلاً adopt شده) مسیر `Blocked`→`Close` می‌رود — یعنی ری‌استارت
مرورگر/SW تب‌های سالم را می‌بندد و جریان از لاگین شروع می‌شود.
**اقدام (ریشه):** binding پایدار — ماندگاری نقشه در `chrome.storage.session` و
بازگردانی هنگام بوت SW + شنود `chrome.tabs.onRemoved` برای پاک‌سازی مدخل تب بسته‌شده؛
بلندمدت: اتصال trend به آژانسِ hostname سمت سرور (تعمیم مسیر adoption رزرو).

---

## بخش ۲ — 🟠 مهم (باگ‌های منطقی و درستی)

> مورد ۲.۲ (تأیید فیکس `JobFallow` این‌دیس با DOM زنده) در ۱۴۰۵/۰۶/۱۳ (2026-09-04) حذف شد:
> fav کردن شغل روی سایت لازم نیست (کاتالوگ شغل‌ها در خود سیستم موجود است)؛ ارسال رزومه در
> صورت نیاز به‌صورت دستی انجام می‌شود. (کد `JobFallow` دست‌نخورده و فعال مانده است.)
>
> آخرین مورد باز بخش ۲ (۲.۱۶ — الگوی `/rc/clk?jk=` در استخراج شغل این‌دیس) در ۱۴۰۵/۰۶/۱۷
> (2026-09-08) با الگوی چندشکلی `IndeedSerp` رفع و به
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد — تأیید نهایی با نخستین اجرای زنده.

### ۲.۱۷ TTL دودقیقه‌ای trend + تایمر ۹۰ ثانیه‌ای بستن، بدون ضربان قلب

**فایل:** `TrendBusiness.cs` (`TREND_EXPIRATION_MINUTES = 2`)، `TrendsCleanupService.cs` (پاک‌سازی هر ۱ دقیقه)، `action-handler.js` (`SetCloseTimer`)

هر جریانی که بیش از ۲ دقیقه بدون page-load بماند (لاگین دومرحله‌ای، صفحهٔ کند،
`wait` بلند) سطر trend را از دست می‌دهد → درخواست بعدی با `had_no_trend && matched`
→ `Blocked` → بسته‌شدن عمدی تب و شروع دوباره از لاگین. تایمر ۹۰ ثانیه‌ای هم تبِ
باز‌شدهٔ اسکریپتی را وسط مکث طولانی می‌بندد؛ `window.close()` روی تب‌های باز‌شدهٔ
کاربر عملاً no-op است (تب زومبی با trend منقضی).
**اقدام:** تمایز «مشغول» از «رها‌شده» — heartbeat سبک از تب فعال (تازه‌کردن
LastActivity بدون تحلیل کامل) و اعمال TTL فقط در نبود ضربان (مثلاً ۵ دقیقه)؛
حداقل: هماهنگی wait های بلند با `CloseTimer`.

### ۲.۱۸ فلگ Reserved بدون مهلت → وقفهٔ دودقیقه‌ای پس از Openِ بلاک‌شده

**فایل:** `TrendsCheckpoint.CheckingSleptJobTrends` / `InjectOpenCommandForNewTrends`

اگر trend رزروشده هرگز adopt نشود (popup بلاک شد — ر.ک. ۲.۲۲ — یا تب فوراً بسته
شد)، همان سطر رزرومانده چون «وجود دارد» جلوی Open بعدی را می‌گیرد تا وقتی
`DeleteExpired` آن را بردارد (تا ۲+ دقیقه). فلگ دائمی است، نه lease مهلت‌دار.
**اقدام:** رزرو باید lease با مهلت باشد: `Reserved=true` + سن `LastActivity` > مثلاً
۳۰s → همان poll بازتخصیص/بازگشایی کند.

### ۲.۱۹ همزمانی داخلی Orders×Take: خواندن-تصمیم-نوشتن غیراتمیک → دو Open و تورم Tries

**فایل:** `TrendsCheckpoint.CheckCurrentTrends`، `JobBusiness.GetFirstJob`، `check-page.js` (هر تب داشبورد interval مستقل دارد)

با وجود تک‌کاربره بودن، poll داشبورد (هر ۲۰ ثانیه، به‌ازای **هر تب داشبورد باز**)
با `Take` تب‌های آژانس مسابقه می‌دهد: هر دو «trend وجود ندارد» می‌بینند → هر دو
`Open` صادر می‌کنند؛ `GetFirstJob` دو بار `Tries` را بالا می‌برد و چون سقف با
`Tries NOT LIKE '%4: %'` کنترل می‌شود، شغل‌ها **زودتر از موعد** از صف تحلیل خارج می‌شوند.
**اقدام:** یا تک‌نمونه‌سازی poll در خود اکستنشن (یک interval در SW با
`chrome.alarms`، نه به‌ازای تب داشبورد)، یا قفل اتمیک سمت سرور (تراکنش
`BEGIN IMMEDIATE` دور FetchAll→تخصیص→تزریق) و قرارگرفتن `GetFirstJob` در همان تراکنش.

### ۲.۲۰ وضعیت mutable سینگلتون Analyzer بدون همگام‌سازی

**فایل:** `Analyzer` (Singleton) + `Agency.AnalyzeContent` (پیشروی `CurrentMethodIndex`، خاموش‌کردن `ActiveSeeking`، `SaveState`)

چند request همزمان (تب‌های موازی آژانس‌ها + Orders) روی یک شیء Agency جهش می‌کنند؛
قفل موجود فقط دور `LoadSettings` است. نتیجه: پرش دو مرحله‌ای method جستجو یا
گم‌شدن flip خاموش‌کردن `ActiveSeeking` (last-writer-wins در `SaveState`).
**اقدام:** قفل per-agency (مثلاً `SemaphoreSlim(1,1)` به‌ازای آژانس) دور
`AnalyzeContent`؛ یا صف‌کردن تحلیل هر آژانس.

### ۲.۲۱ `Q_FETCH_FIRST`: مرتب‌سازی لغوی‌نگارشی `Tries` و `LIKE '%4: %'`

**فایل:** `JobBusiness.cs` (`Q_FETCH_FIRST`)

`ORDER BY ... Tries DESC` روی ستون TEXT با فرمت `"N: date"` — `'9: ' > '10: '` —
ترتیب انتخاب شغل بعدی برای tries≥10 نادرست است؛ و الگوی `'%4: %'` با `'14: '`
هم مچ می‌شود. با سقف فعلی 4 معمولاً پنهان می‌ماند، ولی در صورت تورم ۲.۱۹ زودتر
نمایان می‌شود.
**اقدام:** ستون عددی `Attempts` جداگانه (لاگ متنی `Tries` برای تاریخ حفظ شود) و
شرط سقف روی همان ستون.

### ۲.۲۲ اجرای `open` با `window.open` از content script → وابستگی به popup-blocker

**فایل:** `action-handler.js` (`OnOpen`)

کل جریان Orders (بازکردن صفحهٔ جستجو/شغل بعدی) به allow شدن popup برای همهٔ
دامنه‌ها و localhost وابسته است؛ در حالت پیش‌فرض Chrome بلاک می‌شود و `window.open`
null برمی‌گرداند (محرک اصلی ۲.۱۸). در todo.txt هم به‌صورت علامت «popup هنگام باز
کردن صفحه» ثبت شده — ریشه‌اش همین است.
**اقدام:** مسیریابی فرمان `open` به SW و اجرا با `chrome.tabs.create` (بدون
محدودیت popup-block؛ مجوز اضافه لازم ندارد) — با مالکیت تب در SW، اصلاح ۱.۷ هم
ساده‌تر می‌شود.

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
12. **پروتکل پایدار تب↔trend:** lease مهلت‌دار سمت سرور + binding ماندگار در اکستنشن (`chrome.storage.session` + `onRemoved` + اجرای open از SW با `chrome.tabs.create`) — ریشهٔ مشترک ۱.۷/۲.۱۸/۲.۲۲.
13. **Idempotency در پروتکل درایو:** request-id/sequence در `PageContext` و پاسخ‌ها برای تشخیص درخواست تکراری/stale — پیش‌نیاز بستن قطعی ۲.۱۹.
