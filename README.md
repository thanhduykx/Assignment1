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
- Module RBL de benchmark RAG theo embedding model, chunking strategy, local fine-tuned baseline va endpoint ngoai tuy chon.
- Dashboard hien thi Faithfulness, Answer Relevancy, Context Precision, Context Recall va RAGAS score.
- Xuat bao cao RBL dang PDF.

## Cau truc project

```text
DataAccessLayer/       Entity, DbContext, repository SQL Server
ServicesLayer/         Xu ly upload/index, embedding, RAG chat, benchmark RBL
PresentationLayer/     ASP.NET MVC controller, Razor view, auth, static assets
README.md             Tai lieu nop bai, audit yeu cau va test set 50 cau
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
- Nhieu embedding model trong catalog, gom Gemini va HuggingFace `vinai/phobert-base`.
- Fine-tuned baseline local duoc train tu QA pairs trong experiment, hoac endpoint ngoai neu co.

Moi run luu:

- Generated answer.
- Retrieved chunks.
- Faithfulness.
- Answer Relevancy.
- Context Precision.
- Context Recall.
- RAGAS score.
- Latency.

Fine-tuned local baseline:

- Khi tao experiment, bat `Train local supervised QA baseline`.
- Neu khong nhap training set rieng, baseline train tu QA pairs cua experiment va benchmark theo leave-one-out de tranh copy dung cau dang cham.
- Artifact model duoc luu trong DB `rbl_fine_tuned_models.ConfigJson`.
- Day la supervised QA baseline noi bo de so sanh voi RAG. Neu can fine-tune LLM that, can provider/API rieng.

## Audit de bai

| Yeu cau | Trang thai hien tai |
|---|---|
| Upload PDF/DOCX/PPTX/TXT | Dat |
| Tu dong chunk va embed | Dat |
| Quan ly mon hoc/chuong | Dat: co CRUD `rag_subjects` va `rag_chapters` tren trang Kho tai lieu |
| Xem danh sach tai lieu da index | Dat |
| Chat theo ngu canh hoi thoai | Dat |
| Citation nguon tai lieu goc | Dat |
| Gioi han tra loi trong pham vi tai lieu | Dat sau toi uu: retrieval can bang chung text va co answer-grounding guard |
| Lich su hoi thoai theo phien | Dat |
| So sanh RAG vs fine-tuned | Dat: co RAG runs, local supervised fine-tuned baseline va endpoint ngoai tuy chon |
| Benchmark nhieu chunking strategy | Dat |
| Benchmark nhieu embedding model | Dat: Gemini + HuggingFace `vinai/phobert-base`/PhoBERT-base; chua co local e5/bge-m3 |
| Dashboard/Bang RAGAS | Dat ve code/UI; so lieu that can chay benchmark voi DB/API key |

## Bo test set 50 cau DBA103

```text
DBA103 là môn học gì? | DBA103 là môn Nhạc cụ truyền thống - Đàn Bầu, tên tiếng Anh là Traditional musical instrument.
Tên tiếng Anh của syllabus DBA103 là gì? | Tên tiếng Anh của syllabus là Traditional musical instrument.
DBA103 có bao nhiêu tín chỉ? | DBA103 có 3 tín chỉ.
Cấp độ của môn DBA103 là gì? | Degree Level của môn là Sơ cấp / Beginner.
Thời lượng học trên lớp của DBA103 là bao nhiêu? | Môn học có 30 slot trên lớp, mỗi slot 90 phút.
DBA103 có môn tiên quyết không? | Pre-Requisite của DBA103 là Không / None.
Mục tiêu kiến thức của DBA103 là gì? | Sinh viên nắm đặc trưng lịch sử phát triển, cấu trúc Đàn Bầu, làm quen nhạc lý và kỹ thuật cơ bản của Đàn Bầu.
Mục tiêu kỹ năng của DBA103 là gì? | Sinh viên đánh được tối thiểu 3 bài, trong đó có 1 bài nhạc nước ngoài thông dụng và vận dụng đúng kỹ thuật cơ bản.
Quy mô lớp DBA103 khoảng bao nhiêu sinh viên? | Môn học được triển khai theo lớp có khoảng 15 sinh viên.
Nội dung chính của DBA103 gồm những gì? | Nội dung gồm lịch sử Đàn Bầu ở Việt Nam, cấu trúc và đặc điểm Đàn Bầu, tư thế đánh đàn, nhạc lý, kỹ thuật cơ bản và luyện tập các bài nhạc.
DBA103 dạy những kỹ thuật cơ bản nào? | Kỹ thuật cơ bản gồm gảy dây buông và nhấn lên/xuống quãng 2.
Sinh viên luyện tập bài Việt Nam nào trong DBA103? | Sinh viên luyện tập Cò lả và Lý cây đa theo hình thức hòa tấu.
Bài quốc tế trong DBA103 là bài nào? | Bài quốc tế là Auld lang syne, dân ca Scotland.
Học phần DBA103 áp dụng CNTT như thế nào? | Giảng viên cung cấp địa chỉ website, clip nhạc truyền thống và tài nguyên trên mạng cho sinh viên.
Tài nguyên online trong DBA103 được sử dụng theo nguyên tắc nào? | Giảng viên sử dụng tài nguyên có chọn lọc theo nguyên tắc học phần và hướng dẫn sinh viên tìm thông tin theo chủ đề.
DBA103 phát triển kỹ năng mềm nào? | Môn học rèn luyện tính kiên trì, giúp sinh viên tự tin hơn trước đám đông.
DBA103 có phát triển kỹ năng làm việc nhóm không? | Có, môn học phát triển kỹ năng làm việc nhóm và làm việc độc lập qua nhiệm vụ giảng viên giao.
Điều kiện tham dự thi cuối môn DBA103 là gì? | Sinh viên phải tham dự tối thiểu 80% thời lượng môn học để đủ điều kiện thi cuối môn.
Sinh viên cần làm gì trước khi đến lớp DBA103? | Sinh viên cần ôn tập bài cũ và tìm hiểu tài liệu bài học mới trước khi đến lớp.
Sinh viên cần luyện tập DBA103 ở đâu? | Sinh viên cần luyện tập và thực hành trên lớp và ở nhà.
Sinh viên cần tham gia hoạt động lớp như thế nào? | Sinh viên cần tích cực phát biểu, hỏi đáp, trao đổi, làm việc nhóm, thực hành và làm Portfolio.
Công cụ học tập của DBA103 là gì? | Công cụ là nhạc cụ cho từng sinh viên.
Thang điểm của DBA103 là bao nhiêu? | Scoring Scale của môn là 10.
Điểm assignment của DBA103 chiếm bao nhiêu phần trăm? | Assignment chiếm 15%.
Điểm tham gia lớp của DBA103 chiếm bao nhiêu phần trăm? | Participation chiếm 15%.
Thi cuối môn DBA103 chiếm bao nhiêu phần trăm? | Final exam chiếm 70% và yêu cầu điểm thực hành chơi nhạc cụ theo yêu cầu.
Tổng điểm FE cần đạt trong DBA103 là bao nhiêu? | Final Result yêu cầu >=5.
MinAvgMarkToPass của DBA103 là bao nhiêu? | MinAvgMarkToPass là 5.
Tài liệu chính của DBA103 là gì? | Tài liệu chính là Sách học Đàn Bầu.
Tác giả Sách học Đàn Bầu là ai? | Tác giả là Nguyễn Thanh Tâm và Trần Quốc Lộc.
Tài liệu luyện tập kỹ thuật trong DBA103 là gì? | Tài liệu là Bài tập luyện kỹ thuật đàn Bầu / Exercises to practice techniques of Dan Bau.
Danh mục bài nhạc có thể sử dụng trong học phần có bao nhiêu bài? | Danh mục có 12 bài nhạc có thể sử dụng trong học phần.
Kể tên một số bài nhạc trong danh mục DBA103. | Danh mục gồm Bắc Kim Thang, Inh lả ơi, Xòe hoa, Lý cây đa, Trống cơm, Đội kèn tí hon, Auld lang syne và các bài khác.
CLO1 của DBA103 là gì? | CLO1 là hiểu biết cơ bản về lịch sử và sự phát triển hình thành của nền âm nhạc truyền thống Việt Nam.
CLO2 của DBA103 là gì? | CLO2 là chơi được một số bài cơ bản của nhạc truyền thống Việt Nam và nước ngoài.
Syllabus ghi có bao nhiêu session? | Syllabus ghi 30 sessions.
Nội dung session 1 của DBA103 là gì? | Session 1 gồm tìm hiểu lịch sử, tên gọi nhạc cụ, cấu tạo cây đàn, tư thế ngồi chơi đàn và cách gảy đàn.
Bài đầu tiên sinh viên bắt đầu học trong DBA103 là bài nào? | Bài đầu tiên là Đội kèn tí hon, bắt đầu ở session 7.
Lý cây đa được giới thiệu ở session nào? | Lý cây đa được học ở session 9.
Kiểm tra giữa kỳ DBA103 diễn ra ở session nào và nội dung gì? | Session 15 kiểm tra giữa kỳ 2 bài Lý cây đa và Đội kèn tí hon.
Bài Cò lả được luyện trong những session nào? | Bài Cò lả được áp dụng/luyện tập trong các session 17 đến 19.
Auld lang syne được học trong những session nào? | Auld lang syne được học và luyện từ session 20 đến 23.
Session 25 đến 27 của DBA103 tập trung vào nội dung gì? | Sinh viên hòa tấu Lý cây đa và Cò lả cùng các lớp khác.
Session 28 của DBA103 có yêu cầu gì đặc biệt? | Session 28 có hòa tấu Lý cây đa, Cò lả và thu bài luận / submit essays.
Thi kết thúc khóa học DBA103 diễn ra ở session nào? | Thi kết thúc khóa học diễn ra ở session 29 và 30.
DBA103 có bao nhiêu assessment? | Syllabus ghi 3 assessment.
Assignment của DBA103 có thể chấm sản phẩm nào? | Assignment có thể chấm bài thuyết trình, slides, bài luận hoặc sản phẩm từ workshop.
Participation của DBA103 được ghi nhận dựa trên gì? | Participation dựa trên việc sinh viên tích cực tham gia hoạt động lớp, đi học đầy đủ và nộp bài luận.
Thời lượng thi cuối môn cho mỗi sinh viên là bao nhiêu? | Final exam có thời lượng 5 phút mỗi sinh viên.
Thi cuối môn DBA103 yêu cầu sinh viên chơi mấy bài? | Sinh viên chơi 3 bài gồm 2 bài hòa tấu và 1 bài độc tấu, trong đó có 2 bài dân ca Việt Nam và 1 bài quốc tế.
```

## Cach chay

Yeu cau:

- .NET SDK 9.x.
- SQL Server LocalDB/Express/Developer.
- Gemini API key neu dung upload, chat hoac RBL voi Gemini.
- HuggingFace API key neu chay RBL voi `vinai/phobert-base`/PhoBERT-base.

Lenh chay nhanh:

```powershell
cd C:\Assignment1
dotnet restore
dotnet build Group7_SE1950.sln
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project PresentationLayer\Group07MVC.csproj
dotnet user-secrets set "HuggingFace:ApiKey" "YOUR_HUGGINGFACE_API_KEY" --project PresentationLayer\Group07MVC.csproj
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
- PhoBERT-base duoc goi qua HuggingFace feature-extraction va mean-pooling de tao vector so sanh; day khong phai native sentence embedding model nen can doc ket qua benchmark theo huong tham khao thuc nghiem.
- Khong nen dua API key hoac OAuth secret that vao source khi nop/chia se project.
