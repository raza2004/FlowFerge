from app.rag.chunking import chunk_pages
from app.rag.extract import extract_pages


def test_short_page_is_one_chunk():
    chunks = chunk_pages(["one two three"])
    assert len(chunks) == 1
    assert chunks[0].page == 1


def test_long_page_splits_with_overlap_and_keeps_page_numbers():
    words = [f"w{i}" for i in range(1000)]
    chunks = chunk_pages(["", " ".join(words)], chunk_tokens=100, overlap_tokens=20)

    assert len(chunks) > 5
    assert all(c.page == 2 for c in chunks)  # page 1 was empty
    # consecutive chunks share words (the overlap) so a sentence on the boundary is not lost
    first, second = chunks[0].content.split(), chunks[1].content.split()
    assert set(first[-10:]) & set(second[:20])
    # every word is covered
    assert {w for c in chunks for w in c.content.split()} == set(words)


def test_indexes_are_sequential():
    chunks = chunk_pages(["a " * 400, "b " * 400], chunk_tokens=50, overlap_tokens=10)
    assert [c.index for c in chunks] == list(range(len(chunks)))


def test_text_pages_use_marker():
    pages = extract_pages(b"first page\n---page---\nsecond page", "notes.md")
    assert pages == ["first page", "second page"]


def test_unsupported_type_is_rejected():
    try:
        extract_pages(b"x", "photo.png")
    except ValueError as ex:
        assert "Unsupported" in str(ex)
    else:
        raise AssertionError("expected ValueError")
