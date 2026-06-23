(function () {
  document.querySelectorAll('[data-student-chat-form]').forEach((form) => {
    const thread = document.querySelector('[data-student-chat-thread]');
    const sessionInput = form.querySelector('[data-student-chat-session]');
    const questionInput = form.querySelector('[data-student-chat-question]');
    const subjectSelect = form.querySelector('[data-student-chat-subject]');
    const submitButton = form.querySelector('button[type="submit"]');
    const validation = form.querySelector('.student-chat-validation');
    const citationList = document.querySelector('[data-student-chat-citations]');
    const sourceCount = document.querySelector('[data-student-chat-source-count]');
    const citationModal = document.querySelector('[data-citation-modal]');
    const citationTitle = citationModal?.querySelector('[data-citation-title]');
    const citationMeta = citationModal?.querySelector('[data-citation-meta]');
    const citationScore = citationModal?.querySelector('[data-citation-score]');
    const citationExcerpt = citationModal?.querySelector('[data-citation-excerpt]');
    let lastFocusedCitation = null;
    let connection = null;
    let connectionReady = null;
    let currentStream = null;
    let isThreadPinnedToBottom = true;
    const threadBottomThreshold = 72;
    const streamRenderIntervalMs = 48;
    const streamScrollIntervalMs = 120;
    const requestFrame = window.requestAnimationFrame?.bind(window) ?? ((callback) => window.setTimeout(callback, 16));

    if (!thread || !questionInput || !subjectSelect || !submitButton) {
      return;
    }

    if (window.signalR) {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/student-chat')
        .withAutomaticReconnect()
        .build();

      connection.on('chatAnswerDelta', (delta) => {
        if (currentStream) {
          appendDelta(currentStream, typeof delta === 'string' ? delta : '');
        }
      });

      connection.on('chatAnswerCompleted', (payload) => {
        if (!currentStream) {
          return;
        }

        finalizeStream(
          currentStream,
          getValue(payload, 'answer') || currentStream.answer || 'Gemini did not return an answer.',
          getValue(payload, 'citations') || [],
          getValue(payload, 'sessionId'));
      });

      connection.on('chatAnswerError', (message) => {
        if (!currentStream) {
          return;
        }

        failStream(
          currentStream,
          typeof message === 'string' ? message : getValue(message, 'message') || 'Could not generate an answer. Please try again.');
      });

      connectionReady = connection.start().catch(() => {
        connection = null;
        return null;
      });
    }

    questionInput.addEventListener('input', updateSendButtonState);
    questionInput.addEventListener('change', updateSendButtonState);
    thread.addEventListener('scroll', updateThreadPinnedState, { passive: true });
    hydrateExistingMarkdownAnswers();
    requestFrame(() => {
      scrollThreadToBottom(true);
    });

    form.addEventListener('submit', async (event) => {
      event.preventDefault();

      const question = questionInput.value.trim();
      if (!question) {
        renderValidation(['Please enter a question.']);
        questionInput.focus();
        return;
      }

      clearValidation();
      removeEmptyState();
      appendUserMessage(question);
      currentStream = appendPendingAnswer();
      pinThreadToBottom();

      const sessionId = sessionInput?.value || '';
      const subjectId = subjectSelect.value;
      questionInput.value = '';
      setSubmitting(true);

      try {
        const canStream = await canUseSignalR();
        if (canStream && connection) {
          await connection.invoke('AskAsync', {
            sessionId: sessionId || null,
            subjectId,
            question
          });
        } else {
          await submitViaFetch(question, sessionId, subjectId);
        }
      } catch (error) {
        failStream(
          currentStream,
          error instanceof Error ? error.message : 'Could not generate an answer. Please try again.');
      } finally {
        setSubmitting(false);
        scrollThreadToBottom();
      }
    });

    updateSendButtonState();

    document.addEventListener('click', (event) => {
      const citationCard = event.target.closest('[data-citation-card]');
      if (citationCard) {
        openCitationModal(citationCard);
        return;
      }

      if (event.target.closest('[data-citation-close]')) {
        closeCitationModal();
      }
    });

    document.addEventListener('keydown', (event) => {
      const citationCard = event.target.closest?.('[data-citation-card]');
      if (citationCard && (event.key === 'Enter' || event.key === ' ')) {
        event.preventDefault();
        openCitationModal(citationCard);
        return;
      }

      if (event.key === 'Escape' && citationModal && !citationModal.hidden) {
        closeCitationModal();
      }
    });

    async function canUseSignalR() {
      if (!connection || !connectionReady) {
        return false;
      }

      try {
        await connectionReady;
        return connection.state === signalR.HubConnectionState.Connected;
      } catch {
        return false;
      }
    }

    async function submitViaFetch(question, sessionId, subjectId) {
      const formData = new FormData(form);
      formData.set('Input.Question', question);
      formData.set('Input.SubjectId', subjectId);
      if (sessionId) {
        formData.set('Input.SessionId', sessionId);
      }

      const response = await fetch(form.dataset.askUrl || form.action, {
        method: 'POST',
        body: formData,
        headers: {
          'Accept': 'application/json',
          'X-Requested-With': 'XMLHttpRequest'
        }
      });
      const contentType = response.headers.get('content-type') || '';
      const isJsonResponse = contentType.includes('application/json');
      const payload = isJsonResponse ? await response.json().catch(() => ({})) : {};

      if (!response.ok) {
        const errors = getValue(payload, 'errors') || [isJsonResponse
          ? 'Could not generate an answer. Please try again.'
          : 'Unexpected server response. Please try again.'];
        throw new Error(errors.join(' '));
      }

      if (!isJsonResponse) {
        throw new Error('Unexpected server response. Please try again.');
      }

      finalizeStream(
        currentStream,
        getValue(payload, 'answer') || 'Gemini did not return an answer.',
        getValue(payload, 'citations') || [],
        getValue(payload, 'sessionId'));
    }

    function appendUserMessage(text) {
      const row = document.createElement('div');
      row.className = 'student-chat-message-row user';

      const bubble = document.createElement('div');
      bubble.className = 'student-chat-user-bubble';
      bubble.textContent = text;

      row.append(bubble);
      thread.append(row);
    }

    function appendPendingAnswer() {
      const row = document.createElement('div');
      row.className = 'student-chat-message-row assistant';

      const avatar = document.createElement('div');
      avatar.className = 'student-chat-bot-avatar';
      avatar.setAttribute('aria-hidden', 'true');
      avatar.textContent = 'AI';

      const answerWrap = document.createElement('div');
      answerWrap.className = 'student-chat-answer';

      const bubble = document.createElement('div');
      bubble.className = 'student-chat-ai-bubble analyzing';

      const streamingLine = document.createElement('div');
      streamingLine.className = 'student-chat-streaming-line';

      const streamText = document.createElement('span');
      streamText.className = 'student-chat-streaming-text';
      streamText.textContent = 'Analyzing course materials';

      const typing = document.createElement('span');
      typing.className = 'student-chat-typing student-chat-streaming-typing';
      typing.setAttribute('aria-hidden', 'true');
      typing.append(document.createElement('span'), document.createElement('span'), document.createElement('span'));

      streamingLine.append(streamText, typing);
      bubble.append(streamingLine);

      const citations = document.createElement('div');
      citations.className = 'student-chat-citations student-chat-message-citations';
      citations.hidden = true;

      answerWrap.append(bubble, citations);
      row.append(avatar, answerWrap);
      thread.append(row);

      return {
        bubble,
        citations,
        answer: '',
        active: true,
        renderHandle: null,
        renderQueued: false,
        lastRenderAt: 0,
        lastScrollAt: 0,
        streamText
      };
    }

    function appendDelta(state, delta) {
      if (!state || !delta) {
        return;
      }

      state.answer += delta;
      scheduleRender(state);
    }

    function finalizeStream(state, answer, citations, sessionId) {
      if (!state || !state.active) {
        return;
      }

      state.active = false;
      cancelScheduledRender(state);
      renderMarkdownAnswer(state.bubble, answer);
      renderMessageCitations(state.citations, citations);
      renderCitations(citations);
      if (isThreadPinnedToBottom) {
        scrollThreadToBottom(true);
      }

      if (sessionId && sessionInput) {
        sessionInput.value = sessionId;
      }

      currentStream = null;
      updateSendButtonState();
    }

    function failStream(state, message) {
      if (!state || !state.active) {
        return;
      }

      state.active = false;
      cancelScheduledRender(state);
      renderMarkdownAnswer(state.bubble, message, true);
      renderMessageCitations(state.citations, []);
      if (isThreadPinnedToBottom) {
        scrollThreadToBottom(true);
      }
      currentStream = null;
      updateSendButtonState();
    }

    function scheduleRender(state) {
      if (!state.active || state.renderQueued) {
        return;
      }

      var now = nowMs();
      var delay = Math.max(0, streamRenderIntervalMs - (now - state.lastRenderAt));

      if (delay === 0) {
        renderStreamingAnswer(state);
        return;
      }

      state.renderQueued = true;
      state.renderHandle = window.setTimeout(() => {
        state.renderHandle = null;
        if (!state.active) {
          state.renderQueued = false;
          return;
        }

        renderStreamingAnswer(state);
      }, delay);
    }

    function cancelScheduledRender(state) {
      if (!state.renderQueued || state.renderHandle === null) {
        return;
      }

      window.clearTimeout(state.renderHandle);
      state.renderQueued = false;
      state.renderHandle = null;
    }

    function hydrateExistingMarkdownAnswers() {
      document.querySelectorAll('[data-student-chat-markdown-source]').forEach((bubble) => {
        renderMarkdownAnswer(bubble, bubble.dataset.studentChatMarkdownSource || bubble.textContent || '');
      });
    }

    function updateThreadPinnedState() {
      isThreadPinnedToBottom = isThreadNearBottom();
    }

    function isThreadNearBottom() {
      return thread.scrollHeight - thread.scrollTop - thread.clientHeight <= threadBottomThreshold;
    }

    function pinThreadToBottom() {
      isThreadPinnedToBottom = true;
      scrollThreadToBottom(true);
    }

    function scrollThreadToBottom(force = false) {
      if (!force && !isThreadPinnedToBottom) {
        return;
      }

      thread.scrollTop = thread.scrollHeight;
    }

    function renderStreamingAnswer(state, forceScroll = false) {
      if (!state?.bubble) {
        return;
      }

      state.renderQueued = false;
      state.lastRenderAt = nowMs();
      state.bubble.dataset.studentChatMarkdownSource = String(state.answer ?? '');
      if (state.streamText) {
        state.streamText.textContent = String(state.answer ?? '');
      }

      if ((forceScroll || isThreadPinnedToBottom) && nowMs() - state.lastScrollAt >= streamScrollIntervalMs) {
        state.lastScrollAt = nowMs();
        scrollThreadToBottom(forceScroll);
      }
    }

    function renderMarkdownAnswer(bubble, text, isError = false) {
      bubble.className = `student-chat-ai-bubble${isError ? ' error' : ''}`;
      bubble.dataset.studentChatMarkdownSource = String(text ?? '');
      bubble.replaceChildren();

      const nodes = buildMarkdownNodes(String(text ?? ''));
      if (nodes.length === 0) {
        const paragraph = document.createElement('p');
        paragraph.textContent = String(text ?? '');
        bubble.append(paragraph);
        return;
      }

      nodes.forEach((node) => {
        bubble.append(node);
      });
    }

    function buildMarkdownNodes(markdownText) {
      const normalized = String(markdownText || '').replace(/\r\n/g, '\n');
      const lines = normalized.split('\n');
      const nodes = [];
      let index = 0;

      while (index < lines.length) {
        const line = lines[index];
        const trimmed = line.trim();

        if (!trimmed) {
          index++;
          continue;
        }

        const fenceMatch = line.match(/^```([\w-]+)?\s*$/);
        if (fenceMatch) {
          const codeLines = [];
          const language = sanitizeToken(fenceMatch[1] || '');
          index++;

          while (index < lines.length && !lines[index].match(/^```\s*$/)) {
            codeLines.push(lines[index]);
            index++;
          }

          if (index < lines.length && lines[index].match(/^```\s*$/)) {
            index++;
          }

          const pre = document.createElement('pre');
          const code = document.createElement('code');
          if (language) {
            code.className = `language-${language}`;
          }
          code.textContent = codeLines.join('\n');
          pre.append(code);
          nodes.push(pre);
          continue;
        }

        const headingMatch = line.match(/^(#{1,3})\s+(.+)$/);
        if (headingMatch) {
          const level = Math.min(3, headingMatch[1].length);
          const heading = document.createElement(`h${level + 1}`);
          appendInlineMarkdown(heading, headingMatch[2]);
          nodes.push(heading);
          index++;
          continue;
        }

        if (/^>\s?/.test(line)) {
          const quoteLines = [];
          while (index < lines.length && /^>\s?/.test(lines[index])) {
            quoteLines.push(lines[index].replace(/^>\s?/, ''));
            index++;
          }

          const blockquote = document.createElement('blockquote');
          const paragraph = document.createElement('p');
          appendInlineMarkdown(paragraph, quoteLines.join(' '));
          blockquote.append(paragraph);
          nodes.push(blockquote);
          continue;
        }

        const listMatch = line.match(/^(\s*)([-*+]|(\d+)\.)\s+(.+)$/);
        if (listMatch) {
          const ordered = Boolean(listMatch[3]);
          const list = document.createElement(ordered ? 'ol' : 'ul');

          while (index < lines.length) {
            const itemMatch = lines[index].match(/^(\s*)([-*+]|(\d+)\.)\s+(.+)$/);
            if (!itemMatch || Boolean(itemMatch[3]) !== ordered) {
              break;
            }

            const item = document.createElement('li');
            appendInlineMarkdown(item, itemMatch[4]);
            list.append(item);
            index++;
          }

          nodes.push(list);
          continue;
        }

        if (/^(-{3,}|\*{3,}|_{3,})\s*$/.test(trimmed)) {
          nodes.push(document.createElement('hr'));
          index++;
          continue;
        }

        const paragraphLines = [trimmed];
        index++;
        while (index < lines.length) {
          const nextLine = lines[index];
          if (!nextLine.trim() || isMarkdownBlockStart(nextLine)) {
            break;
          }

          paragraphLines.push(nextLine.trim());
          index++;
        }

        const paragraph = document.createElement('p');
        appendInlineMarkdown(paragraph, paragraphLines.join(' '));
        nodes.push(paragraph);
      }

      return nodes;
    }

    function isMarkdownBlockStart(line) {
      const trimmed = line.trim();
      return /^```([\w-]+)?\s*$/.test(line)
        || /^(#{1,3})\s+/.test(line)
        || /^>\s?/.test(line)
        || /^(\s*)([-*+]|(\d+)\.)\s+/.test(line)
        || /^(-{3,}|\*{3,}|_{3,})\s*$/.test(trimmed);
    }

    function appendInlineMarkdown(parent, text) {
      parseInlineMarkdown(String(text || '')).forEach((node) => parent.append(node));
    }

    function parseInlineMarkdown(text) {
      const nodes = [];
      let index = 0;

      while (index < text.length) {
        if (text.startsWith('**', index) || text.startsWith('__', index)) {
          const marker = text.slice(index, index + 2);
          const end = text.indexOf(marker, index + 2);
          if (end > index + 2) {
            const strong = document.createElement('strong');
            appendInlineMarkdown(strong, text.slice(index + 2, end));
            nodes.push(strong);
            index = end + 2;
            continue;
          }
        }

        const current = text[index];
        if ((current === '*' || current === '_') && text[index + 1] !== current) {
          const end = text.indexOf(current, index + 1);
          if (end > index + 1) {
            const em = document.createElement('em');
            appendInlineMarkdown(em, text.slice(index + 1, end));
            nodes.push(em);
            index = end + 1;
            continue;
          }
        }

        if (current === '`') {
          const end = text.indexOf('`', index + 1);
          if (end > index + 1) {
            const code = document.createElement('code');
            code.textContent = text.slice(index + 1, end);
            nodes.push(code);
            index = end + 1;
            continue;
          }
        }

        if (current === '[') {
          const closeBracket = text.indexOf(']', index + 1);
          const openParen = closeBracket > -1 ? text.indexOf('(', closeBracket + 1) : -1;
          const closeParen = openParen > -1 ? text.indexOf(')', openParen + 1) : -1;
          if (closeBracket > index && openParen === closeBracket + 1 && closeParen > openParen + 1) {
            const label = text.slice(index + 1, closeBracket);
            const url = text.slice(openParen + 1, closeParen).trim();
            if (isSafeMarkdownUrl(url)) {
              const anchor = document.createElement('a');
              anchor.href = url;
              anchor.target = '_blank';
              anchor.rel = 'noreferrer noopener';
              appendInlineMarkdown(anchor, label);
              nodes.push(anchor);
              index = closeParen + 1;
              continue;
            }
          }
        }

        let nextIndex = text.length;
        ['**', '__', '*', '_', '`', '['].forEach((marker) => {
          const markerIndex = text.indexOf(marker, index + 1);
          if (markerIndex !== -1) {
            nextIndex = Math.min(nextIndex, markerIndex);
          }
        });

        const chunk = text.slice(index, nextIndex);
        nodes.push(document.createTextNode(chunk));
        index += chunk.length;
      }

      return nodes;
    }

    function isSafeMarkdownUrl(url) {
      if (!url) {
        return false;
      }

      try {
        const parsed = new URL(url, window.location.origin);
        return ['http:', 'https:', 'mailto:'].includes(parsed.protocol);
      } catch {
        return url.startsWith('/') || url.startsWith('#');
      }
    }

    function sanitizeToken(token) {
      return String(token || '').replace(/[^a-zA-Z0-9_-]/g, '').toLowerCase();
    }

    function renderCitations(citations) {
      if (!citationList) {
        return;
      }

      citationList.replaceChildren();

      if (!Array.isArray(citations) || citations.length === 0) {
        const empty = document.createElement('p');
        empty.className = 'student-chat-empty compact';
        empty.dataset.studentChatEmptyCitations = '';
        empty.textContent = 'No resources loaded.';
        citationList.append(empty);
      } else {
        citations.forEach((citation) => citationList.append(createCitationCard(citation)));
      }

      if (sourceCount) {
        const count = Array.isArray(citations) ? citations.length : 0;
        sourceCount.textContent = `${count} ${count === 1 ? 'Source' : 'Sources'}`;
      }
    }

    function renderMessageCitations(container, citations) {
      if (!container) {
        return;
      }

      container.replaceChildren();

      if (!Array.isArray(citations) || citations.length === 0) {
        container.hidden = true;
        return;
      }

      citations.forEach((citation) => container.append(createCitationChip(citation)));
      container.hidden = false;
    }

    function createCitationChip(citation) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'student-chat-citation-chip';
      button.dataset.citationCard = '';

      const documentTitle = getValue(citation, 'documentTitle') || 'Untitled document';
      const subjectCode = getValue(citation, 'subjectCode') || 'Subject';
      const chapterTitle = getValue(citation, 'chapterTitle') || 'Chapter';
      const chunkIndex = getValue(citation, 'chunkIndex') ?? '';
      const excerptText = getValue(citation, 'excerpt') || '';
      const rawScore = Number(getValue(citation, 'score') || 0);

      button.textContent = documentTitle;
      button.dataset.title = documentTitle;
      button.dataset.subject = subjectCode;
      button.dataset.chapter = chapterTitle;
      button.dataset.chunk = String(chunkIndex);
      button.dataset.score = `${Math.round(rawScore * 100)}% match`;
      button.dataset.excerpt = excerptText;

      return button;
    }

    function createCitationCard(citation) {
      const article = document.createElement('article');
      article.className = 'student-chat-resource';
      article.setAttribute('role', 'button');
      article.tabIndex = 0;
      article.dataset.citationCard = '';

      const head = document.createElement('div');
      head.className = 'student-chat-resource-head';

      const status = document.createElement('span');
      status.className = 'student-chat-resource-status';
      status.setAttribute('aria-hidden', 'true');

      const title = document.createElement('strong');
      const documentTitle = getValue(citation, 'documentTitle') || 'Untitled document';
      title.textContent = documentTitle;
      head.append(status, title);

      const meta = document.createElement('span');
      meta.className = 'student-chat-resource-meta';
      const subjectCode = getValue(citation, 'subjectCode') || 'Subject';
      const chapterTitle = getValue(citation, 'chapterTitle') || 'Chapter';
      const chunkIndex = getValue(citation, 'chunkIndex') ?? '';
      meta.textContent = `${subjectCode} - ${chapterTitle} - Chunk ${chunkIndex}`;

      const excerpt = document.createElement('p');
      excerpt.className = 'student-chat-resource-excerpt';
      const excerptText = getValue(citation, 'excerpt') || '';
      excerpt.textContent = excerptText;

      const score = document.createElement('span');
      score.className = 'student-chat-resource-score';
      const rawScore = Number(getValue(citation, 'score') || 0);
      score.textContent = `${Math.round(rawScore * 100)}% match`;

      article.dataset.title = documentTitle;
      article.dataset.subject = subjectCode;
      article.dataset.chapter = chapterTitle;
      article.dataset.chunk = String(chunkIndex);
      article.dataset.score = score.textContent;
      article.dataset.excerpt = excerptText;

      article.append(head, meta, excerpt, score);
      return article;
    }

    function openCitationModal(card) {
      if (!citationModal) {
        return;
      }

      lastFocusedCitation = card;
      citationTitle.textContent = card.dataset.title || 'Untitled document';
      citationMeta.textContent = `${card.dataset.subject || 'Subject'} - ${card.dataset.chapter || 'Chapter'} - Chunk ${card.dataset.chunk || ''}`;
      citationScore.textContent = card.dataset.score || '';
      citationExcerpt.textContent = card.dataset.excerpt || '';
      citationModal.hidden = false;
      document.body.classList.add('student-chat-modal-open');
      citationModal.querySelector('[data-citation-close]')?.focus();
    }

    function closeCitationModal() {
      if (!citationModal) {
        return;
      }

      citationModal.hidden = true;
      document.body.classList.remove('student-chat-modal-open');
      lastFocusedCitation?.focus?.();
    }

    function setSubmitting(isSubmitting) {
      if (submitButton) {
        submitButton.disabled = isSubmitting || questionInput.value.trim().length === 0;
      }

      questionInput.disabled = isSubmitting;
      subjectSelect.disabled = isSubmitting;
    }

    function updateSendButtonState() {
      if (!submitButton || questionInput.disabled) {
        return;
      }

      submitButton.disabled = questionInput.value.trim().length === 0;
    }

    function renderValidation(errors) {
      if (!validation) {
        return;
      }

      validation.replaceChildren();
      const list = document.createElement('ul');
      errors.forEach((error) => {
        const item = document.createElement('li');
        item.textContent = error;
        list.append(item);
      });
      validation.append(list);
    }

    function clearValidation() {
      if (validation) {
        validation.replaceChildren();
      }
    }

    function removeEmptyState() {
      thread.querySelector('[data-student-chat-empty]')?.remove();
    }

    function getValue(source, camelName) {
      if (!source || typeof source !== 'object') {
        return undefined;
      }

      const pascalName = camelName.charAt(0).toUpperCase() + camelName.slice(1);
      return source[camelName] ?? source[pascalName];
    }

    function nowMs() {
      return window.performance?.now?.() ?? Date.now();
    }
  });
}());
