"""Split document pages into overlapping chunks.

Why ~350 tokens with ~50 overlap: a chunk has to be big enough to hold a complete idea (so the
model has context) and small enough that its embedding stays about one topic (so search is
precise). The overlap stops a sentence that straddles a boundary from being cut in half.

Tokens are approximated by whitespace separated words (1 word is about 1.3 tokens for English),
which avoids downloading a tokenizer at runtime. Page numbers are kept because citations need them.
"""

from dataclasses import dataclass

WORDS_PER_TOKEN = 0.75


@dataclass(frozen=True)
class Chunk:
    page: int
    index: int
    content: str


def chunk_pages(pages: list[str], chunk_tokens: int = 350, overlap_tokens: int = 50) -> list[Chunk]:
    size = max(int(chunk_tokens * WORDS_PER_TOKEN), 20)
    overlap = min(int(overlap_tokens * WORDS_PER_TOKEN), size // 2)
    step = size - overlap

    chunks: list[Chunk] = []
    for page_number, text in enumerate(pages, start=1):
        words = text.split()
        if not words:
            continue
        start = 0
        while start < len(words):
            piece = words[start : start + size]
            chunks.append(Chunk(page=page_number, index=len(chunks), content=" ".join(piece)))
            if start + size >= len(words):
                break
            start += step
    return chunks
