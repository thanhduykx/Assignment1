const translations = {
  en: {
    "nav.documents": "Documents",
    "nav.chat": "Chat",
    "nav.logout": "Logout",
    "nav.login": "Login",
    "nav.register": "Create account",
    "shell.portal": "Research Portal",
    "shell.dashboard": "Research Dashboard",
    "shell.chatbot": "Chatbot",
    "shell.documents": "Document Repository",
    "shell.config": "Experiment Config",
    "shell.newResearchProject": "New Research Project",
    "shell.help": "Help",
    "shell.search": "Search the system...",
    "shell.notifications": "Notifications",
    "shell.history": "History",
    "research.shellTitle": "EdTech AI Research",
    "research.heroKicker": "Active Research",
    "research.heroTitle": "RAG performance analysis in education",
    "research.heroText": "Track benchmarks by embedding model and chunking strategy on real system data.",
    "research.experiments": "Experiments",
    "research.runs": "Runs",
    "research.avgRagas": "Avg RAGAS",
    "research.dashboard": "RBL Dashboard",
    "research.ragasByExperiment": "RAGAS by experiment",
    "research.create": "Create",
    "research.noExperimentTitle": "No experiments yet",
    "research.noExperimentText": "Create the first experiment to start benchmarking RAG.",
    "research.bestPerformer": "Best performer",
    "research.noScore": "No score yet",
    "research.runBenchmarkHint": "Run a benchmark to get comparison data.",
    "research.detailsLabel": "Experiment details",
    "research.experimentList": "RBL experiment list",
    "research.benchmarkQuestionsUnit": "benchmark questions",
    "research.noData": "No data.",
    "research.createFirstHint": "Click Create to configure embedding model and chunking strategy.",
    "research.open": "Open",
    "detail.shellTitle": "RBL Experiment Detail",
    "detail.back": "Back to experiments",
    "detail.downloadPdf": "Download PDF",
    "detail.runBenchmark": "Run benchmark",
    "detail.status": "Status",
    "detail.averageRagas": "Average RAGAS",
    "detail.bestRun": "Best run",
    "detail.completed": "Completed",
    "detail.ragVsFt": "RAG vs Fine-tuned",
    "detail.averageByRunGroup": "Average score by run group",
    "detail.noScoredRun": "No scored run",
    "detail.runForBaseline": "Run benchmark to get comparison data.",
    "detail.runs": "Runs",
    "detail.runResults": "Results by configuration",
    "detail.testQuestions": "Test questions",
    "detail.evaluationQuestions": "Evaluation question set",
    "config.shellTitle": "Experiment Config",
    "config.back": "Back to dashboard",
    "config.title": "Experiment configuration",
    "config.subtitle": "Set up a RAG benchmark matrix with multiple embedding models and chunking strategies.",
    "config.sessionParams": "New session settings",
    "config.experimentName": "Experiment name",
    "config.subjectFilter": "Subject filter",
    "config.embeddingModels": "Embedding models",
    "config.chunkingStrategies": "Chunking strategies",
    "config.embeddingRunner": "Embedding runner",
    "config.chunkingConfiguration": "Chunking configuration",
    "config.benchmarkQuestions": "Benchmark questions",
    "config.questionsHint": "One question per line. Use | to separate question and ground truth.",
    "config.createExperiment": "Create experiment",
    "config.cancel": "Cancel",
    "config.systemStatus": "System status",
    "config.lifecycle": "Benchmark lifecycle",
    "config.stepPending": "Create run matrix from model x strategy.",
    "config.stepRunning": "Re-chunk, embed, retrieve, generate answer.",
    "config.stepScored": "Save RAGAS metrics to database.",
    "documents.shellTitle": "Document Repository",
    "documents.manageTitle": "Document repository management",
    "documents.manageSubtitle": "Upload learning materials, extract content, and index them so the chatbot can answer with sources.",
    "documents.uploadKicker": "Upload",
    "documents.dropTitle": "Drop documents here",
    "documents.chooseFile": "Choose a file or drag it here",
    "documents.urlRenderHint": "SPA/React/Vue pages will be rendered before DOM extraction.",
    "documents.storage": "Storage",
    "documents.uploadedSize": "Uploaded size",
    "documents.totalUploaded": "Total uploaded",
    "documents.documentList": "Document list",
    "documents.showing": "Showing",
    "documents.fileName": "File name",
    "documents.status": "Status",
    "documents.uploadDate": "Upload date",
    "documents.indexedStatus": "Indexed",
    "documents.emptyTitle": "No documents yet.",
    "documents.emptyHint": "Upload a syllabus or lecture URL to start source-grounded Q&A.",
    "assistant.open": "Open chat page",
    "assistant.hidden": "Hi, I am your chatbot assistant for finding information faster.",
    "documents.title": "Course document repository",
    "documents.subtitle": "Manage uploaded learning materials, extract content, and index them for the dedicated chat page.",
    "documents.openChat": "Open chat",
    "documents.statsAria": "Document repository statistics",
    "documents.statsDocuments": "Documents",
    "documents.statsIndexed": "Indexed",
    "documents.statsProcessing": "Processing",
    "documents.uploadTitle": "Upload document",
    "documents.uploadSubtitle": "PDF, DOCX, PPTX, TXT, or a lecture page URL will be extracted, chunked, and embedded automatically.",
    "documents.subject": "Subject",
    "documents.chapter": "Chapter",
    "documents.source": "Source",
    "documents.subjectPlaceholder": "Example: CODE - Subject name",
    "documents.chapterPlaceholder": "Example: Chapter 1 or Week 1",
    "documents.dropzoneTitle": "Drag and drop a document or click to choose a file",
    "documents.dropzoneDefault": "Supports PDF, DOCX, PPTX, TXT",
    "documents.or": "or",
    "documents.url": "Lecture page URL",
    "documents.urlPlaceholder": "https://example.com/react-vue-lecture",
    "documents.urlHint": "SPA/React/Vue pages will be rendered with Playwright before DOM extraction.",
    "documents.submit": "Upload and index",
    "documents.indexedTitle": "Indexed documents",
    "documents.filesUnit": "files",
    "documents.empty": "No documents yet. Upload course materials to start asking source-grounded questions.",
    "documents.view": "View",
    "chat.sessionsAria": "Chat session history",
    "chat.documents": "Documents",
    "chat.newSession": "New session",
    "chat.history": "Session history",
    "chat.sessionsUnit": "sessions",
    "chat.messagesUnit": "messages",
    "chat.noSessions": "No sessions yet.",
    "chat.mainAria": "Document chat",
    "chat.title": "Document chat",
    "chat.subtitle": "Ask questions based on indexed documents. Questions outside the document scope will be marked as insufficient data.",
    "chat.headerKicker": "Course assistant",
    "chat.headerTitle": "Introduction to AI",
    "chat.headerSubtitle": "Ask questions from indexed documents. If the data is insufficient, the chatbot must report missing sources instead of guessing.",
    "chat.currentSession": "Current session",
    "chat.emptyTitle": "Start with a specific question",
    "chat.emptyText": "Choose a suggestion below or type your question. If the documents do not contain enough data, the chatbot will say so instead of guessing.",
    "chat.suggestionsAria": "Question suggestions",
    "chat.welcome": "You can ask about uploaded document content. I will answer concisely when there is enough data.",
    "chat.placeholder": "Ask about a subject, chapter, or indexed document...",
    "chat.send": "Send",
    "chat.relatedLabel": "Related questions",
    "chat.relatedAria": "Related questions",
    "chat.defaultSessionTitle": "Session without a question",
    "chat.sessionActions": "Session actions",
    "chat.starSession": "Star",
    "chat.unstarSession": "Unstar",
    "chat.renameSession": "Rename",
    "chat.deleteSession": "Delete",
    "chat.renamePrompt": "New session name",
    "chat.deleteConfirm": "Delete this chat session?",
    "chat.sessionActionError": "Could not update the chat session.",
    "chat.loading": "Searching the documents...",
    "chat.requestError": "Could not process the question.",
    "chat.connectionError": "Could not connect to the server. Check the app and try again.",
    "chat.suggestions": [
      "Which subjects have indexed documents?",
      "Summarize the uploaded documents.",
      "What can I ask from the document repository?",
      "Which document should I read first?"
    ]
  },
  vi: {
    "nav.documents": "T\u00e0i li\u1ec7u",
    "nav.chat": "H\u1ecfi \u0111\u00e1p",
    "nav.logout": "\u0110\u0103ng xu\u1ea5t",
    "nav.login": "\u0110\u0103ng nh\u1eadp",
    "nav.register": "T\u1ea1o t\u00e0i kho\u1ea3n",
    "shell.portal": "C\u1ed5ng nghi\u00ean c\u1ee9u",
    "shell.dashboard": "Dashboard nghi\u00ean c\u1ee9u",
    "shell.chatbot": "Chatbot",
    "shell.documents": "Kho t\u00e0i li\u1ec7u",
    "shell.config": "C\u1ea5u h\u00ecnh th\u1ef1c nghi\u1ec7m",
    "shell.newResearchProject": "D\u1ef1 \u00e1n nghi\u00ean c\u1ee9u m\u1edbi",
    "shell.help": "Tr\u1ee3 gi\u00fap",
    "shell.search": "T\u00ecm ki\u1ebfm trong h\u1ec7 th\u1ed1ng...",
    "shell.notifications": "Th\u00f4ng b\u00e1o",
    "shell.history": "L\u1ecbch s\u1eed",
    "research.shellTitle": "EdTech AI Research",
    "research.heroKicker": "Nghi\u00ean c\u1ee9u \u0111ang ho\u1ea1t \u0111\u1ed9ng",
    "research.heroTitle": "Ph\u00e2n t\u00edch hi\u1ec7u n\u0103ng RAG trong gi\u00e1o d\u1ee5c",
    "research.heroText": "Theo d\u00f5i benchmark theo embedding model v\u00e0 chunking strategy tr\u00ean d\u1eef li\u1ec7u th\u1eadt c\u1ee7a h\u1ec7 th\u1ed1ng.",
    "research.experiments": "Th\u1ef1c nghi\u1ec7m",
    "research.runs": "Runs",
    "research.avgRagas": "Avg RAGAS",
    "research.dashboard": "RBL Dashboard",
    "research.ragasByExperiment": "RAGAS theo th\u1ef1c nghi\u1ec7m",
    "research.create": "T\u1ea1o m\u1edbi",
    "research.noExperimentTitle": "Ch\u01b0a c\u00f3 th\u1ef1c nghi\u1ec7m",
    "research.noExperimentText": "T\u1ea1o experiment \u0111\u1ea7u ti\u00ean \u0111\u1ec3 b\u1eaft \u0111\u1ea7u benchmark RAG.",
    "research.bestPerformer": "C\u1ea5u h\u00ecnh t\u1ed1t nh\u1ea5t",
    "research.noScore": "Ch\u01b0a c\u00f3 \u0111i\u1ec3m",
    "research.runBenchmarkHint": "Ch\u1ea1y benchmark \u0111\u1ec3 c\u00f3 d\u1eef li\u1ec7u so s\u00e1nh.",
    "research.detailsLabel": "Chi ti\u1ebft th\u1ef1c nghi\u1ec7m",
    "research.experimentList": "Danh s\u00e1ch RBL experiments",
    "research.benchmarkQuestionsUnit": "c\u00e2u h\u1ecfi benchmark",
    "research.noData": "Kh\u00f4ng c\u00f3 d\u1eef li\u1ec7u.",
    "research.createFirstHint": "Nh\u1ea5n T\u1ea1o m\u1edbi \u0111\u1ec3 c\u1ea5u h\u00ecnh embedding model v\u00e0 chunking strategy.",
    "research.open": "M\u1edf",
    "detail.shellTitle": "Chi ti\u1ebft th\u1ef1c nghi\u1ec7m RBL",
    "detail.back": "Quay l\u1ea1i danh s\u00e1ch th\u1ef1c nghi\u1ec7m",
    "detail.downloadPdf": "T\u1ea3i PDF",
    "detail.runBenchmark": "Ch\u1ea1y benchmark",
    "detail.status": "Tr\u1ea1ng th\u00e1i",
    "detail.averageRagas": "RAGAS trung b\u00ecnh",
    "detail.bestRun": "Run t\u1ed1t nh\u1ea5t",
    "detail.completed": "Ho\u00e0n t\u1ea5t",
    "detail.ragVsFt": "RAG vs Fine-tuned",
    "detail.averageByRunGroup": "\u0110i\u1ec3m trung b\u00ecnh theo nh\u00f3m run",
    "detail.noScoredRun": "Ch\u01b0a c\u00f3 run \u0111\u01b0\u1ee3c ch\u1ea5m",
    "detail.runForBaseline": "Ch\u1ea1y benchmark \u0111\u1ec3 c\u00f3 d\u1eef li\u1ec7u so s\u00e1nh.",
    "detail.runs": "Runs",
    "detail.runResults": "K\u1ebft qu\u1ea3 t\u1eebng c\u1ea5u h\u00ecnh",
    "detail.testQuestions": "C\u00e2u h\u1ecfi test",
    "detail.evaluationQuestions": "B\u1ed9 c\u00e2u h\u1ecfi \u0111\u00e1nh gi\u00e1",
    "config.shellTitle": "C\u1ea5u h\u00ecnh th\u1ef1c nghi\u1ec7m",
    "config.back": "Quay l\u1ea1i dashboard",
    "config.title": "Tr\u00ecnh c\u1ea5u h\u00ecnh th\u1ef1c nghi\u1ec7m",
    "config.subtitle": "Thi\u1ebft l\u1eadp ma tr\u1eadn benchmark RAG theo nhi\u1ec1u embedding model v\u00e0 chunking strategy.",
    "config.sessionParams": "Th\u00f4ng s\u1ed1 phi\u00ean m\u1edbi",
    "config.experimentName": "T\u00ean th\u1ef1c nghi\u1ec7m",
    "config.subjectFilter": "B\u1ed9 l\u1ecdc m\u00f4n h\u1ecdc",
    "config.embeddingModels": "M\u00f4 h\u00ecnh Embedding",
    "config.chunkingStrategies": "Chi\u1ebfn l\u01b0\u1ee3c chunking",
    "config.embeddingRunner": "B\u1ed9 ch\u1ea1y embedding",
    "config.chunkingConfiguration": "C\u1ea5u h\u00ecnh chunking",
    "config.benchmarkQuestions": "C\u00e2u h\u1ecfi benchmark",
    "config.questionsHint": "M\u1ed7i d\u00f2ng m\u1ed9t c\u00e2u h\u1ecfi. D\u00f9ng d\u1ea5u | \u0111\u1ec3 t\u00e1ch c\u00e2u h\u1ecfi v\u00e0 ground truth.",
    "config.createExperiment": "T\u1ea1o th\u1ef1c nghi\u1ec7m",
    "config.cancel": "H\u1ee7y",
    "config.systemStatus": "Tr\u1ea1ng th\u00e1i h\u1ec7 th\u1ed1ng",
    "config.lifecycle": "V\u00f2ng \u0111\u1eddi benchmark",
    "config.stepPending": "T\u1ea1o run matrix t\u1eeb model x strategy.",
    "config.stepRunning": "Re-chunk, embed, retrieve, generate answer.",
    "config.stepScored": "L\u01b0u RAGAS metrics v\u00e0o database.",
    "documents.shellTitle": "Kho t\u00e0i li\u1ec7u",
    "documents.manageTitle": "Qu\u1ea3n l\u00fd kho t\u00e0i li\u1ec7u",
    "documents.manageSubtitle": "Upload t\u00e0i li\u1ec7u h\u1ecdc t\u1eadp, tr\u00edch xu\u1ea5t n\u1ed9i dung v\u00e0 index \u0111\u1ec3 chatbot tr\u1ea3 l\u1eddi c\u00f3 ngu\u1ed3n.",
    "documents.uploadKicker": "Upload",
    "documents.dropTitle": "K\u00e9o th\u1ea3 t\u00e0i li\u1ec7u v\u00e0o \u0111\u00e2y",
    "documents.chooseFile": "Ch\u1ecdn t\u1ec7p ho\u1eb7c k\u00e9o th\u1ea3 v\u00e0o \u0111\u00e2y",
    "documents.urlRenderHint": "Trang SPA/React/Vue s\u1ebd \u0111\u01b0\u1ee3c render tr\u01b0\u1edbc khi tr\u00edch xu\u1ea5t DOM.",
    "documents.storage": "L\u01b0u tr\u1eef",
    "documents.uploadedSize": "Dung l\u01b0\u1ee3ng \u0111\u00e3 upload",
    "documents.totalUploaded": "T\u1ed5ng \u0111\u00e3 upload",
    "documents.documentList": "Danh s\u00e1ch t\u00e0i li\u1ec7u",
    "documents.showing": "Hi\u1ec3n th\u1ecb",
    "documents.fileName": "T\u00ean file",
    "documents.status": "Tr\u1ea1ng th\u00e1i",
    "documents.uploadDate": "Ng\u00e0y upload",
    "documents.indexedStatus": "\u0110\u00e3 Index",
    "documents.emptyTitle": "Ch\u01b0a c\u00f3 t\u00e0i li\u1ec7u.",
    "documents.emptyHint": "Upload gi\u00e1o tr\u00ecnh ho\u1eb7c URL b\u00e0i gi\u1ea3ng \u0111\u1ec3 b\u1eaft \u0111\u1ea7u h\u1ecfi \u0111\u00e1p theo ngu\u1ed3n.",
    "assistant.open": "M\u1edf trang chat",
    "assistant.hidden": "Ch\u00e0o b\u1ea1n, m\u00ecnh l\u00e0 chatbot h\u1ed7 tr\u1ee3 t\u00ecm ki\u1ebfm th\u00f4ng tin nhanh h\u01a1n.",
    "documents.title": "Kho t\u00e0i li\u1ec7u m\u00f4n h\u1ecdc",
    "documents.subtitle": "Qu\u1ea3n l\u00fd t\u00e0i li\u1ec7u \u0111\u00e3 upload, tr\u00edch xu\u1ea5t n\u1ed9i dung v\u00e0 l\u1eadp ch\u1ec9 m\u1ee5c cho trang h\u1ecfi \u0111\u00e1p ri\u00eang.",
    "documents.openChat": "M\u1edf trang chat",
    "documents.statsAria": "Th\u1ed1ng k\u00ea kho t\u00e0i li\u1ec7u",
    "documents.statsDocuments": "T\u00e0i li\u1ec7u",
    "documents.statsIndexed": "\u0110\u00e3 index",
    "documents.statsProcessing": "\u0110ang x\u1eed l\u00fd",
    "documents.uploadTitle": "Upload t\u00e0i li\u1ec7u",
    "documents.uploadSubtitle": "PDF, DOCX, PPTX, TXT ho\u1eb7c URL trang b\u00e0i gi\u1ea3ng s\u1ebd \u0111\u01b0\u1ee3c tr\u00edch xu\u1ea5t, chunk v\u00e0 embed t\u1ef1 \u0111\u1ed9ng.",
    "documents.subject": "M\u00f4n h\u1ecdc",
    "documents.chapter": "Ch\u01b0\u01a1ng",
    "documents.source": "Ngu\u1ed3n",
    "documents.subjectPlaceholder": "VD: M\u00e3 m\u00f4n - T\u00ean m\u00f4n",
    "documents.chapterPlaceholder": "VD: Ch\u01b0\u01a1ng 1 ho\u1eb7c Tu\u1ea7n 1",
    "documents.dropzoneTitle": "K\u00e9o th\u1ea3 t\u00e0i li\u1ec7u ho\u1eb7c b\u1ea5m \u0111\u1ec3 ch\u1ecdn file",
    "documents.dropzoneDefault": "H\u1ed7 tr\u1ee3 PDF, DOCX, PPTX, TXT",
    "documents.or": "ho\u1eb7c",
    "documents.url": "URL trang b\u00e0i gi\u1ea3ng",
    "documents.urlPlaceholder": "https://example.com/bai-giang-react-vue",
    "documents.urlHint": "Trang SPA/React/Vue s\u1ebd \u0111\u01b0\u1ee3c render b\u1eb1ng Playwright tr\u01b0\u1edbc khi tr\u00edch xu\u1ea5t DOM.",
    "documents.submit": "T\u1ea3i l\u00ean v\u00e0 index",
    "documents.indexedTitle": "T\u00e0i li\u1ec7u \u0111\u00e3 l\u1eadp ch\u1ec9 m\u1ee5c",
    "documents.filesUnit": "file",
    "documents.empty": "Ch\u01b0a c\u00f3 t\u00e0i li\u1ec7u n\u00e0o. H\u00e3y t\u1ea3i t\u00e0i li\u1ec7u m\u00f4n h\u1ecdc \u0111\u1ec3 b\u1eaft \u0111\u1ea7u h\u1ecfi \u0111\u00e1p theo ngu\u1ed3n.",
    "documents.view": "Xem",
    "chat.sessionsAria": "L\u1ecbch s\u1eed phi\u00ean chat",
    "chat.documents": "Kho t\u00e0i li\u1ec7u",
    "chat.newSession": "Phi\u00ean m\u1edbi",
    "chat.history": "L\u1ecbch s\u1eed phi\u00ean",
    "chat.sessionsUnit": "phi\u00ean",
    "chat.messagesUnit": "tin",
    "chat.noSessions": "Ch\u01b0a c\u00f3 phi\u00ean n\u00e0o.",
    "chat.mainAria": "Chat theo t\u00e0i li\u1ec7u",
    "chat.title": "Chat t\u00e0i li\u1ec7u",
    "chat.subtitle": "H\u1ecfi theo kho t\u00e0i li\u1ec7u \u0111\u00e3 index. C\u00e2u h\u1ecfi ngo\u00e0i ph\u1ea1m vi t\u00e0i li\u1ec7u s\u1ebd \u0111\u01b0\u1ee3c b\u00e1o kh\u00f4ng \u0111\u1ee7 d\u1eef li\u1ec7u.",
    "chat.headerKicker": "Tr\u1ee3 l\u00fd m\u00f4n h\u1ecdc",
    "chat.headerTitle": "Nh\u1eadp m\u00f4n AI",
    "chat.headerSubtitle": "H\u1ecfi \u0111\u00e1p d\u1ef1a tr\u00ean t\u00e0i li\u1ec7u \u0111\u00e3 index. N\u1ebfu d\u1eef li\u1ec7u kh\u00f4ng \u0111\u1ee7, chatbot ph\u1ea3i b\u00e1o thi\u1ebfu ngu\u1ed3n thay v\u00ec \u0111o\u00e1n.",
    "chat.currentSession": "Phi\u00ean hi\u1ec7n t\u1ea1i",
    "chat.emptyTitle": "B\u1eaft \u0111\u1ea7u b\u1eb1ng m\u1ed9t c\u00e2u h\u1ecfi c\u1ee5 th\u1ec3",
    "chat.emptyText": "Ch\u1ecdn g\u1ee3i \u00fd b\u00ean d\u01b0\u1edbi ho\u1eb7c nh\u1eadp c\u00e2u h\u1ecfi c\u1ee7a b\u1ea1n. N\u1ebfu t\u00e0i li\u1ec7u kh\u00f4ng \u0111\u1ee7 d\u1eef li\u1ec7u, chatbot s\u1ebd b\u00e1o r\u00f5 thay v\u00ec \u0111o\u00e1n.",
    "chat.suggestionsAria": "G\u1ee3i \u00fd c\u00e2u h\u1ecfi",
    "chat.welcome": "B\u1ea1n c\u00f3 th\u1ec3 h\u1ecfi v\u1ec1 n\u1ed9i dung \u0111\u00e3 upload. M\u00ecnh s\u1ebd tr\u1ea3 l\u1eddi ng\u1eafn g\u1ecdn khi c\u00f3 \u0111\u1ee7 d\u1eef li\u1ec7u.",
    "chat.placeholder": "H\u1ecfi v\u1ec1 m\u00f4n, ch\u01b0\u01a1ng ho\u1eb7c t\u00e0i li\u1ec7u \u0111\u00e3 index...",
    "chat.send": "G\u1eedi",
    "chat.relatedLabel": "C\u00e2u h\u1ecfi li\u00ean quan",
    "chat.relatedAria": "C\u00e2u h\u1ecfi li\u00ean quan",
    "chat.defaultSessionTitle": "Phi\u00ean ch\u01b0a c\u00f3 c\u00e2u h\u1ecfi",
    "chat.sessionActions": "Thao t\u00e1c phi\u00ean",
    "chat.starSession": "Ghim",
    "chat.unstarSession": "B\u1ecf ghim",
    "chat.renameSession": "\u0110\u1ed5i t\u00ean",
    "chat.deleteSession": "X\u00f3a",
    "chat.renamePrompt": "T\u00ean phi\u00ean m\u1edbi",
    "chat.deleteConfirm": "X\u00f3a phi\u00ean chat n\u00e0y?",
    "chat.sessionActionError": "Kh\u00f4ng c\u1eadp nh\u1eadt \u0111\u01b0\u1ee3c phi\u00ean chat.",
    "chat.loading": "\u0110ang t\u00ecm trong t\u00e0i li\u1ec7u...",
    "chat.requestError": "Kh\u00f4ng x\u1eed l\u00fd \u0111\u01b0\u1ee3c c\u00e2u h\u1ecfi.",
    "chat.connectionError": "Kh\u00f4ng k\u1ebft n\u1ed1i \u0111\u01b0\u1ee3c server. Ki\u1ec3m tra l\u1ea1i \u1ee9ng d\u1ee5ng r\u1ed3i th\u1eed ti\u1ebfp.",
    "chat.suggestions": [
      "Hi\u1ec7n c\u00f3 nh\u1eefng m\u00f4n n\u00e0o \u0111\u00e3 index t\u00e0i li\u1ec7u?",
      "T\u00f3m t\u1eaft c\u00e1c t\u00e0i li\u1ec7u \u0111\u00e3 upload.",
      "T\u00f4i c\u00f3 th\u1ec3 h\u1ecfi g\u00ec t\u1eeb kho t\u00e0i li\u1ec7u?",
      "N\u00ean \u0111\u1ecdc t\u00e0i li\u1ec7u n\u00e0o tr\u01b0\u1edbc?"
    ]
  }
};

