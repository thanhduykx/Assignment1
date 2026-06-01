# Setup Code First

Project này dùng EF Core code-first runtime schema initializer. Khi app start, database/tables sẽ được tạo bằng code nếu `DefaultConnection` trỏ tới SQL Server hợp lệ.

## Yêu cầu

- .NET SDK 9.x
- SQL Server LocalDB, SQL Server Express, hoặc SQL Server Developer
- Gemini API key nếu dùng upload/chat/RBL Gemini

## Chạy nhanh trên máy mới

```powershell
cd C:\Assignment1
dotnet restore
dotnet build Group7_SE1950.sln
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project PresentationLayer\Group07MVC.csproj
dotnet run --project PresentationLayer\Group07MVC.csproj --urls http://localhost:5097
```

Mặc định app dùng SQL Server local theo cấu hình lớp:

```json
"DefaultConnection": "Server=localhost;Database=EduVietRAG;User Id=sa;Password=;TrustServerCertificate=True;MultipleActiveResultSets=True;"
```

Nếu máy khác dùng password SQL Server khác, override bằng user-secrets hoặc biến môi trường.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=EduVietRAG;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True;" --project PresentationLayer\Group07MVC.csproj
```

Hoặc dùng environment variable:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost;Database=EduVietRAG;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$env:GEMINI_API_KEY="YOUR_GEMINI_API_KEY"
dotnet run --project PresentationLayer\Group07MVC.csproj --urls http://localhost:5097
```

## Code-first behavior

- `Program.cs` đọc `ConnectionStrings:DefaultConnection`.
- `SqlKnowledgeRepository` / `SqlResearchRepository` dùng `KnowledgeSqlDbContext`.
- `KnowledgeSqlSchemaInitializer.EnsureTablesCreated()` gọi `Database.EnsureCreated()`.
- RBL seed catalog tự tạo Gemini embedding models và chunking strategies.
- Không cần chạy migration thủ công cho bản hiện tại.

## Lỗi duplicate entity trong Visual Studio

Nếu gặp lỗi:

```text
CS0101 The namespace 'DataAccessLayer.Entities' already contains a definition for ...
```

Nguyên nhân là trong `DataAccessLayer/Entities` có các file entity rời cũ như `Document.cs`, `User.cs`, `Experiment.cs` trùng với `DatabaseEntities.cs`.

Trong project hiện tại các file legacy này đã bị loại khỏi compile trong `DataAccessLayer.csproj`. Nếu copy project thủ công sang thư mục khác, hãy đảm bảo mở đúng solution mới nhất và không add lại các file legacy vào project.

Build kiểm tra:

```powershell
dotnet clean
dotnet build Group7_SE1950.sln
```
