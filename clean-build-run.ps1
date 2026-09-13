dotnet clean

Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue

dotnet build

dotnet run