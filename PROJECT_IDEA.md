## Summary

  Add a root-level markdown file named PROJECT_IDEA.md that explains the project concept: an ASP.NET Core MVC
  learning-material assistant where teachers upload documents, the system processes them into searchable knowledge,
  and students/teachers use RAG chat to ask questions over uploaded content.

  ## Key Content

  - Describe the problem: students need easier access to knowledge inside course PDFs/DOCX files.
  - Describe the solution: document upload, parsing, chunking, embedding, vector search, and AI chat with citations.
  - Explain target users:
      - Student asks questions and reviews document-backed answers.

  - Summarize major features: authentication, role-based access, document library, upload pipeline, background
    processing, Qdrant retrieval, Gemini/OpenRouter chat, chat history, and document viewing/download.

  - Include a simple Mermaid flow showing: Teacher Upload -> Parse/Chunk -> Embed -> Qdrant -> Student Chat -> Answer
    with Citations.
