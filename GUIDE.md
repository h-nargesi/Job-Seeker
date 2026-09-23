# راهنمای اجرا و انتشار

جهت اجرای هسته:
```bash
dotnet run --project core-decision-dotnet -c Debug --urls http://localhost:5001
dotnet run --project ai-worker -c Debug --urls http://localhost:5002
```

جهت انتشار هسته:
```bash
dotnet publish core-decision-dotnet -c Release -o .\publish-core
dotnet publish core-decision-dotnet -c Release -o .\publish-ai-worker
```