const languageKey = "courseAssistantLanguage";
const chatPage = document.querySelector(".rbl-chat-page");
const chatForm = document.getElementById("chatForm");
const questionInput = document.getElementById("questionInput");
const chatMessages = document.getElementById("chatMessages");
const newSessionButton = document.getElementById("newSessionButton");
const chatSessionList = document.getElementById("chatSessionList");
const activeSessionTitle = document.getElementById("activeSessionTitle");
const documentDropzone = document.getElementById("documentDropzone");
const documentFileInput = document.getElementById("documentFileInput");
const documentFileName = document.getElementById("documentFileName");
const documentPreviewModal = document.getElementById("documentPreviewModal");
const documentPreviewTitle = document.getElementById("documentPreviewTitle");
const documentPreviewMeta = document.getElementById("documentPreviewMeta");
const documentPreviewBody = document.getElementById("documentPreviewBody");
const assistantLauncher = document.getElementById("chatbotHelper");
const assistantLauncherButton = document.getElementById("chatbotHelperButton");
let isSending = false;
const subjectQuestionSubjects = readSubjectQuestionSubjects();
const relatedQuestionPool = readRelatedQuestionPool();

function getLanguage() {
  return localStorage.getItem(languageKey) === "en" ? "en" : "vi";
}

function t(key) {
  return translations[getLanguage()][key] || translations.en[key] || key;
}

