# EduVietRAG - Chatbot hoi dap tai lieu mon hoc

EduVietRAG la web app ASP.NET MVC cho phep sinh vien upload tai lieu mon hoc, index noi dung bang RAG va hoi dap dua tren cac tai lieu da dua vao he thong. Project duoc xay dung theo 3 lop: Presentation Layer, Services Layer va Data Access Layer.

## Yeu cau de bai da xu ly

- Quan ly tai lieu: upload PDF, DOCX, PPTX/slide va TXT.
- Tu dong trich xuat text, chunk tai lieu va tao embedding.
- Quan ly tai lieu theo mon hoc/chuong.
- Xem danh sach tai lieu da index va xem lai tai lieu goc.
- Chat hoi dap theo ngu canh hoi thoai.
- Tra loi co citation ve tai lieu/chunk nguon.
- Gioi han cau tra loi trong pham vi tai lieu; neu khong du can cu thi tu choi tra loi.
- Luu lich su hoi thoai theo phien.
- Module RBL de benchmark RAG theo embedding model, chunking strategy va fine-tuned baseline endpoint.
- Dashboard hien thi Faithfulness, Answer Relevancy, Context Precision, Context Recall va RAGAS score.
- Xuat bao cao RBL dang PDF.

## Cau truc project

```text
DataAccessLayer/       Entity, DbContext, repository SQL Server
ServicesLayer/         Xu ly upload/index, embedding, RAG chat, benchmark RBL
PresentationLayer/     ASP.NET MVC controller, Razor view, auth, static assets
Docs/                  Tai lieu nộp bài, audit yêu cầu, test set 50 câu
```

## Luong RAG

1. Nguoi dung upload tai lieu hoac URL.
2. He thong trich xuat text tu PDF/DOCX/PPTX/TXT/web page.
3. Text duoc chia chunk va embed.
4. Nguoi dung dat cau hoi trong chat.
5. He thong rewrite cau hoi theo lich su neu can.
6. Retrieval lay cac chunk lien quan, yeu cau co bang chung noi dung trong text.
7. Model chi duoc tra loi tu context da retrieve.
8. He thong kiem tra cau tra loi co bam context hay khong; neu khong, fallback ve cau tra loi trich xuat hoac tu choi.
9. Cau tra loi va citation duoc luu vao SQL Server.

## RBL benchmark

Module RBL tao experiment gom:

- Bo cau hoi benchmark va ground truth.
- Nhieu chunking strategy: fixed, sliding window, paragraph, semantic-lite.
- Nhieu embedding model trong catalog, gom Gemini va OpenAI `text-embedding-3-small`.
- Fine-tuned baseline thong qua endpoint ngoai neu co.

Moi run luu:

- Generated answer.
- Retrieved chunks.
- Faithfulness.
- Answer Relevancy.
- Context Precision.
- Context Recall.
- RAGAS score.
- Latency.

Bo 50 cau hoi benchmark mau nam tai [Docs/DBA103_TEST_SET_50.md](Docs/DBA103_TEST_SET_50.md).

## Cach chay

Yeu cau:

- .NET SDK 9.x.
- SQL Server LocalDB/Express/Developer.
- Gemini API key neu dung upload, chat hoac RBL voi Gemini.
- OpenAI API key neu chay RBL voi `text-embedding-3-small`.

Lenh chay nhanh:

```powershell
cd C:\Assignment1
dotnet restore
dotnet build Group7_SE1950.sln
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project PresentationLayer\Group07MVC.csproj
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY" --project PresentationLayer\Group07MVC.csproj
dotnet run --project PresentationLayer\Group07MVC.csproj --urls http://localhost:5097
```

Mo web:

```text
http://localhost:5097
```

## Ghi chu nghiep vu

- Metadata nhu ma mon, ten file, chuong chi dung de gioi han/tang hang retrieval; khong duoc xem la bang chung tra loi.
- Cau tra loi theo tai lieu phai co citation.
- Cau hoi ngoai pham vi tai lieu phai tra ve thong bao khong du du lieu.
- RBL score chi co y nghia khi co test set va ground truth duoc chuan bi truoc.
- Khong nen dua API key hoac OAuth secret that vao source khi nop/chia se project.

## Tai lieu lien quan

- [Docs/ASSIGNMENT_REQUIREMENT_AUDIT.md](Docs/ASSIGNMENT_REQUIREMENT_AUDIT.md)
- [Docs/DBA103_TEST_SET_50.md](Docs/DBA103_TEST_SET_50.md)
- [SETUP_CODE_FIRST.md](SETUP_CODE_FIRST.md)
