const translations = {
  en: {
    "nav.documents": "Documents",
    "nav.chat": "Chat",
    "nav.logout": "Logout",
    "nav.login": "Login",
    "nav.register": "Create account",
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
    "documents.subjectPlaceholder": "Example: DBA103 - Traditional musical instrument",
    "documents.chapterPlaceholder": "Example: Syllabus 11835 or Chapter 1",
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
    "chat.currentSession": "Current session",
    "chat.emptyTitle": "Start with a specific question",
    "chat.emptyText": "Choose a suggestion below or type your question. If the documents do not contain enough data, the chatbot will say so instead of guessing.",
    "chat.suggestionsAria": "Question suggestions",
    "chat.welcome": "You can ask about uploaded document content. I will answer concisely when there is enough data.",
    "chat.placeholder": "Example: How many credits does DBA103 have?",
    "chat.send": "Send",
    "chat.relatedLabel": "Related questions",
    "chat.relatedAria": "Related questions",
    "chat.defaultSessionTitle": "Session without a question",
    "chat.loading": "Searching the documents...",
    "chat.requestError": "Could not process the question.",
    "chat.connectionError": "Could not connect to the server. Check the app and try again.",
    "chat.suggestions": [
      "Summarize the main content of the uploaded documents",
      "What assessment requirements does this course have?",
      "Explain the most important part of the current chapter",
      "Which document contains this information?"
    ]
  },
  vi: {
    "nav.documents": "T\u00e0i li\u1ec7u",
    "nav.chat": "H\u1ecfi \u0111\u00e1p",
    "nav.logout": "\u0110\u0103ng xu\u1ea5t",
    "nav.login": "\u0110\u0103ng nh\u1eadp",
    "nav.register": "T\u1ea1o t\u00e0i kho\u1ea3n",
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
    "documents.subjectPlaceholder": "VD: DBA103 - Traditional musical instrument",
    "documents.chapterPlaceholder": "VD: Syllabus 11835 ho\u1eb7c Chapter 1",
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
    "chat.currentSession": "Phi\u00ean hi\u1ec7n t\u1ea1i",
    "chat.emptyTitle": "B\u1eaft \u0111\u1ea7u b\u1eb1ng m\u1ed9t c\u00e2u h\u1ecfi c\u1ee5 th\u1ec3",
    "chat.emptyText": "Ch\u1ecdn g\u1ee3i \u00fd b\u00ean d\u01b0\u1edbi ho\u1eb7c nh\u1eadp c\u00e2u h\u1ecfi c\u1ee7a b\u1ea1n. N\u1ebfu t\u00e0i li\u1ec7u kh\u00f4ng \u0111\u1ee7 d\u1eef li\u1ec7u, chatbot s\u1ebd b\u00e1o r\u00f5 thay v\u00ec \u0111o\u00e1n.",
    "chat.suggestionsAria": "G\u1ee3i \u00fd c\u00e2u h\u1ecfi",
    "chat.welcome": "B\u1ea1n c\u00f3 th\u1ec3 h\u1ecfi v\u1ec1 n\u1ed9i dung \u0111\u00e3 upload. M\u00ecnh s\u1ebd tr\u1ea3 l\u1eddi ng\u1eafn g\u1ecdn khi c\u00f3 \u0111\u1ee7 d\u1eef li\u1ec7u.",
    "chat.placeholder": "V\u00ed d\u1ee5: M\u00f4n DBA103 c\u00f3 bao nhi\u00eau t\u00edn ch\u1ec9?",
    "chat.send": "G\u1eedi",
    "chat.relatedLabel": "C\u00e2u h\u1ecfi li\u00ean quan",
    "chat.relatedAria": "C\u00e2u h\u1ecfi li\u00ean quan",
    "chat.defaultSessionTitle": "Phi\u00ean ch\u01b0a c\u00f3 c\u00e2u h\u1ecfi",
    "chat.loading": "\u0110ang t\u00ecm trong t\u00e0i li\u1ec7u...",
    "chat.requestError": "Kh\u00f4ng x\u1eed l\u00fd \u0111\u01b0\u1ee3c c\u00e2u h\u1ecfi.",
    "chat.connectionError": "Kh\u00f4ng k\u1ebft n\u1ed1i \u0111\u01b0\u1ee3c server. Ki\u1ec3m tra l\u1ea1i \u1ee9ng d\u1ee5ng r\u1ed3i th\u1eed ti\u1ebfp.",
    "chat.suggestions": [
      "T\u00f3m t\u1eaft n\u1ed9i dung ch\u00ednh c\u1ee7a t\u00e0i li\u1ec7u \u0111\u00e3 upload",
      "M\u00f4n h\u1ecdc n\u00e0y c\u00f3 nh\u1eefng y\u00eau c\u1ea7u \u0111\u00e1nh gi\u00e1 n\u00e0o?",
      "Gi\u1ea3i th\u00edch ph\u1ea7n quan tr\u1ecdng nh\u1ea5t trong ch\u01b0\u01a1ng hi\u1ec7n t\u1ea1i",
      "Cho m\u00ecnh bi\u1ebft th\u00f4ng tin n\u00e0y n\u1eb1m \u1edf t\u00e0i li\u1ec7u n\u00e0o?"
    ]
  }
};