function readJsonDataAttribute(element, key, fallback) {
  if (!element?.dataset?.[key]) {
    return fallback;
  }

  try {
    return JSON.parse(element.dataset[key]);
  } catch {
    return fallback;
  }
}

function buildDefaultSuggestionItems() {
  const enSuggestions = readJsonDataAttribute(chatPage, "chatSuggestionsEn", translations.en["chat.suggestions"]);
  const viSuggestions = readJsonDataAttribute(chatPage, "chatSuggestionsVi", translations.vi["chat.suggestions"]);
  const total = Math.max(enSuggestions.length, viSuggestions.length);

  return Array.from({ length: total }, (_, index) => ({
    id: `default-${index}`,
    en: enSuggestions[index] || viSuggestions[index] || "",
    vi: viSuggestions[index] || enSuggestions[index] || ""
  })).filter((item) => item.en || item.vi);
}

function getChatSuggestionItems() {
  const selectedSubject = getSelectedSubjectFilter();
  if (selectedSubject) {
    return buildSubjectQuestionItems(selectedSubject);
  }

  return dedupeQuestionItems([
    ...buildAllSubjectQuestionItems(),
    ...relatedQuestionPool,
    ...buildDefaultSuggestionItems()
  ]);
}

function getChatSuggestions(language = getLanguage()) {
  const asked = readAskedQuestions();
  const basePool = getChatSuggestionItems();
  return getAvailableQuestionItems(basePool, asked, getSelectedSubjectFilter())
    .slice(0, 6)
    .map((item) => language === "vi" ? item.vi : item.en)
    .filter(Boolean);
}

