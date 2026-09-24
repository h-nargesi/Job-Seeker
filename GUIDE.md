<div dir="rtl" lang="fa">

# راهنمای اجرا و انتشار

جهت اجرای هسته:
<div dir="ltr">

```bash
dotnet run --project core-decision-dotnet -c Debug --urls http://localhost:5001
dotnet run --project ai-worker -c Debug --urls http://localhost:5002
```

</div>

جهت انتشار هسته:
<div dir="ltr">

```bash
dotnet publish core-decision-dotnet -c Release -o ./publish-core
dotnet publish ai-worker -c Release -r linux-x64 --self-contained -o ./publish-ai-worker
```

</div>

</div>
