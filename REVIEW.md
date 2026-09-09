# گزارش مرور پروژه Job-Seeker — موارد باز

> موارد رفع‌شده‌ای که با کد تطبیق داده شدند به [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md)
> منتقل شدند (بررسی: ۱۴۰۵/۰۶/۰۹ — 2026-08-31). این فایل فقط موارد باز/ناقص را دنبال می‌کند.
>
> رنگ‌بندی اولویت: 🔴 بحرانی · 🟠 مهم · 🟡 متوسط · 🔵 خفیف

---

## فهرست

- [بخش ۱ — بحرانی (همه رفع شد)](#بخش-۱--بحرانی-همه-رفع-شد)
- [بخش ۲ — مهم (باگ‌های منطقی و درستی)](#بخش-۲--مهم-باگهای-منطقی-و-درستی)
- [بخش ۳ — متوسط (طراحی و قابلیت نگهداری)](#بخش-۳--متوسط-طراحی-و-قابلیت-نگهداری)
- [بخش ۴ — خفیف (سبک و املا)](#بخش-۴--خفیف-سبک-و-املا)
- [بخش ۵ — پیشنهادات بهبود](#بخش-۵--پیشنهادات-بهبود)

---

## بخش ۱ — 🔴 بحرانی (همه رفع شد)

آخرین مورد باز (۱.۴ — گذر plaintext از HTTP هنگام fill فرم لاگین) در ۱۴۰۵/۰۶/۱۳
(2026-09-04) با فعال‌سازی HTTPS روی سرور رفع و به
[`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد.

---

## بخش ۲ — 🟠 مهم (باگ‌های منطقی و درستی)

> مورد ۲.۲ (تأیید فیکس `JobFallow` این‌دیس با DOM زنده) در ۱۴۰۵/۰۶/۱۳ (2026-09-04) حذف شد:
> fav کردن شغل روی سایت لازم نیست (کاتالوگ شغل‌ها در خود سیستم موجود است)؛ ارسال رزومه در
> صورت نیاز به‌صورت دستی انجام می‌شود. (کد `JobFallow` دست‌نخورده و فعال مانده است.)
>
> آخرین مورد باز بخش ۲ (۲.۱۶ — الگوی `/rc/clk?jk=` در استخراج شغل این‌دیس) در ۱۴۰۵/۰۶/۱۷
> (2026-09-08) با الگوی چندشکلی `IndeedSerp` رفع و به
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد — تأیید نهایی با نخستین اجرای زنده.

---

## بخش ۳ — 🟡 متوسط (طراحی و قابلیت نگهداری)

> مورد ۳.۱ (ORM بازتابی شکننده) در ۱۴۰۵/۰۶/۱۸ (2026-09-09) با مهاجرت کامل لایهٔ داده به Dapper رفع و به
> [`REVIEW-ARCHIVE.md`](REVIEW-ARCHIVE.md) منتقل شد — به همراه فیکس باگ upsert ترند؛ رفتارهای حفظ‌شده
> (ترکیب UTC/local مهلت `ModifiedOn` و `Tries = NULL` در اسکرپ Stepstone) در همان مدخل آرشیو مستند شد.

### ۳.۲ استفادهٔ بیش از حد `dynamic` و anonymous types

در سراسر لایه‌ها (`List<dynamic>`, `JobOption.Settings` dynamic, خروجی
`AgencyBusiness.LoadByName`). نوع‌ها گم می‌شوند، Intellisense/کامپایل ضعیف.

### ۳.۳ `Database.Open()` دستی همه‌جا — نقض DI

به‌جای تزریق، اتصال استاتیک/دستی باز می‌شود. تست‌پذیری سخت.
پیشنهاد: `IDatabaseFactory` به‌صورت Scoped در DI.
(این مورد نیمهٔ دوم مورد ۲.۸ آرشیوشده نیز هست.)

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
- `ResumeContext.Version => 42` عدد جادویی بدون مهاجرت.
- نبود `.editorconfig`؛ ترکیب tab/space و استایل نامنظم.

---

## بخش ۵ — 💡 پیشنهادات بهبود

1. **معماری:** لایه‌بندی Clean (Controllers → Services → Repositories) با interface‌ها و تزریق وابستگی کامل.
2. **دسترسی داده:** ~~مهاجرت به **Dapper** یا EF Core~~ (Dapper انجام شد — ۱۴۰۵/۰۶/۱۸؛ مورد ۳.۱ آرشیو شد).
3. **امنیت:** فعال‌سازی HSTS برای تحکیم HTTPS (گذر plaintext اعتبارنامه با HTTPS روی سرور رفع شد — مورد ۱.۴ آرشیو شد).
4. **همزمانی SQLite:** اتصال از طریق DI (Scoped) — WAL و BusyTimeout انجام شد.
5. **پایداری scraper:** متریک شمارش شغل + هشدار هنگام افت + تست snapshot برای هر آژانس.
6. **تست:** پروژه xUnit با پوشش برای امتیازدهی/حقوق/زبان/مرتب‌سازی.
7. **مشاهده‌پذیری:** داشبورد وضعیت آژانس‌ها، هشدار خطا (Serilog + Seq/Email).
8. **پیکربندی:** رفع مسیرهای هاردکد (`job-seeker.sh`).
9. **CI/CD:** اضافه کردن workflow برای build/test در `.github/workflows`.
10. **مستندسازی:** توضیح الگوریتم امتیازدهی (فرمول LaTeX در `JobBusiness.cs`) و حالت‌های ماشین Trend در یک سند (بخشی از آن در `TrendsCheckpoint.md` هست ولی ناقص).
11. **todo.txt** موارد باز: افزودن Qatar/Oman (بخشی در SQL هست)، bayt/qatarliving/omanjobs، popup هنگام باز کردن صفحه، حذف ستون HTML، نادیده‌گرفتن JS/JSON در محتوا.
