# Audit yeu cau de bai

Nguon yeu cau: anh de bai do nguoi dung cung cap trong thread.

## Tom tat de bai

Topic: xay dung chatbot cho phep sinh vien hoi dap dua tren tai lieu mon hoc, dong thoi nghien cuu va so sanh hieu qua giua RAG va fine-tuning trong boi canh tieng Viet.

## Doi chieu hien trang

| Nhom yeu cau | Trang thai | Bang chung trong project | Ghi chu thang |
|---|---:|---|---|
| Web app chatbot | Dat | `PresentationLayer`, `HomeController`, `Views/Home/Chat.cshtml` | Co giao dien chat, phien chat va upload. |
| Upload PDF/DOCX/slide | Dat | `DocumentTextExtractor`, `DocumentIndexingService` | Co xu ly PDF, DOCX, PPTX, TXT. |
| Tu dong chunk va embed | Dat | `DocumentIndexingService`, `EmbeddingService` | Chunk size/overlap co san; embedding qua Gemini. |
| Quan ly mon hoc/chuong | Dat mot phan | `IndexedDocument.Subject`, `Chapter`, upload form | Co metadata mon/chuong; chua co CRUD mon hoc rieng. De bai chi can demo 1 mon nen chap nhan duoc. |
| Xem danh sach tai lieu da index | Dat | `HomeController.Index`, `Views/Home/Index.cshtml` | Co danh sach tai lieu. |
| Chat theo ngu canh hoi thoai | Dat | `RagChatService.RewriteQuestionAsync`, chat session | Co rewrite cau hoi theo lich su. |
| Trich dan nguon tai lieu goc | Dat | `SourceCitation`, `rag_citations`, UI chat | Co file, subject, chapter, chunk, excerpt, score. |
| Gioi han tra loi trong pham vi tai lieu | Dat sau toi uu | `RagChatService` | Da sieu retrieval va answer grounding guard de giam doan mo. |
| Lich su hoi thoai theo phien | Dat | `rag_chat_sessions`, `rag_chat_messages` | Co tao, xem va luu phien. |
| So sanh RAG vs fine-tuned | Dat mot phan | `ResearchBenchmarkService`, `FineTunedEndpoint` | Co baseline endpoint, nhung project khong tu train fine-tuned model. |
| Benchmark nhieu chunking strategy | Dat | `KnowledgeSqlSchemaInitializer` | Seed fixed, sliding, paragraph, semantic-lite. |
| Benchmark nhieu embedding model | Dat mot phan manh hon | `KnowledgeSqlSchemaInitializer`, `ResearchBenchmarkService` | Da co Gemini va OpenAI `text-embedding-3-small`. Van chua co provider thuc cho e5/PhoBERT/bge-m3 neu giang vien bat dung cac model tham khao do. |
| Dashboard hien ket qua thuc nghiem | Dat | `Views/Research/Index.cshtml`, `Details.cshtml` | Co bang va bieu do chi so. |
| Test set 50 cau hoi + dap an dung | Dat ve artifact | `Docs/DBA103_TEST_SET_50.md` | Da bo sung artifact. Can chay experiment voi bo cau hoi nay de co so lieu thuc. |
| Bao cao RBL so sanh model | Dat mot phan | `ResearchReportPdfService` | Co xuat PDF tu ket qua experiment; chua co PDF ket qua san neu chua chay benchmark. |
| Bang so lieu RAGAS benchmark | Dat mot phan | `rbl_benchmark_results`, Details UI | Co luu/hien thi sau khi chay; can du lieu run thuc de nop bang so lieu. |
| README GitHub | Dat | `README.md` | README da duoc viet lai theo san pham. |

## Ket luan ky thuat

Project da co khung web app va RAG/RBL kha day du cho demo 1 mon. Phan can than khi nộp:

- Phai chay benchmark voi test set 50 cau de sinh bang RAGAS thuc, khong chi show dashboard rong.
- Neu muon bam sat hon nua de bai ve embedding models, can them provider thuc cho `bge-m3`, `multilingual-e5-base` hoac `PhoBERT-base` qua local service/external endpoint.
- Neu dung fine-tuned baseline, endpoint phai co that va log ket qua trong dashboard.
- Khong nen noi project da "fine-tune model" neu thuc te moi chi benchmark mot fine-tuned endpoint ngoai.