function readSubjectQuestionSubjects() {
  const fromPayload = readJsonDataAttribute(chatPage, "chatSubjectSuggestions", [])
    .map((item) => item?.subject || item?.vi || item?.en || "")
    .filter(Boolean);
  const fromChips = [...document.querySelectorAll(".chat-subject-chip")]
    .map((button) => button.dataset.subjectFilter || "")
    .filter(Boolean);

  return [...new Set([...fromPayload, ...fromChips].map((subject) => subject.trim()).filter(Boolean))];
}

function dedupeQuestionItems(items) {
  const seen = new Set();
  const result = [];
  for (const item of items) {
    if (!item?.en || !item?.vi) {
      continue;
    }

    const key = `${normalizeQuestionForMemory(item.en)}|${normalizeQuestionForMemory(item.vi)}`;
    if (seen.has(key)) {
      continue;
    }

    seen.add(key);
    result.push(item);
  }

  return result;
}

function getAvailableQuestionItems(basePool, asked, selectedSubject = "") {
  const available = dedupeQuestionItems(basePool).filter((item) => !questionWasAsked(item, asked));
  if (available.length > 0) {
    return available;
  }

  const recoveryPool = selectedSubject
    ? buildRecoveryQuestionItems(selectedSubject)
    : subjectQuestionSubjects.flatMap((subject) => buildRecoveryQuestionItems(subject));
  return dedupeQuestionItems(recoveryPool).filter((item) => !questionWasAsked(item, asked));
}

function readRelatedQuestionPool() {
  const items = readJsonDataAttribute(chatPage, "chatRelatedQuestions", []);
  if (Array.isArray(items) && items.length > 0) {
    return items
      .filter((item) => item?.en && item?.vi)
      .map((item, index) => ({
        id: item.id || `subject-${index}`,
        subject: item.subject || "",
        en: item.en,
        vi: item.vi
      }));
  }

  return [
    {
      id: "available-subjects",
      subject: "",
      en: "Which subjects have indexed documents?",
      vi: "Hiện có những môn nào đã index tài liệu?"
    },
    {
      id: "summarize-documents",
      subject: "",
      en: "Summarize the uploaded documents.",
      vi: "Tóm tắt các tài liệu đã upload."
    },
    {
      id: "askable-content",
      subject: "",
      en: "What can I ask from the document repository?",
      vi: "Tôi có thể hỏi gì từ kho tài liệu?"
    }
  ];
}

async function ensureVietnameseFontReady() {
  if (!document.fonts?.load) {
    return;
  }

  await Promise.all([
    document.fonts.load('400 16px "Be Vietnam Pro"'),
    document.fonts.load('600 16px "Be Vietnam Pro"'),
    document.fonts.load('800 16px "Be Vietnam Pro"')
  ]);
}

async function setLanguage(language) {
  const nextLanguage = language === "vi" ? "vi" : "en";
  if (getLanguage() === nextLanguage) {
    return;
  }

  try {
    if (nextLanguage === "vi") {
      await ensureVietnameseFontReady();
    }

    localStorage.setItem(languageKey, nextLanguage);
    document.documentElement.classList.add("is-language-changing");
    applyLanguage();
  } finally {
    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        document.documentElement.classList.remove("is-language-changing");
      });
    });
  }
}

function applyLanguage() {
  const language = getLanguage();
  document.documentElement.lang = language === "vi" ? "vi" : "en";

  document.querySelectorAll("[data-i18n]").forEach((element) => {
    element.textContent = translations[language][element.dataset.i18n] || translations.en[element.dataset.i18n] || element.textContent;
  });

  document.querySelectorAll("[data-i18n-placeholder]").forEach((element) => {
    element.placeholder = translations[language][element.dataset.i18nPlaceholder] || translations.en[element.dataset.i18nPlaceholder] || element.placeholder;
  });

  document.querySelectorAll("[data-i18n-aria-label]").forEach((element) => {
    element.setAttribute("aria-label", translations[language][element.dataset.i18nAriaLabel] || translations.en[element.dataset.i18nAriaLabel] || element.getAttribute("aria-label"));
  });

  document.querySelectorAll("[data-i18n-title]").forEach((element) => {
    element.setAttribute("title", translations[language][element.dataset.i18nTitle] || translations.en[element.dataset.i18nTitle] || element.getAttribute("title"));
  });

  document.querySelectorAll("[data-language-option]").forEach((button) => {
    const isActive = button.dataset.languageOption === language;
    button.classList.toggle("is-active", isActive);
    button.setAttribute("aria-pressed", String(isActive));
  });

  document.querySelectorAll("[data-language-field]").forEach((field) => {
    field.value = language;
  });

  document.querySelectorAll("[data-assistant-greeting]").forEach((element) => {
    const name = element.dataset.assistantName || "you";
    element.textContent = language === "vi"
      ? `Chào ${name}, mình sẽ hỗ trợ bạn tìm kiếm thông tin nhanh hơn.`
      : `Hi ${name}, I can help you find information faster.`;
  });

  updateSuggestionButtons();
  updateRelatedQuestionButtons();
  updateDropzoneDefaultText();
  document.documentElement.classList.remove("i18n-pending");
  document.documentElement.classList.add("i18n-ready");
}

function updateSuggestionButtons() {
  const language = getLanguage();
  const suggestions = getChatSuggestions(language);
  document.querySelectorAll(".suggestion-chip").forEach((button, index) => {
    const question = suggestions[index];
    if (!question) {
      button.hidden = true;
      button.dataset.question = "";
      return;
    }

    button.hidden = false;
    button.textContent = question;
    button.dataset.question = question;
  });
}

function updateRelatedQuestionButtons() {
  renderRelatedQuestions();
}

function getSelectedSubjectFilter() {
  return document.querySelector(".chat-subject-chip.is-active")?.dataset.subjectFilter || "";
}

function setSelectedSubjectFilter(subject) {
  const normalizedSubject = subject || "";
  document.querySelectorAll(".chat-subject-chip").forEach((button) => {
    const isActive = (button.dataset.subjectFilter || "") === normalizedSubject;
    button.classList.toggle("is-active", isActive);
    button.setAttribute("aria-pressed", String(isActive));
  });
  updateSuggestionButtons();
  renderRelatedQuestions();
}

function bindSubjectFilterChips() {
  document.querySelectorAll(".chat-subject-chip").forEach((button) => {
    button.addEventListener("click", () => {
      setSelectedSubjectFilter(button.dataset.subjectFilter || "");
      questionInput?.focus();
    });
  });
}

function bindSubjectSuggestionButtons() {
  document.querySelectorAll("[data-subject-suggestion]").forEach((button) => {
    if (button.dataset.subjectSuggestionBound === "true") {
      return;
    }

    button.dataset.subjectSuggestionBound = "true";
    button.addEventListener("click", () => {
      setSelectedSubjectFilter(button.dataset.subjectSuggestion || "");
      questionInput?.focus();
    });
  });
}

