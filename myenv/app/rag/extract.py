"""Turn an uploaded file into a list of page texts."""

import io

PAGE_MARKER = "---page---"


def extract_pages(data: bytes, file_name: str, content_type: str = "") -> list[str]:
    name = file_name.lower()
    if name.endswith(".pdf") or content_type == "application/pdf":
        from pypdf import PdfReader

        reader = PdfReader(io.BytesIO(data))
        return [(page.extract_text() or "").strip() for page in reader.pages]

    if name.endswith((".txt", ".md", ".markdown")) or content_type.startswith("text/"):
        text = data.decode("utf-8", errors="replace")
        # Plain text has no pages. A line containing only the marker starts a new page, which
        # lets the eval corpus and tests control page numbers exactly.
        return [part.strip() for part in text.split(PAGE_MARKER)]

    raise ValueError(f"Unsupported file type: {file_name}")
