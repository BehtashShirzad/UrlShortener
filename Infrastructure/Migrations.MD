dotnet ef migrations add init --startup-project .\Api\  --project .\Infrastructure\
dotnet ef database update --startup-project .\Api\  --project .\Infrastructure\