function normalizeQuestionForMemory(question) {
  return (question || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .trim();
}

function getAskedQuestionKey() {
  return `ragChatAskedQuestions:${getSessionId()}`;
}

function readAskedQuestions() {
  try {
    return new Set(JSON.parse(localStorage.getItem(getAskedQuestionKey()) || "[]"));
  } catch {
    return new Set();
  }
}

function rememberAskedQuestion(question) {
  const normalized = normalizeQuestionForMemory(question);
  if (!normalized) {
    return;
  }

  const asked = readAskedQuestions();
  asked.add(normalized);
  localStorage.setItem(getAskedQuestionKey(), JSON.stringify([...asked].slice(-80)));
}

function collectVisibleUserQuestions() {
  document.querySelectorAll(".message.user .bubble").forEach((bubble) => {
    rememberAskedQuestion(bubble.textContent || "");
  });
}

function getRelatedRotationIndex() {
  return Number(sessionStorage.getItem(`ragChatRelatedRotation:${getSessionId()}`) || "0");
}

function advanceRelatedRotation() {
  const key = `ragChatRelatedRotation:${getSessionId()}`;
  sessionStorage.setItem(key, String(getRelatedRotationIndex() + 1));
}

function questionWasAsked(item, asked) {
  return asked.has(normalizeQuestionForMemory(item.en))
    || asked.has(normalizeQuestionForMemory(item.vi));
}

function normalizeSubjectForCompare(subject) {
  return normalizeQuestionForMemory(subject);
}

function extractCourseCode(value) {
  const match = String(value || "").match(/\b[A-Za-z]{2,}\d{2,}\b/);
  return match ? match[0].toUpperCase() : "";
}

function questionItemMatchesSubject(item, subject) {
  const selectedCode = extractCourseCode(subject);
  const itemCode = extractCourseCode(item?.subject || `${item?.en || ""} ${item?.vi || ""}`);
  if (selectedCode && itemCode) {
    return selectedCode === itemCode;
  }

  const normalizedSubject = normalizeSubjectForCompare(subject);
  const normalizedItemSubject = normalizeSubjectForCompare(item?.subject || "");
  return normalizedSubject
    && normalizedItemSubject
    && (normalizedItemSubject === normalizedSubject
      || normalizedItemSubject.includes(normalizedSubject)
      || normalizedSubject.includes(normalizedItemSubject));
}

function getKnownQuestionItemsForSubject(subject) {
  return relatedQuestionPool.filter((item) => questionItemMatchesSubject(item, subject));
}

function buildSubjectQuestionItems(subject) {
  const trimmedSubject = (subject || "").trim();
  if (!trimmedSubject) {
    return [];
  }

  const knownItems = getKnownQuestionItemsForSubject(trimmedSubject);
  if (knownItems.length > 0) {
    return knownItems;
  }

  return [
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-credits`,
      en: `How many credits does ${trimmedSubject} have?`,
      vi: `${trimmedSubject} c\u00f3 bao nhi\u00eau t\u00edn ch\u1ec9?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-about`,
      en: `What is ${trimmedSubject} about?`,
      vi: `${trimmedSubject} l\u00e0 m\u00f4n g\u00ec?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-contents`,
      en: `What are the main contents of ${trimmedSubject}?`,
      vi: `N\u1ed9i dung ch\u00ednh c\u1ee7a ${trimmedSubject} g\u1ed3m nh\u1eefng g\u00ec?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-assessment`,
      en: `How is ${trimmedSubject} assessed?`,
      vi: `${trimmedSubject} \u0111\u01b0\u1ee3c \u0111\u00e1nh gi\u00e1 nh\u01b0 th\u1ebf n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-outcomes`,
      en: `What learning outcomes does ${trimmedSubject} mention?`,
      vi: `${trimmedSubject} c\u00f3 chu\u1ea9n \u0111\u1ea7u ra n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-materials`,
      en: `What materials or resources are used in ${trimmedSubject}?`,
      vi: `${trimmedSubject} d\u00f9ng t\u00e0i li\u1ec7u ho\u1eb7c ngu\u1ed3n h\u1ecdc n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-student-tasks`,
      en: `What does the syllabus say students need to do in ${trimmedSubject}?`,
      vi: `Sinh vi\u00ean c\u1ea7n l\u00e0m g\u00ec trong ${trimmedSubject}?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-exams`,
      en: `What exam or assessment percentages are listed for ${trimmedSubject}?`,
      vi: `${trimmedSubject} c\u00f3 t\u1ef7 l\u1ec7 thi ho\u1eb7c \u0111\u00e1nh gi\u00e1 n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-chapters`,
      en: `Which chapters or sections are indexed for ${trimmedSubject}?`,
      vi: `${trimmedSubject} \u0111\u00e3 index nh\u1eefng ch\u01b0\u01a1ng ho\u1eb7c ph\u1ea7n n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-objectives`,
      en: `What are the objectives of ${trimmedSubject}?`,
      vi: `M\u1ee5c ti\u00eau c\u1ee7a ${trimmedSubject} l\u00e0 g\u00ec?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-prerequisites`,
      en: `Does ${trimmedSubject} mention any prerequisites?`,
      vi: `${trimmedSubject} c\u00f3 y\u00eau c\u1ea7u ti\u00ean quy\u1ebft n\u00e0o kh\u00f4ng?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-schedule`,
      en: `What study schedule or weekly plan is listed for ${trimmedSubject}?`,
      vi: `${trimmedSubject} c\u00f3 l\u1ecbch h\u1ecdc ho\u1eb7c k\u1ebf ho\u1ea1ch tu\u1ea7n n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-activities`,
      en: `What learning activities are mentioned in ${trimmedSubject}?`,
      vi: `${trimmedSubject} c\u00f3 nh\u1eefng ho\u1ea1t \u0111\u1ed9ng h\u1ecdc t\u1eadp n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-tools`,
      en: `What tools or platforms are used in ${trimmedSubject}?`,
      vi: `${trimmedSubject} s\u1eed d\u1ee5ng c\u00f4ng c\u1ee5 ho\u1eb7c n\u1ec1n t\u1ea3ng n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-completion`,
      en: `What completion criteria are listed for ${trimmedSubject}?`,
      vi: `${trimmedSubject} c\u00f3 ti\u00eau ch\u00ed ho\u00e0n th\u00e0nh n\u00e0o?`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-summary`,
      en: `Summarize the indexed syllabus for ${trimmedSubject}.`,
      vi: `T\u00f3m t\u1eaft syllabus \u0111\u00e3 index c\u1ee7a ${trimmedSubject}.`
    },
    {
      id: `${normalizeSubjectForCompare(trimmedSubject)}-important-notes`,
      en: `What important notes should students remember for ${trimmedSubject}?`,
      vi: `Sinh vi\u00ean c\u1ea7n l\u01b0u \u00fd g\u00ec khi h\u1ecdc ${trimmedSubject}?`
    }
  ];
}

function buildAllSubjectQuestionItems() {
  return subjectQuestionSubjects.flatMap((subject) => buildSubjectQuestionItems(subject));
}

function buildRecoveryQuestionItems(subject) {
  const trimmedSubject = (subject || "").trim();
  if (!trimmedSubject) {
    return [];
  }

  const subjectKey = normalizeSubjectForCompare(trimmedSubject);
  return [
    {
      id: `${subjectKey}-recovery-teacher-expectations`,
      en: `What does the lecturer expect students to prepare for ${trimmedSubject}?`,
      vi: `Gi\u1ea3ng vi\u00ean y\u00eau c\u1ea7u sinh vi\u00ean chu\u1ea9n b\u1ecb g\u00ec cho ${trimmedSubject}?`
    },
    {
      id: `${subjectKey}-recovery-output-products`,
      en: `What products, assignments, or submissions are required in ${trimmedSubject}?`,
      vi: `${trimmedSubject} y\u00eau c\u1ea7u b\u00e0i t\u1eadp, s\u1ea3n ph\u1ea9m ho\u1eb7c b\u00e0i n\u1ed9p n\u00e0o?`
    },
    {
      id: `${subjectKey}-recovery-study-resources`,
      en: `Which links, files, or learning resources are mentioned for ${trimmedSubject}?`,
      vi: `${trimmedSubject} c\u00f3 link, file ho\u1eb7c ngu\u1ed3n h\u1ecdc n\u00e0o \u0111\u01b0\u1ee3c nh\u1eafc \u0111\u1ebfn?`
    },
    {
      id: `${subjectKey}-recovery-grading-guide`,
      en: `What grading guide or rubrics are mentioned for ${trimmedSubject}?`,
      vi: `${trimmedSubject} c\u00f3 h\u01b0\u1edbng d\u1eabn ch\u1ea5m \u0111i\u1ec3m ho\u1eb7c rubric n\u00e0o?`
    },
    {
      id: `${subjectKey}-recovery-first-read`,
      en: `What should I read first in the indexed material for ${trimmedSubject}?`,
      vi: `N\u00ean \u0111\u1ecdc ph\u1ea7n n\u00e0o tr\u01b0\u1edbc trong t\u00e0i li\u1ec7u \u0111\u00e3 index c\u1ee7a ${trimmedSubject}?`
    }
  ];
}

function updateRelatedQuestionsLabel(selectedSubject) {
  const label = document.querySelector(".chat-related-strip > span");
  if (!label) {
    return;
  }

  label.textContent = selectedSubject
    ? t("chat.relatedLabel")
    : (getLanguage() === "vi" ? "C\u00e2u h\u1ecfi g\u1ee3i \u00fd" : "Suggested questions");
}

