# Setup chạy trên máy mới

Project dùng EF Core code-first runtime schema initializer. App tự tạo database/tables khi connection string hợp lệ. API key nên cấu hình bằng user-secrets hoặc environment variable, không đặt trực tiếp vào source.

## Yêu cầu còn lại

- Cài .NET SDK 9.x.
- Có SQL Server LocalDB. Máy có Visual Studio thường đã có sẵn `MSSQLLocalDB`.
- Có mạng Internet nếu dùng Gemini hoặc OpenAI embedding trong RBL.

Không cần tạo database thủ công. App tự tạo database `EduVietRAG`, tự tạo bảng và tự import dữ liệu từ `PresentationLayer/App_Data/rag-store.json` nếu database đang rỗng.

## Cấu hình runtime

File cấu hình chính:

```text
PresentationLayer/appsettings.json
```

Database mặc định:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=EduVietRAG;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
```

Gemini và OpenAI API key nên đặt bằng user-secrets:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project PresentationLayer\Group07MVC.csproj
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY" --project PresentationLayer\Group07MVC.csproj
```

Hoặc dùng environment variables:

```powershell
$env:GEMINI_API_KEY="YOUR_GEMINI_API_KEY"
$env:OPENAI_API_KEY="YOUR_OPENAI_API_KEY"
```

## Chạy nhanh

```powershell
cd C:\Assignment1
dotnet restore
dotnet build Group7_SE1950.sln
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project PresentationLayer\Group07MVC.csproj
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY" --project PresentationLayer\Group07MVC.csproj
dotnet run --project PresentationLayer\Group07MVC.csproj --urls http://localhost:5097
```

Mở:

```text
http://localhost:5097
```

## Khi copy sang máy khác

Copy các thư mục source chính, không copy cache build:

```text
DataAccessLayer
ServicesLayer
PresentationLayer
Assignment1.sln
Group7_SE1950.sln
README.md
SETUP_CODE_FIRST.md
```

Không copy:

```text
.vs
bin
obj
```

Nếu đã lỡ copy cache, chạy:

```powershell
dotnet clean
Remove-Item -Recurse -Force .vs, DataAccessLayer\bin, DataAccessLayer\obj, ServicesLayer\bin, ServicesLayer\obj, PresentationLayer\bin, PresentationLayer\obj -ErrorAction SilentlyContinue
dotnet restore
dotnet build Group7_SE1950.sln
```

## Lỗi thường gặp

Nếu máy khác vẫn báo:

```text
ExperimentRun could not be found
TestQuestion could not be found
BenchmarkResult.cs
```

Máy đó đang giữ file entity cũ hoặc chưa copy đúng bản mới. Bản hiện tại đã exclude entity legacy khỏi build trong `DataAccessLayer/DataAccessLayer.csproj`, bao gồm `Entities/BenchmarkResult.cs`.

Nếu máy khác không có dữ liệu, kiểm tra database đang dùng có phải `EduVietRAG` trên `(localdb)\MSSQLLocalDB` không. Nếu database đã tồn tại nhưng rỗng/lỗi, xóa database `EduVietRAG` trong SQL Server Object Explorer rồi chạy lại app để seed lại từ `rag-store.json`.
