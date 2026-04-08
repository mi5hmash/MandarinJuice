### Publish Profiles Commands
```
dotnet publish /p:PublishProfile=test.pubxml -v diag
---
dotnet publish /p:PublishProfile=win-x64.pubxml -v diag
dotnet publish /p:PublishProfile=win-arm64.pubxml -v diag
dotnet publish /p:PublishProfile=osx-x64.pubxml -v diag
dotnet publish /p:PublishProfile=osx-arm64.pubxml -v diag
dotnet publish /p:PublishProfile=linux-x64.pubxml -v diag
dotnet publish /p:PublishProfile=linux-arm64.pubxml -v diag
---
dotnet publish /p:PublishProfile=win-x64-portable.pubxml -v diag
dotnet publish /p:PublishProfile=win-arm64-portable.pubxml -v diag
dotnet publish /p:PublishProfile=osx-x64-portable.pubxml -v diag
dotnet publish /p:PublishProfile=osx-arm64-portable.pubxml -v diag
dotnet publish /p:PublishProfile=linux-x64-portable.pubxml -v diag
dotnet publish /p:PublishProfile=linux-arm64-portable.pubxml -v diag

```