function renderRelatedQuestions() {
  const list = document.querySelector(".chat-related-list");
  if (!list) {
    return;
  }

  const strip = list.closest(".chat-related-strip");
  collectVisibleUserQuestions();
  const language = getLanguage();
  const selectedSubject = getSelectedSubjectFilter();
  updateRelatedQuestionsLabel(selectedSubject);

  const asked = readAskedQuestions();
  const basePool = selectedSubject
    ? buildSubjectQuestionItems(selectedSubject)
    : dedupeQuestionItems([
        ...buildAllSubjectQuestionItems(),
        ...relatedQuestionPool,
        ...buildDefaultSuggestionItems()
      ]);
  const pool = getAvailableQuestionItems(basePool, asked, selectedSubject);
  if (pool.length === 0) {
    list.innerHTML = "";
    if (strip) {
      strip.hidden = true;
    }
    return;
  }

  if (strip) {
    strip.hidden = false;
  }
  const offset = getRelatedRotationIndex() % pool.length;
  const ordered = [...pool.slice(offset), ...pool.slice(0, offset)];
  const currentQuestions = new Set(
    [...list.querySelectorAll(".related-question-chip")]
      .map((button) => normalizeQuestionForMemory(button.dataset.question || button.textContent))
      .filter(Boolean));
  const picked = [];

  for (const item of ordered) {
    const text = language === "vi" ? item.vi : item.en;
    const normalized = normalizeQuestionForMemory(text);
    if (!normalized || picked.some((pickedItem) => pickedItem.id === item.id)) {
      continue;
    }

    if (pool.length > 8 && currentQuestions.has(normalized)) {
      continue;
    }

    picked.push(item);
    if (picked.length === 8) {
      break;
    }
  }

  if (picked.length < 8) {
    for (const item of ordered) {
      if (!picked.some((pickedItem) => pickedItem.id === item.id)) {
        picked.push(item);
      }
      if (picked.length === 8) {
        break;
      }
    }
  }

  list.innerHTML = picked.map((item) => {
    const text = language === "vi" ? item.vi : item.en;
    return `<button type="button" class="related-question-chip" data-question-id="${escapeHtml(item.id)}" data-question-subject="${escapeHtml(item.subject || "")}" data-question="${escapeHtml(text)}" data-question-en="${escapeHtml(item.en)}" data-question-vi="${escapeHtml(item.vi)}">${escapeHtml(text)}</button>`;
  }).join("");
  bindSuggestionButtons();
}

function updateDropzoneDefaultText() {
  if (!documentFileInput || !documentFileName || documentFileInput.files.length > 0) {
    return;
  }

  documentFileName.textContent = t("documents.dropzoneDefault");
}

function createSessionId() {
  if (crypto.randomUUID) {
    return crypto.randomUUID();
  }

  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (character) => {
    const random = Math.random() * 16 | 0;
    const value = character === "x" ? random : (random & 0x3 | 0x8);
    return value.toString(16);
  });
}

function getSessionId() {
  let sessionId = localStorage.getItem("ragChatSessionId");
  if (!sessionId) {
    sessionId = createSessionId();
    localStorage.setItem("ragChatSessionId", sessionId);
  }

  return sessionId;
}

function setSessionId(sessionId) {
  localStorage.setItem("ragChatSessionId", sessionId);
  markActiveSession(sessionId);
  updateSuggestionButtons();
  renderRelatedQuestions();
}

function formatSessionTime(value) {
  if (!value) {
    return "";
  }

  return new Date(value).toLocaleString(getLanguage() === "vi" ? "vi-VN" : "en-US", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function getSessionTitle(session) {
  return session?.title || t("chat.defaultSessionTitle");
}

function renderWelcomeMessage() {
  if (!chatMessages) {
    return;
  }

  const suggestions = getChatSuggestions();
  chatMessages.innerHTML = `
    <div class="chat-empty-state">
      <span class="empty-state-mark">AI</span>
      <h3 data-i18n="chat.emptyTitle">${escapeHtml(t("chat.emptyTitle"))}</h3>
      <p data-i18n="chat.emptyText">${escapeHtml(t("chat.emptyText"))}</p>
      <div class="suggestion-grid" aria-label="${escapeHtml(t("chat.suggestionsAria"))}" data-i18n-aria-label="chat.suggestionsAria">
        ${suggestions.map((question) => `<button type="button" class="suggestion-chip" data-question="${escapeHtml(question)}">${escapeHtml(question)}</button>`).join("")}
      </div>
    </div>
    <div class="message assistant">
      <div class="bubble" data-i18n="chat.welcome">${escapeHtml(t("chat.welcome"))}</div>
    </div>`;
  bindSuggestionButtons();
  applyLanguage();
}

function renderSessionMessages(messages) {
  if (!chatMessages) {
    return;
  }

  chatMessages.innerHTML = "";
  if (!messages || messages.length === 0) {
    renderWelcomeMessage();
    return;
  }

  messages.forEach((message) => {
    appendMessageTo(chatMessages, message.role, message.content, message.citations || []);
  });
  renderRelatedQuestions();
}

function markActiveSession(sessionId) {
  document.querySelectorAll(".chat-session-item").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.sessionId === sessionId);
  });
}

function closeSessionMenus(exceptMenu = null) {
  document.querySelectorAll(".chat-session-menu.is-open").forEach((menu) => {
    if (menu !== exceptMenu) {
      menu.classList.remove("is-open");
    }
  });
}

function renderSessionList(sessions) {
  if (!chatSessionList) {
    return;
  }

  const sessionCount = document.getElementById("sessionCount");
  if (sessionCount) {
    sessionCount.textContent = sessions?.length ?? 0;
  }

  if (!sessions || sessions.length === 0) {
    chatSessionList.innerHTML = `<p class="session-empty" data-i18n="chat.noSessions">${escapeHtml(t("chat.noSessions"))}</p>`;
    applyLanguage();
    return;
  }

  chatSessionList.innerHTML = sessions.map((session) => `
    <div class="chat-session-entry${session.isStarred ? " is-starred" : ""}" data-session-entry data-session-id="${session.id}">
      <button type="button" class="chat-session-item" data-session-id="${session.id}">
        <span>${session.isStarred ? `<span class="material-symbols-outlined session-star" aria-hidden="true">star</span>` : ""}${escapeHtml(getSessionTitle(session))}</span>
        <small>${formatSessionTime(session.updatedAt)} / ${session.messageCount ?? 0} ${escapeHtml(t("chat.messagesUnit"))}</small>
      </button>
      <button type="button" class="chat-session-menu-button" data-session-menu-toggle data-session-id="${session.id}" aria-label="${escapeHtml(t("chat.sessionActions"))}">
        <span class="material-symbols-outlined" aria-hidden="true">more_vert</span>
      </button>
      <div class="chat-session-menu" data-session-menu>
        <button type="button" data-session-action="star" data-session-id="${session.id}" data-session-starred="${session.isStarred ? "true" : "false"}">
          <span class="material-symbols-outlined" aria-hidden="true">star</span>
          <span>${escapeHtml(t(session.isStarred ? "chat.unstarSession" : "chat.starSession"))}</span>
        </button>
        <button type="button" data-session-action="rename" data-session-id="${session.id}">
          <span class="material-symbols-outlined" aria-hidden="true">edit</span>
          <span>${escapeHtml(t("chat.renameSession"))}</span>
        </button>
        <button type="button" class="danger" data-session-action="delete" data-session-id="${session.id}">
          <span class="material-symbols-outlined" aria-hidden="true">delete</span>
          <span>${escapeHtml(t("chat.deleteSession"))}</span>
        </button>
      </div>
    </div>
  `).join("");
  bindSessionButtons();
  markActiveSession(getSessionId());
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value || "";
  return div.innerHTML;
}

function formatPreviewBytes(bytes) {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = Math.max(0, Number(bytes) || 0);
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  return unitIndex === 0 ? `${value} ${units[unitIndex]}` : `${value.toFixed(value >= 10 ? 1 : 2).replace(/\.0+$/, "")} ${units[unitIndex]}`;
}

