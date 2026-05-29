# EduVietRAG - Kho tài liệu và chatbot học tập

EduVietRAG là web app quản lý tài liệu học tập và hỏi đáp theo nội dung đã upload. Người dùng có thể tải tài liệu môn học lên hệ thống, hệ thống tự trích xuất nội dung, chia nhỏ thành các đoạn dữ liệu, tạo embedding và dùng chatbot để trả lời câu hỏi dựa trên tài liệu.

Mục tiêu chính của dự án là giúp sinh viên tìm thông tin trong syllabus, PDF, DOCX, PPTX, TXT hoặc trang bài giảng nhanh hơn, đồng thời vẫn kiểm soát được nguồn trích dẫn để tránh trả lời bịa.

## Chức năng chính

### Quản lý tài liệu

- Upload tài liệu theo môn học và chương.
- Hỗ trợ PDF, DOCX, PPTX, TXT và URL trang bài giảng.
- Tự động trích xuất nội dung từ tài liệu.
- Với trang web động như SPA, React hoặc Vue, hệ thống có thể render trang trước khi lấy nội dung.
- Tự động chia nội dung thành các `DocumentChunk`.
- Tạo embedding cho từng chunk để phục vụ tìm kiếm ngữ nghĩa.
- Lưu tài liệu, chunk và metadata vào SQL Server.
- Cho phép xem lại tài liệu đã upload.
- Hiển thị danh sách tài liệu theo môn học, chương và trạng thái index.

### Chat tài liệu

- Tạo và lưu lịch sử phiên chat.
- Hỏi đáp dựa trên tài liệu đã index.
- Rewrite câu hỏi dựa trên lịch sử hội thoại để xử lý câu hỏi thiếu ngữ cảnh.
- Tìm các chunk liên quan trước khi gọi AI.
- Nếu tài liệu không đủ dữ liệu, chatbot phải báo không đủ dữ liệu thay vì tự suy đoán.
- Lưu citation cho câu trả lời gồm tài liệu, chương, chunk và điểm liên quan.
- Hỗ trợ câu hỏi giao tiếp cơ bản như chào hỏi, hỏi bot là ai, hỏi người dùng là ai.
- Hỗ trợ câu hỏi gần đúng hoặc sai chính tả nhẹ, ví dụ người dùng gõ nhầm mã môn.

### AI và RAG

Luồng hỏi đáp chính:

1. Người dùng gửi câu hỏi.
2. Hệ thống lưu câu hỏi vào lịch sử chat.
3. Nếu có lịch sử, AI viết lại câu hỏi thành câu độc lập.
4. Câu hỏi được embedding.
5. Hệ thống tìm các `DocumentChunk` liên quan trong SQL Server.
6. Nếu không đủ dữ liệu liên quan, trả về thông báo không đủ dữ liệu.
7. Nếu có dữ liệu, hệ thống gửi context cho model Ollama.
8. AI tạo câu trả lời dựa trên context.
9. Hệ thống lưu câu trả lời và citation.
10. Giao diện hiển thị câu trả lời cho người dùng.

Model chat hiện dùng qua Ollama, ưu tiên model local như `qwen2.5:3b`. Embedding cũng được thiết kế để gọi model embedding thật qua Ollama, có fallback hashing để app vẫn chạy khi model embedding chưa sẵn sàng.

## Kiến trúc tổng quan

Dự án đi theo mô hình 3 lớp:

### Presentation Layer

Chứa giao diện ASP.NET MVC, Razor Views, controller và view model.

Thành phần chính:

- `HomeController`
- `AccountController`
- Razor Views
- ViewModels cho upload, chat, xem tài liệu
- Cấu hình authentication, Google Auth và cookie login trong `Program.cs`

Presentation Layer chỉ nên nhận request, gọi service, trả view hoặc JSON. Không nên xử lý trực tiếp nghiệp vụ RAG hoặc truy cập database thẳng.

### Business Logic Layer

Chứa nghiệp vụ chính của hệ thống.

