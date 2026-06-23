# EduVietRAG

EduVietRAG là web app ASP.NET Core MVC hỗ trợ quản lý tài liệu môn học, lập chỉ mục nội dung bằng RAG và hỏi đáp dựa trên tài liệu đã đưa vào hệ thống. Project được tổ chức theo mô hình 3 layer: Presentation Layer, Services Layer và Data Access Layer.

## Mục Lục

- [Tổng quan chức năng](#tổng-quan-chức-năng)
- [Sơ đồ hệ thống](#sơ-đồ-hệ-thống)
- [Cấu trúc source code](#cấu-trúc-source-code)
- [Phân quyền người dùng](#phân-quyền-người-dùng)
- [Luồng xử lý chính](#luồng-xử-lý-chính)
- [Hướng dẫn chạy project](#hướng-dẫn-chạy-project)
- [Hướng dẫn sử dụng](#hướng-dẫn-sử-dụng)
- [RBL benchmark](#rbl-benchmark)
- [Kiểm thử](#kiểm-thử)
- [Lưu ý vận hành](#lưu-ý-vận-hành)

## Tổng Quan Chức Năng

### Quản lý tài liệu học tập

- Upload tài liệu `PDF`, `DOCX`, `PPTX`, `TXT` hoặc index nội dung từ URL.
- Quản lý tài liệu theo môn học và chương.
- Tự động trích xuất text, chia chunk và tạo embedding.
- Theo dõi trạng thái index: đang xử lý, đã index, lỗi.
- Xem danh sách tài liệu, xem text đã trích xuất và mở lại file gốc.

### Chat hỏi đáp theo tài liệu

- Chat theo ngữ cảnh tài liệu đã index.
- Lưu lịch sử hội thoại theo phiên.
- Trả lời kèm citation tới tài liệu/chunk nguồn.
- Giới hạn câu trả lời trong phạm vi tài liệu được retrieve.
- Khi không đủ căn cứ, hệ thống ưu tiên từ chối hoặc trả lời theo fallback có kiểm soát.

### Quản trị và nghiên cứu RAG

- Quản lý tài khoản người dùng và vai trò.
- Gán môn học cho giảng viên phụ trách.
- Tạo experiment benchmark RAG.
- So sánh nhiều embedding model, chunking strategy và baseline fine-tuned cục bộ.
- Theo dõi các chỉ số: Faithfulness, Answer Relevancy, Context Precision, Context Recall, RAGAS score và latency.
- Xuất báo cáo benchmark dạng PDF.

## Sơ Đồ Hệ Thống

Sơ đồ chi tiết được thiết kế trên diagrams.net:

[Mở sơ đồ hệ thống](https://app.diagrams.net/#G1Ireq80WAgnChacUyk76dZQyWvbLdtH19#%7B%22pageId%22%3A%22Vo5JyxTqIsNgc_N_pEdJ%22%7D)

Sơ đồ kiến trúc tổng quan trong project:

![Sơ đồ kiến trúc EduVietRAG](Docs\images\architecture.jpg)

## Cấu Trúc Source Code Project

```text
C:\Assignment1
├── Group7_SE1950.sln
├── README.md
├── DataAccessLayer/
│   ├── Context/                    # DbContext và factory kết nối SQL Server
│   ├── Entities/                   # Entity map với bảng dữ liệu
│   ├── Enums/                      # Enum nghiệp vụ: role, document status, experiment status
│   ├── Mapping/                    # Mapper giữa entity và domain model
│   ├── Repositories/               # Repository SQL cho knowledge base và research module
│   ├── Schema/                     # Tự khởi tạo/bổ sung schema khi chạy app
│   ├── IKnowledgeRepository.cs
│   ├── IResearchRepository.cs
│   └── Models.cs
├── ServicesLayer/
│   ├── DocumentTextExtractor.cs    # Trích xuất text từ PDF/DOCX/PPTX/TXT
│   ├── WebPageTextExtractor.cs     # Trích xuất text từ URL/web page
│   ├── DocumentIndexingService.cs  # Upload, queue và index tài liệu
│   ├── DocumentIndexJobQueue.cs    # Queue xử lý index nền
│   ├── TextChunker.cs              # Chunking theo paragraph/fixed/sliding
│   ├── GeminiSemanticTextChunker.cs# Chunking semantic qua Gemini khi bật cấu hình
│   ├── EmbeddingService.cs         # Gemini embedding + hashing fallback
│   ├── RagChatService.cs           # Retrieval, prompt, answer và citation
│   ├── FineTunedChatService.cs     # Baseline supervised QA cục bộ hoặc endpoint ngoài
│   └── ResearchBenchmarkService.cs # Chạy experiment RBL/RAGAS-like metrics
├── PresentationLayer/
│   ├── Controllers/
│   │   ├── AccountController.cs    # Login, Google login, logout
│   │   ├── AdminController.cs      # Quản lý user, role, subject owner
│   │   ├── HomeController.cs       # Dashboard tài liệu, upload, chat
│   │   └── ResearchController.cs   # Benchmark RBL và report PDF
│   ├── Models/                     # ViewModel cho Razor views
│   ├── Security/                   # AppRoles và AuthorizationPolicies
│   ├── Services/                   # User store, background worker, PDF report
│   ├── Views/                      # Razor pages
│   ├── wwwroot/                    # CSS, JS, Bootstrap, jQuery, font, image
│   ├── App_Data/                   # users.json, fine-tuned examples, uploads runtime
│   ├── Program.cs                  # DI, auth, policy, config, middleware
│   └── appsettings.json
├── ServicesLayer.Tests/
│   └── *.cs                        # Unit tests cho chunking, indexing, Gemini chunker, RAG chat
├── TestData/
│   └── qa-test-50-vi-q-a.txt       # Bộ câu hỏi kiểm thử tiếng Việt
└── Docs/                           # Tài liệu bổ sung nếu có
```

## Phân Quyền Người Dùng

Hệ thống hiện có 3 vai trò chính được định nghĩa trong `PresentationLayer/Security/AppRoles.cs`:

| Vai trò | Quyền chính | Màn hình mặc định sau đăng nhập |
|---|---|---|
| `Student` | Chat hỏi đáp, xem tài liệu được phép truy cập | `/Home/Chat` |
| `Lecturer` | Xem tài liệu, upload/index tài liệu, quản lý chương trong môn được giao | `/Home/Index` |
| `Admin` | Toàn quyền: quản lý user, role, môn học, phân công giảng viên, tài liệu, chat và benchmark RBL | `/Research/Index` |

Các policy đang dùng:

| Policy | Role được phép | Mục đích |
|---|---|---|
| `ChatAccess` | Student, Lecturer, Admin | Truy cập chat và quản lý phiên chat cá nhân |
| `DocumentRead` | Student, Lecturer, Admin | Xem danh sách tài liệu và nội dung tài liệu |
| `DocumentManagement` | Lecturer, Admin | Upload, sửa, xóa, index tài liệu; quản lý chương theo phạm vi được phép |
| `AdminOnly` | Admin | Quản trị người dùng, phân quyền, benchmark RBL |

Quy tắc nghiệp vụ quan trọng:

- Người dùng không tự đăng ký tài khoản. Tài khoản được cấp bởi Admin.
- Google login chỉ hoạt động nếu email Google đã tồn tại trong danh sách tài khoản được cấp.
- Admin có thể tạo user local, đổi tên, đổi role và gán môn cho Lecturer.
- Lecturer chỉ nên thao tác trên môn được phân công.
- Student tập trung vào luồng hỏi đáp, không có quyền upload hoặc quản trị tài liệu.

## Luồng Xử Lý Chính

### Luồng index tài liệu

1. Lecturer hoặc Admin upload file hoặc nhập URL.
2. Hệ thống kiểm tra môn học/chương và quyền thao tác.
3. File được lưu vào `PresentationLayer/App_Data/uploads`.
4. Job index được đưa vào queue nền.
5. Worker trích xuất text từ tài liệu hoặc web page.
6. Text được chia chunk theo cấu hình.
7. Embedding được tạo bằng Gemini; khi không đủ điều kiện, hệ thống có hashing fallback.
8. Metadata, chunk và embedding được lưu vào SQL Server.

### Luồng chat RAG

1. Người dùng chọn hoặc tạo phiên chat.
2. Người dùng đặt câu hỏi, có thể lọc theo môn học.
3. Hệ thống xác định tập tài liệu người dùng được phép truy cập.
4. Retrieval lấy các chunk liên quan nhất.
5. Chat service gọi model để tạo câu trả lời dựa trên context.
6. Câu trả lời được kiểm soát để bám tài liệu nguồn.
7. Citation và lịch sử hội thoại được lưu lại.

### Luồng benchmark RBL

1. Admin tạo experiment với danh sách câu hỏi dạng `question | expected answer`.
2. Chọn embedding model và chunking strategy cần so sánh.
3. Có thể bật baseline supervised QA cục bộ.
4. Chạy benchmark nền.
5. Xem metric từng run và xuất report PDF.

## Hướng Dẫn Chạy Project

### Yêu cầu môi trường

- .NET SDK 9.x.
- SQL Server LocalDB, Express hoặc Developer.
- Gemini API key nếu dùng chat, embedding hoặc semantic chunking bằng Gemini.
- HuggingFace API key nếu benchmark với model từ HuggingFace, ví dụ `vinai/phobert-base`.

### Chạy nhanh

```powershell
cd C:\Assignment1
dotnet restore
dotnet build Group7_SE1950.sln
dotnet run --project PresentationLayer\Group07MVC.csproj --urls http://localhost:5097
```

Sau khi app chạy, mở:

```text
http://localhost:5097
```

### Cấu hình nên kiểm tra

File cấu hình chính:

```text
PresentationLayer/appsettings.json
```

Các nhóm cấu hình quan trọng:

| Nhóm | Mục đích |
|---|---|
| `ConnectionStrings:DefaultConnection` | Kết nối SQL Server |
| `SeedAdmin` | Tài khoản admin mặc định khi hệ thống chưa có user |
| `Authentication:Google` | Google OAuth login |
| `Gemini` | Chat model, semantic chunking và API key |
| `Embedding` | Model embedding và số chiều vector |
| `HuggingFace` | API key/base address cho benchmark HuggingFace |
| `FineTunedChat` | Baseline supervised QA cục bộ hoặc endpoint ngoài |

Khuyến nghị cho môi trường thật: không commit API key/password vào `appsettings.json`. Dùng User Secrets hoặc biến môi trường:

```powershell
cd C:\Assignment1\PresentationLayer
dotnet user-secrets set "GEMINI_API_KEY" "your-gemini-key"
dotnet user-secrets set "HUGGINGFACE_API_KEY" "your-huggingface-key"
dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
```

## Hướng Dẫn Sử Dụng

### 1. Đăng nhập

1. Truy cập `/Account/Login`.
2. Đăng nhập bằng tài khoản được Admin cấp.
3. Nếu dùng Google login, email Google phải trùng với user đã có trong hệ thống.

### 2. Admin quản lý hệ thống

1. Vào trang Admin để tạo tài khoản cho Student, Lecturer hoặc Admin.
2. Tạo môn học nếu cần.
3. Gán môn học cho Lecturer phụ trách.
4. Theo dõi và điều chỉnh role người dùng khi nghiệp vụ thay đổi.

### 3. Lecturer quản lý tài liệu

1. Vào trang tài liệu `/Home/Index`.
2. Chọn môn học và chương phù hợp.
3. Upload file `PDF`, `DOCX`, `PPTX`, `TXT` hoặc nhập URL.
4. Chờ trạng thái tài liệu chuyển sang indexed.
5. Kiểm tra lại nội dung trích xuất và citation nếu cần.

### 4. Student chat với tài liệu

1. Vào `/Home/Chat`.
2. Chọn môn học nếu muốn giới hạn phạm vi hỏi đáp.
3. Đặt câu hỏi bằng tiếng Việt hoặc tiếng Anh tùy tài liệu.
4. Đọc câu trả lời kèm citation nguồn.
5. Tạo, đổi tên, đánh dấu hoặc xóa phiên chat khi cần quản lý lịch sử.

### 5. Admin chạy benchmark RBL

1. Vào `/Research/Index`.
2. Tạo experiment mới.
3. Nhập câu hỏi theo format:

```text
Câu hỏi | Câu trả lời kỳ vọng
```

4. Chọn embedding model và chunking strategy.
5. Chạy benchmark.
6. Xem kết quả chi tiết và xuất PDF report.

## RBL Benchmark

Module RBL hỗ trợ đánh giá chất lượng RAG theo experiment. Mỗi experiment có thể gồm:

- Bộ câu hỏi benchmark và ground truth.
- Nhiều chunking strategy: fixed, sliding window, paragraph, semantic-lite.
- Nhiều embedding model trong catalog, gồm Gemini và HuggingFace `vinai/phobert-base`.
- Baseline fine-tuned local dạng supervised QA để so sánh với RAG.
- Endpoint ngoài tùy chọn nếu muốn tích hợp model khác.

Thông tin lưu cho mỗi run:

- Generated answer.
- Retrieved chunks.
- Faithfulness.
- Answer Relevancy.
- Context Precision.
- Context Recall.
- RAGAS score.
- Latency.

Lưu ý: baseline fine-tuned local trong project là supervised QA baseline nội bộ, không phải fine-tune LLM thật trên hạ tầng cloud. Nếu cần fine-tune LLM thật, cần bổ sung pipeline training/deployment riêng.

## Kiểm Thử

Chạy toàn bộ test:

```powershell
cd C:\Assignment1
dotnet test Group7_SE1950.sln
```

Các nhóm test hiện có trong `ServicesLayer.Tests`:

- `ParagraphAwareTextChunkerTests`.
- `GeminiSemanticTextChunkerTests`.
- `DocumentIndexingServiceTests`.
- `RagChatServiceTests`.

Bộ dữ liệu hỏi đáp tiếng Việt dùng cho kiểm thử/benchmark nằm tại:

```text
TestData/qa-test-50-vi-q-a.txt
```

## Lưu Ý Vận Hành

- Luôn kiểm tra quyền trước khi mở thêm action mới trong controller.
- Không để Student có quyền upload, sửa, xóa hoặc benchmark.
- Không để Lecturer thao tác ngoài môn được giao.
- Khi thêm loại file mới, cần cập nhật cả extractor, validation upload và test.
- Khi đổi model embedding, cần chú ý số chiều vector và dữ liệu index cũ.
- Không commit secret thật vào repository.
- Với môi trường production, nên dùng HTTPS, secret manager, logging tập trung và backup SQL Server định kỳ.

## Thông Tin Kỹ Thuật Nhanh

| Thành phần | Công nghệ |
|---|---|
| Backend web | ASP.NET Core MVC `.NET 9` |
| View | Razor Views |
| Auth | Cookie Authentication, Google OAuth tùy cấu hình |
| Database | SQL Server |
| ORM | Entity Framework Core SQL Server |
| PDF report | QuestPDF |
| Document parsing | PdfPig, OpenXML |
| Web extraction | HttpClient, Playwright package |
| Test | xUnit |
| AI provider | Gemini, HuggingFace tùy cấu hình |