function formatPreviewDate(value) {
  if (!value) {
    return "";
  }

  return new Date(value).toLocaleString(getLanguage() === "vi" ? "vi-VN" : "en-US", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function closeDocumentPreview() {
  if (!documentPreviewModal) {
    return;
  }

  documentPreviewModal.classList.remove("is-open");
  documentPreviewModal.setAttribute("aria-hidden", "true");
}

function renderDocumentPreview(document) {
  if (!documentPreviewTitle || !documentPreviewMeta || !documentPreviewBody) {
    return;
  }

  const chunks = Array.isArray(document.chunks) ? document.chunks : [];
  const embeddingLabel = document.embeddingModel
    ? `Embedding: ${document.embeddingModel}${document.embeddingDimensions ? ` (${document.embeddingDimensions} dims)` : ""}`
    : "";
  const indexedLabel = document.indexedAt ? `Indexed: ${formatPreviewDate(document.indexedAt)}` : "";
  const uploader = document.uploadedByName ? `Upload: ${document.uploadedByName}` : "Upload: Không rõ";
  const subjectOwner = document.subjectOwnerName ? `Phụ trách: ${document.subjectOwnerName}` : "Phụ trách: Chưa phân công";
  documentPreviewTitle.textContent = document.fileName || "Tài liệu";
  documentPreviewMeta.textContent = [
    document.subject,
    document.chapter,
    document.status ? `Status: ${document.status}` : "",
    indexedLabel,
    uploader,
    subjectOwner,
    embeddingLabel,
    document.chunkingStrategy ? `Chunking: ${document.chunkingStrategy}` : "",
    `${chunks.length || document.chunkCount || 0} chunks`,
    formatPreviewBytes(document.fileSizeBytes),
    formatPreviewDate(document.uploadedAt)
  ].filter(Boolean).join(" / ");

  if (chunks.length === 0) {
    documentPreviewBody.innerHTML = `
      <div class="rbl-empty-state compact">
        <strong>Chưa có nội dung index.</strong>
        <p>Tài liệu này chưa có chunk text để hiển thị.</p>
      </div>`;
    return;
  }

  const totalChars = chunks.reduce((sum, chunk) => sum + (chunk.text || "").length, 0);
  const summary = `
    <div class="rbl-index-summary">
      <article><span>Chunks</span><strong>${escapeHtml(String(chunks.length))}</strong></article>
      <article><span>Indexed chars</span><strong>${escapeHtml(String(totalChars))}</strong></article>
      <article><span>Strategy</span><strong>${escapeHtml(document.chunkingStrategy || "unknown")}</strong></article>
    </div>`;

  documentPreviewBody.innerHTML = summary + chunks.map((chunk) => `
    <article class="rbl-document-preview-chunk">
      <span>Chunk ${escapeHtml(String(chunk.chunkIndex ?? ""))}${chunk.sectionTitle ? ` / ${escapeHtml(chunk.sectionTitle)}` : ""}</span>
      <small>${[
        Number.isInteger(chunk.charStart) && Number.isInteger(chunk.charEnd) ? `${chunk.charStart}-${chunk.charEnd}` : "",
        chunk.text ? `${chunk.text.length} chars` : ""
      ].filter(Boolean).map((value) => escapeHtml(String(value))).join(" / ")}</small>
      <p>${escapeHtml(chunk.text || "")}</p>
    </article>
  `).join("");
}

async function openDocumentPreview(url) {
  if (!documentPreviewModal || !documentPreviewBody) {
    return;
  }

  documentPreviewModal.classList.add("is-open");
  documentPreviewModal.setAttribute("aria-hidden", "false");
  if (documentPreviewTitle) {
    documentPreviewTitle.textContent = "Tài liệu";
  }
  if (documentPreviewMeta) {
    documentPreviewMeta.textContent = "";
  }
  documentPreviewBody.innerHTML = `<p class="rbl-catalog-muted">Đang tải nội dung đã index...</p>`;

  try {
    const response = await fetch(url, { headers: { "Accept": "application/json" } });
    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.error || "Could not load document preview.");
    }

    renderDocumentPreview(payload);
  } catch (error) {
    documentPreviewBody.innerHTML = `
      <div class="rbl-alert is-error">
        ${escapeHtml(error.message || "Không tải được nội dung tài liệu.")}
      </div>`;
  }
}

function bindDocumentPreviewButtons() {
  document.querySelectorAll("[data-document-preview-url]").forEach((button) => {
    button.addEventListener("click", () => {
      openDocumentPreview(button.dataset.documentPreviewUrl);
    });
  });

  document.querySelectorAll("[data-document-preview-close]").forEach((button) => {
    button.addEventListener("click", closeDocumentPreview);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && documentPreviewModal?.classList.contains("is-open")) {
      closeDocumentPreview();
    }
  });
}

async function refreshSessionList() {
  if (!chatSessionList) {
    return;
  }

  try {
    const response = await fetch("/Home/ChatSessions");
    if (!response.ok) {
      return;
    }

    renderSessionList(await response.json());
  } catch {
    // Session history is helpful, but chat should still work if it cannot refresh.
  }
}

async function loadChatSession(sessionId) {
  if (!sessionId) {
    return;
  }

  const response = await fetch(`/Home/ChatSession/${sessionId}`);
  if (!response.ok) {
    return;
  }

  const session = await response.json();
  setSessionId(session.id);
  if (activeSessionTitle) {
    activeSessionTitle.textContent = getSessionTitle(session);
  }
  renderSessionMessages(session.messages || []);
  questionInput?.focus();
}

async function postSessionJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(payload.error || t("chat.sessionActionError"));
  }

  return payload;
}

async function renameChatSession(sessionId) {
  const currentTitle = document
    .querySelector(`.chat-session-item[data-session-id="${CSS.escape(sessionId)}"] span`)
    ?.textContent
    ?.trim() || "";
  const title = window.prompt(t("chat.renamePrompt"), currentTitle);
  if (title === null) {
    return;
  }

  const normalizedTitle = title.trim();
  if (!normalizedTitle) {
    return;
  }

  const session = await postSessionJson("/Home/RenameChatSession", { sessionId, title: normalizedTitle });
  if (getSessionId() === session.id && activeSessionTitle) {
    activeSessionTitle.textContent = getSessionTitle(session);
  }
  await refreshSessionList();
}

async function toggleChatSessionStar(sessionId, isCurrentlyStarred) {
  await postSessionJson("/Home/StarChatSession", {
    sessionId,
    isStarred: !isCurrentlyStarred
  });
  await refreshSessionList();
}

async function deleteChatSession(sessionId) {
  if (!window.confirm(t("chat.deleteConfirm"))) {
    return;
  }

  await postSessionJson("/Home/DeleteChatSession", { sessionId });
  if (getSessionId() === sessionId) {
    const response = await fetch("/Home/CreateChatSession", { method: "POST" });
    if (response.ok) {
      const session = await response.json();
      setSessionId(session.id);
    } else {
      setSessionId(createSessionId());
    }

    if (activeSessionTitle) {
      activeSessionTitle.textContent = t("chat.defaultSessionTitle");
    }
    renderWelcomeMessage();
  }

  await refreshSessionList();
}

function bindSessionButtons() {
  document.querySelectorAll(".chat-session-item").forEach((button) => {
    button.addEventListener("click", () => {
      loadChatSession(button.dataset.sessionId);
    });
  });

  document.querySelectorAll("[data-session-menu-toggle]").forEach((button) => {
    button.addEventListener("click", (event) => {
      event.stopPropagation();
      const entry = button.closest("[data-session-entry]");
      const menu = entry?.querySelector("[data-session-menu]");
      if (!menu) {
        return;
      }

      const willOpen = !menu.classList.contains("is-open");
      closeSessionMenus(menu);
      menu.classList.toggle("is-open", willOpen);
    });
  });

  document.querySelectorAll("[data-session-action]").forEach((button) => {
    button.addEventListener("click", async (event) => {
      event.stopPropagation();
      const sessionId = button.dataset.sessionId;
      const action = button.dataset.sessionAction;
      closeSessionMenus();
      if (!sessionId || !action) {
        return;
      }

      try {
        if (action === "rename") {
          await renameChatSession(sessionId);
        } else if (action === "star") {
          await toggleChatSessionStar(sessionId, button.dataset.sessionStarred === "true");
        } else if (action === "delete") {
          await deleteChatSession(sessionId);
        }
      } catch (error) {
        appendMessageTo(chatMessages, "assistant", error.message || t("chat.sessionActionError"));
      }
    });
  });
}

function appendMessageTo(target, role, content, citations = []) {
  if (!target) {
    return null;
  }

  if (role === "user") {
    target.querySelector(".chat-empty-state")?.remove();
  }

  const wrapper = document.createElement("div");
  wrapper.className = `message ${role}`;

  const bubble = document.createElement("div");
  bubble.className = "bubble";
  bubble.textContent = content;

  wrapper.appendChild(bubble);
  appendCitationsToMessage(wrapper, citations);
  target.appendChild(wrapper);
  target.scrollTop = target.scrollHeight;
  return wrapper;
}

function appendCitationsToMessage(messageWrapper, citations) {
  const sourceItems = Array.isArray(citations)
    ? citations.filter((citation) => citation && typeof citation === "object")
    : [];

  if (!messageWrapper || sourceItems.length === 0) {
    return;
  }

  const seenSources = new Set();
  const compactSources = [];
  for (const citation of sourceItems) {
    const fileName = citation.fileName || citation.FileName || "Document";
    const chunkIndex = citation.chunkIndex ?? citation.ChunkIndex;
    const sourceKey = `${fileName}|${chunkIndex ?? ""}`;
    if (seenSources.has(sourceKey)) {
      continue;
    }

    seenSources.add(sourceKey);
    compactSources.push(citation);
    if (compactSources.length === 3) {
      break;
    }
  }

  if (compactSources.length === 0) {
    return;
  }

  const list = document.createElement("div");
  list.className = "citations compact-citations";
  list.setAttribute("aria-label", "Sources");

  const label = document.createElement("span");
  label.className = "citation-label";
  label.textContent = getLanguage() === "vi" ? "Nguồn:" : "Sources:";
  list.appendChild(label);

  compactSources.forEach((citation) => {
    const item = document.createElement("span");
    item.className = "citation citation-source";
    const fileName = citation.fileName || citation.FileName || "Document";
    const chunkIndex = citation.chunkIndex ?? citation.ChunkIndex;
    const metaValues = [
      citation.subject || citation.Subject,
      citation.chapter || citation.Chapter
    ].filter(Boolean);
    item.textContent = chunkIndex ? `${fileName} / chunk ${chunkIndex}` : fileName;
    item.title = metaValues.join(" / ");

    list.appendChild(item);
  });

  messageWrapper.appendChild(list);
}