Thành phần chính:

- `DocumentIndexingService`: xử lý upload, chunk và embed tài liệu.
- `DocumentTextExtractor`: trích xuất text từ PDF, DOCX, PPTX, TXT.
- `WebPageTextExtractor`: trích xuất nội dung từ URL, có hỗ trợ trang render bằng JavaScript.
- `RagChatService`: xử lý luồng hỏi đáp, rewrite, retrieve, gọi AI và lưu citation.
- `OllamaChatCompletionService`: gọi model chat local qua Ollama.
- `OllamaEmbeddingService`: gọi model embedding qua Ollama.

Business Logic Layer là nơi giữ quy tắc nghiệp vụ: chỉ trả lời theo tài liệu, không dùng chunk sai ngữ cảnh, không hallucinate khi dữ liệu không đủ.

### Data Access Layer

Chứa logic truy cập dữ liệu và ánh xạ dữ liệu.

Thành phần chính:

- `IKnowledgeRepository`
- `SqlKnowledgeRepository`
- `KnowledgeSqlDbContext`
- `Entities`
- `Mapping`
- `SchemaInitializer`
- Các model dữ liệu như `IndexedDocument`, `DocumentChunk`, `ChatSession`, `ChatMessage`, `SourceCitation`

Data Access Layer chịu trách nhiệm đọc/ghi dữ liệu vào SQL Server, không xử lý nghiệp vụ chatbot.

## Database

Database chính: `EduVietRAG`

Các bảng quan trọng:

- `rag_documents`: lưu thông tin tài liệu.
- `rag_chunks`: lưu các chunk đã trích xuất và embedding.
- `rag_chat_sessions`: lưu phiên chat.
- `rag_chat_messages`: lưu tin nhắn user và assistant.
- `rag_citations`: lưu nguồn trích dẫn của từng câu trả lời.

Connection string nằm trong:

```text
PresentationLayer/appsettings.json
```

## Lưu trữ file

File upload được lưu vật lý trong:

```text
PresentationLayer/App_Data/uploads
```

Database chỉ lưu metadata, nội dung chunk và đường dẫn file. File gốc vẫn nằm trên ổ đĩa để người dùng có thể mở lại.

## Cách chạy project

Yêu cầu:

- .NET SDK phù hợp với project.
- SQL Server đang chạy.
- Database `EduVietRAG` tồn tại.
- Ollama đang chạy nếu muốn dùng AI local.

Build project:

```bash
dotnet build Assignment1.sln
```

Chạy web app:

```bash
dotnet run --project PresentationLayer/Group07MVC.csproj --urls http://localhost:5097
```

Mở trình duyệt:

```text
http://localhost:5097
```

## Quy tắc nghiệp vụ quan trọng

- Chatbot chỉ được trả lời dựa trên tài liệu đã index.
- Nếu tài liệu không đủ dữ liệu, phải trả lời rõ là không đủ dữ liệu.
- Mỗi câu trả lời theo tài liệu phải có citation.
- Câu hỏi sai chính tả nhẹ cần được xử lý thông minh, nhưng không được tự bịa dữ liệu.
- Metadata như mã môn học chỉ dùng để giới hạn phạm vi tìm kiếm, không được tự xem là bằng chứng trả lời.
- Các câu hỏi gợi ý trên giao diện phải là câu hệ thống có khả năng trả lời được.

## Quy tắc làm việc trong project

File này cần được đọc trước khi làm task mới.

Khi sửa code:

- Nói thẳng vấn đề, không che lỗi.
- Chỉ sửa đúng phạm vi yêu cầu.
- Không refactor lan rộng nếu không cần.
- Không thêm abstraction khi chưa có nhu cầu thật.
- Build hoặc kiểm tra runtime trước khi bàn giao nếu có thay đổi code.
- Nếu đã chạy dev server để kiểm tra, phải dừng server trước khi bàn giao.
- Trước khi bàn giao, kiểm tra port `5097` không còn listener nếu đã dùng port này.