const languageKey = "courseAssistantLanguage";
const chatForm = document.getElementById("chatForm");
const questionInput = document.getElementById("questionInput");
const chatMessages = document.getElementById("chatMessages");
const newSessionButton = document.getElementById("newSessionButton");
const chatSessionList = document.getElementById("chatSessionList");
const activeSessionTitle = document.getElementById("activeSessionTitle");
const documentDropzone = document.getElementById("documentDropzone");
const documentFileInput = document.getElementById("documentFileInput");
const documentFileName = document.getElementById("documentFileName");
const assistantLauncher = document.getElementById("chatbotHelper");
const assistantLauncherButton = document.getElementById("chatbotHelperButton");
let isSending = false;

function getLanguage() {
  return localStorage.getItem(languageKey) === "vi" ? "vi" : "en";
}

function t(key) {
  return translations[getLanguage()][key] || translations.en[key] || key;
}

function setLanguage(language) {
  const nextLanguage = language === "vi" ? "vi" : "en";
  localStorage.setItem(languageKey, nextLanguage);
  applyLanguage();
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
}

function updateSuggestionButtons() {
  const suggestions = translations[getLanguage()]["chat.suggestions"];
  document.querySelectorAll(".suggestion-chip").forEach((button, index) => {
    const question = suggestions[index] || button.dataset.questionEn || button.textContent;
    button.textContent = question;
    button.dataset.question = question;
  });
}

function updateRelatedQuestionButtons() {
  const language = getLanguage();
  document.querySelectorAll(".related-question-chip").forEach((button) => {
    const question = language === "vi" ? button.dataset.questionVi : button.dataset.questionEn;
    if (!question) {
      return;
    }

    button.textContent = question;
    button.dataset.question = question;
  });
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

  const suggestions = translations[getLanguage()]["chat.suggestions"];
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
    appendMessageTo(chatMessages, message.role, message.content);
  });
}

function markActiveSession(sessionId) {
  document.querySelectorAll(".chat-session-item").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.sessionId === sessionId);
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
    <button type="button" class="chat-session-item" data-session-id="${session.id}">
      <span>${escapeHtml(getSessionTitle(session))}</span>
      <small>${formatSessionTime(session.updatedAt)} / ${session.messageCount ?? 0} ${escapeHtml(t("chat.messagesUnit"))}</small>
    </button>
  `).join("");
  bindSessionButtons();
  markActiveSession(getSessionId());
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value || "";
  return div.innerHTML;
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

function bindSessionButtons() {
  document.querySelectorAll(".chat-session-item").forEach((button) => {
    button.addEventListener("click", () => {
      loadChatSession(button.dataset.sessionId);
    });
  });
}

function appendMessageTo(target, role, content) {
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
  target.appendChild(wrapper);
  target.scrollTop = target.scrollHeight;
  return wrapper;
}

async function submitChatQuestion(input, messagesTarget, focusAfter = true) {
  const question = input?.value.trim();
  if (!question || !messagesTarget) {
    return false;
  }

  appendMessageTo(messagesTarget, "user", question);
  input.value = "";
  const loadingMessage = appendMessageTo(messagesTarget, "assistant", t("chat.loading"));

  try {
    const response = await fetch("/Home/Ask", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sessionId: getSessionId(), question, language: getLanguage() })
    });

    const payload = await response.json();
    loadingMessage?.remove();

    if (!response.ok) {
      appendMessageTo(messagesTarget, "assistant", payload.error || t("chat.requestError"));
      return false;
    }

    setSessionId(payload.sessionId);
    if (activeSessionTitle) {
      activeSessionTitle.textContent = question.length <= 56 ? question : `${question.slice(0, 56)}...`;
    }
    appendMessageTo(messagesTarget, "assistant", payload.answer);
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
    button.addEventListener("click", () => {
      if (!questionInput) {
        return;
      }

      questionInput.value = button.dataset.question || "";
      questionInput.focus();
      if (button.classList.contains("related-question-chip")) {
        chatForm?.requestSubmit();
      }
    });
  });
}

bindSuggestionButtons();
bindSessionButtons();
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