function renderClarificationOptions(messageWrapper, options, originalQuestion) {
  const subjects = Array.isArray(options)
    ? options.filter((subject) => typeof subject === "string" && subject.trim().length > 0).slice(0, 6)
    : [];

  if (!messageWrapper || subjects.length === 0) {
    return;
  }

  const actions = document.createElement("div");
  actions.className = "chat-clarification-options";
  subjects.forEach((subject) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "chat-clarification-chip";
    button.textContent = subject;
    button.addEventListener("click", () => {
      setSelectedSubjectFilter(subject);
      if (questionInput && chatForm) {
        questionInput.value = originalQuestion;
        chatForm.requestSubmit();
      }
    });
    actions.appendChild(button);
  });

  messageWrapper.appendChild(actions);
  messageWrapper.parentElement.scrollTop = messageWrapper.parentElement.scrollHeight;
}

async function submitChatQuestion(input, messagesTarget, focusAfter = true) {
  const question = input?.value.trim();
  if (!question || !messagesTarget) {
    return false;
  }

  rememberAskedQuestion(question);
  advanceRelatedRotation();
  updateSuggestionButtons();
  renderRelatedQuestions();
  appendMessageTo(messagesTarget, "user", question);
  input.value = "";
  const loadingMessage = appendMessageTo(messagesTarget, "assistant", t("chat.loading"));

  try {
    const response = await fetch("/Home/Ask", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sessionId: getSessionId(),
        question,
        subjectFilter: getSelectedSubjectFilter(),
        language: getLanguage()
      })
    });

    const payload = await response.json();
    loadingMessage?.remove();

    if (!response.ok) {
      appendMessageTo(messagesTarget, "assistant", payload.error || t("chat.requestError"));
      return false;
    }

    setSessionId(payload.sessionId);
    rememberAskedQuestion(question);
    if (activeSessionTitle && (!activeSessionTitle.textContent?.trim() || activeSessionTitle.textContent.trim() === t("chat.defaultSessionTitle"))) {
      activeSessionTitle.textContent = question.length <= 56 ? question : `${question.slice(0, 56)}...`;
    }
    const answerMessage = appendMessageTo(messagesTarget, "assistant", payload.answer, payload.citations || []);
    if (payload.needsClarification && Array.isArray(payload.subjectOptions)) {
      renderClarificationOptions(answerMessage, payload.subjectOptions, question);
    }

    advanceRelatedRotation();
    renderRelatedQuestions();
    refreshSessionList();
    return true;
  } catch {
    loadingMessage?.remove();
    appendMessageTo(messagesTarget, "assistant", t("chat.connectionError"));
    return false;
  } finally {
    if (focusAfter) {
      input.focus();
    }
  }
}

document.querySelectorAll("[data-language-option]").forEach((button) => {
  button.addEventListener("click", () => {
    setLanguage(button.dataset.languageOption);
  });
});

document.addEventListener("click", (event) => {
  if (!event.target.closest?.("[data-session-entry]")) {
    closeSessionMenus();
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    closeSessionMenus();
  }
});

if (newSessionButton) {
  newSessionButton.addEventListener("click", async () => {
    try {
      const response = await fetch("/Home/CreateChatSession", { method: "POST" });
      if (response.ok) {
        const session = await response.json();
        setSessionId(session.id);
      } else {
        setSessionId(createSessionId());
      }
    } catch {
      setSessionId(createSessionId());
    }

    if (activeSessionTitle) {
      activeSessionTitle.textContent = t("chat.defaultSessionTitle");
    }
    renderWelcomeMessage();
    refreshSessionList();
  });
}

if (documentDropzone && documentFileInput && documentFileName) {
  function updateSelectedFileName(files) {
    const file = files?.[0];
    documentFileName.textContent = file ? file.name : t("documents.dropzoneDefault");
    documentDropzone.classList.toggle("has-file", Boolean(file));
  }

  documentFileInput.addEventListener("change", () => {
    updateSelectedFileName(documentFileInput.files);
  });

  ["dragenter", "dragover"].forEach((eventName) => {
    documentDropzone.addEventListener(eventName, (event) => {
      event.preventDefault();
      documentDropzone.classList.add("is-dragover");
    });
  });

  ["dragleave", "drop"].forEach((eventName) => {
    documentDropzone.addEventListener(eventName, () => {
      documentDropzone.classList.remove("is-dragover");
    });
  });

  documentDropzone.addEventListener("drop", (event) => {
    event.preventDefault();
    if (event.dataTransfer?.files?.length) {
      documentFileInput.files = event.dataTransfer.files;
      updateSelectedFileName(documentFileInput.files);
    }
  });
}

function initAssistantLauncherDrag() {
  if (!assistantLauncher || !assistantLauncherButton) {
    return;
  }

  let startX = 0;
  let startY = 0;
  let startLeft = 0;
  let startTop = 0;
  let lastX = 0;
  let didDrag = false;
  let suppressClick = false;

  function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
  }

  function moveLauncher(left, top, deltaX) {
    const rect = assistantLauncher.getBoundingClientRect();
    const maxLeft = window.innerWidth - rect.width - 10;
    const maxTop = window.innerHeight - rect.height - 10;
    assistantLauncher.style.left = `${clamp(left, 10, maxLeft)}px`;
    assistantLauncher.style.top = `${clamp(top, 10, maxTop)}px`;
    assistantLauncher.style.right = "auto";
    assistantLauncher.style.bottom = "auto";
    assistantLauncher.style.setProperty("--launcher-stretch-x", String(1 + Math.min(Math.abs(deltaX) / 160, 0.16)));
    assistantLauncher.style.setProperty("--launcher-stretch-y", String(1 - Math.min(Math.abs(deltaX) / 240, 0.08)));
    assistantLauncher.style.setProperty("--launcher-rotate", `${clamp(deltaX / 12, -8, 8)}deg`);
  }

  assistantLauncherButton.addEventListener("pointerdown", (event) => {
    if (event.button !== 0) {
      return;
    }

    const rect = assistantLauncher.getBoundingClientRect();
    startX = event.clientX;
    startY = event.clientY;
    lastX = event.clientX;
    startLeft = rect.left;
    startTop = rect.top;
    didDrag = false;
    event.preventDefault();
    assistantLauncher.classList.add("is-dragging");
    assistantLauncher.classList.remove("is-released");
    assistantLauncherButton.setPointerCapture?.(event.pointerId);
  });

  assistantLauncherButton.addEventListener("pointermove", (event) => {
    if (!assistantLauncher.classList.contains("is-dragging")) {
      return;
    }

    const deltaX = event.clientX - startX;
    const deltaY = event.clientY - startY;
    if (Math.hypot(deltaX, deltaY) > 5) {
      didDrag = true;
    }

    if (!didDrag) {
      return;
    }

    event.preventDefault();
    moveLauncher(startLeft + deltaX, startTop + deltaY, event.clientX - lastX);
    lastX = event.clientX;
  });

  function finishDrag(event) {
    if (!assistantLauncher.classList.contains("is-dragging")) {
      return;
    }

    assistantLauncher.classList.remove("is-dragging");
    assistantLauncher.classList.add("is-released");
    assistantLauncher.style.setProperty("--launcher-stretch-x", "1");
    assistantLauncher.style.setProperty("--launcher-stretch-y", "1");
    assistantLauncher.style.setProperty("--launcher-rotate", "0deg");
    assistantLauncherButton.releasePointerCapture?.(event.pointerId);

    if (didDrag) {
      suppressClick = true;
      window.setTimeout(() => {
        suppressClick = false;
      }, 0);
    }

    window.setTimeout(() => {
      assistantLauncher.classList.remove("is-released");
    }, 280);
  }

  assistantLauncherButton.addEventListener("pointerup", finishDrag);
  assistantLauncherButton.addEventListener("pointercancel", finishDrag);
  assistantLauncherButton.addEventListener("click", (event) => {
    event.preventDefault();

    const clickDistance = Math.hypot(event.clientX - startX, event.clientY - startY);
    if (suppressClick || clickDistance > 5) {
      return;
    }

    window.location.href = assistantLauncherButton.href;
  });
}

function bindSuggestionButtons() {
  document.querySelectorAll("[data-question]").forEach((button) => {
    if (button.dataset.questionBound === "true") {
      return;
    }

    button.dataset.questionBound = "true";
    button.addEventListener("click", () => {
      if (!questionInput) {
        return;
      }

      questionInput.value = button.dataset.question || "";
      questionInput.focus();
      rememberAskedQuestion(questionInput.value);
      updateSuggestionButtons();
      renderRelatedQuestions();
      if (button.classList.contains("related-question-chip")) {
        chatForm?.requestSubmit();
      }
    });
  });
}

bindSuggestionButtons();
bindSessionButtons();
bindSubjectFilterChips();
bindDocumentPreviewButtons();
initAssistantLauncherDrag();
markActiveSession(getSessionId());
applyLanguage();

if (chatForm) {
  chatForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (isSending) {
      return;
    }

    isSending = true;
    try {
      await submitChatQuestion(questionInput, chatMessages);
    } finally {
      isSending = false;
    }
  });
}

if (questionInput && chatForm) {
  questionInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      chatForm.requestSubmit();
    }
  });
}
